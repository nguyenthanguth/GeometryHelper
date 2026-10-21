using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two circles whose centres are closer than the point tolerance, but which draw further apart than it somewhere, cross
    /// where they truly cross; two that stay within the tolerance of each other all the way round are one circle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A distance between the centres of no more than the point tolerance was read as one centre, so the circle of radius
    /// 1000 about the origin and the circle of radius 1000.0005 about (0.0006, 0), as much as 1.1E-3 apart in places, were
    /// given no crossing, though they cross at (-833.33324, 552.77094) and (-833.33324, -552.77094). The arcs and the edges
    /// on them were given none either.
    /// </para>
    /// <para>
    /// Two circles whose centres and radii are no more than the tolerance apart put together stay within it of each other
    /// all the way round, and are one circle, which meets along its length rather than at points: the circles of radius
    /// 1000 and 1000.00002 with their centres 1E-4 apart, and two arcs of one circle whose ends are rounded to six places.
    /// Further apart than that, centres closer than the tolerance are one centre only where the radical line, the
    /// difference of the squared radii over twice the distance between the centres, cannot be placed within the point
    /// tolerance for the rounding of the numbers; it is worked out as the product of the difference and the sum of the
    /// radii.
    /// </para>
    /// </remarks>
    public class CircleFaultLN1NearConcentricTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        /// <summary>
        /// The crossings worked out to fifty digits from the doubles the circles are made of.
        /// </summary>
        private static readonly GeoPoint2 Upper = new GeoPoint2(-833.33324164696101, 552.77093661494875);

        private static readonly GeoPoint2 Lower = new GeoPoint2(-833.33324164696101, -552.77093661494875);

        private static GeoCircle2 First() => new GeoCircle2(new GeoPoint2(0, 0), 1000);

        /// <summary>
        /// A circle 6E-4 off the first and 5E-4 larger: 1.1E-3 from it at most, just outside the tolerance.
        /// </summary>
        private static GeoCircle2 Outside() => new GeoCircle2(new GeoPoint2(6E-4, 0), 1000.0005);

        /// <summary>
        /// A circle 1E-4 off the first and 2E-5 larger: 1.2E-4 from it at most, well within the tolerance.
        /// </summary>
        private static GeoCircle2 Within() => new GeoCircle2(new GeoPoint2(1E-4, 0), 1000.00002);

        private static void AssertCrossings(GeoPoint2[] crossings)
        {
            Assert.Equal(2, crossings.Length);
            GeoPoint2 upper = crossings[0].Y > 0 ? crossings[0] : crossings[1];
            GeoPoint2 lower = crossings[0].Y > 0 ? crossings[1] : crossings[0];

            Assert.Equal(0.0, upper.DistanceTo(Upper), 9);
            Assert.Equal(0.0, lower.DistanceTo(Lower), 9);
        }

        [Fact]
        public void TwoCirclesApartByMoreThanTheToleranceNearOneCentre_CrossWhereTheyTrulyCross()
        {
            AssertCrossings(First().GetIntersections(Outside(), Thousandth));
            AssertCrossings(Outside().GetIntersections(First(), Thousandth));

            Assert.True(First().TryIntersectWith(Outside(), out GeoPoint2[] crossings, Thousandth));
            AssertCrossings(crossings);
        }

        [Fact]
        public void TheCrossings_AreTheOnesTheSplinesFind()
        {
            // The splines find them to a millionth, their own accuracy.
            GeoPoint2[] fromSplines = GeoSpline2.FromCircle(First()).GetIntersections(GeoSpline2.FromCircle(Outside()), Thousandth);
            GeoPoint2[] fromCircles = First().GetIntersections(Outside(), Thousandth);

            Assert.Equal(2, fromSplines.Length);
            Assert.Equal(2, fromCircles.Length);

            foreach (GeoPoint2 crossing in fromCircles)
            {
                Assert.Contains(fromSplines, p => p.DistanceTo(crossing) < 1E-5);
            }
        }

        [Fact]
        public void ArcsAndEdgesOnThem_CrossThereToo()
        {
            // From 2 to 3 radians: the upper crossing at 2.5559 radians is on both, the lower is on neither.
            var first = new GeoArc2(First().Center, First().Radius, 2.0, 3.0);
            var second = new GeoArc2(Outside().Center, Outside().Radius, 2.0, 3.0);

            Assert.Equal(0.0, Assert.Single(first.GetIntersections(second, Thousandth)).DistanceTo(Upper), 9);
            Assert.Equal(0.0, Assert.Single(second.GetIntersections(first, Thousandth)).DistanceTo(Upper), 9);
            Assert.Equal(0.0, Assert.Single(first.GetIntersections(Outside(), Thousandth)).DistanceTo(Upper), 9);
            Assert.Equal(0.0, Assert.Single(Outside().GetIntersections(first, Thousandth)).DistanceTo(Upper), 9);

            // An edge holds its ends and its bulge, and the centre and radius worked out of them again are some 1E-13 off the
            // arc's. Near-concentric circles cross where the difference of their radii, times their sum over twice the
            // distance between their centres, puts them, so that rounding moves the crossing a little.
            Assert.True(Assert.Single(new GeoEdge2(first).GetIntersections(new GeoEdge2(second), Thousandth)).DistanceTo(Upper) < 1E-5);
        }

        [Fact]
        public void TheArcsThatCross_TouchAndAreNoughtApart()
        {
            var first = new GeoArc2(First().Center, First().Radius, 2.0, 3.0);
            var second = new GeoArc2(Outside().Center, Outside().Radius, 2.0, 3.0);

            Assert.True(first.CollidesWith(second, Thousandth));
            Assert.True(second.CollidesWith(first, Thousandth));
            Assert.Equal(0.0, first.DistanceTo(second, Thousandth), 9);
            Assert.True(First().CollidesWith(Outside(), Thousandth));
            Assert.Equal(0.0, First().DistanceTo(Outside()));
        }

        [Fact]
        public void TwoCirclesWithinTheToleranceOfEachOtherAllRound_AreOneCircle()
        {
            // 1.2E-4 apart at most: one circle at a tolerance of a thousandth, which meets along its length.
            Assert.Empty(First().GetIntersections(Within(), Thousandth));
            Assert.Empty(Within().GetIntersections(First(), Thousandth));
            Assert.False(First().TryIntersectWith(Within(), out _, Thousandth));

            var first = new GeoArc2(First().Center, First().Radius, 1.5, 2.5);
            var second = new GeoArc2(Within().Center, Within().Radius, 1.5, 2.5);

            Assert.Empty(first.GetIntersections(second, Thousandth));
            Assert.Empty(new GeoEdge2(first).GetIntersections(new GeoEdge2(second), Thousandth));
            Assert.True(first.CollidesWith(second, Thousandth));
        }

        [Fact]
        public void TwoArcsOfOneCircleWithTheirEndsRoundedToSixPlaces_CrossNowhere_AndCutNothing()
        {
            // Two arcs of the circle of radius 750 about (1234.5678, 2345.6789), their ends rounded to six places: their
            // centres, worked out from their ends and bulges, are 1.6E-6 apart, and they were cut at (1650.09, 2970.05).
            var e1 = new GeoEdge2(new GeoPoint2(1980.820924, 2420.553962), new GeoPoint2(1287.620701, 3093.80014), 0.36502849483042454);
            var e2 = new GeoEdge2(new GeoPoint2(1925.363546, 2637.742657), new GeoPoint2(1506.336116, 3044.708214), 0.2027100355086725);

            Assert.Empty(e1.GetIntersections(e2, Thousandth));
            Assert.Empty(e2.GetIntersections(e1, Thousandth));
            Assert.True(e1.CollidesWith(e2, Thousandth));

            var chain = new GeoPolylineArc2(new[] { e1.StartPoint, e1.EndPoint }, new[] { e1.Bulge, 0 });
            Assert.False(chain.TrySplitBy(e2.ToArc(), out _, Thousandth));
        }

        [Fact]
        public void OneCircleTwice_StillCrossesNowhere()
        {
            Assert.Empty(First().GetIntersections(First(), Thousandth));
            Assert.False(First().TryIntersectWith(First(), out _, Thousandth));
        }

        [Fact]
        public void CirclesAboutOneCentre_StillCrossNowhere()
        {
            var a = First();
            var b = new GeoCircle2(new GeoPoint2(0, 0), 1000.0005);
            var c = new GeoCircle2(new GeoPoint2(0, 0), 999);

            Assert.Empty(a.GetIntersections(b, Thousandth));
            Assert.Empty(b.GetIntersections(a, Thousandth));
            Assert.Empty(a.GetIntersections(c, Thousandth));
        }

        [Fact]
        public void TwoEdgesOfOneCircleBuiltFromTheirBulges_StillHaveOneCentre()
        {
            // The circle of radius 500 about (1234.5, -678.9): the two edges overlap from 1 to 1.5 radians, and their centres,
            // worked out from their ends and bulges, are a few roundings apart.
            var centre = new GeoPoint2(1234.5, -678.9);
            GeoEdge2 Edge(double from, double to) => new GeoEdge2(
                new GeoPoint2(centre.X + 500 * Math.Cos(from), centre.Y + 500 * Math.Sin(from)),
                new GeoPoint2(centre.X + 500 * Math.Cos(to), centre.Y + 500 * Math.Sin(to)),
                Math.Tan((to - from) / 4));

            GeoEdge2 first = Edge(0.5, 1.5);
            GeoEdge2 second = Edge(1.0, 2.0);

            Assert.NotEqual(first.ToArc().Center, second.ToArc().Center);
            Assert.Empty(first.GetIntersections(second, Thousandth));
            Assert.Empty(second.GetIntersections(first, Thousandth));
        }
    }
}
