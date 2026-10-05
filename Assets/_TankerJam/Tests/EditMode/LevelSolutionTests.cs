using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    /// <summary>Every shipped level (and the reference level) must be valid, fit the renderer, and win.</summary>
    public class LevelSolutionTests
    {
        const int MaxVesselUnits = 16; // VesselLiquid shader layer limit
        const int MaxRegularBays = 4;

        static string[] LevelFiles()
        {
            var files = new List<string>(Directory.GetFiles(Path.Combine(Application.dataPath, "_TankerJam/Data/Levels"), "*.json"));
            files.Add(GameSessionTests.ReferenceLevelPath);
            return files.ToArray();
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void LevelIsValidAndFitsTheBoard(string path)
        {
            var level = LevelJson.Parse(File.ReadAllText(path));
            var errors = new List<string>();
            Assert.IsTrue(LevelJson.Validate(level, errors), string.Join("\n", errors));
            Assert.LessOrEqual(level.MaxVesselHeight, MaxVesselUnits, "Vessel too tall for the VesselLiquid shader.");
            Assert.LessOrEqual(level.Slots, MaxRegularBays, "Too many regular bays for the bay row.");
        }

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

        [TestCaseSource(nameof(LevelFiles))]
        public void StoredSolutionWinsThroughSession(string path)
        {
            // Same as above but with physical bays: a full truck holds its bay until it leaves.
            var level = LevelJson.Parse(File.ReadAllText(path));
            var s = new GameSession(level, 0, 0);
            foreach (int id in level.Solution)
            {
                var r = s.Tap(id);
                Assert.AreEqual(TapOutcome.Assigned, r.Outcome, $"Truck {id}");
                foreach (var bay in s.Bays)
                {
                    if (bay.TruckId < 0) continue;
                    bool filling = false;
                    foreach (var slot in s.Rules.Slots) if (slot.TruckId == bay.TruckId) filling = true;
                    if (!filling) s.ReleaseBay(bay.Index);
                }
            }
            Assert.AreEqual(EndState.Won, s.Evaluate());
        }

        [Test]
        public void BottomLayerDrainsFirst()
        {
            var level = new LevelDef { Size = 3, Slots = 3 };
            level.Trucks.Add(new TruckDef { Id = 0, X = 0, Y = 0, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Vessels.Add(new List<char> { 'P', 'P', 'Y' });
            var rules = new GameRules(level);
            var units = rules.Assign(0);
            Assert.AreEqual(2, units.Count);
            Assert.AreEqual(1, rules.Vessels[0].Count);
            Assert.AreEqual('Y', rules.Vessels[0][0]);
            Assert.IsTrue(rules.IsWon);
        }
    }
}
