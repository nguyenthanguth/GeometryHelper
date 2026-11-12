using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// A fillet radius of infinity is refused in space as in the plane: <c>Corner3</c> took it for a radius.
    /// </summary>
    public class FilletRadiusOfInfinityTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static GeoPolyline3 Chain() =>
            new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 20));

        [Fact]
        public void ARadiusOfInfinity_IsRefusedByEveryFilletInSpace()
        {
            var curved = new GeoPolylineArc3(Chain());

            Assert.Throws<ArgumentOutOfRangeException>(() => Corner3.Fillet(Chain(), double.PositiveInfinity, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() => Corner3.Fillet(curved, double.PositiveInfinity, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Corner3.TryFilletAt(Chain(), 1, double.PositiveInfinity, out _, Tol)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Corner3.TryFilletAt(curved, 1, double.PositiveInfinity, out _, Tol)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Corner3.Fillet(Chain(), new[] { 0.0, double.PositiveInfinity, 0.0 }, Tol)
            );
        }

        [Fact]
        public void AFiniteRadius_IsTakenAsBefore()
        {
            Assert.True(Corner3.TryFilletAt(Chain(), 1, 10.0, out GeoPolylineArc3 rounded, Tol));
            Assert.Equal(4, rounded.VertexCount);
        }
    }
}
