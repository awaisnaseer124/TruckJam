// One play-through of a level. Owns the rules plus everything the rules don't model: physical bays
// (a full truck holds its bay until it drives off), boosters, and win/jam evaluation.
// Views send commands here and play out the results; they never change state themselves.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public enum BayKind { Vip, Regular, Extra }

    public sealed class Bay
    {
        public readonly int Index;
        public readonly BayKind Kind;
        public bool Open;
        /// <summary>Truck physically in (or driving to) the bay, or -1.</summary>
        public int TruckId = -1;

        public Bay(int index, BayKind kind, bool open) { Index = index; Kind = kind; Open = open; }
        public bool IsFree => Open && TruckId < 0;
    }

    public enum TapOutcome
    {
        /// <summary>Truck can't be tapped now (already left the lot).</summary>
        Ignored,
        /// <summary>Path out of the lot is blocked. <see cref="TapResult.FreeDistance"/> is the lurch distance.</summary>
        Blocked,
        /// <summary>Path is clear but every open bay is occupied.</summary>
        NoFreeBay,
        /// <summary>Truck drives to <see cref="TapResult.Bay"/>.</summary>
        Assigned,
        /// <summary>VIP lift: truck flies to the VIP bay, ignoring the lot.</summary>
        VipLifted,
    }

    public readonly struct TapResult
    {
        public readonly TapOutcome Outcome;
        public readonly int TruckId, Bay;
        /// <summary>How far the truck can move before touching what blocks it (board units).</summary>
        public readonly float FreeDistance;
        public int FreeCells => (int)(FreeDistance + 1e-3f);
        /// <summary>Range of new units in <see cref="GameSession.Units"/> created by this tap.</summary>
        public readonly int FirstUnit, UnitCount;

        public TapResult(TapOutcome outcome, int truckId, int bay = -1, float freeDistance = 0f, int firstUnit = 0, int unitCount = 0)
        {
            Outcome = outcome; TruckId = truckId; Bay = bay; FreeDistance = freeDistance; FirstUnit = firstUnit; UnitCount = unitCount;
        }
    }

    public enum EndState { Playing, Won, JammedBaysFull, JammedNoExit }

    public sealed class GameSession
    {
        public readonly LevelDef Level;
        public readonly GameRules Rules;

        readonly Bay[] bays;
        /// <summary>Every unit decided so far, in global pump order. Views play these by index.</summary>
        public readonly List<Unit> Units = new List<Unit>(64);

        public int VipLeft { get; private set; }
        public int ExtraLeft { get; private set; }
        public bool VipArmed { get; private set; }

        public IReadOnlyList<Bay> Bays => bays;
        public int VipBay => 0;
        public int ExtraBay => bays.Length - 1;

        public GameSession(LevelDef level, int vipBoosters, int extraBoosters)
        {
            Level = level;
            Rules = new GameRules(level);
            VipLeft = vipBoosters;
            ExtraLeft = extraBoosters;

            // Bay row left to right: [VIP][Regular x slots][Extra].
            bays = new Bay[level.Slots + 2];
            bays[0] = new Bay(0, BayKind.Vip, open: false);
            for (int i = 1; i <= level.Slots; i++) bays[i] = new Bay(i, BayKind.Regular, open: true);
            bays[bays.Length - 1] = new Bay(bays.Length - 1, BayKind.Extra, open: false);
        }

        public bool InLot(int truckId) => Rules.InLot(truckId);

        public TapResult Tap(int truckId)
        {
            if (!Rules.InLot(truckId)) return new TapResult(TapOutcome.Ignored, truckId);

            if (VipArmed)
            {
                VipArmed = false;
                VipLeft--;
                var vip = bays[VipBay];
                vip.Open = true;
                return AssignTo(truckId, vip, TapOutcome.VipLifted);
            }

            var exit = Rules.CheckExit(truckId);
            if (!exit.Clear) return new TapResult(TapOutcome.Blocked, truckId, freeDistance: exit.FreeDistance);

            var bay = FirstFreeBay();
            if (bay == null) return new TapResult(TapOutcome.NoFreeBay, truckId);
            return AssignTo(truckId, bay, TapOutcome.Assigned);
        }

        TapResult AssignTo(int truckId, Bay bay, TapOutcome outcome)
        {
            bay.TruckId = truckId;
            int first = Units.Count;
            Rules.Assign(truckId, Units);
            return new TapResult(outcome, truckId, bay.Index, 0f, first, Units.Count - first);
        }

        /// <summary>First open, empty regular or extra bay, left to right. The VIP bay only takes VIP lifts.</summary>
        Bay FirstFreeBay()
        {
            for (int i = 1; i < bays.Length; i++)
                if (bays[i].IsFree) return bays[i];
            return null;
        }

        public bool HasFreeBay => FirstFreeBay() != null;

        /// <summary>The view reports the truck has physically cleared its bay.</summary>
        public void ReleaseBay(int bayIndex)
        {
            var bay = bays[bayIndex];
            if (bay.TruckId < 0) throw new InvalidOperationException($"Bay {bayIndex} is already empty.");
            bay.TruckId = -1;
            if (bay.Kind == BayKind.Vip) bay.Open = false;
        }

        /// <summary>Arms or disarms the VIP lift. Returns false if it can't be armed (none left or VIP bay busy).</summary>
        public bool ToggleVip()
        {
            if (VipArmed) { VipArmed = false; return true; }
            if (!CanArmVip) return false;
            VipArmed = true;
            return true;
        }

        public bool CanArmVip => VipLeft > 0 && bays[VipBay].TruckId < 0 && Rules.TrucksInLot > 0;

        public bool CanOpenExtraBay => ExtraLeft > 0 && !bays[ExtraBay].Open;

        public bool OpenExtraBay()
        {
            if (!CanOpenExtraBay) return false;
            ExtraLeft--;
            bays[ExtraBay].Open = true;
            return true;
        }

        /// <summary>Grants boosters mid-level (e.g. bought with coins on the Jammed popup).</summary>
        public void AddBoosters(int vip, int extra)
        {
            VipLeft += vip;
            ExtraLeft += extra;
        }

        /// <summary>
        /// Call only when nothing is moving or pumping (the views have been calm for a short time).
        /// Rules from the prototype: win when every truck is gone; jammed when no open bay is free,
        /// or no truck left in the lot can drive out.
        /// </summary>
        public EndState Evaluate()
        {
            bool baysEmpty = true;
            foreach (var b in bays) if (b.TruckId >= 0) { baysEmpty = false; break; }
            if (Rules.IsWon && baysEmpty) return EndState.Won;
            if (!HasFreeBay) return EndState.JammedBaysFull;
            if (!Rules.AnyTruckCanExit()) return EndState.JammedNoExit;
            return EndState.Playing;
        }
    }
}
