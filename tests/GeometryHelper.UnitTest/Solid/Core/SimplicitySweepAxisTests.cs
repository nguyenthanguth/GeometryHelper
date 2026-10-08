using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Whether a polygon in space is simple is found by a sweep of its edges along the axis that leaves the fewest pairs
    /// to measure, and comes out as it did along X, to the last bit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The combs are 40 teeth 10 000 long and 5 wide, 5 apart, sheared so that the teeth run a quarter up for each along
    /// them, on the plane z = y / 2, every corner then swapped X with Y, or X with Z, which rounds nothing. Edges side by
    /// side along the length overlap every other along X, and the sweep chooses another axis.
    /// </para>
    /// <para>
    /// A sliver tooth has its top edge 0.00093 over its bottom one, laid parallel: at that distance, measured in 3D on
    /// the sloping plane, the two come within the point tolerance of each other or not by the last bit, and the
    /// shortest line between parallel edges starts at the start of the first edge it is given, so which edge comes first
    /// decides it. The shapes and the distances were searched for at 3a2da08, where the sweep ran along X.
    /// </para>
    /// </remarks>
    public class SimplicitySweepAxisTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private const double Length = 10000.0;

        /// <summary>
        /// The corners of a sheared comb: the spine down the left from x 0 to 20, the teeth to the right.
        /// </summary>
        /// <param name="axis">Which axis the teeth run along once swapped.</param>
        /// <param name="teeth">How many teeth.</param>
        /// <param name="shear">How far up the teeth run for each along them.</param>
        /// <param name="change">What one tooth is made: given the corners so far, its bottom and top, and its index, it adds its tip and top corners and returns true, or returns false for a plain tooth.</param>
        private static List<GeoPoint3> Comb(char axis, int teeth, double shear, Func<Action<double, double>, double, double, int, bool> change)
        {
            var corners = new List<GeoPoint3>();

            void Add(double x, double y)
            {
                double v = y + shear * x;
                corners.Add(SweepAxisText.Along(axis, new GeoPoint3(x, v, 0.5 * v)));
            }

            Add(0.0, 0.0);

            for (int i = 0; i < teeth; i++)
            {
                double bottom = 10.0 * i, top = bottom + 5.0;
                Add(Length, bottom);

                if (change == null || !change(Add, bottom, top, i))
                {
                    Add(Length, top);
                }

                if (i + 1 < teeth)
                {
                    Add(20.0, top);
                    Add(20.0, bottom + 10.0);
                }
            }

            Add(0.0, 10.0 * (teeth - 1) + 5.0);

            return corners;
        }

        /// <summary>
        /// A comb whose twentieth tooth is a sliver: from the tip of its bottom edge a hair out to the right and up to a
        /// height, then back along that height for a run.
        /// </summary>
        private static List<GeoPoint3> Sliver(char axis, double shear, double run, double height)
            => Comb(axis, 40, shear, (add, bottom, top, i) =>
            {
                if (i != 20)
                {
                    return false;
                }

                add(Length + 1.0, height);
                add(Length - run, height);
                return true;
            });

        /// <summary>
        /// A comb whose twentieth tooth has its top edge pushed down half way along to a height over the next tooth's
        /// bottom edge.
        /// </summary>
        private static List<GeoPoint3> Pushed(char axis, double under)
            => Comb(axis, 40, 0.25, (add, bottom, top, i) =>
            {
                if (i != 20)
                {
                    return false;
                }

                add(Length, top);
                add(Length * 0.5, bottom + 10.0 - under);
                return true;
            });

        /// <summary>
        /// A comb whose twentieth tooth folds its tip back over its bottom edge, or nearly.
        /// </summary>
        private static List<GeoPoint3> Folded(char axis, double rise)
            => Comb(axis, 40, 0.25, (add, bottom, top, i) =>
            {
                if (i != 20)
                {
                    return false;
                }

                // Back along the bottom edge, the rise over it: within the tolerance it lies on it.
                add(Length - 3000.0, bottom + rise);
                add(Length - 2000.0, top);
                return true;
            });

        /// <summary>
        /// Polygons about points scattered over a sloping plane from a fixed seed, of 6 to 300 corners in the order of
        /// the way round, at distances of their own; some with two corners swapped, which mostly makes them cross, and
        /// some with a corner pulled to within a whisker of the next but one edge, either side of the tolerance.
        /// </summary>
        private static List<List<GeoPoint3>> Scattered(char axis)
        {
            var random = new Random(3000);
            var polygons = new List<List<GeoPoint3>>();

            for (int k = 0; k < 120; k++)
            {
                int count = 6 + random.Next(k < 60 ? 30 : 295);
                var flat = new List<(double X, double Y)>();

                for (int i = 0; i < count; i++)
                {
                    // Round the square [-1, 1] by its perimeter, 8 long, an even share of it each, a little shaken, and
                    // stretched along x.
                    double s = (i + 0.2 + 0.6 * random.NextDouble()) * 8.0 / count;
                    double r = 50.0 + 50.0 * random.NextDouble();
                    double u = s < 2.0 ? 1.0 : s < 4.0 ? 3.0 - s : s < 6.0 ? -1.0 : s - 7.0;
                    double v = s < 2.0 ? s - 1.0 : s < 4.0 ? 1.0 : s < 6.0 ? 5.0 - s : -1.0;
                    flat.Add((40.0 * r * u, r * v));
                }

                int pick = random.Next(4);

                if (pick == 0)
                {
                    int a = random.Next(count), b = (a + 2 + random.Next(count - 3)) % count;
                    (flat[a], flat[b]) = (flat[b], flat[a]);
                }
                else if (pick == 1)
                {
                    // Corner a pulled onto the line through the middle of the edge two ahead, then off it by a whisker.
                    int a = random.Next(count);
                    (double X, double Y) p = flat[(a + 2) % count], q = flat[(a + 3) % count];
                    double whisker = random.Next(2) == 0 ? 0.0006 : 0.0014;
                    double mx = 0.5 * (p.X + q.X), my = 0.5 * (p.Y + q.Y);
                    double nx = q.Y - p.Y, ny = p.X - q.X, n = Math.Sqrt(nx * nx + ny * ny);
                    flat[a] = (mx + whisker * nx / n, my + whisker * ny / n);
                }

                polygons.Add(flat.Select(p => SweepAxisText.Along(axis, new GeoPoint3(p.X, p.Y, 0.25 * p.X + 0.5 * p.Y))).ToList());
            }

            return polygons;
        }

        /// <summary>
        /// Combs from a fixed seed, of 5 to 64 teeth of lengths of their own, on the sloping plane, sheared or not: in two
        /// of three, one tooth pushed to within a whisker of the next, or a sliver whose top edge runs parallel to its
        /// bottom one a whisker over it, either side of the tolerance or near it; now and then two corners swapped.
        /// </summary>
        private static List<List<GeoPoint3>> RandomCombs(char axis)
        {
            var random = new Random(5000);
            var polygons = new List<List<GeoPoint3>>();
            double[] whiskers = { 0.0005, 0.00089, 0.0015, 0.003 };

            for (int k = 0; k < 60; k++)
            {
                int teeth = 5 + random.Next(60);
                double shear = random.Next(5) * 0.05;
                var lengths = new double[teeth];

                for (int i = 0; i < teeth; i++)
                {
                    lengths[i] = 2000.0 + random.Next(8001);
                }

                int special = random.Next(teeth), kind = random.Next(3);
                double whisker = whiskers[random.Next(whiskers.Length)];
                var corners = new List<GeoPoint3>();

                void Add(double x, double y)
                {
                    double v = y + shear * x;
                    corners.Add(SweepAxisText.Along(axis, new GeoPoint3(x, v, 0.5 * v)));
                }

                Add(0.0, 0.0);

                for (int i = 0; i < teeth; i++)
                {
                    double bottom = 10.0 * i, top = bottom + 5.0, length = lengths[i];
                    Add(length, bottom);

                    if (i == special && kind == 0 && i + 1 < teeth)
                    {
                        Add(length, top);
                        Add(0.5 * Math.Min(length, lengths[i + 1]), bottom + 10.0 - whisker);
                    }
                    else if (i == special && kind == 1)
                    {
                        Add(length + 1.0, bottom + whisker);
                        Add(30.0 + random.Next((int)length - 100), bottom + whisker);
                    }
                    else
                    {
                        Add(length, top);
                    }

                    if (i + 1 < teeth)
                    {
                        Add(20.0, top);
                        Add(20.0, bottom + 10.0);
                    }
                }

                Add(0.0, 10.0 * (teeth - 1) + 5.0);

                if (random.Next(6) == 0)
                {
                    int a = random.Next(corners.Count), b = (a + 2 + random.Next(corners.Count - 3)) % corners.Count;
                    (corners[a], corners[b]) = (corners[b], corners[a]);
                }

                polygons.Add(corners);
            }

            return polygons;
        }

        private static GeoPolygon3 Polygon(List<GeoPoint3> corners) => new GeoPolygon3(corners, Within);

        private static string Simple(IEnumerable<List<GeoPoint3>> polygons)
        {
            var text = new StringBuilder();

            foreach (List<GeoPoint3> corners in polygons)
            {
                GeoPolygon3 polygon;

                try
                {
                    polygon = Polygon(corners);
                }
                catch (ArgumentException)
                {
                    text.Append('-');
                    continue;
                }

                text.Append(Intersection3.IsSimple(polygon, Within) ? 'T' : 'F');
            }

            return text.ToString();
        }

        [Theory]
        [InlineData('X', "FFFT")]
        [InlineData('Y', "FTFT")]
        [InlineData('Z', "FFFT")]
        public void SliverTeethAtTheTolerance_AreAsSimpleAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            var polygons = new[]
            {
                // The edge further along X comes first, and the two are within the tolerance; the other way round, not.
                Sliver(axis, 0.254, 1029.0, 200.0009297935259),

                // Both edges start at x 20, the first of them as the sort of their starts along X left them decides.
                Sliver(axis, 0.251, 9980.0, 200.00092897847128),
                Sliver(axis, 0.25, 4000.0, 200.0005),
                Sliver(axis, 0.25, 4000.0, 200.002),
            };

            Assert.Equal(expected, Simple(polygons));
        }

        [Theory]
        [InlineData('X', 200.0005, false)]
        [InlineData('Y', 200.0005, false)]
        [InlineData('Z', 200.0005, false)]
        [InlineData('X', 200.0015, true)]
        [InlineData('Y', 200.0015, true)]
        [InlineData('Z', 200.0015, true)]
        public void AToothRunningAlongItselfLevelAcrossTheSweep_IsSimpleOnlyWhereItStandsFurtherOffThanTheTolerance(char axis, double height, bool simple)
        {
            // Not sheared, the bottom edge of the tooth spans nothing across the teeth, and its top edge comes down to
            // run along it for 3 900, 0.00056 or 0.00168 over it on the sloping plane: the sweep across has to keep the
            // bottom edge open past where it ends for those to be measured.
            GeoPolygon3 polygon = Polygon(Comb(axis, 40, 0.0, (add, bottom, top, i) =>
            {
                if (i != 20)
                {
                    return false;
                }

                add(Length, bottom + 3.0);
                add(Length - 100.0, height);
                add(Length - 4000.0, height);
                return true;
            }));

            Assert.Equal(simple, Intersection3.IsSimple(polygon, Within));
            Assert.Equal(simple, EveryPair(polygon, Within));
        }

        [Theory]
        [InlineData('X', "TFTFTT")]
        [InlineData('Y', "TFTFTT")]
        [InlineData('Z', "TFTFTT")]
        public void CombsTouchingFoldedAndClear_AreAsSimpleAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            var polygons = new[]
            {
                Comb(axis, 40, 0.25, null),
                Pushed(axis, 0.0005),
                Pushed(axis, 0.0015),
                Folded(axis, 0.0005),
                Folded(axis, 0.01),
                Comb(axis, 40, 0.0, null),
            };

            Assert.Equal(expected, Simple(polygons));
        }

        [Theory]
        [InlineData('X', "FTTFFTTFTFTTTTTTFTTTTTFTFTTFFFTFFFTFTTTTTTTTFFFFTFTFFFTFTFTTFFTTFTTFTTTFTTFTTFTFFTFFTFFTFFTFTFTFTTTFFTFFTTFTTTTTTTTFTTTT")]
        [InlineData('Y', "FTTFFTTFTFTTTTTTFTTTTTFTFTTFFFTFFFTFTTTTTTTTFFFFTFTFFFTFTFTTFFTTFTTFTTTFTTFTTFTFFTFFTFFTFFTFTFTFTTTFFTFFTTFTTTTTTTTFTTTT")]
        [InlineData('Z', "FTTFFTTFTFTTTTTTFTTTTTFTFTTFFFTFFFTFTTTTTTTTFFFFTFTFFFTFTFTTFFTTFTTFTTTFTTFTTFTFFTFFTFFTFFTFTFTFTTTFFTFFTTFTTTTTTTTFTTTT")]
        public void ScatteredPolygons_AreAsSimpleAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            Assert.Equal(expected, Simple(Scattered(axis)));
        }

        [Theory]
        [InlineData('X', "FTFFFFTFTTFFFFFTFTTTFFTTFTFFFFFTTFTTTFTTFTTFFFFFFFTTTTTTTFFT")]
        [InlineData('Y', "FTFFFFTFTTFFFFFTFTTTFFTTFTFFFFFTTFTTTFTTFTTFFFFFFFTTTTTTTFFT")]
        [InlineData('Z', "FTFFFFTFTTFFFFFTFTTTFFTTFTFFFFFTTFTTTFTTFTTFFFFFFFTTTTTTTFFT")]
        public void RandomCombs_AreAsSimpleAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            Assert.Equal(expected, Simple(RandomCombs(axis)));
        }

        [Theory]
        [InlineData('X')]
        [InlineData('Y')]
        [InlineData('Z')]
        public void EveryPolygon_IsAsSimpleAsEveryPairMeasured(char axis)
        {
            List<List<GeoPoint3>> polygons = Scattered(axis);
            polygons.AddRange(RandomCombs(axis));
            polygons.Add(Sliver(axis, 0.254, 1029.0, 200.0009297935259));
            polygons.Add(Sliver(axis, 0.251, 9980.0, 200.00092897847128));
            polygons.Add(Pushed(axis, 0.0005));
            polygons.Add(Pushed(axis, 0.0015));
            polygons.Add(Folded(axis, 0.0005));
            polygons.Add(Folded(axis, 0.01));

            foreach (List<GeoPoint3> corners in polygons)
            {
                GeoPolygon3 polygon = Polygon(corners);
                Assert.Equal(EveryPair(polygon, Within), Intersection3.IsSimple(polygon, Within));
            }
        }

        /// <summary>
        /// A point with two of its coordinates swapped as asked: "XY" swaps X and Y, "YZ" Y and Z, "XZ" X and Z, and
        /// nothing swaps none.
        /// </summary>
        private static GeoPoint3 Swapped(string swap, GeoPoint3 p)
        {
            switch (swap)
            {
                case "XY": return new GeoPoint3(p.Y, p.X, p.Z);
                case "YZ": return new GeoPoint3(p.X, p.Z, p.Y);
                case "XZ": return new GeoPoint3(p.Z, p.Y, p.X);
                default: return p;
            }
        }

        /// <summary>
        /// The boxes of the edges of a comb of 20 teeth 1 000 long on the plane z = 0, its teeth along X and side by
        /// side along Y, every corner swapped as asked.
        /// </summary>
        private static GeoAabb3[] FlatComb(string swap)
        {
            var corners = new List<GeoPoint3> { new GeoPoint3(0, 0, 0) };

            for (int i = 0; i < 20; i++)
            {
                double bottom = 10.0 * i, top = bottom + 5.0;
                corners.Add(new GeoPoint3(1000, bottom, 0));
                corners.Add(new GeoPoint3(1000, top, 0));

                if (i + 1 < 20)
                {
                    corners.Add(new GeoPoint3(20, top, 0));
                    corners.Add(new GeoPoint3(20, bottom + 10.0, 0));
                }
            }

            corners.Add(new GeoPoint3(0, 195, 0));

            return Polygon(corners.Select(p => Swapped(swap, p)).ToList()).GetEdges().Select(e => e.GetAabb()).ToArray();
        }

        [Theory]
        [InlineData("", 1)]
        [InlineData("XY", 0)]
        [InlineData("YZ", 2)]
        [InlineData("XZ", 1)]
        public void TheEdgesAreSweptAlongTheAxisTheTeethStandSideBySideAlong(string swap, int expected)
        {
            Assert.Equal(expected, Intersection3.SweepAxis(FlatComb(swap), Within.EqualPoint));
        }

        [Fact]
        public void ASquareIsSweptAlongX_WhereYLeavesAsManyPairs()
        {
            var square = Polygon(new List<GeoPoint3> { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0) });

            Assert.Equal(0, Intersection3.SweepAxis(square.GetEdges().Select(e => e.GetAabb()).ToArray(), Within.EqualPoint));
        }

        [Fact]
        public void BoxesUpTheDiagonalAreSweptAlongX_WhereEveryAxisLeavesAsManyPairs()
        {
            GeoAabb3[] boxes = Enumerable.Range(0, 10).Select(i => new GeoAabb3(new GeoPoint3(i, i, i), new GeoPoint3(i + 1.5, i + 1.5, i + 1.5))).ToArray();

            Assert.Equal(0, Intersection3.SweepAxis(boxes, Within.EqualPoint));
        }

        [Theory]
        [InlineData(double.NaN, true)]
        [InlineData(0.0005, false)]
        [InlineData(0.0015, true)]
        public void ACombStandingUpOnItsSide_IsSweptAlongZAndAsSimpleAsEveryPairMeasured(double under, bool simple)
        {
            // The teeth along X, side by side up Z, every corner on y = 0: along X every tooth meets every other, and along
            // Y every edge, so the sweep chooses Z. The twentieth tooth has a corner pushed in to stand under the next
            // tooth by as much as asked: within the tolerance it touches it, beyond it the comb is simple.
            List<GeoPoint3> corners = Comb('X', 40, 0.0, (add, bottom, top, i) =>
            {
                if (double.IsNaN(under) || i != 20)
                {
                    return false;
                }

                add(Length, top);
                add(Length * 0.5, bottom + 10.0 - under);
                return true;
            });

            GeoPolygon3 polygon = Polygon(corners.Select(p => new GeoPoint3(p.X, 0.0, p.Y)).ToList());
            GeoAabb3[] bounds = polygon.GetEdges().Select(e => e.GetAabb()).ToArray();

            Assert.True(PairsAlongX(bounds, Within.EqualPoint) > Intersection3.SweepAxisPairs * bounds.Length);
            Assert.Equal(2, Intersection3.SweepAxis(bounds, Within.EqualPoint));
            Assert.Equal(simple, Intersection3.IsSimple(polygon, Within));
            Assert.Equal(EveryPair(polygon, Within), Intersection3.IsSimple(polygon, Within));
        }

        [Theory]
        [InlineData('X', true)]
        [InlineData('Y', false)]
        [InlineData('Z', false)]
        public void CombsLaidAlongX_AreSweptAcross(char axis, bool across)
        {
            // Laid along X, the sweep along X looks at more pairs than it is let before it chooses, and chooses another
            // axis, as it does for the sheared slivers; laid along Y or Z, X is the axis to sweep, and it never chooses.
            // Sheared, laid along Y or Z, the teeth run across X too, and no axis is much better than another.
            List<GeoPoint3>[] polygons = axis == 'X'
                ? new[] { Comb(axis, 40, 0.0, null), Sliver(axis, 0.254, 1029.0, 200.0009297935259), Sliver(axis, 0.251, 9980.0, 200.00092897847128) }
                : new[] { Comb(axis, 40, 0.0, null) };

            foreach (List<GeoPoint3> corners in polygons)
            {
                GeoPolygon3 polygon = Polygon(corners);
                GeoAabb3[] bounds = polygon.GetEdges().Select(e => e.GetAabb()).ToArray();

                Assert.Equal(across, PairsAlongX(bounds, Within.EqualPoint) > Intersection3.SweepAxisPairs * bounds.Length);
                Assert.Equal(across, Intersection3.SweepAxis(bounds, Within.EqualPoint) != 0);
            }
        }

        /// <summary>
        /// How many pairs a sweep along X looks at, as the sweep counts them against what it is let look at: at each edge
        /// it comes to, the edges still open.
        /// </summary>
        private static long PairsAlongX(GeoAabb3[] bounds, double slack)
        {
            double[] keys = bounds.Select(b => b.Min.X).ToArray();
            int[] order = Enumerable.Range(0, bounds.Length).ToArray();
            Array.Sort(keys, order);

            var open = new List<int>();
            long pairs = 0;

            foreach (int i in order)
            {
                open.RemoveAll(j => bounds[j].Max.X < bounds[i].Min.X - slack);
                pairs += open.Count;
                open.Add(i);
            }

            return pairs;
        }

        /// <summary>
        /// Whether a polygon is simple, every pair of its edges measured as the sweep along X measured them: the boxes
        /// of the two compared as it compared them, the edge it took later along X as the one it came to, and given
        /// first to the shortest line.
        /// </summary>
        private static bool EveryPair(GeoPolygon3 polygon, Tolerance tolerance)
        {
            GeoLine3[] edges = polygon.GetEdges();
            int count = edges.Length;
            var bounds = new GeoAabb3[count];
            var keys = new double[count];
            var order = new int[count];

            for (int i = 0; i < count; i++)
            {
                bounds[i] = edges[i].GetAabb();
                keys[i] = bounds[i].Min.X;
                order[i] = i;
            }

            Array.Sort(keys, order);
            var rank = new int[count];

            for (int k = 0; k < count; k++)
            {
                rank[order[k]] = k;
            }

            double slack = tolerance.EqualPoint;

            for (int p = 0; p < count; p++)
            {
                for (int q = p + 1; q < count; q++)
                {
                    int i = rank[p] > rank[q] ? p : q, j = i == p ? q : p;

                    if (bounds[j].Max.X < bounds[i].Min.X - slack
                        || bounds[j].Max.Y < bounds[i].Min.Y - slack || bounds[j].Min.Y > bounds[i].Max.Y + slack
                        || bounds[j].Max.Z < bounds[i].Min.Z - slack || bounds[j].Min.Z > bounds[i].Max.Z + slack)
                    {
                        continue;
                    }

                    bool iThenJ = j == (i + 1) % count;
                    bool jThenI = i == (j + 1) % count;

                    if (iThenJ || jThenI)
                    {
                        GeoLine3 incoming = iThenJ ? edges[i] : edges[j];
                        GeoLine3 outgoing = iThenJ ? edges[j] : edges[i];

                        if (Containment3.IsPointOn(outgoing, incoming.StartPoint, tolerance) || Containment3.IsPointOn(incoming, outgoing.EndPoint, tolerance))
                        {
                            return false;
                        }
                    }
                    else if (Projection3.GetShortestLineTo(edges[i], edges[j], tolerance).Length <= tolerance.EqualPoint)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
