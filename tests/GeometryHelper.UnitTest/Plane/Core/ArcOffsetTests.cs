using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Offsetting a shape that curves without straightening it. The arcs have to come back as arcs of the
    /// right radius, and the parts that fold over have to go — which is checked against the straight
    /// offset, the one that goes through Clipper, so the two halves of the library hold each other up.
    /// </summary>
    public class ArcOffsetTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-6, 1E-9);

        /// <summary>
        /// Round joins, cut fine enough that Clipper's approximation of them does not muddy a comparison.
        /// </summary>
        private static readonly OffsetOptions FineRound = new OffsetOptions(OffsetJoin.Round, OffsetOptions.DefaultMiterLimit, 0.0005);

        /// <summary>
        /// A slot 200 long and 50 wide, with a half circle of radius 25 on each end.
        /// </summary>
        private static GeoPolygonArc2 Slot()
        {
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });
        }

        [Fact]
        public void GrowingASlotKeepsItsEndsRoundAndMakesThemBigger()
        {
            GeoPolygonArc2[] grown = Slot().Offset(20.0);

            Assert.Single(grown);

            GeoPolygonArc2 one = grown[0];

            Assert.True(one.HasArcs);

            // Two straight sides of 200, and two half circles that grew from 25 to 45.
            Assert.Equal(400.0 + Math.PI * 90.0, one.Length, 6);
            Assert.Equal(200.0 * 90.0 + Math.PI * 45.0 * 45.0, one.Area, 5);

            foreach (GeoEdge2 edge in one.GetEdges().Where(edge => edge.IsArc))
            {
                Assert.Equal(45.0, edge.ToArc().Radius, 8);
            }

            // It runs the way the loop that made it ran.
            Assert.Equal(Slot().IsClockwise, one.IsClockwise);
        }

        [Fact]
        public void ShrinkingASlotKeepsItsEndsRoundAndMakesThemSmaller()
        {
            GeoPolygonArc2[] shrunk = Slot().Offset(-20.0);

            Assert.Single(shrunk);
            Assert.Equal(200.0 * 10.0 + Math.PI * 25.0, shrunk[0].Area, 5);

            foreach (GeoEdge2 edge in shrunk[0].GetEdges().Where(edge => edge.IsArc))
            {
                Assert.Equal(5.0, edge.ToArc().Radius, 8);
            }

            // Shrunk past its own width, there is nothing left of it.
            Assert.Empty(Slot().Offset(-40.0));
            Assert.Empty(Slot().Offset(-100.0));
        }

        [Theory]
        [InlineData(5.0)]
        [InlineData(20.0)]
        [InlineData(60.0)]
        [InlineData(-5.0)]
        [InlineData(-20.0)]
        public void TheArcOffsetAgreesWithTheStraightOneThroughClipper(double distance)
        {
            GeoPolygonArc2 slot = Slot();

            GeoPolygonArc2[] curved = slot.Offset(distance, OffsetJoin.Round);
            GeoPolygon2[] straight = slot.Flatten(0.0005).Offset(distance, FineRound);

            Assert.Equal(straight.Length, curved.Length);

            double fromArcs = curved.Sum(piece => piece.Area);
            double fromClipper = straight.Sum(piece => piece.Area);

            // The two come at it from opposite directions and have to land in the same place.
            Assert.True(Math.Abs(fromArcs - fromClipper) / fromClipper < 1E-4,
                $"arcs {fromArcs:0.####} against clipper {fromClipper:0.####}");
        }

        [Theory]
        [InlineData(10.0)]
        [InlineData(-10.0)]
        [InlineData(30.0)]
        public void APlateWithRoundedCornersOffsetsAgainstClipperToo(double distance)
        {
            GeoPolygonArc2 plate = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 200), new GeoPoint2(0, 200)))
                .Fillet(40.0);

            Assert.Equal(4, plate.GetEdges().Count(edge => edge.IsArc));

            GeoPolygonArc2[] curved = plate.Offset(distance, OffsetJoin.Round);
            GeoPolygon2[] straight = plate.Flatten(0.0005).Offset(distance, FineRound);

            Assert.Equal(straight.Length, curved.Length);
            Assert.Single(curved);

            // A fillet of 40 offset by d is a fillet of 40 + d, exactly.
            foreach (GeoEdge2 edge in curved[0].GetEdges().Where(edge => edge.IsArc))
            {
                Assert.Equal(40.0 + distance, edge.ToArc().Radius, 6);
            }

            Assert.True(Math.Abs(curved[0].Area - straight[0].Area) / straight[0].Area < 1E-4);
        }

        [Fact]
        public void APinchedShapeFallsIntoTwoPiecesJustAsTheStraightOneDoes()
        {
            // A dumbbell: two wide ends joined by a narrow waist.
            var dumbbell = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 90), new GeoPoint2(120, 90),
                new GeoPoint2(120, 110), new GeoPoint2(200, 110), new GeoPoint2(200, 200), new GeoPoint2(0, 200),
                new GeoPoint2(0, 110), new GeoPoint2(80, 110), new GeoPoint2(80, 90), new GeoPoint2(0, 90));

            var curvedForm = new GeoPolygonArc2(dumbbell);

            GeoPolygonArc2[] curved = curvedForm.Offset(-25.0, OffsetJoin.Round);
            GeoPolygon2[] straight = dumbbell.Offset(-25.0, FineRound);

            // The waist runs from x=80 to x=120, so it is 40 wide and 25 a side parts it in two.
            Assert.Equal(2, straight.Length);
            Assert.Equal(2, curved.Length);
            // The arcs are exact and Clipper's are cut fine, so the two answers meet closely.
            Assert.True(Math.Abs(curved.Sum(p => p.Area) - straight.Sum(p => p.Area)) / straight.Sum(p => p.Area) < 1E-5);
        }

        [Fact]
        public void OffsettingAChainGoesToTheLeftOfTheWayItRuns()
        {
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0) });

            GeoPolylineArc2[] left = chain.Offset(10.0);
            GeoPolylineArc2[] right = chain.Offset(-10.0);

            Assert.Single(left);
            Assert.Single(right);
            Assert.True(left[0][0].IsEqualTo(new GeoPoint2(0, 10), Loose));
            Assert.True(right[0][0].IsEqualTo(new GeoPoint2(0, -10), Loose));

            // The same way round as the straight chains do it.
            var plain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            Assert.True(plain.Offset(10.0)[0][0].IsEqualTo(left[0][0], Loose));
        }

        [Fact]
        public void AnOpenedCornerIsFilledTheWayTheOptionsSay()
        {
            var ell = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) });

            // To the right of the way it runs, the corner opens up.
            GeoPolylineArc2 round = ell.Offset(-20.0, OffsetJoin.Round)[0];
            GeoPolylineArc2 chamfer = ell.Offset(-20.0, OffsetJoin.Chamfer)[0];
            GeoPolylineArc2 miter = ell.Offset(-20.0, OffsetJoin.Miter)[0];

            // Round puts an arc of the offset distance about the corner in the gap.
            GeoEdge2 filled = round.GetEdges().Single(edge => edge.IsArc);
            Assert.Equal(20.0, filled.ToArc().Radius, 8);
            Assert.True(filled.ToArc().Center.IsEqualTo(new GeoPoint2(100, 0), Loose));
            Assert.Equal(Math.PI / 2.0, Math.Abs(filled.ToArc().SweptAngle), 8);

            // Chamfer cuts straight across it, and miter runs both pieces on to where they meet.
            Assert.False(chamfer.HasArcs);
            Assert.False(miter.HasArcs);
            Assert.Contains(miter.Vertices, v => v.IsEqualTo(new GeoPoint2(120, -20), Loose));

            // Round is the longest way round the corner and miter the shortest across it.
            Assert.True(round.Length > chamfer.Length);
            Assert.True(miter.Length > round.Length);
        }

        [Fact]
        public void OffsettingByNothingGivesTheShapeBack()
        {
            GeoPolygonArc2 slot = Slot();

            Assert.True(slot.Offset(0.0)[0].IsEqualTo(slot));

            Assert.Throws<ArgumentNullException>(() => Offset2.Offset((GeoPolygonArc2)null, 10.0));
            Assert.Throws<ArgumentNullException>(() => Offset2.Offset((GeoPolylineArc2)null, 10.0));
            Assert.Throws<ArgumentNullException>(() => Offset2.Offset(slot, 10.0, (OffsetOptions)null));
        }
        [Fact]
        public void AMiterAgainstACurveRunsTheArcOnRoundItsOwnCircle()
        {
            // A straight run into an arc that bends away, offset so the corner opens up.
            var bent = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(140, 60) },
                new[] { 0.0, -0.4 });

            GeoArc2 was = bent.GetEdgeAt(1).ToArc();

            GeoPolylineArc2 mitered = bent.Offset(-20.0, OffsetJoin.Miter)[0];
            GeoPolylineArc2 rounded = bent.Offset(-20.0, OffsetJoin.Round)[0];
            GeoPolylineArc2 chamfered = bent.Offset(-20.0, OffsetJoin.Chamfer)[0];

            // Mitered, the two pieces reach each other, so nothing is put in between them.
            Assert.Equal(2, mitered.EdgeCount);
            Assert.Equal(3, rounded.EdgeCount);
            Assert.Equal(3, chamfered.EdgeCount);

            GeoArc2 now = mitered.GetEdges().Single(edge => edge.IsArc).ToArc();

            // The arc was run on round the circle it was already on: same centre, same radius as the plain
            // offset of it, and more sweep than it had. It runs clockwise, so moving right of the way it
            // goes moves it toward its own centre and the radius shrinks.
            Assert.True(was.TryOffset(-20.0, out GeoArc2 plain));
            Assert.Equal(plain.Radius, now.Radius, 8);
            Assert.True(plain.Center.IsEqualTo(now.Center, Loose));
            Assert.True(Math.Abs(now.SweptAngle) > Math.Abs(plain.SweptAngle));

            // Round bridges the same gap with an arc of the offset distance about the corner.
            GeoEdge2 bridge = rounded.GetEdges().First(edge => edge.IsArc);
            Assert.Equal(20.0, bridge.ToArc().Radius, 8);

            // Round goes the long way round, chamfer the short way across, miter further than either.
            Assert.True(mitered.Length > rounded.Length);
            Assert.True(rounded.Length > chamfered.Length);

            // Where the curves do not reach each other there is no miter, and it says so by cutting across.
            var steep = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(140, 60) },
                new[] { 0.0, -0.9 });

            Assert.Equal(steep.Offset(-20.0, OffsetJoin.Chamfer)[0].Length, steep.Offset(-20.0, OffsetJoin.Miter)[0].Length, 9);
        }
    }
}
