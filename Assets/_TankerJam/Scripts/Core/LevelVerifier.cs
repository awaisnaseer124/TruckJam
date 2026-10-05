// One place that decides whether a level may ship: structurally valid, within board/renderer limits,
// and its stored solution wins through the real GameSession (physical bays included).
// Used by the editor level importer and by tests.
using System.Collections.Generic;

namespace TankerJam.Core
{
    public static class LevelVerifier
    {
        public const int MaxVesselUnits = 16; // VesselLiquid shader layer limit
        public const int MaxRegularBays = 4;
        public const int MaxVessels = 5;

        public static bool Verify(LevelDef level, List<string> errors)
        {
            int before = errors.Count;
            if (!LevelJson.Validate(level, errors)) return false;

            if (level.MaxVesselHeight > MaxVesselUnits) errors.Add($"A vessel holds {level.MaxVesselHeight} units; max is {MaxVesselUnits}.");
            if (level.Slots > MaxRegularBays) errors.Add($"{level.Slots} regular bays; max is {MaxRegularBays}.");
            if (level.Vessels.Count > MaxVessels) errors.Add($"{level.Vessels.Count} vessels; max is {MaxVessels}.");
            if (level.Solution.Count == 0) errors.Add("No stored solution ('sol').");
            if (errors.Count > before) return false;

            var session = new GameSession(level, 0, 0);
            foreach (int id in level.Solution)
            {
                var r = session.Tap(id);
                if (r.Outcome != TapOutcome.Assigned)
                {
                    errors.Add($"Solution step for truck {id} gave {r.Outcome}.");
                    return false;
                }
                ReleaseFullTrucks(session);
            }
            if (session.Evaluate() != EndState.Won) errors.Add("Stored solution does not win.");
            return errors.Count == before;
        }

        /// <summary>Frees every bay whose truck is logically full (what the views do when it drives off).</summary>
        public static void ReleaseFullTrucks(GameSession session)
        {
            foreach (var bay in session.Bays)
            {
                if (bay.TruckId < 0) continue;
                bool filling = false;
                foreach (var slot in session.Rules.Slots)
                    if (slot.TruckId == bay.TruckId) { filling = true; break; }
                if (!filling) session.ReleaseBay(bay.Index);
            }
        }
    }
}
