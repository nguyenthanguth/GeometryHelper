using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A ring of the plane with tens of thousands of corners is cut into triangles in time that grows with its corners.
    /// </summary>
    /// <remarks>
    /// A polygon or a face of the plane is meshed as the face of space it lifts to, so the clipping that looks only at the
    /// reflex corners of a ring serves it too: a convex ring of 64 000 corners takes some 70 ms, where the clipping once took
    /// 15 s. The limits here are far above that, to stand a slow machine, and far below what the clipping took. The ring is
    /// cut with no sine or cosine, so that no two runtimes differ in a bit.
    /// </remarks>
    public class Triangulation2BigRingTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>A convex ring of 64 000 corners round a circle of 500, by the rational parametrization.</summary>
        private static GeoPolygon2 Ring()
        {
            const int perQuarter = 16000;
            var corners = new List<GeoPoint2>();

            for (int quarter = 0; quarter < 4; quarter++)
            {
                for (int k = 0; k < perQuarter; k++)
                {
                    double t = (double)k / perQuarter;
                    double x = 500.0 * (1.0 - t * t) / (1.0 + t * t), y = 500.0 * 2.0 * t / (1.0 + t * t);
                    corners.Add(quarter == 0 ? new GeoPoint2(x, y) : quarter == 1 ? new GeoPoint2(-y, x) : quarter == 2 ? new GeoPoint2(-x, -y) : new GeoPoint2(y, -x));
                }
            }

            return new GeoPolygon2(corners);
        }

        [Fact]
        public void AConvexPolygonOf64000Corners_IsCutIntoTrianglesInUnderTwoSeconds()
        {
            GeoPolygon2 ring = Ring();
            var clock = Stopwatch.StartNew();
            GeoTriangle2[] triangles = ring.TriangulateSurface(Within);
            TimeSpan time = clock.Elapsed;

            Assert.True(time < TimeSpan.FromSeconds(2), $"{time.TotalSeconds:F2} s");
            Assert.Equal(63998, triangles.Length);
            Assert.Equal(Math.Abs(ring.SignedArea), triangles.Sum(t => t.Area), 6);
        }

        [Fact]
        public void AConvexFaceOf64000Corners_IsCutIntoTrianglesInUnderTwoSeconds()
        {
            var face = new GeoFace2(Ring());
            var clock = Stopwatch.StartNew();
            GeoTriangle2[] triangles = face.TriangulateSurface(Within);
            TimeSpan time = clock.Elapsed;

            Assert.True(time < TimeSpan.FromSeconds(2), $"{time.TotalSeconds:F2} s");
            Assert.Equal(63998, triangles.Length);
            Assert.Equal(face.Area, triangles.Sum(t => t.Area), 6);
        }
    }
}
