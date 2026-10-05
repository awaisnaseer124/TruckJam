// Menu: Tanker Jam > Dev > ...  Play-mode helpers for designers and automation: start, auto-win the current
// level, jump to a level, reset progress. They drive the running AppRoot like a player would.
using TankerJam.App;
using TankerJam.Game;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class DevMenu
    {
        static AppRoot App => Object.FindFirstObjectByType<AppRoot>();

        [MenuItem("Tanker Jam/Dev/Play (close home)")]
        static void Play() => App?.DevPlay();

        [MenuItem("Tanker Jam/Dev/Auto-solve On")]
        static void AutoSolveOn() => SetAutoSolve(true);

        [MenuItem("Tanker Jam/Dev/Auto-solve Off")]
        static void AutoSolveOff() => SetAutoSolve(false);

        [MenuItem("Tanker Jam/Dev/Jump To Level 6 (VIP intro)")]
        static void Level6() => App?.DevJumpTo(6);

        [MenuItem("Tanker Jam/Dev/Jump To Level 10 (Extra bay intro)")]
        static void Level10() => App?.DevJumpTo(10);

        [MenuItem("Tanker Jam/Dev/Play Radial Sample")]
        static void RadialSample() => App?.DevPlayLevel(TankerJam.Core.SampleLevels.Radial());

        [MenuItem("Tanker Jam/Dev/Play Heart Sample")]
        static void HeartSample() => App?.DevPlayLevel(TankerJam.Core.SampleLevels.Heart());

        [MenuItem("Tanker Jam/Dev/Play Mandala Sample")]
        static void MandalaSample() => App?.DevPlayLevel(TankerJam.Core.SampleLevels.Mandala());

        [MenuItem("Tanker Jam/Dev/Preview Win Popup")]
        static void PreviewWin() => App?.DevPreviewPopup(true);

        [MenuItem("Tanker Jam/Dev/Preview Jammed Popup")]
        static void PreviewJam() => App?.DevPreviewPopup(false);

        [MenuItem("Tanker Jam/Dev/Reset Progress")]
        static void ResetProgress() => App?.DevResetProgress();

        [MenuItem("Tanker Jam/Dev/Play (close home)", true)]
        [MenuItem("Tanker Jam/Dev/Auto-solve On", true)]
        [MenuItem("Tanker Jam/Dev/Auto-solve Off", true)]
        [MenuItem("Tanker Jam/Dev/Jump To Level 6 (VIP intro)", true)]
        [MenuItem("Tanker Jam/Dev/Jump To Level 10 (Extra bay intro)", true)]
        [MenuItem("Tanker Jam/Dev/Play Radial Sample", true)]
        [MenuItem("Tanker Jam/Dev/Play Heart Sample", true)]
        [MenuItem("Tanker Jam/Dev/Play Mandala Sample", true)]
        [MenuItem("Tanker Jam/Dev/Preview Win Popup", true)]
        [MenuItem("Tanker Jam/Dev/Preview Jammed Popup", true)]
        [MenuItem("Tanker Jam/Dev/Reset Progress", true)]
        static bool InPlayMode() => Application.isPlaying;

        static void SetAutoSolve(bool on)
        {
            var hud = Object.FindFirstObjectByType<DebugHud>();
            if (hud != null) hud.AutoSolve = on;
        }
    }
}
