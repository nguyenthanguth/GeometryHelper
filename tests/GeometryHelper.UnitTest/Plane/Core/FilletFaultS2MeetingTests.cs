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
    /// Where the corners at an edge take all of it, give or take the tolerance, nothing of the edge is left and the
    /// pieces either side meet at one point; a chain's end points and a corner left alone never move, and nothing
    /// throws.
    /// </summary>
    /// <remarks>
    /// The pieces left within the tolerance were taken out and the chain's constructor, which reads the global
    /// tolerance, refused the gap: hairpins whose turning edge is 0.1 long, filleted at a tolerance of 0.1, threw "The
    /// edges of a chain must run end to end", and so did a star whose straight between two fillets was 0.032 long.
    /// A fillet or a cut taking a whole edge left a stub or moved the chain's start, and a hairpin loop asked for no
    /// fillet at all threw "A loop needs at least 3 distinct vertices".
    /// </remarks>
    public class FilletFaultS2MeetingTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static void AssertChainEndsKept(GeoPolylineArc2 source, GeoPolylineArc2 result)
        {
            Assert.Equal(0.0, result.StartPoint.DistanceTo(source.StartPoint));
            Assert.Equal(0.0, result.EndPoint.DistanceTo(source.EndPoint));
        }

        /// <summary>Rounding cuts corners off, so with no stub running back along an edge the chain gets no longer.</summary>
        private static void AssertNoLonger(GeoPolylineArc2 source, GeoPolylineArc2 result)
        {
            Assert.True(result.Length <= source.Length, "the rounded chain is " + result.Length + " long, the source " + source.Length);
        }

        [Fact]
        public void TheSquareFilletedTwoHundredthsPastHalfASideAtATenth_IsTheCircleThroughTheMidpoints()
        {
            // Neighbours ask 10.04 of every side of 10, within 0.1: each side vanishes and the arcs meet midway, at
            // the midpoints of the sides, so the four quarter turns are the circle of radius 5 about (5, 5).
            GeoPolygonArc2 rounded = Corner2.Fillet(new GeoPolygon2(Square10()), 5.02, Tenth);

            Assert.Equal(4, rounded.VertexCount);
            Assert.Contains(rounded.Vertices, v => v.IsEqualTo(P(5, 0), Tight));
            Assert.Contains(rounded.Vertices, v => v.IsEqualTo(P(10, 5), Tight));
            Assert.Contains(rounded.Vertices, v => v.IsEqualTo(P(5, 10), Tight));
            Assert.Contains(rounded.Vertices, v => v.IsEqualTo(P(0, 5), Tight));
            Assert.Equal(Math.PI * 25.0, rounded.Area, 9);
        }

        [Fact]
        public void AHairpinFilletedAtATenThousandthAtATenth_DoesNotThrowAndKeepsItsEnds()
        {
            // Two long legs 10 apart joined by a turning edge 0.1 long, the tolerance itself.
            var hairpin = new GeoPolyline2(P(14.984265650678289, -0.98837158913957635), P(6.6118449764712821, 4.4799528392489849),
                P(6.6665282207551684, 4.5636770459910547), P(15.038948894962175, -0.90464738239750631));

            GeoPolylineArc2 rounded = Corner2.Fillet(hairpin, 0.0001, Tenth);

            // The turning edge goes, and the two fillets meet midway along it, within the tolerance of where it was.
            AssertChainEndsKept(new GeoPolylineArc2(hairpin), rounded);
            Assert.Equal(5, rounded.VertexCount);
            Assert.True(rounded[2].IsEqualTo(P((6.6118449764712821 + 6.6665282207551684) / 2, (4.4799528392489849 + 4.5636770459910547) / 2), new Tolerance(1E-4, 1E-6)));
        }

        [Fact]
        public void AStarWhoseStraightBetweenTwoFilletsIsShorterThanTheTolerance_DoesNotThrow()
        {
            var star = new GeoPolyline2(
                P(-16.752634443739773, 19.369716686033485), P(-14.450877130710158, 18.008830092806512), P(-14.824450841954166, 17.109260215551963),
                P(-17.267887329402875, 15.045746025435575), P(-18.082375608259721, 17.4728719492298), P(-18.747142850616939, 15.741459321134577),
                P(-19.547227854969627, 15.127637211878604), P(-19.044743787490514, 17.705678736775649), P(-22.395372677312942, 19.98346329692405),
                P(-20.753832117755703, 20.686109074508057), P(-18.306165077593565, 21.083502603545838), P(-17.815216894810941, 20.987196038745914),
                P(-16.943545532833063, 22.713619407430379), P(-16.00001815373906, 21.927500423841245), P(-14.88611822243719, 21.196939366216355),
                P(-14.157536612246712, 20.500741042226718), P(-16.519993075493367, 19.792942745764137));

            GeoPolylineArc2 rounded = Corner2.Fillet(star, 0.5, Tenth);

            AssertChainEndsKept(new GeoPolylineArc2(star), rounded);
            AssertNoLonger(new GeoPolylineArc2(star), rounded);
        }

        [Fact]
        public void OneCornerWhoseFilletTakesTheEdgeBeforeItWithinATenth_MeetsTheCornerBehindAndKeepsTheChainWhole()
        {
            // The corner at (10, 10) of three sides of the square, at 9.96: its fillet runs back to (10, 0.04) on a side
            // of 10, 0.04 short of the corner at (10, 0), which is left alone. Nothing of that side is left at 0.1, and
            // the arc starts at (10, 0) itself.
            GeoPolylineArc2 chain = Chain(Square10());

            Assert.True(Corner2.TryFilletAt(chain, 2, 9.96, out GeoPolylineArc2 rounded, Tenth));

            AssertChainEndsKept(chain, rounded);
            Assert.True(rounded[1].IsEqualTo(P(10, 0), Tight));
            Assert.True(rounded.GetEdgeAt(1).IsArc);
        }

        [Fact]
        public void ACutTakingTheWholeFirstEdge_LeavesNoRepeatedVertex()
        {
            // A chamfer of 10 back along the first edge, 10 long, at the default tolerance: the cut starts at the
            // chain's own start.
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(10, 10) });

            Assert.True(Corner2.TryChamferAt(chain, 1, 10.0, 5.0, out GeoPolylineArc2 cut, Thousandth));

            Assert.Equal(3, cut.VertexCount);
            Assert.True(cut[0].IsEqualTo(P(0, 0), Tight));
            Assert.True(cut[1].IsEqualTo(P(10, 5), Tight));
            Assert.True(cut[2].IsEqualTo(P(10, 10), Tight));
        }

        [Fact]
        public void AHairpinLoopNoLongerThanTheToleranceAcross_IsGivenBackWhenAskedForNoFillet()
        {
            // A loop 10 long and 0.02 wide asked for radii of nought: there is nothing to round, and the loop comes back
            // as it went in.
            var loop = new GeoPolygonArc2(new[] { P(-17.29143972875875, 3.6101964286048527), P(-22.2486991572574, -5.0745936643427747),
                P(-22.266068737443295, -5.064679145485778), P(-17.308809308944646, 3.62011094746185) }, null);

            GeoPolygonArc2 rounded = Corner2.Fillet(loop, new[] { 0.0, 0.0, 0.0 }, Tenth);

            Assert.Equal(loop.VertexCount, rounded.VertexCount);

            for (int i = 0; i < loop.VertexCount; i++)
            {
                Assert.True(rounded[i].IsEqualTo(loop[i], Tight));
            }
        }

        [Fact]
        public void ARectangleFilletedAtTenAtTheDefaultTolerance_IsTheSameAsBefore()
        {
            // Guard: nothing vanishes, and the answer is the one worked out by hand.
            var rectangle = new GeoPolygon2(P(0, 0), P(100, 0), P(100, 50), P(0, 50));

            GeoPolygonArc2 rounded = Corner2.Fillet(rectangle, 10.0, Thousandth);

            Assert.Equal(8, rounded.VertexCount);
            Assert.Equal(5000.0 - (4.0 - Math.PI) * 100.0, rounded.Area, 9);
            Assert.Equal(4, Enumerable.Range(0, rounded.VertexCount).Count(i => rounded.GetEdgeAt(i).IsArc));
        }
    }
}
