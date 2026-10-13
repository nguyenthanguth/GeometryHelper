using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What a curved offset keeps is what stands the offset distance from its source, and all of it: a corner that closes
    /// is never bridged, a piece is cut where it comes nearer, and the part of it still standing |d| away stays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A corner was taken as closing only when the two moved pieces crossed. The chain 10 along x, then round an arc of
    /// bulge 0.5 that leaves turning 167.87 degrees to the left, rises 0.14 above the axis and curls back down, offset by
    /// 1 to the left: the moved segment y = 1 and the arc moved in to radius 5.25 do not cross, so the corner was bridged
    /// by a chord and the segment kept whole, standing 0.9588 from the arc at x = 7.5. What stands 1 from the chain is
    /// the segment to x = 7.27108, where it comes 1 from the arc's outside, and the moved arc from (7.48428, -1), where it
    /// comes 1 from the segment's other side, to its end.
    /// </para>
    /// <para>
    /// And folds were found only where moved pieces cross, so a piece partly nearer than |d| was kept or dropped whole.
    /// The hairpin 1 wide, 10 along x, 1 down and 9 back, offset by -0.6 into its inside: every moved piece comes within
    /// 0.4 of the other leg, and the whole offset went. The moved first leg y = -0.6 stands 0.6 from the chain as far as
    /// the circle of 0.6 about the chain's end (1, -1), up to x = 1 - sqrt 0.2 = 0.55279.
    /// </para>
    /// <para>
    /// The expected pieces were found by hand and checked on a dense sampling of the moved pieces against the chain
    /// (handcalc.py beside these drafts). Every answer is also read by the oracle in <see cref="OffsetFaultShapes"/>.
    /// </para>
    /// </remarks>
    public class OffsetFaultS4FoldedPiecesTests
    {
        [Fact]
        public void AnArcCurlingBackOverItsSegment_CutsTheSegmentWhereItComesNearerAndKeepsTheRestOfTheArc()
        {
            GeoPolylineArc2 chain = CurlingBack();

            GeoPolylineArc2[] offset = Offset2.Offset(chain, 1.0, OffsetOptions.Default, Thousandth);

            // Two pieces: the segment y = 1 from x = 0 to 8.68667 - sqrt(7.25^2 - 7.11046^2) = 7.27108, and the arc of
            // radius 5.25 about the arc's centre from x = 8.68667 - sqrt(5.25^2 - 5.11046^2) = 7.48428 on y = -1 to the
            // arc's end moved 1 towards the centre, (3.45031, -6.48857).
            Assert.Equal(2, offset.Length);
            Assert.Equal(0.0, offset[0].StartPoint.DistanceTo(P(0, 1)), 1E-6);
            Assert.Equal(0.0, offset[0].EndPoint.DistanceTo(P(7.27107659924183, 1)), 1E-6);
            Assert.Equal(0.0, offset[1].StartPoint.DistanceTo(P(7.484284689958933, -1)), 1E-6);
            Assert.Equal(0.0, offset[1].EndPoint.DistanceTo(P(3.450307279344803, -6.488568814846329)), 1E-6);
            Assert.True(WorstOffDistance(offset.SelectMany(PiecesOf).ToArray(), PiecesOf(chain), 1.0) <= Thousandth.EqualPoint);
        }

        [Fact]
        public void TheInsideOfAHairpinOneWideOffsetBySixTenths_KeepsTheStartOfTheFirstLeg()
        {
            GeoPolylineArc2 chain = Chain(Hairpin());

            GeoPolylineArc2[] offset = Offset2.Offset(chain, -0.6, OffsetOptions.Default, Thousandth);

            GeoPolylineArc2 one = Assert.Single(offset);
            Assert.Equal(2, one.VertexCount);
            Assert.Equal(0.0, one.StartPoint.DistanceTo(P(0, -0.6)), 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(1 - Math.Sqrt(0.2), -0.6)), 1E-6);
        }

        [Fact]
        public void TheOutsideOfTheSameHairpin_StaysOneChainAroundIt()
        {
            // Guard: 0.6 to the left, the outside, nothing folds. The legs move to y = 0.6, x = 10.6 and y = -1.6, mitred at
            // (10.6, 0.6) and (10.6, -1.6): one chain from (0, 0.6) to (1, -1.6), 10.6 + 2.2 + 9.6 = 22.4 long.
            GeoPolylineArc2 chain = Chain(Hairpin());

            GeoPolylineArc2[] offset = Offset2.Offset(chain, 0.6, OffsetOptions.Default, Thousandth);

            GeoPolylineArc2 one = Assert.Single(offset);
            Assert.Equal(22.4, one.Length, 1E-9);
            Assert.Equal(0.0, one.StartPoint.DistanceTo(P(0, 0.6)), 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(1, -1.6)), 1E-9);
        }
    }
}
