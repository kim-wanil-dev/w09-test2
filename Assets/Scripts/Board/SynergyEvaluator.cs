using System;
using System.Collections.Generic;

using UnityEngine;

namespace BackpackDefense
{
    /// <summary>
    /// Holds adjustable thresholds and bonuses for the board's three synergy layers.
    /// </summary>
    [Serializable]
    public sealed class SynergySettings
    {
        [Header("Global Type Thresholds")]
        [SerializeField, Min(1)] private int _secondTierTypeCount = 2;
        [SerializeField, Min(1)] private int _thirdTierTypeCount = 3;
        [SerializeField, Min(0f)] private float _twoTypeBonus = 0.08f;
        [SerializeField, Min(0f)] private float _threeTypeBonus = 0.15f;

        [Header("Race Placement Effects")]
        [SerializeField, Min(0f)] private float _stoneAttackPerNeighbor = 0.08f;
        [SerializeField, Min(0)] private int _stoneNeighborCap = 3;
        [SerializeField, Min(1)] private int _fungalFirstClusterSize = 3;
        [SerializeField, Min(1)] private int _fungalSecondClusterSize = 5;
        [SerializeField, Min(0f)] private float _fungalFirstAttackSpeedBonus = 0.12f;
        [SerializeField, Min(0f)] private float _fungalSecondAttackSpeedBonus = 0.24f;
        [SerializeField, Min(0f)] private float _resonantRangePerNeighborType = 0.25f;
        [SerializeField, Min(0f)] private float _resonantRangeCap = 0.50f;

        [Header("Role Placement Effects")]
        [SerializeField, Min(0f)] private float _mortarBaseBlastRadius = 0.60f;
        [SerializeField, Min(0f)] private float _mortarBlastRadiusPerPathContact = 0.05f;
        [SerializeField, Min(0f)] private float _mortarBlastRadiusCap = 0.30f;
        [SerializeField, Min(0f)] private float _sniperAttackPerGap = 0.05f;
        [SerializeField, Min(0f)] private float _sniperAttackCap = 0.20f;
        [SerializeField, Min(0f)] private float _sniperRangePerGap = 0.25f;
        [SerializeField, Min(0f)] private float _sniperRangeCap = 1.00f;
        [SerializeField, Min(0f)] private float _tunerAuraAttackSpeedBonus = 0.12f;

        public int SecondTierTypeCount => _secondTierTypeCount;
        public int ThirdTierTypeCount => Mathf.Max(_secondTierTypeCount, _thirdTierTypeCount);
        public float TwoTypeBonus => _twoTypeBonus;
        public float ThreeTypeBonus => _threeTypeBonus;
        public float StoneAttackPerNeighbor => _stoneAttackPerNeighbor;
        public int StoneNeighborCap => _stoneNeighborCap;
        public int FungalFirstClusterSize => _fungalFirstClusterSize;
        public int FungalSecondClusterSize => Mathf.Max(_fungalFirstClusterSize, _fungalSecondClusterSize);
        public float FungalFirstAttackSpeedBonus => _fungalFirstAttackSpeedBonus;
        public float FungalSecondAttackSpeedBonus => _fungalSecondAttackSpeedBonus;
        public float ResonantRangePerNeighborType => _resonantRangePerNeighborType;
        public float ResonantRangeCap => _resonantRangeCap;
        public float MortarBaseBlastRadius => _mortarBaseBlastRadius;
        public float MortarBlastRadiusPerPathContact => _mortarBlastRadiusPerPathContact;
        public float MortarBlastRadiusCap => _mortarBlastRadiusCap;
        public float SniperAttackPerGap => _sniperAttackPerGap;
        public float SniperAttackCap => _sniperAttackCap;
        public float SniperRangePerGap => _sniperRangePerGap;
        public float SniperRangeCap => _sniperRangeCap;
        public float TunerAuraAttackSpeedBonus => _tunerAuraAttackSpeedBonus;
    }

    /// <summary>
    /// Stores one deployed tower's resolved bonuses, final attack values, and buff sources.
    /// </summary>
    public sealed class TowerSynergyStats
    {
        private readonly IReadOnlyList<string> _sources;

