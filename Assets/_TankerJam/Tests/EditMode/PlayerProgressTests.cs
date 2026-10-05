using NUnit.Framework;
using TankerJam.Meta;
using UnityEngine;

namespace TankerJam.Tests
{
    public class PlayerProgressTests
    {
        EconomyConfig economy;
        MemoryPersistence store;

        [SetUp]
        public void SetUp()
        {
            economy = ScriptableObject.CreateInstance<EconomyConfig>();
            store = new MemoryPersistence();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(economy);

        [Test]
        public void NewPlayerStartsWithGddBoosters()
        {
            var p = new PlayerProgress(store, economy);
            Assert.AreEqual(0, p.Level);
            Assert.AreEqual(2, p.Boosters(BoosterKind.Vip));
            Assert.AreEqual(2, p.Boosters(BoosterKind.Extra));
            Assert.AreEqual(0, p.Coins);
        }

        [Test]
        public void WinPaysCoinsDoublesOnHardAndAdvances()
        {
            var p = new PlayerProgress(store, economy);
            Assert.AreEqual(40, p.CompleteLevel(0, hard: false, boostersUsed: 0));
            Assert.AreEqual(1, p.Level);
            Assert.AreEqual(80, p.CompleteLevel(1, hard: true, boostersUsed: 0));
            Assert.AreEqual(120, p.Coins);
            Assert.AreEqual(2, p.Level);
        }

        [Test]
        public void ReplayingAnOldLevelDoesNotSkipAhead()
        {
            var p = new PlayerProgress(store, economy);
            p.CompleteLevel(0, false, 0);
            p.CompleteLevel(1, false, 0);
            p.CompleteLevel(0, false, 0);
            Assert.AreEqual(2, p.Level);
        }

        [Test]
        public void StarsKeepTheBestResult()
        {
            var p = new PlayerProgress(store, economy);
            p.CompleteLevel(0, false, boostersUsed: 2);
            Assert.AreEqual(1, p.StarsFor(0));
            p.CompleteLevel(0, false, boostersUsed: 0);
            Assert.AreEqual(3, p.StarsFor(0));
            p.CompleteLevel(0, false, boostersUsed: 1);
            Assert.AreEqual(3, p.StarsFor(0));
        }

        [Test]
        public void BuyingNeedsEnoughCoins()
        {
            var p = new PlayerProgress(store, economy);
            Assert.IsFalse(p.BuyBooster(BoosterKind.Extra));
            for (int i = 0; i < 15; i++) p.CompleteLevel(i, false, 0); // 600 coins
            Assert.IsTrue(p.BuyBooster(BoosterKind.Extra));
            Assert.AreEqual(0, p.Coins);
            Assert.AreEqual(3, p.Boosters(BoosterKind.Extra));
        }

        [Test]
        public void IntroductionGrantsOnlyOnce()
        {
            var p = new PlayerProgress(store, economy);
            Assert.IsTrue(p.Introduce(BoosterKind.Vip));
            Assert.AreEqual(3, p.Boosters(BoosterKind.Vip));
            Assert.IsFalse(p.Introduce(BoosterKind.Vip));
            Assert.AreEqual(3, p.Boosters(BoosterKind.Vip));
        }

        [Test]
        public void ProgressSurvivesReload()
        {
            var p = new PlayerProgress(store, economy);
            p.CompleteLevel(0, true, 0);
            p.UseBooster(BoosterKind.Vip);
            p.Introduce(BoosterKind.Extra);

            var again = new PlayerProgress(store, economy);
            Assert.AreEqual(1, again.Level);
            Assert.AreEqual(80, again.Coins);
            Assert.AreEqual(1, again.Boosters(BoosterKind.Vip));
            Assert.AreEqual(3, again.Boosters(BoosterKind.Extra));
            Assert.IsTrue(again.IsIntroduced(BoosterKind.Extra));
            Assert.AreEqual(3, again.StarsFor(0));
        }

        [Test]
        public void CorruptSaveStartsFresh()
        {
            store.Json = "{ not json";
            var p = new PlayerProgress(store, economy);
            Assert.AreEqual(0, p.Level);
            Assert.AreEqual(2, p.Boosters(BoosterKind.Vip));
        }
    }
}
