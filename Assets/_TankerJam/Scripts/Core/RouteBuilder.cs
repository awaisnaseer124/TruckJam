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
        public Vec2 RingEntry(Vec2 from, Facing facing) => RingEntry(from, BoardLayout.Dir(facing));

        /// <summary>Where a truck at <paramref name="from"/> (inside the ring) driving along world direction
        /// <paramref name="dir"/> first meets the ring road. The result lies exactly on a ring line.</summary>
        public Vec2 RingEntry(Vec2 from, Vec2 dir)
        {
            float tx = dir.X > 1e-6f ? (rx - from.X) / dir.X : dir.X < -1e-6f ? (-rx - from.X) / dir.X : float.PositiveInfinity;
            float tz = dir.Z > 1e-6f ? (rb - from.Z) / dir.Z : dir.Z < -1e-6f ? (rt - from.Z) / dir.Z : float.PositiveInfinity;
            if (tx < tz)
            {
                float x = dir.X > 0f ? rx : -rx;
                return new Vec2(x, Clamp(from.Z + dir.Z * tx, rt, rb));
            }
            float z = dir.Z > 0f ? rb : rt;
            return new Vec2(Clamp(from.X + dir.X * tz, -rx, rx), z);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Lot → ring road → under the bay → up into the bay, parked at <paramref name="parkZ"/> (straight bays).</summary>
        public void ToBay(PolylinePath path, Vec2 from, Facing facing, int bay, float parkZ) =>
            ToBay(path, from, BoardLayout.Dir(facing), new Vec2(layout.BayX(bay), parkZ), new Vec2(0f, -1f));

        /// <summary>
        /// From a lot position along world heading <paramref name="dir"/> to the ring road, the shorter way round
        /// to below the bay, then into the bay ending at <paramref name="parkCenter"/> facing <paramref name="parkAxis"/>.
        /// Tilted bays are entered along their axis from an approach point behind the parked position.
        /// </summary>
        public void ToBay(PolylinePath path, Vec2 from, Vec2 dir, Vec2 parkCenter, Vec2 parkAxis)
        {
            path.Clear();
            var entry = RingEntry(from, dir);
            path.AddPoint(from);
            path.AddPoint(entry);

            bool straight = Math.Abs(parkAxis.X) < 1e-3f;
            Vec2 approach = straight ? parkCenter : parkCenter - parkAxis * ApproachLength;
            var foot = new Vec2(Clamp(approach.X, -rx, rx), rt);
            AddRingCorners(path, entry, foot);
            if (Math.Abs(entry.X - foot.X) > Tolerance || Math.Abs(entry.Z - foot.Z) > Tolerance)
                path.AddPoint(foot);
            if (!straight) path.AddPoint(approach);
            path.AddPoint(parkCenter);
            path.Build(layout.P.CornerRadius);
        }

        /// <summary>How far behind its parked position a truck lines up with a tilted bay before driving in.</summary>
        public const float ApproachLength = 2f;

        /// <summary>Bay → forward onto the exit road → off the nearest screen side (straight bays).</summary>
        public void Leave(PolylinePath path, Vec2 from) => Leave(path, from, new Vec2(0f, -1f));

        /// <summary>Bay → forward along <paramref name="axis"/> onto the exit road → off the nearest screen side.</summary>
        public void Leave(PolylinePath path, Vec2 from, Vec2 axis)
        {
            path.Clear();
            float exitZ = layout.P.ExitZ;
            float t = axis.Z < -1e-3f ? (exitZ - from.Z) / axis.Z : 0f;
            var onRoad = Math.Abs(axis.X) < 1e-3f ? new Vec2(from.X, exitZ) : new Vec2(from.X + axis.X * t, exitZ);
            float side = onRoad.X <= 0f ? -layout.P.ExitSideX : layout.P.ExitSideX;
            path.AddPoint(from);
            path.AddPoint(onRoad);
            path.AddPoint(side, exitZ);
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
