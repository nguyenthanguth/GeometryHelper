using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A radius that is not a positive number is refused as a radius: the fillets of a chain or a loop, flat or in space,
    /// said "A chamfer distance must be a positive number." of it.
    /// </summary>
    public class FilletRadiusMessageTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly GeoPolygon2 Square = new GeoPolygon2(
            new GeoPoint2(0, 0),
            new GeoPoint2(100, 0),
            new GeoPoint2(100, 100),
            new GeoPoint2(0, 100)
        );

        private static readonly GeoPolyline2 Bend = new GeoPolyline2(
            new GeoPoint2(0, 0),
            new GeoPoint2(100, 0),
            new GeoPoint2(100, 100)
        );

        private static readonly GeoPolygon3 SquareInSpace = new GeoPolygon3(
            new GeoPoint3(0, 0, 10),
            new GeoPoint3(100, 0, 10),
            new GeoPoint3(100, 100, 10),
            new GeoPoint3(0, 100, 10)
        );

        private static void SaysRadius(Action fillet)
        {
            ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(fillet);

            Assert.Equal("radius", refused.ParamName);
            Assert.Contains("radius", refused.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("chamfer", refused.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void AFilletAtOneCorner_RefusesABadRadiusAsARadius(double radius)
        {
            SaysRadius(() => Square.TryFilletAt(1, radius, out _, Tol));
            SaysRadius(() => Bend.TryFilletAt(1, radius, out _, Tol));
            SaysRadius(() => Corner2.TryFilletAt(new GeoPolygonArc2(Square), 1, radius, out _, Tol));
            SaysRadius(() => Corner2.TryFilletAt(new GeoPolylineArc2(Bend), 1, radius, out _, Tol));
            SaysRadius(() => SquareInSpace.TryFilletAt(1, radius, out _, Tol));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(double.NaN)]
        public void AFilletAtEveryCorner_RefusesABadRadiusAsARadius(double radius)
        {
            SaysRadius(() => Corner2.Fillet(new GeoPolygonArc2(Square), radius, Tol));
            SaysRadius(() => Corner2.Fillet(new GeoPolylineArc2(Bend), radius, Tol));
        }

        [Fact]
        public void AGoodRadius_StillRoundsTheCorner()
        {
            Assert.True(Square.TryFilletAt(1, 10, out GeoPolygonArc2 rounded, Tol));
            Assert.Equal(10000 - (100 - 25 * Math.PI), rounded.Area, 6);
        }
    }
}
