// 2D oriented rectangles on the board plane and the two queries the lot needs:
//  - Overlaps: do two rectangles penetrate (Separating Axis Test)?
//  - TimeOfImpact: sliding one rectangle along a direction, how far until it first penetrates another?
// Touching is not a hit: penetration must exceed Epsilon, so trucks may sit flush against each other.
using System;

namespace TankerJam.Core
{
    /// <summary>Oriented rectangle: center, unit length axis, half extents along the axis and across it.</summary>
    public readonly struct Obb
    {
        public readonly Vec2 Center;
        /// <summary>Unit vector along the length (the heading for a truck).</summary>
        public readonly Vec2 Axis;
        public readonly float HalfLength, HalfWidth;

        public Obb(Vec2 center, Vec2 axis, float halfLength, float halfWidth)
        {
            Center = center; Axis = axis; HalfLength = halfLength; HalfWidth = halfWidth;
        }

        /// <summary>Unit vector across the length (axis rotated 90 degrees).</summary>
        public Vec2 Side => new Vec2(-Axis.Z, Axis.X);

        public Vec2 Corner(int i)
        {
            float a = (i == 0 || i == 3) ? HalfLength : -HalfLength;
            float b = (i < 2) ? HalfWidth : -HalfWidth;
            var s = Side;
            return new Vec2(Center.X + Axis.X * a + s.X * b, Center.Z + Axis.Z * a + s.Z * b);
        }

        /// <summary>Half the extent of this rectangle projected on unit axis n.</summary>
        public float Radius(Vec2 n) => HalfLength * Math.Abs(Geometry2D.Dot(Axis, n)) + HalfWidth * Math.Abs(Geometry2D.Dot(Side, n));
    }

    public static class Geometry2D
    {
        /// <summary>Minimum penetration that counts as overlap (board units, 1 unit = 1 cell).</summary>
        public const float Epsilon = 0.01f;

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;

        /// <summary>Unit heading for an angle in degrees: 0 = toward the bays (-z), clockwise (90 = +x).</summary>
        public static Vec2 Heading(float degrees)
        {
            double r = degrees * Math.PI / 180.0;
            return new Vec2((float)Math.Sin(r), (float)-Math.Cos(r));
        }

        public static bool Overlaps(in Obb a, in Obb b)
        {
            Vec2 d = b.Center - a.Center;
            return !Separated(d, a.Axis, a, b) && !Separated(d, a.Side, a, b)
                && !Separated(d, b.Axis, a, b) && !Separated(d, b.Side, a, b);
        }

        static bool Separated(Vec2 d, Vec2 n, in Obb a, in Obb b) =>
            Math.Abs(Dot(d, n)) >= a.Radius(n) + b.Radius(n) - Epsilon;

        /// <summary>
        /// Slides <paramref name="moving"/> along unit direction <paramref name="dir"/> by t >= 0. Returns true and
        /// the smallest t at which it penetrates <paramref name="target"/>, or false if it never does.
        /// Exact for convex shapes: intersection of the overlap intervals on the four separating axes.
        /// </summary>
        public static bool TimeOfImpact(in Obb moving, Vec2 dir, in Obb target, out float t)
        {
            float enter = 0f, exit = float.PositiveInfinity;
            if (!Narrow(moving.Axis, moving, dir, target, ref enter, ref exit) ||
                !Narrow(moving.Side, moving, dir, target, ref enter, ref exit) ||
                !Narrow(target.Axis, moving, dir, target, ref enter, ref exit) ||
                !Narrow(target.Side, moving, dir, target, ref enter, ref exit))
            {
                t = 0f;
                return false;
            }
            t = enter;
            return enter < exit;
        }

        /// <summary>Narrows [enter, exit] to the t range where the shapes overlap on axis n. False if never.</summary>
        static bool Narrow(Vec2 n, in Obb a, Vec2 dir, in Obb b, ref float enter, ref float exit)
        {
            float gap = Dot(b.Center - a.Center, n);           // center distance on the axis at t = 0
            float reach = a.Radius(n) + b.Radius(n) - Epsilon;  // overlap when |gap - v t| < reach
            float v = Dot(dir, n);
            if (Math.Abs(v) < 1e-6f) return Math.Abs(gap) < reach; // no motion along this axis
            float t0 = (gap - reach) / v, t1 = (gap + reach) / v;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > enter) enter = t0;
            if (t1 < exit) exit = t1;
            return enter < exit;
        }
    }
}
