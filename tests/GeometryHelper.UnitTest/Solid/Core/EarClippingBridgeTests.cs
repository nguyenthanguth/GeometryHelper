using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Holes in a row whose edges lie a hair out of line with each other are bridged to the corner a bridge meets
    /// first, rather than run along the edge of the next hole.
    /// </summary>
    /// <remarks>
    /// The pits of a slab drawn in Tekla Structures stand in rows, their edges along a row a few hundred-thousandths of
    /// a millimetre out of line. A hole is bridged from its rightmost corner along a ray, and of the corners of the loop
    /// in the way the one turning least off the ray was taken: of two corners of the next pit on a line 4.66E-8 above
    /// the ray, that was the far one, the same rise over a longer run being the shallower slope. The bridge ran along
    /// the near pit's edge, the loop touched itself there, and no ear could be clipped from it; the face fell back to the
    /// fan of its outline, laid across every pit. A slab's bottom face of 1 836 m2 was meshed as 5 391 m2, and the mass
    /// properties of such slabs put their centroids up to 14.5 mm off the middle of their thickness.
    /// </remarks>
    public class EarClippingBridgeTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        [Theory]
        [InlineData(4.66E-8)]
        [InlineData(1E-3)]
        public void HolesInARowAHairOutOfLineAreMeshedRoundEveryOne(double hair)
        {
            GeoPoint3[][] rings = Rings(hair, 0.0);
            var face = new GeoFace3(new GeoPolygon3(rings[0], Tolerance), rings.Skip(1).Select(ring => new GeoPolygon3(ring, Tolerance)), Tolerance);

            Assert.True(EarClipping.TryTriangulate(face, Tolerance, out GeoTriangle3[] triangles));
            Assert.InRange(triangles.Sum(t => t.Area), face.Area * (1 - 1E-12), face.Area * (1 + 1E-12));

            GeoTriangle3[] surface = face.TriangulateSurface(Tolerance);
            Assert.InRange(surface.Sum(t => t.Area), face.Area * (1 - 1E-12), face.Area * (1 + 1E-12));

            foreach (GeoTriangle3 triangle in surface)
            {
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    Assert.NotEqual(PointLocation.Inside, Containment3.Locate(hole, triangle.Centroid, Tolerance));
                }
            }
        }

        [Fact]
        public void ASlabWithPitsInARowIsMeshedAsItsSurface()
        {
            GeoSolid3 slab = Slab(4.66E-8, 300.0);

            Assert.True(slab.IsClosed(Tolerance));
            Assert.InRange(slab.Triangulate(Tolerance).Sum(t => t.Area), slab.GetSurfaceArea() * (1 - 1E-12), slab.GetSurfaceArea() * (1 + 1E-12));

            // The mass properties are summed over that mesh. The volume came out right all the same, being summed along x
            // and the face lying level, but the centroid stood 2.6 mm off the middle of the slab's thickness.
            MassProperties3 mass = slab.GetMassProperties(1.0, Tolerance);
            Assert.InRange(mass.Volume, slab.GetVolume() * (1 - 1E-9), slab.GetVolume() * (1 + 1E-9));
            Assert.Equal(slab.GetCentroid().Z, mass.Centroid.Z, 6);
        }

        /// <summary>
        /// An outline 40 m by 20 m and three pits 4.2 m square in a row, the two on the right a hair higher than the one on
        /// the left, laid out in the frame the face's own plane gives, so that the bridges run as described above. Each ring
        /// is wound counter-clockwise; the outline comes first.
        /// </summary>
        private static GeoPoint3[][] Rings(double hair, double z)
        {
            var frame = new GeoCoordinateSystem3(new GeoPlane3(new GeoPoint3(0, 0, z), GeoVector3.ZAxis));
            GeoPoint3 P(double u, double v) => frame.ToGlobal(new GeoPoint3(u, v, 0));

            // The upper corner of each pit's right edge a ten-millionth left of the lower, so that the lower is where its
            // bridge leaves from, as rounding left them.
            const double lean = 1E-7;

            GeoPoint3[] Pit(double left, double low) => new[] { P(left, low), P(left + 4200, low), P(left + 4200 - lean, 8400), P(left, 8400) };

            return new[]
            {
                new[] { P(0, 0), P(0, 10450), P(-40000, 10450), P(-40000, -10000), P(0, -10000) },
                Pit(-30950, 4200),
                Pit(-14350, 4200 + hair),
                Pit(-6050, 4200 + hair),
            };
        }

        /// <summary>
        /// The outline and pits above as a slab: the outline's faces standing outwards and the pits' walls facing into them.
        /// </summary>
        private static GeoSolid3 Slab(double hair, double thickness)
        {
            GeoPoint3[][] low = Rings(hair, 0.0);
            GeoPoint3[][] high = Rings(hair, thickness);

            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(low[0].Reverse(), Tolerance), low.Skip(1).Select(ring => new GeoPolygon3(ring.Reverse(), Tolerance)), Tolerance),
                new GeoFace3(new GeoPolygon3(high[0], Tolerance), high.Skip(1).Select(ring => new GeoPolygon3(ring, Tolerance)), Tolerance),
            };

            for (int r = 0; r < low.Length; r++)
            {
                for (int i = 0; i < low[r].Length; i++)
                {
                    int j = (i + 1) % low[r].Length;
                    GeoPoint3[] corners = r == 0
                        ? new[] { low[r][i], low[r][j], high[r][j], high[r][i] }
                        : new[] { low[r][j], low[r][i], high[r][i], high[r][j] };
                    faces.Add(new GeoFace3(new GeoPolygon3(corners, Tolerance), null, Tolerance));
                }
            }

            return new GeoSolid3(faces);
        }
    }
}
