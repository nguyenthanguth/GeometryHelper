using System;
using System.Collections.Generic;
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
    /// Fault round F3, the third pass: a curved loop that crosses itself and cannot be cut within the tolerance is read in
    /// finer steps, a hundredth and a ten-thousandth of it, and where no step cuts it, or it crosses itself at more than
    /// 2 000 places, it is offset as a simple loop, as it was before the first pass, never as nothing.
    /// </summary>
    /// <remarks>
    /// The second pass gave nothing for a loop that could not be cut within the tolerance or a hundredth of it, and for one
    /// crossing itself at more than 2 000 places. Every expected answer is the one the same loop gives within 1E-3 or 1E-5,
    /// the straight path's, or the one it had at d65ff2d; none was found by the code under test alone.
    /// </remarks>
    public class OffsetFaultF3LadderTests
    {
        private static readonly Tolerance HundredThousandth = new Tolerance(1E-5, 1E-7);

        /// <summary>The star polygon {n/m} of the radius given about the origin, its first corner turned 0.1 from the x axis.</summary>
        private static GeoPolygonArc2 Star(int n, int m, double radius)
        {
            var points = new GeoPoint2[n];

            for (int k = 0; k < n; k++)
            {
                double angle = 0.1 + 2.0 * Math.PI * k * m / n;
                points[k] = P(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }

            return new GeoPolygonArc2(points, new double[n]);
        }

        /// <summary>A loop of fifteen corners in a box 1.2 across, most edges arcs, whose crossings lie too near along its edges to cut within 1E-3.</summary>
        private static GeoPolygonArc2 FifteenCornersInABoxOfOnePointTwo() => new GeoPolygonArc2(
            new[]
            {
                P(0.5096992854412276, 0.8004695466312066), P(0.8748631173711786, 0.8056697970323272), P(0.5538435484208427, 0.05647774683546407),
                P(0.8647094179314039, 0.32605177357556286), P(0.9078126622779543, 1.1238662602340916), P(0.003870023421347067, 0.1692856130540717),
                P(1.0132270706473248, 0.6664542407714154), P(0.4687826209207229, 0.34359131678478005), P(0.09011753118870756, 0.18933417207081482),
                P(0.8322402143119063, 0.5278090659652349), P(1.1835588063523077, 1.0654600915858996), P(0.14344890636336366, 0.48727452487506745),
                P(1.1034384093485945, 0.16941276644120107), P(0.38160270396613866, 1.0968413764758547), P(0.27212991899614364, 0.49626298520462747),
            },
            new[]
            {
                0.0, 0.0, -0.13074696838424865, 0.12204055172486256, 0.022987113391508884, -0.005233003900029232, 0.0, 0.23690161431995288,
                -0.02662272254313469, 0.1235202518401296, -0.1582495548102304, 0.2403122115602308, 0.2892861415582179, -0.26493942242345747,
                0.1944247941926237,
            });

        /// <summary>A loop of four corners with a short arc of bulge -0.44058 0.005 long beside the origin, its first edge 8 long.</summary>
        private static GeoPolygonArc2 FourCornersWithAShortArc() => new GeoPolygonArc2(
            new[] { P(-8, 0), P(0, 0), P(-0.003614, 0.003455), P(2.632903, 6.989023) },
            new[] { 0.0, -0.44058, 0.0, 0.0 });

        [Fact]
        public void AStarOf71By35GrownAtATenth_IsTheOneLoopRoundIt()
        {
            // The star of radius 10 crosses itself at 2 414 places, more than are cut and read again in finer steps, and it
            // cannot be cut within a tenth: it is offset as a simple loop, as at d65ff2d, one loop of 302.6313 standing 0.3 off
            // its points all round. The second pass gave nothing.
            GeoPolygonArc2 star = Star(71, 35, 10.0);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(star, 0.3, new OffsetOptions(OffsetJoin.Round), Tenth));

            Assert.Equal(302.6313, grown.Area, 1E-3);
            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(star), 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void AStarCrossingItselfAtMoreThanTwoThousandPlaces_IsOffsetAsASimpleLoopWithinTheTolerance()
        {
            // The star {75/37} of radius 5 crosses itself at more than 2 000 places within 1E-3 and is not cut there: grown by
            // 0.3 it is the loop round it, 87.3190, as at d65ff2d. The second pass gave nothing.
            GeoPolygonArc2 star = Star(75, 37, 5.0);
            ArcOffset2.TrySplitAtCrossings(ArcChain2.EdgesOf(star), Thousandth, out _, out _, out int crossings);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(star, 0.3, new OffsetOptions(OffsetJoin.Round), Thousandth));

            Assert.True(crossings > ArcOffset2.MostCrossingsRetried, crossings + " crossings");
            Assert.Equal(87.3190, grown.Area, 1E-3);
            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(star), 0.3) <= Thousandth.EqualPoint);
        }

        [Fact]
        public void FifteenCornersInABoxOfOnePointTwoGrownByOneAtATenth_IsCutWithinATenThousandthOfIt()
        {
            // The loop cannot be cut within a tenth or within 1E-3, places of its crossings lying too near along an edge; within
            // 1E-5 it is, and grown by 1 it is one loop of 7.8584, as within 1E-5 and as the straight path within 1E-3 gives it
            // (7.8583). The second pass gave nothing.
            GeoPolygonArc2 loop = FifteenCornersInABoxOfOnePointTwo();
            double fine = Assert.Single(Offset2.Offset(loop, 1.0, new OffsetOptions(OffsetJoin.Round), HundredThousandth)).Area;

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 1.0, new OffsetOptions(OffsetJoin.Round), Tenth));

            Assert.Equal(fine, grown.Area, 1E-6);
            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(loop), 1.0) <= Tenth.EqualPoint);
        }

        [Theory]
        [InlineData(OffsetJoin.Round)]
        [InlineData(OffsetJoin.Miter)]
        [InlineData(OffsetJoin.Chamfer)]
        public void FourCornersWithAShortArcGrownAtATenth_IsTheLoopItIsWithinAThousandth(OffsetJoin join)
        {
            // The arc, 0.005 long, lies wholly within a tenth of the origin. Grown by 0.3 within 1E-3 the loop is one loop,
            // 36.6796 round, 37.0294 mitered and 36.7339 chamfered; at a tenth it was nothing for every join, and read again
            // at the finer steps it is the same loop.
            GeoPolygonArc2 loop = FourCornersWithAShortArc();
            double fine = Assert.Single(Offset2.Offset(loop, 0.3, new OffsetOptions(join), Thousandth)).Area;

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 0.3, new OffsetOptions(join), Tenth));

            Assert.Equal(fine, grown.Area, 1E-3);
        }
    }
}
