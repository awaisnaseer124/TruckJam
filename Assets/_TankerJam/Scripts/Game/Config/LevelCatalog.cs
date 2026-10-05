using System;
using System.Collections.Generic;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// The ordered, verified level list the game plays. Built by the editor LevelImporter from
    /// Data/Levels/*.json (never edited by hand); only levels that passed LevelVerifier are included.
    /// </summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public TextAsset Json;
            public int Index;
            public int Tier;
            public bool Hard;
            public string Introduces;
            [Range(0, 1)] public float RandomWinRate;
            public int Trucks;
            public int Par;
        }

        public List<Entry> Levels = new List<Entry>();

        public int Count => Levels.Count;

        /// <summary>Entry for a 0-based position. Past the end, loops over the last tier (endless play).</summary>
        public Entry At(int position)
        {
            if (Levels.Count == 0) throw new InvalidOperationException("Level catalog is empty.");
            if (position < Levels.Count) return Levels[Math.Max(0, position)];
            int loopStart = Mathf.Max(0, Levels.Count - 10);
            return Levels[loopStart + (position - Levels.Count) % (Levels.Count - loopStart)];
        }

        public LevelDef Load(int position) => LevelJson.Parse(At(position).Json.text);
    }
}
