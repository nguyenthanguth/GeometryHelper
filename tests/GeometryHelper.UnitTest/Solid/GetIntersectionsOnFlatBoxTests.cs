using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The crossings of a triangle, a polygon, a face or a plane with a box flat on some axis are those of its rectangle or
    /// its segment, and with an empty box none; they threw "A box with a zero size on some axis has no faces to build", or
    /// "An empty bounding box has no shape to convert". A box with its faces is crossed as before.
    /// </summary>
    public class GetIntersectionsOnFlatBoxTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>A box 100 by 100 by 0 about (50, 50, 0): the rectangle (0, 0, 0) to (100, 100, 0).</summary>
        private static GeoObb3 Sheet() => new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, 0);

        /// <summary>A box 100 by 0 by 0 about (50, 200, 0): the segment (0, 200, 0) to (100, 200, 0).</summary>
        private static GeoObb3 Stick() => new GeoObb3(new GeoPoint3(50, 200, 0), 100, 0, 0);

        /// <summary>A box 100 by 100 by 0.0000000000001 about (50, 50, 0): thinner than the rounding of its numbers.</summary>
        private static GeoObb3 Film() => new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, 1E-13);

        private static GeoObb3 Cube() => new GeoObb3(new GeoPoint3(50, 50, 50), 100, 100, 100);

        private static readonly GeoTriangle3 Triangle = new GeoTriangle3(
            new GeoPoint3(20, -50, -50),
            new GeoPoint3(20, 300, -50),
            new GeoPoint3(20, -50, 300)
        );

        private static readonly GeoPolygon3 Polygon = new GeoPolygon3(
            new GeoPoint3(20, -10, -10),
            new GeoPoint3(20, 110, -10),
            new GeoPoint3(20, 110, 10),
            new GeoPoint3(20, -10, 10)
        );

        private static readonly GeoPlane3 Plane = new GeoPlane3(new GeoPoint3(20, 0, 0), new GeoVector3(1, 0, 0));

        [Fact]
        public void ARegionOrAPlaneAgainstABoxFlatOnSomeAxis_CrossesItsRectangleOrItsSegment_NotAThrow()
        {
            var face = new GeoFace3(Polygon);

            // The rectangle (0, 0, 0) to (100, 100, 0) is crossed in x = 20 on its edges y = 100 and y = 0.
            foreach (GeoObb3 box in new[] { Sheet(), Film() })
            {
                Assert.Equal(2, Triangle.GetIntersections(box, Tol).Length);
                Assert.Equal(2, Polygon.GetIntersections(box, Tol).Length);
                Assert.Equal(2, face.GetIntersections(box, Tol).Length);
                Assert.Equal(2, Plane.GetIntersections(box, Tol).Length);
                Assert.Equal(2, box.GetIntersections(Triangle, Tol).Length);
                Assert.Equal(2, box.GetIntersections(Polygon, Tol).Length);
                Assert.Equal(2, box.GetIntersections(face, Tol).Length);
                Assert.Equal(2, box.GetIntersections(Plane, Tol).Length);
                Assert.True(Plane.GetIntersections(box, Tol)[0].IsEqualTo(new GeoPoint3(20, 100, 0), Tol));
                Assert.True(Plane.GetIntersections(box, Tol)[1].IsEqualTo(new GeoPoint3(20, 0, 0), Tol));
            }

            // The segment (0, 200, 0) to (100, 200, 0) passes through the triangle and the plane at (20, 200, 0), and
            // misses the square, which stops at y = 110.
            Assert.True(Triangle.GetIntersections(Stick(), Tol)[0].IsEqualTo(new GeoPoint3(20, 200, 0), Tol));
            Assert.True(Plane.GetIntersections(Stick(), Tol)[0].IsEqualTo(new GeoPoint3(20, 200, 0), Tol));
            Assert.Empty(Polygon.GetIntersections(Stick(), Tol));
            Assert.Empty(face.GetIntersections(Stick(), Tol));
        }

        [Fact]
        public void ARegionOrAPlaneAgainstAnAxisAlignedBoxFlatOrEmpty_CrossesItsRectangleOrNothing_NotAThrow()
        {
            var face = new GeoFace3(Polygon);
            var flat = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0));
            GeoAabb3 empty = GeoAabb3.FromPoints(new GeoPoint3[0]);

            Assert.Equal(2, Triangle.GetIntersections(flat, Tol).Length);
            Assert.Equal(2, Polygon.GetIntersections(flat, Tol).Length);
            Assert.Equal(2, face.GetIntersections(flat, Tol).Length);
            Assert.Equal(2, Plane.GetIntersections(flat, Tol).Length);
            Assert.Equal(2, flat.GetIntersections(Triangle, Tol).Length);
            Assert.Equal(2, flat.GetIntersections(Plane, Tol).Length);

            foreach (GeoAabb3 box in new[] { empty })
            {
                Assert.Empty(Triangle.GetIntersections(box, Tol));
                Assert.Empty(Polygon.GetIntersections(box, Tol));
                Assert.Empty(face.GetIntersections(box, Tol));
                Assert.Empty(Plane.GetIntersections(box, Tol));
                Assert.Empty(box.GetIntersections(Triangle, Tol));
                Assert.Empty(box.GetIntersections(Polygon, Tol));
                Assert.Empty(box.GetIntersections(face, Tol));
                Assert.Empty(box.GetIntersections(Plane, Tol));
            }
        }

        [Fact]
        public void ABoxWithItsFaces_IsCrossedAsItsBody()
        {
            // The plane x = 20 cuts the four edges of the cube along x; the triangle and the square in x = 20 cross its
            // faces where they pass out of it.
            Assert.Equal(4, Plane.GetIntersections(Cube(), Tol).Length);
            Assert.Equal(Plane.GetIntersections(Cube().ToSolid(), Tol), Plane.GetIntersections(Cube(), Tol));
            Assert.Equal(Triangle.GetIntersections(Cube().ToSolid(), Tol), Triangle.GetIntersections(Cube(), Tol));
            Assert.Equal(Polygon.GetIntersections(Cube().ToSolid(), Tol), Polygon.GetIntersections(Cube(), Tol));
            Assert.NotEmpty(Polygon.GetIntersections(Cube(), Tol));
        }

        [Fact]
        public void ANullBox_IsStillRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Plane.GetIntersections((GeoObb3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => Polygon.GetIntersections((GeoObb3)null, Tol));
        }
    }
}
