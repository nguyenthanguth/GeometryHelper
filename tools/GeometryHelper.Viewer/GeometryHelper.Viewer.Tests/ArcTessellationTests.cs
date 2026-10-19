using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ArcTessellationTests
    {
        private const double Tenth = 1E-4;

        private static double WorstChordError(Vec3[] ring, Vec3 center, double radius)
        {
            double worst = 0.0;

            for (int i = 0; i < ring.Length; i++)
            {
                Vec3 middle = (ring[i] + ring[(i + 1) % ring.Length]) * 0.5;
                worst = Math.Max(worst, radius - (middle - center).Length);
            }

            return worst;
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.01)]
        [InlineData(0.03)]
        [InlineData(Math.PI / 2.0)]
        [InlineData(-Math.PI)]
        [InlineData(2.0 * Math.PI)]
        public void AnArcOfASmallRadius_IsCutInPiecesOfNoMoreThanTwoDegrees(double sweep)
        {
            var rule = new ChordRule(100.0);

            int pieces = rule.Pieces(sweep, 10.0);

            Assert.True(pieces >= 1);
            Assert.True(Math.Abs(sweep) / pieces <= ChordRule.MaxPieceAngle + 1E-15);
        }

        [Fact]
        public void AFullTurn_IsAlwaysCutInAtLeastSixteenPieces_AndNoCurveGoesPastTheCap()
        {
            Assert.True(new ChordRule(1E9).Pieces(2.0 * Math.PI, 1.0) >= ChordRule.MinPiecesPerTurn);
            Assert.Equal(16, ChordRule.MinPiecesPerTurn);
            Assert.Equal(65536, ChordRule.MaxPiecesPerTurn);
        }

        [Theory]
        [InlineData(10.0)]
        [InlineData(500.0)]
        [InlineData(5000.0)]
        [InlineData(60000.0)]
        public void ACircle_IsCutSoThatNoChordLiesFurtherThanTheToleranceFromIt(double radius)
        {
            var rule = new ChordRule(Tenth);

            Vec3[] ring = ArcTessellation.Circle2(rule, new Vec3(0, 0, 0), radius);

            Assert.False(rule.Capped);
            Assert.True(WorstChordError(ring, new Vec3(0, 0, 0), radius) <= Tenth * (1.0 + 1E-6), "worst " + WorstChordError(ring, new Vec3(0, 0, 0), radius));
        }

        [Fact]
        public void TheNumberOfPiecesOfALargeCircle_FollowsTheChordError()
        {
            var rule = new ChordRule(Tenth);

            // The piece turning through 2 acos(1 - 1E-4 / r): 4 968 a turn at 500, 54 415 at 60 000.
            Assert.InRange(rule.Pieces(2.0 * Math.PI, 500.0), 4967, 4969);
            Assert.InRange(rule.Pieces(2.0 * Math.PI, 60000.0), 54414, 54416);
            Assert.Equal(180, new ChordRule(Tenth).Pieces(2.0 * Math.PI, 0.001));
        }

        [Fact]
        public void ACurveThatWouldTakeMoreThanTheCap_IsCutInTheCapAndTheRuleSaysSo()
        {
            var rule = new ChordRule(Tenth);

            Assert.Equal(65536, rule.Pieces(2.0 * Math.PI, 1E9));
            Assert.True(rule.Capped);

            var half = new ChordRule(Tenth);
            Assert.Equal(32768, half.Pieces(Math.PI, 1E9));
            Assert.True(half.Capped);
        }

        [Fact]
        public void ARuleWithNoToleranceAboveNought_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChordRule(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChordRule(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChordRule(-1.0));
        }

        [Fact]
        public void TheExtremesOfATiltedCircle_AreThoseOfTheExactCurve_WhichTheChordsMiss()
        {
            var normal = new Vec3(0.3, -0.5, 0.8).Normalized();
            var center = new Vec3(100, 200, 300);
            var rule = new ChordRule(10.0);

            Vec3[] ring = ArcTessellation.Circle3(rule, center, normal, 50.0);

            double[] n = { normal.X, normal.Y, normal.Z };
            double[] c = { center.X, center.Y, center.Z };
            Func<Vec3, double>[] axis = { v => v.X, v => v.Y, v => v.Z };

            for (int i = 0; i < 3; i++)
            {
                double reach = 50.0 * Math.Sqrt(1.0 - n[i] * n[i]);
                Assert.Equal(c[i] + reach, rule.Extremes.Max(axis[i]), 9);
                Assert.Equal(c[i] - reach, rule.Extremes.Min(axis[i]), 9);

                // The chords stop short of it at this tolerance, so the box of the chords alone would be too small.
                Assert.True(ring.Max(axis[i]) <= c[i] + reach + 1E-9);
            }
        }

        [Fact]
        public void TheExtremesOfAnArc_AreOnlyThoseItReaches()
        {
            var rule = new ChordRule(Tenth);

            // A quarter from 10 degrees to 100: it reaches the top (90 degrees) but neither the right nor the left.
            ArcTessellation.Arc2(rule, new Vec3(0, 0, 0), 100.0, 10.0 * Math.PI / 180.0, Math.PI / 2.0);

            Assert.Equal(100.0, rule.Extremes.Max(v => v.Y), 9);
            Assert.DoesNotContain(rule.Extremes, v => Math.Abs(v.X - 100.0) < 1E-6 || Math.Abs(v.X + 100.0) < 1E-6);
        }

        [Fact]
        public void AnArcClockwise_ReachesTheSameExtremesAsTheSameArcRunBackwards()
        {
            var forward = new ChordRule(Tenth);
            var backward = new ChordRule(Tenth);

            ArcTessellation.Arc2(forward, new Vec3(0, 0, 0), 100.0, 0.5, 4.0);
            ArcTessellation.Arc2(backward, new Vec3(0, 0, 0), 100.0, 4.5, -4.0);

            Assert.Equal(forward.Extremes.Max(v => v.X), backward.Extremes.Max(v => v.X), 9);
            Assert.Equal(forward.Extremes.Min(v => v.Y), backward.Extremes.Min(v => v.Y), 9);
        }

        [Fact]
        public void AnEllipse_IsCutByTheChordErrorOfItsLongerRadius()
        {
            var rule = new ChordRule(Tenth);

            Vec3[] ring = ArcTessellation.Ellipse(rule, new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), 400.0, 40.0);

            double worst = 0.0;

            for (int i = 0; i < ring.Length; i++)
            {
                Vec3 a = ring[i];
                Vec3 b = ring[(i + 1) % ring.Length];
                Vec3 middle = (a + b) * 0.5;

                // The ellipse's own distance from the chord's middle, along the normal of the ellipse there, by a fine search.
                double best = double.MaxValue;

                for (int k = 0; k <= 400; k++)
                {
                    double t = 2.0 * Math.PI * (i + k / 400.0) / ring.Length;
                    best = Math.Min(best, (middle - new Vec3(400.0 * Math.Cos(t), 40.0 * Math.Sin(t), 0.0)).Length);
                }

                worst = Math.Max(worst, best);
            }

            Assert.True(worst <= Tenth * (1.0 + 1E-6), "worst " + worst);
        }

        [Fact]
        public void ABulgeOfNought_AddsNoPointsBetweenTheVertices()
        {
            Vec3[] chain = ArcTessellation.Chain2(new ChordRule(Tenth), new[] { new Vec3(0, 0, 0), new Vec3(5, 0, 0), new Vec3(5, 5, 0) }, new[] { 0.0, 0.0, 0.0 }, true);

            Assert.Equal(3, chain.Length);
        }

        [Fact]
        public void ABulgeThatIsNotANumber_IsDrawnStraight()
        {
            Vec3[] chain = ArcTessellation.Chain2(new ChordRule(Tenth), new[] { new Vec3(0, 0, 0), new Vec3(5, 0, 0) }, new[] { double.NaN }, false);

            Assert.Equal(2, chain.Length);
        }

        [Fact]
        public void TheEdgeOfAChainThatBulges_IsCutByItsOwnRadius()
        {
            // A semicircle of radius 5000 on a chord 10 000 long.
            var rule = new ChordRule(Tenth);

            Vec3[] chain = ArcTessellation.Chain2(rule, new[] { new Vec3(0, 0, 0), new Vec3(10000, 0, 0) }, new[] { 1.0, 0.0 }, false);

            Assert.True(chain.Length > 1000);

            for (int i = 0; i + 1 < chain.Length; i++)
            {
                Vec3 middle = (chain[i] + chain[i + 1]) * 0.5;
                Assert.True(5000.0 - (middle - new Vec3(5000, 0, 0)).Length <= Tenth * (1.0 + 1E-6));
            }

            Assert.Equal(-5000.0, rule.Extremes.Min(v => v.Y), 6);
        }
    }
}
