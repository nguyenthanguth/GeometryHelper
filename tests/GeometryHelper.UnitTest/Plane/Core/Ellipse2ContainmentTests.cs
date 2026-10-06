using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Where a point stands against an ellipse, and which rim point is closest to it. The rim is read as a distance in
    /// drawing units: a point within EqualPoint of it is on it, however thin or large the ellipse, which the equation
    /// u² + v² = 1 compared with a number would not give. Closest points are held against a dense sample of the rim
    /// (<see cref="Ellipse2Oracle.ClosestDistance"/>); where two rim points are equally close, the spec picks one.
    /// </summary>
    public class Ellipse2ContainmentTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        // Eccentric angles all round, none at an end of an axis, in all four quadrants.
        private static readonly double[] Angles = { 0.3, 1.1, 2.0, 2.9, 3.7, 4.4, 5.2, 6.0 };

        [Fact]
        public void APointWellInsideIsInsideAndContained()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            foreach (GeoPoint2 point in new[] { ellipse.Center, Ellipse2Oracle.FromFrame(ellipse, 200, 50), Ellipse2Oracle.FromFrame(ellipse, -20, -90) })
            {
                Assert.Equal(PointLocation.Inside, ellipse.Locate(point, Tol));
                Assert.True(ellipse.Contains(point, Tol));
                Assert.False(ellipse.IsPointOn(point, Tol));
            }
        }

        [Fact]
        public void APointFourTenthsOfTheToleranceOffTheRimEitherWayIsOnIt()
        {
            // 0.0004 out along the normal and 0.0004 in: both within the thousandth of the rim.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            foreach (double t in Angles)
            {
                foreach (double offset in new[] { 0.0004, -0.0004 })
                {
                    GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, t, offset);

                    Assert.Equal(PointLocation.OnSide, ellipse.Locate(point, Tol));
                    Assert.True(ellipse.IsPointOn(point, Tol));
                    Assert.True(ellipse.Contains(point, Tol));
                }
            }
        }

        [Fact]
        public void APointThreeTolerancesOutsideTheRimIsOutside()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            foreach (double t in Angles)
            {
                GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, t, 0.003);

                Assert.Equal(PointLocation.OutSide, ellipse.Locate(point, Tol));
                Assert.False(ellipse.IsPointOn(point, Tol));
                Assert.False(ellipse.Contains(point, Tol));
            }
        }

        [Fact]
        public void APointThreeTolerancesInsideTheRimIsInside()
        {
            // 0.003 in is far below the least radius of curvature, 33.3 at the ends of the major axis, so the point
            // lies 0.003 from the rim.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            foreach (double t in Angles)
            {
                GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, t, -0.003);

                Assert.Equal(PointLocation.Inside, ellipse.Locate(point, Tol));
                Assert.False(ellipse.IsPointOn(point, Tol));
                Assert.True(ellipse.Contains(point, Tol));
            }
        }

        [Fact]
        public void ALargeEllipseIsNotOnItsRimThirtyUnitsAway()
        {
            // 100 000 by 50 000, and points 30 beyond and 30 short of the end of the major axis. There u² - 1 is
            // +6.0E-4 and -6.0E-4, inside a thousandth, so the equation read against the tolerance would call both on
            // the rim; they are 30 off it.
            GeoEllipse2 large = Ellipse2Oracle.Upright(100000, 50000);
            GeoPoint2 beyond = Ellipse2Oracle.FromFrame(large, 100030, 0);
            GeoPoint2 shortOf = Ellipse2Oracle.FromFrame(large, 99970, 0);

            Assert.Equal(PointLocation.OutSide, large.Locate(beyond, Tol));
            Assert.False(large.Contains(beyond, Tol));
            Assert.Equal(PointLocation.Inside, large.Locate(shortOf, Tol));
            Assert.False(large.IsPointOn(shortOf, Tol));
        }

        [Fact]
        public void ANarrowEllipseIsOnItsRimFiveTenThousandthsAway()
        {
            // 50 by 0.002, and a point 0.0005 beyond the end of the minor axis: v is 1.25, so u² + v² - 1 is 0.5625, far
            // over a thousandth, yet the point is half a thousandth from the rim and on it. 0.003 beyond is outside.
            GeoEllipse2 narrow = Ellipse2Oracle.Upright(50, 0.002);

            Assert.Equal(PointLocation.OnSide, narrow.Locate(Ellipse2Oracle.FromFrame(narrow, 0, 0.0025), Tol));
            Assert.True(narrow.Contains(Ellipse2Oracle.FromFrame(narrow, 0, 0.0025), Tol));
            Assert.Equal(PointLocation.OutSide, narrow.Locate(Ellipse2Oracle.FromFrame(narrow, 0, 0.005), Tol));
        }

        [Fact]
        public void AnEllipseThinnerThanThePointToleranceIsReadWithItLikeAnyOtherShape()
        {
            // 50 by 0.0005. Its centre is 0.0005 from the rim, so on it, and so is every point of its major axis between
            // the ends: (10, 0) is 0.00049 from the rim points just above and below it. 0.0012 up is 0.0007 off, on it still;
            // 0.003 up is 0.0025 off, outside. Beyond the end of the major axis, 0.0006 out is on it and 0.003 out is not.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(50, 0.0005);

            Assert.Equal(PointLocation.OnSide, thin.Locate(thin.Center, Tol));
            Assert.True(thin.Contains(thin.Center, Tol));
            Assert.Equal(PointLocation.OnSide, thin.Locate(Ellipse2Oracle.FromFrame(thin, 10, 0), Tol));
            Assert.Equal(PointLocation.OnSide, thin.Locate(Ellipse2Oracle.FromFrame(thin, 0, 0.0012), Tol));
            Assert.Equal(PointLocation.OutSide, thin.Locate(Ellipse2Oracle.FromFrame(thin, 0, 0.003), Tol));
            Assert.False(thin.Contains(Ellipse2Oracle.FromFrame(thin, 0, 0.003), Tol));
            Assert.Equal(PointLocation.OnSide, thin.Locate(Ellipse2Oracle.FromFrame(thin, 50.0006, 0), Tol));
            Assert.Equal(PointLocation.OutSide, thin.Locate(Ellipse2Oracle.FromFrame(thin, 50.003, 0), Tol));
        }

        [Fact]
        public void ACircleHoldsItsRimAsGeoCircle2Does()
        {
            // GeoCircle2 contains a point within EqualPoint outside its rim (Containment2.Contains: distance <= radius +
            // EqualPoint) and calls a point within EqualPoint either side OnSide (Containment2.Locate). Radius 250 with
            // the axis along (1, 1), at points 0.0004 and 0.003 either side of the rim and well inside and outside.
            var circle = new GeoCircle2(new GeoPoint2(12, -7), 250);
            var ellipse = new GeoEllipse2(circle.Center, new GeoVector2(1, 1), 250, 250);

            foreach (double angle in Angles)
            {
                foreach (double offset in new[] { -100.0, -0.003, -0.0004, 0.0004, 0.003, 100.0 })
                {
                    double r = 250 + offset;
                    var point = new GeoPoint2(12 + r * Math.Cos(angle), -7 + r * Math.Sin(angle));

                    Assert.Equal(circle.Locate(point, Tol), ellipse.Locate(point, Tol));
                    Assert.Equal(circle.Contains(point, Tol), ellipse.Contains(point, Tol));
                    Assert.Equal(circle.IsPointOn(point, Tol), ellipse.IsPointOn(point, Tol));
                }
            }
        }

        [Fact]
        public void AtTheCentreTheClosestRimPointIsTheEndOfTheMinorAxisOnItsPositiveSide()
        {
            // The two ends of the minor axis are equally close, 100 away; the spec picks +b, at t = pi/2.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPoint2 expected = ellipse.Center + ellipse.MinorAxis * 100.0;

            Assert.True(ellipse.GetClosestPointOnBoundary(ellipse.Center).IsEqualTo(expected, Tight));
            Assert.Equal(Math.PI / 2, ellipse.GetAngleAtPoint(ellipse.Center), 1E-12);
        }

        [Fact]
        public void AtTheCentreOfACircleTheClosestPointIsAQuarterTurnRoundAsForAnyEllipse()
        {
            // GeoCircle2 answers its centre with the point at angle nought (Projection2.ProjectToCircle). An ellipse
            // that is a circle answers as every ellipse does, at t = pi/2: here (12, -7) + 250 (-0.6, 0.8).
            var ellipse = new GeoEllipse2(new GeoPoint2(12, -7), new GeoVector2(0.8, 0.6), 250, 250);

            Assert.True(ellipse.GetClosestPointOnBoundary(ellipse.Center).IsEqualTo(new GeoPoint2(-138, 193), Tight));
            Assert.Equal(Math.PI / 2, ellipse.GetAngleAtPoint(ellipse.Center), 1E-12);
        }

        [Fact]
        public void OnTheMajorAxisInsideTheEvoluteTheClosestPointIsTheOneAboveTheAxis()
        {
            // 300 by 100: the tip is the nearest point of the major axis only beyond (a² - b²)/a = 266.7 from the centre.
            // From (100, 0) the nearest are (112.5, ±92.70), solved from the normal at 40 digits; the spec picks v >= 0.
            // The ellipse is upright, so the point's v is exactly nought and the tie is the spec's, not rounding's.
            GeoEllipse2 ellipse = Ellipse2Oracle.Upright(300, 100);

            GeoPoint2 right = ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 100, 0));
            GeoPoint2 left = ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, -100, 0));

            Assert.True(right.IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 112.5, 92.70248108869579), Tight));
            Assert.True(left.IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, -112.5, 92.70248108869579), Tight));
            Assert.Equal(Math.Acos(112.5 / 300), ellipse.GetAngleAtPoint(Ellipse2Oracle.FromFrame(ellipse, 100, 0)), 1E-12);
        }

        [Fact]
        public void JustOffTheMajorAxisInsideTheEvoluteTheClosestPointIsOnThatSide()
        {
            // Either side of the tie above: a hundredth above the axis the upper point is nearer, a hundredth below the
            // lower one. Tilted, so the frame is not the drawing's.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            Ellipse2Oracle.ToFrame(ellipse, ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 100, 0.01)), out double xUp, out double yUp);
            Ellipse2Oracle.ToFrame(ellipse, ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 100, -0.01)), out double xDown, out double yDown);

            Assert.InRange(yUp, 92.0, 93.5);
            Assert.InRange(yDown, -93.5, -92.0);
            Assert.Equal(xUp, xDown, 1E-9);
        }

        [Fact]
        public void OnTheMajorAxisBeyondTheEvoluteTheClosestPointIsTheEndOfIt()
        {
            // 290 from the centre is past 266.7, inside; 400 is outside. Both are nearest the end at 300, 10 and 100 away.
            GeoEllipse2 ellipse = Ellipse2Oracle.Upright(300, 100);

            Assert.True(ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 290, 0)).IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 300, 0), Tight));
            Assert.True(ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 400, 0)).IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 300, 0), Tight));
            Assert.True(ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, -290, 0)).IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, -300, 0), Tight));
            Assert.Equal(0.0, ellipse.GetAngleAtPoint(Ellipse2Oracle.FromFrame(ellipse, 400, 0)), 1E-12);
            Assert.Equal(Math.PI, ellipse.GetAngleAtPoint(Ellipse2Oracle.FromFrame(ellipse, -290, 0)), 1E-12);
        }

        [Fact]
        public void OnTheMinorAxisTheClosestPointIsTheNearerEndOfIt()
        {
            // (0, 50) inside and (0, -250) outside: 50 from (0, 100) and 150 from (0, -100).
            GeoEllipse2 ellipse = Ellipse2Oracle.Upright(300, 100);

            Assert.True(ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 0, 50)).IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100), Tight));
            Assert.True(ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 0, -50)).IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 0, -100), Tight));
            Assert.True(ellipse.GetClosestPointOnBoundary(Ellipse2Oracle.FromFrame(ellipse, 0, -250)).IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 0, -100), Tight));
            Assert.Equal(3 * Math.PI / 2, ellipse.GetAngleAtPoint(Ellipse2Oracle.FromFrame(ellipse, 0, -250)), 1E-12);
        }

        [Theory]
        [InlineData(300.0, 100.0)]
        [InlineData(1000.0, 1.0)]
        [InlineData(1000.0, 999.0)]
        [InlineData(300.0, 299.9999999)]
        [InlineData(1000.0, 0.001)]
        public void TheClosestPointIsAsNearAsADenseSampleOfTheRimFinds(double majorRadius, double minorRadius)
        {
            // 300 by 100; a thousand to one; a thousandth and 1E-7 short of a circle; and a million to one, as thin as
            // the point tolerance. 60 points from a fixed seed, inside and outside, in all four quadrants, up to twice
            // the radii from the centre. The point found lies on the rim, is the one its angle names, and is as near as
            // the nearest of 65 536 rim points refined by golden section, to 1E-9.
            var ellipse = new GeoEllipse2(new GeoPoint2(-35, 60), new GeoVector2(Math.Cos(2.5), Math.Sin(2.5)), majorRadius, minorRadius);
            var random = new Random(20261006);

            for (int i = 0; i < 60; i++)
            {
                double x = (random.NextDouble() * 4.0 - 2.0) * majorRadius;
                double y = (random.NextDouble() * 4.0 - 2.0) * minorRadius;
                GeoPoint2 point = Ellipse2Oracle.FromFrame(ellipse, x, y);

                GeoPoint2 closest = ellipse.GetClosestPointOnBoundary(point);

                Assert.InRange(Ellipse2Oracle.GapToRim(ellipse, closest), 0.0, 1E-9);
                Assert.Equal(Ellipse2Oracle.ClosestDistance(majorRadius, minorRadius, x, y), point.DistanceTo(closest), 1E-9);
                Assert.True(ellipse.GetPointAtAngle(ellipse.GetAngleAtPoint(point)).IsEqualTo(closest, Tight));
            }
        }

        [Fact]
        public void ARimPointIsItsOwnClosestPointAndGivesBackItsAngle()
        {
            // All round, on a 300 by 100 and on a 1 000 by 1, whose ends have a radius of curvature of a thousandth.
            foreach (GeoEllipse2 ellipse in new[] { Ellipse2Oracle.Tilted(), new GeoEllipse2(new GeoPoint2(5, 5), new GeoVector2(-1, 2), 1000, 1) })
            {
                for (int k = 0; k < 64; k++)
                {
                    double t = (k + 0.37) * 2.0 * Math.PI / 64;
                    GeoPoint2 rim = Ellipse2Oracle.Rim(ellipse, t);

                    Assert.True(ellipse.GetClosestPointOnBoundary(rim).IsEqualTo(rim, Tight));
                    Assert.Equal(t, ellipse.GetAngleAtPoint(rim), 1E-9);
                }
            }
        }

        [Fact]
        public void TheAngleAtAPointIsWrappedIntoOneTurnFromNought()
        {
            // Below the major axis the angle runs on past pi rather than turning negative: a point 20 out from t = 5.5
            // gives 5.5, not 5.5 - 2 pi. The end of the major axis gives nought, never 2 pi: the ellipse is upright, so
            // the point's v is exactly nought.
            GeoEllipse2 ellipse = Ellipse2Oracle.Upright(300, 100);

            Assert.Equal(5.5, ellipse.GetAngleAtPoint(Ellipse2Oracle.OffRim(ellipse, 5.5, 20)), 1E-9);
            Assert.Equal(0.0, ellipse.GetAngleAtPoint(Ellipse2Oracle.FromFrame(ellipse, 300, 0)));
        }

        [Fact]
        public void ACircleFindsTheClosestPointsGeoCircle2Finds()
        {
            // Radius 250, the axis along (0.8, 0.6), 0.6435 from X. Away from the centre the closest point is the
            // circle's; the angle is the circle's less the turn of the axis, since t is counted from the axis.
            var circle = new GeoCircle2(new GeoPoint2(12, -7), 250);
            var ellipse = new GeoEllipse2(circle.Center, new GeoVector2(0.8, 0.6), 250, 250);
            double axis = Math.Atan2(0.6, 0.8);
            var random = new Random(61);

            for (int i = 0; i < 40; i++)
            {
                var point = new GeoPoint2(12 + random.NextDouble() * 1000 - 500, -7 + random.NextDouble() * 1000 - 500);

                Assert.True(ellipse.GetClosestPointOnBoundary(point).IsEqualTo(circle.GetClosestPointOnBoundary(point), new Tolerance(250 * 1E-12, 1E-12)));

                double expected = circle.GetParameterAtPoint(point) * 2.0 * Math.PI - axis;
                if (expected < 0.0)
                {
                    expected += 2.0 * Math.PI;
                }

                Assert.Equal(expected, ellipse.GetAngleAtPoint(point), 1E-12);
            }
        }
    }
}
