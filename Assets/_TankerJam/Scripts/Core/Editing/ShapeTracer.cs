// Stands trucks along an outline (ring, heart, star, ...) on the square lot: the editor's "trace a shape"
// tool and the seed of the F5 shape generator. Walks the outline by arc length and drops a truck every
// `Spacing` units where it fits; trucks that would overlap are skipped and the walk slides on, so tight
// bends pack as densely as they can.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public enum TraceShape { Ring, Heart, Star, Square, Diamond, Triangle }

    /// <summary>Which way the traced trucks point relative to the outline.</summary>
    public enum TraceFacing { Outward, Inward, Along, AlongReverse }

    public sealed class TraceOptions
    {
        public TraceShape Shape = TraceShape.Ring;
        public TraceFacing Facing = TraceFacing.Outward;
        /// <summary>Half extent of the outline in board units (radius for a ring).</summary>
        public float Size = 4f;
        public float CenterX, CenterZ;
        /// <summary>Distance along the outline between trucks.</summary>
        public float Spacing = 0.9f;
        public int Len = 2;
        /// <summary>Headings snap to this many degrees (15 in the data, 45 reads cleanest).</summary>
        public float AngleStep = 15f;
        /// <summary>Colors are dealt in this order, one truck after another.</summary>
        public string Colors = "PYCG";
    }

    public static class ShapeTracer
    {
        const int Steps = 2400;

        /// <summary>Point on the unit outline (extent about 1) at parameter u in [0, 1). Lobes/top point up the screen (-z).</summary>
        public static Vec2 UnitPoint(TraceShape shape, double u)
        {
            double t = u * 2 * Math.PI;
            switch (shape)
            {
                case TraceShape.Ring:
                    return new Vec2((float)Math.Sin(t), (float)-Math.Cos(t));
                case TraceShape.Heart:
                {
                    double x = 16 * Math.Pow(Math.Sin(t), 3);
                    double y = 13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t);
                    // y spans about -17..12; center it and scale uniformly so the width is 2.
                    return new Vec2((float)(x / 16), (float)(-(y + 2.5) / 16));
                }
                case TraceShape.Star:
                    return OnPolygon(StarVertices, u);
                case TraceShape.Square:
                    return OnPolygon(SquareVertices, u);
                case TraceShape.Diamond:
                    return OnPolygon(DiamondVertices, u);
                default:
                    return OnPolygon(TriangleVertices, u);
            }
        }

        static readonly Vec2[] SquareVertices = { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 1), new Vec2(-1, 1) };
        static readonly Vec2[] DiamondVertices = { new Vec2(0, -1), new Vec2(1, 0), new Vec2(0, 1), new Vec2(-1, 0) };
        static readonly Vec2[] TriangleVertices = { new Vec2(0, -1), new Vec2(0.866f, 0.5f), new Vec2(-0.866f, 0.5f) };
        static readonly Vec2[] StarVertices = BuildStar();

        static Vec2[] BuildStar()
        {
            var v = new Vec2[10];
            for (int i = 0; i < 10; i++)
            {
                double a = i * Math.PI / 5;
                float r = i % 2 == 0 ? 1f : 0.45f;
                v[i] = new Vec2((float)Math.Sin(a) * r, (float)-Math.Cos(a) * r);
            }
            return v;
        }

        /// <summary>Point at fraction u of a closed polygon's perimeter.</summary>
        static Vec2 OnPolygon(Vec2[] v, double u)
        {
            double total = 0;
            for (int i = 0; i < v.Length; i++) total += (v[(i + 1) % v.Length] - v[i]).Length;
            double target = (u - Math.Floor(u)) * total;
            for (int i = 0; i < v.Length; i++)
            {
                var a = v[i];
                var b = v[(i + 1) % v.Length];
                double len = (b - a).Length;
                if (target <= len || i == v.Length - 1)
                {
                    float f = (float)(len > 0 ? Math.Min(1.0, target / len) : 0);
                    return a + (b - a) * f;
                }
                target -= len;
            }
            return v[0];
        }

        /// <summary>Adds trucks along the outline to <paramref name="draft"/>; returns the trucks added.</summary>
        public static List<DraftTruck> Trace(LevelDraft draft, TraceOptions o)
        {
            var added = new List<DraftTruck>();
            var center = new Vec2(o.CenterX, o.CenterZ);
            Vec2 At(int i) => center + UnitPoint(o.Shape, (double)i / Steps) * o.Size;
            string colors = string.IsNullOrEmpty(o.Colors) ? "P" : o.Colors;

            var prev = At(0);
            float walked = o.Spacing; // try a truck right at the start
            for (int i = 1; i <= Steps; i++)
            {
                var p = At(i);
                var d = p - prev;
                var at = prev;
                prev = p;
                walked += d.Length;
                if (walked < o.Spacing || d.Length < 1e-6f) continue;

                float angle = HeadingFor(d, at - center, o.Facing);
                angle = (float)Math.Round(angle / o.AngleStep) * o.AngleStep;
                var t = draft.TryAdd(at.X, at.Z, angle, o.Len, colors[added.Count % colors.Length]);
                if (t == null) continue;
                // Don't close the loop onto the first truck: TryAdd already rejects overlaps.
                added.Add(t);
                walked = 0f;
            }
            return added;
        }

        /// <summary>Heading (degrees, board convention) for a truck on the outline with tangent <paramref name="tangent"/>.</summary>
        static float HeadingFor(Vec2 tangent, Vec2 fromCenter, TraceFacing facing)
        {
            Vec2 dir;
            if (facing == TraceFacing.Along) dir = tangent;
            else if (facing == TraceFacing.AlongReverse) dir = new Vec2(-tangent.X, -tangent.Z);
            else
            {
                var n = new Vec2(tangent.Z, -tangent.X);
                if (Geometry2D.Dot(n, fromCenter) < 0f) n = new Vec2(-n.X, -n.Z);
                dir = facing == TraceFacing.Outward ? n : new Vec2(-n.X, -n.Z);
            }
            // Inverse of Geometry2D.Heading: (sin a, -cos a).
            return LevelDraft.Norm((float)(Math.Atan2(dir.X, -dir.Z) * 180.0 / Math.PI));
        }
    }
}
