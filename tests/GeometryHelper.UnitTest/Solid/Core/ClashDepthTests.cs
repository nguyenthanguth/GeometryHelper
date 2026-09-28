using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// How deep two parts run into each other, and the options that take a shallow or small overlap as touching.
    /// </summary>
    public class ClashDepthTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 10);

        // A bar-like block lying a millimetre into the top of the plate, 60 long.
        private static GeoSolid3 Graze() => Box(20, 40, 9, 80, 50, 20);

        // A pin through the plate: a region 10 x 10 x 10.
        private static GeoSolid3 Pin() => Box(40, 60, -20, 50, 70, 30);

        [Fact]
        public void AGraze_IsAsDeepAsItReaches_HoweverLong()
        {
            ClashResult graze = Assert.Single(Clash3.Find(new[] { Plate() }, new[] { Graze() }));

            Assert.Equal(ClashKind.Hard, graze.Kind);
            Assert.Equal(600.0, graze.Volume, 6);
            Assert.Equal(1.0, graze.Depth, 6);
        }

        [Fact]
        public void APinThroughAPlate_IsAsDeepAsTheThinnerOfTheTwo()
        {
            ClashResult pin = Assert.Single(Clash3.Find(new[] { Plate() }, new[] { Pin() }));

            Assert.Equal(1000.0, pin.Volume, 6);
            Assert.Equal(10.0, pin.Depth, 6);
        }

        [Fact]
        public void TwoRegions_ReportTheDeeper()
        {
            // Two plates; a beam runs through the first, 10 thick there, and a block beside it grazes the top of the
            // second by 4.
            Assert.True(Box(0, 0, 0, 30, 100, 30).TryUnion(Box(70, 0, 0, 100, 100, 30), out GeoSolid3 plates));
            Assert.True(Box(-10, 40, 10, 50, 60, 20).TryUnion(Box(75, 40, 26, 95, 60, 40), out GeoSolid3 beamAndBlock));

            ClashResult clash = Assert.Single(Clash3.Find(new[] { plates }, new[] { beamAndBlock }));

            Assert.Equal(2, clash.Overlaps.Count);
            Assert.Equal(10.0, clash.Depth, 6);
        }

        [Fact]
        public void AMinimumDepth_TakesAShallowOverlapAsTouching_AndKeepsWhatItFound()
        {
            ClashResult[] found = Clash3.Find(new[] { Plate(), Plate().Translate(new GeoVector3(0, 0, 1000)) }, new[] { Graze(), Pin() }, new ClashOptions(minimumDepth: 2.0));

            ClashResult graze = found.Single(r => r.Second == 0);
            Assert.Equal(ClashKind.Touch, graze.Kind);
            Assert.Equal(600.0, graze.Volume, 6);
            Assert.Equal(1.0, graze.Depth, 6);
            Assert.Single(graze.Overlaps);
            Assert.Empty(graze.Contact);

            Assert.Equal(ClashKind.Hard, found.Single(r => r.Second == 1).Kind);
        }

        [Fact]
        public void AMinimumDepth_WithoutTouching_LeavesAShallowOverlapOut()
        {
            ClashResult[] found = Clash3.Find(new[] { Plate() }, new[] { Graze(), Pin() }, new ClashOptions(includeTouching: false, minimumDepth: 2.0));

            ClashResult pin = Assert.Single(found);
            Assert.Equal(1, pin.Second);
            Assert.Equal(ClashKind.Hard, pin.Kind);
        }

        [Fact]
        public void AMinimumVolume_TakesASmallOverlapAsTouching()
        {
            ClashResult[] found = Clash3.Find(new[] { Plate() }, new[] { Graze(), Pin() }, new ClashOptions(minimumVolume: 1000.0));

            Assert.Equal(ClashKind.Touch, found.Single(r => r.Second == 0).Kind);
            Assert.Equal(ClashKind.Hard, found.Single(r => r.Second == 1).Kind);
        }

        [Fact]
        public void NoMinimum_ReportsEveryOverlap_AsBefore()
        {
            ClashResult[] found = Clash3.Find(new[] { Plate() }, new[] { Graze(), Pin() });

            Assert.All(found, r => Assert.Equal(ClashKind.Hard, r.Kind));
        }

        [Theory]
        [InlineData(-1.0, 0.0)]
        [InlineData(double.NaN, 0.0)]
        [InlineData(0.0, -1.0)]
        [InlineData(0.0, double.PositiveInfinity)]
        public void AMinimumThatIsNotANumberOfNoughtOrMore_IsRefused(double depth, double volume)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashOptions(minimumDepth: depth, minimumVolume: volume));
        }

        [Fact]
        public void TheConstructorWithoutTheMinimums_StaysForCodeBuiltAgainstIt()
        {
            // A program built against 6.0.0 calls this very constructor; without it, it stops with MissingMethodException.
            Assert.NotNull(typeof(ClashOptions).GetConstructor(new[] { typeof(double), typeof(bool), typeof(int) }));

            var before = new ClashOptions(5.0, false, 2);

            Assert.Equal(new ClashOptions(clearance: 5.0, includeTouching: false, maxDegreeOfParallelism: 2), before);
            Assert.Equal(0.0, before.MinimumDepth);
            Assert.Equal(0.0, before.MinimumVolume);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClashOptions(-1.0, true, -1));
        }

        [Fact]
        public void TheMinimumsTakePartInEquality()
        {
            var a = new ClashOptions(clearance: 5, minimumDepth: 1, minimumVolume: 2);
            var b = new ClashOptions(clearance: 5, minimumDepth: 1, minimumVolume: 2);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, new ClashOptions(clearance: 5, minimumDepth: 1.5, minimumVolume: 2));
            Assert.NotEqual(a, new ClashOptions(clearance: 5, minimumDepth: 1, minimumVolume: 3));
            Assert.Contains("MinimumDepth: 1", a.ToString());
            Assert.Contains("MinimumVolume: 2", a.ToString());
        }
    }
}