        public TowerUnit Unit { get; }
        public TowerShape Definition { get; }
        public int AdjacentTowerCount { get; }
        public int SameRaceNeighborCount { get; }
        public int RaceClusterSize { get; }
        public int ResonantNeighborTypeCount { get; }
        public int PathContactCells { get; }
        public int PathGap { get; }
        public float RaceGlobalAttackBonus { get; }
        public float JobGlobalAttackSpeedBonus { get; }
        public float RacePlacementAttackBonus { get; }
        public float RacePlacementAttackSpeedBonus { get; }
        public float RacePlacementRangeBonus { get; }
        public float JobPlacementAttackBonus { get; }
        public float JobPlacementRangeBonus { get; }
        public float TunerAuraAttackSpeedBonus { get; }
        public float Attack { get; }
        public float AttackSpeedMultiplier { get; }
        public float AttackInterval { get; }
        public float Range { get; }
        public float BlastRadius { get; }
        public IReadOnlyList<string> Sources => _sources;

        /// <summary>
        /// Creates immutable combat values and source descriptions for one deployed tower.
        /// </summary>
        internal TowerSynergyStats(TowerUnit unit, TowerShape definition, int adjacentTowerCount,
            int sameRaceNeighborCount, int raceClusterSize, int resonantNeighborTypeCount,
            int pathContactCells, int pathGap, float raceGlobalAttackBonus, float jobGlobalAttackSpeedBonus,
            float racePlacementAttackBonus, float racePlacementAttackSpeedBonus, float racePlacementRangeBonus,
            float jobPlacementAttackBonus, float jobPlacementRangeBonus, float tunerAuraAttackSpeedBonus,
            float attack, float attackSpeedMultiplier, float attackInterval, float range, float blastRadius,
            List<string> sources)
        {
            Unit = unit;
            Definition = definition;
            AdjacentTowerCount = adjacentTowerCount;
            SameRaceNeighborCount = sameRaceNeighborCount;
            RaceClusterSize = raceClusterSize;
            ResonantNeighborTypeCount = resonantNeighborTypeCount;
            PathContactCells = pathContactCells;
            PathGap = pathGap;
            RaceGlobalAttackBonus = raceGlobalAttackBonus;
            JobGlobalAttackSpeedBonus = jobGlobalAttackSpeedBonus;
            RacePlacementAttackBonus = racePlacementAttackBonus;
            RacePlacementAttackSpeedBonus = racePlacementAttackSpeedBonus;
            RacePlacementRangeBonus = racePlacementRangeBonus;
            JobPlacementAttackBonus = jobPlacementAttackBonus;
            JobPlacementRangeBonus = jobPlacementRangeBonus;
            TunerAuraAttackSpeedBonus = tunerAuraAttackSpeedBonus;
            Attack = attack;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            AttackInterval = attackInterval;
            Range = range;
            BlastRadius = blastRadius;
            _sources = sources.AsReadOnly();
        }
    }

    /// <summary>
    /// Provides resolved board-wide counts and per-tower combat values for a layout.
    /// </summary>
    public sealed class BoardSynergyEvaluation
    {
        private readonly IReadOnlyList<TowerSynergyStats> _towers;
        private readonly Dictionary<TowerUnit, TowerSynergyStats> _statsByUnit;
        private readonly int[] _raceTypeCounts;
        private readonly int[] _roleTypeCounts;
        private readonly float[] _raceGlobalAttackBonuses;
        private readonly float[] _roleGlobalAttackSpeedBonuses;

        public IReadOnlyList<TowerSynergyStats> Towers => _towers;

