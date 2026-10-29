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
                new TowerShape("Stone Mortar", new[] { V(0,0), V(1,0), V(0,1), V(1,1), V(0,2), V(1,2) }, V(0,1), 1,
                    "stone_mortar", TowerRace.Stone, TowerRole.Mortar, 8f, 1.50f, 2.50f),
                new TowerShape("Stone Sniper", new[] { V(0,0), V(1,0), V(0,1), V(0,2), V(0,3) }, V(0,1), 2,
                    "stone_sniper", TowerRace.Stone, TowerRole.Sniper, 24f, 1.20f, 3.00f),
                new TowerShape("Stone Tuner", new[] { V(0,0), V(1,0), V(1,1), V(2,1), V(2,2) }, V(1,1), 3,
                    "stone_tuner", TowerRace.Stone, TowerRole.Tuner, 13f, 1.00f, 2.25f),
                new TowerShape("Fungal Mortar", new[] { V(0,0), V(1,0), V(2,0), V(1,1), V(1,2) }, V(1,0), 2,
                    "fungal_mortar", TowerRace.Fungal, TowerRole.Mortar, 16f, 1.50f, 2.50f),
                new TowerShape("Fungal Sniper", new[] { V(0,0), V(1,0), V(1,1), V(1,2), V(2,2) }, V(1,1), 3,
                    "fungal_sniper", TowerRace.Fungal, TowerRole.Sniper, 38f, 1.20f, 3.00f),
                new TowerShape("Fungal Tuner", new[] { V(0,0), V(1,0), V(2,0), V(3,0), V(4,0) }, V(2,0), 1,
                    "fungal_tuner", TowerRace.Fungal, TowerRole.Tuner, 4f, 1.00f, 2.25f),
                new TowerShape("Resonant Mortar", new[] { V(0,0), V(1,0), V(2,0), V(0,1), V(2,1) }, V(1,0), 3,
                    "resonant_mortar", TowerRace.Resonant, TowerRole.Mortar, 26f, 1.50f, 2.50f),
                new TowerShape("Resonant Sniper", new[] { V(0,0), V(1,0), V(2,0), V(3,0), V(4,0), V(5,0) }, V(2,0), 1,
                    "resonant_sniper", TowerRace.Resonant, TowerRole.Sniper, 12f, 1.20f, 3.00f),
                new TowerShape("Resonant Tuner", new[] { V(0,0), V(1,0), V(0,1), V(1,1), V(0,2) }, V(0,1), 2,
                    "resonant_tuner", TowerRace.Resonant, TowerRole.Tuner, 8f, 1.00f, 2.25f)
            };
        }

        /// <summary>
        /// Creates a local grid coordinate from its x and y components.
        /// </summary>
        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    }
}
