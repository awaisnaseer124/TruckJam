// World-space layout of a level, derived from the level itself (grid size, vessel count, bay count)
// instead of hand-placed scene objects. Defaults reproduce the web prototype exactly for a 7x7 lot,
// 4 vessels and 3 regular bays. Units: 1 = one grid cell. +z points toward the camera, so the bays and
// vessels sit at negative z (top of the screen).
// Handedness: the prototype is three.js (right-handed); Unity is left-handed, so a camera at +z looking
// toward -z sees +x on its LEFT. World x is therefore the mirror of the prototype's x (XSign = -1): grid
// column 0 and the VIP bay end up on the left of the screen, exactly as in the prototype.
using System;

namespace TankerJam.Core
{
    /// <summary>A point on the ground plane (world x, world z).</summary>
    public readonly struct Vec2
    {
        public readonly float X, Z;
        public Vec2(float x, float z) { X = x; Z = z; }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Z * s);
        public float Length => (float)Math.Sqrt(X * X + Z * Z);
        public override string ToString() => $"({X:0.###}, {Z:0.###})";
    }

    /// <summary>Spacing constants. Values match reference-prototype.html; tune through the LayoutConfig asset.</summary>
    [Serializable]
    public sealed class LayoutParams
    {
        public float LotZ0 = 0f;              // z of the lot's top edge
        public float RingMargin = 0.8f;       // ring road centerline distance outside the lot's side edge
        public float RingTopGap = 0.75f;      // ring road centerline distance above/below the lot
        public float RoadWidth = 1.2f;

        public float BaySpacing = 1.4f;
        public float BayWidth = 1.18f;
        public float BayLength = 4.1f;
        public float HoseZ = -3.75f;          // z of the hose above each bay
        public float BayPlaneOffsetZ = -0.3f; // bay outline center relative to HoseZ
        public float HeaderY = 2.6f;
        public float HeaderOffsetZ = -1.3f;   // header pipe z relative to HoseZ

        public float ExitZ = -7.1f;
        public float ExitSideX = 22f;
        public float PumpZ = -9.3f;

        public float VesselSpacing = 2.6f;
        public float VesselZ = -11.6f;
        public float VesselRadius = 0.74f;
        public float VesselUnitHeight = 0.42f;
        public float VesselBaseY = 1.0f;
        public float ManifoldY = 0.55f;

        public float CornerRadius = 0.7f;     // route corner rounding (prototype rounded(..., .7))
        public float LeaveCornerRadius = 0.8f;
    }

    public sealed class BoardLayout
    {
        /// <summary>World x = XSign * prototype x. See the file header.</summary>
        public const float XSign = -1f;

        public readonly LayoutParams P;
        public readonly int Size, BayCount, VesselCount, MaxVesselUnits;

        public readonly float RingX, RingTop, RingBottom;
        readonly float[] bayX;
        readonly float[] vesselX;

        public BoardLayout(LevelDef level, LayoutParams p = null)
        {
            P = p ?? new LayoutParams();
            Size = level.Size;
            BayCount = level.Slots + 2;
            VesselCount = level.Vessels.Count;
            MaxVesselUnits = Math.Max(1, level.MaxVesselHeight);

            RingX = Size / 2f + P.RingMargin;
            RingTop = P.LotZ0 - P.RingTopGap;
            RingBottom = P.LotZ0 + Size + P.RingTopGap;

            // Keep the outer bays inside the ring road.
            float maxHalf = RingX - P.BayWidth;
            float bayStep = BayCount > 1 ? Math.Min(P.BaySpacing, 2f * maxHalf / (BayCount - 1)) : 0f;
            bayX = Centered(BayCount, bayStep);

            float maxVesselHalf = RingX + P.RoadWidth / 2f - P.VesselRadius;
            float vesselStep = VesselCount > 1 ? Math.Min(P.VesselSpacing, 2f * maxVesselHalf / (VesselCount - 1)) : 0f;
            vesselX = Centered(VesselCount, vesselStep);
        }

        static float[] Centered(int count, float step)
        {
            var xs = new float[count];
            for (int i = 0; i < count; i++) xs[i] = XSign * (i - (count - 1) / 2f) * step;
            return xs;
        }

        // ---------- lot ----------

        /// <summary>Center of grid cell (x, y). y grows toward the camera.</summary>
        public Vec2 CellCenter(int x, int y) => new Vec2(XSign * (x - (Size - 1) / 2f), P.LotZ0 + y + 0.5f);

        public Vec2 LotCenter => new Vec2(0f, P.LotZ0 + Size / 2f);

        /// <summary>Center of the truck's footprint in the lot.</summary>
        public Vec2 TruckHome(TruckDef t)
        {
            float half = (t.Len - 1) / 2f;
            var c = CellCenter(t.X, t.Y);
            return t.Horizontal ? new Vec2(c.X + XSign * half, c.Z) : new Vec2(c.X, c.Z + half);
        }

        /// <summary>Unit direction a facing points to on the ground.</summary>
        public static Vec2 Dir(Facing f)
        {
            switch (f)
            {
                case Facing.U: return new Vec2(0, -1);
                case Facing.D: return new Vec2(0, 1);
                case Facing.L: return new Vec2(-XSign, 0);
                default: return new Vec2(XSign, 0);
            }
        }

        // ---------- bays and pipes ----------

        public float BayX(int bay) => bayX[bay];
        public float BayPlaneZ => P.HoseZ + P.BayPlaneOffsetZ;
        public float HeaderZ => P.HoseZ + P.HeaderOffsetZ;
        public float BayRowMinX => Math.Min(bayX[0], bayX[BayCount - 1]);
        public float BayRowMaxX => Math.Max(bayX[0], bayX[BayCount - 1]);

        /// <summary>World z where a truck parks so that a point <paramref name="hatchOffset"/> ahead of its
        /// center (along its length, toward the cab) sits under the hose. Trucks park nose-up (facing −z).</summary>
        public float ParkZ(float hatchOffset) => P.HoseZ + hatchOffset;

        // ---------- vessels ----------

        public float VesselX(int vessel) => vesselX[vessel];
        public float VesselHeight => MaxVesselUnits * P.VesselUnitHeight + 0.45f;
        public Vec2 Pump => new Vec2(0f, P.PumpZ);

        // ---------- bounds ----------

        /// <summary>Ground-plane extents of everything that must be on screen.</summary>
        public void GroundBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float half = RingX + P.RoadWidth / 2f;
            if (VesselCount > 0) half = Math.Max(half, Math.Abs(vesselX[0]) + P.VesselRadius + 0.2f);
            minX = -half; maxX = half;
            minZ = P.VesselZ - P.VesselRadius - 0.2f;
            maxZ = RingBottom + P.RoadWidth / 2f;
        }

        /// <summary>Highest point of the static scene (vessel lips), for camera fitting.</summary>
        public float TopY => P.VesselBaseY + VesselHeight + 0.2f;
    }
}
