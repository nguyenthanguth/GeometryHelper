using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polygon smaller than the vector tolerance in area but with real width is a polygon.
    /// </summary>
    /// <remarks>
    /// The polygon in space was refused as collinear when its area, a length squared, was within the vector tolerance, a
    /// length: a square 0.002 by 0.002, of 4E-6 square units, was refused at the default tolerance, and so was a triangle of
    /// the surface mesh 0.002 across read as a face. A loop under that area is kept now where it has width, twice its area
    /// over its span beyond the point tolerance, as a triangle is read; what was kept before is kept.
    /// </remarks>
    public class PolygonFaultLN2SmallTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        [Fact]
        public void ASquare0Point002Across_IsAPolygon()
        {
            var square = new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0.002, 0, 0), new GeoPoint3(0.002, 0.002, 0), new GeoPoint3(0, 0.002, 0) }, Default);

            Assert.Equal(4E-6, square.Area, 15);
            Assert.Equal(GeoVector3.ZAxis, square.Normal);
        }

        [Fact]
        public void ATriangle0Point002Across_ReadsAsAFace()
        {
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(0.002, 0, 0), new GeoPoint3(0, 0.002, 0));

            Assert.Equal(2E-6, triangle.ToFace3().Area, 15);
            Assert.Equal(2E-6, triangle.ToFace3(Default).Area, 15);
        }

        [Fact]
        public void CornersInARow_AreStillRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0.002, 0, 0), new GeoPoint3(0.004, 1E-9, 0) }, Default));
        }

        [Fact]
        public void ATriangleThinnerThanTheTolerance_IsStillRefused()
        {
            // 0.003 long and 0.0006 high, its corners further apart than the tolerance: it lies within it of its longest side.
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0.003, 0, 0), new GeoPoint3(0.0015, 0.0006, 0) }, Default));
        }

        [Fact]
        public void ALongSliver_IsStillAPolygon()
        {
            // 100 long and 0.0005 high: within the tolerance of its longest side, but over the vector tolerance in area, as
            // it was kept before.
            var sliver = new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 0.0005, 0) }, Default);

            Assert.Equal(0.025, sliver.Area, 12);
        }
    }
}
