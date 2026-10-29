using System;
using System.Collections.Generic;

using UnityEngine;

namespace BackpackDefense
{
    /// <summary>
    /// Identifies a tower's race for global and adjacency synergies.
    /// </summary>
    public enum TowerRace
    {
        Stone,
        Fungal,
        Resonant
    }

    /// <summary>
    /// Identifies a tower's combat role for global, path, and aura effects.
    /// </summary>
    public enum TowerRole
    {
        Mortar,
        Sniper,
        Tuner
    }

    /// <summary>
    /// Stores tower geometry and its attack anchor in local grid coordinates.
    /// </summary>
    [Serializable]
    public sealed class TowerShape
    {
        [SerializeField] private string _definitionId;
        [SerializeField] private string _name;
        [SerializeField] private Vector2Int[] _cells;
        [SerializeField] private Vector2Int _anchor;
        [SerializeField, Range(1, 3)] private int _cost;
        [SerializeField] private TowerRace _race;
        [SerializeField] private TowerRole _role;
        [SerializeField, Min(0f)] private float _baseAttack;
        [SerializeField, Min(0.01f)] private float _baseAttackInterval = 1f;
        [SerializeField, Min(0f)] private float _baseRange;

        public string DefinitionId => _definitionId;
        public string Name => _name;
        public int Cost => _cost;
        public TowerRace Race => _race;
        public TowerRole Role => _role;
        public float BaseAttack => _baseAttack;
        public float BaseAttackInterval => _baseAttackInterval;
        public float BaseRange => _baseRange;

        /// <summary>
        /// Creates a tower definition with footprint, identity, combat role, and base attack values.
        /// </summary>
        public TowerShape(string name, Vector2Int[] cells, Vector2Int anchor, int cost = 1,
            string definitionId = "", TowerRace race = TowerRace.Stone, TowerRole role = TowerRole.Mortar,
            float baseAttack = 0f, float baseAttackInterval = 1f, float baseRange = 0f)
        {
            _definitionId = string.IsNullOrWhiteSpace(definitionId) ? name : definitionId;
            _name = name;
            _cells = cells;
            _anchor = anchor;
            _cost = cost;
            _race = race;
            _role = role;
            _baseAttack = baseAttack;
            _baseAttackInterval = Mathf.Max(0.01f, baseAttackInterval);
            _baseRange = baseRange;
        }

        /// <summary>
        /// Returns occupied world cells after rotation around the attack anchor.
        /// </summary>
        public List<Vector2Int> GetCells(Vector2Int position, int rotation)
        {
            var result = new List<Vector2Int>(_cells.Length);
            foreach (Vector2Int cell in _cells)
            {
                Vector2Int offset = cell - _anchor;
                for (int i = 0; i < (rotation % 4 + 4) % 4; i++)
                    offset = new Vector2Int(-offset.y, offset.x);
                result.Add(position + offset);
            }
            return result;
        }
    }

    /// <summary>
    /// Represents a tower placed at an anchor and a quarter-turn rotation.
    /// </summary>
    public sealed class PlacedTower
    {
        public TowerShape Shape { get; }
        public Vector2Int Position { get; }
        public int Rotation { get; }

