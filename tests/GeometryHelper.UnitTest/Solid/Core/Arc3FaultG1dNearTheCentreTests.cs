using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The distance from an arc in space to a point is the exact one, also for a point near the centre of the arc. A point
    /// whose foot in the arc's plane fell within the point tolerance of the centre was carried to the arc's start first, so on
    /// an arc whose radius is about the tolerance a point 0.0003 from the arc read 0.00122 from it, and was off it.
    /// </summary>
    /// <remarks>
    /// Every arc here is a half circle of radius as large as the tolerance, about (1000, -2000, 500), in the plan or tilted 30
    /// degrees about X. The points are placed by the arc's own start and middle, and the expected distances follow from the
    /// circle alone: a point straight in from the arc, at a direction the arc covers, is as far from the arc as it is from the
    /// circle.
    /// </remarks>
    public class Arc3FaultG1dNearTheCentreTests
    {
        private static readonly GeoPoint3 Centre = new GeoPoint3(1000, -2000, 500);

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        private static GeoArc3 HalfCircle(double radius, double degrees)
        {
            double tilt = degrees * Math.PI / 180.0;

            return new GeoArc3(Centre, new GeoVector3(0, -Math.Sin(tilt), Math.Cos(tilt)), radius, 0.0, Math.PI);
        }

        /// <summary>The point of the arc's circle at an angle from its start, towards its middle, and a height over its plane.</summary>
        private static GeoPoint3 Around(GeoArc3 arc, double angle, double fromCentre, double height)
        {
            GeoVector3 toStart = arc.Center.GetVectorTo(arc.StartPoint).Normalize();
            GeoVector3 toMiddle = arc.Center.GetVectorTo(arc.MidPoint).Normalize();

            return arc.Center
                .Add(toStart.Multiply(fromCentre * Math.Cos(angle)))
                .Add(toMiddle.Multiply(fromCentre * Math.Sin(angle)))
                .Add(arc.Normal.Multiply(height));
        }

        [Theory]
        [InlineData(1E-3, 0.0)]
        [InlineData(1E-3, 30.0)]
        [InlineData(1E-2, 0.0)]
        [InlineData(1E-2, 30.0)]
        public void APointNearTheCentreOfAnArcAsLargeAsTheTolerance_IsTheExactDistanceFromIt(double equalPoint, double degrees)
        {
            // At a tolerance of 0.001, the half circle of radius 0.001 and the point 0.0007 from its centre under its middle:
            // 0.0003 from it, and on it. Carried to the start it stood 0.00122 away and was off. Raised by 0.0004 it is 0.0005
            // away.
            Tolerance t = At(equalPoint);
            double r = equalPoint;
            GeoArc3 arc = HalfCircle(r, degrees);
            GeoPoint3 under = Around(arc, Math.PI / 2, 0.7 * r, 0.0);
            GeoPoint3 above = Around(arc, Math.PI / 2, 0.7 * r, 0.4 * r);
            double slack = 1E-11;

            Assert.InRange(arc.DistanceTo(under, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.InRange(arc.DistanceTo(above, t), 0.5 * r - slack, 0.5 * r + slack);
            Assert.InRange(under.DistanceTo(arc, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.True(arc.IsPointOn(under, t));
            Assert.True(arc.IsPointOn(above, t));
        }

        [Fact]
        public void APointMovedOffAnArcAsLargeAsTheTolerance_IsNoFurtherFromItThanItMoved()
        {
            // Each point of the arc, every ten degrees, moved 0.00029 straight towards the centre: 0.00029 from the arc. Carried
            // to the start, the one moved from 120 degrees read 0.00149, five times as far as it had moved.
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(1E-3, 30.0);

            for (int degrees = 0; degrees <= 180; degrees += 10)
            {
                double angle = degrees * Math.PI / 180.0;
                GeoPoint3 onArc = Around(arc, angle, 1E-3, 0.0);
                GeoPoint3 moved = Around(arc, angle, 0.71E-3, 0.0);

                Assert.InRange(arc.DistanceTo(moved, t), 0.29E-3 - 1E-11, 0.29E-3 + 1E-11);
                Assert.True(arc.DistanceTo(moved, t) <= onArc.DistanceTo(moved) + 1E-11, $"moved from {degrees} degrees");
            }
        }

        [Fact]
        public void APointExactlyAtTheCentreOfAnArcAsLargeAsTheTolerance_IsItsRadiusFromIt()
        {
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(1E-3, 30.0);

            Assert.InRange(arc.DistanceTo(Centre, t), 1E-3 - 1E-11, 1E-3 + 1E-11);
        }

        /// <summary>The distance from an arc to a point, read off 20 000 points along it.</summary>
        private static double Sampled(GeoArc3 arc, GeoPoint3 point)
        {
            double best = double.MaxValue;

            for (int i = 0; i <= 20000; i++)
            {
                best = Math.Min(best, arc.GetPointAtParameter(i / 20000.0).DistanceTo(point));
            }

            return best;
        }

        [Theory]
        [InlineData(49.9992, 0.0)]
        [InlineData(49.99, 0.0091)]
        public void TheShortestLineFromABallCentredNearTheCentreOfAnArc_IsAsLongAsTheDistanceAndLandsAtTheContact(double radius, double expected)
        {
            // The lower half of the circle of radius 50 about the origin and a ball about (0.0009, 0, 0), at a tolerance of a
            // thousandth. The arc's end (50, 0, 0) is 49.9991 from the centre of the ball, so the ball of radius 49.9992 meets
            // it and the one of radius 49.99 is 0.0091 short. The line was drawn to the arc's start (-50, 0, 0) instead: a
            // point outside the first ball, and a line 0.0109 long from the second.
            Tolerance t = At(1E-3);
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(-50, 0, 0), new GeoPoint3(0, -50, 0), new GeoPoint3(50, 0, 0), t);
            var ball = new GeoSphere3(new GeoPoint3(0.0009, 0, 0), radius);

            double distance = ball.DistanceTo(arc, t);
            GeoLine3 line = ball.GetShortestLineTo(arc, t);
            GeoLine3 back = arc.GetShortestLineTo(ball, t);
            double slack = 1E-12 * radius;

            Assert.InRange(distance, expected - 1E-11, expected + 1E-11);
            Assert.InRange(line.Length, distance - slack, distance + slack);
            Assert.InRange(line.StartPoint.DistanceTo(ball.Center), 0.0, radius + slack);
            Assert.InRange(line.EndPoint.DistanceTo(new GeoPoint3(50, 0, 0)), 0.0, 1E-9);
            Assert.InRange(back.Length, distance - slack, distance + slack);
            Assert.True(back.StartPoint.IsEqualTo(line.EndPoint, Tolerance.Default));

            if (expected > 0.0)
            {
                Assert.InRange(line.StartPoint.DistanceTo(ball.Center), radius - slack, radius + slack);
            }
            else
            {
                Assert.True(ball.CollidesWith(arc, t));
            }
        }

        [Fact]
        public void ABallNearTheCentreOfAnArcAsLargeAsTheTolerance_IsTheExactDistanceFromItAndTouchesIt()
        {
            // At a tolerance of 0.001, the half circle of radius 0.001 tilted 30 degrees and a ball of radius 0.0001 about the
            // point 0.0007 from its centre under its middle: 0.0002 from it, and touching it. Carried to the start, the centre
            // of the ball stood 0.00122 from the arc, and the ball 0.00112, too far to touch.
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(1E-3, 30.0);
            var ball = new GeoSphere3(Around(arc, Math.PI / 2, 0.7E-3, 0.0), 1E-4);

            Assert.InRange(ball.DistanceTo(arc, t), 2E-4 - 1E-11, 2E-4 + 1E-11);
            Assert.InRange(Sampled(arc, ball.Center) - ball.Radius, 2E-4 - 1E-9, 2E-4 + 1E-9);
            Assert.InRange(arc.DistanceTo(ball, t), 2E-4 - 1E-11, 2E-4 + 1E-11);
            Assert.True(ball.CollidesWith(arc, t));
            Assert.True(arc.CollidesWith(ball, t));
        }

        [Fact]
        public void ASegmentAlongTheDiameterOfAnArcSmallerThanTheTolerance_CrossesItAtBothEnds()
        {
            // At a tolerance of 0.001, the half circle of radius 0.0008 tilted 30 degrees and the segment 0.008 long along its
            // diameter, through its end and then its start. The segment's line meets the circle at those two points. Each was
            // read as at the centre and carried to the start, so the end, 0.0016 from the start, was dropped.
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(0.8E-3, 30.0);
            GeoVector3 toStart = arc.Center.GetVectorTo(arc.StartPoint).Normalize();
            var segment = new GeoLine3(arc.Center.Add(toStart.Multiply(-4E-3)), arc.Center.Add(toStart.Multiply(4E-3)));

            GeoPoint3[] crossings = arc.GetIntersections(segment, t);

            Assert.Equal(2, crossings.Length);
            Assert.InRange(crossings[0].DistanceTo(arc.EndPoint), 0.0, 1E-12);
            Assert.InRange(crossings[1].DistanceTo(arc.StartPoint), 0.0, 1E-12);
            Assert.InRange(Sampled(arc, crossings[0]), 0.0, 1E-12);
            Assert.InRange(Sampled(arc, crossings[1]), 0.0, 1E-12);
        }
    }
}
