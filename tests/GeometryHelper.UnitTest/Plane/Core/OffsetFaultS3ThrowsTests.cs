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
    /// A curved offset gives its pieces, or none, and does not throw: not at a coarse tolerance, and not where an arc run
    /// on would sweep a whole turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pieces of a curved offset were built with constructors that compare with the global tolerance, while the work
    /// had been done with the tolerance passed. At 0.1, pieces shorter than that were merged away and left gaps of up to
    /// 0.1 that the constructor, at 1E-3, refused: the chain from the origin to (-5, 0) and round an arc of bulge -0.5 to
    /// (-7, 5), offset by 0.3 with an unlimited mitre, threw "The edges of a chain must run end to end". Its offset runs
    /// along y = -0.3 to the mitre at (-5.03882, -0.3), 0.03882 past the corner, and round the arc moved out to radius
    /// 3.66573 about (-4.125, 3.25) to (-7.25626, 5.15598).
    /// </para>
    /// <para>
    /// The U from (10, 0) to the origin, round the half circle of radius 2 about (0, 2) and back to (10, 4), offset by -2.5
    /// to its inside, threw "An arc sweeping a whole turn has no chord". The inside is 4 wide: moved 2.5 in, each straight
    /// comes within 1.5 of the other and the half circle's radius runs out, so nothing of it stands 2.5 from the U.
    /// </para>
    /// </remarks>
    public class OffsetFaultS3ThrowsTests
    {
        private static GeoPolylineArc2 U() => Chain(new[] { P(10, 0), P(0, 0), P(0, 4), P(10, 4) }, 0.0, -1.0, 0.0);

        [Fact]
        public void AChainOffsetAtATenthWithAnUnlimitedMitre_GivesItsOffsetInsteadOfThrowing()
        {
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(-5, 0), P(-7, 5) }, 0.0, -0.5);
            var options = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);

            GeoPolylineArc2[] offset = Offset2.Offset(chain, 0.3, options, Tenth);

            GeoPolylineArc2 one = Assert.Single(offset);
            Assert.True(one.StartPoint.DistanceTo(P(0, -0.3)) <= Tenth.EqualPoint, "starts at " + one.StartPoint);
            Assert.True(one.EndPoint.DistanceTo(P(-7.2562595666843315, 5.155984084068723)) <= Tenth.EqualPoint, "ends at " + one.EndPoint);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(chain), 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void TheInsideOfAUFourWideOffsetByTwoAndAHalf_IsNothing()
        {
            Assert.Empty(Offset2.Offset(U(), -2.5, new OffsetOptions(OffsetJoin.Round), Thousandth));
        }

        [Fact]
        public void TheInsideOfAUFourWideOffsetByOneAndAHalf_IsTheUOneWide()
        {
            // Guard: 1.5 in, the straights move to y = 1.5 and y = 2.5 and the half circle shrinks to radius 0.5 about
            // (0, 2): one chain 10 + 0.5 pi + 10 = 21.5708 long, every point 1.5 from the U.
            GeoPolylineArc2[] offset = Offset2.Offset(U(), -1.5, new OffsetOptions(OffsetJoin.Round), Thousandth);

            GeoPolylineArc2 one = Assert.Single(offset);
            Assert.Equal(20.0 + Math.PI * 0.5, one.Length, 1E-9);
            Assert.Equal(0.0, one.StartPoint.DistanceTo(P(10, 1.5)), 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(10, 2.5)), 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(U()), -1.5) <= 1E-9);
        }
    }
}
