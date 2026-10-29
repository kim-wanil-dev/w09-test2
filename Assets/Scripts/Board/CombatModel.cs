using System;
using System.Collections.Generic;

using UnityEngine;

namespace BackpackDefense
{
    /// <summary>
    /// Identifies the current run phase and whether the run has ended.
    /// </summary>
    public enum CombatPhase
    {
        Preparation,
        Combat,
        WaveResult,
        Victory,
        Defeat
    }

    /// <summary>
    /// Identifies one of the four enemy movement and leak profiles.
    /// </summary>
    public enum EnemyKind
    {
        Normal,
        Fast,
        Tank,
        Boss
    }

    /// <summary>
    /// Identifies why the run ended in defeat.
    /// </summary>
    public enum DefeatReason
    {
        None,
        BaseDestroyed,
        BossReachedGoal
    }

    /// <summary>
    /// Configures one ordered group of enemies in a wave.
    /// </summary>
    [Serializable]
    public sealed class SpawnBatchDefinition
    {
        [SerializeField] private EnemyKind _kind;
        [SerializeField, Min(0f)] private float _startTime;
        [SerializeField, Min(0f)] private float _spawnInterval;
        [SerializeField, Min(1)] private int _count = 1;

        public EnemyKind Kind => _kind;
        public float StartTime => _startTime;
        public float SpawnInterval => _spawnInterval;
        public int Count => Mathf.Max(1, _count);

        /// <summary>
        /// Creates one scheduled group with a first spawn time, interval, and count.
        /// </summary>
        public SpawnBatchDefinition(EnemyKind kind, float startTime, float spawnInterval, int count)
        {
            _kind = kind;
            _startTime = Mathf.Max(0f, startTime);
            _spawnInterval = Mathf.Max(0f, spawnInterval);
            _count = Mathf.Max(1, count);
        }
    }

    /// <summary>
    /// Configures the ordered enemy batches for one wave.
    /// </summary>
    [Serializable]
    public sealed class WaveDefinition
    {
        [SerializeField, Min(1)] private int _waveNumber = 1;
        [SerializeField] private List<SpawnBatchDefinition> _batches = new List<SpawnBatchDefinition>();

        public int WaveNumber => Mathf.Max(1, _waveNumber);
        public IReadOnlyList<SpawnBatchDefinition> Batches => _batches;

        /// <summary>
        /// Creates a wave definition with its ordered spawn batches.
        /// </summary>
        public WaveDefinition(int waveNumber, List<SpawnBatchDefinition> batches)
        {
            _waveNumber = Mathf.Max(1, waveNumber);
            _batches = batches ?? new List<SpawnBatchDefinition>();
        }
    }

    /// <summary>
    /// Holds adjustable simulation settings and the eight prototype wave schedules.
    /// </summary>
    [Serializable]
    public sealed class CombatSettings
    {
        [SerializeField, Min(1)] private int _startingBaseHealth = 10;
        [SerializeField, Min(1f)] private float _simulationStepsPerSecond = 30f;
        [SerializeField, Min(0f)] private float _mortarTravelTime = 0.25f;
        [SerializeField, Min(0)] private int _waveClearGold = 7;
        [SerializeField, Min(1)] private int _startingWave = 1;
        [SerializeField, Min(1)] private int _lastWave = 8;
        [SerializeField, Min(1f)] private float _enemyHealthGrowth = 1.18f;
        [SerializeField] private List<WaveDefinition> _waves = CombatCatalog.CreateDefaultWaves();

        public int StartingBaseHealth => _startingBaseHealth;
        public float FixedStepSeconds => 1f / Mathf.Max(1f, _simulationStepsPerSecond);
        public float MortarTravelTime => _mortarTravelTime;
        public int WaveClearGold => _waveClearGold;
        public int StartingWave => Mathf.Clamp(_startingWave, 1, WaveCount);
        public int LastWave => Mathf.Clamp(_lastWave, StartingWave, WaveCount);
        public int WaveCount => _waves == null ? 0 : _waves.Count;
        public IReadOnlyList<WaveDefinition> Waves => _waves;
        public float EnemyHealthGrowth => Mathf.Max(1f, _enemyHealthGrowth);

