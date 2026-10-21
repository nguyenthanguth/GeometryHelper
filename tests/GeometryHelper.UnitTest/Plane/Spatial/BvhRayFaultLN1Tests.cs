using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The crossings of a ray with the edges a hierarchy holds are the ones each edge gives the ray, whatever else is indexed.
    /// </summary>
    /// <remarks>
    /// The ray was cut past the box round everything indexed and the segment it was cut to asked of every edge it reached, so
    /// an edge nearly along the ray was judged parallel or not over the length of that segment, which the edges far from it
    /// set. The edge from (0, 0) to (100, 0.0005), drawing half a thousandth off the ray from (-50, 0) along X over its own
    /// length, is parallel to it at the default tolerance and crosses it nowhere; with an edge a million away indexed beside
    /// it the cut was a million long, the edge no longer parallel to it, and the two crossed at (0, 0). Each leaf asks the edge
    /// itself now, as for a line without ends, and a branch is left alone when the ray, a slab test away, never enters its box
    /// grown by the point tolerance.
    /// </remarks>
    public class BvhRayFaultLN1Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Strict = new Tolerance(1E-8, 1E-8);

        /// <summary>The plain scan: every edge asked the ray, the points of one within the tolerance of a kept one dropped.</summary>
        private static GeoPoint2[] Scan(IEnumerable<GeoEdge2> edges, GeoRay2 ray, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 p in edge.GetIntersections(ray, tolerance))
                {
                    if (!found.Any(k => k.IsEqualTo(p, tolerance)))
                    {
                        found.Add(p);
                    }
                }
            }

            return found.ToArray();
        }

        private static bool Same(GeoPoint2[] scanned, GeoPoint2[] found)
            => scanned.Length == found.Length && scanned.All(p => found.Any(q => q.DistanceTo(p) <= 1E-9));

        [Fact]
        public void AnEdgeNearlyAlongTheRay_IsJudgedByItsOwnLength_WhateverElseIsIndexed()
        {
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0.0005));
            var far = new GeoEdge2(new GeoPoint2(1E6, 1E6), new GeoPoint2(1E6 + 1, 1E6));
            var ray = new GeoRay2(new GeoPoint2(-50, 0), GeoVector2.XAxis);

            Assert.Empty(edge.GetIntersections(ray, Thousandth));
            Assert.Empty(new GeoBvh2(new[] { edge }).GetIntersections(ray, Thousandth));
            Assert.Empty(new GeoBvh2(new[] { edge, far }).GetIntersections(ray, Thousandth));
        }

        [Fact]
        public void ManyRaysAmongNearParallelAndFarEdges_GiveTheScansAnswer_WithNoDifference()
        {
            var random = new Random(20261021);
            int differences = 0;
            int crossings = 0;

            for (int k = 0; k < 3000; k++)
            {
                double height = random.NextDouble() * 100 - 50;
                var edges = new List<GeoEdge2>();

                // Edges lying along the ray, some a hair off it at either end, some curved, and some far from it.
                for (int e = 0; e < 1 + random.Next(12); e++)
                {
                    double x = random.NextDouble() * 400 - 200;
                    double length = random.NextDouble() * 150 + 0.5;
                    bool away = random.Next(4) == 0;
                    double offset = away ? random.NextDouble() * 3000 - 1500 : (random.NextDouble() - 0.5) * 0.003;
                    double rise = away ? (random.NextDouble() - 0.5) * 100 : (random.NextDouble() - 0.5) * 0.003 - offset;
                    var from = new GeoPoint2(x, height + offset);
                    var to = new GeoPoint2(x + length, height + offset + rise);
                    edges.Add(random.Next(5) == 0 ? new GeoEdge2(from, to, random.NextDouble() * 0.02 - 0.01) : new GeoEdge2(from, to));
                }

                var direction = new GeoVector2(random.Next(2) == 0 ? 1 : -1, (random.NextDouble() - 0.5) * 0.0005);
                var ray = new GeoRay2(new GeoPoint2(random.NextDouble() * 800 - 400, height + (random.NextDouble() - 0.5) * 0.002), direction);

                GeoPoint2[] scanned = Scan(edges, ray, Thousandth);
                GeoPoint2[] found = new GeoBvh2(edges).GetIntersections(ray, Thousandth);

                if (!Same(scanned, found))
                {
                    differences++;
                }

                crossings += scanned.Length > 0 ? 1 : 0;
            }

            Assert.Equal(0, differences);
            Assert.InRange(crossings, 300, 3000);
        }

        [Fact]
        public void DiagonalRaysThroughAGridOfEdges_GiveTheScansAnswer()
        {
            var edges = new List<GeoEdge2>();

            for (int i = 0; i < 30; i++)
            {
                for (int j = 0; j < 30; j++)
                {
                    edges.Add(new GeoEdge2(new GeoPoint2(i * 10, j * 10), new GeoPoint2(i * 10 + 8, j * 10 + 1)));
                    edges.Add(new GeoEdge2(new GeoPoint2(i * 10 + 8, j * 10 + 1), new GeoPoint2(i * 10 + 7, j * 10 + 8), 0.3));
                }
            }

            var index = new GeoBvh2(edges);

            foreach (GeoRay2 ray in new[]
            {
                new GeoRay2(new GeoPoint2(-5, -5), new GeoVector2(1, 1)),
                new GeoRay2(new GeoPoint2(150.5, 3.3), new GeoVector2(1, 0.31)),
                new GeoRay2(new GeoPoint2(150.5, 3.3), new GeoVector2(-1, -0.31)),
                new GeoRay2(new GeoPoint2(3, 3), GeoVector2.YAxis),
                new GeoRay2(new GeoPoint2(400, 400), new GeoVector2(-1, -1)),
            })
            {
                GeoPoint2[] scanned = Scan(edges, ray, Strict);

                Assert.NotEmpty(scanned);
                Assert.True(Same(scanned, index.GetIntersections(ray, Strict)), ray.ToString());
            }
        }

        [Fact]
        public void ARayPointingAwayFromEverything_CrossesNothing()
        {
            var edges = new[] { new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)), new GeoEdge2(new GeoPoint2(100, 0), new GeoPoint2(100, 100)) };
            var index = new GeoBvh2(edges);

            Assert.Empty(index.GetIntersections(new GeoRay2(new GeoPoint2(200, 50), GeoVector2.XAxis), Thousandth));
            Assert.Equal(2, index.GetIntersections(new GeoRay2(new GeoPoint2(200, 50), new GeoVector2(-1, -0.3)), Thousandth).Length);
        }
    }
}
