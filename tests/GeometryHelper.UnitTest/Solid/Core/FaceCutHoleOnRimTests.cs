using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A face whose hole touches its boundary at two corners, cut through the hole's third: every corner of the hole stands
    /// on the rim of the piece it falls in, and the hole is still a hole of it.
    /// </summary>
    public class FaceCutHoleOnRimTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        /// <summary>A plate 1000 by 800, its triangular hole touching the top at (350, 800) and the left side at (0, 400).</summary>
        private static GeoFace3 Face()
        {
            var boundary = new GeoPolygon3(new[] { P(0, 0), P(1000, 0), P(1000, 800), P(350, 800), P(0, 800) }, Tolerance);
            var hole = new GeoPolygon3(new[] { P(400, 250), P(350, 800), P(0, 400) }, Tolerance);
            return new GeoFace3(boundary, new[] { hole }, Tolerance);
        }

        [Fact]
        public void ACutThroughACornerOfAHoleOnTheRimKeepsTheHoleOpen()
        {
            GeoFace3 face = Face();
            double hole = face.Holes[0].Area;
            var cut = new GeoPlane3(P(400, 0), GeoVector3.XAxis);

            Assert.True(Splition3.TrySplitBy(face, cut, out GeoFace3[] above, out GeoFace3[] below, Tolerance));
            Assert.Equal(600.0 * 800, above.Sum(f => f.Area), 6);
            Assert.Equal(400.0 * 800 - hole, below.Sum(f => f.Area), 6);
            Assert.Equal(face.Area, above.Sum(f => f.Area) + below.Sum(f => f.Area), 6);
        }

        [Fact]
        public void ALoopWhoseCornersAllStandOnAnotherIsInsideItWhereItsSidesAre()
        {
            var outer = new GeoPolygon3(new[] { P(0, 0), P(400, 0), P(400, 800), P(350, 800), P(0, 800) }, Tolerance);
            var inner = new GeoPolygon3(new[] { P(400, 250), P(350, 800), P(0, 400) }, Tolerance);
            var beside = new GeoPolygon3(new[] { P(400, 0), P(600, 0), P(400, 800) }, Tolerance);

            Assert.True(LoopAssembly.IsLoopInside(inner, outer, Tolerance));
            Assert.False(LoopAssembly.IsLoopInside(beside, outer, Tolerance));
            Assert.False(LoopAssembly.IsLoopInside(outer, outer, Tolerance));
        }
    }
}
