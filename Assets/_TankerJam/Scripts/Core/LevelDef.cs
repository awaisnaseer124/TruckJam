// Level description types. Pure data: loaded once from JSON, never mutated during play.
using System.Collections.Generic;

namespace TankerJam.Core
{
    /// <summary>Truck facing. U points toward the bays (−z in world space), D toward the camera.</summary>
    public enum Facing { U, D, L, R }

    public readonly struct Cell
    {
        public readonly int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public override string ToString() => $"({X},{Y})";
    }

    public sealed class TruckDef
    {
        public int Id, X, Y, Len;
        public Facing Facing;
        public char Color;

        /// <summary>Units of oil the truck holds: length 2, 3, 4 hold 2, 4, 6. Must match the Python solver.</summary>
        public int Capacity => CapacityFor(Len);

        public static int CapacityFor(int len) => len == 2 ? 2 : len == 3 ? 4 : 6;

        public bool Horizontal => Facing == Facing.L || Facing == Facing.R;

        /// <summary>The i-th cell covered by the truck, from its top-left cell.</summary>
        public Cell CellAt(int i) => Horizontal ? new Cell(X + i, Y) : new Cell(X, Y + i);

        /// <summary>The cell at the front of the truck, in the direction it faces.</summary>
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
    }

    public sealed class LevelDef
    {
        public int Size = 7;
        public List<TruckDef> Trucks = new List<TruckDef>();
        public List<Cell> Cones = new List<Cell>();
        /// <summary>One list per vessel, bottom layer first, one entry per unit.</summary>
        public List<List<char>> Vessels = new List<List<char>>();
        /// <summary>Regular bays open at the start (VIP and the extra bay are on top of this).</summary>
        public int Slots = 3;
        /// <summary>Truck ids in a winning tap order, from the solver. May be empty.</summary>
        public List<int> Solution = new List<int>();
        /// <summary>Share of random games that win (solver metadata, 0..1). Negative when unknown.</summary>
        public float RandomWinRate = -1f;

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
