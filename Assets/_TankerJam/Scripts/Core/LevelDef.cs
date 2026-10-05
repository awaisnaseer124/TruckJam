// Level description types. Pure data: loaded once from JSON, never mutated during play.
//
// Board space (shared by grid and free-form levels): origin at the board center, +x to screen right,
// +z toward the camera (the grid's +y), angles in degrees with 0 = toward the bays (-z), clockwise.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    /// <summary>Grid facing. U points toward the bays (−z), D toward the camera.</summary>
    public enum Facing { U, D, L, R }

    public readonly struct Cell
    {
        public readonly int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>A truck's placement on the board (center + heading).</summary>
    public readonly struct TruckPose
    {
        public readonly float X, Z, Angle;
        public TruckPose(float x, float z, float angle) { X = x; Z = z; Angle = angle; }
        public Vec2 Center => new Vec2(X, Z);
        public Vec2 Heading => Geometry2D.Heading(Angle);
    }

    public sealed class TruckDef
    {
        /// <summary>Collision width (visual body is 0.76): neighbors one lane apart never collide.</summary>
        public const float CollisionWidth = 0.8f;
        /// <summary>Collision length = cells minus this (visual body is cells - 0.12).</summary>
        public const float LengthClearance = 0.1f;

        public int Id, Len;
        public char Color;

        // Grid placement (version 1 levels): top-left cell and facing.
        public int X, Y;
        public Facing Facing;

        // Free-form placement (version 2 levels). When HasPose is false the pose comes from the grid fields.
        public bool HasPose;
        public TruckPose FreePose;

        /// <summary>Units of oil the truck holds: length 2, 3, 4 hold 2, 4, 6. Must match the solver.</summary>
        public int Capacity => CapacityFor(Len);

        public static int CapacityFor(int len) => len == 2 ? 2 : len == 3 ? 4 : 6;

        public bool Horizontal => Facing == Facing.L || Facing == Facing.R;

        /// <summary>The i-th cell covered by the truck, from its top-left cell (grid levels only).</summary>
        public Cell CellAt(int i) => Horizontal ? new Cell(X + i, Y) : new Cell(X, Y + i);

        /// <summary>The cell at the front of the truck, in the direction it faces (grid levels only).</summary>
        public Cell Head()
        {
            bool frontIsLast = Facing == Facing.D || Facing == Facing.R;
            return CellAt(frontIsLast ? Len - 1 : 0);
        }

        /// <summary>Grid step for one cell of movement in the facing direction.</summary>
        public static void Step(Facing f, out int dx, out int dy)
        {
            dx = 0; dy = 0;
            switch (f)
            {
                case Facing.U: dy = -1; break;
                case Facing.D: dy = 1; break;
                case Facing.L: dx = -1; break;
                case Facing.R: dx = 1; break;
            }
        }

        public static float AngleOf(Facing f) => f == Facing.U ? 0f : f == Facing.R ? 90f : f == Facing.D ? 180f : 270f;

        /// <summary>Nearest grid facing for an angle (used by grid-era code paths and routes).</summary>
        public static Facing FacingOf(float angle)
        {
            float a = ((angle % 360f) + 360f) % 360f;
            if (a < 45f || a >= 315f) return Facing.U;
            if (a < 135f) return Facing.R;
            if (a < 225f) return Facing.D;
            return Facing.L;
        }

        /// <summary>Board-space placement: the explicit pose, or the center of the grid footprint.</summary>
        public TruckPose GetPose(int gridSize)
        {
            if (HasPose) return FreePose;
            float half = gridSize / 2f;
            float cx = Horizontal ? X + Len / 2f : X + 0.5f;
            float cz = Horizontal ? Y + 0.5f : Y + Len / 2f;
            return new TruckPose(cx - half, cz - half, AngleOf(Facing));
        }

        /// <summary>Collision rectangle in board space.</summary>
        public Obb Shape(int gridSize)
        {
            var p = GetPose(gridSize);
            return new Obb(p.Center, p.Heading, (Len - LengthClearance) / 2f, CollisionWidth / 2f);
        }
    }

    /// <summary>Fixed blocker (cone, planter). Square, axis aligned, in board space.</summary>
    public readonly struct Obstacle
    {
        public readonly float X, Z, HalfSize;
        public Obstacle(float x, float z, float halfSize) { X = x; Z = z; HalfSize = halfSize; }
        public Obb Shape => new Obb(new Vec2(X, Z), new Vec2(0, -1), HalfSize, HalfSize);

        /// <summary>Half size of a grid cone: blocks its lane, never the neighbor lanes.</summary>
        public const float ConeHalfSize = 0.35f;
    }

    public enum BoardKind { Square, Circle, RoundedRect }

    /// <summary>The lot's outline in board space (centered on the origin).</summary>
    public readonly struct BoardShape
    {
        public readonly BoardKind Kind;
        public readonly float Width, Height, Radius;

        public BoardShape(BoardKind kind, float width, float height, float radius)
        {
            Kind = kind; Width = width; Height = height; Radius = radius;
        }

        public static BoardShape Square(int size) => new BoardShape(BoardKind.Square, size, size, 0f);
        public static BoardShape Circle(float radius) => new BoardShape(BoardKind.Circle, radius * 2f, radius * 2f, radius);
        public static BoardShape Rounded(float width, float height, float corner) => new BoardShape(BoardKind.RoundedRect, width, height, corner);

        /// <summary>Whether a board-space point lies inside the outline.</summary>
        public bool Contains(Vec2 p, float margin = 0f)
        {
            switch (Kind)
            {
                case BoardKind.Circle:
                    return p.X * p.X + p.Z * p.Z <= (Radius - margin) * (Radius - margin);
                case BoardKind.RoundedRect:
                {
                    float hx = Width / 2f - margin, hz = Height / 2f - margin, r = Math.Max(0f, Radius - margin);
                    float dx = Math.Abs(p.X) - (hx - r), dz = Math.Abs(p.Z) - (hz - r);
                    if (dx <= 0f || dz <= 0f) return Math.Abs(p.X) <= hx && Math.Abs(p.Z) <= hz;
                    return dx * dx + dz * dz <= r * r;
                }
                default:
                    return Math.Abs(p.X) <= Width / 2f - margin && Math.Abs(p.Z) <= Height / 2f - margin;
            }
        }
    }

    public sealed class LevelDef
    {
        /// <summary>1 = grid level (cells + U/D/L/R), 2 = free-form level (poses + board shape).</summary>
        public int Version = 1;
        /// <summary>Grid size (version 1). For version 2 levels, the board's bounding size rounded up.</summary>
        public int Size = 7;
        BoardShape board = BoardShape.Square(7);
        /// <summary>The lot outline. Grid levels always use their Size x Size square.</summary>
        public BoardShape Board
        {
            get => IsGrid ? BoardShape.Square(Size) : board;
            set => board = value;
        }
        public List<TruckDef> Trucks = new List<TruckDef>();
        /// <summary>Grid cones (version 1).</summary>
        public List<Cell> Cones = new List<Cell>();
        /// <summary>Free-form obstacles (version 2).</summary>
        public List<Obstacle> Obstacles = new List<Obstacle>();
        /// <summary>One list per vessel, bottom layer first, one entry per unit.</summary>
        public List<List<char>> Vessels = new List<List<char>>();
        /// <summary>Regular bays open at the start (VIP and the extra bay are on top of this).</summary>
        public int Slots = 3;
        /// <summary>Truck ids in a winning tap order, from the solver. May be empty.</summary>
        public List<int> Solution = new List<int>();
        /// <summary>Share of random games that win (solver metadata, 0..1). Negative when unknown.</summary>
        public float RandomWinRate = -1f;

        // Curve metadata ("meta" block). Optional.
        /// <summary>1-based position in the level curve, or 0 when unknown.</summary>
        public int Index;
        public int Tier;
        /// <summary>Marked Hard in the sawtooth (double coins, "Hard" badge).</summary>
        public bool Hard;
        /// <summary>Feature introduced on this level ("vip", "extra"), or null.</summary>
        public string Introduces;
        /// <summary>Layout pattern name for generated free-form levels (e.g. "rings-6"), or null.</summary>
        public string Pattern;

        public bool IsGrid => Version < 2;

        /// <summary>All blockers in board space: free-form obstacles plus grid cones.</summary>
        public void CollectObstacles(List<Obb> into)
        {
            into.Clear();
            foreach (var o in Obstacles) into.Add(o.Shape);
            float half = Size / 2f;
            foreach (var c in Cones)
                into.Add(new Obstacle(c.X + 0.5f - half, c.Y + 0.5f - half, Obstacle.ConeHalfSize).Shape);
        }

        public int MaxVesselHeight
        {
            get
            {
                int max = 0;
                foreach (var v in Vessels) if (v.Count > max) max = v.Count;
                return max;
            }
        }
    }
}
