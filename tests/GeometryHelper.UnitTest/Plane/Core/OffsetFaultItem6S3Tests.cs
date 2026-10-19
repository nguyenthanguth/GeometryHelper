using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Item 6, step S3: curved offsets that come back empty, lose a loop or grow a spike, read at 9ee3f57.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stub beside a join: a stretch of a moved piece lying inside another corner's miter or square cut by less than the
    /// slack is kept, since a stretch is judged at its middle and only a point further inside than the slack is in the join.
    /// Kept, a stub longer than the tolerance leaves its run open by its own length, and the run is dropped: a slot just
    /// wider than the distance, and the audit's GA 22422, 10570 and 1094, come back empty.
    /// </para>
    /// <para>
    /// A needle thinner than the tolerance: its tip turns back within the tolerance, two straight edges folding back are read
    /// as a corner that closes, the tip is tied by a chord that the pruning throws away, and the run is open by twice the
    /// distance there.
    /// </para>
    /// <para>
    /// A run-on crossing its neighbour: a piece run on to a miter can cross the piece after its neighbour's other end, away
    /// from the corner the two share, and the crossings of neighbouring pieces are not looked for on the simple path. The
    /// loop a miter should pinch off is dropped.
    /// </para>
    /// <para>
    /// A reversed sliver: a corner whose pieces cross within the tolerance past the end of the piece after it cuts that piece
    /// back to a sliver running the wrong way, its bulge lost. At the next corner the near miter is refused, and the cut square
    /// meets nothing short of the far crossing of the two circles, which the unlimited meeting then takes: a spike 35 from the
    /// corner where the limit is 3.
    /// </para>
    /// </remarks>
    public class OffsetFaultItem6S3Tests
    {
        private static readonly OffsetOptions Round = new OffsetOptions(OffsetJoin.Round);

        private static readonly OffsetOptions Miter = new OffsetOptions(OffsetJoin.Miter);

        private static readonly OffsetOptions Chamfer = new OffsetOptions(OffsetJoin.Chamfer);

        /// <summary>The same loop through the straight path: its vertices as a polygon, every edge of it straight.</summary>
        private static double StraightArea(GeoPolygonArc2 loop, double distance, OffsetOptions options, Tolerance tolerance)
        {
            GeoPolygon2[] loops = Offset2.Offset(new GeoPolygon2(loop.Vertices.ToList()), distance, options, tolerance);
            bool clockwise = loop.IsClockwise;
            return loops.Sum(p => p.IsClockwise == clockwise ? p.Area : -p.Area);
        }

        [Theory]
        [InlineData(0.12, 1E-3)]
        [InlineData(0.038, 1E-4)]
        public void ASlotJustWiderThanTheDistanceGrownWithMiters_FillsAndTheRectangleGrowsSquare(double past, double point)
        {
            // Grown by 5 the slot fills, and the rectangle 40 by 30 comes back 50 by 40 with mitered corners: 2 000. The left
            // mouth's miter corner (5, 5) stands sqrt(past^2 + 25) from the right mouth (5 + past, 0), so the moved left wall
            // stands the distance or more from the right mouth for a stub of about past^2 / 10: 0.00144 at 0.12, more than
            // the tolerance 1E-3 and at its middle less than the slack 1E-3 inside the right mouth's miter; 1.44E-4 at 0.038
            // against 1E-4. At 9ee3f57 the stubs leave the loop open by their length, and the answer is empty.
            var tolerance = new Tolerance(point, point * 1E-2);
            GeoPolygonArc2 slotted = RectangleWithASlot(5.0 + past);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(slotted, 5.0, Miter, tolerance));

            Assert.Equal(2000.0, grown.Area, 1E-6);
        }

        [Theory]
        [InlineData(0.05)]
        [InlineData(0.3)]
        public void ASlotFurtherFromTheBandGrownWithMiters_FillsAndTheRectangleGrowsSquare(double past)
        {
            // The stub is under the tolerance at 0.05 (2.5E-4) and well inside the miter at 0.3 (0.009): both come back right.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(RectangleWithASlot(5.0 + past), 5.0, Miter, Thousandth));

            Assert.Equal(2000.0, grown.Area, 1E-6);
        }

        [Fact]
        public void TheAuditCombGrownWithChamfers_IsTheStraightPathsLoop()
        {
            // GA 10570 is straight all round, so the two paths draw the same chamfers: 81.900658. At 9ee3f57 two stubs 0.0022
            // long beside the square cuts leave the run open and the answer is empty.
            GeoPolygonArc2 comb = AuditComb10570();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(comb, 2.5, Chamfer, Thousandth));

            Assert.Equal(StraightArea(comb, 2.5, Chamfer, Thousandth), grown.Area, 1E-6);
        }

        [Fact]
        public void TheAuditNotchGrownByEightyWithAMiterLimitOfTwo_IsOneLoopRoundTheRoundOne()
        {
            // GA 1094: the side moved 80 and the notch's bottom moved 80 cross twice, 26.6 apart, running all but along each
            // other, and the side stands 0.0015 to 0 inside the bottom's miter for 0.24 before the first crossing, its middle
            // 0.00074 inside, under the slack. Kept, that stretch leaves three open runs and the answer empty. Grown, a miter
            // holds everything a round join does: the round answer encloses 54 303.27.
            GeoPolygonArc2 notch = AuditNotch1094();
            double round = Assert.Single(Offset2.Offset(notch, 80.0, Round, Thousandth)).Area;

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(notch, 80.0, new OffsetOptions(OffsetJoin.Miter, 2.0), Thousandth));

            Assert.True(grown.Area >= round);
        }

        [Fact]
        public void ASquareWithANeedleThinnerThanTheToleranceGrownByAThird_IsOneLoop()
        {
            // At 1E-3 the needle, 0.0005 wide, turns back within the tolerance at its tip (sine x reach 0.0005). Either reading
            // is allowed there: the needle dropped, the square of side 10 grown by 0.3, 100 + 12 + 0.09 pi = 112.282743; or the
            // needle kept, as at 1E-4, 115.845469. At 9ee3f57 the tip is tied by a chord that goes, the run is open by 0.6, and
            // the answer is empty.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithANeedle(), 0.3, Round, Thousandth));

            Assert.InRange(grown.Area, 112.282743 - 1E-3, 115.845469 + 1E-3);
        }

        [Fact]
        public void TheSameNeedleAtATenThousandth_GrowsRoundItsTip()
        {
            // At 1E-4 the needle is simple and its tip opens: one loop of 115.845469, as the straight path gives (115.845450,
            // its round joins cut within the default share).
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithANeedle(), 0.3, Round, TenThousandth));

            Assert.Equal(115.845469, grown.Area, 1E-5);
        }

        [Theory]
        [InlineData(-0.3, 11.4715)]
        [InlineData(-0.2, 13.0463)]
        public void ALoopPinchedBesideAMiterShrunk_KeepsBothLoops(double distance, double pinched)
        {
            // Shrunk by 0.3 the loop pinches in two beside (2, 1); by 0.2 the miter there pinches it, where the round join
            // does not. The straight path keeps both loops: 11.471412 and 5.954356, and 13.046518 and 7.597693. At 9ee3f57 the
            // second arc's offset is never cut where it crosses the first arc's run-on, and the loop beyond goes: one loop,
            // 5.954407 and 7.597749.
            GeoPolygonArc2[] shrunk = Offset2.Offset(LoopPinchedBesideAMiter(), distance, Miter, Thousandth);

            Assert.Equal(2, shrunk.Length);
            Assert.Contains(shrunk, loop => Math.Abs(loop.Area - pinched) <= 1E-3);
        }

        [Fact]
        public void ALoopPinchedBesideAMiterShrunkWithRoundJoins_KeepsBothLoops()
        {
            // The round join is a piece of its own between the two arcs' offsets, so their crossings with it are found: 5.954407
            // and 11.471640, as the straight path gives (5.954356 and 11.471536).
            GeoPolygonArc2[] shrunk = Offset2.Offset(LoopPinchedBesideAMiter(), -0.3, Round, Thousandth);

            Assert.Equal(2, shrunk.Length);
            Assert.Contains(shrunk, loop => Math.Abs(loop.Area - 11.47164) <= 1E-5);
        }

        [Fact]
        public void ALoopWithASliverBeforeATipGrownWithMiters_StaysWithinTheLimit()
        {
            // The tip at (3.4164, -7.0194) is 22.6 degrees, mitered 1.546 from it, under the limit of 3; the round answer
            // encloses 52.43 and the straight path's mitered one 54.29. At 9ee3f57 the piece before the tip is a reversed
            // sliver 1.94E-4 long, and the miter is taken 35.14 out: 5 945.26.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(LoopWithASliverBeforeATip(), 0.3, Miter, Thousandth));

            Assert.InRange(grown.Area, 52.43, 55.0);
        }

        [Fact]
        public void TheSameLoopAtATenThousandth_IsMiteredAtTheTip()
        {
            // At 1E-4 the crossing past the end of the piece is further than the tolerance, the piece keeps its arc, and the
            // tip is mitered 1.546 out: 54.769871.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(LoopWithASliverBeforeATip(), 0.3, Miter, TenThousandth));

            Assert.Equal(54.769871, grown.Area, 1E-5);
        }

        [Fact]
        public void ACornerCutSquareAtTheLimit_HasEndsFurtherThanTheLimitFromIt()
        {
            // Not a fault: grown by 2 with miters, the corner (0, 10), 15.26 degrees between the left side and the notch's arc,
            // is cut square 20 out along its bisector, the arc run on round its circle, and the cut's end (4.714, 30.810)
            // stands 21.34 from the corner. Every point of the miter beyond the round join lies inside the documented wedges;
            // the straight path runs the arc on along its tangent and gives 196.481242 on the loop flattened within 1E-5
            // (196.544320 flattened within 1E-6, and 188.569589 on its vertices alone).
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithABowedNotch(), 2.0, Miter, Thousandth));

            Assert.Equal(243.508485, grown.Area, 1E-5);
        }
    }
}
