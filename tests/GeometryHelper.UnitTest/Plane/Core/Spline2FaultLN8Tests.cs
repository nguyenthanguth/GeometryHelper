using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A closed spline has no ends; a spline of arcs of circles meets a circle, an arc, or another such spline as the circles do; and
    /// two splines near copies of each other give the same places however the pair is turned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seam of a closed spline was read as an end: the line tangent to <see cref="GeoSpline2.FromCircle"/> of radius 100 at 0.003
    /// of a radian was also given the seam (100, 0), 4.5E-4 off it, as a place, where the circle gives the one touch.
    /// </para>
    /// <para>
    /// A circle spline moved a few ten-thousandths was given a place at its seam against the circle, and against the spline of that
    /// circle cut pairs of parts until the budget ran out and missed crossings: the splines of radius 100 000 and 100 000.0005 with
    /// their centres 0.01 apart took 16 seconds and gave no crossing, and were 4.8E-5 apart though they cross. Two arcs of circles now
    /// meet in closed form, by the rule of <see cref="GeoCircle2.GetIntersections(GeoCircle2, Tolerance)"/>, its one circle included.
    /// </para>
    /// <para>
    /// The stretches two near copies lie along each other for were as long as a quarter of the smaller span along the axes, which
    /// changes as the pair is turned: 14 of 60 copies moved a few ten-thousandths gave a place more or fewer turned by 0.7 of a radian.
    /// They are now a quarter of how far the control points reach from the first, as in space.
    /// </para>
    /// </remarks>
    public class Spline2FaultLN8Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoLine2 TangentAt(double radius, double angle, double half)
        {
            GeoPoint2 at = P(radius * Math.Cos(angle), radius * Math.Sin(angle));
            var along = new GeoVector2(-Math.Sin(angle), Math.Cos(angle));

            return new GeoLine2(P(at.X - half * along.X, at.Y - half * along.Y), P(at.X + half * along.X, at.Y + half * along.Y));
        }

        [Theory]
        [InlineData(0.003)]
        [InlineData(-0.003)]
        [InlineData(0.001)]
        public void ALineTangentToACircleSplineBesideItsSeam_TouchesItOnce(double angle)
        {
            var circle = new GeoCircle2(P(0, 0), 100);
            GeoSpline2 ring = GeoSpline2.FromCircle(circle);
            GeoLine2 line = TangentAt(100, angle, 50);
            GeoPoint2 touch = Assert.Single(circle.GetIntersections(line, Thousandth));

            Assert.True(ring.IsClosed(Thousandth));
            Assert.InRange(Assert.Single(ring.GetIntersections(line, Thousandth)).DistanceTo(touch), 0.0, 1E-5);
            Assert.InRange(Assert.Single(line.GetIntersections(ring, Thousandth)).DistanceTo(touch), 0.0, 1E-5);
            Assert.InRange(Assert.Single(ring.GetIntersections(new GeoXLine2(line.StartPoint, line.Direction), Thousandth)).DistanceTo(touch), 0.0, 1E-5);
        }

        [Fact]
        public void ALineTangentToAClosedSplineAtItsSeam_TouchesItThere()
        {
            GeoSpline2 ring = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 100));

            Assert.Equal(0.0, Assert.Single(ring.GetIntersections(TangentAt(100, 0.0, 50), Thousandth)).DistanceTo(P(100, 0)), 9);
        }

        /// <summary>
        /// A circle and the same circle moved, each as itself and as its spline, from either side: as many places as the two circles
        /// give, and the same places to a millionth.
        /// </summary>
        private static void AssertAsTheCircles(GeoCircle2 circle, GeoCircle2 moved, int expected)
        {
            GeoPoint2[] exact = moved.GetIntersections(circle, Thousandth);
            GeoSpline2 movedSpline = GeoSpline2.FromCircle(moved);
            GeoSpline2 spline = GeoSpline2.FromCircle(circle);

            Assert.Equal(expected, exact.Length);

            foreach (GeoPoint2[] places in new[]
            {
                movedSpline.GetIntersections(circle, Thousandth),
                circle.GetIntersections(movedSpline, Thousandth),
                movedSpline.GetIntersections(spline, Thousandth),
                spline.GetIntersections(movedSpline, Thousandth),
            })
            {
                Assert.Equal(expected, places.Length);

                foreach (GeoPoint2 place in places)
                {
                    Assert.Contains(exact, p => p.DistanceTo(place) < 1E-6);
                }
            }
        }

        [Fact]
        public void ACircleSplineMovedToCrossTheCircle_CrossesItAndItsSplineWhereTheCirclesCross()
        {
            AssertAsTheCircles(
                new GeoCircle2(P(901.4389924246068, 948.9434119076207), 24.947747268543765),
                new GeoCircle2(P(901.4389779037871, 948.9441528391063), 24.947145993772455),
                2);
        }

        [Fact]
        public void ASmallCircleSplineMovedToCrossTheCircle_CrossesItAndItsSplineWhereTheCirclesCross()
        {
            AssertAsTheCircles(
                new GeoCircle2(P(-906.5143046465304, 911.0924861911183), 3.6037337484887098),
                new GeoCircle2(P(-906.5151618244047, 911.0935218721324), 3.603830174325828),
                2);
        }

        [Fact]
        public void ACircleSplineMovedLessThanTheTolerance_IsOneCircleWithTheCircleAndItsSpline()
        {
            AssertAsTheCircles(
                new GeoCircle2(P(-533.0696439058844, 452.8232596129287), 5.968916233706382),
                new GeoCircle2(P(-533.0696471073766, 452.82321214859485), 5.968916233706382),
                0);
        }

        [Fact]
        public void AnArcSplineOfTheCircleMovedLessThanTheTolerance_MeetsAnArcOfItNowhere()
        {
            // Within 9E-4 of each other all round, the arcs of the one circle overlap from 1.3 to 2.5 radians, and their ends lie
            // within the tolerance of the other: no place, as the arcs give none.
            var arc = new GeoArc2(P(100, 200), 50, 1.0, 2.5);
            GeoSpline2 moved = GeoSpline2.FromArc(new GeoArc2(P(100.0004, 200.0003), 50.0004, 1.3, 2.8));

            Assert.Empty(new GeoArc2(P(100.0004, 200.0003), 50.0004, 1.3, 2.8).GetIntersections(arc, Thousandth));
            Assert.Empty(moved.GetIntersections(arc, Thousandth));
            Assert.Empty(moved.GetIntersections(GeoSpline2.FromArc(arc), Thousandth));
            Assert.Empty(moved.GetIntersections(new GeoCircle2(P(100, 200), 50), Thousandth));
        }

        [Fact]
        public void TwoCircleSplinesNearlyAboutOneCentreThatCross_CrossWhereTheCirclesCrossAtOnce()
        {
            var first = new GeoCircle2(P(0, 0), 1E5);
            var second = new GeoCircle2(P(0.01, 0), 1E5 + 0.0005);
            GeoPoint2[] exact = first.GetIntersections(second, Thousandth);
            GeoSpline2 a = GeoSpline2.FromCircle(first);
            GeoSpline2 b = GeoSpline2.FromCircle(second);
            var watch = Stopwatch.StartNew();

            GeoPoint2[] places = a.GetIntersections(b, Thousandth);
            double apart = a.DistanceTo(b, Thousandth);

            Assert.InRange(watch.ElapsedMilliseconds, 0, 2000);
            Assert.Equal(2, exact.Length);
            Assert.Equal(2, places.Length);

            // The two nearly concentric circles cross at an angle of a ten-millionth of a radian, so the rounding of numbers of 100 000
            // moves a crossing along them by a few millionths (4.5E-6 here, the centres being read off the control points); the splines'
            // points lie on both curves to a hundred-millionth.
            foreach (GeoPoint2 place in places)
            {
                Assert.Contains(exact, p => p.DistanceTo(place) < 1E-4);
                Assert.InRange(Math.Abs(place.DistanceTo(first.Center) - first.Radius), 0.0, 1E-8);
                Assert.InRange(Math.Abs(place.DistanceTo(second.Center) - second.Radius), 0.0, 1E-8);
            }

            Assert.Equal(0.0, apart);
            Assert.True(a.CollidesWith(b, Thousandth));
        }

        [Theory]
        [InlineData(1E7, 1E5, 1E5, 5E-5, 3E-4, 0.0, 1E-4, true)]
        [InlineData(2.5E8, 0.0, 0.0, 2E-7, 3E-4, 0.0, 4E-7, true)]
        [InlineData(1E6, 0.0, 0.0, 1.5E-4, 0.0, 2E-4, 3E-4, false)]
        public void AFlatArcSplineAgainstAnArcOfNearlyItsCircle_MeetsItAsTheArcsDo(double radius, double topX, double topY, double half, double moveX, double grow, double otherHalf, bool againstTheArc)
        {
            // An arc of radius 10 000 000 a hundred thousand out, a thousand long, and one of radius 250 000 000 and chord 100, against
            // the circle moved 0.0003 along the chord; and one of radius 1 000 000 and chord 300 against the arc of the circle 0.0002
            // larger about the same centre. The centre of such an arc read from its control points is off by up to 0.6 and 2.3E-4, and the
            // circles' rule read so gave the splines 1 to 4 places the arcs do not have; the pieces are now cut. The last spline against
            // the arc itself is cut as it always was, and given 2 places it does not have.
            var centre = P(topX, topY - radius);
            var arc = new GeoArc2(centre, radius, Math.PI / 2 - half, Math.PI / 2 + half);
            var other = new GeoArc2(P(centre.X + moveX, centre.Y), radius + grow, Math.PI / 2 - otherHalf, Math.PI / 2 + otherHalf);
            int expected = arc.GetIntersections(other, Thousandth).Length;
            GeoSpline2 spline = GeoSpline2.FromArc(arc);

            Assert.Equal(expected, spline.GetIntersections(GeoSpline2.FromArc(other), Thousandth).Length);
            Assert.Equal(expected, GeoSpline2.FromArc(other).GetIntersections(spline, Thousandth).Length);

            if (againstTheArc)
            {
                Assert.Equal(expected, spline.GetIntersections(other, Thousandth).Length);
                Assert.Equal(expected, other.GetIntersections(spline, Thousandth).Length);
            }
        }

        [Fact]
        public void ANearCopyOfASpline_GivesTheSamePlacesTurned()
        {
            GeoSpline2 spline = GeoSpline2.FromControlPoints(2, new[]
            {
                P(5.083271272053599, -5.105790121064421), P(-15.678945870920527, -19.026555260190065), P(3.1332935547145464, -17.789843402705085),
                P(-24.468244465286034, -7.539400759869908), P(-19.13079290144649, -7.292941639801926), P(-12.247571513172051, -13.138004333310764),
            });
            GeoSpline2 copy = spline.Translate(new GeoVector2(-0.00025720663100144543, 0.0007134552540530254));
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), 0.7);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -0.7);

            // The copy crosses where the spline's tangent runs along the move, three times, and at each of the two places the spline
            // crosses itself, at 67 and 17 degrees, twice, its other branch crossing the copy 0.0011 and 0.0034 apart: seven, as the
            // exact curves cross (G6). The count of three left out the four, the parts inside the stretch the two lie along never
            // being cut against the copy's other branch.
            GeoPoint2[] places = spline.GetIntersections(copy, Thousandth);
            GeoPoint2[] turned = spline.TransformBy(turn).GetIntersections(copy.TransformBy(turn), Thousandth).Select(p => p.TransformBy(back)).ToArray();

            Assert.Equal(7, places.Length);
            Assert.Equal(7, turned.Length);

            foreach (GeoPoint2 place in turned)
            {
                Assert.Contains(places, p => p.DistanceTo(place) < 1E-6);
            }
        }
    }
}