        /// <summary>
        /// Creates an immutable placement for a shape, anchor, and rotation.
        /// </summary>
        public PlacedTower(TowerShape shape, Vector2Int position, int rotation)
        {
            Shape = shape;
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// Validates tower placements and computes the deterministic shortest enemy path.
    /// </summary>
    public sealed class BoardModel
    {
        public const int WIDTH = 10;
        public const int HEIGHT = 8;

        private static readonly Vector2Int[] DIRECTIONS =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };

        private readonly List<PlacedTower> _towers = new List<PlacedTower>();

        public Vector2Int Spawn { get; } = new Vector2Int(0, 3);
        public Vector2Int Goal { get; } = new Vector2Int(9, 3);
        public IReadOnlyList<PlacedTower> Towers => _towers;

        /// <summary>
        /// Evaluates a proposed placement without changing the board and returns its path or failure reason.
        /// </summary>
        public bool TryPreview(TowerShape shape, Vector2Int position, int rotation,
            PlacedTower moving, out List<Vector2Int> path, out string reason)
        {
            bool[,] blocked = new bool[WIDTH, HEIGHT];
            foreach (PlacedTower tower in _towers)
            {
                if (tower == moving) continue;
                foreach (Vector2Int cell in tower.Shape.GetCells(tower.Position, tower.Rotation))
                    blocked[cell.x, cell.y] = true;
            }

            foreach (Vector2Int cell in shape.GetCells(position, rotation))
            {
                if (!IsInside(cell))
                {
                    path = null;
                    reason = "Outside the board";
                    return false;
                }
                if (cell == Spawn || cell == Goal || blocked[cell.x, cell.y])
                {
                    path = null;
                    reason = "Occupied or reserved";
                    return false;
                }
                blocked[cell.x, cell.y] = true;
            }

            path = FindPath(blocked);
            reason = path == null ? "Path blocked" : null;
            return path != null;
        }

        /// <summary>
        /// Commits a valid placement or move and returns the resulting tower instance.
        /// </summary>
        public bool TryPlace(TowerShape shape, Vector2Int position, int rotation,
            PlacedTower moving, out PlacedTower result)
        {
            result = null;
            if (!TryPreview(shape, position, rotation, moving, out _, out _)) return false;
            if (moving != null) _towers.Remove(moving);
            result = new PlacedTower(shape, position, rotation);
            _towers.Add(result);
            return true;
        }

        /// <summary>
        /// Removes a placed tower and returns whether it belonged to this board.
        /// </summary>
        public bool Remove(PlacedTower tower) => _towers.Remove(tower);

        /// <summary>
        /// Returns the shortest path for the current tower configuration.
        /// </summary>
        public List<Vector2Int> GetPath()
        {
            return GetPathWithout(null);
        }

        /// <summary>
        /// Returns the shortest path after hypothetically removing one tower.
        /// </summary>
        public List<Vector2Int> GetPathWithout(PlacedTower excluded)
        {
            bool[,] blocked = new bool[WIDTH, HEIGHT];
            foreach (PlacedTower tower in _towers)
            {
                if (tower == excluded) continue;
                foreach (Vector2Int cell in tower.Shape.GetCells(tower.Position, tower.Rotation))
                    blocked[cell.x, cell.y] = true;
            }
            return FindPath(blocked);
        }

        /// <summary>
        /// Finds a tower occupying the requested grid cell or returns null.
        /// </summary>
        public PlacedTower FindTower(Vector2Int cell)
        {
            foreach (PlacedTower tower in _towers)
                if (tower.Shape.GetCells(tower.Position, tower.Rotation).Contains(cell))
                    return tower;
            return null;
        }

        /// <summary>
        /// Computes goal distances and reconstructs a path with fixed directional tie breaking.
        /// </summary>
        private List<Vector2Int> FindPath(bool[,] blocked)
        {
            int[,] distances = new int[WIDTH, HEIGHT];
            for (int x = 0; x < WIDTH; x++)
                for (int y = 0; y < HEIGHT; y++)
                    distances[x, y] = -1;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(Goal);
            distances[Goal.x, Goal.y] = 0;
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in DIRECTIONS)
                {
                    Vector2Int next = current + direction;
                    if (!IsInside(next) || blocked[next.x, next.y] || distances[next.x, next.y] >= 0) continue;
                    distances[next.x, next.y] = distances[current.x, current.y] + 1;
                    queue.Enqueue(next);
                }
            }
            if (distances[Spawn.x, Spawn.y] < 0) return null;
            var path = new List<Vector2Int> { Spawn };
            Vector2Int point = Spawn;
            while (point != Goal)
            {
                foreach (Vector2Int direction in DIRECTIONS)
                {
                    Vector2Int next = point + direction;
                    if (!IsInside(next) || distances[next.x, next.y] != distances[point.x, point.y] - 1) continue;
                    point = next;
                    path.Add(point);
                    break;
                }
            }
            return path;
        }

        /// <summary>
        /// Checks whether a cell is inside the board bounds.
        /// </summary>
        private static bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < WIDTH && cell.y >= 0 && cell.y < HEIGHT;
        }
    }
}
