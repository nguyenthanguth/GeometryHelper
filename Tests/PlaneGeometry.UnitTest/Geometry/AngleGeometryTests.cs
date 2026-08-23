using System;
using CommonGeometry;
using CommonGeometry.Datatype;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the seam between the Angle type and the geometry that consumes radians. The Angle type
    /// itself is tested in CommonGeometry.UnitTest; what is checked here is that the value it carries
    /// arrives unchanged at the geometry APIs that take a bare double of radians.
    /// </summary>
    public class AngleGeometryTests
    {
        [Fact]
        public void Angle_FeedsTheGeometryApisThatTakeRadians()
        {
            var quarterTurn = Angle.FromDegrees(90);

            var rotated = GeoVector2.XAxis.RotateBy(quarterTurn.Radians);
            Assert.True(rotated.IsEqualTo(GeoVector2.YAxis));

            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 4, 2, quarterTurn.Radians);
            Assert.True(rect.IsRotated);

            // A full turn is not a rotation, which is what GeoRectangle2.IsRotated relies on.
            var fullTurn = new GeoRectangle2(new GeoPoint2(0, 0), 4, 2, Angle.FullTurn.Radians);
            Assert.False(fullTurn.IsRotated);
        }

        [Fact]
        public void Angle_ExpressesTheTurnBetweenTwoVectors()
        {
            // The 270 degree case: GetAngleTo folds it to 90, the signed angle keeps the direction, and
            // normalizing the signed angle recovers the full turn.
            var from = GeoVector2.XAxis;
            var to = from.RotateBy(Angle.ToRadians(270));

            Assert.Equal(90.0, Angle.FromRadians(from.GetAngleTo(to)).Degrees, 9);
            Assert.Equal(-90.0, Angle.FromRadians(from.GetSignedAngleTo(to)).Degrees, 9);
            Assert.Equal(270.0, Angle.FromRadians(from.GetSignedAngleTo(to)).Normalize().Degrees, 9);
        }

    }
}
