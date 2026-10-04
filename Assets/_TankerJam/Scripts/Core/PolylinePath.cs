// Ground path with rounded corners, sampled by arc length. Port of rounded()/at() from the web prototype.
// Reusable: Clear() + AddPoint() + Build() rewrites the same buffers, so steady-state use allocates nothing.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public sealed class PolylinePath
    {
        const float Epsilon = 1e-4f;

        readonly List<Vec2> control = new List<Vec2>(16);
        readonly List<Vec2> points = new List<Vec2>(64);
        readonly List<float> cumulative = new List<float>(64);

        public float Length { get; private set; }
        public int PointCount => points.Count;
        public Vec2 Start => points[0];
        public Vec2 End => points[points.Count - 1];

        public void Clear()
        {
            control.Clear();
            points.Clear();
            cumulative.Clear();
            Length = 0f;
        }

        public void AddPoint(Vec2 p) => control.Add(p);
        public void AddPoint(float x, float z) => control.Add(new Vec2(x, z));

        /// <summary>
        /// Builds the sampled path from the control points, replacing every interior corner with a quadratic
        /// curve of the given radius (clamped to half of each adjacent segment), like the prototype's rounded().
        /// </summary>
        public void Build(float cornerRadius, int steps = 10)
        {
            points.Clear();
            cumulative.Clear();
            if (control.Count == 0) throw new InvalidOperationException("Path has no points.");

            Push(control[0]);
            for (int i = 1; i < control.Count - 1; i++)
            {
                Vec2 a = control[i - 1], p = control[i], b = control[i + 1];
                float la = (p - a).Length, lb = (b - p).Length;
                float r = Math.Min(cornerRadius, Math.Min(la / 2f, lb / 2f));
                if (r < 1e-3f) { Push(p); continue; }
                Vec2 s = p + (a - p) * (r / la);
                Vec2 e = p + (b - p) * (r / lb);
                for (int k = 0; k <= steps; k++)
                {
                    float u = (float)k / steps, w0 = (1 - u) * (1 - u), w1 = 2 * u * (1 - u), w2 = u * u;
                    Push(new Vec2(w0 * s.X + w1 * p.X + w2 * e.X, w0 * s.Z + w1 * p.Z + w2 * e.Z));
                }
            }
            if (control.Count > 1) Push(control[control.Count - 1]);
            Length = cumulative[cumulative.Count - 1];
        }

        void Push(Vec2 p)
        {
            if (points.Count == 0)
            {
                points.Add(p);
                cumulative.Add(0f);
                return;
            }
            float d = (p - points[points.Count - 1]).Length;
            if (d <= Epsilon) return;
            points.Add(p);
            cumulative.Add(cumulative[cumulative.Count - 1] + d);
        }

        /// <summary>
        /// Position and heading at arc length <paramref name="s"/> (clamped to the path).
        /// <paramref name="cursor"/> caches the segment between calls; start it at 0 and pass the same variable
        /// while sampling forward, so lookups are O(1) amortized. Heading is a Unity yaw in radians:
        /// atan2(dx, dz), 0 = +z, π/2 = +x.
        /// </summary>
        public void Sample(float s, ref int cursor, out Vec2 position, out float heading)
        {
            if (points.Count == 1)
            {
                position = points[0];
                heading = 0f;
                return;
            }
            if (s < 0f) s = 0f;
            else if (s > Length) s = Length;

            int i = Math.Max(1, Math.Min(cursor, points.Count - 1));
            while (i > 1 && cumulative[i - 1] > s) i--;
            while (i < points.Count - 1 && cumulative[i] < s) i++;
            cursor = i;

            Vec2 a = points[i - 1], b = points[i];
            float seg = cumulative[i] - cumulative[i - 1];
            float u = seg > 0f ? (s - cumulative[i - 1]) / seg : 0f;
            position = new Vec2(a.X + (b.X - a.X) * u, a.Z + (b.Z - a.Z) * u);
            heading = (float)Math.Atan2(b.X - a.X, b.Z - a.Z);
        }

        /// <summary>Convenience overload without a cursor (linear search).</summary>
        public Vec2 PositionAt(float s)
        {
            int c = 1;
            Sample(s, ref c, out var p, out _);
            return p;
        }
    }
}
