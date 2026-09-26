using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Cutting an area in the plane into areas. Everything the plane could cut came back as lines; nothing came
    /// back as an area, though <see cref="GeoPolygon3"/> and <see cref="GeoFace3"/> had both readings all along.
    /// </summary>
    /// <remarks>
    /// The shape is laid flat at z = 0 and cut by the arithmetic space already holds, so the two cannot drift
    /// apart — there is only one of them. The tests below check that the answer is the same as space's on the
    /// same shape, and that nothing is lost: the pieces add back up to what went in.
    /// </remarks>
    public class AreaSplittingTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A two hundred square at the origin.</summary>
        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200));

        /// <summary>An L, concave but in one piece whichever way it is cut across.</summary>
        private static GeoPolygon2 Ell() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 100),
            new GeoPoint2(100, 100), new GeoPoint2(100, 300), new GeoPoint2(0, 300));

        /// <summary>A U, whose two prongs part company when it is cut across them.</summary>
        private static GeoPolygon2 Yoke() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 300), new GeoPoint2(200, 300),
            new GeoPoint2(200, 100), new GeoPoint2(100, 100), new GeoPoint2(100, 300), new GeoPoint2(0, 300));

        [Fact]
        public void ALoopCutByALineComesBackAsTwoLoopsThatAddUp()
        {
            GeoPolygon2 square = Square();

            // Up the middle, travelling in +y, so the left is the small x side.
            var cutter = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 250));

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon2[] left, out GeoPolygon2[] right));

            Assert.Single(left);
            Assert.Single(right);
            Assert.Equal(square.Area, left[0].Area + right[0].Area, 6);
            Assert.Equal(square.Area / 2.0, left[0].Area, 6);

            // Left is the left of the cutter's travel: going up, that is the small x side.
            Assert.True(left[0].Contains(new GeoPoint2(50, 100), Loose));
            Assert.True(right[0].Contains(new GeoPoint2(150, 100), Loose));
        }

        [Fact]
        public void ReversingTheCutterSwapsTheTwoAnswersAndNothingElse()
        {
            GeoPolygon2 square = Square();
            var cutter = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 250));

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon2[] left, out GeoPolygon2[] right));
            Assert.True(square.TrySplitBy(cutter.Reverse(), out GeoPolygon2[] backLeft, out GeoPolygon2[] backRight));

            Assert.Equal(left[0].Area, backRight[0].Area, 6);
            Assert.Equal(right[0].Area, backLeft[0].Area, 6);
            Assert.True(left[0].Contains(new GeoPoint2(50, 100), Loose));
            Assert.True(backRight[0].Contains(new GeoPoint2(50, 100), Loose));
        }

        [Fact]
        public void TheCuttersLengthIsIgnoredBecauseItIsReadAsTheWholeLine()
        {
            GeoPolygon2 square = Square();

            // A stub inside the square, nowhere near either boundary.
            var stub = new GeoLine2(new GeoPoint2(100, 90), new GeoPoint2(100, 110));

            Assert.True(square.TrySplitBy(stub, out GeoPolygon2[] left, out GeoPolygon2[] right));
            Assert.Equal(square.Area / 2.0, left[0].Area, 6);
            Assert.Equal(square.Area / 2.0, right[0].Area, 6);

            // A line missing the loop altogether cuts nothing, and the loop comes back on the side it is on.
            var clear = new GeoLine2(new GeoPoint2(500, 0), new GeoPoint2(500, 100));

            Assert.False(square.TrySplitBy(clear, out GeoPolygon2[] whole, out GeoPolygon2[] none));
            Assert.Single(whole);
            Assert.Empty(none);
            Assert.Equal(square.Area, whole[0].Area, 6);

            // A cutter with no length picks out no line at all.
            var nowhere = new GeoLine2(new GeoPoint2(100, 100), new GeoPoint2(100, 100));

            Assert.False(square.TrySplitBy(nowhere, out GeoPolygon2[] still, out _));
            Assert.Equal(square.Area, still[0].Area, 6);
        }

        [Fact]
        public void AConcaveLoopCanGiveOneSideMoreThanOnePiece()
        {
            GeoPolygon2 yoke = Yoke();

            // A horizontal line through both prongs, travelling in +x, so above it is the left.
            var cutter = new GeoLine2(new GeoPoint2(-50, 200), new GeoPoint2(350, 200));

            Assert.True(yoke.TrySplitBy(cutter, out GeoPolygon2[] above, out GeoPolygon2[] below));

            Assert.Equal(yoke.Area, above.Sum(p => p.Area) + below.Sum(p => p.Area), 6);

            // Above the line the two prongs have parted company; below it the base is still one piece.
            Assert.Equal(2, above.Length);
            Assert.Single(below);

            // An L, cut the same way across its foot, stays in one piece on both sides: concave is not enough
            // on its own, the cut has to separate something.
            GeoPolygon2 ell = Ell();

            Assert.True(ell.TrySplitBy(new GeoLine2(new GeoPoint2(-50, 50), new GeoPoint2(350, 50)),
                out GeoPolygon2[] ellAbove, out GeoPolygon2[] ellBelow));
            Assert.Single(ellAbove);
            Assert.Single(ellBelow);
            Assert.Equal(ell.Area, ellAbove[0].Area + ellBelow[0].Area, 6);
        }

        [Fact]
        public void AFaceKeepsAHoleTheLineMissesAndOpensOneItCrosses()
        {
            var boundary = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 300), new GeoPoint2(0, 300));
            var hole = new GeoPolygon2(
                new GeoPoint2(200, 200), new GeoPoint2(260, 200), new GeoPoint2(260, 260), new GeoPoint2(200, 260));

            var face = new GeoFace2(boundary, new[] { hole });

            // Well clear of the hole: the piece that keeps it keeps it as a hole.
            var missing = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 350));

            Assert.True(face.TrySplitBy(missing, out GeoFace2[] left, out GeoFace2[] right));
            Assert.Equal(face.Area, left.Sum(f => f.Area) + right.Sum(f => f.Area), 6);
            Assert.Equal(1, left.Concat(right).Sum(f => f.Holes.Count));

            // Straight through the hole: it opens into the outline of both pieces, so neither has a hole left.
            var crossing = new GeoLine2(new GeoPoint2(230, -50), new GeoPoint2(230, 350));

            Assert.True(face.TrySplitBy(crossing, out GeoFace2[] cutLeft, out GeoFace2[] cutRight));
            Assert.Equal(face.Area, cutLeft.Sum(f => f.Area) + cutRight.Sum(f => f.Area), 6);
            Assert.All(cutLeft.Concat(cutRight), piece => Assert.Empty(piece.Holes));
        }

        [Fact]
        public void ALoopCanBeCutAlongAChainDrawnAcrossIt()
        {
            GeoPolygon2 square = Square();

            // A dog-leg from one side to the other, staying inside all the way.
            var cutLine = new GeoPolyline2(
                new GeoPoint2(0, 100), new GeoPoint2(100, 60), new GeoPoint2(200, 100));

            Assert.True(square.TrySplitBy(cutLine, out GeoPolygon2[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(square.Area, pieces.Sum(p => p.Area), 6);

            // The pieces keep the chain's own vertices, so the cut is the dog-leg and not a straight line.
            Assert.Contains(pieces, p => p.Vertices.Any(v => v.IsEqualTo(new GeoPoint2(100, 60), Loose)));
            Assert.All(pieces, p => Assert.True(p.Area < square.Area));
        }

        [Fact]
        public void AChainThatLeavesTheLoopOrStopsShortOfItCutsNothing()
        {
            GeoPolygon2 square = Square();

            // Both ends inside: there is no way to tell which two pieces are meant.
            var short_ = new GeoPolyline2(new GeoPoint2(50, 100), new GeoPoint2(150, 100));

            Assert.False(square.TrySplitBy(short_, out GeoPolygon2[] whole));
            Assert.Single(whole);
            Assert.Equal(square.Area, whole[0].Area, 6);

            // Out and back again would divide the loop into more than two, so it is refused.
            var wandering = new GeoPolyline2(
                new GeoPoint2(0, 100), new GeoPoint2(100, -100), new GeoPoint2(200, 100));

            Assert.False(square.TrySplitBy(wandering, out _));

            // A chain that comes back to where it started divides nothing off, so it is refused too.
            var returning = new GeoPolyline2(
                new GeoPoint2(0, 100), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

            Assert.False(square.TrySplitBy(returning, out GeoPolygon2[] intact));
            Assert.Single(intact);
            Assert.Equal(square.Area, intact[0].Area, 6);
        }

        [Fact]
        public void ThePlaneAndSpaceCutTheSameShapeTheSameWay()
        {
            GeoPolygon2 ell = Yoke();
            var cutter = new GeoLine2(new GeoPoint2(-50, 200), new GeoPoint2(350, 200));

            Assert.True(ell.TrySplitBy(cutter, out GeoPolygon2[] left, out GeoPolygon2[] right));

            // The same shape lifted to z = 0, cut by the plane standing on the same line.
            GeoCoordinateSystem3 frame = GeoCoordinateSystem3.Global;
            GeoPolygon3 lifted = PlanarMap.ToPolygon3(frame, ell);
            var plane = new GeoPlane3(new GeoPoint3(-50, 200, 0), GeoVector3.ZAxis.CrossProduct(new GeoVector3(400, 0, 0)));

            Assert.True(Splition3.TrySplitBy(lifted, plane, out GeoPolygon3[] above, out GeoPolygon3[] below));

            Assert.Equal(above.Length, left.Length);
            Assert.Equal(below.Length, right.Length);
            Assert.Equal(above.Sum(p => p.Area), left.Sum(p => p.Area), 6);
            Assert.Equal(below.Sum(p => p.Area), right.Sum(p => p.Area), 6);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolygon2 square = Square();
            var face = new GeoFace2(square);
            var cutter = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 250));
            var cutLine = new GeoPolyline2(new GeoPoint2(0, 100), new GeoPoint2(100, 60), new GeoPoint2(200, 100));

            Assert.Equal(
                square.TrySplitBy(cutter, out GeoPolygon2[] a, out _),
                square.TrySplitBy(cutter, out GeoPolygon2[] b, out _, global));
            Assert.Equal(a[0].Area, b[0].Area, 9);

            Assert.Equal(
                face.TrySplitBy(cutter, out GeoFace2[] c, out _),
                face.TrySplitBy(cutter, out GeoFace2[] d, out _, global));
            Assert.Equal(c[0].Area, d[0].Area, 9);

            Assert.Equal(
                square.TrySplitBy(cutLine, out GeoPolygon2[] e),
                square.TrySplitBy(cutLine, out GeoPolygon2[] f, global));
            Assert.Equal(e.Length, f.Length);

            Assert.Throws<ArgumentNullException>(() => Splition2.TrySplitBy((GeoPolygon2)null, cutter, out _, out _));
            Assert.Throws<ArgumentNullException>(() => Splition2.TrySplitBy((GeoFace2)null, cutter, out _, out _));
            Assert.Throws<ArgumentNullException>(() => Splition2.TrySplitBy(square, (GeoPolyline2)null, out _));
        }
    }
}
