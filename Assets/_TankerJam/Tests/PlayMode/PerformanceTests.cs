// Mobile budget guards (GDD "Performance and testing"):
//  - no managed allocations per frame while trucks drive, park and fill (taps themselves may allocate),
//  - draw calls at a busy moment stay under the 150 budget.
using System.Collections;
using NUnit.Framework;
using TankerJam.Core;
using TankerJam.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankerJam.Tests
{
    public class PerformanceTests
    {
        const float Dt = 1f / 60f;
        GameController game;

        [TearDown]
        public void ClearPersistence() => TankerJam.App.AppRoot.PersistenceOverride = null;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            TankerJam.App.AppRoot.PersistenceOverride = new TankerJam.Meta.MemoryPersistence(); // never touch the real save
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            game = Object.FindFirstObjectByType<GameController>();
            // Tests use the prototype's reference level (fixed truck ids), whatever level the app starts on.
            string path = System.IO.Path.Combine(Application.dataPath, "_TankerJam/Tests/EditMode/Fixtures/reference_level.json");
            game.Load(LevelJson.Parse(System.IO.File.ReadAllText(path)), 1, 1);
            var hud = Object.FindFirstObjectByType<DebugHud>();
            if (hud != null) hud.AutoSolve = false;
        }

        void Frames(float seconds)
        {
            int n = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < n; i++)
            {
                game.Advance(Dt);
                game.SubmitFx();
            }
        }

        [Test]
        public void SteadyPlayAllocatesNothingPerFrame()
        {
            // Two departures: drive, brake, hose down, pump, glugs, blobs, a full truck punch, leave.
            game.TapTruck(3);
            game.TapTruck(4);
            Frames(0.5f); // warm-up (first-use caches)

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            Frames(6f);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(EndState.Playing, game.EndState);
            Assert.AreEqual(0, allocated, $"{allocated} bytes allocated over 6 s of steady play.");
        }

        [Test]
        public void DrawCallsAtPeakStayUnderBudget()
        {
            // Busy moment: three trucks heading to bays and the pump running.
            var solver = new SolutionPlayer();
            for (int i = 0; i < 400 && (game.Session.Rules.Slots.Count < 2 || !game.IsPouring); i++)
            {
                if (i % 9 == 0) solver.Tick(game);
                game.Advance(Dt);
                game.SubmitFx();
            }

            int main = 0, shadow = 0, renderers = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                int subMeshes = r.sharedMaterials.Length;
                renderers++;
                main += subMeshes;
                if (r.shadowCastingMode != ShadowCastingMode.Off) shadow += subMeshes;
            }
            main += game.BlobDrawCalls;
            int total = main + shadow;
            Debug.Log($"Tanker Jam: draw estimate at peak: {renderers} renderers, main {main}, shadow {shadow}, total {total}, blobs {game.ActiveBlobCount}");
            // GDD hard limit is 150; fail at 100 to keep headroom for final art.
            Assert.Less(total, 100, $"Estimated draw calls {total} (main {main}, shadow {shadow}) exceed the 100 working budget.");
        }
    }
}
