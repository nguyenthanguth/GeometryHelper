using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A loop in the plane cut along a chain is cut at the tolerance asked, as in space: a side or a piece of a side
    /// shorter than the global tolerance stays where the tolerance asked keeps it.
    /// </summary>
    /// <remarks>
    /// The cut went through space, and the loop, the chain and the pieces were built there and back at the global tolerance
    /// of a thousandth: a corner within a thousandth of the next was dropped, and with it the side the chain started on,
    /// or the place a piece ended. The loops with a side that short are built as a cut at a millionth leaves them. No test
    /// sets the global tolerance.
    /// </remarks>
    public class PolygonCutG1wmTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);

        // The square 10 by 10 at the origin with its corner (0, 0) cut off by a side 0.00057 long.
        private static GeoPolygon2 Chamfered()
        {
            var corners = new[] { new GeoPoint2(0.0004, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0.0004) };
            return new GeoPolygon2(corners, corners.Length);
        }

        [Fact]
        public void ALoopWithASideShorterThanTheGlobalTolerance_IsCutAcrossAtTheToleranceAsked()
        {
            GeoPolygon2 plate = Chamfered();
            var chain = new GeoPolyline2(new GeoPoint2(0, 5), new GeoPoint2(10, 5));

            Assert.True(plate.TrySplitBy(chain, out GeoPolygon2[] pieces, Millionth));

            Assert.Equal(2, pieces.Length);
            Assert.Contains(pieces, piece => piece.Vertices.Contains(new GeoPoint2(0.0004, 0)) && piece.Vertices.Contains(new GeoPoint2(0, 0.0004)));
            Assert.Equal(plate.Area, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainStartingOnASideShorterThanTheGlobalTolerance_StartsBothPiecesThere()
        {
            GeoPolygon2 plate = Chamfered();
            var chain = new GeoPolyline2(new GeoPoint2(0.0002, 0.0002), new GeoPoint2(10, 10));

            Assert.True(plate.TrySplitBy(chain, out GeoPolygon2[] pieces, Millionth));

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Contains(piece.Vertices, corner => corner.IsEqualTo(new GeoPoint2(0.0002, 0.0002), Millionth)));
            Assert.Equal(plate.Area, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainEndingCloserToACornerThanTheGlobalTolerance_EndsBothPiecesWhereItEnds()
        {
            var plate = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var chain = new GeoPolyline2(new GeoPoint2(5, 0), new GeoPoint2(9.9995, 10));

            Assert.True(plate.TrySplitBy(chain, out GeoPolygon2[] pieces, Millionth));

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Contains(piece.Vertices, corner => corner.IsEqualTo(new GeoPoint2(9.9995, 10), Millionth)));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void ALoopWithASideShorterThanTheGlobalTolerance_IsCutByALineAtTheToleranceAsked()
        {
            GeoPolygon2 plate = Chamfered();
            var cutter = new GeoLine2(new GeoPoint2(10, 5), new GeoPoint2(0, 5));

            Assert.True(plate.TrySplitBy(cutter, out GeoPolygon2[] left, out GeoPolygon2[] right, Millionth));

            GeoPolygon2 bottom = Assert.Single(left);
            Assert.Single(right);
            Assert.Contains(new GeoPoint2(0.0004, 0), bottom.Vertices);
            Assert.Contains(new GeoPoint2(0, 0.0004), bottom.Vertices);
            Assert.Equal(plate.Area, bottom.Area + right[0].Area, 9);
        }

        [Fact]
        public void ALineCuttingOffACornerSmallerThanTheGlobalTolerance_GivesTheCornerAsAPiece()
        {
            var plate = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var cutter = new GeoLine2(new GeoPoint2(0.0005, 0), new GeoPoint2(0, 0.0005));

            Assert.True(plate.TrySplitBy(cutter, out GeoPolygon2[] left, out GeoPolygon2[] right, Millionth));

            Assert.Equal(1.25E-7, Assert.Single(left).Area, 15);
            Assert.Equal(100 - 1.25E-7, Assert.Single(right).Area, 9);
        }

        [Fact]
        public void ALoopSmallerThanTheGlobalTolerance_IsCutInTwoAtTheToleranceAsked()
        {
            var corners = new[] { new GeoPoint2(0, 0), new GeoPoint2(0.0005, 0), new GeoPoint2(0.0005, 0.0005), new GeoPoint2(0, 0.0005) };
            var plate = new GeoPolygon2(corners, corners.Length);
            var ends = new[] { new GeoPoint2(0.00025, 0), new GeoPoint2(0.00025, 0.0005) };
            var chain = new GeoPolyline2(ends, ends.Length);
            var tenMillionth = new Tolerance(1E-7, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-7);

            Assert.True(plate.TrySplitBy(chain, out GeoPolygon2[] pieces, tenMillionth));

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Equal(1.25E-7, piece.Area, 15));
        }
    }
}
