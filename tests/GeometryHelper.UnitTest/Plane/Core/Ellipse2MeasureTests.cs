using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What an ellipse measures: its area, exact; its eccentricity, exact; and its perimeter, by the arithmetic-geometric
    /// mean, held against the numbers mpmath gives at 40 digits and against quadrature of the speed round the rim
    /// (<see cref="Ellipse2Oracle.Perimeter"/>). None of these reads a tolerance.
    /// </summary>
    public class Ellipse2MeasureTests
    {
        [Fact]
        public void TheAreaIsPiTimesBothRadii()
        {
            // 300 by 100: 30 000 pi, 94 247.78.
            Assert.Equal(30000.0 * Math.PI, Ellipse2Oracle.Tilted().Area, 1E-9);
        }

        [Fact]
        public void AThreeByOneIsThirteenPointThreeSixFourEightNineThreeTwoTwoLongRound()
        {
            // 4 a E(1 - b²/a²) at 40 digits is 13.36489322055525823; the nearest double is 13.364893220555258, and the
            // lead's Python AGM gives the same. Ten units in the last place either way is 1.8E-14.
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 3, 1);

            Assert.Equal(13.364893220555258, ellipse.Length, 2E-14);
            Assert.Equal(Ellipse2Oracle.Perimeter(3, 1), ellipse.Length, 2E-14);
        }

        [Fact]
        public void AFiveByFiveIsTenPiRoundAsTheCircleIs()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 5, 5);

            Assert.Equal(10.0 * Math.PI, ellipse.Length, 1E-14);
            Assert.Equal(new GeoCircle2(new GeoPoint2(0, 0), 5).Length, ellipse.Length, 1E-14);
        }

        [Theory]
        [InlineData(1000.0, 6283.185307179587)]
        [InlineData(500.0, 4844.224110273838)]
        [InlineData(100.0, 4063.974180100896)]
        [InlineData(10.0, 4001.0983297226517)]
        [InlineData(1.0, 4000.015588104688)]
        public void ThePerimeterIsRightFromOneToOneToAThousandToOne(double minorRadius, double perimeter)
        {
            // A major radius of 1 000 and minor radii from 1 000 down to 1. The expected lengths are mpmath's
            // 4 a E(1 - b²/a²) rounded to a double; quadrature of the speed gives the same to rounding. At a thousand to
            // one the rim is barely longer than the four radii, 4 000.0156.
            var ellipse = new GeoEllipse2(new GeoPoint2(-300, 800), new GeoVector2(1, 3), 1000.0, minorRadius);

            Assert.Equal(perimeter, ellipse.Length, 1E-12 * perimeter);
            Assert.Equal(Ellipse2Oracle.Perimeter(1000.0, minorRadius), ellipse.Length, 1E-12 * perimeter);
        }

        [Fact]
        public void AnEllipseThinnerThanThePointToleranceIsMeasuredAsAnyOther()
        {
            // 50 by 0.0005, a hundred thousand to one: the rim is a hair over 200 long, by quadrature.
            var thin = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 50, 0.0005);
            double oracle = Ellipse2Oracle.Perimeter(50, 0.0005);

            Assert.InRange(oracle, 200.0, 200.001);
            Assert.Equal(oracle, thin.Length, 1E-12 * oracle);
        }

        [Fact]
        public void TheEccentricityIsTheRootOfOneLessTheSquaredRatioOfTheRadii()
        {
            // 5 by 3: the root of 1 - 9/25 is exactly four fifths. 3 by 1: the root of 8/9, 0.9428. A circle: nought.
            Assert.Equal(0.8, new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 5, 3).Eccentricity, 1E-15);
            Assert.Equal(Math.Sqrt(8.0) / 3.0, new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 3, 1).Eccentricity, 1E-15);
            Assert.Equal(0.0, new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.YAxis, 4, 4).Eccentricity);
        }

        [Fact]
        public void ACircleMeasuresAsGeoCircle2DoesWhateverItsAxis()
        {
            // Radius 250 with its axis along (1, 1): the circle's 196 349.5 of area and 1 570.8 round, to 1E-12 of each.
            var circle = new GeoCircle2(new GeoPoint2(12, -7), 250);
            var ellipse = new GeoEllipse2(circle.Center, new GeoVector2(1, 1), 250, 250);

            Assert.Equal(circle.Area, ellipse.Area, 1E-12 * circle.Area);
            Assert.Equal(circle.Length, ellipse.Length, 1E-12 * circle.Length);
        }
    }
}
