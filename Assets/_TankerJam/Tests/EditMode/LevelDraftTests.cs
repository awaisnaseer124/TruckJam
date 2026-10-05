// F4: the level editor's model. Every operation the editor window offers is a LevelDraft / ShapeTracer
// call, so the editor is tested here without its UI. Gate: the reference mandala is rebuilt with editor
// operations only, filled, solved, saved and reloaded, and still wins.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public class LevelDraftTests
    {
        const float Eps = 1e-3f;

        static void AssertHeading(Vec2 expected, float angle)
        {
            var h = Geometry2D.Heading(angle);
            Assert.AreEqual(expected.X, h.X, Eps, "heading x");
            Assert.AreEqual(expected.Z, h.Z, Eps, "heading z");
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(120f)]
        [TestCase(270f)]
        public void MirrorsFlipPositionAndHeading(float angle)
        {
            var t = new DraftTruck { X = 1.5f, Z = -2f, Angle = angle };
            var h = Geometry2D.Heading(angle);

            var mx = LevelDraft.MirroredX(t);
            Assert.AreEqual(-1.5f, mx.X, Eps);
            Assert.AreEqual(-2f, mx.Z, Eps);
            AssertHeading(new Vec2(-h.X, h.Z), mx.Angle);

            var mz = LevelDraft.MirroredZ(t);
            Assert.AreEqual(1.5f, mz.X, Eps);
            Assert.AreEqual(2f, mz.Z, Eps);
            AssertHeading(new Vec2(h.X, -h.Z), mz.Angle);
        }

        [Test]
        public void RotationTurnsPositionAndHeadingTogether()
        {
            // A spoke truck pointing outward stays outward-pointing at every rotation.
            var t = new DraftTruck { X = 0f, Z = -3f, Angle = 0f };
            for (int deg = 0; deg < 360; deg += 30)
            {
                var r = LevelDraft.Rotated(t, deg);
                var outward = Geometry2D.Dot(Geometry2D.Heading(r.Angle), r.Center) / r.Center.Length;
                Assert.AreEqual(1f, outward, 1e-2f, $"{deg} deg");
                Assert.AreEqual(3f, r.Center.Length, 0.03f);
            }
        }

        [Test]
        public void RotateCopiesMakesKFoldSymmetryAndSkipsCollisions()
        {
            var d = new LevelDraft { LotSize = 11 };
            var seed = d.TryAdd(0f, -3f, 0f, 2, 'P');
            var added = d.RotateCopies(new[] { seed }, 8, out int rejected);
            Assert.AreEqual(7, added.Count);
            Assert.AreEqual(0, rejected);
            Assert.AreEqual(8, d.Trucks.Count);

            // A truck near the center can't be copied 8 times without the copies touching.
            var crowded = new LevelDraft { LotSize = 11 };
            var c = crowded.TryAdd(0f, -0.9f, 90f, 2, 'P');
            crowded.RotateCopies(new[] { c }, 8, out rejected);
            Assert.Greater(rejected, 0);
            Assert.AreEqual(0, crowded.Analyze().ProblemCount, "rejected copies are never placed");
        }

        [Test]
        public void MirrorSkipsTrucksOnTheAxis()
        {
            var d = new LevelDraft();
            var onAxis = d.TryAdd(0f, -2f, 0f, 2, 'P');
            var off = d.TryAdd(2f, -2f, 45f, 2, 'Y');
            var added = d.MirrorX(new[] { onAxis, off }, out int rejected);
            Assert.AreEqual(1, added.Count, "the axis truck is its own mirror image");
            Assert.AreEqual(0, rejected);
            Assert.AreEqual(-2f, added[0].X, Eps);
            Assert.AreEqual(315f, added[0].Angle, Eps);
        }

        [Test]
        public void TryAddRejectsOverlapsAndOffLot()
        {
            var d = new LevelDraft { LotSize = 7 };
            Assert.IsNotNull(d.TryAdd(0f, 0f, 0f, 2, 'P'));
            Assert.IsNull(d.TryAdd(0.3f, 0f, 0f, 2, 'P'), "overlaps");
            Assert.IsNull(d.TryAdd(3.3f, 0f, 0f, 2, 'P'), "sticks out of the lot");
            Assert.IsNotNull(d.TryAdd(1f, 0f, 0f, 2, 'P'), "one lane over is fine");
        }

        [Test]
        public void AnalyzeFlagsProblemsAndJamDepth()
        {
            var d = new LevelDraft { LotSize = 9 };
            d.Trucks.Add(new DraftTruck { X = 0f, Z = 0f, Angle = 0f });
            d.Trucks.Add(new DraftTruck { X = 0.2f, Z = 0f, Angle = 0f });     // overlaps truck 0
            d.Trucks.Add(new DraftTruck { X = 4.4f, Z = 0f, Angle = 0f });     // off the lot
            var r = d.Analyze();
            Assert.IsTrue((r.Issues[0] & DraftIssue.Overlap) != 0);
            Assert.IsTrue((r.Issues[1] & DraftIssue.Overlap) != 0);
            Assert.IsTrue((r.Issues[2] & DraftIssue.OutOfLot) != 0);
            Assert.AreEqual(-1, r.JamDepth);

            var cone = new LevelDraft { LotSize = 9 };
            cone.Cones.Add(new Vec2(0f, -3f));
            cone.Trucks.Add(new DraftTruck { X = 0f, Z = 0f, Angle = 0f });
            Assert.AreEqual(DraftIssue.Stuck, cone.Analyze().Issues[0]);

            // Trucks facing inward on a ring block each other forever.
            var ring = new LevelDraft { LotSize = 11 };
            ShapeTracer.Trace(ring, new TraceOptions { Shape = TraceShape.Ring, Facing = TraceFacing.Inward, Size = 4f });
            Assert.AreEqual(-1, ring.Analyze().JamDepth);
            var outward = new LevelDraft { LotSize = 11 };
            ShapeTracer.Trace(outward, new TraceOptions { Shape = TraceShape.Ring, Facing = TraceFacing.Outward, Size = 4f });
            Assert.AreEqual(1, outward.Analyze().JamDepth);
        }

        [TestCase(TraceShape.Ring)]
        [TestCase(TraceShape.Heart)]
        [TestCase(TraceShape.Star)]
        [TestCase(TraceShape.Square)]
        [TestCase(TraceShape.Diamond)]
        [TestCase(TraceShape.Triangle)]
        public void TracedShapesAreCleanAndOnTheAngleGrid(TraceShape shape)
        {
            var d = new LevelDraft { LotSize = 12 };
            var added = ShapeTracer.Trace(d, new TraceOptions { Shape = shape, Size = 4.5f, AngleStep = 45f });
            Assert.GreaterOrEqual(added.Count, 10);
            var r = d.Analyze();
            Assert.AreEqual(0, r.ProblemCount);
            foreach (var t in d.Trucks) Assert.AreEqual(0f, t.Angle % 45f, Eps, "45-degree steps");
        }

        [Test]
        public void AutoColorBalancesOil()
        {
            var d = SampleLevels.MandalaDraft();
            d.AutoColor(new[] { 'P', 'Y', 'C' }, 3);
            var r = d.Analyze();
            int min = int.MaxValue, max = 0;
            foreach (var kv in r.UnitsNeeded) { min = Mathf.Min(min, kv.Value); max = Mathf.Max(max, kv.Value); }
            Assert.AreEqual(3, r.UnitsNeeded.Count);
            Assert.LessOrEqual(max - min, TruckDef.CapacityFor(4), "within one truck of each other");
        }

        [Test]
        public void ConvertedGridLevelsKeepTheirSolutions()
        {
            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "_TankerJam/Data/Levels"), "*.json"))
            {
                var level = LevelJson.Parse(File.ReadAllText(file));
                var converted = LevelDraft.From(level).ToLevel();
                var errors = new List<string>();
                Assert.IsTrue(LevelVerifier.Verify(converted, errors), Path.GetFileName(file) + ": " + string.Join("; ", errors));
            }
        }

        [Test]
        public void CloneIsIndependent()
        {
            var d = SampleLevels.MandalaDraft();
            var c = d.Clone();
            c.Trucks[0].X += 1f;
            c.Vessels.Add(new List<char> { 'P' });
            Assert.AreNotEqual(d.Trucks[0].X, c.Trucks[0].X);
            Assert.AreNotEqual(d.Vessels.Count, c.Vessels.Count);
        }

        [Test]
        public void EditsInvalidateTheStoredSolution()
        {
            var d = LevelDraft.From(SampleLevels.Radial());
            Assert.Greater(d.Solution.Count, 0);
            Assert.IsNotNull(d.TryAdd(4.6f, 4.4f, 0f, 2, 'P'), "a free corner of the lot");
            Assert.AreEqual(0, d.Solution.Count);
        }

        [Test]
        public void ReferenceMandalaIsRebuiltWithEditorToolsAndWins()
        {
            // F4 gate (model half; the PlayMode test plays it through the real game).
            var d = SampleLevels.MandalaDraft();
            var report = d.Analyze();
            Assert.AreEqual(15, d.Trucks.Count, "one 3-truck petal, 6-fold rotation");
            Assert.AreEqual(0, report.ProblemCount);
            Assert.AreEqual(2, report.JamDepth);

            var fill = d.AutoFill(4, 1, 64);
            Assert.IsTrue(fill.Solved, "a solvable vessel order exists");
            Assert.IsTrue(d.Analyze().OilBalanced);

            // Save (JSON v2) and reload exactly as the editor does, then verify like the catalog importer.
            var saved = LevelJson.ToJsonV2(d.ToLevel());
            var reloaded = LevelJson.Parse(saved);
            var errors = new List<string>();
            Assert.IsTrue(LevelVerifier.Verify(reloaded, errors), string.Join("; ", errors));
            Assert.AreEqual(saved, LevelJson.ToJsonV2(LevelDraft.From(reloaded).ToLevel()), "load/save round trip is lossless");
        }
    }
}
