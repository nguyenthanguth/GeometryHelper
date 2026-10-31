using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polygon cut along a chain whose end lands within the tolerance of a corner is cut from the corner: both pieces keep
    /// the corner, and nothing of the polygon is lost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plate is the square 10 by 10 at the origin, cut at a hundredth. A chain starting 0.004 along its bottom from the
    /// corner (0, 0, 0) cut the corner off: the piece round the corner ran from (0, 10, 0) straight to (0.004, 0, 0), and the
    /// sliver of 0.02 square units between was in neither piece.
    /// </para>
    /// <para>
    /// The notched plate is that square with the corner (0.008, 0.004, 0) between (0, 10, 0) and the origin, 0.0089 from
    /// the origin. A chain starting 0.003 along the bottom is within the hundredth of both corners, and the piece ending
    /// there gave up its end for the notch corner, so the two pieces started the cut 0.0064 apart. No test sets the global
    /// tolerance.
    /// </para>
    /// </remarks>
    public class PolygonCutG1wlTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static GeoPolygon3 Plate()
            => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

        private static GeoPolygon3 NotchedPlate() => new GeoPolygon3(
            new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(0.008, 0.004, 0) },
            new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9));

        private static void AssertBothKeep(GeoPolygon3[] pieces, GeoPoint3 corner)
        {
            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Contains(corner, piece.Vertices));
        }

        [Fact]
        public void AChainStartingWithinTheToleranceOfACorner_StartsBothPiecesOnTheCorner()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0.004, 0, 0), new GeoPoint3(10, 5, 0));

            Assert.True(Plate().TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            AssertBothKeep(pieces, new GeoPoint3(0, 0, 0));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
            Assert.Equal(25, pieces.Min(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainEndingWithinTheToleranceOfACorner_EndsBothPiecesOnTheCorner()
        {
            // 0.006 below the corner (10, 10, 0), a hair off the side.
            var chain = new GeoPolyline3(new GeoPoint3(5, 0, 0), new GeoPoint3(5, 5, 0), new GeoPoint3(10.002, 9.994, 0));

            Assert.True(Plate().TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            AssertBothKeep(pieces, new GeoPoint3(10, 10, 0));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainStartingFartherThanTheToleranceFromACorner_StartsWhereItMeetsTheOutline()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0.011, 0, 0), new GeoPoint3(10, 5, 0));

            Assert.True(Plate().TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Contains(piece.Vertices, corner => corner.DistanceTo(new GeoPoint3(0.011, 0, 0)) <= 1E-12));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainStartingWithinTheToleranceOfACornerOnATiltedPlate_StartsBothPiecesOnTheCorner()
        {
            // The plate tilted about x: (x, y, 0) goes to (x, 0.6 y, 0.8 y); the chain starts 0.005 up its left side.
            var plate = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 6, 8), new GeoPoint3(0, 6, 8));
            var chain = new GeoPolyline3(new GeoPoint3(0, 0.003, 0.004), new GeoPoint3(10, 3, 4));

            Assert.True(plate.TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            AssertBothKeep(pieces, new GeoPoint3(0, 0, 0));
            Assert.Equal(100, pieces.Sum(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainStartingWithinTheToleranceOfTwoCorners_StartsBothPiecesOnTheNearer()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0.003, 0, 0), new GeoPoint3(10, 5, 0));

            Assert.True(NotchedPlate().TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            AssertBothKeep(pieces, new GeoPoint3(0, 0, 0));
            Assert.All(pieces, piece => Assert.DoesNotContain(new GeoPoint3(0.003, 0, 0), piece.Vertices));
        }

        [Fact]
        public void AChainStartingBesideACornerAfterTwoShortSides_StartsBothPiecesOnTheCorner()
        {
            // The square with the corners (0.005, 0.0085, 0) and (0, 0.006, 0) before the origin: two sides about 0.006 long,
            // folded so the first is 0.0099 from the origin though 0.0116 round. The piece ending at the origin kept that corner
            // and gave up its end for it, so it met the outline 0.0087 from where the other did.
            var plate = new GeoPolygon3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(0.005, 0.0085, 0), new GeoPoint3(0, 0.006, 0) },
                new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9));
            var chain = new GeoPolyline3(new GeoPoint3(0.003, 0, 0), new GeoPoint3(10, 5, 0));

            Assert.True(plate.TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            AssertBothKeep(pieces, new GeoPoint3(0, 0, 0));
            Assert.Equal(25, pieces.Min(piece => piece.Area), 9);
        }

        [Fact]
        public void AChainWhoseEndsLandWithinTheToleranceOfOneCorner_IsRefused()
        {
            // Both ends 0.009 from the origin, one on each side of it and 0.0127 apart, and the chain bent inside between
            // them; it cut off a piece of 4 square units round the origin, where it now meets the outline at one place.
            var chain = new GeoPolyline3(new GeoPoint3(0.009, 0, 0), new GeoPoint3(3, 1, 0), new GeoPoint3(1, 3, 0), new GeoPoint3(0, 0.009, 0));

            Assert.False(Plate().TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            Assert.Single(pieces);
        }
    }
}
