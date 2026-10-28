using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two splines that lie on each other exactly for a stretch between two lobes of their offset cross once, where they come on each
    /// other, however the pair is turned; and a copy of a spline that crosses itself crosses the spline at that crossing, inside the
    /// stretch the two lie along each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A polyline over a line from 0 to 10, on it from 10 to 50 and under it after, crosses it once. The place was wherever the
    /// bisection of a change of sign of the offset, or Newton's method, settled in the rounding of the overlap: at 10 as the pair lay,
    /// at 34.95 turned by 0.3 of a radian, at 30.18 turned by 0.7 with the polyline reversed; and the end of the overlap the other side,
    /// at 50, could be given as well, found by a part of the curve cut across the stretch. Between two lobes to one side the two do not
    /// cross, and the ends of the overlap were given as places.
    /// </para>
    /// <para>
    /// A spline that crosses itself and its copy moved a few ten-thousandths lie along each other within the tolerance nearly all the
    /// way, and the copy's other branch crosses the spline at the loop's own crossing, at the angle of the loop. That crossing was a
    /// meeting found inside the stretch along, where a place is the rounding of the two lying on each other, and it was taken out,
    /// asked either way and turned.
    /// </para>
    /// </remarks>
    public class Spline2FaultG6Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// The places the first curve gives against the second, asked from the first and from the second, with both turned about the
        /// origin and the places turned back.
        /// </summary>
        private static GeoPoint2[][] Asked(GeoSpline2 first, GeoSpline2 second, double angle)
        {
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), angle);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -angle);
            GeoSpline2 a = first.TransformBy(turn);
            GeoSpline2 b = second.TransformBy(turn);

            return new[]
            {
                a.GetIntersections(b, Thousandth).Select(p => p.TransformBy(back)).ToArray(),
                b.GetIntersections(a, Thousandth).Select(p => p.TransformBy(back)).ToArray(),
            };
        }

        [Theory]
        [InlineData(0.0, false)]
        [InlineData(0.3, false)]
        [InlineData(0.7, false)]
        [InlineData(2.0, false)]
        [InlineData(0.0, true)]
        [InlineData(0.3, true)]
        [InlineData(0.7, true)]
        public void APolylineOnALineBetweenLobesToEitherSide_CrossesItOnceWhereItComesOnIt_HoweverTurned(double angle, bool reversed)
        {
            // Over the line by 3E-4 at the start, on it from 10 to 50, under it by 3E-4 from 60: the line comes first of the two, and
            // its earlier lobe is the one before 10.
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(100, 0) });
            GeoSpline2 polyline = GeoSpline2.FromControlPoints(1, new[] { P(0, 3E-4), P(10, 0), P(50, 0), P(60, -3E-4), P(100, -3E-4) });

            foreach (GeoPoint2[] places in Asked(reversed ? polyline.Reverse() : polyline, line, angle))
            {
                Assert.InRange(Assert.Single(places).DistanceTo(P(10, 0)), 0.0, 1E-6);
            }
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.3)]
        [InlineData(0.7)]
        public void APolylineOnALineBetweenTwoLobesToOneSide_DoesNotCrossIt(double angle)
        {
            // Over the line by 3E-4 before 20 and by 3E-4 to 4.5E-4 after 50, on it between, and away from it by half a unit at either
            // end, where it leaves the tolerance of the line.
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(-1, 0), P(81, 0) });
            GeoSpline2 polyline = GeoSpline2.FromControlPoints(1, new[] { P(-10, 0.5), P(0, 3E-4), P(20, 0), P(50, 0), P(60, 3E-4), P(80, 4.5E-4), P(90, 0.5) });

            foreach (GeoPoint2[] places in Asked(polyline, line, angle))
            {
                Assert.Empty(places);
            }

            Assert.True(polyline.CollidesWith(line, Thousandth));
        }

        [Theory]
        [InlineData(3E-4, 4E-4, 0.0, 3)]
        [InlineData(3E-4, 4E-4, 0.7, 3)]
        [InlineData(-5E-4, 1E-4, 0.0, 2)]
        [InlineData(-5E-4, 1E-4, 0.7, 2)]
        [InlineData(2E-4, -6E-4, 0.0, 3)]
        [InlineData(2E-4, -6E-4, 0.7, 3)]
        public void ACopyOfASplineThatCrossesItselfMovedAFewTenThousandths_CrossesItAtTheLoopsOwnCrossing(double x, double y, double angle, int count)
        {
            // The cubic crosses itself at (50, 42.857) at 74 degrees; its copy's other branch crosses it there, twice, within the
            // tolerance of each other, which is one place; besides it the copy crosses where the cubic's tangent runs along the move,
            // once or twice, as the exact curves do.
            GeoSpline2 loop = GeoSpline2.FromControlPoints(3, new[] { P(0, 0), P(150, 100), P(-50, 100), P(100, 0) });
            GeoSpline2 copy = loop.Translate(new GeoVector2(x, y));
            GeoPoint2 crossing = P(50, 42.857142857142854);

            foreach (GeoPoint2[] places in Asked(loop, copy, angle))
            {
                Assert.Equal(count, places.Length);
                Assert.Contains(places, p => p.DistanceTo(crossing) <= 1E-3);
                Assert.All(places, p => Assert.InRange(loop.DistanceTo(p), 0.0, 1E-9));
                Assert.All(places, p => Assert.InRange(copy.DistanceTo(p), 0.0, 1E-9));
            }
        }

        [Fact]
        public void TwoCrossingsAtALoopsOwnCrossingOnePlaceApart_AreStoodForByTheSameOneHoweverTurned()
        {
            // Moved 0.0008 square to the line between the two tangents of the loop's crossing, the copy crosses the loop at
            // (49.9997, 42.8575) and (50.0003, 42.8575), 0.0006 apart, one place: the one first along the two curves together is, to a
            // billionth, neither, and the one nearer the point half way between the starts of the two curves stands for it.
            GeoSpline2 loop = GeoSpline2.FromControlPoints(3, new[] { P(0, 0), P(150, 100), P(-50, 100), P(100, 0) });
            GeoSpline2 copy = loop.Translate(new GeoVector2(0, 8E-4));
            GeoPoint2 crossing = P(50, 42.857142857142854);
            GeoPoint2 first = loop.GetIntersections(copy, Thousandth).Single(p => p.DistanceTo(crossing) < 0.01);

            foreach (double angle in new[] { 0.3, 0.7, 1.1, 2.0, 3.0, 4.0, 5.5 })
            {
                foreach (GeoPoint2[] places in Asked(loop, copy, angle))
                {
                    Assert.InRange(places.Single(p => p.DistanceTo(crossing) < 0.01).DistanceTo(first), 0.0, 1E-9);
                }
            }
        }

        [Theory]
        [InlineData(1E-3, 35.0)]
        [InlineData(1E-2, 35.0)]
        [InlineData(1E-3, 20.0)]
        [InlineData(1E-3, 19.5)]
        public void APolylineOnALineThatComesBackSquareAcrossIt_CrossesItThere(double tolerance, double at)
        {
            // On the line from 20 to 50 between two lobes over it by thirty hundredths of the tolerance, away from it at either end, and
            // then back square across it at the given x: that crossing, of another branch of the polyline, is the one place, inside the
            // overlap, at its end or before it.
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(-1, 0), P(81, 0) });
            double lobe = 0.3 * tolerance;
            GeoSpline2 polyline = GeoSpline2.FromControlPoints(1, new[]
            {
                P(-10, 0.5), P(0, lobe), P(20, 0), P(50, 0), P(60, lobe), P(80, 1.5 * lobe), P(90, 0.5), P(90, 5), P(at, 5), P(at, -5),
            });
            var tol = new Tolerance(tolerance, 1E-5);

            foreach (double angle in new[] { 0.0, 0.7 })
            {
                foreach (GeoPoint2[] places in AskedAt(polyline, line, angle, tol).Concat(AskedAt(polyline.Reverse(), line, angle, tol)))
                {
                    Assert.InRange(Assert.Single(places).DistanceTo(P(at, 0)), 0.0, 1E-6);
                }
            }
        }

        [Theory]
        [InlineData(0.0, 30.0)]
        [InlineData(-2E-6, 30.0)]
        [InlineData(0.0, 50.0)]
        [InlineData(-2E-6, 50.0)]
        public void APolylineOnALineBetweenLobesToEitherSideThatComesBackSquareAcrossIt_CrossesItTwice(double dip, double at)
        {
            // On the line from 10 to 50, or 2E-6 under it at 50, between a lobe over it and one under it, and then back square across
            // it at 30, inside the overlap, or at 50, its end: at 10 and there.
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(100, 0) });
            GeoSpline2 polyline = GeoSpline2.FromControlPoints(1, new[]
            {
                P(0, 3E-4), P(10, 0), P(50, dip), P(60, -3E-4), P(100, -3E-4), P(120, -5), P(at, -5), P(at, 5),
            });

            foreach (double angle in new[] { 0.0, 0.7 })
            {
                foreach (GeoPoint2[] places in Asked(polyline, line, angle).Concat(Asked(polyline.Reverse(), line, angle)))
                {
                    Assert.Equal(2, places.Length);
                    Assert.Contains(places, p => p.DistanceTo(P(10, 0)) <= 1E-6);
                    Assert.Contains(places, p => p.DistanceTo(P(at, 0)) <= 1E-6);
                }
            }
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void APolylineThatComesOnALineFromTheSideItLeavesItTo_CrossesItWhereItChangesSide(double tolerance)
        {
            // Over the line to 10 by thirty hundredths of the tolerance, under it by half a hundredth of it to 30, on it to 50
            // and under it after: it crosses at 10, and from 30 only touches the line from under. The other way round, on it from 10
            // to 30 and over it by the hair to 40, it crosses at 40.
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(100, 0) });
            double lobe = 0.3 * tolerance;
            double hair = 5E-3 * tolerance;
            GeoSpline2 before = GeoSpline2.FromControlPoints(1, new[] { P(0, lobe), P(10, 0), P(20, -hair), P(30, 0), P(50, 0), P(60, -lobe), P(100, -lobe) });
            GeoSpline2 after = GeoSpline2.FromControlPoints(1, new[] { P(0, lobe), P(10, 0), P(30, 0), P(35, hair), P(40, 0), P(50, -lobe), P(100, -lobe) });
            var tol = new Tolerance(tolerance, 1E-5);

            foreach (double angle in new[] { 0.0, 0.3, 0.7, 2.0 })
            {
                foreach (GeoPoint2[] places in AskedAt(before, line, angle, tol).Concat(AskedAt(before.Reverse(), line, angle, tol)))
                {
                    Assert.InRange(Assert.Single(places).DistanceTo(P(10, 0)), 0.0, 1E-5);
                }

                foreach (GeoPoint2[] places in AskedAt(after, line, angle, tol).Concat(AskedAt(after.Reverse(), line, angle, tol)))
                {
                    Assert.InRange(Assert.Single(places).DistanceTo(P(40, 0)), 0.0, 1E-5);
                }
            }
        }

        [Theory]
        [InlineData(5.0, 90, 1E-2)]
        [InlineData(5.0, 120, 1E-2)]
        [InlineData(5.0, 180, 1E-2)]
        [InlineData(10.0, 100, 1E-2)]
        [InlineData(2.0, 170, 1E-3)]
        [InlineData(1.5, 150, 1E-3)]
        public void ACircleSplineAgainstThePolygonInscribedInIt_MeetsItNowhereAtItsSeam(double radius, int corners, double tolerance)
        {
            // The polygon's corners lie on the circle, one of them on its seam at (radius, 0), and its sides within the tolerance of
            // it: the two lie along each other all round and meet nowhere, the corner on the seam no more than any other.
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), radius));
            GeoSpline2 polygon = GeoSpline2.FromPolyline(new GeoPolyline2(Enumerable.Range(0, corners + 1).Select(i =>
            {
                double angle = 2 * Math.PI * (i % corners) / corners;
                return P(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }).ToArray()));
            var tol = new Tolerance(tolerance, 1E-5);

            Assert.Empty(circle.GetIntersections(polygon, tol));
            Assert.Empty(polygon.GetIntersections(circle, tol));
        }

        /// <summary>
        /// As <see cref="Asked"/>, at a tolerance.
        /// </summary>
        private static GeoPoint2[][] AskedAt(GeoSpline2 first, GeoSpline2 second, double angle, Tolerance tolerance)
        {
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), angle);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -angle);
            GeoSpline2 a = first.TransformBy(turn);
            GeoSpline2 b = second.TransformBy(turn);

            return new[]
            {
                a.GetIntersections(b, tolerance).Select(p => p.TransformBy(back)).ToArray(),
                b.GetIntersections(a, tolerance).Select(p => p.TransformBy(back)).ToArray(),
            };
        }
    }
}
