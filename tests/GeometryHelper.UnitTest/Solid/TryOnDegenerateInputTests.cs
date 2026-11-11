using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Try methods answer false, never a throw, on geometry they cannot work with: a box flat on some axis or empty, a
    /// circle standing upright in the plane of an ellipse within the planar tolerance, a face of no area, and a chamfer
    /// that leaves no polygon. Found by a reflection fuzz of every public Try method with ordinary and degenerate shapes.
    /// </summary>
    public class TryOnDegenerateInputTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>A box 100 by 100 by 0 about (50, 50, 0): the rectangle (0, 0, 0) to (100, 100, 0).</summary>
        private static GeoObb3 Sheet() => new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, 0);

        /// <summary>A box 100 by 0 by 0 about (50, 200, 0): the segment (0, 200, 0) to (100, 200, 0).</summary>
        private static GeoObb3 Stick() => new GeoObb3(new GeoPoint3(50, 200, 0), 100, 0, 0);

        private static GeoObb3 Cube() => new GeoObb3(new GeoPoint3(50, 50, 50), 100, 100, 100);

        [Fact]
        public void AnArcThroughABoxFlatOnOneAxis_MeetsItsRectangle_AndAStickIsMetNowhere()
        {
            // The arc of radius 50 about (50, 50, 0) standing upright in x = 50, through (50, 0, 0), (50, 50, 50) and
            // (50, 100, 0): it passes the rectangle's edges at its two ends. Once an InvalidOperationException from GetFaces.
            GeoArc3 arc = GeoArc3.FromThreePoints(
                new GeoPoint3(50, 0, 0),
                new GeoPoint3(50, 50, 50),
                new GeoPoint3(50, 100, 0)
            );

            Assert.True(arc.TryIntersectWith(Sheet(), out GeoPoint3[] points, Tol));
            Assert.Equal(2, points.Length);
            Assert.Contains(points, p => p.IsEqualTo(new GeoPoint3(50, 0, 0), Tol));
            Assert.Contains(points, p => p.IsEqualTo(new GeoPoint3(50, 100, 0), Tol));

            var circle = new GeoCircle3(new GeoPoint3(50, 50, 0), new GeoVector3(1, 0, 0), 30);
            Assert.True(circle.TryIntersectWith(Sheet(), out GeoPoint3[] crossings, Tol));
            Assert.Equal(2, crossings.Length);

            Assert.False(arc.TryIntersectWith(Stick(), out GeoPoint3[] none, Tol));
            Assert.Empty(none);

            var flatAabb = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0));
            Assert.True(arc.TryIntersectWith(flatAabb, out GeoPoint3[] fromAabb, Tol));
            Assert.Equal(2, fromAabb.Length);
        }

        [Fact]
        public void AnEllipseThroughABoxFlatOnOneAxis_MeetsItsRectangle()
        {
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(
                new GeoCircle3(new GeoPoint3(50, 50, 0), new GeoVector3(1, 0, 0), 30)
            );

            Assert.True(ellipse.TryIntersectWith(Sheet(), out GeoPoint3[] points, Tol));
            Assert.Equal(2, points.Length);
            Assert.Contains(points, p => p.IsEqualTo(new GeoPoint3(50, 20, 0), Tol));
            Assert.Contains(points, p => p.IsEqualTo(new GeoPoint3(50, 80, 0), Tol));
            Assert.False(ellipse.TryIntersectWith(Stick(), out _, Tol));
        }

        [Fact]
        public void AFlatRegionOrAPlaneAgainstABoxFlatOrEmpty_IsFalse_NotAThrow()
        {
            var triangle = new GeoTriangle3(
                new GeoPoint3(-50, -50, 0),
                new GeoPoint3(300, -50, 0),
                new GeoPoint3(-50, 300, 0)
            );
            var polygon = new GeoPolygon3(
                new GeoPoint3(20, -10, -10),
                new GeoPoint3(20, 110, -10),
                new GeoPoint3(20, 110, 10),
                new GeoPoint3(20, -10, 10)
            );
            var face = new GeoFace3(polygon);
            var plane = new GeoPlane3(new GeoPoint3(20, 0, 0), new GeoVector3(1, 0, 0));
            var flatAabb = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0));
            GeoAabb3 empty = GeoAabb3.FromPoints(new GeoPoint3[0]);

            foreach (GeoObb3 box in new[] { Sheet(), Stick() })
            {
                Assert.False(triangle.TryIntersectWith(box, out GeoPoint3[] a, Tol));
                Assert.Empty(a);
                Assert.False(polygon.TryIntersectWith(box, out GeoPoint3[] b, Tol));
                Assert.Empty(b);
                Assert.False(face.TryIntersectWith(box, out GeoPoint3[] c, Tol));
                Assert.Empty(c);
                Assert.False(plane.TryIntersectWith(box, out GeoPoint3[] d, Tol));
                Assert.Empty(d);
            }

            foreach (GeoAabb3 box in new[] { flatAabb, empty })
            {
                Assert.False(triangle.TryIntersectWith(box, out _, Tol));
                Assert.False(polygon.TryIntersectWith(box, out _, Tol));
                Assert.False(face.TryIntersectWith(box, out _, Tol));
                Assert.False(plane.TryIntersectWith(box, out _, Tol));
            }

            // A box with its faces still answers as its body: the plane x = 20 cuts the cube's four edges along x.
            Assert.True(plane.TryIntersectWith(Cube(), out GeoPoint3[] cut, Tol));
            Assert.Equal(4, cut.Length);
        }

        [Fact]
        public void ABooleanWithABoxFlatOnSomeAxis_IsFalse_WithNoResult()
        {
            GeoSolid3 body = Cube().ToSolid();

            Assert.False(Sheet().TryUnion(Cube(), out GeoSolid3 a, Tol));
            Assert.Null(a);
            Assert.False(Cube().TryIntersect(Sheet(), out GeoSolid3 b, Tol));
            Assert.Null(b);
            Assert.False(Cube().TrySubtract(Stick(), out GeoSolid3 c, Tol));
            Assert.Null(c);
            Assert.False(Sheet().TryUnion(body, out _, Tol));
            Assert.False(Sheet().TryIntersect(body, out _, Tol));
            Assert.False(Sheet().TrySubtract(body, out _, Tol));
            Assert.False(body.TryUnion(Sheet(), out _, Tol));
            Assert.False(body.TryIntersect(Sheet(), out _, Tol));
            Assert.False(body.TrySubtract(Sheet(), out GeoSolid3 d, Tol));
            Assert.Null(d);

            Assert.Throws<ArgumentNullException>(() => Sheet().TryUnion((GeoSolid3)null, out _, Tol));
            Assert.Throws<ArgumentNullException>(() => body.TryUnion((GeoObb3)null, out _, Tol));
        }

        [Fact]
        public void AnArcChainCutByABoxFlatOnOneAxis_LiesWhollyOutsideIt_NotAThrow()
        {
            // The arc of radius 30 about (50, 20, 0) upright in x = 50, from below the rectangle to above it through (50, 50, 0):
            // cut there, both pieces are outside a box that holds nothing, and run on as one. Once an
            // InvalidOperationException from GetFaces.
            GeoArc3 arc = GeoArc3.FromThreePoints(
                new GeoPoint3(50, 20, -30),
                new GeoPoint3(50, 50, 0),
                new GeoPoint3(50, 20, 30)
            );
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(arc) });

            Assert.False(chain.TrySplitBy(Sheet(), out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside, Tol));
            Assert.Empty(inside);
            Assert.Single(outside);
        }

        [Fact]
        public void TwoEllipsesOneACircleOfRadius0Point0001StandingUpright_AreAnswered_NotAThrow()
        {
            // The small circle stands in y = 0 with its major axis along z, square to the plane of the large one, and lies in
            // that plane within the planar tolerance. Once an ArgumentException from laying it out with an axis of no length.
            GeoEllipse3 large = GeoEllipse3.FromCircle(
                new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100)
            );
            GeoEllipse3 small = GeoEllipse3.FromCircle(
                new GeoCircle3(new GeoPoint3(100, 0, 0), new GeoVector3(0, 1, 0), 0.0001)
            );
            GeoEllipse3 far = GeoEllipse3.FromCircle(
                new GeoCircle3(new GeoPoint3(50, 0, 0), new GeoVector3(0, 1, 0), 0.0001)
            );

            Assert.True(large.TryIntersectWith(small, out GeoPoint3[] touch, Tol));
            Assert.All(touch, p => Assert.True(p.DistanceTo(new GeoPoint3(100, 0, 0)) <= 0.002));
            Assert.False(large.TryIntersectWith(far, out _, Tol));
        }

        [Fact]
        public void AFaceOfNoAreaCutByALine_ComesBackWhole_NotAThrow()
        {
            var flat = new GeoFace2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(50, 0)));

            Assert.False(
                flat.TrySplitBy(
                    new GeoLine2(new GeoPoint2(50, -10), new GeoPoint2(50, 10)),
                    out GeoFace2[] left,
                    out GeoFace2[] right,
                    Tol
                )
            );
            Assert.Same(flat, Assert.Single(left));
            Assert.Empty(right);
        }

        [Fact]
        public void AChamferThatLeavesNoPolygon_IsFalse_AndThePolygonStays()
        {
            // Cut 100 back each way from the corner at the origin, the triangle with sides of 100 there keeps two corners.
            var triangle2 = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100));
            Assert.False(triangle2.TryChamferAt(0, 100, 100, out GeoPolygon2 kept2, Tol));
            Assert.Same(triangle2, kept2);
            Assert.True(triangle2.TryChamferAt(0, 10, 10, out GeoPolygon2 cut2, Tol));
            Assert.Equal(4, cut2.VertexCount);

            var triangle3 = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            Assert.False(triangle3.TryChamferAt(0, 100, 100, out GeoPolygon3 kept3, Tol));
            Assert.Same(triangle3, kept3);
            Assert.True(triangle3.TryChamferAt(0, 10, 10, out GeoPolygon3 cut3, Tol));
            Assert.Equal(4, cut3.VertexCount);
        }
    }
}
