using System;
using System.Collections.Generic;

using UnityEngine;

namespace BackpackDefense
{
    /// <summary>
    /// Provides adjustable starting economy, upgrade, and shop probabilities.
    /// </summary>
    [Serializable]
    public sealed class EconomySettings
    {
        [SerializeField, Min(0)] private int _startingGold = 10;
        [SerializeField, Min(1)] private int _startingLevel = 3;
        [SerializeField, Min(1)] private int[] _levelCosts = { 6, 10, 14 };
        [SerializeField, Min(0)] private int _rerollCost = 1;
        [SerializeField, Min(0)] private int _oneCostWeight = 50;
        [SerializeField, Min(0)] private int _twoCostWeight = 35;
        [SerializeField, Min(0)] private int _threeCostWeight = 15;
        [SerializeField, Min(0f)] private float _feedAttackStep = 0.30f;

        public int StartingGold => _startingGold;
        public int StartingLevel => _startingLevel;
        public int MaxLevel => _startingLevel + _levelCosts.Length;
        public int RerollCost => _rerollCost;
        public int OneCostWeight => _oneCostWeight;
        public int TwoCostWeight => _twoCostWeight;
        public int ThreeCostWeight => _threeCostWeight;
        public float FeedAttackStep => _feedAttackStep;

        /// <summary>
        /// Returns the configured cost of the next level, or zero at maximum.
        /// </summary>
        public int CostForLevel(int level)
        {
            int index = level - _startingLevel;
            return index >= 0 && index < _levelCosts.Length ? _levelCosts[index] : 0;
        }
    }

    /// <summary>
    /// Stores one purchased tower and its persistent upgrade and placement state.
    /// </summary>
    public sealed class TowerUnit
    {
        private int _feedCount;
        private int _investedGold;
        private PlacedTower _placement;
        private readonly float _feedAttackStep;

        public int TypeIndex { get; }
        public int FeedCount => _feedCount;
        public int InvestedGold => _investedGold;
        public int Star => _feedCount < 2 ? 1 : _feedCount < 5 ? 2 : 3;
        public float DamageMultiplier => 1f + _feedAttackStep * _feedCount;
        public float NextDamageMultiplier => 1f + _feedAttackStep * Mathf.Min(_feedCount + 1, 5);
        public PlacedTower Placement => _placement;

        /// <summary>
        /// Creates a pristine tower of the given catalog type and purchase price.
        /// </summary>
        internal TowerUnit(int typeIndex, int price, float feedAttackStep)
        {
            TypeIndex = typeIndex;
            _investedGold = price;
            _feedAttackStep = feedAttackStep;
        }

        /// <summary>
        /// Records a board placement, or clears it when moved to the bench.
        /// </summary>
        internal void SetPlacement(PlacedTower placement) => _placement = placement;

        /// <summary>
        /// Consumes one eligible tower's investment and increases the feed count.
        /// </summary>
        internal void AddMaterial(TowerUnit material)
        {
            _feedCount++;
            _investedGold += material.InvestedGold;
        }
    }

    /// <summary>
    /// Owns the shop, tower inventory, paid levels, and manual upgrade transactions.
    /// </summary>
    public sealed class EconomyModel
    {
        private const int SHOP_SIZE = 4;
        private const int BENCH_SIZE = 6;
        private readonly System.Random _random;
        private readonly BoardModel _board;
        private readonly IReadOnlyList<TowerShape> _shapes;
        private readonly EconomySettings _settings;
        private readonly List<TowerUnit> _units = new List<TowerUnit>();
        private readonly int[] _offers = new int[SHOP_SIZE];
        private int _gold;
        private int _level;
        private bool _isLocked;

        public int Gold => _gold;
        public int Level => _level;
        public bool IsLocked => _isLocked;
        public IReadOnlyList<int> Offers => _offers;
        public IReadOnlyList<TowerUnit> Units => _units;
        public int LevelCost => _settings.CostForLevel(_level);
        public int RerollCost => _settings.RerollCost;

        /// <summary>
        /// Initializes a deterministic shop and associates inventory with the given board and catalog.
        /// </summary>
        public EconomyModel(BoardModel board, IReadOnlyList<TowerShape> shapes, int seed, EconomySettings settings = null)
        {
            _board = board;
            _shapes = shapes;
            _settings = settings ?? new EconomySettings();
            _gold = _settings.StartingGold;
            _level = _settings.StartingLevel;
            _random = new System.Random(seed);
            List<int> low = GetTypesAtCost(1);
            int first = _random.Next(low.Count);
            int second = (first + 1 + _random.Next(low.Count - 1)) % low.Count;
            _offers[0] = low[first];
            _offers[1] = low[second];
            _offers[2] = PickPrice(2);
            _offers[3] = PickPrice(3);
            for (int i = SHOP_SIZE - 1; i > 0; i--)
            {
                int other = _random.Next(i + 1);
                int value = _offers[i];
                _offers[i] = _offers[other];
                _offers[other] = value;
            }
        }

        /// <summary>
        /// Returns the catalog price for the given tower type.
        /// </summary>
        public int GetPrice(int typeIndex) => _shapes[typeIndex].Cost;

