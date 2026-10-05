// Tanker Jam core rules. Pure C#: no UnityEngine references, so it can be unit tested
// and must behave exactly like the Python solver in Tools/levelgen/levelgen.py.
// Parity is enforced by Tests/EditMode/RulesParityTests (recorded Python traces).
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    /// <summary>One unit of oil moving from a vessel's bottom into a truck. Played in order by the pump.</summary>
    public readonly struct Unit
    {
        public readonly int TruckId, Vessel;
        public readonly char Color;
        public Unit(int truckId, int vessel, char color) { TruckId = truckId; Vessel = vessel; Color = color; }
        public override string ToString() => $"{Color}:v{Vessel}->t{TruckId}";
    }

    /// <summary>A logical filling slot. Exists from assignment until the truck is logically full.</summary>
    public sealed class Slot
    {
        public int TruckId, Fill, Cap;
        public char Color;
    }

    public sealed class GameRules
    {
        public readonly LevelDef Level;
        readonly ILot lot;
        readonly bool[] inLot;
        int trucksInLot;

        /// <summary>Logical slots in arrival order. A slot is removed as soon as it is full.</summary>
        public readonly List<Slot> Slots = new List<Slot>();
        /// <summary>Vessel contents, bottom layer first.</summary>
        public readonly List<List<char>> Vessels = new List<List<char>>();

        /// <param name="model">Geometric (default) handles any truck angle; Grid is the original cell model,
        /// kept as the oracle the geometric model is tested against (grid levels only).</param>
        public GameRules(LevelDef level, LotModel model = LotModel.Geometric)
        {
            Level = level;
            if (model == LotModel.Grid && !level.IsGrid) throw new ArgumentException("The grid model only supports grid (version 1) levels.");
            lot = model == LotModel.Grid ? (ILot)new LotGrid(level) : new FreeLot(level);
            inLot = new bool[level.Trucks.Count];
            for (int i = 0; i < inLot.Length; i++)
            {
                if (level.Trucks[i].Id != i) throw new ArgumentException($"Truck at index {i} has id {level.Trucks[i].Id}.");
                inLot[i] = true;
            }
            trucksInLot = inLot.Length;
            foreach (var v in level.Vessels) Vessels.Add(new List<char>(v));
        }

        public bool InLot(int truckId) => inLot[truckId];
        public int TrucksInLot => trucksInLot;
        public TruckDef Truck(int id) => Level.Trucks[id];
        public ExitResult CheckExit(int truckId) => lot.CheckExit(truckId);

        public bool AnyTruckCanExit()
        {
            for (int id = 0; id < inLot.Length; id++)
                if (inLot[id] && lot.CheckExit(id).Clear) return true;
            return false;
        }

        /// <summary>Convenience overload that allocates a result list (tests, tools).</summary>
        public List<Unit> Assign(int truckId)
        {
            var units = new List<Unit>();
            Assign(truckId, units);
            return units;
        }

        /// <summary>
        /// Moves a truck out of the lot into a logical slot and settles all oil that can now flow.
        /// The caller checks CheckExit (unless it is a VIP lift) and that a bay is physically free.
        /// Appends the new units to <paramref name="unitsOut"/> in the exact order the pump must play them.
        /// </summary>
        public void Assign(int truckId, List<Unit> unitsOut)
        {
            if (!inLot[truckId]) throw new InvalidOperationException($"Truck {truckId} is not in the lot.");
            inLot[truckId] = false;
            trucksInLot--;
            lot.Remove(truckId);
            var t = Level.Trucks[truckId];
            Slots.Add(new Slot { TruckId = truckId, Color = t.Color, Cap = t.Capacity });
            Settle(unitsOut);
        }

        /// <summary>Same order as the solver: slots by arrival, vessels left to right, bottom layer only.</summary>
        void Settle(List<Unit> units)
        {
            while (true)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int si = 0; si < Slots.Count; si++)
                    {
                        var s = Slots[si];
                        if (s.Fill >= s.Cap) continue;
                        for (int v = 0; v < Vessels.Count; v++)
                        {
                            var vessel = Vessels[v];
                            if (s.Fill < s.Cap && vessel.Count > 0 && vessel[0] == s.Color)
                            {
                                vessel.RemoveAt(0);
                                s.Fill++;
                                changed = true;
                                units.Add(new Unit(s.TruckId, v, s.Color));
                            }
                        }
                    }
                }
                if (RemoveFullSlots() == 0) break;
            }
        }

        /// <summary>Removes full slots while keeping arrival order. Returns how many were removed.</summary>
        int RemoveFullSlots()
        {
            int write = 0;
            for (int read = 0; read < Slots.Count; read++)
                if (Slots[read].Fill < Slots[read].Cap) Slots[write++] = Slots[read];
            int removed = Slots.Count - write;
            if (removed > 0) Slots.RemoveRange(write, removed);
            return removed;
        }

        public bool IsWon => trucksInLot == 0 && Slots.Count == 0;

        /// <summary>Colors currently at the bottom of a non-empty vessel, left to right, without duplicates.</summary>
        public void BottomColors(List<char> into)
        {
            into.Clear();
            foreach (var v in Vessels)
                if (v.Count > 0 && !into.Contains(v[0])) into.Add(v[0]);
        }
    }
}
