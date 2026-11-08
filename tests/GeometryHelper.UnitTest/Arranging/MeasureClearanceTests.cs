using System;
using System.Collections.Generic;
using GeometryHelper.Arranging;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// The clearance of a place: the obstacles whose boxes stand further off than the nearest found so far are passed over
    /// unmeasured, and the clearance comes out to the bit as measuring every obstacle gives it.
    /// </summary>
    public class MeasureClearanceTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The clearance as it was measured before: every obstacle, the least distance.</summary>
        private static double EveryObstacle(List<Obstacle> obstacles, GeoRectangle2 moved)
        {
            double best = double.MaxValue;

            foreach (Obstacle obstacle in obstacles)
            {
                double distance = double.MaxValue;

                switch (obstacle.Type)
                {
                    case ObstacleType.Rectangle:
                        distance = moved.DistanceTo(obstacle.Rectangle);
                        break;
                    case ObstacleType.Polygon:
                        distance = moved.DistanceTo(obstacle.Polygon);
                        break;
                    case ObstacleType.Line:
                        distance = Distance2.DistanceTo(moved, obstacle.Line, Tol);
                        break;
                }

                best = Math.Min(best, distance);
            }

            return best;
        }

        [Theory]
        [InlineData(1, 1.0)]
        [InlineData(2, 1.0)]
        [InlineData(3, 1E5)]
        [InlineData(4, 1E6)]
        [InlineData(5, 1E-3)]
        public void TheClearanceOfAPlaceIsTheSameToTheBitAsMeasuringEveryObstacle(int seed, double size)
        {
            var random = new Random(seed);

            for (int trial = 0; trial < 300; trial++)
            {
                // A scene a hundred labels wide about a point far off or near the origin, of lines, boxes and triangles,
                // some a hair from the place, some touching it, some far.
                double ox = (random.NextDouble() - 0.5) * size * 1000, oy = (random.NextDouble() - 0.5) * size * 1000;
                var obstacles = new List<Obstacle>();
                var moved = new GeoRectangle2(new GeoPoint2(ox, oy), size * (1 + random.NextDouble()), size * (0.5 + random.NextDouble()));

                for (int k = 0; k < 60; k++)
                {
                    double x = ox + (random.NextDouble() - 0.5) * size * 100, y = oy + (random.NextDouble() - 0.5) * size * 100;
                    double w = size * random.NextDouble() * 5;

                    switch (random.Next(4))
                    {
                        case 0:
                            obstacles.Add(new Obstacle(new GeoLine2(x, y, x + w, y + (random.NextDouble() - 0.5) * w)));
                            break;
                        case 1:
                            obstacles.Add(new Obstacle(new GeoRectangle2(new GeoPoint2(x, y), w + size * 0.1, w * 0.5 + size * 0.1)));
                            break;
                        case 2:
                            obstacles.Add(new Obstacle(new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + w + size, y), new GeoPoint2(x, y + w + size))));
                            break;
                        default:
                            // A hair off a side of the place.
                            double right = moved.Center.X + moved.Width / 2;
                            double hair = random.NextDouble() * size * 1E-3;
                            obstacles.Add(new Obstacle(new GeoLine2(right + hair, moved.Center.Y, right + hair + w, moved.Center.Y + w)));
                            break;
                    }
                }

                double expected = EveryObstacle(obstacles, moved);
                double measured = PlacementHeuristics.MeasureClearance(obstacles, moved, Tol);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(measured));
            }
        }
    }
}
