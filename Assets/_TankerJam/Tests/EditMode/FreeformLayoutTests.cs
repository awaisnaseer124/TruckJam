// F3: free-form placement in world space, tilted bays and the radial sample level.
using System.Collections.Generic;
using NUnit.Framework;
using TankerJam.Core;

namespace TankerJam.Tests
{
    public class FreeformLayoutTests
    {
        const float Eps = 1e-3f;

        static LevelDef Sample(string name) => name == "heart" ? SampleLevels.Heart() : SampleLevels.Radial();

        [TestCase("radial")]
        [TestCase("heart")]
        public void SampleIsValidAndSolved(string name)
        {
            var level = Sample(name);
            Assert.AreEqual(BoardKind.RoundedRect, level.Board.Kind, "Samples use a square lot.");
            Assert.AreEqual(level.Board.Width, level.Board.Height);
            Assert.GreaterOrEqual(level.Trucks.Count, 16);
            Assert.LessOrEqual(level.MaxVesselHeight, 16, "Vessel shader shows 16 layers.");
            var errors = new List<string>();
            Assert.IsTrue(LevelJson.Validate(level, errors), string.Join("\n", errors));
            Assert.IsTrue(LevelVerifier.Verify(level, errors), string.Join("\n", errors));
        }

        [TestCase(0f)]
        [TestCase(45f)]
        public void ParkedHatchSitsUnderTheHose(float bayAngle)
        {
            var level = SampleLevels.Radial();
            var b = new BoardLayout(level, new LayoutParams { BayAngle = bayAngle });
            const float hatch = -0.36f;
            for (int i = 0; i < b.BayCount; i++)
            {
                var hatchAt = b.ParkCenter(i, hatch) + b.BayAxis * hatch;
                Assert.AreEqual(b.HosePoint(i).X, hatchAt.X, Eps);
                Assert.AreEqual(b.HosePoint(i).Z, hatchAt.Z, Eps);
            }
        }
    
        [Test]
        public void GridPosesMapToCellCenters()
        {
            var level = new LevelDef { Size = 7, Slots = 3 };
            level.Vessels.Add(new List<char> { 'P' });
            var b = new BoardLayout(level);
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 7; y++)
                {
                    var t = new TruckDef { X = x, Y = y, Len = 1, Facing = Facing.U };
                    var cell = b.CellCenter(x, y);
                    var home = b.TruckHome(t);
                    Assert.AreEqual(cell.X, home.X, Eps);
                    Assert.AreEqual(cell.Z, home.Z, Eps);
                }
        }

        [TestCase(45f)]
        [TestCase(135f)]
        [TestCase(225f)]
        [TestCase(315f)]
        public void DiagonalRouteStartsAlongHeadingAndParksAlongBayAxis(float truckAngle)
        {
            var level = SampleLevels.Radial();
            var b = new BoardLayout(level, new LayoutParams { BayAngle = 45f });
            var routes = new RouteBuilder(b);
            var path = new PolylinePath();
            var truck = new TruckDef { Len = 2, HasPose = true, FreePose = new TruckPose(0f, 0f, truckAngle) };
            var from = b.TruckHome(truck);
            var dir = b.TruckHeading(truck);
            for (int bay = 0; bay < b.BayCount; bay++)
            {
                var park = b.ParkCenter(bay, -0.36f);
                routes.ToBay(path, from, dir, park, b.BayAxis);
                var early = path.PositionAt(0.3f) - from;
                Assert.Greater(early.X * dir.X + early.Z * dir.Z, 0.28f, "Leaves the lot along its heading.");
                Assert.AreEqual(park.X, path.End.X, Eps);
                Assert.AreEqual(park.Z, path.End.Z, Eps);
                // The last stretch runs along the bay axis.
                var late = path.End - path.PositionAt(path.Length - 0.3f);
                Assert.Greater(late.X * b.BayAxis.X + late.Z * b.BayAxis.Z, 0.29f, "Drives into the bay along its axis.");
            }
        }

        [Test]
        public void LeaveFromATiltedBayFollowsItsAxis()
        {
            var b = new BoardLayout(SampleLevels.Radial(), new LayoutParams { BayAngle = 45f });
            var routes = new RouteBuilder(b);
            var path = new PolylinePath();
            var from = b.ParkCenter(2, -0.36f);
            routes.Leave(path, from, b.BayAxis);
            var early = path.PositionAt(0.3f) - from;
            Assert.Greater(early.X * b.BayAxis.X + early.Z * b.BayAxis.Z, 0.29f);
            Assert.AreEqual(b.P.ExitZ, path.End.Z, Eps);
            Assert.AreEqual(b.P.ExitSideX, System.Math.Abs(path.End.X), Eps);
        }
    }
}