        /// <summary>
        /// Creates a result snapshot containing type counts and resolved tower stats.
        /// </summary>
        internal BoardSynergyEvaluation(List<TowerSynergyStats> towers,
            Dictionary<TowerUnit, TowerSynergyStats> statsByUnit, int[] raceTypeCounts,
            int[] roleTypeCounts, float[] raceGlobalAttackBonuses, float[] roleGlobalAttackSpeedBonuses)
        {
            _towers = towers.AsReadOnly();
            _statsByUnit = statsByUnit;
            _raceTypeCounts = raceTypeCounts;
            _roleTypeCounts = roleTypeCounts;
            _raceGlobalAttackBonuses = raceGlobalAttackBonuses;
            _roleGlobalAttackSpeedBonuses = roleGlobalAttackSpeedBonuses;
        }

        /// <summary>
        /// Returns how many distinct deployed definitions belong to a race.
        /// </summary>
        public int GetRaceTypeCount(TowerRace race)
        {
            int index = (int)race;
            return index >= 0 && index < _raceTypeCounts.Length ? _raceTypeCounts[index] : 0;
        }

        /// <summary>
        /// Returns how many distinct deployed definitions belong to a role.
        /// </summary>
        public int GetRoleTypeCount(TowerRole role)
        {
            int index = (int)role;
            return index >= 0 && index < _roleTypeCounts.Length ? _roleTypeCounts[index] : 0;
        }

        /// <summary>
        /// Returns the global attack bonus currently active for a race.
        /// </summary>
        public float GetRaceGlobalAttackBonus(TowerRace race)
        {
            int index = (int)race;
            return index >= 0 && index < _raceGlobalAttackBonuses.Length ? _raceGlobalAttackBonuses[index] : 0f;
        }

        /// <summary>
        /// Returns the global attack-speed bonus currently active for a role.
        /// </summary>
        public float GetRoleGlobalAttackSpeedBonus(TowerRole role)
        {
            int index = (int)role;
            return index >= 0 && index < _roleGlobalAttackSpeedBonuses.Length
                ? _roleGlobalAttackSpeedBonuses[index]
                : 0f;
        }

        /// <summary>
        /// Finds the resolved stats for a deployed tower, or returns null if it is not on the board.
        /// </summary>
        public TowerSynergyStats GetStats(TowerUnit unit)
        {
            return unit != null && _statsByUnit.TryGetValue(unit, out TowerSynergyStats stats) ? stats : null;
        }
    }

    /// <summary>
    /// Calculates global, race, adjacency, path, and tuner-aura effects for one board state.
    /// </summary>
    public static class SynergyEvaluator
    {
        private const int RACE_COUNT = 3;
        private const int ROLE_COUNT = 3;
        private const float MIN_ATTACK_INTERVAL = 0.01f;

        private static readonly Vector2Int[] DIRECTIONS =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };

        private sealed class EvaluationEntry
        {
            public TowerUnit Unit { get; }
            public TowerShape Definition { get; }
            public PlacedTower Placement { get; }
            public int FeedCount { get; }
            public List<Vector2Int> Cells { get; }
            public HashSet<EvaluationEntry> Neighbors { get; } = new HashSet<EvaluationEntry>();
            public int ClusterSize { get; set; } = 1;

            /// <summary>
            /// Stores the unit and candidate placement data used during one board evaluation.
            /// </summary>
            public EvaluationEntry(TowerUnit unit, TowerShape definition, PlacedTower placement, int feedCount)
            {
                Unit = unit;
                Definition = definition;
                Placement = placement;
                FeedCount = feedCount;
                Cells = definition.GetCells(placement.Position, placement.Rotation);
            }
        }

