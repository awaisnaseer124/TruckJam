// Drives the real Game scene (controller + views) with a fixed timestep, synchronously, so results don't
// depend on frame rate or editor focus. Any Debug.LogError (e.g. vessel/unit color mismatch) fails a test.
using System.Collections;
using NUnit.Framework;
using TankerJam.Core;
using TankerJam.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankerJam.Tests
{
    public class GameplayTests
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
            Assert.IsNotNull(game, "Game scene has no GameController.");
            var hud = Object.FindFirstObjectByType<DebugHud>();
            if (hud != null) hud.AutoSolve = false;
            Time.timeScale = 1f;
            Assert.IsNotNull(game.Session, "Start level did not load.");
        }

        /// <summary>Simulates up to <paramref name="seconds"/>; stops early when <paramref name="until"/> is true.</summary>
        void Simulate(float seconds, System.Func<bool> until = null, SolutionPlayer solver = null)
        {
            int frames = Mathf.CeilToInt(seconds / Dt);
            for (int f = 0; f < frames; f++)
            {
                if (solver != null && f % 9 == 0) solver.Tick(game);
                game.Advance(Dt);
                if (until != null && until()) return;
            }
        }

        [Test]
        public void StoredSolutionWinsEndToEnd()
        {
            var solver = new SolutionPlayer();
            Simulate(240f, () => game.EndState != EndState.Playing, solver);
            Assert.AreEqual(EndState.Won, game.EndState, $"Ended {game.EndState} at solution step {solver.Step}. Status: {game.Status}");
            for (int i = 0; i < game.TruckCount; i++)
                Assert.AreEqual(TruckState.Gone, game.Truck(i).State, $"Truck {i}");
        }

        [TestCase("radial")]
        [TestCase("heart")]
        public void FreeFormSampleWinsEndToEnd(string name)
        {
            // F3 gate: free-form arrangements (trucks at any angle on the square lot) play through the real views.
            game.Load(name == "heart" ? SampleLevels.Heart() : SampleLevels.Radial(), 1, 1);
            var solver = new SolutionPlayer();
            Simulate(300f, () => game.EndState != EndState.Playing, solver);
            Assert.AreEqual(EndState.Won, game.EndState, $"Ended {game.EndState} at solution step {solver.Step}. Status: {game.Status}");
            for (int i = 0; i < game.TruckCount; i++)
                Assert.AreEqual(TruckState.Gone, game.Truck(i).State, $"Truck {i}");
        }

        [Test]
        public void TapNearASmallTruckPicksIt()
        {
            game.Load(SampleLevels.Radial(), 1, 1);
            var cam = game.GameCamera;
            Assert.IsNotNull(cam);
            var trucks = new System.Collections.Generic.List<TruckView>();
            for (int i = 0; i < game.TruckCount; i++) trucks.Add(game.Truck(i));
            var t = game.Truck(0);
            Vector2 center = cam.WorldToScreenPoint(t.Transform.position);
            var side = cam.WorldToScreenPoint(t.Transform.position + t.Transform.right) - cam.WorldToScreenPoint(t.Transform.position);
            // Just beside the truck body (half width 0.4) but inside the finger radius.
            var near = center + (Vector2)side.normalized * (0.02f * cam.pixelHeight);
            Assert.AreEqual(t.Rig, TapInput.Nearest(cam, near, trucks));
            var far = center + (Vector2)side.normalized * (0.2f * cam.pixelHeight);
            Assert.AreNotEqual(t.Rig, TapInput.Nearest(cam, far, trucks));
        }

        [Test]
        public void BlockedTapBumpsAndReturnsHome()
        {
            // Truck 0 faces up in column 1; truck 8 covers (1,0).
            var t = game.Truck(0);
            var home = t.Transform.position;
            game.TapTruck(0);
            Assert.IsTrue(t.IsBumping);
            Assert.IsTrue(game.Session.InLot(0));
            Simulate(0.15f);
            Assert.Greater(Vector3.Distance(home, t.Transform.position), 0.05f, "Truck should lurch forward.");
            Simulate(1f);
            Assert.IsFalse(t.IsBumping);
            Assert.AreEqual(TruckState.Lot, t.State);
            Assert.Less(Vector3.Distance(home, t.Transform.position), 1e-3f);
        }

        [Test]
        public void TruckLeavesAtFullSpeedOnTheFirstFrame()
        {
            var tuning = game.Config.Tuning;
            if (tuning.DriveAccel > 0f) Assert.Ignore("Ramped acceleration is configured.");
            var t = game.Truck(3);
            var start = t.Transform.position;
            game.TapTruck(3);
            game.Advance(Dt);
            float moved = Vector3.Distance(start, t.Transform.position);
            Assert.AreEqual(tuning.DriveMaxSpeed * Dt, moved, 0.02f, "Uniform motion: no ramp-up after a tap.");
        }

        [Test]
        public void TapsDuringMotionAreIgnored()
        {
            game.TapTruck(3);
            Assert.AreEqual(TruckState.Driving, game.Truck(3).State);
            int units = game.Session.Units.Count;
            game.TapTruck(3);
            game.TapTruck(0);  // blocked truck: starts a bump
            game.TapTruck(0);  // ignored while bumping
            Assert.AreEqual(units, game.Session.Units.Count);
            Simulate(5f);
            CollectionAssert.Contains(new[] { TruckState.Filling, TruckState.Full, TruckState.Leaving, TruckState.Gone }, game.Truck(3).State);
        }

        [Test]
        public void VipLiftsBlockedTruckToVipBay()
        {
            Assert.IsTrue(game.ToggleVip());
            game.TapTruck(0);
            var t = game.Truck(0);
            Assert.AreEqual(TruckState.Lifting, t.State);
            Assert.AreEqual(game.Session.VipBay, t.Bay);
            Simulate(3f, () => t.State != TruckState.Lifting);
            CollectionAssert.Contains(new[] { TruckState.Parked, TruckState.Filling, TruckState.Full }, t.State);
            Assert.AreEqual(0f, t.Transform.position.y, 1e-4f, "Lands on the ground.");
        }

        [Test]
        public void RetryMidAnimationResetsCleanly()
        {
            game.TapTruck(3);
            game.TapTruck(4);
            Simulate(2f);
            game.Retry();
            Assert.AreEqual(0, game.Session.Units.Count);
            for (int i = 0; i < game.TruckCount; i++)
            {
                Assert.AreEqual(TruckState.Lot, game.Truck(i).State);
                Assert.AreEqual(0f, game.Truck(i).Fill);
            }
            var solver = new SolutionPlayer();
            Simulate(240f, () => game.EndState != EndState.Playing, solver);
            Assert.AreEqual(EndState.Won, game.EndState);
        }
    }
}
