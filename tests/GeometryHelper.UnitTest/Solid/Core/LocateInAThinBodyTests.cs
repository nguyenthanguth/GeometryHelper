using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A point of a body thinner than four point tolerances, near one of its sides: every ray from it crosses a face within
    /// twice the tolerance of a rim, where a crossing cannot be trusted, and the point was called outside for want of one.
    /// </summary>
    public class LocateInAThinBodyTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Slab(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        [Fact]
        public void APointOfAThinBodyNearItsSideIsInside()
        {
            // A cell of a plate 0.035 thick, as the cells cut it. The point stands 0.0108 below the top, 0.024 above the
            // bottom and 0.013 in from a side, further from each than the point tolerance: the rays up meet the top within
            // 0.02 of that side, and those that reach the side meet a face 0.035 tall, within 0.02 of a rim wherever they do.
            GeoSolid3 slab = Slab(0, 0, -0.0175, 100, 100, 0.0175);
            GeoPoint3 point = P(50, 100 - 0.013, 0.0175 - 0.0108);

            Assert.Equal(PointLocation.Inside, slab.Locate(point, Tolerance));
            Assert.True(slab.Contains(point, Tolerance));

            // Turned and moved far out, as the cell was found.
            GeoTransform3 placed = GeoTransform3.Translation(new GeoVector3(995161, 1827838, -43283)) * GeoTransform3.RotationZ(0.3);
            Assert.Equal(PointLocation.Inside, slab.TransformBy(placed).Locate(point.TransformBy(placed), Tolerance));

            // Beyond the side, and beyond the top, it is outside; within the tolerance of either, on the side.
            Assert.Equal(PointLocation.OutSide, slab.Locate(P(50, 100 + 0.013, 0.0067), Tolerance));
            Assert.Equal(PointLocation.OutSide, slab.Locate(P(50, 99.987, 0.0175 + 0.0108), Tolerance));
            Assert.Equal(PointLocation.OnSide, slab.Locate(P(50, 100 - 0.005, 0.0067), Tolerance));
            Assert.Equal(PointLocation.OnSide, slab.Locate(P(50, 99.987, 0.0175 - 0.005), Tolerance));
        }

        [Fact]
        public void APointInAThinBodysCavityNearItsSideIsOutside()
        {
            // The same where the material is round a cavity as thin: a point of the cavity 0.013 in from its side, every ray
            // refused, is in no material.
            GeoSolid3 outer = Slab(-10, -10, -10, 110, 110, 10);
            GeoSolid3 cavity = Slab(0, 0, -0.0175, 100, 100, 0.0175);
            var hollow = new GeoSolid3(System.Linq.Enumerable.Concat(outer.Faces, System.Linq.Enumerable.Select(cavity.Faces, f => new GeoFace3(f.Boundary.Flip()))));
            GeoPoint3 point = P(50, 100 - 0.013, 0.0175 - 0.0108);

            Assert.Equal(PointLocation.OutSide, hollow.Locate(point, Tolerance));
            Assert.Equal(PointLocation.Inside, hollow.Locate(P(50, 50, 5), Tolerance));
        }
    }
}
