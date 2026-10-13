using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved chain or loop in space that drops a vertex as a repeat of the one before keeps the bulge of the edge that
    /// has a length, as the plane's do.
    /// </summary>
    /// <remarks>
    /// The constructors drop the later of two vertices within the point tolerance and kept the earlier one's bulge and
    /// normal, those of the edge that vanishes. In the chain (0, 0, 0), (10, 0, 0), (10.0004, 0, 0), (20, 0, 0) with
    /// bulges 0, 0, 1 about Z, the half circle from (10.0004, 0, 0) to (20, 0, 0) became its chord.
    /// </remarks>
    public class CollidesB12ChainRepeatVertex3Tests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        [Fact]
        public void AHalfCircleAfterARepeatedVertexInSpace_IsKept()
        {
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10.0004, 0, 0), new GeoPoint3(20, 0, 0) },
                new[] { 0.0, 0.0, 1.0, 0.0 },
                new[] { Up, Up, Up, Up });

            Assert.Equal(3, chain.VertexCount);
            Assert.False(chain.GetEdgeAt(0).IsArc);
            Assert.True(chain.GetEdgeAt(1).IsArc);
            Assert.Equal(10 + 5 * Math.PI, chain.Length, 1E-9);
        }

        [Fact]
        public void ARepeatInsideALoopInSpace_KeepsTheBulgeOfTheEdgeAfterIt()
        {
            // The square from the origin to (20, 20, 0) with (10.0004, 0, 0) repeating (10, 0, 0) on its bottom; the edge
            // from it to (20, 0, 0) bulges 1, a half circle of radius 5 bowing out below the square: area 400 + 25 pi / 2.
            var loop = new GeoPolygonArc3(
                new[]
                {
                    new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10.0004, 0, 0),
                    new GeoPoint3(20, 0, 0), new GeoPoint3(20, 20, 0), new GeoPoint3(0, 20, 0),
                },
                new[] { 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 },
                new[] { Up, Up, Up, Up, Up, Up });

            Assert.Equal(5, loop.VertexCount);
            Assert.True(loop.GetEdgeAt(1).IsArc);
            Assert.Equal(400 + 12.5 * Math.PI, loop.Area, 1E-9);
        }
    }
}
