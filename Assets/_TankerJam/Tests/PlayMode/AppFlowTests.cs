// End-to-end app flow through the real scene: home -> play -> win popup -> next level, booster intros,
// and buying a booster with coins. Uses in-memory persistence so the editor's save is never touched.
using System.Collections;
using NUnit.Framework;
using TankerJam.App;
using TankerJam.Core;
using TankerJam.Game;
using TankerJam.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankerJam.Tests
{
    public class AppFlowTests
    {
        const float Dt = 1f / 60f;
        AppRoot app;
        GameController game;
        MemoryPersistence store;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            store = new MemoryPersistence();
            AppRoot.PersistenceOverride = store;
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<AppRoot>();
            game = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(app, "Scene has no AppRoot.");
            var hud = Object.FindFirstObjectByType<DebugHud>();
            if (hud != null) hud.AutoSolve = false;
        }

        [TearDown]
        public void TearDown() => AppRoot.PersistenceOverride = null;

        void PlayCurrentLevelToEnd()
        {
            var solver = new SolutionPlayer();
            for (int f = 0; f < 60 * 240 && game.EndState == EndState.Playing; f++)
            {
                if (f % 9 == 0) solver.Tick(game);
                game.Advance(Dt);
            }
        }

        static void ClickFirstPopupButton(AppRoot app)
        {
            foreach (var b in app.Popup.GetComponentsInChildren<Button>())
                if (b.gameObject.activeInHierarchy) { b.onClick.Invoke(); return; }
            Assert.Fail("No active popup button.");
        }

        [Test]
        public void StartsOnHomeWithLevelOneBehindIt()
        {
            Assert.IsTrue(app.Home.IsOpen);
            Assert.AreEqual(0, app.Position);
            Assert.AreEqual(1, game.Level.Index);
            Assert.IsFalse(game.InputEnabled, "Board taps are blocked while the home screen is up.");
        }

        [Test]
        public void WinningPaysCoinsAndNextStartsLevelTwo()
        {
            app.DevPlay();
            Assert.IsFalse(app.Home.IsOpen);
            Assert.IsTrue(app.Tutorial.IsShowing, "First level shows the tap tutorial.");

            PlayCurrentLevelToEnd();
            Assert.AreEqual(EndState.Won, game.EndState);
            Assert.IsTrue(app.Popup.IsOpen);
            Assert.AreEqual(1, app.Progress.Level);
            Assert.AreEqual(40, app.Progress.Coins);
            Assert.AreEqual(3, app.Progress.StarsFor(0));

            ClickFirstPopupButton(app); // Next
            Assert.IsFalse(app.Popup.IsOpen);
            Assert.AreEqual(1, app.Position);
            Assert.AreEqual(2, game.Level.Index);
            Assert.AreEqual(EndState.Playing, game.EndState);
            Assert.IsTrue(store.Json.Contains("\"Level\":1"), "Progress was saved.");
        }

        [Test]
        public void VipIntroductionGrantsOneAndPointsAtTheButton()
        {
            app.DevJumpTo(6);
            Assert.AreEqual(6, game.Level.Index);
            Assert.AreEqual(3, app.Progress.Boosters(BoosterKind.Vip), "2 starting + 1 intro grant.");
            Assert.AreEqual(3, game.Session.VipLeft);
            Assert.IsTrue(app.Tutorial.IsShowing);

            app.DevJumpTo(6);
            Assert.AreEqual(3, app.Progress.Boosters(BoosterKind.Vip), "Granted only once.");
        }

        [Test]
        public void UsingAVipLiftSpendsFromInventory()
        {
            app.DevPlay();
            Assert.IsTrue(game.ToggleVip());
            game.TapTruck(game.Level.Solution[0]);
            Assert.AreEqual(1, app.Progress.Boosters(BoosterKind.Vip));
            Assert.AreEqual(1, game.Session.VipLeft);
        }
    }
}
