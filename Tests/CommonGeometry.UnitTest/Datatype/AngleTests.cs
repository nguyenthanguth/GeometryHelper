using System;
using CommonGeometry;
using CommonGeometry.Datatype;
using Xunit;

namespace CommonGeometry.UnitTest.Datatype
{
    public class AngleTests
    {
        #region Conversion

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(30.0, Math.PI / 6.0)]
        [InlineData(45.0, Math.PI / 4.0)]
        [InlineData(90.0, Math.PI / 2.0)]
        [InlineData(180.0, Math.PI)]
        [InlineData(270.0, 3.0 * Math.PI / 2.0)]
        [InlineData(360.0, 2.0 * Math.PI)]
        [InlineData(-90.0, -Math.PI / 2.0)]
        [InlineData(720.0, 4.0 * Math.PI)]
        public void FromDegreesAndFromRadians_DescribeTheSameAngle(double degrees, double radians)
        {
            var fromDegrees = Angle.FromDegrees(degrees);
            var fromRadians = Angle.FromRadians(radians);

            Assert.Equal(radians, fromDegrees.Radians, 12);
            Assert.Equal(degrees, fromDegrees.Degrees, 12);

            Assert.Equal(radians, fromRadians.Radians, 12);
            Assert.Equal(degrees, fromRadians.Degrees, 12);

            Assert.True(fromDegrees.IsEqualTo(fromRadians));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-37.5)]
        [InlineData(123.456)]
        [InlineData(1e-9)]
        [InlineData(1e9)]
        public void ConvertingBothWays_ReturnsTheOriginalValue(double degrees)
        {
            // The bound has to scale with the magnitude: a double near 1e9 only resolves to about 1e-7, so
            // an absolute criterion cannot hold across the nine orders of magnitude covered here.
            double bound = 1e-12 * Math.Max(1.0, Math.Abs(degrees));

            double viaStatics = Angle.ToDegrees(Angle.ToRadians(degrees));
            Assert.True(Math.Abs(viaStatics - degrees) <= bound, $"static round trip of {degrees} gave {viaStatics}");

            double viaInstance = Angle.FromDegrees(degrees).Degrees;
            Assert.True(Math.Abs(viaInstance - degrees) <= bound, $"instance round trip of {degrees} gave {viaInstance}");
        }

        [Fact]
        public void StaticConverters_MatchTheInstanceProperties()
        {
            Assert.Equal(Angle.FromDegrees(90).Radians, Angle.ToRadians(90), 12);
            Assert.Equal(Angle.FromRadians(Math.PI).Degrees, Angle.ToDegrees(Math.PI), 12);
        }

        [Fact]
        public void Constants_HoldTheExpectedRatios()
        {
            Assert.Equal(180.0 / Math.PI, Angle.DegreesPerRadian, 12);
            Assert.Equal(Math.PI / 180.0, Angle.RadiansPerDegree, 12);
            Assert.Equal(1.0, Angle.DegreesPerRadian * Angle.RadiansPerDegree, 12);
            Assert.Equal(2.0 * Math.PI, Angle.FullTurnRadians, 12);
            Assert.Equal(360.0, Angle.FullTurnDegrees, 12);
        }

        #endregion

        #region Well-known values

        [Fact]
        public void WellKnownValues_AreCorrect()
        {
            Assert.Equal(0.0, Angle.Zero.Degrees, 12);
            Assert.Equal(90.0, Angle.Right.Degrees, 12);
            Assert.Equal(180.0, Angle.Straight.Degrees, 12);
            Assert.Equal(360.0, Angle.FullTurn.Degrees, 12);
        }

        [Fact]
        public void DefaultInstance_IsZero()
        {
            Angle uninitialized = default(Angle);

            Assert.Equal(0.0, uninitialized.Radians);
            Assert.Equal(0.0, uninitialized.Degrees);
            Assert.Equal(Angle.Zero, uninitialized);
        }

        #endregion

        #region Normalization

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(90.0, 90.0)]
        [InlineData(270.0, 270.0)]
        [InlineData(360.0, 0.0)]
        [InlineData(450.0, 90.0)]
        [InlineData(-90.0, 270.0)]      // the turn that reaches the same direction
        [InlineData(-270.0, 90.0)]
        [InlineData(720.0, 0.0)]
        [InlineData(-720.0, 0.0)]
        public void Normalize_WrapsIntoZeroToFullTurn(double degrees, double expected)
        {
            var normalized = Angle.FromDegrees(degrees).Normalize();

            Assert.Equal(expected, normalized.Degrees, 9);
            Assert.InRange(normalized.Radians, 0.0, Angle.FullTurnRadians);
            Assert.True(normalized.Radians < Angle.FullTurnRadians);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(90.0, 90.0)]
        [InlineData(180.0, 180.0)]      // the range is half-open at -PI, so PI stays positive
        [InlineData(-180.0, 180.0)]
        [InlineData(270.0, -90.0)]      // the shortest rotation to the same direction
        [InlineData(-270.0, 90.0)]
        [InlineData(360.0, 0.0)]
        [InlineData(540.0, 180.0)]
        public void NormalizeSigned_WrapsIntoMinusPiToPi(double degrees, double expected)
        {
            var normalized = Angle.FromDegrees(degrees).NormalizeSigned();

            Assert.Equal(expected, normalized.Degrees, 9);
            Assert.True(normalized.Radians > -Math.PI);
            Assert.True(normalized.Radians <= Math.PI);
        }

        [Fact]
        public void Normalize_KeepsAVerySmallNegativeInputInsideTheHalfOpenRange()
        {
            // Shifting a tiny negative value by a full turn rounds to exactly 2*PI, which is outside the
            // range the method promises.
            var normalized = Angle.FromRadians(-1e-20).Normalize();

            Assert.True(normalized.Radians >= 0.0);
            Assert.True(normalized.Radians < Angle.FullTurnRadians);
        }

        [Fact]
        public void NormalizedForms_AgreeOnDirection()
        {
            // 270 degrees and -90 degrees describe the same direction reached by opposite rotations.
            var turn = Angle.FromDegrees(270);
            var shortest = Angle.FromDegrees(-90);

            Assert.Equal(turn.Normalize().Degrees, shortest.Normalize().Degrees, 9);
            Assert.Equal(turn.NormalizeSigned().Degrees, shortest.NormalizeSigned().Degrees, 9);

            // But as rotation amounts they remain different.
            Assert.False(turn.IsEqualTo(shortest));
        }

        #endregion

        #region Arithmetic

        [Fact]
        public void Arithmetic_WorksThroughMethodsAndOperators()
        {
            var a = Angle.FromDegrees(90);
            var b = Angle.FromDegrees(30);

            Assert.Equal(120.0, a.Add(b).Degrees, 9);
            Assert.Equal(120.0, (a + b).Degrees, 9);

            Assert.Equal(60.0, a.Subtract(b).Degrees, 9);
            Assert.Equal(60.0, (a - b).Degrees, 9);

            Assert.Equal(180.0, a.Multiply(2.0).Degrees, 9);
            Assert.Equal(180.0, (a * 2.0).Degrees, 9);
            Assert.Equal(180.0, (2.0 * a).Degrees, 9);

            Assert.Equal(45.0, a.Divide(2.0).Degrees, 9);
            Assert.Equal(45.0, (a / 2.0).Degrees, 9);

            Assert.Equal(-90.0, a.Negate().Degrees, 9);
            Assert.Equal(-90.0, (-a).Degrees, 9);

            Assert.Equal(90.0, a.Negate().Abs().Degrees, 9);
        }

        #endregion

        #region Comparison and equality

        [Fact]
        public void Equality_ComparesTheRawValue()
        {
            var a = Angle.FromDegrees(90);
            var b = Angle.FromRadians(Math.PI / 2.0);
            var c = Angle.FromDegrees(45);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a != c);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            Assert.True(a.Equals((object)b));
            Assert.False(a.Equals("not an angle"));
        }

        [Fact]
        public void IsEqualTo_UsesTheAngularTolerance()
        {
            var a = Angle.FromDegrees(90.0);
            var justUnder = Angle.FromDegrees(90.5);   // half a degree, inside the 1 degree default
            var justOver = Angle.FromDegrees(92.0);

            Assert.True(a.IsEqualTo(justUnder));
            Assert.False(a.IsEqualTo(justOver));

            // A wider tolerance brings it back in.
            var wide = new Tolerance(1e-4, 1e-4, Angle.ToRadians(5.0));
            Assert.True(a.IsEqualTo(justOver, wide));
        }

        [Fact]
        public void IsEqualTo_DoesNotTreatAFullTurnAsZero()
        {
            // These are the same direction but different rotation amounts, so they are not equal until
            // both sides are normalized.
            var zero = Angle.Zero;
            var fullTurn = Angle.FullTurn;

            Assert.False(zero.IsEqualTo(fullTurn));
            Assert.True(zero.Normalize().IsEqualTo(fullTurn.Normalize()));
        }

        [Fact]
        public void CompareTo_OrdersByRotationAmount()
        {
            var quarter = Angle.FromDegrees(90);
            var half = Angle.FromDegrees(180);
            var full = Angle.FromDegrees(360);

            Assert.True(quarter < half);
            Assert.True(half < full);
            Assert.True(full > quarter);
            Assert.True(quarter <= Angle.FromDegrees(90));
            Assert.True(quarter >= Angle.FromDegrees(90));

            Assert.Equal(-1, quarter.CompareTo(half));
            Assert.Equal(1, half.CompareTo(quarter));
            Assert.Equal(0, quarter.CompareTo(Angle.FromDegrees(90)));
        }

        [Fact]
        public void Sorting_UsesTheComparableImplementation()
        {
            var angles = new[]
            {
                Angle.FromDegrees(180),
                Angle.FromDegrees(-90),
                Angle.FromDegrees(45)
            };

            Array.Sort(angles);

            Assert.Equal(-90.0, angles[0].Degrees, 9);
            Assert.Equal(45.0, angles[1].Degrees, 9);
            Assert.Equal(180.0, angles[2].Degrees, 9);
        }

        #endregion

        #region Formatting

        [Fact]
        public void ToString_ShowsBothUnitsInInvariantCulture()
        {
            Assert.Equal("90.000 deg (1.5708 rad)", Angle.FromDegrees(90).ToString());
            Assert.Equal("-45.000 deg (-0.7854 rad)", Angle.FromDegrees(-45).ToString());
        }

        #endregion

        #region Cases that came from the SolidGeometry copy of this type

        [Fact]
        public void RadiansAndDegreesAreTwoViewsOfOneValue()
        {
            Angle right = Angle.FromDegrees(90.0);

            Assert.Equal(Math.PI / 2.0, right.Radians, 12);
            Assert.Equal(90.0, right.Degrees, 12);
            Assert.Equal(right.Radians, Angle.FromRadians(Math.PI / 2.0).Radians, 12);
        }

        [Fact]
        public void WellKnownValuesAreWhereTheySay()
        {
            Assert.Equal(0.0, Angle.Zero.Degrees, 12);
            Assert.Equal(90.0, Angle.Right.Degrees, 12);
            Assert.Equal(180.0, Angle.Straight.Degrees, 12);
            Assert.Equal(360.0, Angle.FullTurn.Degrees, 12);
        }

        [Fact]
        public void StaticConversionMatchesTheInstanceView()
        {
            Assert.Equal(180.0, Angle.ToDegrees(Math.PI), 12);
            Assert.Equal(Math.PI, Angle.ToRadians(180.0), 12);
        }

        [Theory]
        [InlineData(-90.0, 270.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(45.0, 45.0)]
        [InlineData(360.0, 0.0)]
        [InlineData(450.0, 90.0)]
        [InlineData(-450.0, 270.0)]
        public void NormalizeWrapsIntoAFullTurn(double input, double expected)
        {
            Assert.Equal(expected, Angle.FromDegrees(input).Normalize().Degrees, 9);
        }

        [Fact]
        public void NormalizeAlwaysLandsInsideTheHalfOpenRange()
        {
            double[] samples = { -1E-15, -1E-9, -360.0, 359.9999999, 720.0, -720.0 };

            foreach (double sample in samples)
            {
                double wrapped = Angle.FromDegrees(sample).Normalize().Radians;

                Assert.True(wrapped >= 0.0);
                Assert.True(wrapped < Angle.FullTurnRadians);
            }
        }

        [Theory]
        [InlineData(270.0, -90.0)]
        [InlineData(180.0, 180.0)]
        [InlineData(-180.0, 180.0)]
        [InlineData(45.0, 45.0)]
        [InlineData(-270.0, 90.0)]
        public void NormalizeSignedGivesTheShortestRotation(double input, double expected)
        {
            Assert.Equal(expected, Angle.FromDegrees(input).NormalizeSigned().Degrees, 9);
        }

        [Fact]
        public void ArithmeticWorksOnTheStoredValue()
        {
            Angle a = Angle.FromDegrees(30.0);
            Angle b = Angle.FromDegrees(45.0);

            Assert.Equal(75.0, a.Add(b).Degrees, 9);
            Assert.Equal(-15.0, a.Subtract(b).Degrees, 9);
            Assert.Equal(90.0, a.Multiply(3.0).Degrees, 9);
            Assert.Equal(15.0, a.Divide(2.0).Degrees, 9);
            Assert.Equal(-30.0, a.Negate().Degrees, 9);
            Assert.Equal(30.0, a.Negate().Abs().Degrees, 9);
        }

        [Fact]
        public void OperatorsMatchTheNamedMethods()
        {
            Angle a = Angle.FromDegrees(30.0);
            Angle b = Angle.FromDegrees(45.0);

            Assert.Equal(a.Add(b), a + b);
            Assert.Equal(a.Subtract(b), a - b);
            Assert.Equal(a.Multiply(2.0), a * 2.0);
            Assert.Equal(a.Multiply(2.0), 2.0 * a);
            Assert.Equal(a.Divide(2.0), a / 2.0);
            Assert.Equal(a.Negate(), -a);
        }

        [Fact]
        public void ComparisonFollowsTheStoredValue()
        {
            Angle small = Angle.FromDegrees(10.0);
            Angle large = Angle.FromDegrees(20.0);

            Assert.True(small < large);
            Assert.True(large > small);
            Assert.True(small <= large);
            Assert.True(large >= small);
            Assert.Equal(-1, small.CompareTo(large));
            Assert.Equal(0, small.CompareTo(Angle.FromDegrees(10.0)));
        }

        [Fact]
        public void ToleranceEqualityUsesTheAngularThreshold()
        {
            Angle a = Angle.FromDegrees(45.0);
            Angle b = Angle.FromDegrees(45.5);

            // The default angular threshold is one degree.
            Assert.True(a.IsEqualTo(b));
            Assert.False(a.IsEqualTo(Angle.FromDegrees(47.0)));
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void EqualityComparesRotationsNotDirections()
        {
            // Zero and a full turn point the same way but are different rotations, so they are not equal
            // until both are normalized.
            Assert.False(Angle.Zero.IsEqualTo(Angle.FullTurn));
            Assert.True(Angle.Zero.Normalize().IsEqualTo(Angle.FullTurn.Normalize()));
        }

        [Fact]
        public void EqualHashCodesFollowEqualValues()
        {
            Angle a = Angle.FromDegrees(33.0);
            Angle b = Angle.FromRadians(33.0 * Angle.RadiansPerDegree);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a == b);
        }
    

        #endregion
}
}
