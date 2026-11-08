using System;
using System.Collections.Generic;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// Whether a place collides, asked of the index for the place's own box: the same answer going over every obstacle of the
    /// label's neighbourhood gives, whatever the scene.
    /// </summary>
    public class CollidesNearTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        [Theory]
        [InlineData(1, 1.0)]
        [InlineData(2, 10.0)]
        [InlineData(3, 1E4)]
        [InlineData(4, 1E-2)]
        public void APlaceCollidesJustWhereGoingOverTheWholeNeighbourhoodSaysItDoes(int seed, double size)
        {
            var random = new Random(seed);
            var index = new ObstacleSpatialIndex();

            for (int k = 0; k < 400; k++)
            {
                double x = random.NextDouble() * size * 200, y = random.NextDouble() * size * 200, w = random.NextDouble() * size * 8;

                switch (random.Next(3))
                {
                    case 0:
                        index.Add(new Obstacle(new GeoLine2(x, y, x + w, y + (random.NextDouble() - 0.5) * w)));
                        break;
                    case 1:
                        index.Add(new Obstacle(new GeoRectangle2(new GeoPoint2(x, y), w + size * 0.1, w * 0.4 + size * 0.1)));
                        break;
                    default:
                        index.Add(new Obstacle(new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + w + size, y), new GeoPoint2(x, y + w + size))));
                        break;
                }
            }

            var near = new List<int>();
            var touching = new List<Obstacle>();
            int collisions = 0;

            for (int trial = 0; trial < 500; trial++)
            {
                // A neighbourhood, and places in it and running a little out of it, some a hair off an obstacle.
                double cx = random.NextDouble() * size * 200, cy = random.NextDouble() * size * 200, reach = size * (5 + random.NextDouble() * 40);
                var region = Bounds.Of(new GeoRectangle2(new GeoPoint2(cx, cy), reach, reach));
                List<Obstacle> neighbourhood = index.Overlapping(region);

                for (int p = 0; p < 10; p++)
                {
                    var moved = new GeoRectangle2(new GeoPoint2(cx + (random.NextDouble() - 0.5) * reach, cy + (random.NextDouble() - 0.5) * reach), size * (1 + random.NextDouble() * 3), size);
                    bool expected = Obstacle.AnyCollides(neighbourhood, moved, Tol);
                    Assert.Equal(expected, GreedyAlgorithm.CollidesNear(index, region, moved, Tol, near, touching));
                    collisions += expected ? 1 : 0;
                }
            }

            Assert.InRange(collisions, 50, 4950);
        }
    }
}
