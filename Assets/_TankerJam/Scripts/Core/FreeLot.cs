// The parking lot as rotated rectangles. A truck can leave when sliding it forward along its heading never
// penetrates another truck still on the board or an obstacle; the bump distance is the slide length to the
// first contact. Works for any angle; for grid levels it gives exactly the grid answers (see tests).
using System.Collections.Generic;

namespace TankerJam.Core
{
    public sealed class FreeLot : ILot
    {
        readonly Obb[] trucks;
        readonly bool[] present;
        readonly List<Obb> obstacles = new List<Obb>();
        readonly BoardShape board;

        public FreeLot(LevelDef level)
        {
            trucks = new Obb[level.Trucks.Count];
            present = new bool[level.Trucks.Count];
            for (int i = 0; i < trucks.Length; i++)
            {
                trucks[i] = level.Trucks[i].Shape(level.Size);
                present[i] = true;
            }
            level.CollectObstacles(obstacles);
            board = level.Board;
        }

        public Obb TruckShape(int id) => trucks[id];
        public bool IsPresent(int id) => present[id];

        public void Remove(int truckId) => present[truckId] = false;

        public ExitResult CheckExit(int truckId)
        {
            var me = trucks[truckId];
            var dir = me.Axis;
            float first = float.PositiveInfinity;

            for (int i = 0; i < trucks.Length; i++)
            {
                if (i == truckId || !present[i]) continue;
                if (Geometry2D.TimeOfImpact(me, dir, trucks[i], out float t) && t < first) first = t;
            }
            for (int i = 0; i < obstacles.Count; i++)
                if (Geometry2D.TimeOfImpact(me, dir, obstacles[i], out float t) && t < first) first = t;

            return float.IsPositiveInfinity(first)
                ? new ExitResult(true, DistanceToEdge(me))
                : new ExitResult(false, first);
        }

        /// <summary>From the truck's front face along its heading to the board outline (0 if already at it).</summary>
        float DistanceToEdge(in Obb me)
        {
            var d = me.Axis;
            var p = new Vec2(me.Center.X + d.X * me.HalfLength, me.Center.Z + d.Z * me.HalfLength);
            float t;
            if (board.Kind == BoardKind.Circle)
            {
                // |p + t d| = R  ->  t^2 + 2 (p.d) t + |p|^2 - R^2 = 0, take the positive root.
                float b = Geometry2D.Dot(p, d), c = Geometry2D.Dot(p, p) - board.Radius * board.Radius;
                float disc = b * b - c;
                t = disc > 0f ? -b + (float)System.Math.Sqrt(disc) : 0f;
            }
            else
            {
                float hx = board.Width / 2f, hz = board.Height / 2f;
                float tx = d.X > 1e-6f ? (hx - p.X) / d.X : d.X < -1e-6f ? (-hx - p.X) / d.X : float.PositiveInfinity;
                float tz = d.Z > 1e-6f ? (hz - p.Z) / d.Z : d.Z < -1e-6f ? (-hz - p.Z) / d.Z : float.PositiveInfinity;
                t = System.Math.Min(tx, tz);
            }
            return t > 0f ? t : 0f;
        }
    }
}