        /// <summary>
        /// Returns the configured wave at a one-based wave number or null when absent.
        /// </summary>
        public WaveDefinition GetWave(int waveNumber)
        {
            int index = waveNumber - 1;
            return _waves != null && index >= 0 && index < _waves.Count ? _waves[index] : null;
        }
    }

    /// <summary>
    /// Provides the prototype's deterministic eight-wave schedule.
    /// </summary>
    public static class CombatCatalog
    {
        /// <summary>
        /// Creates the eight spawn schedules defined for the prototype.
        /// </summary>
        public static List<WaveDefinition> CreateDefaultWaves()
        {
            return new List<WaveDefinition>
            {
                Wave(1, Batch(EnemyKind.Normal, 0f, 1.0f, 8)),
                Wave(2, Batch(EnemyKind.Normal, 0f, 0.9f, 10), Batch(EnemyKind.Fast, 5f, 0.7f, 4)),
                Wave(3, Batch(EnemyKind.Tank, 0f, 1.8f, 3), Batch(EnemyKind.Normal, 2f, 0.8f, 10)),
                Wave(4, Batch(EnemyKind.Fast, 0f, 0.6f, 12), Batch(EnemyKind.Normal, 3f, 0.8f, 8)),
                Wave(5, Batch(EnemyKind.Tank, 0f, 1.5f, 5), Batch(EnemyKind.Normal, 1f, 0.7f, 12)),
                Wave(6, Batch(EnemyKind.Fast, 0f, 0.55f, 12), Batch(EnemyKind.Tank, 3f, 1.3f, 6)),
                Wave(7, Batch(EnemyKind.Tank, 0f, 1.2f, 8), Batch(EnemyKind.Fast, 2f, 0.5f, 16)),
                Wave(8, Batch(EnemyKind.Boss, 0f, 0f, 1), Batch(EnemyKind.Normal, 2f, 0.7f, 12),
                    Batch(EnemyKind.Fast, 5f, 0.6f, 8), Batch(EnemyKind.Tank, 8f, 1.5f, 4))
            };
        }

        /// <summary>
        /// Creates one wave definition from its ordered batch definitions.
        /// </summary>
        private static WaveDefinition Wave(int number, params SpawnBatchDefinition[] batches)
        {
            return new WaveDefinition(number, new List<SpawnBatchDefinition>(batches));
        }

        /// <summary>
        /// Creates one compact spawn batch definition.
        /// </summary>
        private static SpawnBatchDefinition Batch(EnemyKind kind, float start, float interval, int count)
        {
            return new SpawnBatchDefinition(kind, start, interval, count);
        }
    }

    /// <summary>
    /// Stores immutable path and resolved tower values captured when a wave begins.
    /// </summary>
    public sealed class WaveSnapshot
    {
        private readonly IReadOnlyList<Vector2Int> _path;
        private readonly IReadOnlyList<TowerCombatSnapshot> _towers;

        public int WaveNumber { get; }
        public int Seed { get; }
        public int PathLength => Mathf.Max(0, _path.Count - 1);
        public string SynergySummary { get; }
        public IReadOnlyList<Vector2Int> Path => _path;
        public IReadOnlyList<TowerCombatSnapshot> Towers => _towers;

        /// <summary>
        /// Copies the active path and tower stats into a read-only wave snapshot.
        /// </summary>
        internal WaveSnapshot(int waveNumber, int seed, IReadOnlyList<Vector2Int> path,
            BoardSynergyEvaluation evaluation)
        {
            WaveNumber = waveNumber;
            Seed = seed;
            var pathCopy = new List<Vector2Int>(path);
            var towerCopy = new List<TowerCombatSnapshot>();
            foreach (TowerSynergyStats stats in evaluation.Towers)
                towerCopy.Add(new TowerCombatSnapshot(towerCopy.Count, stats));
            _path = pathCopy.AsReadOnly();
            _towers = towerCopy.AsReadOnly();
            SynergySummary = BuildSynergySummary(evaluation);
        }

