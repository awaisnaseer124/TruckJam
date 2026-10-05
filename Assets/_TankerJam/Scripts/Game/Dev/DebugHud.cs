// Greybox HUD for Phase 1 (IMGUI, development only): status line, Retry, boosters, end popup and an
// auto-solver that taps the level's stored solution. Replaced by the uGUI HUD in Phase 3.
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Game
{
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] GameController game;
        [Tooltip("Taps the stored solution automatically (gate check: the level wins end to end).")]
        public bool AutoSolve;
        [Range(0.25f, 8f)] public float TimeScale = 1f;
        public bool ShowStats = true;
        [Tooltip("Retry/booster buttons, status line and end popup. Off when the real HUD is in the scene.")]
        public bool ShowGameplayControls = true;

        readonly SolutionPlayer solver = new SolutionPlayer();
        RenderStats stats;
        float solveTimer, fps;
        GUIStyle label, button;

        void OnEnable()
        {
            if (game != null) game.LevelEnded += OnEnded;
            stats = new RenderStats();
        }

        void OnDisable()
        {
            if (game != null) game.LevelEnded -= OnEnded;
            stats?.Dispose();
            stats = null;
        }

        void OnEnded(EndState state) => Debug.Log($"Tanker Jam: level ended: {state} (t={Time.time:0.0}s)");

        public void Restart()
        {
            solver.Reset();
            game.Retry();
        }

        void Update()
        {
            Time.timeScale = TimeScale;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), 0.05f);
            if (!AutoSolve) return;
            solveTimer -= Time.unscaledDeltaTime;
            if (solveTimer > 0f) return;
            solveTimer = 0.15f;
            solver.Tick(game);
        }

        void OnGUI()
        {
            if (game == null || game.Session == null) return;
            float s = Mathf.Max(1f, Screen.height / 900f);
            label ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter };
            button ??= new GUIStyle(GUI.skin.button);
            label.fontSize = (int)(14 * s);
            button.fontSize = (int)(14 * s);

            float w = Screen.width, pad = 8 * s, bh = 40 * s;
            // With the real HUD present, keep dev tools in a corner below the top bar.
            float top = ShowGameplayControls ? pad : Screen.height * 0.5f;
            if (ShowGameplayControls)
            {
                if (GUI.Button(new Rect(pad, pad, 90 * s, bh), "Retry", button)) Restart();
                if (GUI.Button(new Rect(pad + 98 * s, pad, 90 * s, bh), AudioManager.Muted ? "Sound off" : "Sound on", button))
                    AudioManager.Muted = !AudioManager.Muted;
            }
            var toggleRect = ShowGameplayControls ? new Rect(w - 130 * s - pad, top, 130 * s, bh) : new Rect(pad, top, 110 * s, bh * 0.7f);
            AutoSolve = GUI.Toggle(toggleRect, AutoSolve, ShowGameplayControls ? " Auto-solve" : "Auto", button);
            if (ShowStats && stats != null)
                GUI.Label(new Rect(pad, top + bh + 4 * s, 260 * s, 110 * s),
                    $"{fps:0} fps\ndraw {stats.DrawCalls}  batch {stats.Batches}  setpass {stats.SetPass}\ntris {stats.Triangles / 1000}k  gc {stats.GcBytes} B  blobs {game.ActiveBlobCount}");

            if (!ShowGameplayControls) return;
            var session = game.Session;
            float y = Screen.height - bh - pad;
            string vip = session.VipArmed ? "VIP (armed)" : $"VIP ({session.VipLeft})";
            if (GUI.Button(new Rect(w / 2f - 160 * s, y, 150 * s, bh), vip, button)) game.ToggleVip();
            if (GUI.Button(new Rect(w / 2f + 10 * s, y, 150 * s, bh), $"Extra bay ({session.ExtraLeft})", button)) game.OpenExtraBay();
            GUI.Label(new Rect(pad, y - 60 * s, w - 2 * pad, 56 * s), game.Status, label);

            if (game.EndState != EndState.Playing)
            {
                var box = new Rect(w / 2f - 160 * s, Screen.height / 2f - 70 * s, 320 * s, 140 * s);
                GUI.Box(box, "");
                string title = game.EndState == EndState.Won ? "LEVEL CLEAR" : "JAMMED";
                GUI.Label(new Rect(box.x, box.y + 10 * s, box.width, 40 * s), title, label);
                if (GUI.Button(new Rect(box.x + 20 * s, box.yMax - bh - 16 * s, box.width - 40 * s, bh), "Play again", button)) Restart();
            }
        }
    }
}
