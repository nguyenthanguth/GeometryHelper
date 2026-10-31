using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A loop in the plane cut along a chain whose end lands within the tolerance of a corner is cut from the corner, as in
    /// space: both pieces keep the corner, and nothing of the loop is lost.
    /// </summary>
    /// <remarks>
    /// The plate is the square 10 by 10 at the origin, cut at a hundredth. A chain starting 0.004 along its bottom from the
    /// corner (0, 0) cut the corner off, and the sliver of 0.02 square units between was in neither piece. No test sets the
    /// global tolerance.
    /// </remarks>
    public class PolygonCutG1wlTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static GeoPolygon2 Plate() => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

        [Fact]
        public void AChainStartingWithinTheToleranceOfACorner_StartsBothPiecesOnTheCorner()
        {
            var chain = new GeoPolyline2(new GeoPoint2(0.004, 0), new GeoPoint2(10, 5));

            Assert.True(Plate().TrySplitBy(chain, out GeoPolygon2[] pieces, Hundredth));

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Contains(new GeoPoint2(0, 0), piece.Vertices));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainEndingWithinTheToleranceOfACorner_EndsBothPiecesOnTheCorner()
        {
            var chain = new GeoPolyline2(new GeoPoint2(5, 0), new GeoPoint2(5, 5), new GeoPoint2(9.997, 10));

            Assert.True(Plate().TrySplitBy(chain, out GeoPolygon2[] pieces, Hundredth));

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Contains(new GeoPoint2(10, 10), piece.Vertices));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
        }
    }
}