        /// <summary>
        /// Evaluates deployed units and an optional proposed move or feed without mutating the board.
        /// </summary>
        public static BoardSynergyEvaluation Evaluate(IReadOnlyList<TowerUnit> units,
            IReadOnlyList<TowerShape> definitions, IReadOnlyList<Vector2Int> path, SynergySettings settings,
            TowerUnit replacementUnit = null, PlacedTower proposedPlacement = null,
            TowerUnit feedTarget = null, TowerUnit consumedMaterial = null)
        {
            SynergySettings resolvedSettings = settings ?? new SynergySettings();
            List<EvaluationEntry> entries = CreateEntries(units, definitions, replacementUnit,
                proposedPlacement, feedTarget, consumedMaterial);
            int[] raceTypeCounts = new int[RACE_COUNT];
            int[] roleTypeCounts = new int[ROLE_COUNT];
            float[] raceGlobalBonuses = new float[RACE_COUNT];
            float[] roleGlobalBonuses = new float[ROLE_COUNT];
            BuildGlobalCounts(entries, resolvedSettings, raceTypeCounts, roleTypeCounts,
                raceGlobalBonuses, roleGlobalBonuses);
            BuildAdjacency(entries);
            AssignRaceClusters(entries);

            var pathCells = new HashSet<Vector2Int>();
            if (path != null)
                foreach (Vector2Int point in path)
                    pathCells.Add(point);

            var stats = new List<TowerSynergyStats>(entries.Count);
            var statsByUnit = new Dictionary<TowerUnit, TowerSynergyStats>();
            foreach (EvaluationEntry entry in entries)
            {
                TowerSynergyStats towerStats = ResolveTowerStats(entry, pathCells, raceGlobalBonuses,
                    roleGlobalBonuses, raceTypeCounts, roleTypeCounts, resolvedSettings);
                stats.Add(towerStats);
                statsByUnit[entry.Unit] = towerStats;
            }

            return new BoardSynergyEvaluation(stats, statsByUnit, raceTypeCounts, roleTypeCounts,
                raceGlobalBonuses, roleGlobalBonuses);
        }

