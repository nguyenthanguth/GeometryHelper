using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Cutting a bar. <see cref="GeoPolyline3"/> could be cut twelve ways and a curved chain could not be cut
    /// at all, so the only way to stop a bar at a pour break was to straighten it first — which throws away
    /// the bends and with them the length a schedule needs.
    /// </summary>
    /// <remarks>
    /// The one thing every test here checks is that the pieces still hold arcs and still add up: cutting an
    /// arc gives two arcs of the same radius, so the pieces put back end to end draw exactly what went in.
    /// </remarks>
    public class BarCuttingTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A bar in z = 0: along the x axis to (400, 0, 0), then up to (400, 200, 0), with a fifty radius at
        /// the one corner.
        /// </summary>
        private static GeoPolylineArc3 Bar() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0)).Fillet(50.0);

        private static void AddsUp(GeoPolylineArc3 whole, GeoPolylineArc3[] pieces)
        {
            Assert.Equal(whole.Length, pieces.Sum(piece => piece.Length), 6);
        }

        [Fact]
        public void ABarStoppedAtAPourBreakComesBackAsTwoBars()
        {
            GeoPolylineArc3 bar = Bar();
            var pourBreak = new GeoPlane3(new GeoPoint3(200, 0, 0), new GeoVector3(1, 0, 0));

            Assert.True(bar.TrySplitBy(pourBreak, out GeoPolylineArc3[] pieces));
            Assert.Equal(2, pieces.Length);

            // The first piece runs from the start to the break; the second carries the bend.
            Assert.Equal(200.0, pieces[0].Length, 6);
            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Loose));
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint3(200, 0, 0), Loose));

            // Nothing is lost and nothing is straightened.
            AddsUp(bar, pieces);
            Assert.Contains(true, pieces[1].GetEdges().Select(edge => edge.IsArc));

            // A break the bar never reaches leaves it whole.
            Assert.False(bar.TrySplitBy(new GeoPlane3(new GeoPoint3(1000, 0, 0), new GeoVector3(1, 0, 0)), out GeoPolylineArc3[] whole));
            Assert.Single(whole);
            Assert.Equal(bar.Length, whole[0].Length, 6);
        }

        [Fact]
        public void ABreakThroughTheBendCutsTheArcAndKeepsItsRadius()
        {
            GeoPolylineArc3 bar = Bar();

            // The fillet is centred at (350, 50, 0) with radius 50, so x = 380 passes through it.
            var throughBend = new GeoPlane3(new GeoPoint3(380, 0, 0), new GeoVector3(1, 0, 0));

            Assert.True(bar.TrySplitBy(throughBend, out GeoPolylineArc3[] pieces));
            Assert.Equal(2, pieces.Length);
            AddsUp(bar, pieces);

            // Both pieces hold a piece of the bend, and both pieces' arcs keep the radius they were cut from.
            foreach (GeoPolylineArc3 piece in pieces)
            {
                foreach (GeoEdge3 edge in piece.GetEdges().Where(edge => edge.IsArc))
                {
                    Assert.Equal(50.0, edge.ToArc().Radius, 6);
                    Assert.True(edge.ToArc().Center.IsEqualTo(new GeoPoint3(350, 50, 0), Loose),
                        edge.ToArc().Center.ToString());
                }
            }
        }

        [Fact]
        public void CuttingAtALengthAlongTheBarIsNotCuttingAtALengthAlongItsSetOut()
        {
            GeoPolylineArc3 bar = Bar();

            Assert.True(bar.TrySplitAtDistance(bar.Length / 2.0, out GeoPolylineArc3[] halves));
            Assert.Equal(2, halves.Length);

            // Two pieces of the same length, measured along the arcs.
            Assert.Equal(halves[0].Length, halves[1].Length, 6);
            AddsUp(bar, halves);

            // And the bar is shorter than the set-out it was filleted from, so the halves are shorter too.
            GeoPolyline3 setOut = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0));

            Assert.True(bar.Length < setOut.Length);
            Assert.True(halves[0].Length < setOut.Length / 2.0);

            // A distance past either end cuts nothing.
            Assert.False(bar.TrySplitAtDistance(0.0, out _));
            Assert.False(bar.TrySplitAtDistance(bar.Length, out _));
            Assert.False(bar.TrySplitAtDistance(-1.0, out _));
        }

        [Fact]
        public void ABarCanBeCutAtAPointOnIt()
        {
            GeoPolylineArc3 bar = Bar();
            GeoPoint3 on = bar.GetPointAtDistance(120.0);

            Assert.True(bar.TrySplitBy(on, out GeoPolylineArc3[] pieces));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(120.0, pieces[0].Length, 6);
            AddsUp(bar, pieces);

            // A point that is not on the bar cuts nothing and the bar comes back whole.
            Assert.False(bar.TrySplitBy(new GeoPoint3(0, 500, 0), out GeoPolylineArc3[] whole));
            Assert.Single(whole);
        }

        [Fact]
        public void ABarTrimmedToAMemberKnowsWhatIsInTheConcreteAndWhatSticksOut()
        {
            GeoPolylineArc3 bar = Bar();

            // A slab reaching out to x = 300, with the start of the bar well inside it.
            GeoSolid3 slab = new GeoAabb3(new GeoPoint3(-100, -100, -50), new GeoPoint3(300, 300, 50)).ToObb().ToSolid();

            Assert.True(bar.TrySplitBy(slab, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside));

            Assert.Single(inside);
            Assert.Single(outside);

            // The part in the concrete runs from the start to x = 300.
            Assert.Equal(300.0, inside[0].Length, 6);
            Assert.True(inside[0].StartPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Loose));

            // What sticks out carries the bend.
            Assert.Contains(true, outside[0].GetEdges().Select(edge => edge.IsArc));
            Assert.Equal(bar.Length, inside[0].Length + outside[0].Length, 6);

            // A bar wholly clear of the slab is all outside, and none of it is cut.
            GeoPolylineArc3 clear = bar.Translate(new GeoVector3(0, 0, 5000));

            Assert.False(clear.TrySplitBy(slab, out GeoPolylineArc3[] none, out GeoPolylineArc3[] all));
            Assert.Empty(none);
            Assert.Single(all);
        }

        [Fact]
        public void ABarCutAtAFaceStopsAtItsMaterialAndNotAtItsHoles()
        {
            GeoPolylineArc3 bar = Bar();

            // A wall square to the bar at x = 200, with a hole where the bar passes through.
            var wall = new GeoPolygon3(
                new GeoPoint3(200, -100, -100), new GeoPoint3(200, 300, -100),
                new GeoPoint3(200, 300, 100), new GeoPoint3(200, -100, 100));
            var hole = new GeoPolygon3(
                new GeoPoint3(200, -20, -20), new GeoPoint3(200, 20, -20),
                new GeoPoint3(200, 20, 20), new GeoPoint3(200, -20, 20));

            Assert.True(bar.TrySplitBy(new GeoFace3(wall), out GeoPolylineArc3[] cut));
            Assert.Equal(2, cut.Length);

            // With the hole there, the bar goes straight through and is not cut at all.
            Assert.False(bar.TrySplitBy(new GeoFace3(wall, new[] { hole }), out GeoPolylineArc3[] whole));
            Assert.Single(whole);
            Assert.Equal(bar.Length, whole[0].Length, 6);
        }

        [Fact]
        public void TwoBreaksGiveThreePieces()
        {
            GeoPolylineArc3 bar = Bar();

            Assert.True(bar.TrySplitBy(bar.GetPointAtDistance(100.0), out GeoPolylineArc3[] first));
            Assert.True(first[1].TrySplitBy(first[1].GetPointAtDistance(100.0), out GeoPolylineArc3[] second));

            GeoPolylineArc3[] all = new[] { first[0] }.Concat(second).ToArray();

            Assert.Equal(3, all.Length);
            Assert.Equal(100.0, all[0].Length, 6);
            Assert.Equal(100.0, all[1].Length, 6);
            Assert.Equal(bar.Length, all.Sum(piece => piece.Length), 6);
        }

        [Fact]
        public void EveryCutTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolylineArc3 bar = Bar();
            var plane = new GeoPlane3(new GeoPoint3(200, 0, 0), new GeoVector3(1, 0, 0));
            GeoSolid3 slab = new GeoAabb3(new GeoPoint3(-100, -100, -50), new GeoPoint3(300, 300, 50)).ToObb().ToSolid();

            Assert.Equal(
                bar.TrySplitBy(plane, out GeoPolylineArc3[] a),
                bar.TrySplitBy(plane, out GeoPolylineArc3[] b, global));
            Assert.Equal(a.Length, b.Length);

            Assert.Equal(
                bar.TrySplitAtDistance(100.0, out GeoPolylineArc3[] c),
                bar.TrySplitAtDistance(100.0, out GeoPolylineArc3[] d, global));
            Assert.Equal(c.Length, d.Length);

            Assert.Equal(
                bar.TrySplitBy(slab, out GeoPolylineArc3[] e, out _),
                bar.TrySplitBy(slab, out GeoPolylineArc3[] f, out _, global));
            Assert.Equal(e.Length, f.Length);

            Assert.Throws<ArgumentNullException>(() => ArcChain3.TrySplitBy(null, plane, out _));
            Assert.Throws<ArgumentNullException>(() => ArcChain3.TrySplitAtDistance(null, 1.0, out _));
            Assert.Throws<ArgumentNullException>(() => bar.TrySplitBy((GeoSolid3)null, out _, out _));
        }
    }
}
