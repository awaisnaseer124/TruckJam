using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    public class GameSessionTests
    {
        /// <summary>The web prototype's "vessels" level, kept as a fixed fixture.</summary>
        public static string ReferenceLevelPath =>
            Path.Combine(Application.dataPath, "_TankerJam/Tests/EditMode/Fixtures/reference_level.json");

        /// <summary>Five pink trucks facing up in columns 0..4 of a 5x5 lot; empty vessels so slots never fill.</summary>
        static LevelDef OpenLot(int slots = 3)
        {
            var level = new LevelDef { Size = 5, Slots = slots };
            for (int i = 0; i < 5; i++)
                level.Trucks.Add(new TruckDef { Id = i, X = i, Y = 3, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Vessels.Add(new List<char>());
            return level;
        }

        /// <summary>Releases every bay whose truck is logically full, like the views do after the leave animation.</summary>
        static void ReleaseFullTrucks(GameSession s)
        {
            foreach (var bay in s.Bays)
            {
                if (bay.TruckId < 0) continue;
                bool filling = false;
                foreach (var slot in s.Rules.Slots) if (slot.TruckId == bay.TruckId) filling = true;
                if (!filling) s.ReleaseBay(bay.Index);
            }
        }

        [Test]
        public void BayRowIsVipRegularsExtra()
        {
            var s = new GameSession(OpenLot(slots: 3), 1, 1);
            Assert.AreEqual(5, s.Bays.Count);
            Assert.AreEqual(BayKind.Vip, s.Bays[0].Kind);
            Assert.IsFalse(s.Bays[0].Open);
            for (int i = 1; i <= 3; i++) Assert.IsTrue(s.Bays[i].Open && s.Bays[i].Kind == BayKind.Regular);
            Assert.AreEqual(BayKind.Extra, s.Bays[4].Kind);
            Assert.IsFalse(s.Bays[4].Open);
        }

        [Test]
        public void TapsFillBaysLeftToRightThenReportNoFreeBay()
        {
            var s = new GameSession(OpenLot(), 0, 1);
            for (int i = 0; i < 3; i++)
            {
                var r = s.Tap(i);
                Assert.AreEqual(TapOutcome.Assigned, r.Outcome);
                Assert.AreEqual(i + 1, r.Bay);
            }
            var full = s.Tap(3);
            Assert.AreEqual(TapOutcome.NoFreeBay, full.Outcome);
            Assert.IsTrue(s.InLot(3), "A refused truck stays in the lot.");
            Assert.AreEqual(EndState.JammedBaysFull, s.Evaluate());

            Assert.IsTrue(s.OpenExtraBay());
            Assert.IsFalse(s.OpenExtraBay(), "Extra bay opens once.");
            var extra = s.Tap(3);
            Assert.AreEqual(TapOutcome.Assigned, extra.Outcome);
            Assert.AreEqual(s.ExtraBay, extra.Bay);
        }

        [Test]
        public void BlockedTapReportsFreeCellsAndKeepsState()
        {
            var level = OpenLot();
            level.Trucks[0].Y = 2;                 // truck 0 at (0,2)-(0,3) facing up
            level.Cones.Add(new Cell(0, 0));       // cone two cells ahead
            var s = new GameSession(level, 0, 0);
            var r = s.Tap(0);
            Assert.AreEqual(TapOutcome.Blocked, r.Outcome);
            Assert.AreEqual(1, r.FreeCells);
            Assert.IsTrue(s.InLot(0));
            Assert.AreEqual(-1, s.Bays[1].TruckId);
        }

        [Test]
        public void VipLiftsBlockedTruckAndClosesAfterRelease()
        {
            var level = OpenLot();
            level.Cones.Add(new Cell(0, 0));
            var s = new GameSession(level, 1, 0);
            Assert.AreEqual(TapOutcome.Blocked, s.Tap(0).Outcome);

            Assert.IsTrue(s.ToggleVip());
            Assert.IsTrue(s.VipArmed);
            var r = s.Tap(0);
            Assert.AreEqual(TapOutcome.VipLifted, r.Outcome);
            Assert.AreEqual(s.VipBay, r.Bay);
            Assert.AreEqual(0, s.VipLeft);
            Assert.IsFalse(s.VipArmed);
            Assert.IsTrue(s.Bays[0].Open);
            Assert.IsFalse(s.ToggleVip(), "No VIP lifts left.");

            s.ReleaseBay(0);
            Assert.IsFalse(s.Bays[0].Open, "VIP bay closes once its truck leaves.");
        }

        [Test]
        public void VipCannotArmWhileVipBayBusy()
        {
            var s = new GameSession(OpenLot(), 2, 0);
            s.ToggleVip();
            s.Tap(0);
            Assert.IsFalse(s.CanArmVip);
            Assert.IsFalse(s.ToggleVip());
        }

        [Test]
        public void TapResultCoversNewUnits()
        {
            var level = OpenLot();
            level.Vessels[0].AddRange(new[] { 'P', 'P', 'P' });
            var s = new GameSession(level, 0, 0);
            var a = s.Tap(0);
            Assert.AreEqual(0, a.FirstUnit);
            Assert.AreEqual(2, a.UnitCount);
            var b = s.Tap(1);
            Assert.AreEqual(2, b.FirstUnit);
            Assert.AreEqual(1, b.UnitCount);
            Assert.AreEqual(3, s.Units.Count);
            Assert.AreEqual(1, s.Units[2].TruckId);
        }

        [Test]
        public void JammedWhenNoTruckCanExit()
        {
            var level = new LevelDef { Size = 3, Slots = 3 };
            level.Trucks.Add(new TruckDef { Id = 0, X = 0, Y = 1, Len = 2, Facing = Facing.U, Color = 'P' });
            level.Cones.Add(new Cell(0, 0));
            level.Vessels.Add(new List<char> { 'P', 'P' });
            var s = new GameSession(level, 0, 0);
            Assert.AreEqual(EndState.JammedNoExit, s.Evaluate());
        }

        [Test]
        public void StoredSolutionWinsThroughSession()
        {
            string path = ReferenceLevelPath;
            var level = LevelJson.Parse(File.ReadAllText(path));
            var s = new GameSession(level, 0, 0);
            foreach (int id in level.Solution)
            {
                var r = s.Tap(id);
                Assert.AreEqual(TapOutcome.Assigned, r.Outcome, $"Truck {id}");
                Assert.AreNotEqual(s.ExtraBay, r.Bay);
                ReleaseFullTrucks(s);
                if (s.Rules.TrucksInLot > 0) Assert.AreEqual(EndState.Playing, s.Evaluate());
            }
            Assert.AreEqual(EndState.Won, s.Evaluate());
        }

        [Test]
        public void ReferenceLevelIsStructurallyValid()
        {
            string path = ReferenceLevelPath;
            var errors = new List<string>();
            Assert.IsTrue(LevelJson.Validate(LevelJson.Parse(File.ReadAllText(path)), errors), string.Join("\n", errors));
        }

        [Test]
        public void ValidationCatchesUnitMismatchAndOverlap()
        {
            var level = OpenLot();
            level.Trucks[1].X = 0; // overlaps truck 0
            var errors = new List<string>();
            Assert.IsFalse(LevelJson.Validate(level, errors));
            Assert.That(errors, Has.Some.Contains("overlaps"));
            Assert.That(errors, Has.Some.Contains("Color P"));
        }
    }
}
