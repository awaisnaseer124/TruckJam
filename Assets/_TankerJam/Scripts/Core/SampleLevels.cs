// Ready-made free-form levels used by tests and the Dev menu, built with the same tools the level editor
// uses (LevelDraft + ShapeTracer). Every sample sits on a plain square lot; the shape comes from how the
// trucks are arranged. Vessel orders are shuffled from a seed until the solver finds a win, so a sample is
// always playable and carries a solution for SolutionPlayer.
using System;

namespace TankerJam.Core
{
    public static class SampleLevels
    {
        /// <summary>
        /// Ring of spokes: 8 spokes at 45-degree steps around a center cone, two trucks per spoke, all pointing
        /// outward, so each inner truck waits for the outer truck on its spoke. 16 trucks, 4 colors.
        /// Built like a designer would: two trucks on one spoke, then 8-fold rotation.
        /// </summary>
        public static LevelDef Radial(int seed = 1)
        {
            var d = new LevelDraft { LotSize = 11, Pattern = "radial-8" };
            d.Cones.Add(new Vec2(0f, 0f));
            var seeds = new[] { d.TryAdd(0f, -4.1f, 0f, 2, 'P'), d.TryAdd(0f, -2.1f, 0f, 2, 'Y') };
            d.RotateCopies(seeds, 8, out _);
            d.AutoColor("PYCG".ToCharArray(), seed);
            return Finish(d, seed);
        }

        /// <summary>
        /// Heart: trucks stand across the outline of a heart (pointing outward), with a smaller heart inside
        /// whose trucks wait for the outer ones. Shows that any outline can be traced with trucks.
        /// </summary>
        public static LevelDef Heart(int seed = 1)
        {
            var d = new LevelDraft { LotSize = 12, Pattern = "heart" };
            ShapeTracer.Trace(d, new TraceOptions { Shape = TraceShape.Heart, Size = 4.65f, Spacing = 0.85f });
            ShapeTracer.Trace(d, new TraceOptions { Shape = TraceShape.Heart, Size = 2.25f, Spacing = 0.85f, Colors = "CGPY" });
            return Finish(d, seed);
        }

        /// <summary>
        /// The F4 reference: a bus-jam style mandala rebuilt only with level-editor operations. One "petal"
        /// of three trucks (a short inner spoke truck, a long outer spoke truck, a tangential truck between
        /// spokes) copied with 6-fold rotation around a center cone, then auto-colored and auto-filled.
        /// </summary>
        public static LevelDef Mandala(int seed = 1)
        {
            var d = MandalaDraft(seed);
            return Finish(d, seed);
        }

        /// <summary>The mandala layout before vessels are filled (what the designer sees after the symmetry step).</summary>
        public static LevelDraft MandalaDraft(int seed = 1)
        {
            var d = new LevelDraft { LotSize = 12, Pattern = "mandala-6" };
            d.Cones.Add(new Vec2(0f, 0f));
            var petal = new[]
            {
                d.TryAdd(0f, -1.6f, 0f, 2, 'P'),                                              // inner spoke, points out
                d.TryAdd(0f, -4.05f, 0f, 3, 'Y'),                                             // outer spoke, points out
                Place(d, 30f, 3.0f, 120f),                                                    // between spokes, clockwise
            };
            d.RotateCopies(petal, 6, out _);
            d.AutoColor("PYCG".ToCharArray(), seed);
            return d;
        }

        /// <summary>Adds a length-2 truck at polar position (bearing, radius) with the given heading.</summary>
        static DraftTruck Place(LevelDraft d, float bearing, float radius, float angle)
        {
            var at = Geometry2D.Heading(bearing) * radius;
            return d.TryAdd(at.X, at.Z, angle, 2, 'C');
        }

        static LevelDef Finish(LevelDraft d, int seed)
        {
            var fill = d.AutoFill(Math.Max(4, d.SuggestedVesselCount(14)), seed, attempts: 64);
            if (!fill.Solved) throw new InvalidOperationException($"No solvable vessel order found for the '{d.Pattern}' sample.");
            return d.ToLevel();
        }
    }
}
