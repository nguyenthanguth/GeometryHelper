using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The cases the second review found, each built from the numbers that showed it: two curves that cross twice a hair apart and run
    /// parallel between the crossings, a dart of one curve out of a stretch it lies along the other for, a polyline of tens of thousands
    /// of edges across a spline, and the nearest approach of two circles a hair apart whose centres are not the same.
    /// </summary>
    public class Spline2ShallowAndHugTests
    {
        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        #region Two crossings a hair apart

        public static TheoryData<double, double, double> Shallow()
        {
            var data = new TheoryData<double, double, double>();

            foreach (double o in new[] { 0.0, 1E5 })
            {
                foreach (double length in new[] { 40.0, 4000.0 })
                {
                    foreach (double chord in new[] { 0.004, 0.02, 0.1, 1.0, 3.0 })
                    {
                        // At a hundred thousand the digits are 1.5E-11 apart, and a chord of 0.004 is a depth of 2E-9.
                        if (o == 0.0 || chord >= 0.1)
                        {
                            data.Add(o, length, chord);
                        }
                    }
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Shallow))]
        public void ALineThatCutsACircleASlitDeep_MeetsItTwice_AsTheExactCurvesDo_AndTheDistanceIsNought(double o, double length, double chord)
        {
            // The line at the depth where it cuts a circle of radius 1000 in a chord of this length, which it meets at x = +-chord / 2.
            double r = 1000.0;
            double half = chord / 2.0;
            double depth = r - Math.Sqrt(r * r - half * half);
            var circle = new GeoCircle2(P(o, o), r);
            GeoSpline2 ring = GeoSpline2.FromCircle(circle);
            GeoPoint2 start = P(o - length / 2.0, o + r - depth);
            GeoPoint2 end = P(o + length / 2.0, o + r - depth);
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { start, end });
            GeoSpline2 cubic = GeoSpline2.FromControlPoints(3, new[] { start, P(o - length / 6.0, o + r - depth), P(o + length / 6.0, o + r - depth), end });

            // The exact curves cross twice, at x = -half and +half.
            Assert.Equal(2, new GeoLine2(start, end).GetIntersections(circle, Strict).Length);

            foreach (GeoSpline2 spline in new[] { line, cubic })
            {
                GeoPoint2[] places = spline.GetIntersections(ring, Strict);
                GeoPoint2[] back = ring.GetIntersections(spline, Strict).OrderBy(p => p.X).ToArray();

                Assert.True(places.Length == 2, places.Length + " places " + string.Join(" ", places.Select(p => (p.X - o).ToString("R"))) + ", two expected");
                Assert.True(Math.Abs(places[0].X - o + half) <= 1E-4 && Math.Abs(places[1].X - o - half) <= 1E-4, string.Join(" ", places.Select(p => (p.X - o).ToString("R"))));
                Assert.Equal(places, spline.GetIntersections(ring, Strict));
                Assert.Equal(2, back.Length);
                Assert.Equal(0.0, spline.DistanceTo(ring, Strict));
                Assert.Equal(0.0, ring.DistanceTo(spline, Strict));
                Assert.True(spline.CollidesWith(ring, Strict) && ring.CollidesWith(spline, Strict));
            }
        }

        [Theory]
        [InlineData(0.004)]
        [InlineData(0.02)]
        [InlineData(0.1)]
        public void ALineThatCutsAnArcASlitDeep_MeetsItTwice_WhateverTheArcIsMadeOf(double chord)
        {
            double r = 1000.0;
            double half = chord / 2.0;
            double depth = r - Math.Sqrt(r * r - half * half);
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(-20, r - depth), P(20, r - depth) });
            var arc = new GeoArc2(P(0, 0), r, 1.2, 0.8);
            GeoSpline2 arcSpline = GeoSpline2.FromArc(arc);

            Assert.Equal(2, line.GetIntersections(arc, Strict).Length);
            Assert.Equal(2, line.GetIntersections(arcSpline, Strict).Length);
            Assert.Equal(2, arcSpline.GetIntersections(line, Strict).Length);
            Assert.Equal(0.0, line.DistanceTo(arcSpline, Strict));
        }

        [Fact]
        public void TwoCirclesThatCrossAtAShallowAngle_MeetTwice_WhereTheyAreWithinTheToleranceAllRound()
        {
            // Radius 1000 about the origin, and 1000.00002 about (1E-4, 0): the second is 2E-5 outside the first at the top and bottom,
            // 1.2E-4 inside it at the left and 8E-5 outside at the right, so they cross where 1E-4 cos(angle) is -2E-5, at (-200, +-979.7959),
            // at an angle of 1E-7. They are within the tolerance of each other all round and the offset goes 8E-5 and 1.2E-4 to either side.
            GeoSpline2 a = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 1000));
            GeoSpline2 b = GeoSpline2.FromCircle(new GeoCircle2(P(1E-4, 0), 1000.00002));

            GeoPoint2[] places = a.GetIntersections(b, Strict);

            Assert.Equal(2, places.Length);
            Assert.Contains(places, p => p.DistanceTo(P(-200, 979.795897)) <= 1E-3);
            Assert.Contains(places, p => p.DistanceTo(P(-200, -979.795897)) <= 1E-3);
            Assert.Equal(places.OrderBy(p => p.Y), b.GetIntersections(a, Strict).OrderBy(p => p.Y));
            Assert.Equal(0.0, a.DistanceTo(b, Strict));
        }
        #endregion

        #region A dart out of a stretch along the other curve

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E5)]
        public void AnotherSplineThatDartsOutOfAStretchAlongTheFirst_CrossesItAtBothEdgesOfTheDart_FromEitherSide(double o)
        {
            // A line along y = o, and a polyline 2E-4 above it that goes down to y = o - 1 for a millimetre at x = 507 and comes back: the
            // exact curves give the crossings at 506.0002 and 507.9998.
            GeoSpline2 a = GeoSpline2.FromControlPoints(1, new[] { P(o, o), P(o + 1000, o) });
            GeoSpline2 b = GeoSpline2.FromPolyline(new GeoPolyline2(P(o, o + 2E-4), P(o + 506, o + 2E-4), P(o + 507, o - 1), P(o + 508, o + 2E-4), P(o + 1000, o + 2E-4)));

            GeoPoint2[] first = a.GetIntersections(b, Strict);
            GeoPoint2[] second = b.GetIntersections(a, Strict);

            Assert.Equal(2, first.Length);
            Assert.True(first[0].DistanceTo(P(o + 506.0002, o)) <= 1E-3 && first[1].DistanceTo(P(o + 507.9998, o)) <= 1E-3, string.Join(" ", first));
            Assert.Equal(first, second);
            Assert.Equal(0.0, a.DistanceTo(b, Strict));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E5)]
        public void ASplineThatDartsOutOfAStretchAlongAnother_CrossesItAtBothEdgesOfTheDart_FromEitherSide(double o)
        {
            // The same, with the dart in the first curve, which is looked at over its length.
            GeoSpline2 a = GeoSpline2.FromPolyline(new GeoPolyline2(P(o, o), P(o + 506, o), P(o + 507, o + 1), P(o + 508, o), P(o + 1000, o)));
            GeoSpline2 b = GeoSpline2.FromPolyline(new GeoPolyline2(P(o, o + 2E-4), P(o + 300, o + 2E-4), P(o + 600, o + 2E-4), P(o + 900, o + 2E-4), P(o + 1000, o + 2E-4)));

            GeoPoint2[] first = a.GetIntersections(b, Strict);

            Assert.Equal(2, first.Length);
            Assert.Equal(first, b.GetIntersections(a, Strict));
        }

        [Fact]
        public void ALineAndAnotherThatCrossesItAtAnAngleOfFourMillionths_CrossOnceInTheMiddle_WhereTheyAreWithinTheToleranceAllAlong()
        {
            // Along y = 0 for 1000, and from y = -2E-4 to y = 2E-4: within a thousandth of each other and level all the way, and crossing
            // at (500, 0), where the offset changes sign by two ten-thousandths each way.
            GeoSpline2 axis = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(1000, 0) });
            GeoSpline2 tilted = GeoSpline2.FromControlPoints(1, new[] { P(0, -2E-4), P(1000, 2E-4) });

            GeoPoint2[] places = axis.GetIntersections(tilted, Strict);

            Assert.Single(places);
            Assert.True(places[0].DistanceTo(P(500, 0)) <= 1E-3, "the crossing is at " + places[0]);
            Assert.Equal(places, tilted.GetIntersections(axis, Strict));
            Assert.Equal(0.0, axis.DistanceTo(tilted, Strict));
        }

        #endregion

        #region Tens of thousands of edges

        [Theory]
        [InlineData(20000)]
        [InlineData(80000)]
        public void APolylineOfManyEdgesAcrossASpline_GivesTheSamePlacesAsTheSplineDoesAcrossIt_InTimeInProportionToItsLength(int edges)
        {
            // Edges that zigzag across the first 40 mm of the cubic, a crossing every couple of edges. The two paths share nothing: the
            // polyline against the spline asks the spline about each edge, and the spline of the polyline against the spline cuts the two.
            GeoSpline2 cubic = GeoSpline2.FromControlPoints(3, Enumerable.Range(0, 9).Select(i => P(100 * i, 100 * Math.Sin(i))).ToArray());
            var vertices = Enumerable.Range(0, edges + 1).Select(i =>
            {
                double x = 40.0 * i / edges;
                GeoPoint2 on = cubic.GetClosestPointOnBoundary(P(x, 100 * Math.Sin(x / 100)));

                return P(x, on.Y + 0.5 * Math.Sin(i * 1.3));
            }).ToList();
            var polyline = new GeoPolyline2(vertices);
            GeoSpline2 asSpline = GeoSpline2.FromPolyline(polyline);

            var watch = Stopwatch.StartNew();
            GeoPoint2[] exact = cubic.GetIntersections(polyline, Strict);
            GeoPoint2[] fromSplines = asSpline.GetIntersections(cubic, Strict);
            watch.Stop();

            Assert.True(exact.Length > edges / 40, exact.Length + " places");

            // Crossings a few ten-thousandths of a millimetre apart chain into one place or stay two as the rounding of each path has them
            // fall either side of the tolerance, so the counts agree to a hundredth and not to the place.
            Assert.True(Math.Abs(exact.Length - fromSplines.Length) <= exact.Length / 100 + 2, exact.Length + " places from the polyline, " + fromSplines.Length + " from the splines");
            Assert.True(watch.Elapsed.TotalSeconds < 20.0, "took " + watch.Elapsed.TotalSeconds + " s");
        }

        [Fact]
        public void TheTimeOfAPolylineAcrossASpline_GrowsInProportionToItsEdges_NotToTheirSquare()
        {
            // Four times the edges is four times the work, and a few more for the caches; where each place asks every other, it is sixteen times.
            // The least of three runs of each, after a smaller one to warm the code, so that the ratio is not that of a pause.
            GeoSpline2 cubic = GeoSpline2.FromControlPoints(3, Enumerable.Range(0, 9).Select(i => P(100 * i, 100 * Math.Sin(i))).ToArray());

            double Time(int edges)
            {
                var vertices = Enumerable.Range(0, edges + 1).Select(i =>
                {
                    double x = 40.0 * i / edges;
                    GeoPoint2 on = cubic.GetClosestPointOnBoundary(P(x, 100 * Math.Sin(x / 100)));

                    return P(x, on.Y + 0.5 * Math.Sin(i * 1.3));
                }).ToList();
                var polyline = new GeoPolyline2(vertices);
                GeoSpline2 asSpline = GeoSpline2.FromPolyline(polyline);
                double least = double.MaxValue;

                for (int run = 0; run < 3; run++)
                {
                    var watch = Stopwatch.StartNew();
                    cubic.GetIntersections(polyline, Strict);
                    asSpline.GetIntersections(cubic, Strict);
                    least = Math.Min(least, watch.Elapsed.TotalMilliseconds);
                }

                return least;
            }

            Time(5000);
            double few = Time(20000);
            double many = Time(80000);

            Assert.True(many < 8.0 * few, "20 000 edges took " + few.ToString("F0") + " ms and 80 000 took " + many.ToString("F0") + " ms");
        }

        #endregion

        #region Two circles a hair apart whose centres are not the same

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E4)]
        [InlineData(1E5)]
        public void TwoCirclesAHalfThousandthApartWithTheirCentresMovedTwoTenThousandthsTowardFortyFiveDegrees_AreNearestWhereTheyAre(double r)
        {
            // The second circle is 0.0005 larger and its centre is moved 2E-4 toward 45 degrees: the gap is 0.0005 - 2E-4 cos(angle) and its least,
            // 0.0003, is at 45 degrees. The cutting runs out of its budget along a gap that is that level, and what it has then is the least
            // it found; it has to have found the one at 45 degrees, whichever piece was first.
            double shift = 2E-4;
            GeoSpline2 first = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), r));
            GeoSpline2 second = GeoSpline2.FromCircle(new GeoCircle2(P(shift * Math.Cos(Math.PI / 4), shift * Math.Sin(Math.PI / 4)), r + 0.0005));

            var watch = Stopwatch.StartNew();
            double distance = first.DistanceTo(second, Strict);
            double back = second.DistanceTo(first, Strict);
            watch.Stop();

            Assert.True(Math.Abs(distance - 0.0003) <= 1E-7, "the distance is " + distance.ToString("R") + ", 0.0003 exact");
            Assert.Equal(distance, back);
            Assert.True(first.CollidesWith(second, new Tolerance(4E-4, 1E-5)));
            Assert.False(first.CollidesWith(second, new Tolerance(2E-4, 1E-5)));
            Assert.True(watch.Elapsed.TotalSeconds < 5.0, "took " + watch.Elapsed.TotalSeconds + " s");
        }

        [Theory]
        [InlineData(1E3, 0.0005)]
        [InlineData(1E4, 0.0005)]
        [InlineData(1E5, 0.0005)]
        [InlineData(1E3, 0.5)]
        [InlineData(1E5, 0.5)]
        public void TwoConcentricCirclesAHairApart_AreThatFarApart_AndQuickly(double r, double gap)
        {
            GeoSpline2 first = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), r));
            GeoSpline2 second = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), r + gap));

            var watch = Stopwatch.StartNew();
            double distance = first.DistanceTo(second, Strict);
            watch.Stop();

            Assert.True(Math.Abs(distance - gap) <= 1E-6 * gap, "the distance is " + distance.ToString("R"));

            // 0.15 s now, 0.8 s where the cutting went on to its budget before anything was measured.
            Assert.True(watch.Elapsed.TotalSeconds < 0.6, "took " + watch.Elapsed.TotalSeconds + " s");
        }

        #endregion

        #region Two crossings are two places

        [Theory]
        [InlineData(1E6, 30.0, 0.009)]
        [InlineData(0.0, 1000.0, 0.003)]
        [InlineData(0.0, 1000.0, 0.002)]
        public void TwoCrossingsMoreThanTheToleranceApart_AreTwoPlaces_ForTheSplineAsForTheExactCircle(double o, double r, double chord)
        {
            double half = chord / 2.0;
            double depth = r - Math.Sqrt(r * r - half * half);
            var circle = new GeoCircle2(P(o, o), r);
            var line = new GeoLine2(P(o - 2 * r, o + r - depth), P(o + 2 * r, o + r - depth));
            GeoSpline2 ring = GeoSpline2.FromCircle(circle);
            GeoSpline2 lineSpline = GeoSpline2.FromControlPoints(1, new[] { line.StartPoint, line.EndPoint });

            int exact = line.GetIntersections(circle, Strict).Length;

            Assert.Equal(2, exact);
            Assert.Equal(exact, ring.GetIntersections(line, Strict).Length);
            Assert.Equal(exact, lineSpline.GetIntersections(circle, Strict).Length);
            Assert.Equal(exact, lineSpline.GetIntersections(ring, Strict).Length);
        }

        #endregion
    }
}
