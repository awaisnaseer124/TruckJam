// Hand-built free-form levels used by tests and the Dev menu before the visual editor exists (F3).
// Every sample sits on a plain square lot; the shape comes from how the trucks are arranged. The vessel
// order is shuffled from a seed until the solver finds a win, so a sample is always playable and carries
// a solution for SolutionPlayer.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public static class SampleLevels
    {
        static readonly char[] Colors = { 'P', 'Y', 'C', 'G' };

        /// <summary>
        /// Ring of spokes: 8 spokes at 45-degree steps around a center cone, two trucks per spoke, all pointing
        /// outward, so each inner truck waits for the outer truck on its spoke. 16 trucks, 4 colors, 4 vessels.
        /// </summary>
        public static LevelDef Radial(int seed = 1)
        {
            var level = NewLevel(11, "radial-8");
            level.Obstacles.Add(new Obstacle(0f, 0f, Obstacle.ConeHalfSize));
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                TryAdd(level, Geometry2D.Heading(a) * 4.1f, a, Colors[k % 4]);
                TryAdd(level, Geometry2D.Heading(a) * 2.1f, a, Colors[(k + 1) % 4]);
            }
            return Finish(level, seed);
        }

        /// <summary>
        /// Heart: trucks stand across the outline of a heart (pointing outward), with a smaller heart inside
        /// whose trucks wait for the outer ones. Shows that any outline can be traced with trucks.
        /// </summary>
        public static LevelDef Heart(int seed = 1)
        {
            var level = NewLevel(12, "heart");
            int color = 0;
            Trace(level, 0.29f, 0.85f, ref color);  // outer heart
            Trace(level, 0.14f, 0.85f, ref color);  // inner heart
            return Finish(level, seed);
        }

        // ---------------- building blocks ----------------

        static LevelDef NewLevel(int size, string pattern) =>
            new LevelDef { Version = 2, Size = size, Slots = 3, Board = BoardShape.Rounded(size, size, 0f), Pattern = pattern };

        /// <summary>Classic heart curve; board space has +z toward the camera, so the lobes point up the screen (-z).</summary>
        static Vec2 HeartPoint(double t, float scale)
        {
            double x = 16 * Math.Pow(Math.Sin(t), 3);
            double y = 13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t);
            // Center the curve vertically (y spans about -17..12).
            return new Vec2((float)(x * scale), (float)(-(y + 2.5) * scale));
        }

        /// <summary>Walks a heart outline by arc length and stands a truck across it every <paramref name="spacing"/>, pointing outward.</summary>
        static void Trace(LevelDef level, float scale, float spacing, ref int color)
        {
            const int Steps = 2000;
            var prev = HeartPoint(0, scale);
            float walked = spacing / 2f;
            for (int i = 1; i <= Steps; i++)
            {
                var p = HeartPoint(i * 2 * Math.PI / Steps, scale);
                var d = p - prev;
                walked += d.Length;
                prev = p;
                if (walked < spacing) continue;
                // Outward normal of a curve traced this way: rotate the tangent; flip if it points at the middle.
                var n = new Vec2(d.Z, -d.X);
                if (Geometry2D.Dot(n, p) < 0f) n = new Vec2(-n.X, -n.Z);
                float angle = (float)(Math.Atan2(n.X, -n.Z) * 180.0 / Math.PI);
                angle = (float)Math.Round(angle / 15.0) * 15f;
                if (angle < 0f) angle += 360f;
                // Keep sliding along the curve until a truck fits, so tight bends pack as densely as they can.
                if (TryAdd(level, p, angle, Colors[color % Colors.Length])) { color++; walked = 0f; }
            }
        }

        /// <summary>Adds a length-2 truck if it fits inside the lot without touching another truck or obstacle.</summary>
        static bool TryAdd(LevelDef level, Vec2 at, float angle, char color)
        {
            var t = new TruckDef
            {
                Id = level.Trucks.Count, Len = 2, Color = color, HasPose = true,
                FreePose = new TruckPose(Snap(at.X), Snap(at.Z), angle), Facing = TruckDef.FacingOf(angle),
            };
            var shape = t.Shape(level.Size);
            for (int k = 0; k < 4; k++)
                if (!level.Board.Contains(shape.Corner(k), 0.05f)) return false;
            foreach (var other in level.Trucks)
                if (Geometry2D.Overlaps(shape, other.Shape(level.Size))) return false;
            foreach (var o in level.Obstacles)
                if (Geometry2D.Overlaps(shape, o.Shape)) return false;
            level.Trucks.Add(t);
            return true;
        }

        /// <summary>Most units a sample vessel holds (the vessel shader shows up to 16 layers).</summary>
        const int MaxVesselUnits = 14;

        /// <summary>Fills 4+ vessels with exactly the trucks' oil (shuffled) and keeps the first order the solver wins.</summary>
        static LevelDef Finish(LevelDef level, int seed)
        {
            var units = new List<char>();
            foreach (var t in level.Trucks)
                for (int u = 0; u < t.Capacity; u++) units.Add(t.Color);
            int vessels = Math.Max(4, (units.Count + MaxVesselUnits - 1) / MaxVesselUnits);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var rng = new Random(seed * 1000 + attempt);
                for (int i = units.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (units[i], units[j]) = (units[j], units[i]);
                }
                level.Vessels.Clear();
                for (int v = 0, start = 0; v < vessels; v++)
                {
                    int count = (units.Count - start) / (vessels - v);
                    level.Vessels.Add(units.GetRange(start, count));
                    start += count;
                }
                var score = LevelSolver.Score(level, 200);
                if (score.Solve.Status != SolveStatus.Solved) continue;
                level.Solution = score.Solve.Solution;
                level.RandomWinRate = score.RandomWinRate;
                return level;
            }
            throw new InvalidOperationException($"No solvable vessel order found for the '{level.Pattern}' sample.");
        }

        static float Snap(float v) => (float)Math.Round(v / 0.05) * 0.05f;
    }
}
