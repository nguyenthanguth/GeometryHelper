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
    /// A fillet or a chamfer that asks for a hair more edge than there is, by less than the tolerance, gives a shape: it
    /// does not throw, a chain keeps its end points where they were, and a loop does not cross itself.
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
    /// Which answer is right within the tolerance (both corners rounded, meeting within it, or the one asking more giving
    /// way) the docs do not say, so these tests ask only what every reading shares. The same shapes with the overrun
    /// beyond the tolerance, at 1E-3, are the guards: there the corner asking more gives way, as plane.md says.
    /// </para>
    /// </remarks>
    public class FilletFaultS2OverrunTests
    {
        private static GeoPoint2[] ThreeSides() => new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };

        /// <summary>Every arc of the chain has the radius asked, within the tolerance, and the ends are where they were.</summary>
        private static void AssertEndsKeptAndRadius(GeoPolylineArc2 result, double radius, double tolerance)
        {
            Assert.Equal(0.0, result.StartPoint.X);
            Assert.Equal(0.0, result.StartPoint.Y);
            Assert.Equal(0.0, result.EndPoint.X);
            Assert.Equal(10.0, result.EndPoint.Y);
            Assert.Contains(result.GetEdges(), e => e.IsArc);
            foreach (GeoEdge2 edge in result.GetEdges().Where(e => e.IsArc))
            {
                Assert.InRange(edge.ToArc().Radius, radius - tolerance, radius + tolerance);
            }
        }

        [Fact]
        public void ThreeSidesOfASquareFilletedTwoHundredthsPastHalfASide_DoNotThrowAtATenth()
        {
            // Both corners ask 5.02 of the side of 10 between them, 0.04 more than it has: within the tolerance of 0.1.
            GeoPolylineArc2 chain = Chain(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, 5.02, Tenth);

            AssertEndsKeptAndRadius(filleted, 5.02, Tenth.EqualPoint);
        }

        [Fact]
        public void ThreeSidesOfAStraightSquareFilletedTwoHundredthsPastHalfASide_DoNotThrowAtATenth()
        {
            GeoPolyline2 chain = new GeoPolyline2(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, 5.02, Tenth);

            AssertEndsKeptAndRadius(filleted, 5.02, Tenth.EqualPoint);
        }

        [Fact]
        public void TwoCornersAskingFourHundredthsTooMuchWithinATenth_DoNotThrow()
        {
            // 5.04 and 5.0 of the side of 10: an overrun of 0.04, inside the tolerance of 0.1.
            GeoPolylineArc2 chain = Chain(ThreeSides());

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, new[] { 0.0, 5.04, 5.0, 0.0 }, Tenth);

            Assert.Equal(0.0, filleted.StartPoint.DistanceTo(P(0, 0)));
            Assert.Equal(0.0, filleted.EndPoint.DistanceTo(P(0, 10)));
            Assert.Contains(filleted.GetEdges(), e => e.IsArc);
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
        public void ACutFiveHundredthsLongerThanTheFirstEdgeAtATenth_KeepsTheCurvedChainsStartPoint()
        {
            // 10.05 along the first edge, 10 long, and 5 up the second. A chain keeps both its end points exactly where they
            // were; on the pin the start moved to (-0.05, 0).
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(10, 10) });

            GeoPolylineArc2 cut = Corner2.Chamfer(chain, 10.05, 5.0, Tenth);

            Assert.Equal(0.0, cut.StartPoint.X);
            Assert.Equal(0.0, cut.StartPoint.Y);
            Assert.Equal(0.0, cut.EndPoint.DistanceTo(P(10, 10)));
            Assert.DoesNotContain(cut.Vertices, v => v.X < 0.0);
        }

        [Fact]
        public void ACutFiveHundredthsLongerThanTheFirstEdgeAtATenth_LeavesNoStubBeforeTheStraightChainsStart()
        {
            // On the pin the straight chain kept (0, 0) but ran back to (-0.05, 0) first, off the chain.
            GeoPolyline2 chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            GeoPolyline2 cut = Corner2.Chamfer(chain, 10.05, 5.0, Tenth);

            Assert.Equal(0.0, cut.StartPoint.DistanceTo(P(0, 0)));
            Assert.Equal(0.0, cut.EndPoint.DistanceTo(P(10, 10)));
            Assert.DoesNotContain(cut.Vertices, v => v.X < 0.0);
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
        public void TheSquareChamferedFourHundredthsPastHalfASideAtATenth_DoesNotCrossItself()
        {
            // 5.04 at every corner of the square of side 10: neighbours over-ask each side by 0.08, within 0.1. On the pin
            // each side kept a stub from (5.04, 0) back to (4.96, 0), and the cuts either side of it crossed at (5, 0.04).
            var square = new GeoPolygon2(Square10());

            GeoPolygon2 cut = Corner2.Chamfer(square, 5.04, 5.04, Tenth);

            Assert.False(CrossesItself(cut.Vertices), "the cut square " + string.Join(" ", cut.Vertices) + " crosses itself");
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
