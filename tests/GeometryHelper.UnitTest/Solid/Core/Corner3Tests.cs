using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Rounding the corners of a chain in space. This is the operation a reinforcing bar is made by: the
    /// points it turns at and one bending radius. A corner between two straight legs is flat whatever the
    /// chain does elsewhere, so each is rounded in its own plane and nothing is approximated.
    /// </summary>
    public class Corner3Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A right-angled bend in the XY plane, each leg three hundred long.
        /// </summary>
        private static GeoPolyline3 Elbow() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 300, 0));

        /// <summary>
        /// A bar that turns twice, and the second turn leaves the plane of the first: along X, then along Y,
        /// then up Z. Every leg three hundred long.
        /// </summary>
        private static GeoPolyline3 Stirrup() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 300, 0), new GeoPoint3(300, 300, 300));

        [Fact]
        public void ARightAngledBendBecomesAQuarterTurnOfTheRadiusAsked()
        {
            GeoPolylineArc3 bent = Elbow().Fillet(50.0);

            // Three pieces: a straight run, a quarter turn, a straight run.
            Assert.Equal(3, bent.EdgeCount);
            Assert.False(bent.GetEdgeAt(0).IsArc);
            Assert.True(bent.GetEdgeAt(1).IsArc);
            Assert.False(bent.GetEdgeAt(2).IsArc);

            GeoArc3 arc = bent.GetEdgeAt(1).ToArc();

            Assert.Equal(50.0, arc.Radius, 8);
            Assert.Equal(Math.PI / 2.0, Math.Abs(arc.SweptAngle), 8);

            // A quarter turn of radius fifty stands fifty back along each leg from the corner.
            Assert.True(bent.GetEdgeAt(0).EndPoint.IsEqualTo(new GeoPoint3(250, 0, 0), Loose));
            Assert.True(bent.GetEdgeAt(2).StartPoint.IsEqualTo(new GeoPoint3(300, 50, 0), Loose));

            // Its centre sits fifty in from both legs, and the bar no longer passes through the corner.
            Assert.True(arc.Center.IsEqualTo(new GeoPoint3(250, 50, 0), Loose));
            Assert.False(bent.IsPointOn(new GeoPoint3(300, 0, 0)));

            // The length is the two shortened legs plus the arc, which is shorter than the sharp corner was.
            Assert.Equal(250.0 + Math.PI * 50.0 / 2.0 + 250.0, bent.Length, 7);
            Assert.True(bent.Length < Elbow().Length);
        }

        [Fact]
        public void ABendThatLeavesThePlaneIsRoundedInItsOwnPlane()
        {
            GeoPolylineArc3 bent = Stirrup().Fillet(50.0);

            // Two corners, so two arcs, and the chain as a whole lies in no one plane.
            Assert.Equal(2, bent.GetEdges().Count(edge => edge.IsArc));
            Assert.False(bent.IsPlanar());

            GeoEdge3[] arcs = bent.GetEdges().Where(edge => edge.IsArc).ToArray();

            // Each is a quarter turn of the radius asked for, and each turns about a different axis.
            foreach (GeoEdge3 edge in arcs)
            {
                Assert.Equal(50.0, edge.ToArc().Radius, 7);
                Assert.Equal(Math.PI / 2.0, Math.Abs(edge.ToArc().SweptAngle), 7);
            }

            Assert.False(arcs[0].Normal.IsParallelTo(arcs[1].Normal, Loose));

            // The first bend turns in the XY plane and the second in the YZ plane.
            Assert.True(arcs[0].Normal.IsParallelTo(new GeoVector3(0, 0, 1), Loose));
            Assert.True(arcs[1].Normal.IsParallelTo(new GeoVector3(1, 0, 0), Loose));

            // Every piece still runs end to end, and the ends of the bar have not moved.
            Assert.True(bent.StartPoint.IsEqualTo(Stirrup().StartPoint, Tight));
            Assert.True(bent.EndPoint.IsEqualTo(Stirrup().EndPoint, Tight));
        }

        [Fact]
        public void AFlatChainRoundedInSpaceMatchesTheSameChainRoundedInThePlane()
        {
            // The binding promise: where the chain is flat, space and the plane agree exactly.
            GeoPolylineArc3 space = Stirrup().Fillet(50.0);
            GeoPolyline3 flatInput = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 300, 0), new GeoPoint3(0, 300, 0));

            GeoPolylineArc3 rounded = flatInput.Fillet(50.0);
            GeoPolylineArc2 plane = new GeoPolylineArc2(new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 300), new GeoPoint2(0, 300))).Fillet(50.0);

            Assert.Equal(plane.EdgeCount, rounded.EdgeCount);
            Assert.Equal(plane.Length, rounded.Length, 7);

            for (int i = 0; i <= 40; i++)
            {
                GeoPoint2 there = plane.GetPointAtParameter(i / 40.0);
                GeoPoint3 here = rounded.GetPointAtParameter(i / 40.0);

                Assert.True(here.IsEqualTo(new GeoPoint3(there.X, there.Y, 0.0), new Tolerance(1E-6, 1E-6)), "at " + i);
            }

            // And the one that is not flat is still rounded, just not in one plane.
            Assert.Equal(2, space.GetEdges().Count(edge => edge.IsArc));
        }

        [Fact]
        public void EachCornerTakesTheRadiusGivenForItsOwnVertex()
        {
            // Vertex 0 is an end, 1 the first bend, 2 the second, 3 an end.
            GeoPolylineArc3 bent = Stirrup().Fillet(new[] { 99.0, 40.0, 0.0, 99.0 });

            // One corner rounded, the other left square, and the ends ignored.
            Assert.Equal(1, bent.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(40.0, bent.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 7);
            Assert.True(bent.IsPointOn(new GeoPoint3(300, 300, 0), Loose));
            Assert.False(bent.IsPointOn(new GeoPoint3(300, 0, 0)));

            // One radius everywhere is the same as the single-radius form.
            Assert.True(Stirrup().Fillet(new[] { 0.0, 50.0, 50.0, 0.0 }).IsEqualTo(Stirrup().Fillet(50.0), Loose));

            // A short list leaves the rest alone, and an empty one changes nothing.
            Assert.Equal(1, Stirrup().Fillet(new[] { 0.0, 50.0 }).GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(0, Stirrup().Fillet(new double[0]).GetEdges().Count(edge => edge.IsArc));
        }

        [Fact]
        public void TwoCornersAskingForMoreThanTheEdgeHoldsMakeTheGreedyOneGiveWay()
        {
            // At a right angle the tangent point stands back by the radius itself, so the middle leg of three
            // hundred can serve 280 at one end and 20 at the other exactly, and both corners are rounded.
            Assert.Equal(2, Stirrup().Fillet(new[] { 0.0, 280.0, 20.0, 0.0 }).GetEdges().Count(edge => edge.IsArc));

            // Ask for forty at the second end and there is not enough to go round, so the one taking more
            // of the leg gives way.
            GeoPolylineArc3 bent = Stirrup().Fillet(new[] { 0.0, 280.0, 40.0, 0.0 });

            Assert.Equal(1, bent.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(40.0, bent.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 7);

            // The corner that asked for too much is still square.
            Assert.True(bent.IsPointOn(new GeoPoint3(300, 0, 0), Loose));

            // Exactly enough is enough: 150 at each end of a leg of 300 leaves nothing between them, and
            // both corners are still rounded.
            GeoPolylineArc3 tight = Stirrup().Fillet(150.0);

            Assert.Equal(2, tight.GetEdges().Count(edge => edge.IsArc));
        }

        [Fact]
        public void OneCornerCanBeRoundedOnItsOwn()
        {
            GeoPolyline3 stirrup = Stirrup();

            Assert.True(stirrup.TryFilletAt(2, 60.0, out GeoPolylineArc3 bent));

            Assert.Equal(1, bent.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(60.0, bent.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 7);

            // Rounding one corner is rounding that corner and nothing else.
            Assert.True(bent.IsEqualTo(stirrup.Fillet(new[] { 0.0, 0.0, 60.0, 0.0 }), Loose));

            // The ends of a chain are not corners.
            Assert.False(stirrup.TryFilletAt(0, 50.0, out _));
            Assert.False(stirrup.TryFilletAt(stirrup.VertexCount - 1, 50.0, out _));

            // A radius with nowhere to fit is refused rather than forced, and the chain comes back untouched.
            Assert.False(stirrup.TryFilletAt(1, 5000.0, out GeoPolylineArc3 untouched));
            Assert.True(untouched.IsEqualTo(new GeoPolylineArc3(stirrup), Tight));

            // And a corner can be rounded again on a chain that already carries arcs.
            Assert.True(bent.TryFilletAt(1, 40.0, out GeoPolylineArc3 twice));
            Assert.Equal(2, twice.GetEdges().Count(edge => edge.IsArc));
        }

        [Fact]
        public void ACornerWithNothingToTurnIsLeftAlone()
        {
            // Three points in a straight line have no corner between them.
            var straight = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(200, 0, 0));

            Assert.Equal(0, straight.Fillet(50.0).GetEdges().Count(edge => edge.IsArc));
            Assert.False(straight.TryFilletAt(1, 50.0, out _));

            // A leg that already curves lies in a plane of its own, so that corner is not rounded.
            GeoPolylineArc3 bent = Elbow().Fillet(50.0);

            Assert.False(bent.TryFilletAt(1, 10.0, out _));
            Assert.False(bent.TryFilletAt(2, 10.0, out _));
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNonsense()
        {
            GeoPolyline3 stirrup = Stirrup();
            var radii = new[] { 0.0, 40.0, 30.0, 0.0 };

            Assert.True(Corner3.Fillet(stirrup, 50.0).IsEqualTo(stirrup.Fillet(50.0), Tight));
            Assert.True(Corner3.Fillet(stirrup, 50.0, Tolerance.Global).IsEqualTo(stirrup.Fillet(50.0, Tolerance.Global), Tight));
            Assert.True(Corner3.Fillet(stirrup, radii).IsEqualTo(stirrup.Fillet(radii), Tight));

            GeoPolylineArc3 widened = new GeoPolylineArc3(stirrup);

            Assert.True(Corner3.Fillet(widened, 50.0).IsEqualTo(widened.Fillet(50.0), Tight));
            Assert.True(Corner3.Fillet(widened, radii, Tolerance.Global).IsEqualTo(widened.Fillet(radii, Tolerance.Global), Tight));

            Assert.Equal(
                Corner3.TryFilletAt(stirrup, 1, 50.0, out GeoPolylineArc3 fromStatic),
                stirrup.TryFilletAt(1, 50.0, out GeoPolylineArc3 fromInstance));
            Assert.True(fromStatic.IsEqualTo(fromInstance, Tight));

            Assert.Throws<ArgumentNullException>(() => Corner3.Fillet((GeoPolyline3)null, 50.0));
            Assert.Throws<ArgumentNullException>(() => Corner3.Fillet((GeoPolylineArc3)null, 50.0));
            Assert.Throws<ArgumentNullException>(() => Corner3.Fillet(stirrup, (System.Collections.Generic.IReadOnlyList<double>)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => stirrup.Fillet(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stirrup.Fillet(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stirrup.Fillet(new[] { 10.0, -5.0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => stirrup.Fillet(new[] { double.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => stirrup.TryFilletAt(99, 10.0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => stirrup.TryFilletAt(1, 0.0, out _));
        }

        [Fact]
        public void ARoundedBarIsShorterThanItsPointsSuggestWhichIsWhatABendingRadiusCosts()
        {
            // A bar set out by its corner points measures more than the bar itself: every bend cuts the
            // corner. This is the number a schedule has to carry, so it is worth being exact about.
            GeoPolyline3 setOut = Stirrup();
            GeoPolylineArc3 bar = setOut.Fillet(50.0);

            // Two right-angled bends of radius fifty: each saves 2r - (pi r / 2).
            double savedPerBend = 2.0 * 50.0 - Math.PI * 50.0 / 2.0;

            Assert.Equal(setOut.Length - 2.0 * savedPerBend, bar.Length, 7);
            Assert.Equal(900.0, setOut.Length, 9);
            Assert.True(bar.Length < 900.0);

            // And flattening the rounded bar is not the bar it came from, because the bends cut the corners.
            Assert.True(bar.Flatten().Length < bar.Length);
        }
    }
}
