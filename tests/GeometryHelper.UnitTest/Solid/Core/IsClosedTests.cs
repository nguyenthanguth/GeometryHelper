using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body is closed when every stretch of every edge is shared by an even number of faces.
    /// </summary>
    /// <remarks>
    /// The test used to match edges by their end points and want each used exactly twice. A long edge beside
    /// two short ones — which merging coplanar faces and the booleans both leave — has no partner by end
    /// points, and two blocks meeting along an edge put one edge under four faces; both bodies are watertight,
    /// measure right and hold the right points, and both were called open. IfcConvert warned on them and meshed
    /// them again, and the guides say to ask IsClosed before trusting a volume.
    /// </remarks>
    public class IsClosedTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoFace3 Face(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners));

        /// <summary>A hundred cube whose top is drawn as two faces while its front keeps one long top edge.</summary>
        private static List<GeoFace3> CubeWithSplitTop()
        {
            GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

            return new List<GeoFace3>
            {
                Face(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0)),          // bottom, facing down
                Face(P(0, 0, 100), P(50, 0, 100), P(50, 100, 100), P(0, 100, 100)),    // top, left half
                Face(P(50, 0, 100), P(100, 0, 100), P(100, 100, 100), P(50, 100, 100)), // top, right half
                Face(P(0, 0, 0), P(100, 0, 0), P(100, 0, 100), P(0, 0, 100)),          // front: one top edge
                Face(P(0, 100, 0), P(0, 100, 100), P(100, 100, 100), P(100, 100, 0)),  // back: one top edge
                Face(P(0, 0, 0), P(0, 0, 100), P(0, 100, 100), P(0, 100, 0)),          // left
                Face(P(100, 0, 0), P(100, 100, 0), P(100, 100, 100), P(100, 0, 100)),  // right
            };
        }

        [Fact]
        public void ALongEdgeBesideTwoShortOnesIsClosed()
        {
            var cube = new GeoSolid3(CubeWithSplitTop());

            Assert.True(cube.IsClosed());
            Assert.Equal(1000000.0, cube.GetVolume(), 6);
        }

        [Fact]
        public void AShortEdgeAHairOutOfTrueBesideALongOneIsClosed()
        {
            // A block 300 high whose left side is two faces, the lower 299 high and a strip of 1 above it, their shared
            // corner at the front a thousandth out of true; the front face keeps its left edge whole. The strip's edge was
            // matched along its own direction, carried the 300 of the front's edge, where the thousandth became three
            // tenths and read as off that line, and the block was called open.
            GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);
            GeoPoint3 corner = P(-0.001, 0, 299);
            var block = new GeoSolid3(new List<GeoFace3>
            {
                Face(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0)),         // bottom
                Face(P(0, 0, 300), P(100, 0, 300), P(100, 100, 300), P(0, 100, 300)), // top
                Face(P(0, 0, 0), P(100, 0, 0), P(100, 0, 300), P(0, 0, 300)),         // front: its left edge whole
                Face(P(0, 100, 0), P(0, 100, 300), P(100, 100, 300), P(100, 100, 0)), // back
                Face(P(100, 0, 0), P(100, 100, 0), P(100, 100, 300), P(100, 0, 300)), // right
                Face(P(0, 0, 0), corner, P(0, 100, 299), P(0, 100, 0)),               // left, the lower face
                Face(corner, P(0, 0, 300), P(0, 100, 300), P(0, 100, 299)),           // left, the strip above it
            });

            Assert.True(block.IsClosed());
            Assert.InRange(block.GetVolume(), 3000000.0 - 100.0, 3000000.0 + 100.0);
        }

        [Fact]
        public void TwoBlocksMeetingAlongAnEdgeAreClosed()
        {
            var two = new GeoSolid3(Box(0, 0, 0, 100, 100, 100).Faces.Concat(Box(100, 100, 0, 200, 200, 100).Faces));

            Assert.True(two.IsClosed());
        }

        [Fact]
        public void TheUnionOfTwoBlocksMeetingAlongAnEdgeIsClosed()
        {
            Assert.True(Box(40, 0, 0, 80, 40, 30).TryUnion(Box(0, 10, 30, 40, 30, 60), out GeoSolid3 union));

            Assert.True(union.IsClosed());
        }

        [Fact]
        public void AMissingFaceIsStillOpen()
        {
            List<GeoFace3> faces = CubeWithSplitTop();
            faces.RemoveAt(2);

            Assert.False(new GeoSolid3(faces).IsClosed());
            Assert.False(new GeoSolid3(Box(0, 0, 0, 10, 10, 10).Faces.Skip(1)).IsClosed());
        }

        [Fact]
        public void AFinStandingOnABodyIsStillOpen()
        {
            // A lone face standing on the top: its bottom edge lies along the top face, and its other three
            // edges belong to nothing.
            GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);
            var finned = new GeoSolid3(Box(0, 0, 0, 100, 100, 100).Faces.Concat(new[] { Face(P(20, 50, 100), P(80, 50, 100), P(80, 50, 150), P(20, 50, 150)) }));

            Assert.False(finned.IsClosed());
        }
    }
}
