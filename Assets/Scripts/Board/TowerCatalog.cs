using System.Collections.Generic;

using UnityEngine;

namespace BackpackDefense
{
    /// <summary>
    /// Provides the nine tower footprints specified by the prototype design.
    /// </summary>
    public static class TowerCatalog
    {
        /// <summary>
        /// Creates the initial tower shapes with their local attack anchors.
        /// </summary>
        public static IReadOnlyList<TowerShape> CreateShapes()
        {
            return new[]
            {
                new TowerShape("Stone Mortar", new[] { V(0,0), V(1,0), V(0,1), V(1,1), V(0,2), V(1,2) }, V(0,1), 1),
                new TowerShape("Stone Sniper", new[] { V(0,0), V(1,0), V(0,1), V(0,2), V(0,3) }, V(0,1), 2),
                new TowerShape("Stone Tuner", new[] { V(0,0), V(1,0), V(1,1), V(2,1), V(2,2) }, V(1,1), 3),
                new TowerShape("Fungal Mortar", new[] { V(0,0), V(1,0), V(2,0), V(1,1), V(1,2) }, V(1,0), 2),
                new TowerShape("Fungal Sniper", new[] { V(0,0), V(1,0), V(1,1), V(1,2), V(2,2) }, V(1,1), 3),
                new TowerShape("Fungal Tuner", new[] { V(0,0), V(1,0), V(2,0), V(3,0), V(4,0) }, V(2,0), 1),
                new TowerShape("Resonant Mortar", new[] { V(0,0), V(1,0), V(2,0), V(0,1), V(2,1) }, V(1,0), 3),
                new TowerShape("Resonant Sniper", new[] { V(0,0), V(1,0), V(2,0), V(3,0), V(4,0), V(5,0) }, V(2,0), 1),
                new TowerShape("Resonant Tuner", new[] { V(0,0), V(1,0), V(0,1), V(1,1), V(0,2) }, V(0,1), 2)
            };
        }

        /// <summary>
        /// Creates a local grid coordinate from its x and y components.
        /// </summary>
        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    }
}
