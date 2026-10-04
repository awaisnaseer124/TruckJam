// Truck routes on the ground plane. Port of ringPath/toSlotPath/leavePath from the web prototype:
// leave the lot the way the truck faces, follow the ring road the shorter way round to the top road
// below the target bay, then drive up into the bay.
using System;

namespace TankerJam.Core
{
    public sealed class RouteBuilder
    {
        const float Tolerance = 1e-4f;

        readonly BoardLayout layout;
        readonly float rx, rt, rb, perimeter;

        // Ring corners clockwise from the top-left, with their perimeter coordinate.
        readonly Vec2[] corners;
        readonly float[] cornerS;
        // Scratch for corners picked by RingPoints (max 4).
        readonly float[] pickD = new float[4];
        readonly Vec2[] pickP = new Vec2[4];

        public RouteBuilder(BoardLayout layout)
        {
            this.layout = layout;
            rx = layout.RingX;
            rt = layout.RingTop;
            rb = layout.RingBottom;
            float h = rb - rt;
            perimeter = 2f * (2f * rx) + 2f * h;
            corners = new[] { new Vec2(-rx, rt), new Vec2(rx, rt), new Vec2(rx, rb), new Vec2(-rx, rb) };
            cornerS = new[] { 0f, 2f * rx, 2f * rx + h, 4f * rx + h };
        }

        /// <summary>Where a truck at <paramref name="from"/> facing <paramref name="facing"/> joins the ring road.</summary>
        public Vec2 RingEntry(Vec2 from, Facing facing)
        {
            switch (facing)
            {
                case Facing.U: return new Vec2(from.X, rt);
                case Facing.D: return new Vec2(from.X, rb);
                case Facing.L: return new Vec2(-BoardLayout.XSign * rx, from.Z);
                default: return new Vec2(BoardLayout.XSign * rx, from.Z);
            }
        }

        /// <summary>Lot → ring road → under the bay → up into the bay, parked at <paramref name="parkZ"/>.</summary>
        public void ToBay(PolylinePath path, Vec2 from, Facing facing, int bay, float parkZ)
        {
            path.Clear();
            var entry = RingEntry(from, facing);
            float bx = layout.BayX(bay);
            var target = new Vec2(bx, rt);

            path.AddPoint(from);
            path.AddPoint(entry);
            AddRingCorners(path, entry, target);
            if (Math.Abs(entry.X - target.X) > Tolerance || Math.Abs(entry.Z - target.Z) > Tolerance)
                path.AddPoint(target);
            path.AddPoint(bx, parkZ);
            path.Build(layout.P.CornerRadius);
        }

        /// <summary>Bay → forward onto the exit road → off the nearest screen side.</summary>
        public void Leave(PolylinePath path, Vec2 from)
        {
            path.Clear();
            float side = from.X <= 0f ? -layout.P.ExitSideX : layout.P.ExitSideX;
            path.AddPoint(from);
            path.AddPoint(from.X, layout.P.ExitZ);
            path.AddPoint(side, layout.P.ExitZ);
            path.Build(layout.P.LeaveCornerRadius);
        }

        /// <summary>Perimeter coordinate of a point on the ring, clockwise from the top-left corner.</summary>
        float PerimeterS(Vec2 p)
        {
            if (Math.Abs(p.Z - rt) < Tolerance) return p.X + rx;
            if (Math.Abs(p.X - rx) < Tolerance) return 2f * rx + (p.Z - rt);
            if (Math.Abs(p.Z - rb) < Tolerance) return 2f * rx + (rb - rt) + (rx - p.X);
            return 4f * rx + (rb - rt) + (rb - p.Z);
        }

        static float Mod(float a, float m) => ((a % m) + m) % m;

        /// <summary>Adds the ring corners passed between a and b, going the shorter way round.</summary>
        void AddRingCorners(PolylinePath path, Vec2 a, Vec2 b)
        {
            float sa = PerimeterS(a), sb = PerimeterS(b);
            float fw = Mod(sb - sa, perimeter), bw = Mod(sa - sb, perimeter);
            int n = 0;
            for (int c = 0; c < 4; c++)
            {
                float d = fw <= bw ? Mod(cornerS[c] - sa, perimeter) : Mod(sa - cornerS[c], perimeter);
                float limit = fw <= bw ? fw : bw;
                if (d > Tolerance && d < limit - Tolerance)
                {
                    pickD[n] = d;
                    pickP[n] = corners[c];
                    n++;
                }
            }
            // Insertion sort by distance along the ring (n <= 4).
            for (int i = 1; i < n; i++)
                for (int j = i; j > 0 && pickD[j] < pickD[j - 1]; j--)
                {
                    (pickD[j], pickD[j - 1]) = (pickD[j - 1], pickD[j]);
                    (pickP[j], pickP[j - 1]) = (pickP[j - 1], pickP[j]);
                }
            for (int i = 0; i < n; i++) path.AddPoint(pickP[i]);
        }
    }
}
