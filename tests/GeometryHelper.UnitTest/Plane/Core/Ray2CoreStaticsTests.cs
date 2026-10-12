using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ray's statics in Core that come in both orders (amendments A3 to A5): asked ray first and shape first they give
    /// the same distance, the same collision and the same crossings as a set, and the shortest line exactly turned round;
    /// a crossing of a ray and a segment is placed on the first member of the call.
    /// </summary>
    /// <remarks>
    /// The rays are those of <see cref="Ray2AgainstShapesTests"/>, under a tolerance of a thousandth and of 1. Where a ray
    /// crosses a boundary twice the shortest line may sit at either crossing, so there the two orders are held to lines of
    /// no length; elsewhere the line is unique and the two orders must give it end for end, to the bit.
    /// </remarks>
    public class Ray2CoreStaticsTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly Tolerance[] Both = { Strict, Loose };

        private static void AssertExactlyReversed(GeoLine2 line, GeoLine2 back)
        {
            Assert.Equal(line.StartPoint, back.EndPoint);
            Assert.Equal(line.EndPoint, back.StartPoint);
        }

        private static void AssertShortestLinesAgree(GeoLine2 line, GeoLine2 back, bool manyCrossings)
        {
            if (manyCrossings)
            {
                Assert.InRange(line.Length, 0.0, 1E-9);
                Assert.InRange(back.Length, 0.0, 1E-9);
            }
            else
            {
                AssertExactlyReversed(line, back);
            }
        }

        public static TheoryData<string> ShapeCases() => new TheoryData<string> { "through", "beside", "behind", "from inside", "four tenths past" };

        private static bool Many(string which) => which == "through";

        /// <summary>
        /// The ray drawn ten thousand along, far past every shape here: a cut of another length than the code's own, asked
        /// through the segment members, so the distance and the collision are checked against something they do not call.
        /// </summary>
        private static GeoLine2 LongCut(GeoRay2 ray) => new GeoLine2(ray.Origin, ray.GetPointAtDistance(10000));

        /// <summary>
        /// Asserts a ray's distance is the long cut's gap, or nought where the cut collides within the tolerance: a region
        /// the cut runs within the tolerance of is nought from it, as the segment members read it.
        /// </summary>
        private static void AssertDistanceAsTheCut(double gap, bool collides, double distance)
        {
            Assert.True(Math.Abs(distance - gap) <= 1E-9 || (collides && distance == 0.0), $"distance {distance:R}, the cut's gap {gap:R}, collides {collides}");
        }

        private static GeoRay2 SquareRay(string which)
        {
            switch (which)
            {
                case "through": return new GeoRay2(P(-50, 50), GeoVector2.XAxis);
                case "beside": return new GeoRay2(P(220, 0), V(-1, 1));
                case "behind": return new GeoRay2(P(150, 50), GeoVector2.XAxis);
                case "from inside": return new GeoRay2(P(30, 40), GeoVector2.XAxis);
                default: return Grazing(P(100, 100), U(1, -1), U(1, 1), out _);
            }
        }

        private static GeoRay2 CircleRay(string which)
        {
            switch (which)
            {
                case "through": return new GeoRay2(P(-100, 30), GeoVector2.XAxis);
                case "beside": return new GeoRay2(P(-100, 80), GeoVector2.XAxis);
                case "behind": return new GeoRay2(P(100, 0), GeoVector2.XAxis);
                case "from inside": return new GeoRay2(P(0, 0), GeoVector2.YAxis);
                default: return new GeoRay2(P(-100, 50.4), GeoVector2.XAxis);
            }
        }

        private static GeoRay2 EllRay(string which)
        {
            switch (which)
            {
                case "through": return new GeoRay2(P(20, -50), V(1, 1));
                case "beside": return new GeoRay2(P(150, -100), V(1, 1));
                case "behind": return new GeoRay2(P(-30, 50), -GeoVector2.XAxis);
                case "from inside": return new GeoRay2(P(50, 0), GeoVector2.YAxis);
                default: return Grazing(P(100, 0), U(1, 1), U(1, -1), out _);
            }
        }

        private static GeoRay2 HookRay(string which)
        {
            switch (which)
            {
                case "through": return new GeoRay2(P(20, -50), V(0, 1));
                case "beside": return new GeoRay2(P(200, -100), GeoVector2.YAxis);
                case "behind": return new GeoRay2(P(-30, -40), -GeoVector2.XAxis);
                case "from inside": return new GeoRay2(P(100, 50), GeoVector2.XAxis);
                default: return new GeoRay2(P(150.4, -100), GeoVector2.YAxis);
            }
        }

        private static GeoRay2 SlotRay(string which)
        {
            switch (which)
            {
                case "through": return new GeoRay2(P(-100, 25), GeoVector2.XAxis);
                case "beside": return new GeoRay2(P(260, -100), GeoVector2.YAxis);
                case "behind": return new GeoRay2(P(300, 25), GeoVector2.XAxis);
                case "from inside": return new GeoRay2(P(100, 25), GeoVector2.YAxis);
                default: return new GeoRay2(P(225.4, -100), GeoVector2.YAxis);
            }
        }

        #region Shape first and ray first

        [Theory]
        [MemberData(nameof(ShapeCases))]
        public void ACircleAskedEitherWay_GivesOneDistanceOneCollisionAndOneLineTurnedRound(string which)
        {
            GeoRay2 ray = CircleRay(which);
            GeoCircle2 circle = Circle();

            foreach (Tolerance t in Both)
            {
                AssertDistanceAsTheCut(circle.DistanceTo(LongCut(ray)), circle.CollidesWith(LongCut(ray), t), Distance2.DistanceTo(circle, ray, t));
                Assert.Equal(circle.CollidesWith(LongCut(ray), t), Collision2.CollidesWith(circle, ray, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(ray, circle, t), Projection2.GetShortestLineTo(circle, ray, t), Many(which));
            }
        }

        [Theory]
        [MemberData(nameof(ShapeCases))]
        public void ASquareAskedEitherWay_GivesOneDistanceOneCollisionAndOneLineTurnedRound(string which)
        {
            GeoRay2 ray = SquareRay(which);
            GeoPolygon2 square = Square();
            GeoRectangle2 rectangle = Rectangle();

            foreach (Tolerance t in Both)
            {
                AssertDistanceAsTheCut(square.DistanceTo(LongCut(ray)), square.CollidesWith(LongCut(ray), t), Distance2.DistanceTo(square, ray, t));
                Assert.Equal(square.CollidesWith(LongCut(ray), t), Collision2.CollidesWith(square, ray, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(ray, square, t), Projection2.GetShortestLineTo(square, ray, t), Many(which));

                AssertDistanceAsTheCut(rectangle.DistanceTo(LongCut(ray)), rectangle.CollidesWith(LongCut(ray), t), Distance2.DistanceTo(rectangle, ray, t));
                Assert.Equal(rectangle.CollidesWith(LongCut(ray), t), Collision2.CollidesWith(rectangle, ray, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(ray, rectangle, t), Projection2.GetShortestLineTo(rectangle, ray, t), Many(which));
            }
        }

        [Theory]
        [MemberData(nameof(ShapeCases))]
        public void ChainsAndLoopsAskedEitherWay_GiveOneDistanceOneCollisionAndOneLineTurnedRound(string which)
        {
            GeoRay2 ell = EllRay(which);
            GeoRay2 hook = HookRay(which);
            GeoRay2 slot = SlotRay(which);

            foreach (Tolerance t in Both)
            {
                AssertDistanceAsTheCut(Ell().DistanceTo(LongCut(ell)), Ell().CollidesWith(LongCut(ell), t), Distance2.DistanceTo(Ell(), ell, t));
                Assert.Equal(Ell().CollidesWith(LongCut(ell), t), Collision2.CollidesWith(Ell(), ell, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(ell, Ell(), t), Projection2.GetShortestLineTo(Ell(), ell, t), Many(which));

                AssertDistanceAsTheCut(Hook().DistanceTo(LongCut(hook)), Hook().CollidesWith(LongCut(hook), t), Distance2.DistanceTo(Hook(), hook, t));
                Assert.Equal(Hook().CollidesWith(LongCut(hook), t), Collision2.CollidesWith(Hook(), hook, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(hook, Hook(), t), Projection2.GetShortestLineTo(Hook(), hook, t), Many(which));

                AssertDistanceAsTheCut(Slot().DistanceTo(LongCut(slot)), Slot().CollidesWith(LongCut(slot), t), Distance2.DistanceTo(Slot(), slot, t));
                Assert.Equal(Slot().CollidesWith(LongCut(slot), t), Collision2.CollidesWith(Slot(), slot, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(slot, Slot(), t), Projection2.GetShortestLineTo(Slot(), slot, t), Many(which));
            }
        }

        [Theory]
        [MemberData(nameof(ShapeCases))]
        public void AnArcAskedEitherWay_GivesOneDistanceOneCollisionAndOneLineTurnedRound(string which)
        {
            GeoRay2 ray = CircleRay(which);
            GeoArc2 arc = Arc();

            foreach (Tolerance t in Both)
            {
                Assert.Equal(arc.CollidesWith(LongCut(ray), t), Collision2.CollidesWith(ray, arc, t));
                AssertDistanceAsTheCut(arc.DistanceTo(LongCut(ray)), arc.CollidesWith(LongCut(ray), t), Arc2.DistanceTo(arc, ray, t));
                AssertShortestLinesAgree(Projection2.GetShortestLineTo(ray, arc, t), Arc2.GetShortestLineTo(arc, ray, t), Many(which));
            }
        }

        #endregion

        #region A ray and a segment (A4, A5)

        public static TheoryData<string> SegmentCases() => new TheoryData<string> { "across", "beside", "behind", "five ten-thousandths behind", "end-on a hair above", "parallel" };

        private static GeoLine2 Segment(string which)
        {
            switch (which)
            {
                case "across": return new GeoLine2(P(50, -10), P(50, 10));
                case "beside": return new GeoLine2(P(50, 10), P(50, 30));
                case "behind": return new GeoLine2(P(-30, -10), P(-30, 10));
                case "five ten-thousandths behind": return new GeoLine2(P(-0.0005, -10), P(-0.0005, 10));
                case "end-on a hair above": return new GeoLine2(P(50, 0.0005), P(50, 10));
                default: return new GeoLine2(P(10, 5), P(60, 5));
            }
        }

        [Theory]
        [MemberData(nameof(SegmentCases))]
        public void ARayAndASegmentAskedEitherWay_CollideAndCrossAlike(string which)
        {
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);
            GeoLine2 segment = Segment(which);

            foreach (Tolerance t in Both)
            {
                Assert.Equal(Collision2.CollidesWith(ray, segment, t), Collision2.CollidesWith(segment, ray, t));
                Assert.Equal(Collision2.CollidesWith(ray, segment, t), ray.CollidesWith(segment, t));
                Assert.Equal(Collision2.CollidesWith(segment, ray, t), segment.CollidesWith(ray, t));

                bool rayFirst = Intersection2.TryIntersectWith(ray, segment, out GeoPoint2 onRay, t);
                bool segmentFirst = Intersection2.TryIntersectWith(segment, ray, out GeoPoint2 onSegment, t);

                Assert.Equal(rayFirst, segmentFirst);

                if (rayFirst)
                {
                    Assert.InRange(onRay.DistanceTo(onSegment), 0.0, t.EqualPoint);
                    Assert.InRange(ray.DistanceTo(onRay), 0.0, 1E-12);
                    Assert.InRange(segment.DistanceTo(onSegment), 0.0, 1E-12);
                }

                Assert.Equal(ray.DistanceTo(segment, t), Distance2.DistanceTo(ray, segment, t));
                Assert.Equal(ray.GetShortestLineTo(segment, t), Projection2.GetShortestLineTo(ray, segment, t));
            }
        }

        [Fact]
        public void ACrossingFiveTenThousandthsBehindTheOrigin_IsPlacedOnTheOriginRayFirst_AndOnTheSegmentSegmentFirst()
        {
            // A5. The carriers cross at (-0.0005, 0): ray first it is brought onto the ray at t = 0, the origin; segment
            // first it is the point of the segment, (-0.0005, 0).
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);
            var segment = new GeoLine2(P(-0.0005, -10), P(-0.0005, 10));

            Assert.True(Intersection2.TryIntersectWith(ray, segment, out GeoPoint2 onRay, Strict));
            Assert.Equal(P(0, 0), onRay);
            Assert.True(Intersection2.TryIntersectWith(segment, ray, out GeoPoint2 onSegment, Strict));
            Assert.InRange(onSegment.DistanceTo(P(-0.0005, 0)), 0.0, 1E-12);
            Assert.True(segment.TryIntersectWith(ray, out GeoPoint2 fromSegment, Strict));
            Assert.InRange(fromSegment.DistanceTo(P(-0.0005, 0)), 0.0, 1E-12);
        }

        [Fact]
        public void ASegmentEndingAHairAboveTheRay_IsMetAtItsEndSegmentFirst_AndAtItsFootRayFirst()
        {
            // A5. The segment up from (50, 0.0005) meets the ray within the tolerance of its end: segment first the point is
            // clamped onto the segment, its end (50, 0.0005); ray first it is on the ray, (50, 0).
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);
            var segment = new GeoLine2(P(50, 0.0005), P(50, 10));

            Assert.True(Intersection2.TryIntersectWith(ray, segment, out GeoPoint2 onRay, Strict));
            Assert.InRange(onRay.DistanceTo(P(50, 0)), 0.0, 1E-12);
            Assert.True(Intersection2.TryIntersectWith(segment, ray, out GeoPoint2 onSegment, Strict));
            Assert.InRange(onSegment.DistanceTo(P(50, 0.0005)), 0.0, 1E-12);
        }

        #endregion

        #region Two rays

        public static TheoryData<string> RayCases() => new TheoryData<string> { "crossing", "pointing away", "diverging", "side by side", "two thousandths behind", "half a degree" };

        private static GeoRay2 Other(string which)
        {
            switch (which)
            {
                case "crossing": return new GeoRay2(P(50, -50), GeoVector2.YAxis);
                case "pointing away": return new GeoRay2(P(50, 30), GeoVector2.YAxis);
                case "diverging": return new GeoRay2(P(20, 10), V(1, 1));
                case "side by side": return new GeoRay2(P(10, 5), GeoVector2.XAxis);
                case "two thousandths behind": return new GeoRay2(P(-0.002, -5), GeoVector2.YAxis);
                default: return new GeoRay2(P(0, 5), V(1, -0.005));
            }
        }

        [Theory]
        [MemberData(nameof(RayCases))]
        public void TwoRaysAskedEitherWay_GiveOneDistanceOneCollisionOneCrossingAndOneLineTurnedRound(string which)
        {
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);
            GeoRay2 other = Other(which);

            foreach (Tolerance t in Both)
            {
                Assert.Equal(Distance2.DistanceTo(ray, other, t), Distance2.DistanceTo(other, ray, t));
                Assert.Equal(Collision2.CollidesWith(ray, other, t), Collision2.CollidesWith(other, ray, t));
                Assert.Equal(Intersection2.TryIntersectWith(ray, other, out GeoPoint2 here, t), Intersection2.TryIntersectWith(other, ray, out GeoPoint2 there, t));
                Assert.InRange(here.DistanceTo(there), 0.0, t.EqualPoint);

                GeoLine2 line = Projection2.GetShortestLineTo(ray, other, t);
                GeoLine2 back = Projection2.GetShortestLineTo(other, ray, t);

                if (Distance2.DistanceTo(ray, other, t) == 0.0)
                {
                    // Where they cross, each line is the crossing placed on the ray asking, within the tolerance of the other.
                    Assert.Equal(0.0, line.Length);
                    Assert.Equal(0.0, back.Length);
                    Assert.InRange(line.StartPoint.DistanceTo(back.StartPoint), 0.0, t.EqualPoint);
                }
                else
                {
                    Assert.InRange(line.StartPoint.DistanceTo(back.EndPoint), 0.0, 1E-9);
                    Assert.InRange(line.EndPoint.DistanceTo(back.StartPoint), 0.0, 1E-9);
                }
            }
        }

        #endregion

        [Fact]
        public void ARayLeavingARectangleTwoTenThousandthsOff_DoesNotTouchItWithinATenThousandth()
        {
            // The ray starts 0.0002 right of the side x = 50 and runs away from it, so within 1E-4 it touches nothing. The
            // segment's own collision asks whether its start is in the rectangle on the global tolerance, 1E-3, and says
            // true; the ray asks on the tolerance it is given.
            var rectangle = new GeoRectangle2(P(0, 0), 100, 100);
            var ray = new GeoRay2(P(50.0002, 0), GeoVector2.XAxis);
            var tight = new Tolerance(1E-4, 1E-5);

            Assert.False(ray.CollidesWith(rectangle, tight));
            Assert.False(Collision2.CollidesWith(rectangle, ray, tight));
            Assert.Equal(0.0002, ray.DistanceTo(rectangle, tight), 9);
        }
    }
}
