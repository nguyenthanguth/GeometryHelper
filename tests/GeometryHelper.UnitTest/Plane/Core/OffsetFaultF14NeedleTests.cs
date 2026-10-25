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
    /// A loop that runs out to a spike and straight back, the spike narrower than the tolerance at its foot, is offset as the
    /// straight path reads it, the spike dropped: offset as drawn, the square 10 by 10 with a needle 0.0005 wide on its top,
    /// grown by 0.3, kept a strip 0.6 wide about the needle, 115.85 with round joins where the straight path gives 112.28, and
    /// a notch 0.0008 wide in its bottom grew a miter spike, 113.63 where the straight path gives 112.36.
    /// </summary>
    public class OffsetFaultF14NeedleTests
    {
        /// <summary>The square 10 by 10 whose bottom dips from (4.9996, 0) to (5, -0.01) and back up to (5.0004, 0).</summary>
        private static GeoPolygonArc2 SquareWithANotch()
            => Loop(new[] { P(0, 0), P(4.9996, 0), P(5, -0.01), P(5.0004, 0), P(10, 0), P(10, 10), P(0, 10) });

        /// <summary>
        /// The triangle (0, 0), (10, 0), (5, 10) with a needle from its top up to (5, 20) and back down to (5.0004, 10).
        /// </summary>
        private static GeoPolygonArc2 TriangleWithANeedle()
            => Loop(new[] { P(0, 0), P(10, 0), P(5, 10), P(5, 20), P(5.0004, 10) });

        /// <summary>The square 10 by 10 with a needle on its top, its sides arcs of bulge 0.1 bowing apart.</summary>
        private static GeoPolygonArc2 SquareWithACurvedNeedle()
            => Loop(new[] { P(0, 0), P(10, 0), P(10, 10), P(4.0005, 10), P(4, 16), P(4, 10), P(0, 10) }, 0, 0, 0, 0.1, 0.1);

        private static OffsetOptions Options(OffsetJoin join) => new OffsetOptions(join);

        /// <summary>
        /// The same loop through the straight path: its vertices as a polygon, every edge of it straight, its round joins cut
        /// within 1E-5.
        /// </summary>
        private static double StraightArea(GeoPolygonArc2 loop, double distance, OffsetJoin join, Tolerance tolerance)
        {
            GeoPolygon2[] loops = Offset2.Offset(new GeoPolygon2(loop.Vertices.ToList()), distance, new OffsetOptions(join, 10, 1E-5), tolerance);
            bool clockwise = loop.IsClockwise;
            return loops.Sum(p => p.IsClockwise == clockwise ? p.Area : -p.Area);
        }

        [Theory]
        [InlineData(OffsetJoin.Round, 112.282743)]
        [InlineData(OffsetJoin.Miter, 112.36)]
        [InlineData(OffsetJoin.Chamfer, 112.298233)]
        public void ASquareWithANeedleThinnerThanTheToleranceGrown_IsTheSquareGrownAsTheStraightPathGivesIt(OffsetJoin join, double area)
        {
            // The needle is 0.0005 wide at its foot, under the tolerance 1E-3: the straight path merges its foot and drops
            // it, and grows the square alone, 100 + 12 + 0.09 pi with round joins, 112.36 mitered, 112.2982 chamfered. Offset
            // as drawn it kept a strip 0.6 wide about the needle: 115.8455, 117.5810 and 115.8996.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithANeedle(), 0.3, Options(join), Thousandth));

            Assert.Equal(area, grown.Area, 1E-4);
            Assert.Equal(StraightArea(SquareWithANeedle(), 0.3, join, Thousandth), grown.Area, 1E-4);
        }

        [Theory]
        [InlineData(OffsetJoin.Round, 112.282743)]
        [InlineData(OffsetJoin.Miter, 112.36)]
        [InlineData(OffsetJoin.Chamfer, 112.298233)]
        public void ASquareWithANotchNarrowerThanTheToleranceGrown_IsTheSquareGrownAsTheStraightPathGivesIt(OffsetJoin join, double area)
        {
            // The notch is 0.0008 wide where it leaves the bottom, under the tolerance: the straight path drops it. Offset as
            // drawn, its tip 0.01 down grew 112.2838 round, a miter spike 3 long, 113.6307, and 112.3040 chamfered.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithANotch(), 0.3, Options(join), Thousandth));

            Assert.Equal(area, grown.Area, 1E-4);
            Assert.Equal(StraightArea(SquareWithANotch(), 0.3, join, Thousandth), grown.Area, 1E-4);
        }

        [Theory]
        [InlineData(OffsetJoin.Round)]
        [InlineData(OffsetJoin.Miter)]
        [InlineData(OffsetJoin.Chamfer)]
        public void ATriangleWithANeedleGrown_IsTheTriangleGrown(OffsetJoin join)
        {
            // The needle's foot, (5, 10) to (5.0004, 10), is one corner, the triangle's top: grown by 0.3 the triangle is
            // 59.990947 round, 60.179450 mitered and 60.020163 chamfered. With the needle dropped and its foot left an edge
            // 0.0004 long, the miter had two corners there, 60.1989; offset as drawn, 67.7568 mitered.
            GeoPolygonArc2 triangle = Loop(new[] { P(0, 0), P(10, 0), P(5, 10) });
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(TriangleWithANeedle(), 0.3, Options(join), Thousandth));

            Assert.Equal(Assert.Single(Offset2.Offset(triangle, 0.3, Options(join), Thousandth)).Area, grown.Area, 1E-9);
            Assert.Equal(StraightArea(triangle, 0.3, join, Thousandth), grown.Area, 1E-4);
        }

        [Fact]
        public void ANeedleWiderThanTheTolerance_IsKept()
        {
            // Within 1E-4 the needle's foot, 0.0005 wide, is five tolerances across, and it grows round its tip as drawn:
            // 115.845469, as the straight path gives it (115.845450, its round joins cut within the default share).
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithANeedle(), 0.3, Options(OffsetJoin.Round), TenThousandth));

            Assert.Equal(115.845469, grown.Area, 1E-5);
        }

        [Fact]
        public void ANeedleWithCurvedSides_IsOffsetAsDrawn()
        {
            // Only a spike between two straight edges is dropped: one whose sides are arcs bowing 0.3 apart encloses more than
            // the tolerance across, and grown by 0.3 keeps its own outline about it, more than the square alone.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(SquareWithACurvedNeedle(), 0.3, Options(OffsetJoin.Round), Thousandth));

            Assert.True(grown.Area > 112.282743 + 1.0);
        }
    }
}
