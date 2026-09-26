using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// The two halves of corner work that each side was missing. The plane could cut a corner off a straight
    /// chain but not round one; space could round one but not cut it.
    /// </summary>
    /// <remarks>
    /// Neither gap was a decision. Rounding a straight chain in the plane is the curved chain's own rounding
    /// with every bulge at nought, and cutting a corner in space needs no plane at all — it moves back along one
    /// leg and forward along the other, so both points land on the legs themselves.
    /// </remarks>
    public class CornerGapTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A hundred square, in the plane.</summary>
        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

        /// <summary>An L in the plane: along x to (400, 0), then up to (400, 200).</summary>
        private static GeoPolyline2 Bent() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(400, 0), new GeoPoint2(400, 200));

        /// <summary>A chain in space that lies in no one plane: along x, then y, then z.</summary>
        private static GeoPolyline3 Crooked() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0), new GeoPoint3(400, 200, 300));

        #region Rounding the straight types in the plane

        [Fact]
        public void AStraightChainInThePlaneCanBeRounded()
        {
            GeoPolyline2 bent = Bent();
            GeoPolylineArc2 rounded = bent.Fillet(50.0);

            // One corner, so one arc, and the chain is shorter than the set-out it was rounded from.
            Assert.Single(rounded.GetEdges(), edge => edge.IsArc);
            Assert.True(rounded.Length < bent.Length);
            Assert.Equal(50.0, rounded.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // The ends are where they were: rounding takes material off the middle, not off the ends.
            Assert.True(rounded[0].IsEqualTo(bent[0], Loose));
            Assert.True(rounded[rounded.VertexCount - 1].IsEqualTo(bent[bent.VertexCount - 1], Loose));
        }

        [Fact]
        public void AStraightLoopInThePlaneCanBeRounded()
        {
            GeoPolygon2 square = Square();
            GeoPolygonArc2 rounded = square.Fillet(30.0);

            // Every vertex of a loop is a corner, so all four are rounded.
            Assert.Equal(4, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.True(rounded.Area < square.Area);

            // A square of side a hundred with thirty off each corner: the area lost is what four quarter-circles
            // leave behind in four thirty-squares.
            double lost = 4.0 * (30.0 * 30.0 - Math.PI * 30.0 * 30.0 / 4.0);

            Assert.Equal(square.Area - lost, rounded.Area, 6);

            // A radius the corner has no room for is left alone, and the loop comes back square.
            Assert.Equal(square.Area, square.Fillet(500.0).Area, 6);
        }

        [Fact]
        public void OneCornerAtATimeInThePlane()
        {
            GeoPolyline2 bent = Bent();

            Assert.True(bent.TryFilletAt(1, 50.0, out GeoPolylineArc2 one));
            Assert.Single(one.GetEdges(), edge => edge.IsArc);

            // The ends of a chain are not corners: nothing turns there.
            Assert.False(bent.TryFilletAt(0, 50.0, out GeoPolylineArc2 atStart));
            Assert.Equal(bent.Length, atStart.Length, 6);
            Assert.False(bent.TryFilletAt(2, 50.0, out _));

            // Every vertex of a loop is one, the last included.
            GeoPolygon2 square = Square();

            Assert.True(square.TryFilletAt(0, 20.0, out GeoPolygonArc2 first));
            Assert.True(square.TryFilletAt(3, 20.0, out GeoPolygonArc2 last));
            Assert.Single(first.GetEdges(), edge => edge.IsArc);
            Assert.Single(last.GetEdges(), edge => edge.IsArc);
            Assert.Equal(first.Area, last.Area, 6);

            Assert.Throws<ArgumentOutOfRangeException>(() => square.TryFilletAt(4, 20.0, out _));
        }

        [Fact]
        public void RoundingTheStraightTypesGivesTheSameAnswerAsRoundingTheCurvedOnes()
        {
            // The straight chain is read as a curved one with every bulge at nought, so the two must agree.
            GeoPolyline2 bent = Bent();

            Assert.Equal(new GeoPolylineArc2(bent).Fillet(50.0).Length, bent.Fillet(50.0).Length, 9);
            Assert.Equal(new GeoPolygonArc2(Square()).Fillet(30.0).Area, Square().Fillet(30.0).Area, 9);

            // A radius each, nought leaving that corner square.
            GeoPolygonArc2 some = Square().Fillet(new[] { 20.0, 0.0, 20.0, 0.0 });

            Assert.Equal(2, some.GetEdges().Count(edge => edge.IsArc));

            Assert.Throws<ArgumentNullException>(() => Corner2.Fillet((GeoPolyline2)null, 10.0));
            Assert.Throws<ArgumentNullException>(() => Corner2.Fillet((GeoPolygon2)null, 10.0));
            Assert.Throws<ArgumentNullException>(() => Corner2.TryFilletAt((GeoPolygon2)null, 0, 10.0, out _));
        }

        #endregion

        #region Cutting corners in space

        [Fact]
        public void ACornerInSpaceCanBeCutWithoutAnyPlaneToDoItIn()
        {
            GeoPolyline3 crooked = Crooked();

            // The chain turns twice and lies in no one plane; both corners are cut all the same.
            Assert.False(crooked.IsPlanar());

            GeoPolyline3 cut = crooked.Chamfer(50.0);

            Assert.Equal(crooked.VertexCount + 2, cut.VertexCount);
            Assert.True(cut.Length < crooked.Length);

            // Each cut point sits exactly fifty from the corner it replaced, along the leg it belongs to.
            Assert.True(cut[1].IsEqualTo(new GeoPoint3(350, 0, 0), Loose));
            Assert.True(cut[2].IsEqualTo(new GeoPoint3(400, 50, 0), Loose));
            Assert.True(cut[3].IsEqualTo(new GeoPoint3(400, 150, 0), Loose));
            Assert.True(cut[4].IsEqualTo(new GeoPoint3(400, 200, 50), Loose));

            // The ends are untouched.
            Assert.True(cut.StartPoint.IsEqualTo(crooked.StartPoint, Loose));
            Assert.True(cut.EndPoint.IsEqualTo(crooked.EndPoint, Loose));
        }

        [Fact]
        public void TwoDistancesCutTheCornerLopsided()
        {
            GeoPolyline3 crooked = Crooked();
            GeoPolyline3 cut = crooked.Chamfer(80.0, 20.0);

            Assert.True(cut[1].IsEqualTo(new GeoPoint3(320, 0, 0), Loose));
            Assert.True(cut[2].IsEqualTo(new GeoPoint3(400, 20, 0), Loose));

            // One corner at a time, and the ends are not corners.
            Assert.True(crooked.TryChamferAt(1, 50.0, 50.0, out GeoPolyline3 one));
            Assert.Equal(crooked.VertexCount + 1, one.VertexCount);

            Assert.False(crooked.TryChamferAt(0, 50.0, 50.0, out GeoPolyline3 atStart));
            Assert.Equal(crooked.VertexCount, atStart.VertexCount);
            Assert.False(crooked.TryChamferAt(crooked.VertexCount - 1, 50.0, 50.0, out _));

            Assert.Throws<ArgumentOutOfRangeException>(() => crooked.TryChamferAt(99, 50.0, 50.0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => crooked.Chamfer(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => crooked.Chamfer(-5.0));
            Assert.Throws<ArgumentNullException>(() => Corner3.Chamfer((GeoPolyline3)null, 10.0));
        }

        [Fact]
        public void ACornerThatDoesNotTurnAndALegTooShortAreBothLeftAlone()
        {
            // Three points on one line: nothing turns at the middle one.
            var straight = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(300, 0, 0));

            Assert.Equal(straight.VertexCount, straight.Chamfer(10.0).VertexCount);
            Assert.False(straight.TryChamferAt(1, 10.0, 10.0, out _));

            // A leg of fifty cannot give eighty away.
            var tight = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0), new GeoPoint3(50, 50, 0));

            Assert.False(tight.TryChamferAt(1, 80.0, 10.0, out _));
            Assert.Equal(tight.VertexCount, tight.Chamfer(80.0).VertexCount);
        }

        [Fact]
        public void TwoCornersWantingTheSameLegAreSettledTheWayThePlaneSettlesThem()
        {
            var loop = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
            var flat = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

            // Room to spare: a corner off each, eight vertices, and half a twenty-square gone four times over.
            Assert.Equal(8, loop.Chamfer(20.0).VertexCount);
            Assert.Equal(100 * 100 - 4 * 0.5 * 20 * 20, loop.Chamfer(20.0).Area, 6);

            // Exactly enough is enough, and what comes back is a diamond: at fifty the two cuts sharing a leg
            // meet in its middle, so the eight points are four and the square has lost half its area.
            GeoPolygon3 exactly = loop.Chamfer(50.0);

            Assert.Equal(4, exactly.VertexCount);
            Assert.Equal(100 * 100 / 2.0, exactly.Area, 6);

            // Sixty is more than a leg can give, so corners give way rather than the shape folding over.
            GeoPolygon3 tooMuch = loop.Chamfer(60.0);

            Assert.True(tooMuch.VertexCount < 8);
            Assert.True(tooMuch.Area > exactly.Area);

            // The plane settles it the same way at every distance, which is the point: the rule carried over
            // untouched because it is about lengths along edges and not about dimensions.
            foreach (double distance in new[] { 20.0, 50.0, 60.0 })
            {
                Assert.Equal(flat.Chamfer(distance).VertexCount, loop.Chamfer(distance).VertexCount);
                Assert.Equal(flat.Chamfer(distance).Area, loop.Chamfer(distance).Area, 6);
            }

            // And the loop stays flat, in its own plane, whatever is cut.
            GeoPlane3 plane = loop.GetPlane();

            Assert.All(exactly.Vertices, vertex => Assert.True(plane.IsPointOn(vertex, Loose)));
            Assert.All(tooMuch.Vertices, vertex => Assert.True(plane.IsPointOn(vertex, Loose)));
        }

        [Fact]
        public void ALoopInATiltedPlaneStaysInItWhenCutAndWhenRounded()
        {
            // A square standing on edge, turned out of every axis plane.
            var tilted = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(100, 100, 100), new GeoPoint3(0, 0, 100));

            GeoPlane3 plane = tilted.GetPlane();

            GeoPolygon3 cut = tilted.Chamfer(20.0);

            Assert.Equal(8, cut.VertexCount);
            Assert.True(cut.Area < tilted.Area);
            Assert.All(cut.Vertices, vertex => Assert.True(plane.IsPointOn(vertex, Loose)));

            GeoPolygonArc3 rounded = tilted.Fillet(20.0);

            Assert.Equal(4, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.True(rounded.Area < tilted.Area);
            Assert.All(rounded.Vertices, vertex => Assert.True(plane.IsPointOn(vertex, Loose)));

            Assert.True(tilted.TryFilletAt(0, 20.0, out GeoPolygonArc3 one));
            Assert.Single(one.GetEdges(), edge => edge.IsArc);

            Assert.True(tilted.TryChamferAt(2, 20.0, 20.0, out GeoPolygon3 oneCut));
            Assert.Equal(tilted.VertexCount + 1, oneCut.VertexCount);
        }

        [Fact]
        public void ACurvedChainKeepsItsArcsAndCutsOnlyTheCornersBetweenStraightLegs()
        {
            // A bar: two straight legs with a fifty radius at the bend, then a third leg out of plane.
            GeoPolylineArc3 bar = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 400, 0), new GeoPoint3(400, 400, 400))
                .Fillet(50.0);

            int arcsBefore = bar.GetEdges().Count(edge => edge.IsArc);

            Assert.Equal(2, arcsBefore);

            GeoPolylineArc3 cut = bar.Chamfer(30.0);

            // Every arc survives with its radius: the corners either side of an arc are left alone, because a
            // leg that curves leaves at a tangent.
            Assert.Equal(arcsBefore, cut.GetEdges().Count(edge => edge.IsArc));
            Assert.All(cut.GetEdges().Where(edge => edge.IsArc), edge => Assert.Equal(50.0, edge.ToArc().Radius, 6));

            // With both corners carrying an arc, there is nothing left to cut and the chain comes back as it was.
            Assert.Equal(bar.Length, cut.Length, 6);

            Assert.False(bar.TryChamferAt(1, 30.0, 30.0, out _));
            Assert.Throws<ArgumentNullException>(() => Corner3.Chamfer((GeoPolylineArc3)null, 10.0));
        }

        [Fact]
        public void ACurvedChainWithAStraightCornerLeftInItHasThatOneCut()
        {
            // Round the first bend only, so the second corner still meets two straight legs.
            GeoPolyline3 setOut = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 400, 0), new GeoPoint3(400, 400, 400));

            Assert.True(setOut.TryFilletAt(1, 50.0, out GeoPolylineArc3 half));
            Assert.Single(half.GetEdges(), edge => edge.IsArc);

            GeoPolylineArc3 cut = half.Chamfer(30.0);

            // The arc is untouched and the straight corner has gained a cut, so the chain is shorter.
            Assert.Single(cut.GetEdges(), edge => edge.IsArc);
            Assert.Equal(half.EdgeCount + 1, cut.EdgeCount);
            Assert.True(cut.Length < half.Length);

            Assert.True(half.TryChamferAt(half.VertexCount - 2, 30.0, 30.0, out GeoPolylineArc3 one));
            Assert.Equal(cut.Length, one.Length, 6);
        }

        #endregion

        #region The plane and space agreeing

        [Fact]
        public void CuttingACornerInSpaceAgreesWithCuttingItInThePlane()
        {
            GeoPolyline2 flat = Bent();
            var lifted = new GeoPolyline3(flat.Vertices.Select(v => new GeoPoint3(v.X, v.Y, 0)));

            GeoPolyline2 inThePlane = flat.Chamfer(60.0, 20.0);
            GeoPolyline3 inSpace = lifted.Chamfer(60.0, 20.0);

            Assert.Equal(inThePlane.VertexCount, inSpace.VertexCount);
            Assert.Equal(inThePlane.Length, inSpace.Length, 6);

            for (int i = 0; i < inThePlane.VertexCount; i++)
            {
                Assert.Equal(inThePlane[i].X, inSpace[i].X, 6);
                Assert.Equal(inThePlane[i].Y, inSpace[i].Y, 6);
                Assert.Equal(0.0, inSpace[i].Z, 9);
            }
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolyline2 bent = Bent();
            GeoPolygon2 square = Square();
            GeoPolyline3 crooked = Crooked();
            var loop = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

            Assert.Equal(bent.Fillet(50.0).Length, bent.Fillet(50.0, global).Length, 9);
            Assert.Equal(square.Fillet(30.0).Area, square.Fillet(30.0, global).Area, 9);
            Assert.Equal(crooked.Chamfer(50.0).Length, crooked.Chamfer(50.0, global).Length, 9);
            Assert.Equal(crooked.Chamfer(50.0, 20.0).Length, crooked.Chamfer(50.0, 20.0, global).Length, 9);
            Assert.Equal(loop.Chamfer(20.0).Area, loop.Chamfer(20.0, global).Area, 9);
            Assert.Equal(loop.Fillet(20.0).Area, loop.Fillet(20.0, global).Area, 9);

            Assert.Equal(
                bent.TryFilletAt(1, 50.0, out GeoPolylineArc2 a),
                bent.TryFilletAt(1, 50.0, out GeoPolylineArc2 b, global));
            Assert.Equal(a.Length, b.Length, 9);

            Assert.Equal(
                crooked.TryChamferAt(1, 50.0, 50.0, out GeoPolyline3 c),
                crooked.TryChamferAt(1, 50.0, 50.0, out GeoPolyline3 d, global));
            Assert.Equal(c.Length, d.Length, 9);

            Assert.Throws<ArgumentNullException>(() => Corner3.Chamfer((GeoPolygon3)null, 10.0));
            Assert.Throws<ArgumentNullException>(() => Corner3.Fillet((GeoPolygon3)null, 10.0));
            Assert.Throws<ArgumentNullException>(() => Corner3.TryChamferAt((GeoPolygon3)null, 0, 10.0, 10.0, out _));
            Assert.Throws<ArgumentNullException>(() => Corner3.TryFilletAt((GeoPolygon3)null, 0, 10.0, out _));
        }

        #endregion
    }
}
