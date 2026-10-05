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

        /// <summary>Free-form levels for the end-to-end tests.</summary>
        static LevelDef FreeForm(string name)
        {
            switch (name)
            {
                case "heart": return SampleLevels.Heart();
                case "mandala": return SampleLevels.Mandala();
                case "tall":
                {
                    // Two vessels of 21 units: more than the 16 layers a vessel shows at once.
                    var d = SampleLevels.MandalaDraft();
                    Assert.IsTrue(d.AutoFill(2, 1, 64).Solved);
                    return d.ToLevel();
                }
                case "radial": return SampleLevels.Radial();
                default: // a shipped generated shape level
                    return LevelJson.Parse(System.IO.File.ReadAllText(
                        System.IO.Path.Combine(Application.dataPath, $"_TankerJam/Data/Levels/{name}.json")));
            }
        }

        [TestCase("radial")]
        [TestCase("heart")]
        [TestCase("mandala")]
        [TestCase("tall")]
        [TestCase("level_023")]
        [TestCase("level_045")]
        public void FreeFormSampleWinsEndToEnd(string name)
        {
            // F3/F4/F5 gates: free-form arrangements (trucks at any angle on the square lot) play through the
            // real views; "mandala" is the reference layout rebuilt with level-editor operations only; "tall"
            // has vessels taller than the glass; level_0NN are generated shape levels (level 45: 5 vessels).
            game.Load(FreeForm(name), 1, 1);
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
        public void VipFromTheJamPopupStaysPlayableUntilTapped()
        {
            // Regression: arming the VIP lift after a "bays full" jam used to re-declare the jam a moment
            // later (before the player could tap), so the popup came straight back.
            // Five pink trucks facing up (columns 0-4) and a yellow truck across the top-left that blocks
            // columns 0 and 1. The vessel's bottom is yellow, so pink trucks in the bays can't fill.
            var level = new LevelDef { Size = 5, Slots = 3 };
            for (int i = 0; i < 5; i++)
                level.Trucks.Add(new TruckDef { Id = i, X = i, Y = 3, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Trucks.Add(new TruckDef { Id = 5, X = 0, Y = 0, Len = 2, Facing = Facing.R, Color = 'Y' });
            var tube = new System.Collections.Generic.List<char> { 'Y', 'Y' };
            for (int u = 0; u < 10; u++) tube.Add('P');
            level.Vessels.Add(tube);
            game.Load(level, 1, 0);
            for (int i = 2; i < 5; i++) { game.TapTruck(i); Simulate(0.2f); }
            Simulate(10f, () => game.EndState != EndState.Playing);
            Assert.AreEqual(EndState.JammedBaysFull, game.EndState);

            Assert.IsTrue(game.ToggleVip());
            Simulate(3f);
            Assert.AreEqual(EndState.Playing, game.EndState, "armed VIP keeps the level playable");
            game.TapTruck(0); // blocked by the yellow truck, but the VIP lift takes it anyway
            Assert.AreEqual(TruckState.Lifting, game.Truck(0).State);
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
