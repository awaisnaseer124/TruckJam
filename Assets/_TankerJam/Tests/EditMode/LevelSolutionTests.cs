using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public class LevelSolutionTests
    {
        static string[] LevelFiles() =>
            Directory.GetFiles(Path.Combine(Application.dataPath, "_TankerJam/Data/Levels"), "*.json");

        [TestCaseSource(nameof(LevelFiles))]
        public void StoredSolutionWinsWithRegularBays(string path)
        {
            var level = LevelJson.Parse(File.ReadAllText(path));
            Assert.IsNotEmpty(level.Solution, "Level has no stored solution ('sol').");
            var rules = new GameRules(level);
            foreach (int id in level.Solution)
            {
                Assert.IsTrue(rules.CheckExit(id).Clear, $"Truck {id} is blocked.");
                Assert.Less(rules.Slots.Count, level.Slots, $"No free bay for truck {id}.");
                rules.Assign(id);
            }
            Assert.IsTrue(rules.IsWon, "Solution did not clear the level.");
        }

        [Test]
        public void BottomLayerDrainsFirst()
        {
            var level = new LevelDef { Size = 3, Slots = 3 };
            level.Trucks.Add(new TruckDef { Id = 0, X = 0, Y = 0, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Vessels.Add(new System.Collections.Generic.List<char> { 'P', 'P', 'Y' });
            var rules = new GameRules(level);
            var units = rules.Assign(0);
            Assert.AreEqual(2, units.Count);
            Assert.AreEqual(1, rules.Vessels[0].Count);
            Assert.AreEqual('Y', rules.Vessels[0][0]);
            Assert.IsTrue(rules.IsWon);
        }
    }
}
