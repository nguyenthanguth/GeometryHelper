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
    /// What the pruning of a curved offset keeps at a coarse tolerance: nothing that stands nearer to the shape than the
    /// distance by more than the tolerance, however near the vertex of the shape that comes nearest to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A point whose nearest part of an edge lay within the tolerance of the edge's end was folded by that vertex only
    /// inside the vertex's own wedge, between the normals of the two edges there. At a tolerance of 0.1 the foot can lie
    /// a tolerance from the end while the point lies well outside the wedge, and then neither edge at the vertex claimed
    /// it. The U from (32.4489, 0) to the origin, round a half turn of radius 1.1 and back along y = 2.19856, offset by
    /// 6.2838 into itself, kept the round joins about the half turn's ends, 4.08 from the U: inside a U 2.2 wide nothing
    /// stands 6.28 from it.
    /// </para>
    /// <para>
    /// And a stretch of a piece that runs to the piece's own end, rather than to a cut, was read at its middle only. A
    /// stretch lies wholly on one side of the distance, but can come nearer towards that end: the end of a round join
    /// about the end of an arc whose radius ran out stood 0.226 inside the distance, its middle 0.094, within the
    /// tolerance of 0.1, and the join was kept.
    /// </para>
    /// </remarks>
    public class OffsetFaultS6PruningTests
    {
        private static readonly OffsetOptions MiterUnlimited = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);

        private static readonly OffsetOptions MiterTwo = new OffsetOptions(OffsetJoin.Miter, 2.0);

        private static readonly OffsetOptions RoundFine = new OffsetOptions(OffsetJoin.Round, OffsetOptions.DefaultMiterLimit, 1E-4);

        [Theory]
        [InlineData(32.448930434160367, 2.1985559799701706, -1.0389975146106432, -6.2838267028722106, 0)]
        [InlineData(2.7094845779749956, 0.38636266383638729, -1.0895103713448673, -1.1695052006605571, 1)]
        [InlineData(194.39995572641487, 70.2662793501589, -1.0014111910487578, -143.5466210327794, 0)]
        [InlineData(34.929709949032272, 5.36676744900959, -1.0199399265087861, -12.940110358381695, 2)]
        [InlineData(29.649554034531839, 3.5253669729108772, -1.0234728204661387, -21.92249117974308, 2)]
        public void AUOffsetIntoItselfFurtherThanItIsWide_GivesNothingAtACoarseTolerance(double length, double width, double bulge, double distance, int join)
        {
            // Two legs along x from (length, 0) and (0, width), joined by a half turn bulging out to the left of the origin.
            // Inside the U nothing stands further than half its width from it, and the offset goes further than the width.
            GeoPolylineArc2 u = Chain(new[] { P(length, 0), P(0, 0), P(0, width), P(length, width) }, 0.0, bulge, 0.0);
            OffsetOptions options = join == 0 ? MiterUnlimited : join == 1 ? RoundFine : MiterTwo;

            Assert.Empty(Offset2.Offset(u, distance, options, Tenth));
        }

        [Fact]
        public void AChainWhoseFirstTwoArcsRunOutOffsetByOnePointZeroSeven_IsOneChainAtTheDistance()
        {
            // The round join about the end of the second arc, whose radius of 0.33 ran out, ends 0.226 inside the distance;
            // what stands 1.078 from the chain is the one chain from (0.617, 0.856) on, round joins and all.
            GeoPolylineArc2 chain = Chain(
                new[] { P(0, 0), P(0.17087987112703132, -0.12224311404053644), P(0.51865848235772294, -0.2178762505694839), P(1.555561444788736, -0.31277993195928966), P(2.0862657618172271, -1.12485313962118), P(2.5254282440508118, -1.881202446696836) },
                0.77037796544394355,
                0.29466556641024777,
                0.0,
                0.0,
                -0.18780819987310471);
            const double distance = 1.0783300307012769;

            GeoPolylineArc2 one = Assert.Single(Offset2.Offset(chain, distance, RoundFine, Tenth));

            Assert.True(one.StartPoint.DistanceTo(P(0.61694301410687613, 0.85596536062420914)) <= 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(chain), distance) <= Tenth.EqualPoint);
        }

        [Fact]
        public void AChainCurlingRoundAnArcOffsetByOnePointTwoSevenWithAMiterLimitOfTwo_KeepsNothingNearerThanTheDistance()
        {
            // The pin and every step up to S5 kept the piece from (-0.083, 1.434) to (0.359, 1.977), 0.64 inside the distance.
            GeoPolylineArc2 chain = Chain(
                new[] { P(0, 0), P(-0.25101130937920674, 0.17283261417764947), P(0.096405454889584186, 0.46626940129931482), P(-0.14266128801081224, 0.80809450710147157), P(0.57471952853091068, 0.50060261005823281) },
                0.0,
                0.73163854457979949);
            const double distance = 1.2722782142331257;

            GeoPolylineArc2[] offset = Offset2.Offset(chain, distance, MiterTwo, Tenth);

            Assert.NotEmpty(offset);
            Assert.All(offset, piece => Assert.True(NearestToSource(PiecesOf(piece), PiecesOf(chain)) >= distance - Tenth.EqualPoint));
        }

        /// <summary>How near the answer comes to the source, read at 25 points a piece, ends included.</summary>
        private static double NearestToSource(Piece[] answer, Piece[] source)
        {
            double nearest = double.PositiveInfinity;

            foreach (Piece piece in answer)
            {
                for (int k = 0; k <= 24; k++)
                {
                    piece.At(k / 24.0, out double x, out double y);
                    nearest = Math.Min(nearest, DistanceTo(source, x, y));
                }
            }

            return nearest;
        }
    }
}
