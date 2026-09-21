using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Cutting corners square, as AutoCAD's CHAMFER does: what the result looks like, which corners are
    /// left alone and why, and that the answer does not depend on where the walk round the shape began.
    /// </summary>
    public class ChamferTests
    {
        private static GeoPolygon2 Rectangle(double width = 100, double height = 60) => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(width, 0), new GeoPoint2(width, height), new GeoPoint2(0, height));

        [Fact]
        public void ARectangleChamferedAtEveryCornerComesBackAnOctagon()
        {
            GeoPolygon2 cut = Rectangle().Chamfer(10.0);

            Assert.Equal(8, cut.VertexCount);

            // Each corner loses a right-angled triangle of legs 10.
            Assert.Equal(100.0 * 60.0 - 4.0 * 0.5 * 10.0 * 10.0, cut.Area, 9);

            // The cut points sit exactly 10 along each edge from the corner they replaced.
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(10, 0)));
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(0, 10)));
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(90, 0)));
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(100, 50)));
        }

        [Fact]
        public void TheTwoDistancesFollowTheWayTheShapeRuns()
        {
            // Running counter-clockwise from the origin: at the corner (100, 0) the edge coming in runs
            // along X and the edge going out runs up Y.
            GeoPolygon2 cut = Rectangle().Chamfer(30.0, 5.0);

            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(70, 0)));
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(100, 5)));

            // Reversed, the same two numbers swap over.
            var reversed = new GeoPolygon2(Rectangle().Vertices.Reverse().ToArray());
            GeoPolygon2 other = reversed.Chamfer(30.0, 5.0);

            Assert.Contains(other.Vertices, v => v.IsEqualTo(new GeoPoint2(95, 0)));
            Assert.Contains(other.Vertices, v => v.IsEqualTo(new GeoPoint2(100, 30)));
        }

        [Fact]
        public void AChainKeepsItsEndPointsAndCutsOnlyWhereItTurns()
        {
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(50, 0), new GeoPoint2(50, 40), new GeoPoint2(90, 40));

            GeoPolyline2 cut = chain.Chamfer(10.0);

            // Two interior corners, each becoming two points: 4 + 2 = 6.
            Assert.Equal(6, cut.VertexCount);
            Assert.True(cut[0].IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(cut[cut.VertexCount - 1].IsEqualTo(new GeoPoint2(90, 40)));
            Assert.True(cut[1].IsEqualTo(new GeoPoint2(40, 0)));
            Assert.True(cut[2].IsEqualTo(new GeoPoint2(50, 10)));
        }

        [Fact]
        public void ACornerWithTooLittleEdgeIsLeftAlone()
        {
            // The short edge is 8 long, so a cut of 10 cannot be measured along it.
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(50, 0), new GeoPoint2(50, 8), new GeoPoint2(100, 8));

            GeoPolyline2 cut = chain.Chamfer(10.0);

            // Neither corner has room, so the chain comes back as it was.
            Assert.Equal(4, cut.VertexCount);
            Assert.True(cut.IsEqualTo(chain));
        }

        [Fact]
        public void TwoNeighboursCannotBothEatMoreThanTheEdgeBetweenThemHasToGive()
        {
            // The middle edge is 30 long; two cuts of 20 would need 40 of it.
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(60, 0), new GeoPoint2(60, 30), new GeoPoint2(0, 30),
                new GeoPoint2(0, 90));

            GeoPolyline2 cut = chain.Chamfer(20.0);

            // Three interior corners ask for a cut. The two sharing the 30-long edge would need 40 of it,
            // so one of them is dropped - not both - and the other two are cut: 5 + 2 = 7 vertices.
            Assert.Equal(7, cut.VertexCount);

            // The dropped one is still there, uncut, exactly where it was.
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(60, 0)));

            // And the two that survived really were cut.
            Assert.DoesNotContain(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(60, 30)));
            Assert.DoesNotContain(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(0, 30)));
        }

        [Fact]
        public void AStraightCornerHasNothingToCutOff()
        {
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(50, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 50));

            GeoPolyline2 cut = chain.Chamfer(10.0);

            // The middle vertex lies on a straight run, so only the real corner is cut: 4 + 1 = 5.
            Assert.Equal(5, cut.VertexCount);
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(50, 0)));
        }

        [Fact]
        public void TheAnswerDoesNotDependOnWhereTheWalkBegan()
        {
            // A shape with corners that compete for the same edges, so the dropping rule has work to do.
            var shape = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(40, 0), new GeoPoint2(55, 0), new GeoPoint2(70, 0),
                new GeoPoint2(70, 50), new GeoPoint2(0, 50));

            GeoPolygon2 first = shape.Chamfer(12.0);

            // The same polygon, started at a different vertex: the cut must land in the same places.
            var rotated = new GeoPolygon2(shape.Vertices.Skip(2).Concat(shape.Vertices.Take(2)).ToArray());
            GeoPolygon2 second = rotated.Chamfer(12.0);

            Assert.Equal(first.VertexCount, second.VertexCount);
            Assert.True(first.IsEqualTo(second));
        }

        [Fact]
        public void EveryCutIsMeasuredOnTheShapeAsItCameIn()
        {
            // Corners 12 apart along one edge, cut 5 each: both fit on the original edge (5 + 5 <= 12),
            // and neither measurement is taken on an edge the other has already shortened.
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(0, 20), new GeoPoint2(12, 20), new GeoPoint2(12, 0));

            GeoPolyline2 cut = chain.Chamfer(5.0);

            Assert.Equal(6, cut.VertexCount);
            Assert.True(cut[1].IsEqualTo(new GeoPoint2(0, 15)));
            Assert.True(cut[2].IsEqualTo(new GeoPoint2(5, 20)));
            Assert.True(cut[3].IsEqualTo(new GeoPoint2(7, 20)));
            Assert.True(cut[4].IsEqualTo(new GeoPoint2(12, 15)));
        }

        [Fact]
        public void OneCornerAtATimeIsPreciseAndSaysNoWhenItCannot()
        {
            GeoPolygon2 rectangle = Rectangle();

            Assert.True(rectangle.TryChamferAt(1, 10.0, 20.0, out GeoPolygon2 cut));
            Assert.Equal(5, cut.VertexCount);
            Assert.True(cut[1].IsEqualTo(new GeoPoint2(90, 0)));
            Assert.True(cut[2].IsEqualTo(new GeoPoint2(100, 20)));

            // Longer than the edge it would be measured along.
            Assert.False(rectangle.TryChamferAt(1, 10.0, 200.0, out GeoPolygon2 refused));
            Assert.True(refused.IsEqualTo(rectangle));

            // A chain has no corner at either end.
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            Assert.False(chain.TryChamferAt(0, 2.0, 2.0, out _));
            Assert.False(chain.TryChamferAt(2, 2.0, 2.0, out _));
            Assert.True(chain.TryChamferAt(1, 2.0, 2.0, out _));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            GeoPolygon2 rectangle = Rectangle();

            Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.Chamfer(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.Chamfer(-5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.Chamfer(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.TryChamferAt(9, 5.0, 5.0, out _));
            Assert.Throws<ArgumentNullException>(() => Corner2.Chamfer((GeoPolygon2)null, 5.0));
            Assert.Throws<ArgumentNullException>(() => Corner2.Chamfer((GeoPolyline2)null, 5.0));
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgree()
        {
            GeoPolygon2 rectangle = Rectangle();

            Assert.True(Corner2.Chamfer(rectangle, 8.0).IsEqualTo(rectangle.Chamfer(8.0)));
            Assert.True(Corner2.Chamfer(rectangle, 8.0, 4.0).IsEqualTo(rectangle.Chamfer(8.0, 4.0)));
            Assert.True(Corner2.Chamfer(rectangle, 8.0, Tolerance.Global).IsEqualTo(rectangle.Chamfer(8.0, Tolerance.Global)));
        }

        [Fact]
        public void ChamferingStaysInTheStraightWorld()
        {
            // The whole point of the type it returns: the result goes straight on into the region
            // operations, with no flattening and no tolerance to choose.
            GeoPolygon2 plate = Rectangle(200, 120).Chamfer(15.0);
            var opening = new GeoPolygon2(
                new GeoPoint2(50, 40), new GeoPoint2(80, 40), new GeoPoint2(80, 70), new GeoPoint2(50, 70));

            GeoFace2[] pierced = Boolean2.Subtract(plate, opening);

            Assert.Single(pierced);
            Assert.Equal(plate.Area - 900.0, pierced[0].Area, 6);
        }
        [Fact]
        public void AChamferThatFitsExactlyIsNotLostToRounding()
        {
            // A square of side one hundred, turned so that measuring a side gives 99.999999999999986 rather
            // than one hundred. Chamfering fifty at every corner takes exactly the whole of every side, and
            // that dust must not make it look like there is too little.
            const double turn = Math.PI / 10.0;

            var along = new GeoVector2(Math.Cos(turn) * 100.0, Math.Sin(turn) * 100.0);
            var across = new GeoVector2(-Math.Sin(turn) * 100.0, Math.Cos(turn) * 100.0);

            var corner = new GeoPoint2(0, 0);
            var square = new GeoPolygon2(corner, corner.Add(along), corner.Add(along).Add(across), corner.Add(across));

            Assert.Equal(100.0, square[0].DistanceTo(square[1]), 9);

            GeoPolygon2 cut = square.Chamfer(50.0);

            // Every corner cut and every side gone: what is left is the diamond through the four midpoints.
            Assert.Equal(4, cut.VertexCount);
            Assert.Equal(5000.0, cut.Area, 6);
            Assert.Equal(4.0 * Math.Sqrt(5000.0), cut.Length, 6);
        }
    }
}
