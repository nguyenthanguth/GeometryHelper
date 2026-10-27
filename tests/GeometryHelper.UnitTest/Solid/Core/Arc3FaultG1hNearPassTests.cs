using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An ellipse whose rim passes within the tolerance of a small arc touches it where the two come nearest. The place given
    /// is the middle of the shortest line between them, and the point of the arc at its end was found at the global point
    /// tolerance, whatever tolerance was asked: a rim point whose foot fell within a thousandth of the centre of the arc was
    /// carried to the arc's start, so on an arc of radius 0.0005 the touch was given 0.00004 off the arc at a tolerance of a
    /// millionth.
    /// </summary>
    /// <remarks>
    /// The arc is the quarter of the circle of radius 0.0005 about the origin in the plan from (0.00035, -0.00035, 0) through
    /// (0.0005, 0, 0) to (0.00035, 0.00035, 0). The ellipse is 2 by 1, its major axis along Y, its plane leaning out from the
    /// arc by the tilt, and its rim passes 4E-7 under the arc's middle, at (0.0005, 0, -4E-7), crossing the plane of the arc
    /// nowhere near it, so that only the search along the rim finds the touch.
    /// </remarks>
    public class Arc3FaultG1hNearPassTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        private static GeoArc3 Quarter() => new GeoArc3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 0.0005, -0.75 * Math.PI, -0.25 * Math.PI);

        private static GeoEllipse3 Passing(double tilt)
        {
            var up = new GeoVector3(Math.Sin(tilt), 0, Math.Cos(tilt));
            var foot = new GeoPoint3(0.0005, 0, -4E-7);

            return new GeoEllipse3(foot.Add(up), new GeoVector3(0, 1, 0).CrossProduct(up), new GeoVector3(0, 1, 0), 2, 1);
        }

        [Theory]
        [InlineData(1E-6, 0.5)]
        [InlineData(1E-6, 1.0)]
        [InlineData(1E-5, 0.2)]
        public void AnEllipsePassingUnderASmallArc_TouchesItWhereTheyMeet(double equalPoint, double tilt)
        {
            // The touch was given at (0.00043, -0.00018, -2E-7), 0.000038 off the arc and 0.000073 off the rim: halfway to the
            // arc's start.
            Tolerance t = At(equalPoint);
            GeoArc3 arc = Quarter();
            GeoEllipse3 ellipse = Passing(tilt);

            GeoPoint3[] touches = ellipse.GetIntersections(arc, t);

            GeoPoint3 touch = Assert.Single(touches);
            Assert.InRange(arc.DistanceTo(touch, t), 0.0, equalPoint);
            Assert.InRange(ellipse.DistanceTo(touch), 0.0, equalPoint);
            Assert.InRange(touch.DistanceTo(new GeoPoint3(0.0005, 0, -2E-7)), 0.0, 1E-6);
            Assert.True(ellipse.CollidesWith(arc, t));
        }

        [Theory]
        [InlineData(1E-6, 0.5)]
        [InlineData(1E-5, 1.0)]
        public void AnEllipsePassingUnderASmallCircle_TouchesItWhereTheyMeet(double equalPoint, double tilt)
        {
            // The circle starts at (0, 0.0005, 0), and the touch was given halfway to there, 0.00025 off the rim.
            Tolerance t = At(equalPoint);
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 0.0005);
            GeoEllipse3 ellipse = Passing(tilt);

            GeoPoint3[] touches = ellipse.GetIntersections(circle, t);

            GeoPoint3 touch = Assert.Single(touches);
            Assert.InRange(circle.DistanceTo(touch), 0.0, equalPoint);
            Assert.InRange(ellipse.DistanceTo(touch), 0.0, equalPoint);
            Assert.InRange(touch.DistanceTo(new GeoPoint3(0.0005, 0, -2E-7)), 0.0, 1E-6);
        }
    }
}
