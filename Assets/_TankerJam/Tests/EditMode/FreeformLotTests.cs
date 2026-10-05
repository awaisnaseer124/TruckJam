// F1 gate: the geometric lot (rotated rectangles) must give exactly the grid lot's answers on every grid
// level, and grid levels must survive conversion to the version 2 format unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public class FreeformLotTests
    {
        static string[] LevelFiles() => TestLevels.Grid();

        static LevelDef Load(string path) => LevelJson.Parse(File.ReadAllText(path));

        /// <summary>Compares every truck still in the lot under both models.</summary>
        static void AssertSameExits(GameRules grid, GameRules geo, string context)
        {
            for (int id = 0; id < grid.Level.Trucks.Count; id++)
            {
                if (!grid.InLot(id)) continue;
                var a = grid.CheckExit(id);
                var b = geo.CheckExit(id);
                Assert.AreEqual(a.Clear, b.Clear, $"{context}: truck {id} clear");
                Assert.AreEqual(a.FreeCells, b.FreeCells, $"{context}: truck {id} free cells (grid {a.FreeDistance}, geo {b.FreeDistance})");
            }
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void GeometricLotMatchesGridAlongTheSolution(string path)
        {
            var level = Load(path);
            var grid = new GameRules(level, LotModel.Grid);
            var geo = new GameRules(level, LotModel.Geometric);
            AssertSameExits(grid, geo, "start");
            for (int step = 0; step < level.Solution.Count; step++)
            {
                int id = level.Solution[step];
                grid.Assign(id);
                geo.Assign(id);
                AssertSameExits(grid, geo, $"after step {step} (truck {id})");
            }
            Assert.IsTrue(geo.IsWon);
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void GeometricLotMatchesGridOnRandomPlay(string path)
        {
            var level = Load(path);
            for (int seed = 0; seed < 20; seed++)
            {
                var rng = new System.Random(seed);
                var grid = new GameRules(level, LotModel.Grid);
                var geo = new GameRules(level, LotModel.Geometric);
                var movable = new List<int>();
                while (true)
                {
                    AssertSameExits(grid, geo, $"seed {seed}");
                    movable.Clear();
                    for (int id = 0; id < level.Trucks.Count; id++)
                        if (grid.InLot(id) && grid.CheckExit(id).Clear) movable.Add(id);
                    if (movable.Count == 0) break;
                    int pick = movable[rng.Next(movable.Count)];
                    grid.Assign(pick);
                    geo.Assign(pick);
                }
            }
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void GridLevelRoundTripsThroughVersion2(string path)
        {
            var original = Load(path);
            var converted = LevelJson.Parse(LevelJson.ToJsonV2(original));

            Assert.AreEqual(2, converted.Version);
            Assert.AreEqual(original.Trucks.Count, converted.Trucks.Count);
            for (int i = 0; i < original.Trucks.Count; i++)
            {
                var a = original.Trucks[i].GetPose(original.Size);
                var b = converted.Trucks[i].GetPose(converted.Size);
                Assert.AreEqual(a.X, b.X, 1e-4f, $"truck {i} x");
                Assert.AreEqual(a.Z, b.Z, 1e-4f, $"truck {i} z");
                Assert.AreEqual(a.Angle, b.Angle, 1e-4f, $"truck {i} angle");
                Assert.AreEqual(original.Trucks[i].Color, converted.Trucks[i].Color);
                Assert.AreEqual(original.Trucks[i].Len, converted.Trucks[i].Len);
            }
            CollectionAssert.AreEqual(original.Solution, converted.Solution);

            var errors = new List<string>();
            Assert.IsTrue(LevelVerifier.Verify(converted, errors), string.Join("\n", errors));

            // Same exits on the converted level as the grid oracle on the original.
            AssertSameExits(new GameRules(original, LotModel.Grid), new GameRules(converted), "converted");
        }

        // ---------------- free-form specifics ----------------

        static LevelDef Radial(params (float x, float z, float angle, int len)[] trucks)
        {
            var level = new LevelDef { Version = 2, Slots = 3, Board = BoardShape.Circle(5f), Size = 10 };
            int units = 0;
            for (int i = 0; i < trucks.Length; i++)
            {
                var t = trucks[i];
                level.Trucks.Add(new TruckDef
                {
                    Id = i, Len = t.len, Color = 'P', HasPose = true,
                    FreePose = new TruckPose(t.x, t.z, t.angle), Facing = TruckDef.FacingOf(t.angle),
                });
                units += TruckDef.CapacityFor(t.len);
            }
            var tube = new List<char>();
            for (int u = 0; u < units; u++) tube.Add('P');
            level.Vessels.Add(tube);
            return level;
        }

        [Test]
        public void DiagonalTruckIsBlockedUntilTheTruckOnItsPathLeaves()
        {
            // Truck 0 at the center heading 45 deg (up-right); truck 1 sits on that diagonal, heading 135 deg.
            var level = Radial((0f, 0f, 45f, 2), (2.2f, -2.2f, 135f, 2));
            var rules = new GameRules(level);
            var blocked = rules.CheckExit(0);
            Assert.IsFalse(blocked.Clear);
            Assert.Greater(blocked.FreeDistance, 0.5f);
            Assert.IsTrue(rules.CheckExit(1).Clear, "truck 1 drives away from truck 0");
            rules.Assign(1);
            Assert.IsTrue(rules.CheckExit(0).Clear);
        }

        [Test]
        public void ClearExitReportsDistanceToTheCircleEdge()
        {
            var level = Radial((0f, 0f, 90f, 2));
            var r = new GameRules(level).CheckExit(0);
            Assert.IsTrue(r.Clear);
            // Front face at x = 0.95, circle edge at x = 5.
            Assert.AreEqual(4.05f, r.FreeDistance, 0.01f);
        }

        [Test]
        public void ValidationCatchesOverlapAndTrucksOffTheBoard()
        {
            var level = Radial((0f, 0f, 0f, 2), (0.3f, 0.2f, 30f, 2), (4.6f, 0f, 0f, 2));
            var errors = new List<string>();
            Assert.IsFalse(LevelJson.Validate(level, errors));
            Assert.That(errors, Has.Some.Contains("overlap"));
            Assert.That(errors, Has.Some.Contains("sticks out"));
        }

        [Test]
        public void Version2JsonParsesBoardTrucksAndObstacles()
        {
            const string json = @"{""version"":2,""board"":{""shape"":""circle"",""radius"":5},""slots"":3,
              ""trucks"":[{""x"":1.234,""z"":-0.5,""angle"":-45,""len"":3,""color"":""C""}],
              ""obstacles"":[{""x"":0,""z"":2,""size"":0.7}],
              ""tubes"":[[""C"",""C"",""C"",""C""]],""sol"":[0]}";
            var level = LevelJson.Parse(json);
            Assert.AreEqual(BoardKind.Circle, level.Board.Kind);
            Assert.AreEqual(5f, level.Board.Radius, 1e-4f);
            var p = level.Trucks[0].GetPose(level.Size);
            Assert.AreEqual(1.25f, p.X, 1e-4f, "positions snap to 0.05");
            Assert.AreEqual(315f, p.Angle, 1e-4f, "angles normalize to [0, 360)");
            Assert.AreEqual(1, level.Obstacles.Count);
            var errors = new List<string>();
            Assert.IsTrue(LevelVerifier.Verify(level, errors), string.Join("\n", errors));
        }
    }
}