        /// <summary>
        /// Returns a display name for a tower race.
        /// </summary>
        public static string GetRaceName(TowerRace race)
        {
            switch (race)
            {
                case TowerRace.Stone: return "Stone";
                case TowerRace.Fungal: return "Fungal";
                case TowerRace.Resonant: return "Resonant";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Returns a display name for a tower role.
        /// </summary>
        public static string GetRoleName(TowerRole role)
        {
            switch (role)
            {
                case TowerRole.Mortar: return "Mortar";
                case TowerRole.Sniper: return "Sniper";
                case TowerRole.Tuner: return "Tuner";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Creates placed entries while excluding a consumed material and substituting a proposed placement.
        /// </summary>
        private static List<EvaluationEntry> CreateEntries(IReadOnlyList<TowerUnit> units,
            IReadOnlyList<TowerShape> definitions, TowerUnit replacementUnit, PlacedTower proposedPlacement,
            TowerUnit feedTarget, TowerUnit consumedMaterial)
        {
            var entries = new List<EvaluationEntry>();
            foreach (TowerUnit unit in units)
            {
                if (unit == consumedMaterial || unit == replacementUnit || unit.Placement == null) continue;
                int feedCount = unit == feedTarget ? Mathf.Min(unit.FeedCount + 1, 5) : unit.FeedCount;
                entries.Add(new EvaluationEntry(unit, definitions[unit.TypeIndex], unit.Placement, feedCount));
            }

            if (replacementUnit != null && proposedPlacement != null)
                entries.Add(new EvaluationEntry(replacementUnit, definitions[replacementUnit.TypeIndex],
                    proposedPlacement, replacementUnit == feedTarget ? Mathf.Min(replacementUnit.FeedCount + 1, 5) : replacementUnit.FeedCount));

            return entries;
        }

        /// <summary>
        /// Counts distinct deployed definitions by race and role, then resolves their active tiers.
        /// </summary>
        private static void BuildGlobalCounts(List<EvaluationEntry> entries, SynergySettings settings,
            int[] raceTypeCounts, int[] roleTypeCounts, float[] raceGlobalBonuses, float[] roleGlobalBonuses)
        {
            var raceTypes = new HashSet<string>[RACE_COUNT];
            var roleTypes = new HashSet<string>[ROLE_COUNT];
            for (int i = 0; i < RACE_COUNT; i++) raceTypes[i] = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ROLE_COUNT; i++) roleTypes[i] = new HashSet<string>(StringComparer.Ordinal);

            foreach (EvaluationEntry entry in entries)
            {
                string key = string.IsNullOrEmpty(entry.Definition.DefinitionId)
                    ? entry.Unit.TypeIndex.ToString()
                    : entry.Definition.DefinitionId;
                raceTypes[(int)entry.Definition.Race].Add(key);
                roleTypes[(int)entry.Definition.Role].Add(key);
            }

            for (int i = 0; i < RACE_COUNT; i++)
            {
                raceTypeCounts[i] = raceTypes[i].Count;
                raceGlobalBonuses[i] = GetTierBonus(raceTypeCounts[i], settings);
            }
            for (int i = 0; i < ROLE_COUNT; i++)
            {
                roleTypeCounts[i] = roleTypes[i].Count;
                roleGlobalBonuses[i] = GetTierBonus(roleTypeCounts[i], settings);
            }
        }

        /// <summary>
        /// Returns the higher active global tier without adding lower-tier bonuses.
        /// </summary>
        private static float GetTierBonus(int count, SynergySettings settings)
        {
            if (count >= settings.ThirdTierTypeCount) return settings.ThreeTypeBonus;
            if (count >= settings.SecondTierTypeCount) return settings.TwoTypeBonus;
            return 0f;
        }

        /// <summary>
        /// Builds unique four-direction adjacency relationships from actual occupied footprint cells.
        /// </summary>
        private static void BuildAdjacency(List<EvaluationEntry> entries)
        {
            var occupancy = new Dictionary<Vector2Int, EvaluationEntry>();
            foreach (EvaluationEntry entry in entries)
                foreach (Vector2Int cell in entry.Cells)
                    occupancy[cell] = entry;

            foreach (EvaluationEntry entry in entries)
                foreach (Vector2Int cell in entry.Cells)
                    foreach (Vector2Int direction in DIRECTIONS)
                        if (occupancy.TryGetValue(cell + direction, out EvaluationEntry neighbor) && neighbor != entry)
                            entry.Neighbors.Add(neighbor);
        }

        /// <summary>
        /// Assigns each tower the size of its same-race connected adjacency component.
        /// </summary>
        private static void AssignRaceClusters(List<EvaluationEntry> entries)
        {
            var visited = new HashSet<EvaluationEntry>();
            foreach (EvaluationEntry entry in entries)
            {
                if (!visited.Add(entry)) continue;
                var cluster = new List<EvaluationEntry>();
                var queue = new Queue<EvaluationEntry>();
                queue.Enqueue(entry);
                while (queue.Count > 0)
                {
                    EvaluationEntry current = queue.Dequeue();
                    cluster.Add(current);
                    foreach (EvaluationEntry neighbor in current.Neighbors)
                        if (neighbor.Definition.Race == entry.Definition.Race && visited.Add(neighbor))
                            queue.Enqueue(neighbor);
                }

                foreach (EvaluationEntry member in cluster)
                    member.ClusterSize = cluster.Count;
            }
        }

        /// <summary>
        /// Resolves race and role bonuses, path metrics, final stats, and readable sources for a tower.
        /// </summary>
        private static TowerSynergyStats ResolveTowerStats(EvaluationEntry entry, HashSet<Vector2Int> pathCells,
            float[] raceGlobalBonuses, float[] roleGlobalBonuses, int[] raceTypeCounts, int[] roleTypeCounts,
            SynergySettings settings)
        {
            int sameRaceNeighbors = 0;
            int resonantNeighborTypes = 0;
            int tunerNeighbors = 0;
            var resonantTypes = new HashSet<string>(StringComparer.Ordinal);
            var sameRaceNames = new List<string>();
            var tunerNames = new List<string>();
            foreach (EvaluationEntry neighbor in entry.Neighbors)
            {
                if (neighbor.Definition.Race == entry.Definition.Race)
                {
                    sameRaceNeighbors++;
                    sameRaceNames.Add(neighbor.Definition.Name);
                }
                if (entry.Definition.Race == TowerRace.Resonant && neighbor.Definition.Race != TowerRace.Resonant)
                    resonantTypes.Add(neighbor.Definition.DefinitionId);
                if (neighbor.Definition.Role == TowerRole.Tuner)
                {
                    tunerNeighbors++;
                    tunerNames.Add(neighbor.Definition.Name);
                }
            }
            resonantNeighborTypes = resonantTypes.Count;

            GetPathMetrics(entry.Cells, pathCells, out int pathContactCells, out int pathDistance);
            int pathGap = Mathf.Max(0, pathDistance - 1);
            int raceTypeCount = raceTypeCounts[(int)entry.Definition.Race];
            int roleTypeCount = roleTypeCounts[(int)entry.Definition.Role];
            float raceGlobalAttackBonus = raceGlobalBonuses[(int)entry.Definition.Race];
            float roleGlobalAttackSpeedBonus = roleGlobalBonuses[(int)entry.Definition.Role];
            float racePlacementAttackBonus = 0f;
            float racePlacementAttackSpeedBonus = 0f;
            float racePlacementRangeBonus = 0f;
            float jobPlacementAttackBonus = 0f;
            float jobPlacementRangeBonus = 0f;
            float tunerAuraBonus = tunerNeighbors > 0 ? settings.TunerAuraAttackSpeedBonus : 0f;

            var sources = new List<string>();
            if (entry.FeedCount > 0)
                sources.Add("Material feeds: x" + entry.Unit.DamageMultiplierAt(entry.FeedCount).ToString("0.00") + " attack");
            if (raceGlobalAttackBonus > 0f)
                sources.Add(GetRaceName(entry.Definition.Race) + " global (" + raceTypeCount + " types): +" +
                    FormatPercent(raceGlobalAttackBonus) + " attack");
            if (roleGlobalAttackSpeedBonus > 0f)
                sources.Add(GetRoleName(entry.Definition.Role) + " global (" + roleTypeCount + " types): +" +
                    FormatPercent(roleGlobalAttackSpeedBonus) + " attack speed");

            if (entry.Definition.Race == TowerRace.Stone)
            {
                int effectiveNeighbors = Mathf.Min(sameRaceNeighbors, settings.StoneNeighborCap);
                racePlacementAttackBonus = effectiveNeighbors * settings.StoneAttackPerNeighbor;
                if (racePlacementAttackBonus > 0f)
                    sources.Add("Stone adjacency (" + JoinNames(sameRaceNames) + "): +" +
                        FormatPercent(racePlacementAttackBonus) + " attack");
            }
            else if (entry.Definition.Race == TowerRace.Fungal)
            {
                if (entry.ClusterSize >= settings.FungalSecondClusterSize)
                    racePlacementAttackSpeedBonus = settings.FungalSecondAttackSpeedBonus;
                else if (entry.ClusterSize >= settings.FungalFirstClusterSize)
                    racePlacementAttackSpeedBonus = settings.FungalFirstAttackSpeedBonus;
                if (racePlacementAttackSpeedBonus > 0f)
                    sources.Add("Fungal cluster (" + entry.ClusterSize + " towers): +" +
                        FormatPercent(racePlacementAttackSpeedBonus) + " attack speed");
            }
            else if (entry.Definition.Race == TowerRace.Resonant)
            {
                racePlacementRangeBonus = Mathf.Min(resonantNeighborTypes * settings.ResonantRangePerNeighborType,
                    settings.ResonantRangeCap);
                if (racePlacementRangeBonus > 0f)
                    sources.Add("Resonant adjacent race types (" + resonantNeighborTypes + "): +" +
                        racePlacementRangeBonus.ToString("0.00") + " range");
            }

            if (entry.Definition.Role == TowerRole.Mortar)
            {
                float blastRadiusBonus = Mathf.Min(pathContactCells * settings.MortarBlastRadiusPerPathContact,
                    settings.MortarBlastRadiusCap);
                sources.Add("Path contact C=" + pathContactCells + ": blast radius " +
                    (settings.MortarBaseBlastRadius + blastRadiusBonus).ToString("0.00"));
            }
            else if (entry.Definition.Role == TowerRole.Sniper)
            {
                jobPlacementAttackBonus = Mathf.Min(pathGap * settings.SniperAttackPerGap, settings.SniperAttackCap);
                jobPlacementRangeBonus = Mathf.Min(pathGap * settings.SniperRangePerGap, settings.SniperRangeCap);
                if (jobPlacementAttackBonus > 0f || jobPlacementRangeBonus > 0f)
                    sources.Add("Sniper path gap D=" + pathGap + ": +" + FormatPercent(jobPlacementAttackBonus) +
                        " attack, +" + jobPlacementRangeBonus.ToString("0.00") + " range");
            }
            else if (entry.Definition.Role == TowerRole.Tuner)
            {
                sources.Add("Tuner aura: +" + FormatPercent(settings.TunerAuraAttackSpeedBonus) +
                    " attack speed to adjacent allies");
            }

            if (tunerAuraBonus > 0f)
                sources.Add("Tuner aura from (" + JoinNames(tunerNames) + "): +" +
                    FormatPercent(tunerAuraBonus) + " attack speed; duplicate auras do not stack");
            if (sources.Count == 0) sources.Add("No active bonuses");

            float raceAttackBonus = raceGlobalAttackBonus + racePlacementAttackBonus;
            float totalAttackBonus = raceAttackBonus + jobPlacementAttackBonus;
            float attackSpeedMultiplier = 1f + roleGlobalAttackSpeedBonus +
                racePlacementAttackSpeedBonus + tunerAuraBonus;
            float attack = entry.Definition.BaseAttack * entry.Unit.DamageMultiplierAt(entry.FeedCount) *
                (1f + totalAttackBonus);
            float attackInterval = entry.Definition.BaseAttackInterval /
                Mathf.Max(MIN_ATTACK_INTERVAL, attackSpeedMultiplier);
            float range = entry.Definition.BaseRange + racePlacementRangeBonus + jobPlacementRangeBonus;
            float blastRadius = entry.Definition.Role == TowerRole.Mortar
                ? settings.MortarBaseBlastRadius + Mathf.Min(pathContactCells * settings.MortarBlastRadiusPerPathContact,
                    settings.MortarBlastRadiusCap)
                : 0f;

            return new TowerSynergyStats(entry.Unit, entry.Definition, entry.Neighbors.Count, sameRaceNeighbors,
                entry.ClusterSize, resonantNeighborTypes, pathContactCells, pathGap, raceGlobalAttackBonus,
                roleGlobalAttackSpeedBonus, racePlacementAttackBonus, racePlacementAttackSpeedBonus,
                racePlacementRangeBonus, jobPlacementAttackBonus, jobPlacementRangeBonus, tunerAuraBonus,
                attack, attackSpeedMultiplier, attackInterval, range, blastRadius, sources);
        }

        /// <summary>
        /// Counts distinct path-adjacent footprint cells and finds the nearest footprint-to-path distance.
        /// </summary>
        private static void GetPathMetrics(List<Vector2Int> cells, HashSet<Vector2Int> pathCells,
            out int pathContactCells, out int pathDistance)
        {
            pathContactCells = 0;
            pathDistance = int.MaxValue;
            if (pathCells.Count == 0)
            {
                pathDistance = 0;
                return;
            }

            foreach (Vector2Int cell in cells)
            {
                bool touchesPath = false;
                foreach (Vector2Int direction in DIRECTIONS)
                    if (pathCells.Contains(cell + direction))
                    {
                        touchesPath = true;
                        break;
                    }
                if (touchesPath) pathContactCells++;

                foreach (Vector2Int pathCell in pathCells)
                    pathDistance = Mathf.Min(pathDistance, Mathf.Abs(cell.x - pathCell.x) + Mathf.Abs(cell.y - pathCell.y));
            }
        }

        /// <summary>
        /// Joins source tower names in a stable order for readable buff details.
        /// </summary>
        private static string JoinNames(List<string> names)
        {
            names.Sort(StringComparer.Ordinal);
            return string.Join(", ", names);
        }

        /// <summary>
        /// Formats a fractional bonus as a whole-number percentage for the source list.
        /// </summary>
        private static string FormatPercent(float bonus)
        {
            return Mathf.RoundToInt(bonus * 100f) + "%";
        }
    }
}
