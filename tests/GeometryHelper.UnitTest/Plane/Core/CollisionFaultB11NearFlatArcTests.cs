using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two arcs, and two circles, meet at the same points whichever is asked first, however flat one of them is.
    /// </summary>
    /// <remarks>
    /// The circles' crossings were worked out from the first circle. An edge 100 long of bulge 1E-7 lies on a circle of
    /// radius 2.5E8, and from there the half chord to a circle of radius 40 is the root of the difference of two squares
    /// near 6E16, which came out 0.076 off. Asked from the edge, the edge missed the arc it crosses and measured 8.357
    /// to it; asked from the arc, they crossed and measured nought. They are now worked out from the smaller circle.
    /// </remarks>
    public class CollisionFaultB11NearFlatArcTests
    {
        /// <summary>The arc of radius 40 about (50, 30) from 3.4 radians sweeping 1.6, crossing y = 0 once, near x = 23.54.</summary>
        private static GeoArc2 Arc() => new GeoArc2(P(50, 30), 40, 3.4, 3.4 + 1.6);

        /// <summary>The edge from (0, 0) to (100, 0) bowing by 50 times its bulge towards -y.</summary>
        private static GeoEdge2 FlatEdge(double bulge) => new GeoEdge2(P(0, 0), P(100, 0), bulge);

        /// <summary>How far a point lies from the circle of radius 40 about (50, 30), worked out here.</summary>
        private static double OffTheArcsCircle(GeoPoint2 point)
            => Math.Abs(Math.Sqrt((point.X - 50) * (point.X - 50) + (point.Y - 30) * (point.Y - 30)) - 40);

        [Theory]
        [InlineData(1E-4)]
        [InlineData(1E-3)]
        public void AnEdgeOfBulgeOneTenMillionth_CrossesTheArcAskedFromEitherSide(double point)
        {
            var tolerance = new Tolerance(point, 1E-5);
            GeoEdge2 edge = FlatEdge(1E-7);
            GeoArc2 arc = Arc();

            Assert.True(edge.CollidesWith(arc, tolerance));
            Assert.True(arc.CollidesWith(edge, tolerance));
            Assert.Equal(0.0, edge.DistanceTo(arc, tolerance));
            Assert.Equal(0.0, arc.DistanceTo(edge, tolerance));
        }

        [Theory]
        [InlineData(1E-6)]
        [InlineData(1E-7)]
        [InlineData(1E-9)]
        public void ANearFlatEdge_MeetsTheArcAtOnePointWhicheverIsAskedFirst(double bulge)
        {
            // The one crossing lies on the arc's circle and within the edge's bow below the chord, and the two orders
            // give it to the last bit.
            GeoArc2 flat = FlatEdge(bulge).ToArc();
            GeoArc2 arc = Arc();

            GeoPoint2[] fromFlat = Arc2.GetIntersections(flat, arc, TenThousandth);
            GeoPoint2[] fromArc = Arc2.GetIntersections(arc, flat, TenThousandth);

            GeoPoint2 meeting = Assert.Single(fromFlat);
            GeoPoint2 other = Assert.Single(fromArc);
            Assert.Equal(meeting.X, other.X);
            Assert.Equal(meeting.Y, other.Y);
            Assert.InRange(OffTheArcsCircle(meeting), 0.0, 1E-6);
            Assert.InRange(meeting.Y, -50 * bulge - 1E-6, 1E-6);
            Assert.Equal(23.5425, meeting.X, 4);

            Assert.True(Collision2.CollidesWith(flat, arc, TenThousandth));
            Assert.True(Collision2.CollidesWith(arc, flat, TenThousandth));
            Assert.Equal(0.0, Arc2.DistanceTo(flat, arc, TenThousandth));
            Assert.Equal(0.0, Arc2.DistanceTo(arc, flat, TenThousandth));
        }

        [Fact]
        public void TheCircleOfANearFlatEdge_MeetsACircleAtTheSamePointsWhicheverIsAskedFirst()
        {
            // The circle of radius 2.5E8 carrying the edge of bulge 1E-7 meets the circle of radius 40 near (23.54, 0)
            // and (76.46, 0). Asked huge circle first, both came out 0.076 off.
            GeoCircle2 huge = FlatEdge(1E-7).ToArc().GetCircle();
            GeoCircle2 circle = Arc().GetCircle();

            GeoPoint2[] fromHuge = Intersection2.GetIntersections(huge, circle, TenThousandth);
            GeoPoint2[] fromCircle = Intersection2.GetIntersections(circle, huge, TenThousandth);

            Assert.Equal(2, fromHuge.Length);
            Assert.Equal(2, fromCircle.Length);
            Assert.Equal(fromHuge[0].X, fromCircle[1].X);
            Assert.Equal(fromHuge[0].Y, fromCircle[1].Y);
            Assert.Equal(fromHuge[1].X, fromCircle[0].X);
            Assert.Equal(fromHuge[1].Y, fromCircle[0].Y);
            Assert.All(fromHuge, p => Assert.InRange(OffTheArcsCircle(p), 0.0, 1E-6));
            Assert.All(fromHuge, p => Assert.InRange(p.Y, -5E-6 - 1E-6, 1E-6));
        }

        [Fact]
        public void AnEdgeOfBulgeOneTenMillionthPassingTwelveHundredThousandthsFromACircle_TouchesItOnlyAtAThousandth()
        {
            // The edge about 5 long runs down beside the circle of radius 24.39, 0.00012 from its rim at its middle.
            // Asked from the edge's huge circle, the two circles' crossings came out where neither is, and one landed on
            // the edge: they collided at a tolerance of 1E-4.
            var edge = new GeoEdge2(P(9.0409113657676077, 30.839478704537957), P(8.86641338255969, 25.842524586892253), -1E-07);
            var circle = new GeoCircle2(P(33.429356028060134, 30.839478704537953), 24.388324662292526);

            Assert.Equal(0.00012, edge.DistanceTo(circle, TenThousandth), 7);
            Assert.Empty(Arc2.GetIntersections(edge.ToArc(), circle, TenThousandth));
            Assert.False(edge.CollidesWith(circle, TenThousandth));
            Assert.False(circle.CollidesWith(edge, TenThousandth));
            Assert.True(edge.CollidesWith(circle, Thousandth));
            Assert.True(circle.CollidesWith(edge, Thousandth));
        }

        [Fact]
        public void TwoOrdinaryArcs_KeepTheirCrossingToTheBitAskedSmallerFirst_AndWithinRoundingAskedLargerFirst()
        {
            // The upper half of the circle of radius 50 about the origin, and the left half of the one of radius 30
            // about (60, 10), cross once. Asked smaller first the crossing is the one given before; asked larger first
            // it is now the same point, where it was one unit in the last place further along X.
            var large = new GeoArc2(P(0, 0), 50, 0, Math.PI);
            var small = new GeoArc2(P(60, 10), 30, Math.PI / 2, 3 * Math.PI / 2);

            GeoPoint2 fromSmall = Assert.Single(Arc2.GetIntersections(small, large, Thousandth));
            GeoPoint2 fromLarge = Assert.Single(Arc2.GetIntersections(large, small, Thousandth));

            Assert.Equal(38.939240119838026, fromSmall.X);
            Assert.Equal(31.364559280971832, fromSmall.Y);
            Assert.Equal(fromSmall.X, fromLarge.X);
            Assert.Equal(fromSmall.Y, fromLarge.Y);
            Assert.Equal(38.939240119838033, fromLarge.X, 12);
        }
    }
}
