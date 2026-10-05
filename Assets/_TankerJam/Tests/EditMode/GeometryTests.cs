using NUnit.Framework;
using TankerJam.Core;

namespace TankerJam.Tests
{
    public class GeometryTests
    {
        const float Eps = 1e-3f;

        static Obb Box(float x, float z, float angle, float len, float wid = 0.8f) =>
            new Obb(new Vec2(x, z), Geometry2D.Heading(angle), len / 2f, wid / 2f);

        [Test]
        public void HeadingConvention()
        {
            var up = Geometry2D.Heading(0f);
            Assert.AreEqual(0f, up.X, Eps); Assert.AreEqual(-1f, up.Z, Eps);
            var right = Geometry2D.Heading(90f);
            Assert.AreEqual(1f, right.X, Eps); Assert.AreEqual(0f, right.Z, Eps);
            var down = Geometry2D.Heading(180f);
            Assert.AreEqual(1f, down.Z, Eps);
        }

        [Test]
        public void ParallelLanesOneUnitApartDoNotOverlap()
        {
            Assert.IsFalse(Geometry2D.Overlaps(Box(0, 0, 0, 2), Box(1, 0, 0, 2)));
        }

        [Test]
        public void TouchingEndToEndIsNotOverlap()
        {
            Assert.IsFalse(Geometry2D.Overlaps(Box(0, 0, 0, 2), Box(0, -2, 0, 2)));
            Assert.IsTrue(Geometry2D.Overlaps(Box(0, 0, 0, 2), Box(0, -1.9f, 0, 2)));
        }

        [Test]
        public void RotatedBoxesOverlapAndSeparate()
        {
            // A 45-degree box whose corner reaches into an axis-aligned one.
            Assert.IsTrue(Geometry2D.Overlaps(Box(0, 0, 45, 2), Box(0.9f, -0.9f, 0, 1)));
            Assert.IsFalse(Geometry2D.Overlaps(Box(0, 0, 45, 2), Box(1.6f, 0.6f, 0, 1)));
        }

        [Test]
        public void TimeOfImpactHeadOn()
        {
            // Moving up (-z); target's near edge is 3 units ahead of the mover's front edge.
            var mover = Box(0, 0, 0, 2);          // front edge at z = -1
            var target = Box(0, -5, 90, 2);       // spans z -5.4..-4.6 (rotated: width along z)
            Assert.IsTrue(Geometry2D.TimeOfImpact(mover, mover.Axis, target, out float t));
            Assert.AreEqual(3.6f, t, 0.02f);
        }

        [Test]
        public void TimeOfImpactMissesSideLanesAndThingsBehind()
        {
            var mover = Box(0, 0, 0, 2);
            Assert.IsFalse(Geometry2D.TimeOfImpact(mover, mover.Axis, Box(1, -5, 0, 2), out _), "neighbor lane");
            Assert.IsFalse(Geometry2D.TimeOfImpact(mover, mover.Axis, Box(0, 4, 0, 2), out _), "behind");
        }

        [Test]
        public void DiagonalMoverHitsWhatIsOnItsDiagonal()
        {
            var mover = Box(0, 0, 45, 2);        // heading up-right
            Assert.IsTrue(Geometry2D.TimeOfImpact(mover, mover.Axis, Box(3, -3, 0, 1, 1), out float t));
            Assert.Greater(t, 2f);
            Assert.IsFalse(Geometry2D.TimeOfImpact(mover, mover.Axis, Box(-3, -3, 0, 1, 1), out _));
        }
    }
}
