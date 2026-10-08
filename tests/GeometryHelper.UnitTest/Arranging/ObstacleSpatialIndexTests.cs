using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// The index the obstacles of a run are held in: whatever is added and asked, in whatever order, a query finds what
    /// going over every obstacle in the order added and keeping those whose box the region overlaps finds, and in that
    /// order.
    /// </summary>
    public class ObstacleSpatialIndexTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        public void AQuery_FindsWhatGoingOverEveryObstacleFinds_InTheOrderAdded(int seed)
        {
            var random = new Random(seed);
            var index = new ObstacleSpatialIndex();
            var added = new List<Obstacle>();
            var found = new List<int>();

            for (int step = 0; step < 1500; step++)
            {
                if (random.Next(3) != 0)
                {
                    Obstacle obstacle = RandomObstacle(random, added);
                    Assert.Equal(added.Count, index.Add(obstacle));
                    added.Add(obstacle);
                    continue;
                }

                Bounds region = RandomRegion(random);
                List<int> expected = Enumerable.Range(0, added.Count).Where(o => region.Overlaps(added[o].Box)).ToList();

                index.Query(region, found);
                Assert.Equal(expected, found);

                List<Obstacle> obstacles = index.Overlapping(region);
                Assert.Equal(expected.Count, obstacles.Count);
                for (int k = 0; k < expected.Count; k++)
                {
                    Assert.Equal(added[expected[k]].Box, obstacles[k].Box);
                    Assert.Equal(added[expected[k]].Type, obstacles[k].Type);
                }
            }

            Assert.Equal(added.Count, index.Count);
        }

        [Fact]
        public void ObstaclesAddedAlongALine_AreFoundAsGoingOverThemFinds()
        {
            // Added in order along X, the worst order for a tree that is not kept balanced.
            var index = new ObstacleSpatialIndex();
            var added = new List<Obstacle>();
            for (int i = 0; i < 3000; i++)
            {
                var obstacle = new Obstacle(new GeoRectangle2(new GeoPoint2(10.0 * i, 0.0), 12.0, 4.0));
                index.Add(obstacle);
                added.Add(obstacle);
            }

            var found = new List<int>();
            for (int q = 0; q < 300; q++)
            {
                Bounds region = Bounds.Of(new GeoLine2(100.0 * q - 3.0, -1.0, 100.0 * q + 25.0, 1.0));
                index.Query(region, found);
                Assert.Equal(Enumerable.Range(0, added.Count).Where(o => region.Overlaps(added[o].Box)), found);
            }
        }

        [Fact]
        public void AnEmptyIndex_FindsNothing()
        {
            var index = new ObstacleSpatialIndex();
            var found = new List<int> { 7 };

            index.Query(Bounds.Of(new GeoLine2(-1E9, -1E9, 1E9, 1E9)), found);

            Assert.Empty(found);
            Assert.Empty(index.Overlapping(Bounds.Of(new GeoLine2(0, 0, 1, 1))));
        }

        [Fact]
        public void AnObstacleWhoseBoxIsNotANumber_IsNumberedButNeverFound()
        {
            var index = new ObstacleSpatialIndex(new[]
            {
                new Obstacle(new GeoLine2(0, 0, 10, 10)),
                new Obstacle(new GeoLine2(double.NaN, 0, 10, 10)),
                new Obstacle(new GeoLine2(5, 5, 6, 6)),
            });
            var found = new List<int>();

            index.Query(Bounds.Of(new GeoLine2(-1E300, -1E300, 1E300, 1E300)), found);

            Assert.Equal(3, index.Count);
            Assert.Equal(new[] { 0, 2 }, found);
        }

        /// <summary>
        /// An obstacle of one of the kinds that trouble an index: a box of a label's size, one of no size, a sliver, a
        /// region as large as a sheet, one larger than any, one copied from an earlier one, one far off, and now and then
        /// one not a number or running out to infinity.
        /// </summary>
        private static Obstacle RandomObstacle(Random random, List<Obstacle> added)
        {
            double x = random.NextDouble() * 20000 - 5000, y = random.NextDouble() * 12000 - 3000;
            switch (random.Next(12))
            {
                case 0:
                    return new Obstacle(new GeoLine2(x, y, x, y));
                case 1:
                    return new Obstacle(new GeoLine2(x, y, x + random.NextDouble() * 1E-6, y));
                case 2:
                    return new Obstacle(new GeoLine2(x, y, x + random.NextDouble() * 30000, y + random.NextDouble() * 2));
                case 3:
                    return new Obstacle(new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + 15000, y), new GeoPoint2(x + 15000, y + 9000)));
                case 4:
                    return new Obstacle(new GeoLine2(-1E12, -1E12, 1E12, 1E12));
                case 5:
                    return added.Count == 0 ? new Obstacle(new GeoLine2(x, y, x + 1, y + 1)) : added[random.Next(added.Count)];
                case 6:
                    return new Obstacle(new GeoRectangle2(new GeoPoint2(x + 1E8, y), 20, 10));
                case 7:
                    return random.Next(4) == 0
                        ? new Obstacle(new GeoLine2(double.NaN, y, x, y))
                        : new Obstacle(new GeoLine2(x, y, double.PositiveInfinity, y));
                default:
                    return new Obstacle(new GeoRectangle2(new GeoPoint2(x, y), 10 + random.NextDouble() * 50, 8 + random.NextDouble() * 12, random.NextDouble() * Math.PI));
            }
        }

        /// <summary>
        /// A region to ask about: the reach of a label, one of no size, one as large as the sheet, one turned inside out as
        /// a negative margin leaves it, or one not a number.
        /// </summary>
        private static Bounds RandomRegion(Random random)
        {
            double x = random.NextDouble() * 20000 - 5000, y = random.NextDouble() * 12000 - 3000;
            Bounds reach = Bounds.Of(new GeoLine2(x, y, x + random.NextDouble() * 400, y + random.NextDouble() * 300));
            switch (random.Next(8))
            {
                case 0:
                    return Bounds.Of(new GeoLine2(x, y, x, y));
                case 1:
                    return reach.Expand(1E5);
                case 2:
                    return reach.Expand(-random.NextDouble() * 2000);
                case 3:
                    return reach.Expand(double.NaN);
                default:
                    return reach.Expand(random.NextDouble() * 100);
            }
        }
    }
}
