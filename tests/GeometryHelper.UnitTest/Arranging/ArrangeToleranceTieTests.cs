using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// Of two places as open, the one found first. Clearances up to the point tolerance of the options count as nought,
    /// and clearances beyond it that differ by no more than a thousandth of it count as equal, so the exact distances do
    /// not tell two places apart by the last digits of a clearance of the same true size, wherever the scene lies.
    /// </summary>
    public class ArrangeToleranceTieTests
    {
        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => new GeoPolygon2(
            new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        /// <summary>
        /// A label 40 by 10 hung off a leader 60 long, among three lines and two regions that stand 0.0002 off the places
        /// beside it, at a tolerance of 0.0001, the whole scene moved along X.
        /// </summary>
        private static GeoVector2 Place(double shift)
        {
            double gap = 0.0002;
            double x = shift;
            double y = 0.0;
            var leader = new GeoLine2(x, y, x + 60, y);
            var item = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 40, 10),
                Offset = 5,
                BlockPolygons = new List<GeoPolygon2>
                {
                    Box(x + 5, y - 30, x + 55, y - gap - 5),
                    Box(x + 65 + gap, y - 10, x + 100, y + 30)
                },
                BlockLines = new List<GeoLine2>
                {
                    new GeoLine2(x + 50 + gap, y + 5, x + 90, y + 5),
                    new GeoLine2(x + 10 - gap, y + 10, x + 10 - gap, y + 40),
                    new GeoLine2(x - 20, y + 15 + gap, x + 80, y + 15 + gap)
                }
            };

            var options = new ArrangeOptions { Tolerance = new Tolerance(1E-4, 1E-5) };

            return Arranger.Run(new List<ArrangeItem> { item }, options).Single().Translation;
        }

        [Theory]
        [InlineData(300.0)]
        [InlineData(900.0)]
        [InlineData(12345.678)]
        public void ALabelAmongClearancesOfTheSameTrueSize_IsPlacedAlikeWhereverTheSceneLies(double shift)
        {
            GeoVector2 home = Place(0.0);
            GeoVector2 moved = Place(shift);

            Assert.Equal(home.X, moved.X, 6);
            Assert.Equal(home.Y, moved.Y, 6);
        }

        /// <summary>
        /// A label 40 by 10 hung 5 off a leader 60 long along X, between a line 0.5 over the first row above and one
        /// 0.5 and a hundred-millionth under the first row below, at the default tolerance, the whole scene moved by the
        /// given amounts.
        /// </summary>
        private static GeoVector2 PlaceBetweenLines(double dx, double dy)
        {
            double d = 1E-8;
            var leader = new GeoLine2(dx, dy, dx + 60, dy);
            var item = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 40, 10),
                Offset = 5,
                BlockLines = new List<GeoLine2>
                {
                    new GeoLine2(dx - 20, dy + 15.5, dx + 80, dy + 15.5),
                    new GeoLine2(dx - 20, dy - 15.5 - d, dx + 80, dy - 15.5 - d)
                }
            };

            var options = new ArrangeOptions { Tolerance = Tolerance.Default };

            return Arranger.Run(new List<ArrangeItem> { item }, options).Single().Translation;
        }

        [Theory]
        [InlineData(1E4, 0.0)]
        [InlineData(1E5, 0.0)]
        [InlineData(1E6, 0.0)]
        [InlineData(1E4, 1E4)]
        [InlineData(1E5, 1E5)]
        [InlineData(1E6, 1E6)]
        public void ALabelBetweenClearancesAHundredMillionthApart_TakesThePlaceTriedFirstWhereverTheSceneLies(double dx, double dy)
        {
            // Above is tried first and 0.5 clear; below is 0.5 and a hundred-millionth clear, within a thousandth of the
            // tolerance of above. A band that grew with the distance from the origin sent the label below near the origin
            // and above a hundred thousand off it.
            GeoVector2 home = PlaceBetweenLines(0.0, 0.0);
            GeoVector2 moved = PlaceBetweenLines(dx, dy);

            Assert.True(home.Y > 0.0, $"{home}");
            Assert.Equal(home.X, moved.X, 6);
            Assert.Equal(home.Y, moved.Y, 6);
        }
    }
}
