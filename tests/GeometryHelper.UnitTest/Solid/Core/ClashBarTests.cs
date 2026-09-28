using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Bars checked by their centre lines: what runs into them, how deep, and how much of the centre line runs inside,
    /// beside the same bars built as bodies.
    /// </summary>
    public class ClashBarTests
    {
        private const double Radius = 4.0;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 10);

        private static ClashBar Straight(double x0, double y0, double z0, double x1, double y1, double z1)
            => new ClashBar(new GeoPolyline3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)), Radius);

        private static ClashResult One(ClashBar bar, GeoSolid3 part, ClashOptions options = null)
            => Clash3.Find(new[] { bar }, new[] { part }, options ?? ClashOptions.Default).SingleOrDefault();

        [Fact]
        public void ABarThroughAPlate_RunsInsideForTheThickness_AndIsInToItsDiameter()
        {
            ClashResult through = One(Straight(50, 50, -20, 50, 50, 30), Plate());

            Assert.Equal(ClashKind.Hard, through.Kind);
            Assert.Equal(10.0, through.LengthInside, 9);
            Assert.Equal(2 * Radius, through.Depth, 9);
            Assert.True(through.Location.DistanceTo(new GeoPoint3(50, 50, 5)) < 1E-9, $"{through.Location}");
            Assert.Empty(through.Overlaps);
            Assert.Equal(0.0, through.Volume);
        }

        [Fact]
        public void APlateThinnerThanTheBar_CutsItThrough_AndIsTheRadiusAndHalfThePlateDeep()
        {
            // A plate 4 thick across a bar 8 wide: the centre line runs 2 beneath the plate's faces at the middle, so the
            // plate reaches 4 + 2 into the bar. The same bar built as a body reads the least thickness of the region they
            // share, the plate's own 4.
            ClashResult through = One(Straight(50, 50, -20, 50, 50, 30), Box(0, 0, 0, 100, 100, 4));

            Assert.Equal(ClashKind.Hard, through.Kind);
            Assert.Equal(4.0, through.LengthInside, 9);
            Assert.Equal(Radius + 2.0, through.Depth, 9);
        }

        [Fact]
        public void ABarDippingIntoAPlate_IsAsDeepAsItsTurnGets()
        {
            // Down to 8 and back up, turning 2 beneath the top of the plate: that turn is where the bar runs deepest.
            var dip = new ClashBar(new GeoPolyline3(new GeoPoint3(0, 50, 40), new GeoPoint3(50, 50, 8), new GeoPoint3(100, 50, 40)), Radius);

            ClashResult clash = One(dip, Plate());

            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.Equal(Radius + 2.0, clash.Depth, 9);
            Assert.Equal(2 * (2.0 / 32.0) * Math.Sqrt(50 * 50 + 32 * 32), clash.LengthInside, 9);
        }

        [Fact]
        public void ABarThroughAPlateAtASlant_RunsInsideForTheSlantLength()
        {
            // Through at 45 degrees in X and Z: the centre line spends the thickness times the square root of two inside.
            ClashResult slant = One(Straight(20, 50, -20, 60, 50, 20), Plate());

            Assert.Equal(ClashKind.Hard, slant.Kind);
            Assert.Equal(10.0 * Math.Sqrt(2.0), slant.LengthInside, 9);
        }

        [Fact]
        public void ABarGrazingAPlate_IsAsDeepAsItReaches_Exactly()
        {
            // The centre line 2 above the plate: the bar reaches 2 into it, along its whole run.
            ClashResult graze = One(Straight(-10, 50, 12, 110, 50, 12), Plate());

            Assert.Equal(ClashKind.Hard, graze.Kind);
            Assert.Equal(2.0, graze.Depth, 9);
            Assert.Equal(0.0, graze.LengthInside);
        }

        [Fact]
        public void ABarJustAsFarAsItsRadius_Touches()
        {
            ClashResult touch = One(Straight(-10, 50, 14, 110, 50, 14), Plate());

            Assert.Equal(ClashKind.Touch, touch.Kind);
            Assert.Equal(10.0, touch.Location.Z, 9);
        }

        [Fact]
        public void ABarWithinTheClearance_IsTooNear_ItsGapFromItsSurface()
        {
            ClashResult near = One(Straight(-10, 50, 20, 110, 50, 20), Plate(), new ClashOptions(clearance: 25));

            Assert.Equal(ClashKind.Clearance, near.Kind);
            Assert.Equal(6.0, near.Distance, 9);
            Assert.True(near.Gap.HasValue);
            Assert.Equal(16.0, near.Gap.Value.StartPoint.Z, 9);
            Assert.Equal(10.0, near.Gap.Value.EndPoint.Z, 9);
        }

        [Fact]
        public void ABarBeyondTheClearance_IsNoClash()
        {
            Assert.Null(One(Straight(-10, 50, 50, 110, 50, 50), Plate(), new ClashOptions(clearance: 25)));
        }

        [Fact]
        public void ABarThroughAHoleItClears_IsNoClash_ButMayBeTooNear()
        {
            // A hole 10 across, a bar 6 across down its middle: 2 clear of every wall.
            GeoSolid3 plate = Plate().WithOpenings(new[] { Box(45, 45, -5, 55, 55, 15) });
            var bar = new ClashBar(new GeoPolyline3(new GeoPoint3(50, 50, -20), new GeoPoint3(50, 50, 30)), 3.0);

            Assert.Null(One(bar, plate));

            ClashResult near = One(bar, plate, new ClashOptions(clearance: 25));
            Assert.Equal(ClashKind.Clearance, near.Kind);
            Assert.Equal(2.0, near.Distance, 9);
        }

        [Fact]
        public void ABentBar_RunsInsideWhereItsLegCrossesThePlate()
        {
            // Along the top at 20, bent down through the plate; the bend is an arc, ending at 15, above the plate.
            GeoPolylineArc3 hook = new GeoPolyline3(new GeoPoint3(10, 50, 20), new GeoPoint3(60, 50, 20), new GeoPoint3(60, 50, -30)).Fillet(5);

            ClashResult bent = One(new ClashBar(hook, Radius), Plate());

            Assert.Equal(ClashKind.Hard, bent.Kind);
            Assert.Equal(10.0, bent.LengthInside, 6);
            Assert.Equal(2 * Radius, bent.Depth, 9);
        }

        [Fact]
        public void AShallowBar_UnderAMinimumDepth_Touches_AndKeepsItsDepth()
        {
            ClashResult graze = One(Straight(-10, 50, 12, 110, 50, 12), Plate(), new ClashOptions(minimumDepth: 3));

            Assert.Equal(ClashKind.Touch, graze.Kind);
            Assert.Equal(2.0, graze.Depth, 9);

            Assert.Null(One(Straight(-10, 50, 12, 110, 50, 12), Plate(), new ClashOptions(includeTouching: false, minimumDepth: 3)));
        }

        [Fact]
        public void AMinimumVolume_DoesNotApplyToBars_WhoseVolumeIsNotMeasured()
        {
            ClashResult through = One(Straight(50, 50, -20, 50, 50, 30), Plate(), new ClashOptions(minimumVolume: 1E9));

            Assert.Equal(ClashKind.Hard, through.Kind);
        }

        [Fact]
        public void ABendRunningIntoThePlate_CountsTheArcInside()
        {
            // A bend of 12 ends at 8, inside the plate: the centre line enters on the arc, a little longer than straight.
            GeoPolylineArc3 hook = new GeoPolyline3(new GeoPoint3(10, 50, 20), new GeoPoint3(60, 50, 20), new GeoPoint3(60, 50, -30)).Fillet(12);
            double arc = 12 * Math.Asin(2.0 / 12);

            var bar = new ClashBar(hook, Radius);
            ClashResult bent = One(bar, Plate());

            // The bend is followed by chords, a thousandth of the radius from it: what is measured on it is that close.
            Assert.True(Math.Abs(bent.LengthInside - (8 + arc)) <= bar.ChordTolerance, $"{bent.LengthInside} against {8 + arc}");
            Assert.Equal(Radius * 1E-3, bar.ChordTolerance, 12);
        }

        [Fact]
        public void AtExactlyTheRadius_TheCentreLineTouches_WhereTheBuiltBarIsAChordShy()
        {
            // Built as a body, the bar is a polygon inside its round, a flat side up to the chord tolerance in: it clears
            // the plate by less than that. Read by its centre line it is round, and touches.
            var line = new GeoPolyline3(new GeoPoint3(-40, 70, 14), new GeoPoint3(140, 70, 14));
            var options = new ClashOptions(clearance: 10);

            ClashResult byCentreLine = One(new ClashBar(line, Radius), Plate(), options);
            ClashResult byBody = Clash3.Find(new[] { GeoSolid3.Pipe(line, Radius, 0.01) }, new[] { Plate() }, options).Single();

            Assert.Equal(ClashKind.Touch, byCentreLine.Kind);
            Assert.True(byBody.Kind == ClashKind.Touch || byBody.Kind == ClashKind.Clearance && byBody.Distance <= 0.01, byBody.ToString());
        }

        [Fact]
        public void TheEnds_AreReadRounded_SoABarStoppingJustShortOfAPlateClashes()
        {
            // The bar stops 2 below the plate. Built as a body it ends flat and is 2 clear; read by its centre line its
            // end is rounded and reaches 4, so it is found 2 into the plate: the check errs on the side of reporting.
            ClashBar bar = Straight(50, 50, -60, 50, 50, -2);
            GeoSolid3 pipe = GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(50, 50, -60), new GeoPoint3(50, 50, -2)), Radius, 0.01);

            ClashResult byCentreLine = One(bar, Plate(), new ClashOptions(clearance: 25));
            ClashResult byBody = Clash3.Find(new[] { pipe }, new[] { Plate() }, new ClashOptions(clearance: 25)).Single();

            Assert.Equal(ClashKind.Hard, byCentreLine.Kind);
            Assert.Equal(2.0, byCentreLine.Depth, 9);
            Assert.Equal(ClashKind.Clearance, byBody.Kind);
            Assert.Equal(2.0, byBody.Distance, 6);
        }

        [Fact]
        public void BarsAndParts_AreIndexedInTheOrderGiven()
        {
            ClashBar[] bars =
            {
                Straight(-10, 50, 50, 110, 50, 50),       // 0: clear of everything
                Straight(50, 50, -20, 50, 50, 30),        // 1: through the first plate
                Straight(250, 50, -20, 250, 50, 30),      // 2: through the second
                Straight(-10, 50, 12, 310, 50, 12),       // 3: grazing both
            };
            GeoSolid3[] parts = { Plate(), Box(200, 0, 0, 300, 100, 10) };

            ClashResult[] found = Clash3.Find(bars, parts);

            Assert.Equal(new[] { (1, 0), (2, 1), (3, 0), (3, 1) }, found.Select(r => (r.First, r.Second)));
            Assert.All(found, r => Assert.Equal(ClashKind.Hard, r.Kind));
        }

        /// <summary>
        /// The same bars built as bodies, a hundredth of a millimetre inside their round, are found clashing with the
        /// same parts in the same way, where no end of a bar comes near a part and none lies exactly a radius off one.
        /// </summary>
        [Fact]
        public void AgreesWithTheBarsBuiltAsBodies_AwayFromTheirEnds()
        {
            GeoPolyline3[] lines =
            {
                new GeoPolyline3(new GeoPoint3(50, 50, -40), new GeoPoint3(50, 50, 40)),                  // through
                new GeoPolyline3(new GeoPoint3(-40, 30, 12.5), new GeoPoint3(140, 30, 12.5)),             // grazing by 1.5
                new GeoPolyline3(new GeoPoint3(-40, 50, 16), new GeoPoint3(140, 50, 16)),                 // 2 clear
                new GeoPolyline3(new GeoPoint3(-40, 90, 19), new GeoPoint3(140, 90, 19)),                 // 5 clear
                new GeoPolyline3(new GeoPoint3(20, -40, 30), new GeoPoint3(20, 60, 30), new GeoPoint3(20, 60, -40)), // bent down through
            };

            ClashBar[] bars = lines.Select(l => new ClashBar(l, Radius)).ToArray();
            GeoSolid3[] pipes = lines.Select(l => GeoSolid3.Pipe(l, Radius, 0.01)).ToArray();
            var options = new ClashOptions(clearance: 10);

            ClashResult[] byCentreLine = Clash3.Find(bars, new[] { Plate() }, options);
            ClashResult[] byBody = Clash3.Find(pipes, new[] { Plate() }, options);

            Assert.Equal(byBody.Select(r => (r.First, r.Kind)), byCentreLine.Select(r => (r.First, r.Kind)));
            Assert.Equal(1.5, byCentreLine.Single(r => r.First == 1).Depth, 9);
            Assert.Equal(1.5, byBody.Single(r => r.First == 1).Depth, 1);
            Assert.Equal(5.0, byCentreLine.Single(r => r.First == 3).Distance, 9);
            Assert.Equal(5.0, byBody.Single(r => r.First == 3).Distance, 1);
        }

        [Fact]
        public void NullsAndBadRadii_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Clash3.Find((ClashBar[])null, new[] { Plate() }));
            Assert.Throws<ArgumentNullException>(() => Clash3.Find(new ClashBar[] { null }, new[] { Plate() }));
            Assert.Throws<ArgumentNullException>(() => new ClashBar((GeoPolylineArc3)null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashBar(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashBar(new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0))), 1, double.NaN));
        }
    }
}
