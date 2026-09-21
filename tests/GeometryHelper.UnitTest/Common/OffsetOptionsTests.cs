using System;
using GeometryHelper;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    public class OffsetOptionsTests
    {
        [Fact]
        public void Default_GivesSharpCornersWithTheDocumentedLimit()
        {
            Assert.Equal(OffsetJoin.Miter, OffsetOptions.Default.Join);
            Assert.Equal(OffsetOptions.DefaultMiterLimit, OffsetOptions.Default.MiterLimit);
            Assert.Equal(10.0, OffsetOptions.DefaultMiterLimit);
            Assert.Equal(0.0, OffsetOptions.Default.ArcTolerance);
        }

        [Fact]
        public void Constructor_StoresEverySetting()
        {
            var options = new OffsetOptions(OffsetJoin.Round, 3.5, 0.25);

            Assert.Equal(OffsetJoin.Round, options.Join);
            Assert.Equal(3.5, options.MiterLimit);
            Assert.Equal(0.25, options.ArcTolerance);
        }

        [Theory]
        [InlineData(0.999)]
        [InlineData(0.0)]
        [InlineData(-2.0)]
        [InlineData(double.NaN)]
        public void MiterLimit_BelowOneOrNotANumber_IsRefused(double limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OffsetOptions(OffsetJoin.Miter, limit));
        }

        [Fact]
        public void MiterLimit_OfInfinity_KeepsEveryCornerSharp()
        {
            var options = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);

            Assert.True(double.IsPositiveInfinity(options.MiterLimit));
        }

        [Theory]
        [InlineData(-0.001)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void ArcTolerance_NegativeOrNotFinite_IsRefused(double arcTolerance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OffsetOptions(OffsetJoin.Round, arcTolerance: arcTolerance));
        }

        [Fact]
        public void UnknownJoin_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OffsetOptions((OffsetJoin)42));
        }

        [Fact]
        public void GetArcTolerance_FollowsTheDistanceWhenAutomatic()
        {
            var automatic = new OffsetOptions(OffsetJoin.Round);
            var fixedTolerance = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.5);

            // Automatic: a share of the distance, whichever way the offset goes.
            Assert.Equal(0.2, automatic.GetArcTolerance(100.0), 12);
            Assert.Equal(0.2, automatic.GetArcTolerance(-100.0), 12);

            // Set explicitly: the same whatever the distance.
            Assert.Equal(0.5, fixedTolerance.GetArcTolerance(100.0));
            Assert.Equal(0.5, fixedTolerance.GetArcTolerance(1.0));
        }

        [Fact]
        public void Equality_ComparesEverySetting()
        {
            var a = new OffsetOptions(OffsetJoin.Chamfer, 4.0, 0.1);
            var b = new OffsetOptions(OffsetJoin.Chamfer, 4.0, 0.1);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, new OffsetOptions(OffsetJoin.Round, 4.0, 0.1));
            Assert.NotEqual(a, new OffsetOptions(OffsetJoin.Chamfer, 5.0, 0.1));
            Assert.NotEqual(a, new OffsetOptions(OffsetJoin.Chamfer, 4.0, 0.2));
            Assert.False(a.Equals(null));
        }

        [Fact]
        public void ReadmeSamples_HoldAsWritten()
        {
            OffsetOptions sharp = OffsetOptions.Default;
            var cut = new OffsetOptions(OffsetJoin.Miter, miterLimit: 2.0);
            var always = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);
            var round = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.5);

            Assert.Equal(OffsetJoin.Miter, sharp.Join);
            Assert.Equal(10.0, sharp.MiterLimit);
            Assert.Equal(2.0, cut.MiterLimit);
            Assert.Equal(double.PositiveInfinity, always.MiterLimit);
            Assert.Equal(0.5, round.GetArcTolerance(100.0));

            // "10 keeps every corner of 11.5 degrees or wider sharp": a miter reaches 1 / sin(half the
            // corner) times the distance from its vertex.
            Assert.True(1.0 / Math.Sin(11.5 * Math.PI / 180.0 / 2.0) < OffsetOptions.DefaultMiterLimit);
            Assert.True(1.0 / Math.Sin(11.4 * Math.PI / 180.0 / 2.0) > OffsetOptions.DefaultMiterLimit);

            // "0.2 % of the offset distance, about 50 segments for a full turn": a chord of a unit circle
            // within that sagitta spans 2 * acos(1 - 0.002).
            double segments = 2.0 * Math.PI / (2.0 * Math.Acos(1.0 - OffsetOptions.AutomaticArcToleranceRatio));
            Assert.InRange(segments, 45.0, 55.0);
            Assert.Equal(0.002, OffsetOptions.Default.GetArcTolerance(1.0));
        }

        [Fact]
        public void ToString_NamesTheAutomaticArcTolerance()
        {
            Assert.Contains("automatic", OffsetOptions.Default.ToString());
            Assert.Contains("Round", new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.5).ToString());
        }
    }
}
