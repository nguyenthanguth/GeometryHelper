using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two ways in that were simply missing: reading a <see cref="GeoTriangle3"/> as a polygon or a face, and
    /// giving a <see cref="GeoObb3"/> a margin.
    /// </summary>
    /// <remarks>
    /// Neither needed new arithmetic. A triangle out of a triangulation had to be rebuilt from its three
    /// corners before anything taking a polygon would accept it, and the square box could be expanded while
    /// the oriented one could not.
    /// </remarks>
    public class ConversionAndMarginTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoTriangle3 Triangle() => new GeoTriangle3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(0, 400, 0));

        [Fact]
        public void ATriangleReadsAsAPolygonOfTheSameAreaAndPlane()
        {
            GeoTriangle3 triangle = Triangle();
            GeoPolygon3 polygon = triangle.ToPolygon3();

            Assert.Equal(3, polygon.VertexCount);
            Assert.Equal(triangle.Area, polygon.Area, 6);
            Assert.Equal(triangle.Perimeter, polygon.Length, 6);
            Assert.True(polygon.Normal.IsParallelTo(triangle.Normal, Loose));

            // The corners come back in the order they went in, so the winding is the triangle's own.
            Assert.True(polygon.Vertices[0].IsEqualTo(triangle.A, Loose));
            Assert.True(polygon.Vertices[1].IsEqualTo(triangle.B, Loose));
            Assert.True(polygon.Vertices[2].IsEqualTo(triangle.C, Loose));

            // Flipping the triangle flips the polygon with it.
            Assert.True(triangle.Flip().ToPolygon3().Normal.IsEqualTo(polygon.Normal.Negate(), Loose));
        }

        [Fact]
        public void ATriangleReadsAsAFaceWithNoHoles()
        {
            GeoTriangle3 triangle = Triangle();
            GeoFace3 face = triangle.ToFace3();

            Assert.Empty(face.Holes);
            Assert.Equal(triangle.Area, face.Area, 6);
            Assert.True(face.Contains(triangle.Centroid, Loose));

            // A face is what the boolean and offset families take, so this is the point of the conversion.
            Assert.NotEmpty(face.Offset(-10.0));
            Assert.True(face.Offset(-10.0).Sum(smaller => smaller.Area) < face.Area);
        }

        [Fact]
        public void ATriangleWithNoAreaIsRefusedRatherThanHandedOverAsAPolygon()
        {
            // Three points on one line: a triangle will hold them, a polygon will not.
            var flat = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(300, 0, 0));

            Assert.True(flat.IsDegenerate());
            Assert.Throws<ArgumentException>(() => flat.ToPolygon3());
            Assert.Throws<ArgumentException>(() => flat.ToFace3());

            // A sliver a tolerance can still see is kept when the tolerance is tightened.
            var sliver = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 0.01, 0));

            Assert.Equal(sliver.Area, sliver.ToPolygon3(Loose).Area, 9);
            Assert.Equal(sliver.Area, sliver.ToFace3(Loose).Area, 9);
        }

        [Fact]
        public void ATriangleFromATriangulationGoesStraightIntoAFace()
        {
            // The case that named this gap: a solid's triangles used as faces without rebuilding them.
            GeoSolid3 cube = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
            GeoTriangle3[] triangles = cube.Triangulate();

            Assert.NotEmpty(triangles);

            double area = triangles.Where(t => !t.IsDegenerate()).Sum(t => t.ToFace3().Area);

            Assert.Equal(6.0 * 100 * 100, area, 3);
        }

        [Fact]
        public void AnOrientedBoxCanBeGrownAndShrunkWithoutLosingItsAxes()
        {
            var turned = new GeoObb3(new GeoPoint3(0, 0, 0), 200, 100, 50,
                new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

            Assert.True(turned.TryExpand(10.0, out GeoObb3 bigger));

            Assert.Equal(220.0, bigger.SizeX, 6);
            Assert.Equal(120.0, bigger.SizeY, 6);
            Assert.Equal(70.0, bigger.SizeZ, 6);

            // The axes and the centre are untouched, so the corners stay square rather than being re-fitted.
            Assert.True(bigger.Center.IsEqualTo(turned.Center, Loose));
            Assert.True(bigger.AxisX.IsEqualTo(turned.AxisX, Loose));
            Assert.True(bigger.AxisY.IsEqualTo(turned.AxisY, Loose));

            // A margin grows every face by exactly that much, measured along its own normal.
            Assert.All(turned.GetCorners(), corner => Assert.True(bigger.Contains(corner, Loose)));

            Assert.True(turned.TryExpand(-10.0, out GeoObb3 smaller));
            Assert.Equal(180.0, smaller.SizeX, 6);
            Assert.All(smaller.GetCorners(), corner => Assert.True(turned.Contains(corner, Loose)));
        }

        [Fact]
        public void AMarginThatWouldCollapseTheBoxIsRefusedAndTheBoxComesBackWhole()
        {
            var box = new GeoObb3(new GeoPoint3(0, 0, 0), 200, 100, 50);

            // The shortest side is fifty, so twenty-five inwards takes it to nothing. That is where the
            // square twin stops too: the box comes back flat and says so, it is not refused.
            Assert.True(box.TryExpand(-25.0, out GeoObb3 flat));
            Assert.Equal(0.0, flat.SizeZ, 9);
            Assert.True(flat.IsDegenerate());

            // Past nothing is refused, and the box comes back whole.
            Assert.False(box.TryExpand(-25.1, out GeoObb3 gone));
            Assert.True(gone.IsEqualTo(box, Loose));
            Assert.False(box.TryExpand(-1000.0, out _));

            Assert.True(box.TryExpand(-24.9, out GeoObb3 thin));
            Assert.Equal(0.2, thin.SizeZ, 6);
            Assert.False(thin.IsDegenerate());

            // Nought is a margin like any other and changes nothing.
            Assert.True(box.TryExpand(0.0, out GeoObb3 same));
            Assert.True(same.IsEqualTo(box, Loose));

            Assert.Throws<ArgumentOutOfRangeException>(() => box.TryExpand(double.NaN, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => box.TryExpand(double.PositiveInfinity, out _));
        }

        [Fact]
        public void TheSquareBoxAndTheOrientedOneAgreeOnWhatAMarginDoes()
        {
            var square = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(200, 100, 50));

            Assert.True(square.ToObb().TryExpand(10.0, out GeoObb3 grown));
            Assert.Equal(square.Expand(10.0).Volume, grown.Volume, 6);

            // And they agree on where a shrink stops, each saying so in its own way. At exactly nothing both
            // hand back something flat; past nothing the square one is empty and the oriented one says false.
            Assert.False(square.Expand(-25.0).IsEmpty);
            Assert.Equal(0.0, square.Expand(-25.0).SizeZ, 9);
            Assert.True(square.ToObb().TryExpand(-25.0, out GeoObb3 flat));
            Assert.True(flat.IsDegenerate());

            Assert.True(square.Expand(-25.1).IsEmpty);
            Assert.False(square.ToObb().TryExpand(-25.1, out _));

            // Every corner of the grown square box is in the grown oriented one and back again.
            Assert.All(square.Expand(10.0).GetCorners(), corner => Assert.True(grown.Contains(corner, Loose)));
            Assert.All(grown.GetCorners(), corner => Assert.True(square.Expand(10.0).Contains(corner, Loose)));
        }
    }
}
