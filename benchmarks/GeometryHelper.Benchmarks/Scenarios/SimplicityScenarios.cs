using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// Whether polygons in space are simple: <see cref="Intersection3.IsSimple(GeoPolygon3, Tolerance)"/>, which sweeps
    /// the boxes of the edges to find the pairs worth measuring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The axis trap is a comb of 1 000 teeth 10 000 long and 5 wide, 5 apart, on a sloping plane, z half of y: 4 000
    /// edges, two thousand of them running the whole length, side by side. It is checked twice, as it is, simple, and
    /// with one corner pushed in at the middle tooth to come 0.0005 short of the next tooth, within the tolerance of
    /// touching it, so that the sweep runs to the end once and stops part of the way once. The X case lays the teeth
    /// along X; the Y and Z cases swap X with Y, or X with Z, in every corner, which rounds nothing. Measured at 36f56df, a
    /// sweep along X looks at 6 995 003 pairs of the simple comb's edges and along Y at 9 995, and the two checks took
    /// 35 ms along X against 1.2 with the comb turned.
    /// </para>
    /// <para>
    /// The small case is 3 000 polygons of 4 to 40 corners, most simple and some with two corners swapped, which
    /// mostly makes them cross themselves, so that a sweep set up for many edges is set up for few, many times.
    /// </para>
    /// </remarks>
    internal static class SimplicityScenarios
    {
        private const int Teeth = 1000;
        private const double ToothLength = 10000.0;
        private const int SmallCount = 3000;

        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>
        /// Every case of simplicity.
        /// </summary>
        internal static IEnumerable<BenchmarkCase> All()
        {
            foreach (char axis in "XYZ")
            {
                yield return new BenchmarkCase("IsSimple.AxisTrap." + axis, 4 * Teeth, "edges", false, () => AxisTrap(axis));
            }

            yield return new BenchmarkCase("IsSimple.Small", SmallCount, "polygons", false, Small);
        }

        /// <summary>
        /// The corners of the comb laid along X in the plane z = y / 2: the spine down the left, from x 0 to 20, and the
        /// teeth from there to the right, the first along y = 0.
        /// </summary>
        /// <param name="pushed">Whether the middle tooth has a corner pushed in to within 0.0005 of the next.</param>
        private static List<GeoPoint3> Comb(bool pushed)
        {
            var corners = new List<GeoPoint3>();

            void Add(double x, double y) => corners.Add(new GeoPoint3(x, y, y * 0.5));

            Add(0.0, 0.0);

            for (int i = 0; i < Teeth; i++)
            {
                double bottom = 10.0 * i, top = bottom + 5.0;
                Add(ToothLength, bottom);
                Add(ToothLength, top);

                if (i + 1 < Teeth)
                {
                    // The top edge of the tooth runs back to the spine; pushed, it bends half way up to just short of the
                    // bottom edge of the next tooth.
                    if (pushed && i == Teeth / 2)
                    {
                        Add(ToothLength * 0.5, bottom + 10.0 - 0.0005);
                    }

                    Add(20.0, top);
                    Add(20.0, bottom + 10.0);
                }
            }

            Add(0.0, 10.0 * (Teeth - 1) + 5.0);

            return corners;
        }

        private static GeoPoint3 Along(char axis, GeoPoint3 p)
        {
            switch (axis)
            {
                case 'X': return p;
                case 'Y': return new GeoPoint3(p.Y, p.X, p.Z);
                default: return new GeoPoint3(p.Z, p.Y, p.X);
            }
        }

        private static Fixture AxisTrap(char axis)
        {
            var simple = new GeoPolygon3(Comb(false).Select(p => Along(axis, p)), Within);
            var touching = new GeoPolygon3(Comb(true).Select(p => Along(axis, p)), Within);
            GeoPolygon3[] polygons = { simple, touching };

            return Fixture.Of(() => polygons.Select(p => Intersection3.IsSimple(p, Within)).ToArray(), Sign, Describe, Pairs(simple));
        }

        /// <summary>
        /// Star-shaped polygons about points scattered over a plane, from a fixed seed. Their corners lie in the order of
        /// the way round a square, each at its own distance out, so that no angle and no sine is needed to make them.
        /// </summary>
        private static Fixture Small()
        {
            var random = new Random(2000);
            var polygons = new GeoPolygon3[SmallCount];

            for (int k = 0; k < polygons.Length; k++)
            {
                double cx = random.Next(100000), cy = random.Next(100000);
                int count = 4 + random.Next(37);
                var corners = new GeoPoint2[count];

                for (int i = 0; i < count; i++)
                {
                    // Round the square [-1, 1] by its perimeter, 8 long: an even share of it each, a little shaken.
                    double s = (i + 0.2 + 0.6 * random.NextDouble()) * 8.0 / count;
                    double r = 50.0 + 50.0 * random.NextDouble();
                    GeoPoint2 d = RoundTheSquare(s);
                    corners[i] = new GeoPoint2(cx + r * d.X, cy + r * d.Y);
                }

                if (random.Next(4) == 0)
                {
                    int a = random.Next(count), b = (a + 2 + random.Next(count - 3)) % count;
                    GeoPoint2 swap = corners[a];
                    corners[a] = corners[b];
                    corners[b] = swap;
                }

                polygons[k] = new GeoPolygon3(corners.Select(p => new GeoPoint3(p.X, p.Y, 0.25 * p.X + 0.5 * p.Y)), Within);
            }

            return Fixture.Of(() => polygons.Select(p => Intersection3.IsSimple(p, Within)).ToArray(), Sign, Describe, null);
        }

        /// <summary>
        /// The point a distance round the perimeter of the square [-1, 1], from (1, -1) the way of the hands of a clock
        /// going backwards.
        /// </summary>
        private static GeoPoint2 RoundTheSquare(double s)
        {
            if (s < 2.0)
            {
                return new GeoPoint2(1.0, s - 1.0);
            }

            if (s < 4.0)
            {
                return new GeoPoint2(3.0 - s, 1.0);
            }

            if (s < 6.0)
            {
                return new GeoPoint2(-1.0, 5.0 - s);
            }

            return new GeoPoint2(s - 7.0, -1.0);
        }

        /// <summary>
        /// How many pairs of edges a sweep along each axis looks at, as the sweep of <see cref="Intersection3.IsSimple(GeoPolygon3, Tolerance)"/> reaches.
        /// </summary>
        private static string Pairs(GeoPolygon3 polygon)
        {
            GeoAabb3[] boxes = polygon.GetEdges().Select(e => e.GetAabb()).ToArray();

            long On(Func<GeoPoint3, double> axis)
                => SweepPairs.Count(boxes.Select(b => axis(b.Min)).ToList(), boxes.Select(b => axis(b.Max)).ToList(), Within.EqualPoint);

            return SweepPairs.Describe(On(p => p.X), On(p => p.Y), On(p => p.Z)) + " (the simple comb)";
        }

        private static void Sign(ResultSignature signature, bool[] results)
        {
            signature.Add(results.Length);

            foreach (bool simple in results)
            {
                signature.Add(simple);
            }
        }

        private static string Describe(bool[] results)
            => Report.Count(results.Count(r => r)) + " of " + Report.Count(results.Length) + " simple";
    }
}
