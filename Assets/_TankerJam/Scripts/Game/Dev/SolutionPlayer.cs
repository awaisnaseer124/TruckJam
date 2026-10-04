using TankerJam.Core;

namespace TankerJam.Game
{
    /// <summary>
    /// Taps a level's stored solution through the real GameController, waiting whenever the next truck can't
    /// be taken yet (busy truck, or no physically free bay). Used by the debug HUD and PlayMode tests.
    /// </summary>
    public sealed class SolutionPlayer
    {
        int index;

        public int Step => index;
        public bool Done(GameController game) => index >= game.Level.Solution.Count;

        public void Reset() => index = 0;

        /// <summary>Tries to tap the next truck. Returns true if a tap was issued this call.</summary>
        public bool Tick(GameController game)
        {
            if (game.Session == null || game.EndState != EndState.Playing) return false;
            var sol = game.Level.Solution;
            while (index < sol.Count && !game.Session.InLot(sol[index])) index++;
            if (index >= sol.Count) return false;

            int id = sol[index];
            if (!game.Session.HasFreeBay || game.Truck(id).IsBusy) return false;
            game.TapTruck(id);
            if (!game.Session.InLot(id)) index++;
            return true;
        }
    }
}
