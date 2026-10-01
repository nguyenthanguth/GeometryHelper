using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face whose holes stand a rib of a millimetre or less apart is meshed round every hole.
    /// </summary>
    /// <remarks>
    /// An ear was refused when a corner of the loop lay near one of its edges, near meaning within the point tolerance
    /// times the width of the whole face, read as an area: on a plate a metre across, a tenth of a millimetre from an edge
    /// 100 mm long. Along a rib between two holes every corner across the rib is that close, every ear along it was
    /// refused, and the plate fell back to the fan of its outline, laid across every hole.
    /// </remarks>
    public class EarClippingRibTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        [Theory]
        [InlineData(1.0, 0.0)]
        [InlineData(0.2, 0.0)]
        [InlineData(0.05, 0.03)]
        public void APlateWithHolesARibApartIsMeshedRoundEveryHole(double rib, double dropped)
        {
            // Where two ribs 0.05 wide cross, the square between four holes is two triangles too small to count as more
            // than nothing, and the twelve such squares are left out.
            GeoFace3 face = Plate(rib);

            Assert.True(EarClipping.TryTriangulate(face, Tolerance, out GeoTriangle3[] triangles));
            Assert.InRange(triangles.Sum(t => t.Area), face.Area - dropped - 1E-6, face.Area + 1E-6);

            GeoTriangle3[] surface = face.TriangulateSurface(Tolerance);
            Assert.InRange(surface.Sum(t => t.Area), face.Area - dropped - 1E-6, face.Area + 1E-6);

            foreach (GeoTriangle3 triangle in surface)
            {
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    Assert.NotEqual(PointLocation.Inside, Containment3.Locate(hole, triangle.Centroid, Tolerance));
                }
            }
        }

        /// <summary>
        /// A plate 1000 by 600 with three rows of seven holes 100 mm square, a rib apart, laid out in the frame of the
        /// face's own plane.
        /// </summary>
        private static GeoFace3 Plate(double rib)
        {
            var frame = new GeoCoordinateSystem3(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis));
            GeoPoint3 P(double u, double v) => frame.ToGlobal(new GeoPoint3(u, v, 0));

            var holes = new List<GeoPolygon3>();

            for (int column = 0; column < 7; column++)
            {
                for (int row = 0; row < 3; row++)
                {
                    double u = 100 + column * (100 + rib), v = 100 + row * (100 + rib);
                    holes.Add(new GeoPolygon3(new[] { P(u, v), P(u + 100, v), P(u + 100, v + 100), P(u, v + 100) }, Tolerance));
                }
            }

            return new GeoFace3(new GeoPolygon3(new[] { P(0, 0), P(1000, 0), P(1000, 600), P(0, 600) }, Tolerance), holes, Tolerance);
        }
    }
}
