// F5: shape-level generation. Gate: batch-generate each tier of the curve; every level verifies, its
// measured difficulty lands in the band, its layout is truly symmetric and deep enough. The shipped levels
// 11-50 must be exactly what the curve says.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public class PatternGeneratorTests
    {
        static string CurveFile => Path.Combine(Application.dataPath, "_TankerJam/Data/Generation/curve_v2.json");
        static PatternCurve Curve() => PatternCurve.Parse(File.ReadAllText(CurveFile));

        static int[] TierStarts()
        {
            var list = new List<int>();
            foreach (var t in Curve().Tiers) list.Add(t.From);
            return list.ToArray();
        }

        [Test]
        public void SameSpecGivesTheSameLevel()
        {
            var spec = Curve().SpecFor(23);
            var a = PatternGenerator.Generate(spec);
            var b = PatternGenerator.Generate(spec);
            Assert.IsTrue(a.Ok, a.Failure);
            Assert.AreEqual(LevelJson.ToJsonV2(a.Draft.ToLevel()), LevelJson.ToJsonV2(b.Draft.ToLevel()));
        }

        [TestCaseSource(nameof(TierStarts))]
        public void TierBatchLandsInBand(int tierStart)
        {
            var curve = Curve();
            var tier = curve.TierFor(tierStart);
            int level = tierStart + 2; // a regular (not Hard, not breather) slot of the tier
            for (int k = 1; k <= 4; k++)
            {
                var spec = curve.SpecFor(level, null, seedOffset: k * 101);
                var r = PatternGenerator.Generate(spec);
                Assert.IsTrue(r.Ok, $"tier {tier.Tier} seed {k}: {r.Failure}");
                var def = r.Draft.ToLevel();
                var errors = new List<string>();
                Assert.IsTrue(LevelVerifier.Verify(def, errors), string.Join("; ", errors));
                Assert.That(r.Score.RandomWinRate, Is.InRange(spec.RateLo, spec.RateHi), "difficulty band");
                // Traced outlines need density to read as a shape, so they may go past the tier's truck count.
                int max = tier.MaxTrucks + (r.Pattern.StartsWith("trace-") ? spec.TraceExtraTrucks : 0);
                Assert.That(def.Trucks.Count, Is.InRange(tier.MinTrucks, max));
                Assert.GreaterOrEqual(r.JamDepth, tier.Depth);
                Assert.GreaterOrEqual(def.Vessels.Count, tier.Vessels);
                Assert.LessOrEqual(def.Vessels.Count, LevelVerifier.MaxVessels);
                Assert.AreEqual(tier.Lot, (int)def.Board.Width);
                AssertSymmetric(r.Draft, r.Pattern);
            }
        }

        [TestCase(PatternFamily.Petals)]
        [TestCase(PatternFamily.Mirror)]
        [TestCase(PatternFamily.Trace)]
        public void EveryFamilyGenerates(PatternFamily family)
        {
            var spec = Curve().SpecFor(20, family, seedOffset: 7);
            var r = PatternGenerator.Generate(spec);
            Assert.IsTrue(r.Ok, r.Failure);
            StringAssert.StartsWith(family == PatternFamily.Petals ? "petals-" : family == PatternFamily.Mirror ? "mirror-" : "trace-", r.Pattern);
            AssertSymmetric(r.Draft, r.Pattern);
        }

        /// <summary>Petal levels map onto themselves under rotation, mirror levels under reflection.</summary>
        static void AssertSymmetric(LevelDraft d, string pattern)
        {
            if (pattern.StartsWith("petals-"))
            {
                int k = int.Parse(pattern.Substring("petals-".Length));
                foreach (var t in d.Trucks) AssertHasTwin(d, LevelDraft.Rotated(t, 360f / k), pattern);
            }
            else if (pattern.StartsWith("mirror-"))
            {
                foreach (var t in d.Trucks) AssertHasTwin(d, LevelDraft.MirroredX(t), pattern);
                if (pattern == "mirror-4")
                    foreach (var t in d.Trucks) AssertHasTwin(d, LevelDraft.MirroredZ(t), pattern);
            }
        }

        static void AssertHasTwin(LevelDraft d, DraftTruck image, string pattern)
        {
            foreach (var o in d.Trucks)
                if (Mathf.Abs(o.X - image.X) < 0.08f && Mathf.Abs(o.Z - image.Z) < 0.08f && o.Len == image.Len &&
                    Mathf.Abs(Mathf.DeltaAngle(o.Angle, image.Angle)) < 0.5f)
                    return;
            Assert.Fail($"{pattern}: no symmetric twin for a truck at ({image.X:0.##}, {image.Z:0.##}) {image.Angle:0} deg");
        }

        static int[] CurveLevels()
        {
            var c = Curve();
            var list = new List<int>();
            for (int i = c.FirstLevel; i <= c.LastLevel; i++) list.Add(i);
            return list.ToArray();
        }

        [TestCaseSource(nameof(CurveLevels))]
        public void ShippedShapeLevelMatchesTheCurve(int index)
        {
            var curve = Curve();
            string path = Path.Combine(Application.dataPath, $"_TankerJam/Data/Levels/level_{index:000}.json");
            var level = LevelJson.Parse(File.ReadAllText(path));
            var tier = curve.TierFor(index);
            curve.Band(index, out float lo, out float hi, out _);

            Assert.IsFalse(level.IsGrid, "shape levels are free-form");
            Assert.IsNotNull(level.Pattern);
            Assert.AreEqual(index, level.Index);
            Assert.AreEqual(tier.Tier, level.Tier);
            Assert.AreEqual(curve.IsHard(index), level.Hard);
            Assert.That(level.RandomWinRate, Is.InRange(lo - 1e-3f, hi + 1e-3f), "stored rate in band");
            // The stored rate is reproducible (same samples, same seed).
            Assert.AreEqual(level.RandomWinRate, new LevelSolver(level).RandomWinRate(400), 1e-3f);
        }

        [Test]
        public void TutorialLevelsStayGrid()
        {
            var curve = Curve();
            for (int i = 1; i < curve.FirstLevel; i++)
            {
                string path = Path.Combine(Application.dataPath, $"_TankerJam/Data/Levels/level_{i:000}.json");
                Assert.IsTrue(LevelJson.Parse(File.ReadAllText(path)).IsGrid, $"level {i}");
            }
        }

        [Test]
        public void TallVesselsVerifyAndKeepTheGlassHeight()
        {
            // 42 units of oil in 2 vessels: 21 each, more than the 16 layers a vessel shows.
            var d = SampleLevels.MandalaDraft();
            var fill = d.AutoFill(2, 1, 64);
            Assert.IsTrue(fill.Solved);
            var level = d.ToLevel();
            Assert.Greater(level.MaxVesselHeight, LevelVerifier.VisibleVesselUnits);
            var errors = new List<string>();
            Assert.IsTrue(LevelVerifier.Verify(level, errors), string.Join("; ", errors));
            Assert.AreEqual(LevelVerifier.VisibleVesselUnits, new BoardLayout(level).MaxVesselUnits, "glass shows 16 layers");
        }
    }
}
