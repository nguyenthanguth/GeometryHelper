using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A box flat on one axis is read as its rectangle, on two as its segment, on all three as its point, when a triangle,
    /// a polygon, a face, a plane, a solid or another box is measured against it or asked whether it touches it; they threw
    /// "A box with a zero size on some axis has no faces to build". A box with its faces is read as its body, as before, and
    /// an empty axis-aligned box is still refused.
    /// </summary>
    public class CollidesWithAndDistanceToOnFlatBoxTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>A box 100 by 100 by 0 about (50, 50, 0): the rectangle (0, 0, 0) to (100, 100, 0).</summary>
        private static GeoObb3 Sheet() => new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, 0);

        /// <summary>The same rectangle as an axis-aligned box.</summary>
        private static GeoAabb3 FlatAabb() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0));

        /// <summary>A box 100 by 100 by 0.0000000000001 about (50, 50, 0): thinner than the rounding of its numbers.</summary>
        private static GeoObb3 Film() => new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, 1E-13);

        /// <summary>A box 100 by 0 by 0 about (50, 200, 0): the segment (0, 200, 0) to (100, 200, 0).</summary>
        private static GeoObb3 Stick() => new GeoObb3(new GeoPoint3(50, 200, 0), 100, 0, 0);

        /// <summary>A box 0 by 0 by 0 at (50, 50, 0): the point.</summary>
        private static GeoObb3 Dot() => new GeoObb3(new GeoPoint3(50, 50, 0), 0, 0, 0);

        private static GeoObb3 Cube() => new GeoObb3(new GeoPoint3(50, 50, 50), 100, 100, 100);

        /// <summary>A triangle lying 20 over the rectangle, its corners (10, 10), (90, 10) and (10, 90).</summary>
        private static readonly GeoTriangle3 TriangleAbove = new GeoTriangle3(
            new GeoPoint3(10, 10, 20),
            new GeoPoint3(90, 10, 20),
            new GeoPoint3(10, 90, 20)
        );

        /// <summary>A triangle standing upright in x = 30, across the rectangle from below it to above it.</summary>
        private static readonly GeoTriangle3 TriangleAcross = new GeoTriangle3(
            new GeoPoint3(30, 20, -40),
            new GeoPoint3(30, 80, -40),
            new GeoPoint3(30, 50, 40)
        );

        /// <summary>The square 80 by 80 lying 20 over the rectangle.</summary>
        private static readonly GeoPolygon3 SquareAbove = Square(10, 10, 80, 20);

        /// <summary>A square standing upright in x = 30, across the rectangle from below it to above it.</summary>
        private static readonly GeoPolygon3 SquareAcross = new GeoPolygon3(
            new GeoPoint3(30, 20, -40),
            new GeoPoint3(30, 80, -40),
            new GeoPoint3(30, 80, 40),
            new GeoPoint3(30, 20, 40)
        );

        private static GeoPolygon3 Square(double x0, double y0, double size, double z) =>
            new GeoPolygon3(
                new GeoPoint3(x0, y0, z),
                new GeoPoint3(x0 + size, y0, z),
                new GeoPoint3(x0 + size, y0 + size, z),
                new GeoPoint3(x0, y0 + size, z)
            );

        [Fact]
        public void ATriangleAgainstABoxFlatOnOneAxis_IsMeasuredToItsRectangle()
        {
            foreach (GeoObb3 box in new[] { Sheet(), Film() })
            {
                Assert.Equal(20.0, TriangleAbove.DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, box.DistanceTo(TriangleAbove, Tol), 9);
                Assert.False(TriangleAbove.CollidesWith(box, Tol));
                Assert.False(box.CollidesWith(TriangleAbove, Tol));
                Assert.Equal(0.0, TriangleAcross.DistanceTo(box, Tol));
                Assert.True(TriangleAcross.CollidesWith(box, Tol));
                Assert.True(box.CollidesWith(TriangleAcross, Tol));
            }

            Assert.Equal(20.0, TriangleAbove.DistanceTo(FlatAabb(), Tol), 9);
            Assert.False(TriangleAbove.CollidesWith(FlatAabb(), Tol));
            Assert.True(TriangleAcross.CollidesWith(FlatAabb(), Tol));
            Assert.True(FlatAabb().CollidesWith(TriangleAcross, Tol));
        }

        [Fact]
        public void APolygonOrAFaceAgainstABoxFlatOnOneAxis_IsMeasuredToItsRectangle()
        {
            var faceAbove = new GeoFace3(SquareAbove);
            var faceAcross = new GeoFace3(SquareAcross);

            // A square 300 by 300 in the floor, with a hole 220 by 220 the rectangle sits in, 60 from its rim all round.
            var ring = new GeoFace3(Square(-100, -100, 300, 0), new[] { Square(-60, -60, 220, 0) });

            foreach (GeoObb3 box in new[] { Sheet(), Film() })
            {
                Assert.Equal(20.0, SquareAbove.DistanceTo(box, Tol), 9);
                Assert.False(SquareAbove.CollidesWith(box, Tol));
                Assert.Equal(0.0, SquareAcross.DistanceTo(box, Tol));
                Assert.True(SquareAcross.CollidesWith(box, Tol));

                Assert.Equal(20.0, faceAbove.DistanceTo(box, Tol), 9);
                Assert.False(faceAbove.CollidesWith(box, Tol));
                Assert.True(faceAcross.CollidesWith(box, Tol));
                Assert.Equal(60.0, ring.DistanceTo(box, Tol), 9);
                Assert.False(ring.CollidesWith(box, Tol));
            }

            Assert.Equal(20.0, FlatAabb().DistanceTo(SquareAbove, Tol), 9);
            Assert.True(FlatAabb().CollidesWith(SquareAcross, Tol));
            Assert.Equal(60.0, FlatAabb().DistanceTo(ring, Tol), 9);
            Assert.True(FlatAabb().CollidesWith(faceAcross, Tol));
        }

        [Fact]
        public void APlaneAgainstABoxFlatOnOneAxis_IsMeasuredToItsRectangle()
        {
            var above = new GeoPlane3(new GeoPoint3(0, 0, 20), new GeoVector3(0, 0, 1));
            var upright = new GeoPlane3(new GeoPoint3(30, 0, 0), new GeoVector3(1, 0, 0));
            var beside = new GeoPlane3(new GeoPoint3(130, 0, 0), new GeoVector3(1, 0, 0));

            foreach (GeoObb3 box in new[] { Sheet(), Film() })
            {
                Assert.Equal(20.0, above.DistanceTo(box, Tol), 9);
                Assert.False(above.CollidesWith(box, Tol));
                Assert.Equal(0.0, upright.DistanceTo(box, Tol));
                Assert.True(upright.CollidesWith(box, Tol));
                Assert.Equal(30.0, box.DistanceTo(beside, Tol), 9);
                Assert.False(box.CollidesWith(beside, Tol));
            }

            Assert.Equal(20.0, above.DistanceTo(FlatAabb(), Tol), 9);
            Assert.True(upright.CollidesWith(FlatAabb(), Tol));
        }

        [Fact]
        public void ASolidAgainstABoxFlatOnOneAxis_IsMeasuredToItsRectangle()
        {
            GeoSolid3 above = new GeoObb3(new GeoPoint3(50, 50, 40), 40, 40, 40).ToSolid();
            GeoSolid3 across = new GeoObb3(new GeoPoint3(30, 30, 0), 20, 20, 20).ToSolid();
            GeoSolid3 around = new GeoObb3(new GeoPoint3(50, 50, 0), 300, 300, 300).ToSolid();

            foreach (GeoObb3 box in new[] { Sheet(), Film() })
            {
                Assert.Equal(20.0, above.DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, box.DistanceTo(above, Tol), 9);
                Assert.False(above.CollidesWith(box, Tol));
                Assert.False(box.CollidesWith(above, Tol));
                Assert.True(across.CollidesWith(box, Tol));
                Assert.True(box.CollidesWith(across, Tol));
                Assert.Equal(0.0, around.DistanceTo(box, Tol));
                Assert.True(box.CollidesWith(around, Tol));
            }

            Assert.Equal(20.0, above.DistanceTo(FlatAabb(), Tol), 9);
            Assert.True(FlatAabb().CollidesWith(across, Tol));
            Assert.False(FlatAabb().CollidesWith(above, Tol));
        }

        [Fact]
        public void TwoBoxesOneOrBothFlat_AreMeasuredAsTheirShapes()
        {
            var cubeAbove = new GeoObb3(new GeoPoint3(50, 50, 40), 40, 40, 40);
            var sheetAbove = new GeoObb3(new GeoPoint3(50, 50, 10), 100, 100, 0);

            Assert.Equal(20.0, Sheet().DistanceTo(cubeAbove, Tol), 9);
            Assert.Equal(20.0, cubeAbove.DistanceTo(Sheet(), Tol), 9);
            Assert.Equal(10.0, Sheet().DistanceTo(sheetAbove, Tol), 9);
            Assert.Equal(10.0, sheetAbove.DistanceTo(FlatAabb(), Tol), 9);
            Assert.Equal(20.0, FlatAabb().DistanceTo(cubeAbove, Tol), 9);
            Assert.Equal(0.0, Sheet().DistanceTo(Cube(), Tol));
        }

        [Fact]
        public void ABoxFlatOnTwoAxes_IsMeasuredToItsSegment()
        {
            // The segment (0, 200, 0) to (100, 200, 0), under a triangle and a square 20 over it, and across a plane.
            var triangle = new GeoTriangle3(
                new GeoPoint3(10, 150, 20),
                new GeoPoint3(90, 150, 20),
                new GeoPoint3(10, 250, 20)
            );
            GeoPolygon3 square = Square(10, 150, 80, 20);
            var upright = new GeoPlane3(new GeoPoint3(30, 0, 0), new GeoVector3(1, 0, 0));
            GeoSolid3 above = new GeoObb3(new GeoPoint3(50, 200, 40), 40, 40, 40).ToSolid();

            Assert.Equal(20.0, triangle.DistanceTo(Stick(), Tol), 9);
            Assert.False(triangle.CollidesWith(Stick(), Tol));
            Assert.Equal(20.0, square.DistanceTo(Stick(), Tol), 9);
            Assert.Equal(20.0, new GeoFace3(square).DistanceTo(Stick(), Tol), 9);
            Assert.True(upright.CollidesWith(Stick(), Tol));
            Assert.Equal(0.0, upright.DistanceTo(Stick(), Tol));
            Assert.Equal(20.0, above.DistanceTo(Stick(), Tol), 9);
            Assert.False(Stick().CollidesWith(above, Tol));
            Assert.Equal(100.0, Stick().DistanceTo(Sheet(), Tol), 9);

            var stickAabb = new GeoAabb3(new GeoPoint3(0, 200, 0), new GeoPoint3(100, 200, 0));
            Assert.Equal(20.0, triangle.DistanceTo(stickAabb, Tol), 9);
            Assert.True(upright.CollidesWith(stickAabb, Tol));
        }

        [Fact]
        public void ABoxFlatOnAllThreeAxes_IsMeasuredToItsPoint()
        {
            // The point (50, 50, 0), on the long side of the triangle's shadow, 20 under it.
            Assert.Equal(20.0, TriangleAbove.DistanceTo(Dot(), Tol), 9);
            Assert.False(TriangleAbove.CollidesWith(Dot(), Tol));
            Assert.Equal(20.0, SquareAbove.DistanceTo(Dot(), Tol), 9);
            Assert.True(SquareAcross.CollidesWith(new GeoObb3(new GeoPoint3(30, 50, 0), 0, 0, 0), Tol));
            Assert.Equal(
                20.0,
                new GeoPlane3(new GeoPoint3(0, 0, 20), new GeoVector3(0, 0, 1)).DistanceTo(Dot(), Tol),
                9
            );
            Assert.True(Cube().ToSolid().CollidesWith(Dot(), Tol));
            Assert.Equal(0.0, Dot().DistanceTo(Cube(), Tol));
            Assert.Equal(0.0, Dot().DistanceTo(Sheet(), Tol));

            var dotAabb = new GeoAabb3(new GeoPoint3(50, 50, 0), new GeoPoint3(50, 50, 0));
            Assert.Equal(20.0, TriangleAbove.DistanceTo(dotAabb, Tol), 9);
        }

        [Fact]
        public void ABoxWithItsFaces_IsReadAsItsBody()
        {
            GeoSolid3 body = Cube().ToSolid();
            var far = new GeoTriangle3(
                new GeoPoint3(0, 0, 130),
                new GeoPoint3(100, 0, 130),
                new GeoPoint3(0, 100, 130)
            );

            Assert.Equal(Distance3Of(body, far), far.DistanceTo(Cube(), Tol));
            Assert.Equal(30.0, far.DistanceTo(Cube(), Tol), 9);
            Assert.True(TriangleAcross.CollidesWith(Cube(), Tol));
            Assert.False(far.CollidesWith(Cube(), Tol));
        }

        [Fact]
        public void AnEmptyOrNullBox_IsStillRefused()
        {
            Assert.Throws<InvalidOperationException>(() => TriangleAbove.DistanceTo(GeoAabb3.Empty, Tol));
            Assert.Throws<InvalidOperationException>(() => TriangleAbove.CollidesWith(GeoAabb3.Empty, Tol));
            Assert.Throws<InvalidOperationException>(() => SquareAbove.DistanceTo(GeoAabb3.Empty, Tol));
            Assert.Throws<ArgumentNullException>(() => TriangleAbove.DistanceTo((GeoObb3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => TriangleAbove.CollidesWith((GeoObb3)null, Tol));
        }

        private static double Distance3Of(GeoSolid3 body, GeoTriangle3 triangle) =>
            GeometryHelper.Core.Distance3.DistanceTo(body, triangle, Tol);
    }
}
