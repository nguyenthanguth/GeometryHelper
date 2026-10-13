using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The curved offset joins an opening corner as the straight one does: a chamfer is the segment at |d| from the
    /// vertex, square to the bisector; a mitre past the limit is squared off at limit times |d|; and a corner a fraction of
    /// a degree off straight is mitred, not cut across.
    /// </summary>
    /// <remarks>
    /// <para>
    /// K3: <c>OffsetJoin.Chamfer</c> is "one segment, square to the corner's bisector at the offset distance". The curved
    /// path cut the chord between the moved ends instead, whose middle stands |d| cos(45 deg) from a square corner: the
    /// square of side 10 grown by 2 as a <c>GeoPolygonArc2</c> came back with the chord (10, -2) to (12, 0), 1.41421 from
    /// the corner, and an area of 188. The segment at 2 from (10, 0) runs from (8 + 2 sqrt 2, -2) to (12, 2 - 2 sqrt 2),
    /// and the area is 196 - 2 (4 - 2 sqrt 2)^2 = 193.25483, which the straight path already gives.
    /// </para>
    /// <para>
    /// K2: past the mitre limit the curved path cut the chord between the moved ends too, where <c>OffsetOptions</c> says
    /// the corner is cut off square at limit times |d|, as the straight path does. The triangle with a 10 degree tip at
    /// the origin, grown by 1: its mitre would reach 1 / sin 5 deg = 11.474 from the tip, past the default limit of 10. The
    /// square across the bisector at 10 from the tip runs from (-9.95071, -1) on the moved bottom to (-9.97318, -0.74311)
    /// on the moved closing side, 0.25787 long; the chord the pin cut, (-0.17365, 0.98481) to (0, -1), came within 0.087 of
    /// the tip. With the other two mitres, at (101.09131, -1) and (99.38186, 18.53913), the loop's area is 1 099.09470.
    /// </para>
    /// <para>
    /// And two straight pieces meeting at less than the angle tolerance of one degree were read as parallel, so they got
    /// no mitre. The loop (0, 0), (50, 0), (100, 0.698), (100, 50), (0, 50) grown by 80 turns 0.7998 degrees at (50, 0):
    /// its mitre is where y = -80 meets the second edge moved by 80, (50.55837, -80), and the chord the pin cut there
    /// passed 80 (1 / cos 0.4 deg - cos 0.4 deg) = 0.0039 from it.
    /// </para>
    /// </remarks>
    public class OffsetFaultS6JoinTests
    {
        private static readonly OffsetOptions Chamfer = new OffsetOptions(OffsetJoin.Chamfer);

        private static readonly double Root2 = Math.Sqrt(2.0);

        [Fact]
        public void TheSquareGrownByTwoWithChamferedCorners_IsCutAtTwoFromEveryCornerOnTheCurvedPath()
        {
            GeoPolygonArc2 square = Loop(Square10());

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(square, 2.0, Chamfer, Thousandth));

            Assert.Equal(196.0 - 2.0 * Math.Pow(4.0 - 2.0 * Root2, 2), one.Area, 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(8 + 2 * Root2, -2)) <= 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(12, 2 - 2 * Root2)) <= 1E-9);
        }

        [Fact]
        public void TheSquareGrownByTwoWithChamferedCorners_IsCutAtTwoFromEveryCornerOnTheStraightPath()
        {
            // Guard: the straight path already gives the segment at 2.
            var square = new GeoPolygon2(Square10());

            GeoPolygon2 one = Assert.Single(Offset2.Offset(square, 2.0, Chamfer, Thousandth));

            Assert.Equal(196.0 - 2.0 * Math.Pow(4.0 - 2.0 * Root2, 2), one.Area, 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(8 + 2 * Root2, -2)) <= 1E-9);
        }

        [Fact]
        public void AChainTurningLeftOffsetToTheRightWithAChamfer_IsCutAtTheDistanceFromTheCorner()
        {
            // The corner (10, 0) of the chain (0, 0), (10, 0), (10, 10) opens on the right: the moved legs y = -2 and x = 12
            // are joined by the segment from (8 + 2 sqrt 2, -2) to (12, 2 - 2 sqrt 2), its middle 2 from the corner.
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(10, 10) });

            GeoPolylineArc2 one = Assert.Single(Offset2.Offset(chain, -2.0, Chamfer, Thousandth));

            Assert.Equal(4, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(8 + 2 * Root2, -2)), 1E-9);
            Assert.Equal(0.0, one.Vertices[2].DistanceTo(P(12, 2 - 2 * Root2)), 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(12, 10)), 1E-9);
        }

        [Fact]
        public void ATriangleWithATenDegreeTipGrownByOne_IsSquaredOffAtTenFromTheTipOnTheCurvedPath()
        {
            GeoPolygonArc2 triangle = Loop(TenDegreeTip());

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(triangle, 1.0, OffsetOptions.Default, Thousandth));

            Assert.Equal(4, one.VertexCount);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(-9.95070971190755, -1.0)) <= 1E-6);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(-9.97318424992736, -0.743114854953158)) <= 1E-6);
            Assert.Equal(1099.09469562, one.Area, 1E-6);
        }

        [Fact]
        public void ATriangleWithATenDegreeTipGrownByOne_IsSquaredOffAtTenFromTheTipOnTheStraightPath()
        {
            // Guard: the straight path, through Clipper, already squares off there.
            var triangle = new GeoPolygon2(TenDegreeTip());

            GeoPolygon2 one = Assert.Single(Offset2.Offset(triangle, 1.0, OffsetOptions.Default, Thousandth));

            Assert.Contains(one.Vertices, v => v.DistanceTo(P(-9.95070971190755, -1.0)) <= 1E-6);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(-9.97318424992736, -0.743114854953158)) <= 1E-6);
        }

        [Fact]
        public void ALoopTurningEightTenthsOfADegreeGrownByEighty_IsMitredThere()
        {
            GeoPolygonArc2 loop = Loop(new[] { P(0, 0), P(50, 0), P(100, 0.698), P(100, 50), P(0, 50) });

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(loop, 80.0, OffsetOptions.Default, Thousandth));

            Assert.Contains(one.Vertices, v => v.DistanceTo(P(50.55837279717891, -80.0)) <= 1E-6);
        }

        [Fact]
        public void TheSameLoopOnTheStraightPath_IsMitredThere()
        {
            // Guard: Clipper gives the mitre.
            var loop = new GeoPolygon2(P(0, 0), P(50, 0), P(100, 0.698), P(100, 50), P(0, 50));

            GeoPolygon2 one = Assert.Single(Offset2.Offset(loop, 80.0, OffsetOptions.Default, Thousandth));

            Assert.Contains(one.Vertices, v => v.DistanceTo(P(50.55837279717891, -80.0)) <= 1E-6);
        }

        [Fact]
        public void ACurvedLoopWithRoundJoins_StaysRoundAtTheDistance()
        {
            // Guard: the round join was right on the curved path. The square of side 10 grown by 2 with round joins is four
            // sides and four quarter circles of 2: area 100 + 80 + 4 pi = 192.56637.
            GeoPolygonArc2 square = Loop(Square10());

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(square, 2.0, new OffsetOptions(OffsetJoin.Round), Thousandth));

            Assert.Equal(180.0 + 4.0 * Math.PI, one.Area, 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(square), 2.0) <= 1E-9);
        }
    }
}
