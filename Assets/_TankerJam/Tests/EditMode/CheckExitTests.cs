using NUnit.Framework;
using TankerJam.Core;

namespace TankerJam.Tests
{
    public class CheckExitTests
    {
        static LevelDef Level(int size, params TruckDef[] trucks)
        {
            var level = new LevelDef { Size = size };
            for (int i = 0; i < trucks.Length; i++)
            {
                trucks[i].Id = i;
                level.Trucks.Add(trucks[i]);
            }
            return level;
        }

        static TruckDef T(int x, int y, int len, Facing f) =>
            new TruckDef { X = x, Y = y, Len = len, Facing = f, Color = 'P' };

        [TestCase(Facing.U, 2, 2, 2)] // vertical, head is first cell (2,2): free cells y=1,0
        [TestCase(Facing.D, 2, 2, 1)] // vertical, head is last cell (2,3): free cells y=4
        [TestCase(Facing.L, 2, 2, 2)] // horizontal, head is first cell (2,2): free cells x=1,0
        [TestCase(Facing.R, 2, 2, 1)] // horizontal, head is last cell (3,2): free cells x=4
        public void OpenLotExitsInEveryFacing(Facing f, int x, int y, int expectedFree)
        {
            var rules = new GameRules(Level(5, T(x, y, 2, f)));
            var r = rules.CheckExit(0);
            Assert.IsTrue(r.Clear);
            Assert.AreEqual(expectedFree, r.FreeCells);
        }

        [Test]
        public void TruckOnEdgeFacingOutIsClearWithZeroFree()
        {
            var rules = new GameRules(Level(5, T(0, 0, 2, Facing.U)));
            var r = rules.CheckExit(0);
            Assert.IsTrue(r.Clear);
            Assert.AreEqual(0, r.FreeCells);
        }

        [Test]
        public void ConeBlocksAndReportsFreeCells()
        {
            var level = Level(5, T(0, 2, 2, Facing.R));
            level.Cones.Add(new Cell(4, 2));
            var r = new GameRules(level).CheckExit(0);
            Assert.IsFalse(r.Clear);
            Assert.AreEqual(2, r.FreeCells); // cells x=2,3 before the cone at x=4
        }

        [Test]
        public void OtherTruckBlocksUntilItLeaves()
        {
            // Truck 0 faces up at column 1; truck 1 lies horizontally across row 0.
            var rules = new GameRules(Level(5, T(1, 3, 2, Facing.U), T(0, 0, 3, Facing.R)));
            var blocked = rules.CheckExit(0);
            Assert.IsFalse(blocked.Clear);
            Assert.AreEqual(2, blocked.FreeCells); // y=2,1 free, y=0 occupied

            Assert.IsTrue(rules.CheckExit(1).Clear);
            rules.Assign(1);
            Assert.IsTrue(rules.CheckExit(0).Clear);
        }

        [Test]
        public void TruckDoesNotBlockItself()
        {
            var rules = new GameRules(Level(5, T(2, 0, 4, Facing.D)));
            var r = rules.CheckExit(0);
            Assert.IsTrue(r.Clear);
            Assert.AreEqual(1, r.FreeCells);
        }
    }
}
