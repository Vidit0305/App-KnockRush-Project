using System;
using System.Collections.Generic;
using System.Linq;
using KnockRush.Maps;
using UnityEngine;

namespace KnockRush.GameModes
{
    /// <summary>
    /// All game modes and the map pool (Resources/GameModeCatalog.asset). Indices into these
    /// arrays are replicated, so the order must be identical on server and clients (same build).
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Game Modes/Catalog", fileName = "GameModeCatalog", order = 1)]
    public sealed class GameModeCatalog : ScriptableObject
    {
        public const string ResourcesPath = "GameModeCatalog";

        [SerializeField] private GameModeDefinition[] modes = new GameModeDefinition[0];
        [SerializeField] private MapDefinition[] maps = new MapDefinition[0];

        public IReadOnlyList<GameModeDefinition> Modes => modes;
        public IReadOnlyList<MapDefinition> Maps => maps;

        public void Configure(GameModeDefinition[] gameModes, MapDefinition[] mapPool)
        {
            modes = gameModes;
            maps = mapPool;
        }

        public static GameModeCatalog Load()
        {
            var catalog = Resources.Load<GameModeCatalog>(ResourcesPath);
            if (catalog == null)
            {
                throw new InvalidOperationException("GameModeCatalog missing from Resources. Run KnockRush > Build > All Generated Content.");
            }

            return catalog;
        }

        public int IndexOfMode(string modeId)
        {
            for (var i = 0; i < modes.Length; i++)
            {
                if (modes[i] != null && modes[i].ModeId == modeId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Picks a map index for a round category: prefers maps of that category not played yet,
        /// then any map of the category, then any race map (so modes still work while maps are added).
        /// </summary>
        public int PickMap(MapCategory category, System.Random random, ICollection<int> alreadyPlayed)
        {
            var candidates = Indices(m => m.Category == category && !alreadyPlayed.Contains(IndexOf(m)));
            if (candidates.Count == 0)
            {
                candidates = Indices(m => m.Category == category);
            }

            if (candidates.Count == 0)
            {
                candidates = Indices(m => m.Category == MapCategory.Race);
            }

            if (candidates.Count == 0)
            {
                candidates = Indices(m => true);
            }

            return candidates.Count == 0 ? -1 : candidates[random.Next(candidates.Count)];
        }

        private List<int> Indices(Func<MapDefinition, bool> predicate)
        {
            return Enumerable.Range(0, maps.Length).Where(i => maps[i] != null && predicate(maps[i])).ToList();
        }

        private int IndexOf(MapDefinition map)
        {
            return Array.IndexOf(maps, map);
        }
    }
}
