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
    /// Beside an arc, the curved offset joins an opening corner as K2, K3 and S6a say: a chamfer runs the arc on along its
    /// tangent to the cut at |d|; a mitre past the limit runs the arc round its own circle to the square at limit times
    /// |d|; and where the arc's circle never reaches that square, the corner is rounded at |d|.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The square 10 with its right side bowed out 45 degrees, grown by 2 with chamfers: the corner (10, 0) turns 67.5
    /// degrees, and the cut 2 from it, square to its bisector, reaches 2 tan(67.5 / 4 deg) = 0.60669 either side of the
    /// bisector. It runs from (10.60669, -2), on y = -2, to (11.61559, -1.32588), and a straight piece leaves the moved
    /// arc's end (11.84776, -0.76537) back along its tangent to it. The corner (10, 10) is cut the same way, and the
    /// square corners at 2 (2 sqrt 2 - 2 either side). The loop encloses 191.37277 within its straight sides and the
    /// arc's segment of 8.88502 beyond them: 200.25779. The chord the pin cut, (10, -2) to (11.84776, -0.76537), has its
    /// middle 1.66 from the corner.
    /// </para>
    /// <para>
    /// The 10 degree tip whose second leg is an arc of radius 501.25 curving away, offset by 1 to the outside of the tip
    /// with the default limit of 10: the moved line y = -1 and the moved circle (radius 500.25) would meet 12.34 from the
    /// tip, past the limit. The square 10 out runs from (-9.95071, -1) on y = -1 to (-9.98183, -0.64431) on the moved
    /// circle, and the arc runs on round its circle from there (bulge 0.05498, up from 0.05). The pin cut the chord from
    /// (0, -1) to the arc's moved start, its middle 0.087 from the tip.
    /// </para>
    /// <para>
    /// The chain 100 along x into a clockwise arc of radius 36.256, offset by 20 to its right: the square would lie 200
    /// from the corner (100, 0), which the arc's moved circle, of radius 16.256 about (123.167, 27.889), never reaches.
    /// The corner is rounded by the circle of 20 about it, from (100, -20) to (112.77956, 15.38450), 140.28 degrees (bulge
    /// 0.70206), and the moved arc runs on to (130.71413, 42.28637). The pin cut the chord between the two.
    /// </para>
    /// </remarks>
    public class OffsetFaultS6aBesideAnArcTests
    {
        private static readonly OffsetOptions Chamfer = new OffsetOptions(OffsetJoin.Chamfer);

        [Fact]
        public void TheSquareWithABowedSideGrownByTwoWithChamfers_RunsTheArcOnAlongItsTangentToTheCut()
        {
            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(SquareWithItsRightSideBowedOut(), 2.0, Chamfer, Thousandth));

            Assert.Equal(10, one.VertexCount);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(10.606693367214685, -2)) <= 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(11.615587564863725, -1.325878449210181)) <= 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(11.847759065022574, -0.7653668647301796)) <= 1E-9);
            Assert.Equal(200.25779079605735, one.Area, 1E-9);
        }

        [Fact]
        public void TheTipWithAnArcLegOffsetByOne_IsSquaredOffAtTenWithTheArcRunRoundItsCircle()
        {
            GeoPolylineArc2 one = Assert.Single(Offset2.Offset(TenDegreeTipWithAnArcLeg(), 1.0, OffsetOptions.Default, Thousandth));

            Assert.Equal(4, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(-9.95070971190755, -1)), 1E-9);
            Assert.Equal(0.0, one.Vertices[2].DistanceTo(P(-9.981828569443163, -0.6443098307657588)), 1E-9);
            Assert.Equal(0.05498257664792722, one.Bulges[2], 1E-9);
        }

        [Fact]
        public void AStraightIntoATightArcOffsetPastWhereItsCircleReaches_IsRoundedAtTheDistance()
        {
            GeoPolylineArc2 one = Assert.Single(Offset2.Offset(StraightIntoATightArc(), -20.0, OffsetOptions.Default, Thousandth));

            Assert.Equal(4, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(100, -20)), 1E-9);
            Assert.Equal(0.0, one.Vertices[2].DistanceTo(P(112.77955700695694, 15.384502679837844)), 1E-9);
            Assert.Equal(0.7020582153682383, one.Bulges[1], 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(130.71413483667158, 42.28636942440981)), 1E-9);
        }
    }
}