        /// <summary>
        /// Captures the active race and role thresholds in a compact wave report string.
        /// </summary>
        private static string BuildSynergySummary(BoardSynergyEvaluation evaluation)
        {
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                TowerRace race = (TowerRace)i;
                if (i > 0) summary.Append(" | ");
                summary.Append(SynergyEvaluator.GetRaceName(race)).Append(' ')
                    .Append(evaluation.GetRaceTypeCount(race));
            }
            summary.Append(" / ");
            for (int i = 0; i < 3; i++)
            {
                TowerRole role = (TowerRole)i;
                if (i > 0) summary.Append(" | ");
                summary.Append(SynergyEvaluator.GetRoleName(role)).Append(' ')
                    .Append(evaluation.GetRoleTypeCount(role));
            }
            return summary.ToString();
        }
    }

    /// <summary>
    /// Stores one tower's frozen combat stats and synergy sources for a wave.
    /// </summary>
    public sealed class TowerCombatSnapshot
    {
        private readonly IReadOnlyList<string> _sources;

        public int Index { get; }
        public string Name { get; }
        public TowerRole Role { get; }
        public Vector2Int Anchor { get; }
        public float Attack { get; }
        public float AttackInterval { get; }
        public float Range { get; }
        public float BlastRadius { get; }
        public IReadOnlyList<string> Sources => _sources;

        /// <summary>
        /// Copies one resolved tower's combat numbers, placement, and source descriptions.
        /// </summary>
        internal TowerCombatSnapshot(int index, TowerSynergyStats stats)
        {
            Index = index;
            Name = stats.Definition.Name;
            Role = stats.Definition.Role;
            Anchor = stats.Unit.Placement.Position;
            Attack = stats.Attack;
            AttackInterval = stats.AttackInterval;
            Range = stats.Range;
            BlastRadius = stats.BlastRadius;
            _sources = new List<string>(stats.Sources).AsReadOnly();
        }
    }

    /// <summary>
    /// Represents one enemy moving along the snapshotted path.
    /// </summary>
    public sealed class EnemyInstance
    {
        private float _pathDistance;

        public int Sequence { get; }
        public EnemyKind Kind { get; }
        public float Health { get; private set; }
        public float MaxHealth { get; }
        public float Speed { get; }
        public int LeakDamage { get; }
        public Vector2 Position { get; private set; }
        public bool HasArrived { get; private set; }
        public bool IsAlive => Health > 0f;
        public float RemainingPathDistance { get; private set; }

        /// <summary>
        /// Creates an enemy instance with scaled health and its movement/leak values.
        /// </summary>
        internal EnemyInstance(int sequence, EnemyKind kind, float health, float speed, int leakDamage,
            Vector2Int spawn, int pathLength)
        {
            Sequence = sequence;
            Kind = kind;
            Health = health;
            MaxHealth = health;
            Speed = speed;
            LeakDamage = leakDamage;
            Position = spawn;
            RemainingPathDistance = Mathf.Max(0, pathLength - 1);
        }

        /// <summary>
        /// Advances an alive enemy along the immutable wave path.
        /// </summary>
        internal void Move(float deltaDistance, IReadOnlyList<Vector2Int> path)
        {
            if (!IsAlive || HasArrived || path.Count == 0) return;
            _pathDistance = Mathf.Min(path.Count - 1, _pathDistance + deltaDistance);
            int segment = Mathf.Min(Mathf.FloorToInt(_pathDistance), path.Count - 1);
            float fraction = _pathDistance - segment;
            Vector2 point = path[segment];
            if (segment + 1 < path.Count) point = Vector2.Lerp(point, path[segment + 1], fraction);
            Position = point;
            RemainingPathDistance = Mathf.Max(0f, path.Count - 1 - _pathDistance);
            HasArrived = _pathDistance >= path.Count - 1;
        }

        /// <summary>
        /// Applies non-negative damage and returns the amount that reduced enemy health.
        /// </summary>
        internal float TakeDamage(float damage)
        {
            float applied = Mathf.Min(Health, Mathf.Max(0f, damage));
            Health = Mathf.Max(0f, Health - applied);
            return applied;
        }
    }

    /// <summary>
    /// Represents a mortar impact fixed at its launch-time target position.
    /// </summary>
    public sealed class PendingAttack
    {
        public int Sequence { get; }
        public int SourceTowerIndex { get; }
        public Vector2 ImpactPoint { get; }
        public float BlastRadius { get; }
        public float Damage { get; }
        public float ImpactTime { get; }

        /// <summary>
        /// Creates a delayed impact which never tracks its original target.
        /// </summary>
        internal PendingAttack(int sequence, int sourceTowerIndex, Vector2 impactPoint,
            float blastRadius, float damage, float impactTime)
        {
            Sequence = sequence;
            SourceTowerIndex = sourceTowerIndex;
            ImpactPoint = impactPoint;
            BlastRadius = blastRadius;
            Damage = damage;
            ImpactTime = impactTime;
        }
    }

    /// <summary>
    /// Summarizes one completed wave and its economy reward.
    /// </summary>
    public sealed class WaveReport
    {
        private readonly IReadOnlyList<TowerDamageReport> _towers;

        public int WaveNumber { get; }
        public int Kills { get; }
        public int Leaks { get; }
        public int BaseDamage { get; }
        public int RemainingBaseHealth { get; }
        public int GoldReward { get; }
        public int PathLength { get; }
        public string SynergySummary { get; }
        public IReadOnlyList<TowerDamageReport> Towers => _towers;

        /// <summary>
        /// Creates an immutable result for a wave that ended without defeating the base.
        /// </summary>
        internal WaveReport(int waveNumber, int kills, int leaks, int baseDamage,
            int remainingBaseHealth, int goldReward, int pathLength, string synergySummary,
            IReadOnlyList<TowerDamageReport> towers)
        {
            WaveNumber = waveNumber;
            Kills = kills;
            Leaks = leaks;
            BaseDamage = baseDamage;
            RemainingBaseHealth = remainingBaseHealth;
            GoldReward = goldReward;
            PathLength = pathLength;
            SynergySummary = synergySummary;
            _towers = new List<TowerDamageReport>(towers).AsReadOnly();
        }
    }

    /// <summary>
    /// Records one tower's attacks and actual damage during a completed wave.
    /// </summary>
    public sealed class TowerDamageReport
    {
        public string TowerName { get; }
        public int AttackCount { get; }
        public float DamageDealt { get; }

        /// <summary>
        /// Creates an immutable per-tower combat summary.
        /// </summary>
        internal TowerDamageReport(string towerName, int attackCount, float damageDealt)
        {
            TowerName = towerName;
            AttackCount = attackCount;
            DamageDealt = damageDealt;
        }
    }

    /// <summary>
    /// Runs deterministic fixed-step enemy movement, targeting, damage, and wave transitions.
    /// </summary>
    public sealed class CombatModel
    {
        private sealed class ScheduledSpawn
        {
            public float Time;
            public int BatchOrder;
            public int UnitOrder;
            public EnemyKind Kind;
        }

        private sealed class TowerRuntime
        {
            public TowerCombatSnapshot Snapshot;
            public float NextAttackTime;
        }

        private readonly CombatSettings _settings;
        private readonly int _seed;
        private readonly List<EnemyInstance> _enemies = new List<EnemyInstance>();
        private readonly List<PendingAttack> _pendingAttacks = new List<PendingAttack>();
        private readonly List<ScheduledSpawn> _scheduledSpawns = new List<ScheduledSpawn>();
        private TowerRuntime[] _towerRuntime = new TowerRuntime[0];
        private int[] _towerAttackCounts = new int[0];
        private float[] _towerDamageDealt = new float[0];
        private float _accumulator;
        private float _simulationTime;
        private int _nextSpawnIndex;
        private int _nextEnemySequence;
        private int _nextImpactSequence;
        private int _kills;
        private int _leaks;
        private int _baseDamage;
        private int _baseHealth;
        private float _simulationSpeed = 1f;
        private bool _isPaused;

        public CombatPhase Phase { get; private set; } = CombatPhase.Preparation;
        public DefeatReason DefeatCause { get; private set; }
        public int CurrentWave { get; private set; }
        public int WaveCount => _settings.WaveCount;
        public int BaseHealth => _baseHealth;
        public int StartingBaseHealth => _settings.StartingBaseHealth;
        public bool IsPaused => _isPaused;
        public float SimulationSpeed => _simulationSpeed;
        public float SimulationTime => _simulationTime;
        public int Seed => _seed;
        public IReadOnlyList<EnemyInstance> Enemies => _enemies;
        public IReadOnlyList<PendingAttack> PendingAttacks => _pendingAttacks;
        public WaveSnapshot Snapshot { get; private set; }
        public WaveReport LastReport { get; private set; }

        /// <summary>
        /// Creates a new run with a fixed seed and full base health.
        /// </summary>
        public CombatModel(int seed, CombatSettings settings = null)
        {
            _seed = seed;
            _settings = settings ?? new CombatSettings();
            _baseHealth = _settings.StartingBaseHealth;
            CurrentWave = _settings.StartingWave;
        }

        /// <summary>
        /// Captures the current resolved board and starts the selected wave.
        /// </summary>
        public bool StartWave(BoardSynergyEvaluation evaluation, IReadOnlyList<Vector2Int> path)
        {
            if (Phase != CombatPhase.Preparation || evaluation == null || path == null || path.Count == 0) return false;
            WaveDefinition wave = _settings.GetWave(CurrentWave);
            if (wave == null) return false;

            Snapshot = new WaveSnapshot(CurrentWave, _seed, path, evaluation);
            _towerRuntime = new TowerRuntime[Snapshot.Towers.Count];
            _towerAttackCounts = new int[Snapshot.Towers.Count];
            _towerDamageDealt = new float[Snapshot.Towers.Count];
            for (int i = 0; i < _towerRuntime.Length; i++)
                _towerRuntime[i] = new TowerRuntime { Snapshot = Snapshot.Towers[i], NextAttackTime = 0f };

            _enemies.Clear();
            _pendingAttacks.Clear();
            _scheduledSpawns.Clear();
            _nextSpawnIndex = 0;
            _nextEnemySequence = 0;
            _nextImpactSequence = 0;
            _simulationTime = 0f;
            _accumulator = 0f;
            _kills = 0;
            _leaks = 0;
            _baseDamage = 0;
            LastReport = null;
            DefeatCause = DefeatReason.None;
            _isPaused = false;
            _simulationSpeed = 1f;
            BuildSpawnSchedule(wave);
            Phase = CombatPhase.Combat;
            return true;
        }

        /// <summary>
        /// Advances the simulation clock and processes complete fixed-size steps.
        /// </summary>
        public bool Advance(float unscaledDeltaTime)
        {
            if (Phase != CombatPhase.Combat || _isPaused || unscaledDeltaTime <= 0f) return false;
            _accumulator += unscaledDeltaTime * _simulationSpeed;
            float step = _settings.FixedStepSeconds;
            bool advanced = false;
            while (_accumulator >= step && Phase == CombatPhase.Combat)
            {
                _accumulator -= step;
                SimulateStep(step);
                advanced = true;
            }
            return advanced;
        }

        /// <summary>
        /// Toggles the combat clock pause without changing the current phase.
        /// </summary>
        public void TogglePause()
        {
            if (Phase == CombatPhase.Combat) _isPaused = !_isPaused;
        }

        /// <summary>
        /// Switches between the prototype's one-times and two-times simulation speeds.
        /// </summary>
        public void ToggleSpeed()
        {
            if (Phase == CombatPhase.Combat) _simulationSpeed = _simulationSpeed < 1.5f ? 2f : 1f;
        }

        /// <summary>
        /// Applies a completed wave's reward and advances to the next preparation phase.
        /// </summary>
        public int ContinueAfterWave()
        {
            if (Phase != CombatPhase.WaveResult || LastReport == null) return 0;
            int reward = LastReport.GoldReward;
            CurrentWave++;
            Phase = CombatPhase.Preparation;
            _isPaused = false;
            return reward;
        }

        /// <summary>
        /// Simulates one tick in the specified spawn, movement, attack, leak, and result order.
        /// </summary>
        private void SimulateStep(float step)
        {
            _simulationTime += step;
            SpawnDueEnemies();
            MoveEnemies(step);
            FireTowerAttacks();
            ResolveDueImpacts();
            RemoveDefeatedEnemies();
            ResolveArrivals();
            RemoveDefeatedEnemies();
            if (Phase != CombatPhase.Combat) return;
            if (_baseHealth <= 0)
            {
                EndInDefeat(DefeatReason.BaseDestroyed);
                return;
            }
            if (_nextSpawnIndex >= _scheduledSpawns.Count && _enemies.Count == 0 && _pendingAttacks.Count == 0)
                CompleteWave();
        }

        /// <summary>
        /// Builds a stable spawn order by scheduled time, batch order, then unit order.
        /// </summary>
        private void BuildSpawnSchedule(WaveDefinition wave)
        {
            for (int batchIndex = 0; batchIndex < wave.Batches.Count; batchIndex++)
            {
                SpawnBatchDefinition batch = wave.Batches[batchIndex];
                for (int unitIndex = 0; unitIndex < batch.Count; unitIndex++)
                {
                    _scheduledSpawns.Add(new ScheduledSpawn
                    {
                        Time = batch.StartTime + batch.SpawnInterval * unitIndex,
                        BatchOrder = batchIndex,
                        UnitOrder = unitIndex,
                        Kind = batch.Kind
                    });
                }
            }
            _scheduledSpawns.Sort((left, right) =>
            {
                int timeOrder = left.Time.CompareTo(right.Time);
                if (timeOrder != 0) return timeOrder;
                int batchOrder = left.BatchOrder.CompareTo(right.BatchOrder);
                return batchOrder != 0 ? batchOrder : left.UnitOrder.CompareTo(right.UnitOrder);
            });
        }

        /// <summary>
        /// Creates every scheduled enemy whose arrival time has passed.
        /// </summary>
        private void SpawnDueEnemies()
        {
            while (_nextSpawnIndex < _scheduledSpawns.Count &&
                _scheduledSpawns[_nextSpawnIndex].Time <= _simulationTime + 0.0001f)
            {
                ScheduledSpawn spawn = _scheduledSpawns[_nextSpawnIndex++];
                GetEnemyStats(spawn.Kind, CurrentWave, out float health, out float speed, out int leakDamage);
                _enemies.Add(new EnemyInstance(_nextEnemySequence++, spawn.Kind, health, speed, leakDamage,
                    Snapshot.Path[0], Snapshot.Path.Count));
            }
        }

        /// <summary>
        /// Moves all alive enemies along the path frozen at wave start.
        /// </summary>
        private void MoveEnemies(float step)
        {
            foreach (EnemyInstance enemy in _enemies)
                enemy.Move(enemy.Speed * step, Snapshot.Path);
        }

        /// <summary>
        /// Lets each ready tower attack its nearest-to-goal enemy in range once per tick.
        /// </summary>
        private void FireTowerAttacks()
        {
            foreach (TowerRuntime runtime in _towerRuntime)
            {
                TowerCombatSnapshot tower = runtime.Snapshot;
                if (_simulationTime + 0.0001f < runtime.NextAttackTime) continue;
                EnemyInstance target = FindTarget(tower);
                if (target == null) continue;
                runtime.NextAttackTime = _simulationTime + Mathf.Max(0.01f, tower.AttackInterval);
                _towerAttackCounts[tower.Index]++;
                if (tower.Role == TowerRole.Mortar)
                {
                    _pendingAttacks.Add(new PendingAttack(_nextImpactSequence++, tower.Index, target.Position,
                        tower.BlastRadius, tower.Attack, _simulationTime + _settings.MortarTravelTime));
                }
                else ApplyDamage(target, tower.Index, tower.Attack);
            }
        }

        /// <summary>
        /// Chooses an in-range target by remaining path distance, then spawn sequence.
        /// </summary>
        private EnemyInstance FindTarget(TowerCombatSnapshot tower)
        {
            EnemyInstance best = null;
            float bestDistance = float.MaxValue;
            Vector2 anchor = tower.Anchor;
            foreach (EnemyInstance enemy in _enemies)
            {
                if (!enemy.IsAlive || Vector2.Distance(anchor, enemy.Position) > tower.Range) continue;
                float remaining = enemy.RemainingPathDistance;
                if (best == null || remaining < bestDistance - 0.0001f ||
                    (Mathf.Abs(remaining - bestDistance) <= 0.0001f && enemy.Sequence < best.Sequence))
                {
                    best = enemy;
                    bestDistance = remaining;
                }
            }
            return best;
        }

        /// <summary>
        /// Resolves impacts due this tick at their fixed launch-time points.
        /// </summary>
        private void ResolveDueImpacts()
        {
            int index = 0;
            while (index < _pendingAttacks.Count)
            {
                PendingAttack impact = _pendingAttacks[index];
                if (impact.ImpactTime > _simulationTime + 0.0001f)
                {
                    index++;
                    continue;
                }
                _pendingAttacks.RemoveAt(index);
                foreach (EnemyInstance enemy in _enemies)
                    if (enemy.IsAlive && Vector2.Distance(enemy.Position, impact.ImpactPoint) <= impact.BlastRadius)
                        ApplyDamage(enemy, impact.SourceTowerIndex, impact.Damage);
            }
        }

        /// <summary>
        /// Applies damage and records a kill exactly once when health reaches zero.
        /// </summary>
        private void ApplyDamage(EnemyInstance enemy, int sourceTowerIndex, float damage)
        {
            if (!enemy.IsAlive) return;
            bool wasAlive = enemy.IsAlive;
            float appliedDamage = enemy.TakeDamage(damage);
            if (sourceTowerIndex >= 0 && sourceTowerIndex < _towerDamageDealt.Length)
                _towerDamageDealt[sourceTowerIndex] += appliedDamage;
            if (wasAlive && !enemy.IsAlive) _kills++;
        }

        /// <summary>
        /// Removes enemies killed by direct attacks or mortar impacts.
        /// </summary>
        private void RemoveDefeatedEnemies()
        {
            _enemies.RemoveAll(enemy => !enemy.IsAlive);
        }

        /// <summary>
        /// Applies leaks after attacks and gives base-health depletion defeat precedence.
        /// </summary>
        private void ResolveArrivals()
        {
            bool bossReachedGoal = false;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                EnemyInstance enemy = _enemies[i];
                if (!enemy.IsAlive || !enemy.HasArrived) continue;
                _leaks++;
                if (enemy.Kind == EnemyKind.Boss)
                {
                    bossReachedGoal = true;
                    enemy.TakeDamage(enemy.Health);
                    continue;
                }
                int appliedDamage = Mathf.Min(_baseHealth, enemy.LeakDamage);
                _baseHealth = Mathf.Max(0, _baseHealth - enemy.LeakDamage);
                _baseDamage += appliedDamage;
                enemy.TakeDamage(enemy.Health);
            }
            if (bossReachedGoal || _baseHealth <= 0)
                EndInDefeat(_baseHealth <= 0 ? DefeatReason.BaseDestroyed : DefeatReason.BossReachedGoal);
        }

        /// <summary>
        /// Finalizes a survived wave only after every spawn, enemy, and delayed attack is resolved.
        /// </summary>
        private void CompleteWave()
        {
            int reward = CurrentWave < _settings.LastWave ? _settings.WaveClearGold : 0;
            LastReport = CreateWaveReport(reward);
            Phase = CurrentWave >= _settings.LastWave ? CombatPhase.Victory : CombatPhase.WaveResult;
        }

        /// <summary>
        /// Copies the current wave's aggregate and per-tower statistics into a result.
        /// </summary>
        private WaveReport CreateWaveReport(int goldReward)
        {
            var towerReports = new List<TowerDamageReport>();
            if (Snapshot != null)
                foreach (TowerCombatSnapshot tower in Snapshot.Towers)
                    towerReports.Add(new TowerDamageReport(tower.Name,
                        _towerAttackCounts[tower.Index], _towerDamageDealt[tower.Index]));
            return new WaveReport(CurrentWave, _kills, _leaks, _baseDamage, _baseHealth, goldReward,
                Snapshot == null ? 0 : Snapshot.PathLength,
                Snapshot == null ? "" : Snapshot.SynergySummary, towerReports);
        }

        /// <summary>
        /// Ends the run and records the primary defeat cause.
        /// </summary>
        private void EndInDefeat(DefeatReason reason)
        {
            DefeatCause = reason;
            LastReport = CreateWaveReport(0);
            Phase = CombatPhase.Defeat;
        }

        /// <summary>
        /// Returns the baseline health, speed, and leak values for one enemy kind and wave.
        /// </summary>
        private void GetEnemyStats(EnemyKind kind, int wave, out float health, out float speed, out int leakDamage)
        {
            switch (kind)
            {
                case EnemyKind.Fast: health = 18f; speed = 2f; leakDamage = 1; break;
                case EnemyKind.Tank: health = 90f; speed = 0.8f; leakDamage = 2; break;
                case EnemyKind.Boss: health = 1200f; speed = 0.65f; leakDamage = 0; break;
                default: health = 25f; speed = 1.2f; leakDamage = 1; break;
            }
            if (kind != EnemyKind.Boss)
                health = Mathf.Ceil(health * Mathf.Pow(_settings.EnemyHealthGrowth, Mathf.Max(0, wave - 1)));
        }
    }
}
