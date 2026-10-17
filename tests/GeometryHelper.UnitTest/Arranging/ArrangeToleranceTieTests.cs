using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// Of two places as open, the one found first. Clearances up to the global point tolerance count as equal, so the exact
    /// distances do not tell two places apart by the last digits of a clearance of the same true size. The scene's gaps of
    /// 0.0002 are within the global tolerance of 0.001 that these tests run at.
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
    }
}
