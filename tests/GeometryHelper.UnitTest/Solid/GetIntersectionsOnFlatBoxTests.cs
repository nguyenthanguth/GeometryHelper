using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The crossings of a triangle, a polygon, a face or a plane with a box flat on some axis, or with an empty box, are
    /// none, as the <c>Try</c> methods answer false for them; they threw "A box with a zero size on some axis has no faces
    /// to build", or "An empty bounding box has no shape to convert". A box with its faces is crossed as before.
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
        public void ARegionOrAPlaneAgainstABoxFlatOnSomeAxis_CrossesNothing_NotAThrow()
        {
            var face = new GeoFace3(Polygon);

            foreach (GeoObb3 box in new[] { Sheet(), Stick(), Film() })
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
        public void ARegionOrAPlaneAgainstAnAxisAlignedBoxFlatOrEmpty_CrossesNothing_NotAThrow()
        {
            var face = new GeoFace3(Polygon);
            var flat = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0));
            GeoAabb3 empty = GeoAabb3.FromPoints(new GeoPoint3[0]);

            foreach (GeoAabb3 box in new[] { flat, empty })
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
