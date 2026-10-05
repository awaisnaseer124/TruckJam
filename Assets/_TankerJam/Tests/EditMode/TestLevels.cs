// Level files the tests run over. Since F5 the shipped curve mixes grid levels (1-10) and generated
// free-form shape levels (11+); the original grid levels 11-50 are kept under Fixtures/Levels so the
// grid-specific checks (Python parity, grid vs geometric lot, solver on grid layouts) keep their coverage.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public static class TestLevels
    {
        static string Root => Path.Combine(Application.dataPath, "_TankerJam");

        /// <summary>Every level in Data/Levels (what the game ships).</summary>
        public static List<string> Shipped() =>
            Directory.GetFiles(Path.Combine(Root, "Data/Levels"), "*.json").OrderBy(p => p).ToList();

        /// <summary>Grid (version 1) levels: shipped grid levels, the preserved originals and the reference level.</summary>
        public static string[] Grid()
        {
            var files = Shipped().Where(IsGridFile).ToList();
            files.AddRange(Directory.GetFiles(Path.Combine(Root, "Tests/EditMode/Fixtures/Levels"), "level_*.json")
                .Where(p => !p.EndsWith(".traces.json")).OrderBy(p => p));
            files.Add(GameSessionTests.ReferenceLevelPath);
            return files.ToArray();
        }

        /// <summary>Shipped levels plus the reference level.</summary>
        public static string[] ShippedAndReference()
        {
            var files = Shipped();
            files.Add(GameSessionTests.ReferenceLevelPath);
            return files.ToArray();
        }

        /// <summary>Everything: shipped, preserved grid originals and the reference level.</summary>
        public static string[] All() => ShippedAndReference().Concat(Grid()).Distinct().ToArray();

        static bool IsGridFile(string path) => LevelJson.Parse(File.ReadAllText(path)).IsGrid;
    }
}
