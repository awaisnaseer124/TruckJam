// F5: generates symmetric free-form levels on the square lot (the "shape" levels of the curve from level
// 11 on). Built from the level editor's own operations (LevelDraft + ShapeTracer), so anything it makes a
// designer can open and tweak in the editor.
//
// One attempt = pick a layout family -> build the trucks -> color -> fill vessels aiming at the target
// random-win rate -> accept only if the layout is clean, the jam is deep enough, the level solves and the
// measured difficulty lands in the band. Everything is seeded: the same spec always gives the same level.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    /// <summary>Layout families. Each yields symmetric boards a player reads as a shape.</summary>
    public enum PatternFamily { Petals, Mirror, Trace }

    /// <summary>What one generated level must look like.</summary>
    public sealed class PatternSpec
    {
        public int Seed = 1;
        public int LotSize = 11;
        public int Slots = 3;
        public int Colors = 4;
        public int MinTrucks = 12, MaxTrucks = 18;
        /// <summary>Truck lengths to draw from (repeat a length to weight it).</summary>
        public int[] Lens = { 2, 2, 3 };
        /// <summary>Accepted random-win-rate band, and the rate to aim for inside it.</summary>
        public float RateLo = 0.2f, RateHi = 0.4f, TargetRate = 0.3f;
        /// <summary>Minimum rounds of "every free truck leaves" to clear the lot (layout depth).</summary>
        public int MinDepth = 2;
        public PatternFamily[] Families = { PatternFamily.Petals, PatternFamily.Mirror, PatternFamily.Trace };
        public int MinVessels = 4;
        /// <summary>Traced outlines only read as shapes when dense, so they may use this many trucks above MaxTrucks.</summary>
        public int TraceExtraTrucks = 10;
        public int Attempts = 300;
        public int Samples = 400;
    }

    public sealed class PatternResult
    {
        public LevelDraft Draft;
        public LevelScore Score;
        public int JamDepth;
        public int Attempts;
        public string Pattern;
        public string Failure;
        public bool Ok => Draft != null;
    }

    public static class PatternGenerator
    {
        static readonly char[] Palette = { 'P', 'Y', 'C', 'G', 'V' };
        static readonly int[] PetalFolds = { 3, 4, 6, 8 };

        public static PatternResult Generate(PatternSpec spec)
        {
            var rng = new Random(spec.Seed);
            var result = new PatternResult();
            var reasons = new Dictionary<string, int>();
            for (int attempt = 1; attempt <= spec.Attempts; attempt++)
            {
                result.Attempts = attempt;
                var family = spec.Families[rng.Next(spec.Families.Length)];
                var d = new LevelDraft { LotSize = spec.LotSize, Slots = spec.Slots };
                string pattern = Build(d, family, spec, rng);
                LevelScore score = null;
                int depth = 0;
                int maxTrucks = family == PatternFamily.Trace ? spec.MaxTrucks + spec.TraceExtraTrucks : spec.MaxTrucks;
                string why = pattern == null ? "asymmetric" : Check(d, spec, maxTrucks, rng, out score, out depth);
                if (why != null)
                {
                    reasons.TryGetValue(why, out int k);
                    reasons[why] = k + 1;
                    continue;
                }
                d.Pattern = pattern;
                result.Draft = d;
                result.Score = score;
                result.JamDepth = depth;
                result.Pattern = pattern;
                return result;
            }
            var parts = new List<string>();
            foreach (var kv in reasons) parts.Add($"{kv.Key} x{kv.Value}");
            result.Failure = "no level in band after " + spec.Attempts + " attempts (" + string.Join(", ", parts) + ")";
            return result;
        }

        /// <summary>Returns null when the draft is accepted, else a short rejection reason.</summary>
        static string Check(LevelDraft d, PatternSpec spec, int maxTrucks, Random rng, out LevelScore score, out int depth)
        {
            score = null;
            depth = 0;
            int n = d.Trucks.Count;
            if (n < spec.MinTrucks) return "too few trucks";
            if (n > maxTrucks) return "too many trucks";
            if (n > LevelSolver.MaxTrucks) return "too many trucks";
            var report = d.Analyze();
            if (report.ProblemCount > 0) return "layout problems";
            depth = report.JamDepth;
            if (depth < 0) return "blocking cycle";
            if (depth < spec.MinDepth) return "too shallow";

            var colors = new char[Math.Min(spec.Colors, Palette.Length)];
            Array.Copy(Palette, colors, colors.Length);
            d.AutoColor(colors, rng.Next());
            int vessels = Math.Min(LevelVerifier.MaxVessels, Math.Max(spec.MinVessels, d.SuggestedVesselCount()));
            var fill = d.AutoFill(vessels, rng.Next(), attempts: 24, targetWinRate: spec.TargetRate, samples: spec.Samples);
            if (!fill.Solved) return "unsolvable";
            score = fill.Score;
            if (score.RandomWinRate < spec.RateLo) return "too hard";
            if (score.RandomWinRate > spec.RateHi) return "too easy";
            if (d.ToLevel().MaxVesselHeight > LevelVerifier.MaxVesselUnits) return "vessels too tall";
            return null;
        }

        // ---------------- families ----------------

        static string Build(LevelDraft d, PatternFamily family, PatternSpec spec, Random rng)
        {
            if (rng.NextDouble() < 0.5) d.Cones.Add(new Vec2(0f, 0f));
            switch (family)
            {
                case PatternFamily.Petals: return Petals(d, spec, rng);
                case PatternFamily.Mirror: return Mirror(d, spec, rng);
                default: return Trace(d, spec, rng);
            }
        }

        static int Len(PatternSpec spec, Random rng) => spec.Lens[rng.Next(spec.Lens.Length)];

        /// <summary>k-fold rotation of a random "petal" of trucks inside one wedge.</summary>
        static string Petals(LevelDraft d, PatternSpec spec, Random rng)
        {
            int k = PetalFolds[rng.Next(PetalFolds.Length)];
            int target = spec.MinTrucks + rng.Next(Math.Max(1, spec.MaxTrucks - spec.MinTrucks + 1));
            int perPetal = Math.Max(1, (int)Math.Round((double)target / k));
            float wedge = 360f / k;
            float maxR = d.Half - 1.1f;
            var petal = new List<DraftTruck>();
            for (int tries = 0; tries < 60 && petal.Count < perPetal; tries++)
            {
                float bearing = (float)(rng.NextDouble() * wedge);
                float r = 1.2f + (float)rng.NextDouble() * (maxR - 1.2f);
                // Headings on the 15-degree grid that stay symmetric under k-fold rotation: relative to the
                // wedge's start, outward along the bearing or tangential either way.
                bearing = (float)Math.Round(bearing / 15f) * 15f;
                double pick = rng.NextDouble();
                float angle = pick < 0.55 ? bearing : pick < 0.8 ? bearing + 90f : bearing - 90f;
                var at = Geometry2D.Heading(bearing) * r;
                var t = d.TryAdd(at.X, at.Z, angle, Len(spec, rng), 'P');
                if (t == null) continue;
                // Keep the petal only if its rotated copies would fit too (checked when copying).
                petal.Add(t);
            }
            if (petal.Count == 0) return null;
            d.RotateCopies(petal, k, out int rejected);
            // A copy with no room would break the symmetry the player reads: reject the whole layout.
            return rejected == 0 ? $"petals-{k}" : null;
        }

        /// <summary>Random trucks on the right half, mirrored left/right (and sometimes top/bottom too).</summary>
        static string Mirror(LevelDraft d, PatternSpec spec, Random rng)
        {
            bool fourWay = rng.NextDouble() < 0.5;
            int target = spec.MinTrucks + rng.Next(Math.Max(1, spec.MaxTrucks - spec.MinTrucks + 1));
            int seeds = Math.Max(1, (int)Math.Round(target / (fourWay ? 4.0 : 2.0)));
            float h = d.Half - 0.5f;
            var half = new List<DraftTruck>();
            for (int tries = 0; tries < 120 && half.Count < seeds; tries++)
            {
                float x = 0.6f + (float)rng.NextDouble() * (h - 0.6f);
                float z = fourWay ? 0.6f + (float)rng.NextDouble() * (h - 0.6f) : -h + (float)rng.NextDouble() * 2f * h;
                float angle = rng.Next(8) * 45f;
                float sx = LevelDraft.Snap(x, 0.5f), sz = LevelDraft.Snap(z, 0.5f);
                var t = d.TryAdd(sx, sz, angle, Len(spec, rng), 'P');
                if (t != null) half.Add(t);
            }
            if (half.Count == 0) return null;
            var all = new List<DraftTruck>(half);
            all.AddRange(d.MirrorX(half, out int rejectedX));
            int rejectedZ = 0;
            if (fourWay) d.MirrorZ(all, out rejectedZ);
            return rejectedX + rejectedZ == 0 ? (fourWay ? "mirror-4" : "mirror-2") : null;
        }

        /// <summary>
        /// A dense outline traced with trucks pointing outward and filling most of the lot, with either a
        /// second, smaller outline inside it or a small core of trucks around the center. The inner trucks
        /// point outward too, so they wait for the outline: that is the jam's depth.
        /// </summary>
        static string Trace(LevelDraft d, PatternSpec spec, Random rng)
        {
            var shapes = (TraceShape[])Enum.GetValues(typeof(TraceShape));
            var shape = shapes[rng.Next(shapes.Length)];
            int len = Len(spec, rng);
            float spacing = 0.9f + (float)rng.NextDouble() * 0.15f;
            float maxOuter = d.Half - len / 2f - 0.15f;
            float outer = maxOuter * (0.8f + (float)rng.NextDouble() * 0.2f);
            ShapeTracer.Trace(d, new TraceOptions { Shape = shape, Size = outer, Spacing = spacing, Len = len, Facing = TraceFacing.Outward });

            // Inner layer: a second outline when there is room for one (trucks reach about a unit to either
            // side of their line), else a k-fold core on spokes.
            float inner = outer - len / 2f - 1.25f;
            if (inner >= 2.2f && rng.NextDouble() < 0.6)
            {
                ShapeTracer.Trace(d, new TraceOptions { Shape = shape, Size = inner, Spacing = spacing, Len = 2, Facing = TraceFacing.Outward });
                return "trace-" + shape.ToString().ToLowerInvariant() + "-2";
            }
            // Core: the largest k-fold ring of spoke trucks that fits completely (a partial ring would break
            // the symmetry); outlines that dip toward the middle (heart, star) may have no room for one.
            int[] cores = rng.NextDouble() < 0.5 ? new[] { 6, 4, 3 } : new[] { 4, 3 };
            float offset = rng.NextDouble() < 0.5 ? 0f : 1f;
            foreach (int k in cores)
            {
                var added = new List<DraftTruck>(k);
                float start = offset * (float)Math.Round(180.0 / k / 15.0) * 15f;
                for (int i = 0; i < k; i++)
                {
                    float bearing = start + i * 360f / k;
                    var at = Geometry2D.Heading(bearing) * CoreRadius;
                    var t = d.TryAdd(at.X, at.Z, bearing, 2, 'P');
                    if (t == null) break;
                    added.Add(t);
                }
                if (added.Count == k) return "trace-" + shape.ToString().ToLowerInvariant() + "-core" + k;
                foreach (var t in added) d.Trucks.Remove(t);
            }
            return "trace-" + shape.ToString().ToLowerInvariant();
        }

        /// <summary>Distance of the core trucks' centers from the middle of the lot.</summary>
        const float CoreRadius = 1.35f;

        static readonly Dictionary<TraceShape, float> perimeters = new Dictionary<TraceShape, float>();

        /// <summary>Length of a shape's unit outline (size 1).</summary>
        static float UnitPerimeter(TraceShape shape)
        {
            lock (perimeters)
            {
                if (perimeters.TryGetValue(shape, out float p)) return p;
                const int steps = 720;
                var prev = ShapeTracer.UnitPoint(shape, 0);
                for (int i = 1; i <= steps; i++)
                {
                    var q = ShapeTracer.UnitPoint(shape, (double)i / steps);
                    p += (q - prev).Length;
                    prev = q;
                }
                perimeters[shape] = p;
                return p;
            }
        }
    }
}