        /// <summary>
        /// Counts towers currently on the bench.
        /// </summary>
        public int BenchCount()
        {
            int count = 0;
            foreach (TowerUnit unit in _units)
                if (unit.Placement == null) count++;
            return count;
        }

        /// <summary>
        /// Purchases an offered tower into the bench when capacity and gold permit.
        /// </summary>
        public bool Buy(int slot)
        {
            if (slot < 0 || slot >= SHOP_SIZE || _offers[slot] < 0 || BenchCount() >= BENCH_SIZE) return false;
            int type = _offers[slot];
            int price = _shapes[type].Cost;
            if (_gold < price) return false;
            _gold -= price;
            _units.Add(new TowerUnit(type, price, _settings.FeedAttackStep));
            _offers[slot] = -1;
            return true;
        }

        /// <summary>
        /// Rerolls all offers for the configured cost unless the shop is locked.
        /// </summary>
        public bool Reroll()
        {
            if (_isLocked || _gold < _settings.RerollCost) return false;
            _gold -= _settings.RerollCost;
            RollOffers();
            return true;
        }

        /// <summary>
        /// Changes whether the whole shop is locked between waves.
        /// </summary>
        public void ToggleLock() => _isLocked = !_isLocked;

        /// <summary>
        /// Refreshes an unlocked shop at the next preparation phase.
        /// </summary>
        public void RefreshForNextWave()
        {
            if (!_isLocked) RollOffers();
        }

        /// <summary>
        /// Pays for one level and increases the deployment limit by one.
        /// </summary>
        public bool LevelUp()
        {
            if (_level >= _settings.MaxLevel || _gold < LevelCost) return false;
            _gold -= LevelCost;
            _level++;
            return true;
        }

        /// <summary>
        /// Places or moves an owned tower atomically while respecting the deployment limit.
        /// </summary>
        public bool Place(TowerUnit unit, Vector2Int anchor, int rotation)
        {
            if (!_units.Contains(unit) || (unit.Placement == null && _board.Towers.Count >= _level)) return false;
            if (!_board.TryPlace(_shapes[unit.TypeIndex], anchor, rotation, unit.Placement, out PlacedTower placement)) return false;
            unit.SetPlacement(placement);
            return true;
        }

        /// <summary>
        /// Returns a deployed tower to the bench when a slot is available.
        /// </summary>
        public bool ReturnToBench(TowerUnit unit)
        {
            if (!_units.Contains(unit) || unit.Placement == null || BenchCount() >= BENCH_SIZE) return false;
            _board.Remove(unit.Placement);
            unit.SetPlacement(null);
            return true;
        }

        /// <summary>
        /// Sells an owned tower and refunds its actual purchase and material investment.
        /// </summary>
        public bool Sell(TowerUnit unit)
        {
            if (!_units.Contains(unit)) return false;
            if (unit.Placement != null) _board.Remove(unit.Placement);
            _gold += unit.InvestedGold;
            _units.Remove(unit);
            return true;
        }

        /// <summary>
        /// Feeds one pristine matching tower to a non-maximum target and removes its placement.
        /// </summary>
        public bool Feed(TowerUnit target, TowerUnit material)
        {
            if (!CanFeed(target, material)) return false;
            if (material.Placement != null) _board.Remove(material.Placement);
            target.AddMaterial(material);
            _units.Remove(material);
            return true;
        }

        /// <summary>
        /// Checks whether two owned towers can participate in one material transfer.
        /// </summary>
        public bool CanFeed(TowerUnit target, TowerUnit material)
        {
            return target != null && material != null && target != material && _units.Contains(target)
                && _units.Contains(material) && target.TypeIndex == material.TypeIndex
                && target.FeedCount < 5 && material.FeedCount == 0;
        }

        /// <summary>
        /// Finds the inventory unit associated with the selected board placement.
        /// </summary>
        public TowerUnit FindUnit(PlacedTower placement)
        {
            foreach (TowerUnit unit in _units)
                if (unit.Placement == placement) return unit;
            return null;
        }

        /// <summary>
        /// Fills the shop using fixed cost probabilities and equal selection within each tier.
        /// </summary>
        private void RollOffers()
        {
            for (int i = 0; i < SHOP_SIZE; i++)
            {
                int one = Mathf.Max(0, _settings.OneCostWeight);
                int two = Mathf.Max(0, _settings.TwoCostWeight);
                int three = Mathf.Max(0, _settings.ThreeCostWeight);
                if (one + two + three == 0) { one = 50; two = 35; three = 15; }
                int roll = _random.Next(Mathf.Max(1, one + two + three));
                _offers[i] = PickPrice(roll < one ? 1 : roll < one + two ? 2 : 3);
            }
        }

        /// <summary>
        /// Picks a random catalog type with the requested cost.
        /// </summary>
        private int PickPrice(int price)
        {
            List<int> candidates = GetTypesAtCost(price);
            return candidates[_random.Next(candidates.Count)];
        }

        /// <summary>
        /// Finds catalog type indexes with a given purchase cost.
        /// </summary>
        private List<int> GetTypesAtCost(int price)
        {
            var candidates = new List<int>();
            for (int i = 0; i < _shapes.Count; i++)
                if (_shapes[i].Cost == price) candidates.Add(i);
            return candidates;
        }
    }
}
