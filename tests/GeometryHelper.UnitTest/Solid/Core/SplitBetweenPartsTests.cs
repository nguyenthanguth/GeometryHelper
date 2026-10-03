using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A plane that parts a body without leaving a rim to close: one passing between two blocks of it, and one passing along
    /// the edge where two parts of it meet. Each half is closed as it is, and the body is split.
    /// </summary>
    public class SplitBetweenPartsTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPlane3 AtY(double y) => new GeoPlane3(P(0, y, 0), GeoVector3.YAxis);

        [Fact]
        public void APlaneBetweenTwoBlocksOfABodySplitsIt()
        {
            GeoSolid3 low = new GeoObb3(P(500, 500, 500), 1000, 1000, 1000).ToSolid();
            GeoSolid3 high = new GeoObb3(P(500, 2500, 500), 1000, 1000, 1000).ToSolid();
            var both = new GeoSolid3(System.Linq.Enumerable.Concat(low.Faces, high.Faces));

            Assert.True(both.TrySplitBy(AtY(1500), out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.Equal(1E9, above.GetVolume(), 3);
            Assert.Equal(1E9, below.GetVolume(), 3);
            Assert.True(above.IsClosed(Tolerance));
            Assert.True(below.IsClosed(Tolerance));
            Assert.True(above.GetAabb().Min.Y >= 2000 - 1E-9);
            Assert.True(below.GetAabb().Max.Y <= 1000 + 1E-9);
        }

        [Fact]
        public void APlaneAlongTheEdgeWhereTwoPartsMeetSplitsTheBody()
        {
            // Two triangular prisms meeting along the edge at (500, 0): the section touches itself there, and the side at
            // x = 500 runs on past the edge, one face for both.
            var section = new GeoPolygon3(P(0, -1000, 0), P(500, 0, 0), P(100, 900, 0), P(500, 1000, 0), P(500, -1000, 0));
            GeoSolid3 body = GeoSolid3.Extrude(section, new GeoVector3(0, 0, 600), Tolerance);

            Assert.True(body.TrySplitBy(AtY(0), out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.Equal(body.GetVolume(), above.GetVolume() + below.GetVolume(), 3);
            Assert.Equal(0.5 * 400 * 1000 * 600, above.GetVolume(), 3);
            Assert.Equal(0.5 * 500 * 1000 * 600, below.GetVolume(), 3);
            Assert.True(above.IsClosed(Tolerance));
            Assert.True(below.IsClosed(Tolerance));
        }
    }
}
