// Tanker Jam core rules. Pure C#: no UnityEngine references, so it can be unit tested
// and must behave exactly like the Python solver in Tools/levelgen/levelgen.py.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public enum Facing { U, D, L, R }

    public struct Cell
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
    }

    public sealed class TruckDef
    {
        public int Id, X, Y, Len;
        public Facing Facing;
        public char Color;

        /// <summary>Units of oil the truck holds: length 2, 3, 4 hold 2, 4, 6.</summary>
        public int Capacity => Len == 2 ? 2 : Len == 3 ? 4 : 6;

        public bool Horizontal => Facing == Facing.L || Facing == Facing.R;

        public IEnumerable<Cell> Cells()
        {
            for (int i = 0; i < Len; i++)
                yield return Horizontal ? new Cell(X + i, Y) : new Cell(X, Y + i);
        }

        /// <summary>The cell at the front of the truck, in the direction it faces.</summary>
        public Cell Head()
        {
            bool frontIsLast = Facing == Facing.D || Facing == Facing.R;
            int i = frontIsLast ? Len - 1 : 0;
            return Horizontal ? new Cell(X + i, Y) : new Cell(X, Y + i);
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
    }

    public struct ExitResult
    {
        public bool Clear;
        /// <summary>Free cells in front of the truck before the first obstacle (for the bump animation).</summary>
        public int FreeCells;
        public ExitResult(bool clear, int free) { Clear = clear; FreeCells = free; }
    }

    /// <summary>One unit of oil moving from a vessel's bottom into a truck. Played in order by the pump.</summary>
    public sealed class Unit
    {
        public int TruckId, Vessel;
        public char Color;
        public override string ToString() => $"{Color}:v{Vessel}->t{TruckId}";
    }

    public sealed class Slot
    {
        public int TruckId, Fill, Cap;
        public char Color;
    }

    public sealed class GameRules
    {
        public readonly LevelDef Level;
        readonly HashSet<int> inLot = new HashSet<int>();
        readonly HashSet<long> cones = new HashSet<long>();
        /// <summary>Logical slots in arrival order. A slot is removed as soon as it is full.</summary>
        public readonly List<Slot> Slots = new List<Slot>();
        public readonly List<List<char>> Vessels = new List<List<char>>();

        public GameRules(LevelDef level)
        {
            Level = level;
            foreach (var t in level.Trucks) inLot.Add(t.Id);
            foreach (var c in level.Cones) cones.Add(Key(c.X, c.Y));
            foreach (var v in level.Vessels) Vessels.Add(new List<char>(v));
        }

        static long Key(int x, int y) => ((long)x << 32) | (uint)y;

        public bool InLot(int truckId) => inLot.Contains(truckId);
        public int TrucksInLot => inLot.Count;
        public TruckDef Truck(int id) => Level.Trucks[id];

        bool Occupied(int x, int y, int exceptId)
        {
            if (cones.Contains(Key(x, y))) return true;
            foreach (int id in inLot)
            {
                if (id == exceptId) continue;
                foreach (var c in Level.Trucks[id].Cells())
                    if (c.X == x && c.Y == y) return true;
            }
            return false;
        }

        public ExitResult CheckExit(int truckId)
        {
            var t = Level.Trucks[truckId];
            int dx = 0, dy = 0;
            switch (t.Facing)
            {
                case Facing.U: dy = -1; break;
                case Facing.D: dy = 1; break;
                case Facing.L: dx = -1; break;
                case Facing.R: dx = 1; break;
            }
            var h = t.Head();
            int x = h.X, y = h.Y, free = 0;
            while (true)
            {
                x += dx; y += dy;
                if (x < 0 || y < 0 || x >= Level.Size || y >= Level.Size) return new ExitResult(true, free);
                if (Occupied(x, y, truckId)) return new ExitResult(false, free);
                free++;
            }
        }

        public bool AnyTruckCanExit()
        {
            foreach (int id in inLot) if (CheckExit(id).Clear) return true;
            return false;
        }

        /// <summary>
        /// Moves a truck out of the lot into a bay (logically) and settles all oil that can now flow.
        /// The caller checks CheckExit (unless it is a VIP lift) and that a bay is physically free.
        /// Returns the new units in the exact order the pump must play them.
        /// </summary>
        public List<Unit> Assign(int truckId)
        {
            if (!inLot.Remove(truckId)) throw new InvalidOperationException($"Truck {truckId} is not in the lot.");
            var t = Level.Trucks[truckId];
            Slots.Add(new Slot { TruckId = truckId, Color = t.Color, Cap = t.Capacity });
            return Settle();
        }

        /// <summary>Same order as the solver: slots by arrival, vessels left to right, bottom layer only.</summary>
        List<Unit> Settle()
        {
            var units = new List<Unit>();
            while (true)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var s in Slots)
                    {
                        if (s.Fill >= s.Cap) continue;
                        for (int v = 0; v < Vessels.Count; v++)
                        {
                            var vessel = Vessels[v];
                            if (s.Fill < s.Cap && vessel.Count > 0 && vessel[0] == s.Color)
                            {
                                vessel.RemoveAt(0);
                                s.Fill++;
                                changed = true;
                                units.Add(new Unit { TruckId = s.TruckId, Vessel = v, Color = s.Color });
                            }
                        }
                    }
                }
                if (Slots.RemoveAll(s => s.Fill == s.Cap) == 0) break;
            }
            return units;
        }

        public bool IsWon => inLot.Count == 0 && Slots.Count == 0;

        /// <summary>Colors currently at the bottom of a non-empty vessel (for hints).</summary>
        public List<char> BottomColors()
        {
            var list = new List<char>();
            foreach (var v in Vessels) if (v.Count > 0 && !list.Contains(v[0])) list.Add(v[0]);
            return list;
        }
    }
}
