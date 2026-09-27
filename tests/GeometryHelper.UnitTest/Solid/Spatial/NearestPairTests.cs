using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Clash;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The walk of two indexes that finds how near two meshes come, and where. Whatever order it opens the boxes
    /// in, it must find what weighing every pair of triangles finds.
    /// </summary>
    public class NearestPairTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>
        /// Parts of every kind the clash check meets, crowded into a small space so that some overlap, some touch,
        /// some pass close and some stand well apart: turned boxes, round bars at any angle, plates with holes.
        /// </summary>
        private static List<GeoSolid3> Parts(int seed, int count)
        {
            var rng = new Random(seed);
            var parts = new List<GeoSolid3>();

            for (int k = 0; k < count; k++)
            {
                double x = rng.Next(0, 30) * 10, y = rng.Next(0, 30) * 10, z = rng.Next(0, 10) * 10;

                switch (k % 3)
                {
                    case 0:
                        GeoSolid3 box = Box(x, y, z, x + rng.Next(1, 8) * 10, y + rng.Next(1, 8) * 10, z + rng.Next(1, 8) * 10);
                        parts.Add(rng.NextDouble() < 0.5
                            ? box.TransformBy(GeoTransform3.RotationAxis(box.GetAabb().Center, new GeoVector3(rng.NextDouble(), rng.NextDouble(), 1), rng.NextDouble()))
                            : box);
                        break;

                    case 1:
                        var end = new GeoPoint3(x + rng.Next(-60, 61), y + rng.Next(-60, 61), z + rng.Next(10, 80));
                        parts.Add(GeoSolid3.Cylinder(new GeoPoint3(x, y, z), end, rng.Next(2, 12), 12));
                        break;

                    default:
                        var hole = GeoSolid3.Cylinder(new GeoPoint3(x + 25, y + 25, z - 5), new GeoPoint3(x + 25, y + 25, z + 15), 8, 12);
                        parts.Add(Box(x, y, z, x + 50, y + 50, z + 10).WithOpenings(new[] { hole }));
                        break;
                }
            }

            return parts;
        }

        private static double ScanDistance(IReadOnlyList<GeoTriangle3> first, IReadOnlyList<GeoTriangle3> second)
        {
            double best = double.PositiveInfinity;

            foreach (GeoTriangle3 a in first)
            {
                foreach (GeoTriangle3 b in second)
                {
                    best = Math.Min(best, Distance3.DistanceTo(a, b));
                }
            }

            return best;
        }

        private static double ScanSegment(IReadOnlyList<GeoTriangle3> first, IReadOnlyList<GeoTriangle3> second)
        {
            double best = double.PositiveInfinity;

            foreach (GeoTriangle3 a in first)
            {
                foreach (GeoTriangle3 b in second)
                {
                    best = Math.Min(best, Projection3.GetShortestLineTo(a, b).Length);
                }
            }

            return best;
        }

        [Fact]
        public void TwoIndexesAreAsNearAsWeighingEveryPairOfTrianglesSays()
        {
            GeoPreparedSolid3[] parts = Parts(11, 18).Select(p => p.Prepare()).ToArray();
            int measured = 0, touching = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    double expected = ScanDistance(parts[i].Surface, parts[j].Surface);
                    double walked = parts[i].Index.DistanceTo(parts[j].Index);

                    Assert.True(Math.Abs(expected - walked) <= 1E-9 * Math.Max(1.0, expected), $"{i}-{j}: scan {expected}, walk {walked}");
                    Assert.Equal(walked, parts[j].Index.DistanceTo(parts[i].Index), 9);

                    measured++;
                    touching += expected == 0.0 ? 1 : 0;
                }
            }

            Assert.True(touching > 3 && touching < measured - 50, $"{touching} of {measured} pairs touch; the test needs both kinds");
        }

        [Fact]
        public void TheShortestSegmentIsAsShortAsWeighingEveryPairSaysAndJoinsTheTwoSurfaces()
        {
            GeoPreparedSolid3[] parts = Parts(12, 15).Select(p => p.Prepare()).ToArray();

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    double expected = ScanSegment(parts[i].Surface, parts[j].Surface);

                    Assert.True(parts[i].Index.TryGetShortestLineTo(parts[j].Index, double.PositiveInfinity, Tolerance.Global, out GeoLine3 line));
                    Assert.True(Math.Abs(expected - line.Length) <= 1E-9 * Math.Max(1.0, expected), $"{i}-{j}: scan {expected}, walk {line.Length}");

                    // It leaves the first surface and lands on the second.
                    Assert.True(parts[i].Index.GetClosestPoint(line.StartPoint).DistanceTo(line.StartPoint) < 1E-9);
                    Assert.True(parts[j].Index.GetClosestPoint(line.EndPoint).DistanceTo(line.EndPoint) < 1E-9);
                }
            }
        }

        [Fact]
        public void AReachAnswersOnlyForWhatComesNearerThanIt()
        {
            GeoPreparedSolid3[] parts = Parts(13, 18).Select(p => p.Prepare()).ToArray();
            int near = 0, beyond = 0;

            var distances = new Dictionary<(int, int), double>();
            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    distances[(i, j)] = ScanSegment(parts[i].Surface, parts[j].Surface);
                }
            }

            // Halfway between two neighbouring distances about the middle: half the pairs come nearer, and none
            // sits on the reach itself.
            double[] sorted = distances.Values.Where(d => d > 0.0).OrderBy(d => d).ToArray();
            int middle = sorted.Length / 2;
            while (sorted[middle + 1] - sorted[middle] < 1E-3)
            {
                middle++;
            }

            double reach = (sorted[middle] + sorted[middle + 1]) / 2.0;

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    double distance = distances[(i, j)];

                    bool found = parts[i].Index.TryGetShortestLineTo(parts[j].Index, reach, Tolerance.Global, out GeoLine3 line);

                    Assert.Equal(distance < reach, found);

                    if (found)
                    {
                        Assert.Equal(distance, line.Length, 9);
                        near++;
                    }
                    else
                    {
                        Assert.Equal(default(GeoLine3), line);
                        beyond++;
                    }
                }
            }

            Assert.True(near > 20 && beyond > 20, $"{near} near, {beyond} beyond; the test needs both");
        }

        [Fact]
        public void AnEmptyIndexIsInfinitelyFarAndNeverNear()
        {
            var empty = new GeoBvh3(new GeoTriangle3[0]);
            GeoBvh3 box = Box(0, 0, 0, 10, 10, 10).Prepare().Index;

            Assert.Equal(double.PositiveInfinity, empty.DistanceTo(box));
            Assert.Equal(double.PositiveInfinity, box.DistanceTo(empty));
            Assert.False(box.TryGetShortestLineTo(empty, double.PositiveInfinity, Tolerance.Global, out _));
            Assert.False(empty.TryGetShortestLineTo(box, double.PositiveInfinity, Tolerance.Global, out _));
        }

        /// <summary>
        /// The prepared body used to mesh both materials again and weigh every pair of faces; walking the two
        /// indexes must give a segment exactly as short, from the one surface to the other.
        /// </summary>
        [Fact]
        public void APreparedBodyFindsTheSegmentTheFaceByFaceSearchFinds()
        {
            List<GeoSolid3> solids = Parts(14, 15);
            GeoPreparedSolid3[] parts = solids.Select(p => p.Prepare()).ToArray();

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    GeoLine3 expected = Projection3.GetShortestLineTo(parts[i].Material, parts[j].Material);
                    GeoLine3 walked = parts[i].GetShortestLineTo(parts[j]);

                    Assert.True(Math.Abs(expected.Length - walked.Length) <= 1E-9 * Math.Max(1.0, expected.Length),
                        $"{i}-{j}: faces {expected.Length}, walk {walked.Length}");
                    Assert.True(parts[i].DistanceTo(walked.StartPoint) < 1E-9 || parts[i].Contains(walked.StartPoint));
                    Assert.True(parts[i].Index.GetClosestPoint(walked.StartPoint).DistanceTo(walked.StartPoint) < 1E-9);
                    Assert.True(parts[j].Index.GetClosestPoint(walked.EndPoint).DistanceTo(walked.EndPoint) < 1E-9);
                }
            }
        }

        /// <summary>
        /// With a clearance, every pair apart but nearer than it is reported, with the gap measured surface to surface
        /// as the face-by-face search measures it, and no other pair is.
        /// </summary>
        [Fact]
        public void TheClearanceCheckFindsWhatMeasuringEveryPairByHandFinds()
        {
            List<GeoSolid3> solids = Parts(15, 36);
            GeoPreparedSolid3[] parts = solids.Select(p => p.Prepare()).ToArray();
            const double clearance = 12.0;

            ClashResult[] found = Clash3.Find(parts, new ClashOptions(clearance));
            var near = found.Where(r => r.Kind == ClashKind.Clearance).ToDictionary(r => (r.First, r.Second));
            var byHand = new Dictionary<(int, int), double>();

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    if (parts[i].CollidesWith(parts[j]))
                    {
                        continue;
                    }

                    double gap = Projection3.GetShortestLineTo(parts[i].Material, parts[j].Material).Length;

                    if (gap < clearance)
                    {
                        byHand.Add((i, j), gap);
                    }
                }
            }

            Assert.True(byHand.Count > 10, $"only {byHand.Count} pairs come near; the test needs more");
            Assert.Equal(byHand.Keys.OrderBy(k => k), near.Keys.OrderBy(k => k));

            foreach (var pair in byHand)
            {
                ClashResult result = near[pair.Key];
                Assert.Equal(pair.Value, result.Distance, 9);
                Assert.True(result.Gap.HasValue);
                Assert.Equal(result.Distance, result.Gap.Value.Length, 12);
                Assert.True(result.Location.IsEqualTo(result.Gap.Value.MidPoint));
            }
        }
    }
}
