using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc a little short of a whole turn comes down into the plane as the arc it is, not as a whole circle.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoArc3.ProjectToArc2(GeoCoordinateSystem3)"/> took any sweep within the default angle tolerance, a whole
    /// degree, of a whole turn for a whole circle starting at angle nought. The arc of radius 10 about the origin in the plan
    /// from 1 radian sweeping 359.5 degrees came down as the whole circle from (10, 0): its start 19.2 from where it is, and
    /// half a degree of circle that is not there. A whole turn is one now only when the arc sweeps one, which is when its
    /// own <see cref="GeoArc3.TransformBy(GeoTransform3)"/> moves it as one.
    /// </remarks>
    public class ArcProjectionFaultLN1NearlyWholeTurnTests
    {
        private static readonly GeoCoordinateSystem3 Plan = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis, GeoVector3.YAxis);

        private static GeoPoint2 Down(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        [Theory]
        [InlineData(359.5, false)]
        [InlineData(359.5, true)]
        [InlineData(359.99, false)]
        [InlineData(359.0, true)]
        public void AnArcALittleShortOfAWholeTurn_ComesDownAsTheArcItIs(double degrees, bool clockwise)
        {
            double sweep = degrees * Math.PI / 180.0;
            var arc = new GeoArc3(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 10, 1.0, clockwise ? 1.0 - sweep : 1.0 + sweep, clockwise);

            GeoArc2 flat = arc.ProjectToArc2(Plan);

            Assert.Equal(0.0, flat.StartPoint.DistanceTo(Down(arc.StartPoint)), 9);
            Assert.Equal(0.0, flat.EndPoint.DistanceTo(Down(arc.EndPoint)), 9);
            Assert.Equal(0.0, flat.MidPoint.DistanceTo(Down(arc.MidPoint)), 9);
            Assert.Equal(arc.SweptAngle, flat.SweptAngle, 9);
            Assert.Equal(10.0, flat.Radius, 9);
        }

        [Fact]
        public void AWholeTurn_StillComesDownAsAWholeCircle()
        {
            var arc = new GeoArc3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 10, 1.0, 1.0);

            GeoArc2 flat = arc.ProjectToArc2(Plan);

            Assert.Equal(2.0 * Math.PI, flat.SweptAngle);
            Assert.Equal(0.0, flat.Center.DistanceTo(new GeoPoint2(3, 4)), 12);
            Assert.Equal(10.0, flat.Radius);
        }
    }
}
