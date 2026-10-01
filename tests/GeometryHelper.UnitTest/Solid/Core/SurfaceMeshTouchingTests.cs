using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face whose holes touch each other or its boundary is meshed round every hole.
    /// </summary>
    /// <remarks>
    /// Ear clipping joins each hole into one loop with the boundary, and that loop is simple only while the rings stand
    /// apart. A hole whose edge lay along the boundary was joined by a bridge running along that edge; the loop doubled
    /// back on itself there, and the clipping, which only ever cuts a corner off, ended with a triangle wound backwards:
    /// an L whose hole met the side of its notch was meshed a third larger than it is, across the notch, and reported
    /// as done. Triangular holes meeting at their corners left a triangle across one of them, the sum of the areas still
    /// right. Such faces are cut into strips at their corners now.
    /// </remarks>
    public class SurfaceMeshTouchingTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        [Fact]
        public void AnLWhoseHolesMeetTheSideOfItsNotchIsMeshedRoundThem()
        {
            GeoCoordinateSystem3 frame = Frame();
            GeoPoint3 P(double u, double v) => frame.ToGlobal(new GeoPoint3(u, v, 0));
            GeoPolygon3 Square(double u, double v, double side) => new GeoPolygon3(new[] { P(u, v), P(u + side, v), P(u + side, v + side), P(u, v + side) }, Tolerance);

            // The notch is the corner beyond (500, 500); two holes stand against its side at u = 500, and one meets its
            // inner corner.
            var face = new GeoFace3(
                new GeoPolygon3(new[] { P(0, 0), P(1000, 0), P(1000, 500), P(500, 500), P(500, 1000), P(0, 1000) }, Tolerance),
                new[] { Square(450, 650, 50), Square(450, 850, 50), Square(450, 450, 50), Square(100, 100, 50), Square(700, 200, 50) },
                Tolerance);

            AssertMeshedRoundTheHoles(face, face.Area, 1E-6);
        }

        [Fact]
        public void TriangularHolesMeetingAtTheirCornersAreMeshedRoundThem()
        {
            // Laid out in the frame the face's own plane gives, as ear clipping saw it when it left a triangle across the
            // first hole: three holes meeting at (1500, -3000) and (3000, -3000), and a fourth standing apart.
            var frame = new GeoCoordinateSystem3(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis));
            GeoPoint3 P(double u, double v) => frame.ToGlobal(new GeoPoint3(u, v, 0));
            GeoPolygon3 Triangle(double u0, double v0, double u1, double v1, double u2, double v2) => new GeoPolygon3(new[] { P(u0, v0), P(u1, v1), P(u2, v2) }, Tolerance);

            var face = new GeoFace3(
                new GeoPolygon3(new[] { P(0, 0), P(0, -10000), P(5000, -10000), P(5000, -5000), P(3600, -2500), P(3600, 0) }, Tolerance),
                new[]
                {
                    Triangle(1500, -3000, 3000, -3000, 3000, -1500),
                    Triangle(1500, -3000, 1500, -4500, 3000, -4500),
                    Triangle(3000, -3000, 3000, -4500, 4500, -4500),
                    Triangle(1500, -6000, 1500, -7500, 3000, -7500),
                },
                Tolerance);

            AssertMeshedRoundTheHoles(face, face.Area, 1E-6);
        }

        [Fact]
        public void HolesSharingTheirEdgesAreMeshedRoundThem()
        {
            GeoCoordinateSystem3 frame = Frame();
            GeoPoint3 P(double u, double v) => frame.ToGlobal(new GeoPoint3(u, v, 0));

            // Three rows of three 100 mm squares, each sharing its sides with the next, their corners stirred by up to 5E-6
            // either way as a modeller leaves them: a hole of 300 by 300 in a plate of 500 by 500, give or take the slivers
            // the stirring opens between the squares.
            var stir = new Random(3);
            double Hair() => (stir.NextDouble() - 0.5) * 1E-5;
            var holes = new List<GeoPolygon3>();

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double u = 100 + 100 * i, v = 100 + 100 * j;
                    holes.Add(new GeoPolygon3(new[] { P(u + Hair(), v + Hair()), P(u + 100 + Hair(), v + Hair()), P(u + 100 + Hair(), v + 100 + Hair()), P(u + Hair(), v + 100 + Hair()) }, Tolerance));
                }
            }

            var face = new GeoFace3(new GeoPolygon3(new[] { P(0, 0), P(500, 0), P(500, 500), P(0, 500) }, Tolerance), holes, Tolerance);

            AssertMeshedRoundTheHoles(face, 500.0 * 500.0 - 300.0 * 300.0, 0.05);
        }

        [Fact]
        public void HolesSharingTheirEdgesExactlyAreMeshedRoundThem()
        {
            GeoCoordinateSystem3 frame = Frame();
            GeoPoint3 P(double u, double v) => frame.ToGlobal(new GeoPoint3(u, v, 0));
            var holes = new List<GeoPolygon3>();

            for (int i = 0; i < 3; i++)
            {
                double u = 100 + 100 * i;
                holes.Add(new GeoPolygon3(new[] { P(u, 100), P(u + 100, 100), P(u + 100, 200), P(u, 200) }, Tolerance));
            }

            var face = new GeoFace3(new GeoPolygon3(new[] { P(0, 0), P(500, 0), P(500, 300), P(0, 300) }, Tolerance), holes, Tolerance);

            AssertMeshedRoundTheHoles(face, 500.0 * 300.0 - 300.0 * 100.0, 1E-6);
        }

        /// <summary>
        /// A frame on a tilted plane far from the origin, so that nothing lines up with the world axes.
        /// </summary>
        private static GeoCoordinateSystem3 Frame()
            => new GeoCoordinateSystem3(new GeoPlane3(new GeoPoint3(41250.5, -18300.25, 7350.0), new GeoVector3(0.2, -0.3, 0.93).Normalize()));

        private static void AssertMeshedRoundTheHoles(GeoFace3 face, double material, double slack)
        {
            GeoTriangle3[] surface = face.TriangulateSurface(Tolerance);

            Assert.InRange(surface.Sum(t => t.Area), material - slack, material + slack);

            foreach (GeoTriangle3 triangle in surface)
            {
                Assert.True(triangle.GetAreaVector().DotProduct(face.Normal) > 0.0);
                Assert.NotEqual(PointLocation.OutSide, Containment3.Locate(face.Boundary, triangle.Centroid, Tolerance));

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    Assert.NotEqual(PointLocation.Inside, Containment3.Locate(hole, triangle.Centroid, Tolerance));
                }
            }
        }
    }
}
