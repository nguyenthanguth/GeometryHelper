using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Datatype;
using GeometryHelper.CommonGeometry.Extension;
using Xunit;

namespace GeometryHelper.CommonGeometry.UnitTest
{
    /// <summary>
    /// Invariants over the shared types. An angle is a number that knows its unit, so what must hold is
    /// that converting it and back changes nothing, and that normalising lands in the range it promises.
    /// A tolerance is a set of thresholds, so what must hold is that they decide consistently.
    /// </summary>
    public class InvariantSweepTests
    {
        [Fact]
        public void AnAngleSurvivesEveryRoundTrip()
        {
            Random rng = new Random(1357);

            for (int t = 0; t < 500; t++)
            {
                double degrees = rng.Next(-100000, 100001) / 7.0;

                Angle fromDegrees = Angle.FromDegrees(degrees);
                Assert.Equal(degrees, fromDegrees.Degrees, 9);

                double radians = fromDegrees.Radians;
                Assert.Equal(radians, Angle.FromRadians(radians).Radians, 12);

                // The static conversions and the type must agree.
                Assert.Equal(Angle.ToRadians(degrees), radians, 12);
                Assert.Equal(Angle.ToDegrees(radians), degrees, 9);
            }
        }

        [Fact]
        public void NormalisingLandsInTheRangeItPromises()
        {
            Random rng = new Random(2468);

            for (int t = 0; t < 500; t++)
            {
                Angle raw = Angle.FromRadians(rng.Next(-100000, 100001) / 137.0);

                Angle turned = raw.Normalize();
                Angle signed = raw.NormalizeSigned();

                // Normalize lands in [0, 2*PI); NormalizeSigned lands in (-PI, PI].
                Assert.InRange(turned.Radians, 0.0, Angle.FullTurnRadians);
                Assert.True(turned.Radians < Angle.FullTurnRadians, $"{turned.Radians} reached a full turn");

                Assert.True(signed.Radians > -Math.PI - 1E-12, $"{signed.Radians} fell below -PI");
                Assert.True(signed.Radians <= Math.PI + 1E-12, $"{signed.Radians} rose above PI");

                // Normalising changes the number by a whole number of turns and nothing else.
                double turns = (raw.Radians - turned.Radians) / Angle.FullTurnRadians;
                Assert.Equal(Math.Round(turns), turns, 6);

                // Normalising twice changes nothing more.
                Assert.Equal(turned.Radians, turned.Normalize().Radians, 9);
                Assert.Equal(signed.Radians, signed.NormalizeSigned().Radians, 9);
            }
        }

        [Fact]
        public void TheNamedAnglesAreWhatTheyClaim()
        {
            Assert.Equal(0.0, Angle.Zero.Radians, 12);
            Assert.Equal(90.0, Angle.Right.Degrees, 9);
            Assert.Equal(180.0, Angle.Straight.Degrees, 9);
            Assert.Equal(360.0, Angle.FullTurn.Degrees, 9);

            // Adding and subtracting are the plain arithmetic on radians.
            Assert.Equal(270.0, Angle.Right.Add(Angle.Straight).Degrees, 9);
            Assert.Equal(90.0, Angle.Straight.Subtract(Angle.Right).Degrees, 9);
        }

        [Fact]
        public void ToleranceThresholdsAreCarriedThroughUnchanged()
        {
            Random rng = new Random(3579);

            for (int t = 0; t < 200; t++)
            {
                double point = rng.Next(1, 10000) / 1000.0;
                double vector = rng.Next(1, 10000) / 1000.0;
                double angle = rng.Next(1, 1000) / 1000.0;
                double planar = rng.Next(1, 10000) / 1000.0;

                var full = new Tolerance(point, vector, angle, planar);

                Assert.Equal(point, full.EqualPoint, 12);
                Assert.Equal(vector, full.EqualVector, 12);
                Assert.Equal(angle, full.EqualAngleRad, 12);
                Assert.Equal(planar, full.EqualPlanar, 12);

                // The three-argument form takes the planar threshold from the point one, because
                // coplanarity is measured as a distance.
                var three = new Tolerance(point, vector, angle);
                Assert.Equal(point, three.EqualPlanar, 12);

                // The two-argument form falls back to the defaults for BOTH of the rest — including the
                // planar one, which the three-argument form instead takes from equalPoint. The two
                // overloads differ here, which is documented but easy to be caught out by: asking for a
                // loose tolerance with two arguments leaves coplanarity at the default 1e-4.
                var two = new Tolerance(point, vector);
                Assert.Equal(Tolerance.DefaultEqualAngleRad, two.EqualAngleRad, 12);
                Assert.Equal(Tolerance.DefaultEqualPlanar, two.EqualPlanar, 12);
            }
        }

        [Fact]
        public void ANegativeThresholdIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(-1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(1.0, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(1.0, 1.0, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(1.0, 1.0, 1.0, -1.0));

            // Zero is not negative, and is a legitimate demand for exactness.
            Tolerance exact = new Tolerance(0.0, 0.0, 0.0, 0.0);
            Assert.Equal(0.0, exact.EqualPoint, 12);
        }

        [Fact]
        public void MinByAndMaxByPickWhatAPlainScanWould()
        {
            Random rng = new Random(4680);

            for (int t = 0; t < 300; t++)
            {
                var numbers = new List<int>();
                for (int i = 0; i < 1 + rng.Next(20); i++) { numbers.Add(rng.Next(-500, 501)); }

                int smallest = int.MaxValue;
                int largest = int.MinValue;
                foreach (int n in numbers)
                {
                    if (n < smallest) { smallest = n; }
                    if (n > largest) { largest = n; }
                }

                // Keyed on the absolute value, so the answer is not simply the smallest number.
                int byKeyMin = numbers.MinBy(n => Math.Abs(n));
                int byKeyMax = numbers.MaxBy(n => Math.Abs(n));

                int expectedMin = int.MaxValue, expectedMax = int.MinValue;
                int keptMin = 0, keptMax = 0;
                foreach (int n in numbers)
                {
                    if (Math.Abs(n) < expectedMin) { expectedMin = Math.Abs(n); keptMin = n; }
                    if (Math.Abs(n) > expectedMax) { expectedMax = Math.Abs(n); keptMax = n; }
                }

                Assert.Equal(Math.Abs(keptMin), Math.Abs(byKeyMin));
                Assert.Equal(Math.Abs(keptMax), Math.Abs(byKeyMax));

                // Keyed on the value itself, they are the plain smallest and largest.
                Assert.Equal(smallest, numbers.MinBy(n => n));
                Assert.Equal(largest, numbers.MaxBy(n => n));
            }
        }

        [Fact]
        public void MinByAndMaxByRefuseAnEmptyOrMissingSequence()
        {
            Assert.Throws<ArgumentNullException>(() => ((List<int>)null).MinBy(n => n));
            Assert.Throws<ArgumentNullException>(() => ((List<int>)null).MaxBy(n => n));
            Assert.Throws<ArgumentNullException>(() => new List<int> { 1 }.MinBy<int, int>(null));

            Assert.Throws<InvalidOperationException>(() => new List<int>().MinBy(n => n));
            Assert.Throws<InvalidOperationException>(() => new List<int>().MaxBy(n => n));
        }
    }
}
