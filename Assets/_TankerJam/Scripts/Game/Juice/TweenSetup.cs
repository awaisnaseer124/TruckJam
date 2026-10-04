using DG.Tweening;

namespace TankerJam.Game
{
    /// <summary>
    /// One-time DOTween configuration for mobile: recycle tweens, preallocate capacity, no safe-mode logging
    /// in release. Gameplay motion does NOT use DOTween (it is simulated in TruckView); DOTween is for UI and
    /// juice only, and tweens are created on events, never per frame.
    /// </summary>
    public static class TweenSetup
    {
        static bool done;

        public static void Init()
        {
            if (done) return;
            done = true;
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly)
                   .SetCapacity(200, 50);
            DOTween.defaultAutoKill = true;
        }
    }
}
