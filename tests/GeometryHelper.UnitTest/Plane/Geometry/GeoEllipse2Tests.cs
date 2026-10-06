using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ellipse as a value: what its constructor takes and refuses, what it holds, when it is a circle, and how two
    /// of them compare. The ellipse is held as a centre, a unit major axis and two radii, the major no smaller than the
    /// minor; the minor axis is the major turned a quarter counter-clockwise.
    /// </summary>
    public class GeoEllipse2Tests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly GeoPoint2 Centre = new GeoPoint2(40, -25);

        [Fact]
        public void TheAxisIsMadeUnitAndTheMinorAxisIsAQuarterTurnCounterClockwiseFromIt()
        {
            // An axis of (3, 4) is five long, so it is held as (0.6, 0.8); a quarter turn counter-clockwise is (-0.8, 0.6).
            var ellipse = new GeoEllipse2(Centre, new GeoVector2(3, 4), 300, 100);

            Assert.Equal(0.6, ellipse.MajorAxis.X, 1E-15);
            Assert.Equal(0.8, ellipse.MajorAxis.Y, 1E-15);
            Assert.Equal(-0.8, ellipse.MinorAxis.X, 1E-15);
            Assert.Equal(0.6, ellipse.MinorAxis.Y, 1E-15);

            // The centre and the radii are kept as given.
            Assert.Equal(Centre, ellipse.Center);
            Assert.Equal(300.0, ellipse.MajorRadius);
            Assert.Equal(100.0, ellipse.MinorRadius);
        }

        [Fact]
        public void AMinorRadiusLargerThanTheMajorIsRefusedRatherThanSwapped()
        {
            // 100 by 300 is not read as 300 by 100 turned a quarter: the caller said which axis is the major one.
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse2(Centre, GeoVector2.XAxis, 100, 300));

            // A minor radius a hair over the major is still over it: the check is on the numbers, not within a tolerance.
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse2(Centre, GeoVector2.XAxis, 100, 100.0001));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(300.0, 0.0)]
        [InlineData(300.0, -1.0)]
        [InlineData(-100.0, -300.0)]
        [InlineData(double.NaN, 100.0)]
        [InlineData(300.0, double.NaN)]
        [InlineData(double.PositiveInfinity, 100.0)]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
        public void ARadiusThatIsNotAPositiveFiniteNumberIsRefused(double majorRadius, double minorRadius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse2(Centre, GeoVector2.XAxis, majorRadius, minorRadius));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(double.NaN, 1.0)]
        [InlineData(1.0, double.PositiveInfinity)]
        public void AnAxisWithNoLengthOrNoDirectionIsRefused(double x, double y)
        {
            // An ArgumentException itself, not the out-of-range one the radii throw.
            Assert.Throws<ArgumentException>(() => new GeoEllipse2(Centre, new GeoVector2(x, y), 300, 100));
        }

        [Fact]
        public void TwoEqualRadiiMakeACircle()
        {
            var circle = new GeoEllipse2(Centre, new GeoVector2(1, 1), 250, 250);

            Assert.True(circle.IsCircle(Tol));
            Assert.True(circle.IsValid);
            Assert.Equal(0.0, circle.Eccentricity);
        }

        [Fact]
        public void ACircleBecomesAnEllipseAlongXWithItsRadiusForBothAxes()
        {
            GeoEllipse2 ellipse = GeoEllipse2.FromCircle(new GeoCircle2(Centre, 25));

            Assert.Equal(Centre, ellipse.Center);
            Assert.Equal(GeoVector2.XAxis, ellipse.MajorAxis);
            Assert.Equal(25.0, ellipse.MajorRadius);
            Assert.Equal(25.0, ellipse.MinorRadius);
        }

        [Fact]
        public void IsCircleReadsTheDifferenceOfTheRadiiAsADistance()
        {
            // 300 against 299.9996 differ by 0.0004, within a thousandth; 300 against 299.997 by 0.003, three times it.
            var nearly = new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 299.9996);
            var not = new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 299.997);

            Assert.True(nearly.IsCircle(Tol));
            Assert.False(not.IsCircle(Tol));

            // Within a ten-thousandth, 0.0004 is four times too much.
            Assert.False(nearly.IsCircle(new Tolerance(1E-4, 1E-5)));
        }

        [Fact]
        public void EqualsIsExactOnTheHeldValues()
        {
            // An axis of (2, 0) is held as (1, 0) exactly, so the two are the same value.
            var first = new GeoEllipse2(Centre, new GeoVector2(2, 0), 300, 100);
            var second = new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 100);

            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.False(first != second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.True(first.Equals((object)second));

            // A ten-thousandth off in the minor radius is a different value, though IsEqualTo calls it the same.
            var off = new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 100.0001);
            Assert.NotEqual(first, off);
            Assert.True(first != off);
            Assert.True(first.IsEqualTo(off, Tol));
        }

        [Fact]
        public void IsEqualToTakesTheAxisUpToItsSign()
        {
            // An axis pointing the other way draws the same ellipse: Equals tells them apart, IsEqualTo does not.
            var along = new GeoEllipse2(Centre, new GeoVector2(1, 2), 300, 100);
            var back = new GeoEllipse2(Centre, new GeoVector2(-1, -2), 300, 100);

            Assert.NotEqual(along, back);
            Assert.True(along.IsEqualTo(back, Tol));
            Assert.True(back.IsEqualTo(along, Tol));
        }

        [Fact]
        public void IsEqualToComparesTheCentreAndBothRadiiWithinEqualPoint()
        {
            var ellipse = new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 100);

            // 0.0004 off is within the thousandth; 0.003 off is three times over it.
            Assert.True(ellipse.IsEqualTo(new GeoEllipse2(new GeoPoint2(40.0004, -25), GeoVector2.XAxis, 300, 100), Tol));
            Assert.False(ellipse.IsEqualTo(new GeoEllipse2(new GeoPoint2(40, -25.003), GeoVector2.XAxis, 300, 100), Tol));
            Assert.True(ellipse.IsEqualTo(new GeoEllipse2(Centre, GeoVector2.XAxis, 300.0004, 100), Tol));
            Assert.False(ellipse.IsEqualTo(new GeoEllipse2(Centre, GeoVector2.XAxis, 300.003, 100), Tol));
            Assert.True(ellipse.IsEqualTo(new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 99.9996), Tol));
            Assert.False(ellipse.IsEqualTo(new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 99.997), Tol));
        }

        [Fact]
        public void IsEqualToComparesTheAxisWithinEqualVector()
        {
            var ellipse = new GeoEllipse2(Centre, GeoVector2.XAxis, 300, 100);

            // Turned 1E-7 radians, the unit axis moves 1E-7, within the 1E-5 of EqualVector; turned 1E-3 it moves a
            // hundred times too far, and the far end of the major axis moves 0.3.
            var hardly = new GeoEllipse2(Centre, new GeoVector2(Math.Cos(1E-7), Math.Sin(1E-7)), 300, 100);
            var turned = new GeoEllipse2(Centre, new GeoVector2(Math.Cos(1E-3), Math.Sin(1E-3)), 300, 100);

            Assert.True(ellipse.IsEqualTo(hardly, Tol));
            Assert.False(ellipse.IsEqualTo(turned, Tol));

            // A quarter turn swaps the axes: the same centre and radii, a different ellipse.
            Assert.False(ellipse.IsEqualTo(new GeoEllipse2(Centre, GeoVector2.YAxis, 300, 100), Tol));
        }

        [Fact]
        public void TwoCirclesAreEqualWhateverTheirAxes()
        {
            // A circle has no major axis to speak of, so one along X and one along (1, 1) are the same circle; so is one
            // whose radii differ by 0.0004, which is a circle within the thousandth.
            var alongX = new GeoEllipse2(Centre, GeoVector2.XAxis, 250, 250);
            var alongDiagonal = new GeoEllipse2(Centre, new GeoVector2(1, 1), 250, 250);
            var nearly = new GeoEllipse2(Centre, new GeoVector2(-2, 7), 250, 249.9996);

            Assert.True(alongX.IsEqualTo(alongDiagonal, Tol));
            Assert.True(alongDiagonal.IsEqualTo(nearly, Tol));
            Assert.True(GeoEllipse2.FromCircle(new GeoCircle2(Centre, 250)).IsEqualTo(alongDiagonal, Tol));
        }

        [Fact]
        public void ADefaultEllipseIsNotValidAndAConstructedOneIs()
        {
            // default has a zero axis and zero radii, which no constructor makes.
            Assert.False(default(GeoEllipse2).IsValid);
            Assert.True(new GeoEllipse2(Centre, new GeoVector2(3, 4), 300, 100).IsValid);
            Assert.True(new GeoEllipse2(Centre, GeoVector2.XAxis, 7, 7).IsValid);
        }

        [Fact]
        public void AnEllipseThinnerThanThePointToleranceIsStillMadeAndValid()
        {
            // 50 by 0.0005: the minor radius is half the thousandth, so the ellipse has hardly any width, but it is an
            // ellipse all the same, of area 0.0785.
            var thin = new GeoEllipse2(Centre, GeoVector2.XAxis, 50, 0.0005);

            Assert.True(thin.IsValid);
            Assert.False(thin.IsCircle(Tol));
            Assert.Equal(Math.PI * 50 * 0.0005, thin.Area, 1E-15);
        }

        [Fact]
        public void ToStringNamesTheTypeTheCentreTheAxisAndBothRadii()
        {
            // Whole numbers throughout, so the text reads the same in every culture.
            string text = new GeoEllipse2(Centre, GeoVector2.YAxis, 300, 100).ToString();

            Assert.StartsWith("GeoEllipse2[", text);
            Assert.Contains(Centre.ToString(), text);
            Assert.Contains(GeoVector2.YAxis.ToString(), text);
            Assert.Contains("300", text);
            Assert.Contains("100", text);
        }

        [Fact]
        public void CloneGivesAnEqualCopy()
        {
            var ellipse = new GeoEllipse2(Centre, new GeoVector2(3, 4), 300, 100);

            Assert.Equal(ellipse, ellipse.Clone());
        }
    }
}
