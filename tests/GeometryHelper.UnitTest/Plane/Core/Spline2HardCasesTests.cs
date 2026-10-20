using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The cases a reviewer found that the first round let through, each built from the numbers that showed it: a near approach at an end
    /// that hid a crossing, places given that were not leasts of the distance, a tangent touch read as a run along, and two splines
    /// that lie within the point tolerance of each other for a length. Many of them sit a hundred thousand or a million from the origin,
    /// or are splines of two thousand spans, where the rounding of the numbers and the size of a span first matter.
    /// </summary>
    public class Spline2HardCasesTests
    {
        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static readonly Tolerance Fine = new Tolerance(1E-5, 1E-5);
        private static readonly Tolerance Tight = new Tolerance(1E-4, 1E-5);

        /// <summary>
        /// A spline through fit points on a circle of radius 1000 about (o, o), from 80 to 100 degrees, so that its top is the top of
        /// the circle to the accuracy of the fit.
        /// </summary>
        private static GeoSpline2 ArcThrough(double o, int count)
        {
            var points = Enumerable.Range(0, count).Select(i =>
            {
                double angle = Math.PI * (80.0 + 20.0 * i / (count - 1)) / 180.0;
                return P(o + 1000.0 * Math.Cos(angle), o + 1000.0 * Math.Sin(angle));
            });

            return GeoSpline2.Interpolate(points, 3, Strict);
        }

        /// <summary>The highest point of a spline, from twenty thousand samples.</summary>
        private static double TopOf(GeoSpline2 spline)
        {
            double top = double.NegativeInfinity;

            for (int i = 0; i <= 20000; i++)
            {
                top = Math.Max(top, spline.GetPointAtKnot(spline.DomainStart + (spline.DomainEnd - spline.DomainStart) * i / 20000.0).Y);
            }

            return top;
        }

        /// <summary>
        /// A tangent touch is one place, but a million from the origin the numbers are 1.2E-10 apart and the curves are within a snap
        /// (1E-7) of each other for a hundredth of a millimetre either side of the touch: the places in it are the crossings of the
        /// rounding, and where more than the tolerance apart they are given as the exact curves would give them. They are all within
        /// that hundredth of the touch, and at less than a million there is one.
        /// </summary>
        private static void AssertTouch(GeoPoint2[] places, double o, GeoPoint2 touch)
        {
            Assert.True(places.Length >= 1 && (places.Length == 1 || o >= 1E6), places.Length + " places " + string.Join(" ", places) + ", one expected");
            Assert.True(places.Length <= 3, places.Length + " places " + string.Join(" ", places));

            foreach (GeoPoint2 place in places)
            {
                Assert.True(place.DistanceTo(touch) <= 0.01, "the touch is at " + place);
            }
        }

        #region A near approach at an end that hid a crossing

        public static TheoryData<double, double> CrossingsBehindTheEnds()
        {
            var data = new TheoryData<double, double>();
            data.Add(1E4, 1E-5);
            data.Add(1E5, 1E-5);
            data.Add(1E6, 1E-3);
            data.Add(1E6, 1E-5);
            return data;
        }

        [Theory]
        [MemberData(nameof(CrossingsBehindTheEnds))]
        public void ACrossingBehindAnApproachAtTheStart_IsFound_AndTheDistanceIsNought(double length, double tolerance)
        {
            // The line along the x axis and a cubic that starts 0.5E-8 of its length above it, the nearest it comes within the first look,
            // then arches up and falls through the line: the start is 0.005 above the line at a million, and the crossing is well beyond it.
            var tol = new Tolerance(tolerance, 1E-5);
            var line = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(length, 0) });
            var cubic = GeoSpline2.FromControlPoints(3, new[] { P(0, 0.5E-8 * length), P(length / 3, length / 1000), P(2 * length / 3, length / 1000), P(length, -length / 1000) });

            GeoPoint2[] crossings = line.GetIntersections(cubic, tol);

            Assert.Contains(crossings, c => Math.Abs(c.Y) <= 1E-6 && c.X > 0.5 * length);
            Assert.Equal(0.0, line.DistanceTo(cubic, tol));
            Assert.Equal(0.0, cubic.DistanceTo(line, tol));
            Assert.True(line.CollidesWith(cubic, tol));
            Assert.True(cubic.CollidesWith(line, tol));
        }

        [Fact]
        public void ANearMissOfNineTenThousandthsAtAMillion_ReadsBelowTheToleranceWhateverIsFoundFirst()
        {
            // The arch touches 0.0009 above the line in the middle; its ends are 0.01 above it, which is what a look at the ends finds
            // first, and 0.01 is a hundred-millionth of the size of the shapes: a bound by that much would have the arch read as 0.01.
            double length = 1E6;
            var line = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(length, 0) });
            var arch = GeoSpline2.FromControlPoints(2, new[] { P(0, 0.01), P(length / 2, 2 * 0.0009 - 0.01), P(length, 0.01) });

            Assert.Equal(0.0009, line.DistanceTo(arch, Strict), 9);
            Assert.Equal(0.0009, arch.DistanceTo(line, Strict), 9);
            Assert.True(line.CollidesWith(arch, Strict));
            Assert.True(arch.CollidesWith(line, Strict));
        }

        #endregion

        #region Places that are leasts of the distance

        public static TheoryData<double, int> TangentToTheTop()
        {
            var data = new TheoryData<double, int>();

            foreach (double o in new[] { 0.0, 1E5, 1E6 })
            {
                foreach (int n in new[] { 10, 200, 2000 })
                {
                    data.Add(o, n);
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(TangentToTheTop))]
        public void ALineTangentToTheTopOfAFittedArc_TouchesItOnce(double o, int count)
        {
            GeoSpline2 arc = ArcThrough(o, count);
            double top = TopOf(arc);
            var line = new GeoLine2(P(o - 300, top), P(o + 300, top));

            GeoPoint2[] places = arc.GetIntersections(line, Strict);

            Assert.True(places.Length == 1, places.Length + " places " + string.Join(" ", places.Select(p => (p.X - o).ToString("0.000000") + "/" + (p.Y - top).ToString("0.0E+0"))) + ", one expected");
            Assert.True(Math.Abs(places[0].X - o) <= 1.5, "the touch is at " + (places[0].X - o) + " from the top");
            Assert.Equal(places, line.GetIntersections(arc, Strict));
        }

        [Theory]
        [InlineData(0.0, 2000)]
        [InlineData(1E5, 2000)]
        [InlineData(1E6, 2000)]
        public void ALineASixMillionthOverTheTopOfAFittedArc_IsOneNearTouch_AndOneUnderItIsTwoCrossings(double o, int count)
        {
            GeoSpline2 arc = ArcThrough(o, count);
            double top = o + 1000.0;

            // 1E-6 over the true top: the least of the distance is 1E-6 in the middle, and the pieces' starts on either side are not leasts.
            var over = new GeoLine2(P(o - 300, top + 1E-6), P(o + 300, top + 1E-6));
            GeoPoint2[] touch = arc.GetIntersections(over, Strict);
            Assert.True(touch.Length == 1, touch.Length + " places " + string.Join(" ", touch) + ", one expected");
            Assert.True(Math.Abs(touch[0].X - o) <= 0.06, "the near touch is at " + (touch[0].X - o));
            Assert.Equal(1E-6, arc.DistanceTo(over, Strict), 7);

            // 1E-6 under it: the line cuts the arc at x = +-sqrt(2000 * 1E-6) = +-0.0447 about the top, and the middle between them is a greatest.
            var under = new GeoLine2(P(o - 300, top - 1E-6), P(o + 300, top - 1E-6));
            GeoPoint2[] cut = arc.GetIntersections(under, Strict).OrderBy(p => p.X).ToArray();
            Assert.True(cut.Length == 2, cut.Length + " places " + string.Join(" ", cut.Select(p => p.X - o)) + ", two expected");
            Assert.True(Math.Abs(cut[0].X - o + 0.0447) <= 0.01 && Math.Abs(cut[1].X - o - 0.0447) <= 0.01, string.Join(" ", cut.Select(p => p.X - o)));
        }

        [Fact]
        public void ALineAHairUnderTheTopOfAnEllipse_CutsItTwice_AndTheMiddleBetweenIsNoPlace()
        {
            // The line y = 49.999999 across the ellipse of radii 100 and 50: it is inside it between x = -+0.02, where the distance to the rim
            // is greatest in the middle at 1E-6.
            var ellipse = new GeoEllipse2(P(0, 0), new GeoVector2(1, 0), 100, 50);
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(-30, 49.999999), P(30, 49.999999) });

            GeoPoint2[] places = line.GetIntersections(ellipse, Strict);

            Assert.True(places.Length == 2, places.Length + " places " + string.Join(" ", places) + ", two expected");
            Assert.True(Math.Abs(places[0].X + 0.02) <= 1E-3 && Math.Abs(places[1].X - 0.02) <= 1E-3, string.Join(" ", places));
            Assert.Equal(places, ellipse.GetIntersections(line, Strict));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E5)]
        public void AWiggleOfTwoTenThousandthsAboutAQuarterArc_CrossesItOnceBetweenEachTwoFitPoints(double o)
        {
            // Thirty-seven fit points on a circle of radius 1000 alternately 2E-4 outside it and 2E-4 inside, in a spline that is
            // never more than a few ten-thousandths off the arc. It crosses the arc once between each two, thirty-six times, at a slope
            // of 5E-6, and every other stationary point of its distance to the arc is a greatest, at 2E-4 or so, and no place.
            var arc = new GeoArc2(P(o, o), 1000, 0, Math.PI / 2);
            var points = Enumerable.Range(0, 37).Select(i =>
            {
                double r = 1000.0 + (i % 2 == 0 ? 2E-4 : -2E-4);
                return P(o + r * Math.Cos(i * Math.PI / 72.0), o + r * Math.Sin(i * Math.PI / 72.0));
            }).ToList();
            GeoSpline2 wiggle = GeoSpline2.Interpolate(points, 3, Strict);

            GeoPoint2[] places = wiggle.GetIntersections(arc, Strict);

            Assert.True(places.Length == 36, places.Length + " places " + string.Join(" ", places.Select(p => Math.Atan2(p.Y - o, p.X - o) * 180.0 / Math.PI)) + ", 36 expected");

            for (int i = 0; i < places.Length; i++)
            {
                double angle = Math.Atan2(places[i].Y - o, places[i].X - o) * 180.0 / Math.PI;
                Assert.True(angle > 2.5 * i && angle < 2.5 * (i + 1), "place " + i + " is at " + angle + " degrees, between " + 2.5 * i + " and " + 2.5 * (i + 1) + " expected");
                Assert.True(arc.DistanceTo(places[i], Strict) <= 1E-6, "place " + i + " is " + arc.DistanceTo(places[i], Strict) + " off the arc");
            }

            Assert.Equal(places, arc.GetIntersections(wiggle, Strict));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E5)]
        public void AFitToThirtySevenPointsOfAQuarterArc_MeetsThatArcAtEachFitPoint_AndEveryPlaceGivenIsOnBoth(double o)
        {
            // The fit points are on the arc, so the spline meets it at each; where else it crosses it, a hundred-millionth off, is the
            // rounding of its numbers and not the shape, so the places are counted no further than that they lie on both and are apart.
            var arc = new GeoArc2(P(o, o), 1000, 0, Math.PI / 2);
            var points = Enumerable.Range(0, 37).Select(i => P(o + 1000.0 * Math.Cos(i * Math.PI / 72.0), o + 1000.0 * Math.Sin(i * Math.PI / 72.0))).ToList();
            GeoSpline2 fit = GeoSpline2.Interpolate(points, 3, Strict);

            GeoPoint2[] places = fit.GetIntersections(arc, Strict);

            // The spline is within a few hundred-billionths of the arc for a hundredth of a millimetre either side of a fit point, and the
            // place there is any one of the points of that stretch.
            foreach (GeoPoint2 p in points)
            {
                Assert.Contains(places, q => q.DistanceTo(p) <= 0.02);
            }

            for (int i = 0; i < places.Length; i++)
            {
                Assert.True(arc.DistanceTo(places[i], Strict) <= 1E-6 && fit.DistanceTo(places[i], Strict) <= 1E-6, "place " + i + " is off a curve");

                for (int j = 0; j < i; j++)
                {
                    Assert.True(places[i].DistanceTo(places[j]) > 1E-3, "places " + j + " and " + i + " are within the point tolerance");
                }
            }

            Assert.Equal(places, arc.GetIntersections(fit, Strict));
        }

        #endregion

        #region A tangent touch is a touch

        [Theory]
        [InlineData(0.0, 1000)]
        [InlineData(0.0, 10000)]
        [InlineData(1E5, 100)]
        [InlineData(1E6, 100)]
        [InlineData(1E6, 1000)]
        public void ALineOfManySegmentsTangentToACircle_TouchesItOnce_AndIsNoRunAlong(double o, int count)
        {
            var points = Enumerable.Range(0, count).Select(i => P(o - 500.0 + 1000.0 * i / (count - 1), o + 1000.0)).ToList();
            GeoSpline2 line = GeoSpline2.FromPolyline(new GeoPolyline2(points));
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(o, o), 1000));

            GeoPoint2[] places = line.GetIntersections(circle, Strict);

            AssertTouch(places, o, P(o, o + 1000));
            Assert.True(line.DistanceTo(circle, Strict) <= 1E-6, "the distance is " + line.DistanceTo(circle, Strict));
            Assert.True(line.CollidesWith(circle, Strict));
            Assert.Equal(places.OrderBy(p => p.X), circle.GetIntersections(line, Strict).OrderBy(p => p.X));
        }

        #endregion

        #region Far from the origin, and splines of thousands of spans

        [Theory]
        [InlineData(1E5)]
        [InlineData(1E6)]
        public void ASegmentAcrossAnArcFittedWithTwoThousandPoints_CrossesItWhereTheCircleIs(double o)
        {
            // The circle of radius 1000 about (o, o) at 85 and at 95 degrees is the points (o + 87.1557, o + 996.1947) and (o - 87.1557, o + 996.1947),
            // and the horizontal line through them cuts the arc, which runs from 80 to 100 degrees, there and nowhere else, first at 85.
            GeoSpline2 arc = ArcThrough(o, 2000);
            double y = o + 1000.0 * Math.Sin(85.0 * Math.PI / 180.0);
            var segment = new GeoLine2(P(o - 300, y), P(o + 300, y));
            double dx = 1000.0 * Math.Cos(85.0 * Math.PI / 180.0);

            GeoPoint2[] places = arc.GetIntersections(segment, Strict);

            Assert.Equal(2, places.Length);
            Assert.True(places[0].DistanceTo(P(o + dx, y)) <= 1E-4, "the first crossing is at " + places[0]);
            Assert.True(places[1].DistanceTo(P(o - dx, y)) <= 1E-4, "the second crossing is at " + places[1]);
            Assert.Equal(places, segment.GetIntersections(arc, Strict));
        }

        [Fact]
        public void ASineFittedWithThreeThousandSpans_CrossesTheAxisThroughItsTwentyNineZeros_ThatAreAHundredApart()
        {
            // 100 sin(2 pi (x + 0.5) / 200) through 3000 points one apart, a hundred thousand from the origin: zero at x = 100 k - 0.5, k = 1 to 29.
            const double o = 1E5;
            var points = Enumerable.Range(0, 3000).Select(i => P(o + i, o + 100.0 * Math.Sin(2.0 * Math.PI * (i + 0.5) / 200.0))).ToList();
            GeoSpline2 sine = GeoSpline2.Interpolate(points, 3, Strict);
            var axis = new GeoXLine2(P(o - 10, o), new GeoVector2(1, 0));

            GeoPoint2[] places = sine.GetIntersections(axis, Strict);

            Assert.Equal(29, places.Length);

            for (int k = 1; k <= 29; k++)
            {
                Assert.True(places[k - 1].DistanceTo(P(o + 100.0 * k - 0.5, o)) <= 1E-4, "zero " + k + " is at " + places[k - 1]);
            }

            Assert.Equal(places, axis.GetIntersections(sine, Strict));
        }

        [Fact]
        public void TwoSplinesOfFiveHundredCollinearSpansEach_CrossOnceAtAMillion()
        {
            // y = x from (o, o) to (o + 500, o + 500), and y = 400 - x / 2 (from o): they cross at x = y = 266.6667.
            const double o = 1E6;
            GeoSpline2 rising = GeoSpline2.FromPolyline(new GeoPolyline2(Enumerable.Range(0, 501).Select(i => P(o + i, o + i)).ToList()));
            GeoSpline2 falling = GeoSpline2.FromPolyline(new GeoPolyline2(Enumerable.Range(0, 501).Select(i => P(o + i, o + 400 - 0.5 * i)).ToList()));

            GeoPoint2[] places = rising.GetIntersections(falling, Strict);

            Assert.Single(places);
            Assert.True(places[0].DistanceTo(P(o + 800.0 / 3.0, o + 800.0 / 3.0)) <= 1E-4, "the crossing is at " + places[0]);
            Assert.Equal(places, falling.GetIntersections(rising, Strict));
            Assert.Equal(0.0, rising.DistanceTo(falling, Strict));
        }

        [Fact]
        public void ASplineOfTwentyThousandCollinearSpans_AlongASegment_IsNoughtFromItAndMeetsItOnlyWhereItLeavesIt()
        {
            // Along y = o from x = o to o + 1000, a million from the origin, in 20 000 spans, then straight up for 500: it lies along the segment
            // from x = o - 100 to o + 1500 for 1000, with no place along the stretch, and leaves it at (o + 1000, o).
            const double o = 1E6;
            var vertices = Enumerable.Range(0, 20001).Select(i => P(o + 0.05 * i, o)).ToList();
            vertices.Add(P(o + 1000, o + 500));
            GeoSpline2 along = GeoSpline2.FromPolyline(new GeoPolyline2(vertices));
            var segment = new GeoLine2(P(o - 100, o), P(o + 1500, o));

            GeoPoint2[] places = along.GetIntersections(segment, Strict);

            Assert.Equal(0.0, along.DistanceTo(segment, Strict));
            Assert.True(along.CollidesWith(segment, Strict));
            Assert.Single(places);
            Assert.True(places[0].DistanceTo(P(o + 1000, o)) <= 1E-6, "the place is at " + places[0]);
            Assert.Equal(places, segment.GetIntersections(along, Strict));
        }

        [Fact]
        public void ASplineThatEndsOnACornerOfAPolygonAtAMillion_MeetsItOnceThere()
        {
            const double o = 1E6;
            var square = new GeoPolygon2(P(o, o), P(o + 100, o), P(o + 100, o + 100), P(o, o + 100));
            GeoSpline2 arriving = GeoSpline2.FromControlPoints(2, new[] { P(o + 10, o + 10), P(o + 60, o + 30), P(o + 100, o + 100) });

            GeoPoint2[] places = arriving.GetIntersections(square, Strict);

            Assert.Single(places);
            Assert.True(places[0].DistanceTo(P(o + 100, o + 100)) <= 1E-6, "the place is at " + places[0]);
            Assert.Equal(0.0, arriving.DistanceTo(square, Strict));
            Assert.Equal(places, square.GetIntersections(arriving, Strict));
        }

        [Theory]
        [InlineData(1E5)]
        [InlineData(1E6)]
        public void ACircleOfRadiusThirtyInsideAnEllipseTouchingItsTop_TouchesItOnce(double o)
        {
            // The ellipse of radii 100 and 50 about (o, o) has the point (o, o + 50) at the top, where it curves five times less than the circle
            // of radius 30 about (o, o + 20), which touches it there from inside.
            var ellipse = new GeoEllipse2(P(o, o), new GeoVector2(1, 0), 100, 50);
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(o, o + 20), 30));

            GeoPoint2[] places = circle.GetIntersections(ellipse, Strict);

            AssertTouch(places, o, P(o, o + 50));
            Assert.True(circle.DistanceTo(ellipse, Strict) <= 1E-6);
            Assert.Equal(places, ellipse.GetIntersections(circle, Strict));
        }

        [Fact]
        public void ALineTangentToTheTopOfAnEllipseAtAMillion_TouchesItOnce()
        {
            const double o = 1E6;
            var ellipse = new GeoEllipse2(P(o, o), new GeoVector2(1, 0), 100, 50);
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(o - 30, o + 50), P(o + 30, o + 50) });

            GeoPoint2[] places = line.GetIntersections(ellipse, Strict);

            AssertTouch(places, o, P(o, o + 50));
            Assert.True(line.DistanceTo(ellipse, Strict) <= 1E-6);
        }

        [Fact]
        public void TheArchAndACircleInsideItTouchingItsTopAtAMillion_MeetAtThatPointFromEitherSide()
        {
            const double o = 1E6;
            GeoSpline2 arch = new GeoSpline2(3, new[] { P(o, o), P(o + 100, o + 150), P(o + 200, o + 150), P(o + 300, o) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(o + 150, o + 92.5), 20));

            GeoPoint2[] places = arch.GetIntersections(circle, Strict);

            Assert.Single(places);
            Assert.True(places[0].DistanceTo(P(o + 150, o + 112.5)) <= 1E-2, "the touch is at " + places[0]);
            Assert.Equal(arch.DistanceTo(circle, Strict), circle.DistanceTo(arch, Strict));
            Assert.Single(circle.GetIntersections(arch, Strict));
        }

        [Fact]
        public void APolylineOfFortyThousandSegments_ACrossingVerticalSegmentThroughItsMiddle_MeetsItOnce()
        {
            // A parabola y = o + x^2 / 2E4, cut into 40 000 segments, a million out; the vertical segment at x = o + 100 crosses it at y = o + 0.5.
            const double o = 1E6;
            var vertices = Enumerable.Range(0, 40001).Select(i => P(o + 0.005 * i, o + 0.005 * i * 0.005 * i / 2E4)).ToList();
            GeoSpline2 parabola = GeoSpline2.FromPolyline(new GeoPolyline2(vertices));
            var vertical = new GeoLine2(P(o + 100, o - 10), P(o + 100, o + 10));

            GeoPoint2[] places = parabola.GetIntersections(vertical, Strict);

            Assert.Single(places);
            Assert.True(places[0].DistanceTo(P(o + 100, o + 100.0 * 100.0 / 2E4)) <= 1E-3, "the crossing is at " + places[0]);
            Assert.Equal(0.0, parabola.DistanceTo(vertical, Strict));
        }

        #endregion

        #region Two splines within the point tolerance of each other for a length

        [Fact]
        public void TwoCirclesAHalfThousandthApart_AtAThousandth_LieAlongEachOther_AndMeetNowhere()
        {
            GeoSpline2 inner = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 1000));
            GeoSpline2 outer = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 1000.0005));

            Assert.Empty(inner.GetIntersections(outer, Strict));
            Assert.Empty(outer.GetIntersections(inner, Strict));
            Assert.True(inner.CollidesWith(outer, Strict));
            Assert.Equal(0.0005, inner.DistanceTo(outer, Strict), 9);
        }

        [Fact]
        public void TwoCirclesAHalfThousandthApart_AtAFortieth_OfAThousandth_AreApart_AndMeetNowhere()
        {
            GeoSpline2 inner = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 1000));
            GeoSpline2 outer = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 1000.0005));

            Assert.Empty(inner.GetIntersections(outer, Tight));
            Assert.False(inner.CollidesWith(outer, Tight));
            Assert.Equal(0.0005, inner.DistanceTo(outer, Tight), 9);
            Assert.Equal(0.0005, outer.DistanceTo(inner, Tight), 9);
        }

        [Fact]
        public void AFitToThirtySevenPointsOfAQuarterArc_AgainstThatArcAsASpline_LiesAlongIt()
        {
            var points = Enumerable.Range(0, 37).Select(i => P(1000.0 * Math.Cos(i * Math.PI / 72.0), 1000.0 * Math.Sin(i * Math.PI / 72.0))).ToList();
            GeoSpline2 fit = GeoSpline2.Interpolate(points, 3, Strict);
            GeoSpline2 exact = GeoSpline2.FromArc(new GeoArc2(P(0, 0), 1000, 0, Math.PI / 2));

            // Within the tolerance all along and level: a run, and no place in the middle, where the fit is 1E-8 off the arc. At each end
            // its end conditions let it stray by 1E-4, and it crosses the arc there, at places that are given, five at each end.
            GeoPoint2[] places = fit.GetIntersections(exact, Strict);

            Assert.True(places.Length <= 12, places.Length + " places");
            Assert.All(places, p => Assert.True(Math.Atan2(p.Y, p.X) < 0.1 * Math.PI / 2 || Math.Atan2(p.Y, p.X) > 0.9 * Math.PI / 2, "the place " + p + " is in the middle"));
            Assert.Equal(places.OrderBy(p => p.Y), exact.GetIntersections(fit, Strict).OrderBy(p => p.Y));
            Assert.True(fit.CollidesWith(exact, Strict));
            Assert.True(fit.DistanceTo(exact, Strict) <= 1E-3);
        }

        [Fact]
        public void AFitToThirtySevenPointsOfAQuarterArc_AgainstAnEllipseAGhostOffThatCircle_MeetsItAtMostOncePerFitPoint()
        {
            var points = Enumerable.Range(0, 37).Select(i => P(1000.0 * Math.Cos(i * Math.PI / 72.0), 1000.0 * Math.Sin(i * Math.PI / 72.0))).ToList();
            GeoSpline2 fit = GeoSpline2.Interpolate(points, 3, Strict);
            var ellipse = new GeoEllipse2(P(0, 0), new GeoVector2(1, 0), 1000, 1000 - 1E-9);

            GeoPoint2[] places = fit.GetIntersections(ellipse, Strict);

            Assert.True(places.Length <= 37, places.Length + " places");
            Assert.True(fit.CollidesWith(ellipse, Strict));
            Assert.Equal(places, ellipse.GetIntersections(fit, Strict));
        }

        [Theory]
        [InlineData(1E-4)]
        [InlineData(5E-4)]
        public void ASineSplineAndACopyMovedAHairUp_LieAlongEachOther_AndMeetNowhere(double moved)
        {
            var fit = Enumerable.Range(0, 21).Select(i => P(50.0 * i, 100 * Math.Sin(i * 0.3))).ToList();
            GeoSpline2 sine = GeoSpline2.Interpolate(fit, 3, Strict);
            GeoSpline2 copy = sine.Translate(new GeoVector2(0, moved));

            Assert.Empty(sine.GetIntersections(copy, Strict));
            Assert.Empty(copy.GetIntersections(sine, Strict));
            Assert.True(sine.CollidesWith(copy, Strict));
            Assert.True(sine.DistanceTo(copy, Strict) <= moved + 1E-9);
        }

        [Fact]
        public void TwoSplinesThatLieAlongEachOtherForAStretchAndThenCross_GiveTheCrossingOutsideTheStretch()
        {
            // A line along the x axis and a spline that is the same line for 400 and then climbs steeply through y = 1 at x = 450.
            GeoSpline2 axis = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(1000, 0) });
            GeoSpline2 bent = GeoSpline2.FromControlPoints(1, new[] { P(0, 0.0004), P(400, 0.0004), P(450, 5), P(500, -5) });

            GeoPoint2[] places = axis.GetIntersections(bent, Strict);

            Assert.True(axis.CollidesWith(bent, Strict));
            Assert.Contains(places, p => Math.Abs(p.X - 475) <= 0.01 && Math.Abs(p.Y) <= 1E-6);
            Assert.DoesNotContain(places, p => p.X < 400);
            Assert.Equal(places, bent.GetIntersections(axis, Strict));
        }

        #endregion
    }
}
