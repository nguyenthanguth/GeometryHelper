using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A concave polygon is measured to where its material is, not to the triangles it fans into.
    /// </summary>
    /// <remarks>
    /// A polygon fanned from its first corner covers itself only when it is convex: fanned from the wrong
    /// corner, an L reaches across its own notch with one triangle and takes it back with another wound the
    /// other way. The signed sums built on the fan — area, centroid, volume — come out right; a distance does
    /// not, and a body sitting in the notch measured nought to the L while CollidesWith said it did not touch.
    /// </remarks>
    public class ConcavePolygonDistanceTests
    {
        /// <summary>An L in the ground plane, begun at the corner whose fan reaches across the notch.</summary>
        private static readonly GeoPolygon3 L = new GeoPolygon3(
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100, 30, 0),
            new GeoPoint3(30, 30, 0),
            new GeoPoint3(30, 100, 0),
            new GeoPoint3(0, 100, 0),
            new GeoPoint3(0, 0, 0));

        [Fact]
        public void ABodySittingInTheNotchIsMeasuredToTheMaterial()
        {
            // Eight clear of the bar below, fifteen of the one beside.
            GeoSolid3 block = new GeoAabb3(new GeoPoint3(45, 38, -5), new GeoPoint3(60, 50, 5)).ToObb().ToSolid();

            Assert.False(block.CollidesWith(L));
            Assert.Equal(8.0, block.DistanceTo(L), 9);
            Assert.Equal(8.0, L.DistanceTo(block), 9);
        }

        [Fact]
        public void AChainThroughTheNotchIsMeasuredToTheMaterial()
        {
            var post = new GeoPolyline3(new GeoPoint3(45, 45, -10), new GeoPoint3(45, 45, 10), new GeoPoint3(45, 45, 20));

            Assert.Equal(15.0, post.DistanceTo(L), 9);
            Assert.Equal(15.0, L.DistanceTo(post), 9);
        }

        [Fact]
        public void TheSignedSumsBuiltOnTheFanStayRight()
        {
            Assert.Equal(100.0 * 30 + 30 * 70, L.Area, 9);
        }
    }
}
