// F2 gate: the C# solver/scorer replaces the Python one. Its settle must match GameRules unit for unit,
// its lot answers must match FreeLot, it must solve every shipped level with solutions that really win,
// and its difficulty numbers must agree with the stored Python ones.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public class SolverTests
    {
        static string[] LevelFiles()
        {
            var files = new List<string>(Directory.GetFiles(Path.Combine(Application.dataPath, "_TankerJam/Data/Levels"), "*.json"));
            files.Add(GameSessionTests.ReferenceLevelPath);
            return files.ToArray();
        }

        static LevelDef Load(string path) => LevelJson.Parse(File.ReadAllText(path));

        [TestCaseSource(nameof(LevelFiles))]
        public void SolverSettleAndExitsMatchGameRules(string path)
        {
            var level = Load(path);
            var solver = new LevelSolver(level);
            for (int seed = 0; seed < 10; seed++)
            {
                var rng = new System.Random(seed);
                var rules = new GameRules(level);
                var state = solver.Start();
                var options = new List<int>();
                var unitsA = new List<Unit>();
                var unitsB = new List<Unit>();
                while (true)
                {
                    options.Clear();
                    for (int id = 0; id < level.Trucks.Count; id++)
                    {
                        bool inLot = rules.InLot(id);
                        Assert.AreEqual(inLot, (state.Remaining & (1UL << id)) != 0);
                        if (!inLot) continue;
                        bool clear = rules.CheckExit(id).Clear;
                        Assert.AreEqual(clear, solver.CanExit(id, state.Remaining), $"seed {seed} truck {id} exit");
                        if (clear) options.Add(id);
                    }
                    if (options.Count == 0 || rules.Slots.Count >= level.Slots) break;
                    int pick = options[rng.Next(options.Count)];
                    unitsA.Clear(); unitsB.Clear();
                    rules.Assign(pick, unitsA);
                    solver.Assign(state, pick, unitsB);
                    CollectionAssert.AreEqual(Strings(unitsA), Strings(unitsB), $"seed {seed} units after truck {pick}");
                    Assert.AreEqual(rules.Slots.Count, state.SlotCount);
                }
            }
        }

        static List<string> Strings(List<Unit> units)
        {
            var list = new List<string>(units.Count);
            foreach (var u in units) list.Add(u.ToString());
            return list;
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void SolverFindsAWinningSolution(string path)
        {
            var level = Load(path);
            var result = new LevelSolver(level).Solve();
            Assert.AreEqual(SolveStatus.Solved, result.Status, $"{result.NodesVisited} nodes");
            Assert.Less(result.Milliseconds, 5000, "solve time");

            level.Solution = result.Solution;
            var errors = new List<string>();
            Assert.IsTrue(LevelVerifier.Verify(level, errors), "C# solution replays as a win: " + string.Join("\n", errors));
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void RandomWinRateAgreesWithStoredPythonRate(string path)
        {
            var level = Load(path);
            if (level.RandomWinRate < 0f) Assert.Ignore("No stored rate.");
            float rate = new LevelSolver(level).RandomWinRate(400);
            // Different random streams; 400 samples gives a standard error of at most 0.025.
            Assert.AreEqual(level.RandomWinRate, rate, 0.1f);
        }

        [Test]
        public void DetectsAnUnsolvableLevel()
        {
            // One bay. Only the yellow truck can leave first (it blocks the pink one), but the vessel's bottom
            // unit is pink: yellow waits in the only bay forever and pink can never get out.
            var level = new LevelDef { Size = 3, Slots = 1 };
            level.Trucks.Add(new TruckDef { Id = 0, X = 0, Y = 0, Len = 2, Facing = Facing.R, Color = 'Y' });
            level.Trucks.Add(new TruckDef { Id = 1, X = 0, Y = 1, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Vessels.Add(new List<char> { 'P', 'P', 'Y', 'Y' });
            var solver = new LevelSolver(level);
            Assert.IsTrue(solver.CanExit(0, solver.AllTrucks));
            Assert.IsFalse(solver.CanExit(1, solver.AllTrucks));
            Assert.AreEqual(SolveStatus.Unsolvable, solver.Solve().Status);
        }

        [Test]
        public void JamDepthCountsRemovalRounds()
        {
            // A column of three trucks all facing up: each round only the front one can leave.
            var level = new LevelDef { Size = 7, Slots = 3 };
            for (int i = 0; i < 3; i++)
                level.Trucks.Add(new TruckDef { Id = i, X = 3, Y = i * 2, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Vessels.Add(new List<char> { 'P', 'P', 'P', 'P', 'P', 'P' });
            Assert.AreEqual(3, new LevelSolver(level).JamDepth());
        }

        [Test]
        public void RadialFreeFormLevelSolves()
        {
            // 12 trucks on two rings around the center, all pointing outward at 60-degree steps: each inner truck
            // is blocked by the outer truck on its spoke. A free-form board the grid could never express.
            var level = new LevelDef { Version = 2, Slots = 3, Board = BoardShape.Circle(6f), Size = 12 };
            var units = new Dictionary<char, int>();
            char[] colors = { 'P', 'Y', 'C' };
            int id = 0;
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f;
                var outer = Geometry2D.Heading(a) * 4.2f;
                var inner = Geometry2D.Heading(a) * 2.1f;
                Add(level, ref id, outer, a, 2, colors[k % 3], units);
                Add(level, ref id, inner, a, 2, colors[(k + 1) % 3], units);
            }
            foreach (var kv in units)
            {
                var tube = new List<char>();
                for (int u = 0; u < kv.Value; u++) tube.Add(kv.Key);
                level.Vessels.Add(tube);
            }
            var errors = new List<string>();
            Assert.IsTrue(LevelJson.Validate(level, errors), string.Join("\n", errors));

            var score = LevelSolver.Score(level, 200);
            Assert.AreEqual(SolveStatus.Solved, score.Solve.Status);
            Assert.AreEqual(2, score.JamDepth, "inner ring waits for the outer ring");
            level.Solution = score.Solve.Solution;
            Assert.IsTrue(LevelVerifier.Verify(level, errors), string.Join("\n", errors));
        }

        static void Add(LevelDef level, ref int id, Vec2 at, float angle, int len, char color, Dictionary<char, int> units)
        {
            level.Trucks.Add(new TruckDef
            {
                Id = id++, Len = len, Color = color, HasPose = true,
                FreePose = new TruckPose(at.X, at.Z, angle), Facing = TruckDef.FacingOf(angle),
            });
            units.TryGetValue(color, out int n);
            units[color] = n + TruckDef.CapacityFor(len);
        }
    }
}
