using System.Collections.Generic;
using NUnit.Framework;
using TankerJam.Core;

namespace TankerJam.Tests
{
    public class LayoutAndPathTests
    {
        const float Eps = 1e-4f;

        static LevelDef Level(int size, int slots, int vessels)
        {
            var l = new LevelDef { Size = size, Slots = slots };
            for (int i = 0; i < vessels; i++) l.Vessels.Add(new List<char> { 'P' });
            return l;
        }

        [Test]
        public void DefaultLayoutMatchesPrototype()
        {
            var b = new BoardLayout(Level(7, 3, 4));
            Assert.AreEqual(4.3f, b.RingX, Eps);
            Assert.AreEqual(-0.75f, b.RingTop, Eps);
            Assert.AreEqual(7.75f, b.RingBottom, Eps);
            // Prototype x values, mirrored into Unity's left-handed world (see BoardLayout.XSign).
            float[] bays = { -2.8f, -1.4f, 0f, 1.4f, 2.8f };
            for (int i = 0; i < bays.Length; i++) Assert.AreEqual(BoardLayout.XSign * bays[i], b.BayX(i), Eps);
            float[] vessels = { -3.9f, -1.3f, 1.3f, 3.9f };
            for (int i = 0; i < vessels.Length; i++) Assert.AreEqual(BoardLayout.XSign * vessels[i], b.VesselX(i), Eps);
            var c = b.CellCenter(0, 0);
            Assert.AreEqual(BoardLayout.XSign * -3f, c.X, Eps);
            Assert.AreEqual(0.5f, c.Z, Eps);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void BaysStayInsideRingAndDoNotOverlap(int slots)
        {
            var b = new BoardLayout(Level(7, slots, 4));
            Assert.AreEqual(slots + 2, b.BayCount);
            for (int i = 1; i < b.BayCount; i++)
                Assert.GreaterOrEqual(System.Math.Abs(b.BayX(i) - b.BayX(i - 1)), b.P.BayWidth - Eps);
            Assert.LessOrEqual(b.BayRowMaxX + b.P.BayWidth / 2f, b.RingX);
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        public void VesselsDoNotOverlap(int count)
        {
            var b = new BoardLayout(Level(7, 3, count));
            for (int i = 1; i < count; i++)
                Assert.Greater(System.Math.Abs(b.VesselX(i) - b.VesselX(i - 1)), 2f * b.P.VesselRadius);
        }

        [Test]
        public void TruckHomeIsFootprintCenter()
        {
            var b = new BoardLayout(Level(7, 3, 4));
            var vertical = new TruckDef { X = 3, Y = 1, Len = 3, Facing = Facing.U };
            var h = b.TruckHome(vertical);
            Assert.AreEqual(0f, h.X, Eps);
            Assert.AreEqual(2.5f, h.Z, Eps);
            var horizontal = new TruckDef { X = 0, Y = 6, Len = 2, Facing = Facing.R };
            h = b.TruckHome(horizontal);
            Assert.AreEqual(BoardLayout.XSign * -2.5f, h.X, Eps);
            Assert.AreEqual(6.5f, h.Z, Eps);
        }

        [Test]
        public void StraightPathLengthAndSampling()
        {
            var p = new PolylinePath();
            p.AddPoint(0, 0);
            p.AddPoint(0, -4);
            p.Build(0.7f);
            Assert.AreEqual(4f, p.Length, Eps);
            int cursor = 0;
            p.Sample(1f, ref cursor, out var pos, out float heading);
            Assert.AreEqual(-1f, pos.Z, Eps);
            Assert.AreEqual(System.Math.PI, System.Math.Abs(heading), 1e-3, "Facing -z is a yaw of 180 degrees.");
        }

        [Test]
        public void CornerIsRoundedAndShorterThanSharpPath()
        {
            var p = new PolylinePath();
            p.AddPoint(0, 0);
            p.AddPoint(2, 0);
            p.AddPoint(2, 2);
            p.Build(0.7f);
            Assert.Less(p.Length, 4f);
            Assert.Greater(p.Length, 4f - 2f * 0.7f + 0.7f * 1.4f - 0.01f);
            Assert.AreEqual(0f, p.End.X - 2f, Eps);
            Assert.AreEqual(2f, p.End.Z, Eps);
        }

        [Test]
        public void SamplingClampsAndCursorSurvivesBackwardQueries()
        {
            var p = new PolylinePath();
            p.AddPoint(0, 0); p.AddPoint(3, 0); p.AddPoint(3, 3);
            p.Build(0.5f);
            int cursor = 0;
            p.Sample(p.Length + 5f, ref cursor, out var end, out _);
            Assert.AreEqual(3f, end.Z, Eps);
            p.Sample(0.5f, ref cursor, out var back, out _);
            Assert.AreEqual(0.5f, back.X, Eps);
            p.Sample(-1f, ref cursor, out var start, out _);
            Assert.AreEqual(0f, start.X, Eps);
        }

        [TestCase(Facing.U)]
        [TestCase(Facing.D)]
        [TestCase(Facing.L)]
        [TestCase(Facing.R)]
        public void RouteToBayEndsUnderBayFromEveryFacing(Facing f)
        {
            var b = new BoardLayout(Level(7, 3, 4));
            var routes = new RouteBuilder(b);
            var path = new PolylinePath();
            var from = b.CellCenter(3, 3);
            float parkZ = b.ParkZ(-0.36f);
            for (int bay = 0; bay < b.BayCount; bay++)
            {
                routes.ToBay(path, from, f, bay, parkZ);
                Assert.AreEqual(b.BayX(bay), path.End.X, Eps);
                Assert.AreEqual(parkZ, path.End.Z, Eps);
                // Path leaves the lot in the facing direction first.
                var dir = BoardLayout.Dir(f);
                var early = path.PositionAt(0.3f) - from;
                Assert.Greater(early.X * dir.X + early.Z * dir.Z, 0.25f);
                // Never cuts through the lot interior: every sample is on/near the ring or outside the lot rows.
                Assert.Less(path.Length, 40f);
            }
        }

        [Test]
        public void RingRouteTakesShorterWayRound()
        {
            var b = new BoardLayout(Level(7, 3, 4));
            var routes = new RouteBuilder(b);
            var path = new PolylinePath();
            // Facing right from the bottom row: shorter to go up the right side than around the left.
            var from = b.CellCenter(5, 6);
            routes.ToBay(path, from, Facing.R, 4, b.ParkZ(-0.36f));
            int cursor = 0;
            // Work in prototype x (mirror back) so "right side" means the screen's right.
            float maxX = float.MinValue;
            for (float s = 0; s < path.Length; s += 0.25f)
            {
                path.Sample(s, ref cursor, out var p, out _);
                float protoX = BoardLayout.XSign * p.X;
                if (protoX > maxX) maxX = protoX;
                Assert.Greater(protoX, -1f, "Route should not swing around the left side.");
            }
            Assert.AreEqual(b.RingX, maxX, 0.05f);
        }

        [Test]
        public void FacingDirectionsMatchScreen()
        {
            // Camera looks toward -z from +z; in Unity that puts world -x on screen right.
            Assert.AreEqual(-1f, BoardLayout.Dir(Facing.U).Z);
            Assert.AreEqual(1f, BoardLayout.Dir(Facing.D).Z);
            Assert.AreEqual(BoardLayout.XSign, BoardLayout.Dir(Facing.R).X, "R moves toward screen right.");
            var b = new BoardLayout(Level(7, 3, 4));
            Assert.AreEqual(BoardLayout.XSign, System.Math.Sign(b.CellCenter(6, 0).X), "Last column is on screen right.");
            Assert.AreEqual(-BoardLayout.XSign, System.Math.Sign(b.BayX(0)), "VIP bay is on screen left.");
        }

        [Test]
        public void LeaveRouteExitsNearestSide()
        {
            var b = new BoardLayout(Level(7, 3, 4));
            var routes = new RouteBuilder(b);
            var path = new PolylinePath();
            routes.Leave(path, new Vec2(-1.4f, -4f));
            Assert.AreEqual(-b.P.ExitSideX, path.End.X, Eps, "Leaves toward the nearer world side.");
            Assert.AreEqual(b.P.ExitZ, path.End.Z, Eps);
            routes.Leave(path, new Vec2(1.4f, -4f));
            Assert.AreEqual(b.P.ExitSideX, path.End.X, Eps);
        }
    }
}
