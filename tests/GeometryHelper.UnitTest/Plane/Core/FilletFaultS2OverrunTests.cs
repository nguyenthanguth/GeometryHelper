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
    /// A fillet or a chamfer that asks for more edge than there is by no more than the tolerance fits exactly: the
    /// neighbours meet at one point, the edge between them goes, nothing runs back along it, nothing throws, and a chain's
    /// end points stay where they were. Beyond the tolerance the one asking more gives way, as plane.md says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fit tests let a fillet or a cut need up to the tolerance more edge than there is, and two neighbours over-ask
    /// the edge between them by as much. What the overrun left was a stub running back along the edge. On a chain the
    /// stubs within the tolerance were then taken out and the constructor, which reads the global tolerance, refused the
    /// gap: three sides of the square of side 10 filleted by 5.02 at a tolerance of 0.1 threw "The edges of a chain must
    /// run end to end". A cut of 10.05 on the first edge, 10 long, moved a chain's start to (-0.05, 0), or gave it a stub
    /// there; and the square chamfered by 5.04 at every corner came back with stubs 0.08 long running back along each
    /// side, a loop that crosses itself.
    /// </para>
    /// <para>
    /// The lead's amendment S2a: an overrun within <c>EqualPoint</c> counts as an exact fit. Where the shapes are
    /// symmetric the meeting point is the middle of the edge that goes; the tests read every moved point within the
    /// tolerance, since the fit is exact only to it. The same shapes with the overrun beyond the tolerance, at 1E-3, are
    /// the guards: there the corner asking more gives way.
    /// </para>
    /// </remarks>
    public class FilletFaultS2OverrunTests
    {
        private static GeoPoint2[] ThreeSides() => new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };

        /// <summary>
        /// Three sides of the square rounded at both corners with the side between them gone: the chain's ends exactly
        /// where they were, the first fillet from near <paramref name="first"/> to near <paramref name="meeting"/> on the
        /// right side, the second on to near <paramref name="last"/>, and nothing straight left on the right side.
        /// </summary>
        private static void AssertMeetingAtOnePoint(GeoPolylineArc2 result, GeoPoint2 first, GeoPoint2 meeting, GeoPoint2 last, double tolerance)
        {
            Assert.Equal(0.0, result.StartPoint.X);
            Assert.Equal(0.0, result.StartPoint.Y);
            Assert.Equal(0.0, result.EndPoint.X);
            Assert.Equal(10.0, result.EndPoint.Y);
            Assert.Equal(5, result.VertexCount);
            Assert.True(result.Vertices[1].DistanceTo(first) <= tolerance, "the first fillet starts at " + result.Vertices[1]);
            Assert.True(result.Vertices[2].DistanceTo(meeting) <= tolerance, "the fillets meet at " + result.Vertices[2]);
            Assert.True(result.Vertices[3].DistanceTo(last) <= tolerance, "the second fillet ends at " + result.Vertices[3]);
            Assert.True(result.GetEdgeAt(1).IsArc && result.GetEdgeAt(2).IsArc, "the two corners are not both rounded");
        }

        [Fact]
        public void ThreeSidesOfASquareFilletedTwoHundredthsPastHalfASide_MeetInTheMiddleAtATenth()
        {
            // Both corners ask 5.02 of the side of 10 between them, 0.04 more than it has: within the tolerance of 0.1, an
            // exact fit. The fillets meet at the middle of the right side, (10, 5), and touch the other sides near
            // (4.98, 0) and (4.98, 10).
            GeoPolylineArc2 chain = Chain(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, 5.02, Tenth);

            AssertMeetingAtOnePoint(filleted, P(4.98, 0), P(10, 5), P(4.98, 10), Tenth.EqualPoint);
        }

        [Fact]
        public void ThreeSidesOfAStraightSquareFilletedTwoHundredthsPastHalfASide_MeetInTheMiddleAtATenth()
        {
            GeoPolyline2 chain = new GeoPolyline2(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, 5.02, Tenth);

            AssertMeetingAtOnePoint(filleted, P(4.98, 0), P(10, 5), P(4.98, 10), Tenth.EqualPoint);
        }

        [Fact]
        public void TwoCornersAskingFourHundredthsTooMuchWithinATenth_MeetAtOnePoint()
        {
            // 5.04 and 5.0 of the side of 10: an overrun of 0.04, inside the tolerance of 0.1. The fillets meet on the
            // right side between (10, 5) and (10, 5.04), and touch near (4.96, 0) and (5, 10).
            GeoPolylineArc2 chain = Chain(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, new[] { 0.0, 5.04, 5.0, 0.0 }, Tenth);

            AssertMeetingAtOnePoint(filleted, P(4.96, 0), P(10, 5.02), P(5, 10), Tenth.EqualPoint);
        }

        [Theory]
        [InlineData(10.0, 4.98)]
        [InlineData(0.3, 0.14)]
        public void TwoFilletsLeavingLessThanTheToleranceBetweenThem_DoNotThrowAtATenth(double side, double radius)
        {
            // The other way from an overrun: the side of 10 filleted by 4.98 at both ends keeps 0.04 of straight between
            // the fillets, the hairpin 0.3 wide filleted by 0.14 keeps 0.02, both shorter than the tolerance of 0.1 (and
            // well longer than the 1E-3 the constructor merges at). Both threw "must run end to end" on the pin and after
            // S1, as the audit's FPL case 4667 does after S1. S2a does not say whether the bit left is kept or the fillets
            // meet at one point, so this asks only that both corners are rounded and the ends stay.
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(10, side), P(0, side) });

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, radius, Tenth);

            Assert.Equal(0.0, filleted.StartPoint.DistanceTo(P(0, 0)));
            Assert.Equal(0.0, filleted.EndPoint.DistanceTo(P(0, side)));
            Assert.Equal(2, filleted.GetEdges().Count(e => e.IsArc));
        }

        [Fact]
        public void TwoCornersAskingFourHundredthsTooMuchAtAThousandth_TheOneAskingMoreGivesWay()
        {
            // Guard: the same overrun, 40 tolerances of 1E-3 beyond the side. The corner asking 5.04 gives way and stays
            // square at (10, 0); the one asking 5 is rounded about (5, 5), touching at (10, 5) and (5, 10).
            GeoPolylineArc2 chain = Chain(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, new[] { 0.0, 5.04, 5.0, 0.0 }, Thousandth);

            Assert.Equal(5, filleted.VertexCount);
            Assert.Equal(0.0, filleted.Vertices[1].DistanceTo(P(10, 0)), 1E-12);
            Assert.Equal(0.0, filleted.Vertices[2].DistanceTo(P(10, 5)), 1E-9);
            Assert.Equal(0.0, filleted.Vertices[3].DistanceTo(P(5, 10)), 1E-9);
            Assert.Equal(Math.Tan(Math.PI / 8), filleted.Bulges[2], 1E-12);
        }

        [Fact]
        public void TheSquareFilletedTwoHundredthsPastHalfItsSideAtATenth_IsFourQuarterTurns()
        {
            // Guard: as a loop the pin already met every pair of fillets at one point, the middles of the sides within
            // 0.1, every straight side gone: four quarter turns.
            var square = new GeoPolygon2(Square10());

            GeoPolygonArc2 round = Corner2.Fillet(square, 5.02, Tenth);

            Assert.Equal(4, round.VertexCount);
            Assert.All(round.GetEdges(), e => Assert.True(e.IsArc));
        }

        [Fact]
        public void AChamferOfFiveHundredthsAtATenth_DoesNotThrowOnACurvedChain()
        {
            // The cut, 0.05 along each edge, is shorter than the tolerance: made or left, the chain keeps its ends.
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(10, 10) });

            GeoPolylineArc2 cut = Corner2.Chamfer(chain, 0.05, 0.05, Tenth);

            Assert.Equal(0.0, cut.StartPoint.DistanceTo(P(0, 0)));
            Assert.Equal(0.0, cut.EndPoint.DistanceTo(P(10, 10)));
        }

        [Fact]
        public void AChamferOfFiveHundredthsAtATenth_StillCutsAStraightChain()
        {
            // Guard: the straight chain was already cut, at (9.95, 0) and (10, 0.05).
            GeoPolyline2 chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            GeoPolyline2 cut = Corner2.Chamfer(chain, 0.05, 0.05, Tenth);

            Assert.Equal(4, cut.VertexCount);
            Assert.Equal(0.0, cut.Vertices[1].DistanceTo(P(9.95, 0)), 1E-12);
            Assert.Equal(0.0, cut.Vertices[2].DistanceTo(P(10, 0.05)), 1E-12);
        }

        [Fact]
        public void ACutFiveHundredthsLongerThanTheFirstEdgeAtATenth_RunsFromTheCurvedChainsStart()
        {
            // 10.05 along the first edge, 10 long, and 5 up the second: within the tolerance an exact fit, so the cut takes
            // the whole first edge and runs from the start, which stays exactly at the origin, to (10, 5). On the pin the
            // start moved to (-0.05, 0).
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(10, 10) });

            GeoPolylineArc2 cut = Corner2.Chamfer(chain, 10.05, 5.0, Tenth);

            Assert.Equal(3, cut.VertexCount);
            Assert.Equal(0.0, cut.StartPoint.X);
            Assert.Equal(0.0, cut.StartPoint.Y);
            Assert.Equal(0.0, cut.Vertices[1].DistanceTo(P(10, 5)), 1E-9);
            Assert.Equal(0.0, cut.EndPoint.DistanceTo(P(10, 10)));
        }

        [Fact]
        public void ACutFiveHundredthsLongerThanTheFirstEdgeAtATenth_RunsFromTheStraightChainsStart()
        {
            // On the pin the straight chain kept (0, 0) but ran back to (-0.05, 0) first, off the chain.
            GeoPolyline2 chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            GeoPolyline2 cut = Corner2.Chamfer(chain, 10.05, 5.0, Tenth);

            Assert.Equal(3, cut.VertexCount);
            Assert.Equal(0.0, cut.StartPoint.DistanceTo(P(0, 0)));
            Assert.Equal(0.0, cut.Vertices[1].DistanceTo(P(10, 5)), 1E-9);
            Assert.Equal(0.0, cut.EndPoint.DistanceTo(P(10, 10)));
        }

        [Fact]
        public void ACutFiveHundredthsLongerThanTheFirstEdgeAtAThousandth_LeavesTheCornerAlone()
        {
            // Guard: 50 tolerances too long, the cut has no edge to be measured along and the corner stays.
            GeoPolylineArc2 curved = Chain(new[] { P(0, 0), P(10, 0), P(10, 10) });
            GeoPolyline2 straight = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            Assert.Equal(3, Corner2.Chamfer(curved, 10.05, 5.0, Thousandth).VertexCount);
            Assert.Equal(3, Corner2.Chamfer(straight, 10.05, 5.0, Thousandth).VertexCount);
        }

        [Fact]
        public void TheSquareChamferedFourHundredthsPastHalfASideAtATenth_IsTheDiamond()
        {
            // 5.04 at every corner of the square of side 10: neighbours over-ask each side by 0.08, within 0.1, an exact fit.
            // The cuts meet at the middles of the sides: the diamond through (5, 0), (10, 5), (5, 10) and (0, 5), area 50
            // within the tolerance. On the pin each side kept a stub from (5.04, 0) back to (4.96, 0), and the cuts either
            // side of it crossed at (5, 0.04).
            var square = new GeoPolygon2(Square10());

            GeoPolygon2 cut = Corner2.Chamfer(square, 5.04, 5.04, Tenth);

            Assert.False(CrossesItself(cut.Vertices), "the cut square " + string.Join(" ", cut.Vertices) + " crosses itself");
            Assert.Equal(4, cut.VertexCount);
            foreach (GeoPoint2 middle in new[] { P(5, 0), P(10, 5), P(5, 10), P(0, 5) })
            {
                Assert.Contains(cut.Vertices, v => v.DistanceTo(middle) <= Tenth.EqualPoint);
            }
        }

        [Fact]
        public void TheSquareFilletedOrChamferedByHalfItsSide_IsTheCircleAndTheDiamond()
        {
            // Guard, plane.md's "exactly enough is enough", at 1E-3: four quarter turns of radius 5 about (5, 5), and the
            // diamond through the four midpoints.
            var square = new GeoPolygon2(Square10());

            GeoPolygonArc2 round = Corner2.Fillet(square, 5.0, Thousandth);
            GeoPolygon2 diamond = Corner2.Chamfer(square, 5.0, 5.0, Thousandth);

            Assert.Equal(4, round.VertexCount);
            Assert.Equal(Math.PI * 25.0, round.Area, 1E-9);
            Assert.Equal(4, diamond.VertexCount);
            Assert.Equal(50.0, diamond.Area, 1E-9);
        }
    }
}
