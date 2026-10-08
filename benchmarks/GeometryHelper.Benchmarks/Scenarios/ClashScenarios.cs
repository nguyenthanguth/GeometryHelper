using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// The clash checks: <see cref="Clash3.Find(IReadOnlyList{GeoPreparedSolid3}, ClashOptions, Tolerance)"/> on one set,
    /// on two, and on bars by their centre lines, all of which sweep the boxes of the parts to find the pairs worth
    /// checking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The axis trap is a bundle of 4 900 bars 6 000 long, 20 by 20 across, laid 70 by 70 at a pitch of 30: every bar
    /// overlaps every other along its length and stands clear of all but its neighbours across it. One bar in a
    /// hundred stands 12 over towards its neighbour, sharing 2 with it; one in a hundred stands 10 over, touching it;
    /// one in a hundred 7 over, 3 short of it, within the clearance of 5. The X case lays the bars along X, where a
    /// sweep along X looks at every pair of them; the Y and Z cases are the same bundle with X and Y, or X and Z,
    /// swapped in every coordinate, which rounds nothing. Two sets are the bundle split like a chessboard, so that two
    /// neighbours are always one of each; bars are the cells of the first set as bars of radius 10 along their middles.
    /// </para>
    /// <para>
    /// Measured at 36f56df, a sweep along X looks at 12 002 550 pairs and along Z at 169 050, and the sweep took 268 ms
    /// of the 273 the one set along X took.
    /// </para>
    /// <para>
    /// The parts are prepared before the timing starts, so the timed call is the sweep and the checks of the pairs it
    /// finds. It runs on one thread, so that the sweep, which runs on one anyway, is not lost in the noise of the
    /// others.
    /// </para>
    /// </remarks>
    internal static class ClashScenarios
    {
        private const int Side = 70;
        private const double Pitch = 30.0;
        private const double Section = 20.0;
        private const double Length = 6000.0;
        private const double Clearance = 5.0;

        private static readonly Tolerance Within = Tolerance.Default;

        private static readonly ClashOptions Options = new ClashOptions(Clearance, true, 1);

        /// <summary>
        /// Every clash case.
        /// </summary>
        internal static IEnumerable<BenchmarkCase> All()
        {
            foreach (char axis in "XYZ")
            {
                yield return new BenchmarkCase("Clash.AxisTrap.OneSet." + axis, Side * Side, "parts", false, () => OneSet(axis));
            }

            foreach (char axis in "XYZ")
            {
                yield return new BenchmarkCase("Clash.AxisTrap.TwoSets." + axis, Side * Side, "parts", false, () => TwoSets(axis));
            }

            foreach (char axis in "XYZ")
            {
                yield return new BenchmarkCase("Clash.AxisTrap.Bars." + axis, Side * Side, "bars and parts", false, () => Bars(axis));
            }

            yield return new BenchmarkCase("Clash.Neutral", NeutralCount, "parts", false, Neutral);
        }

        /// <summary>
        /// One cell of the bundle: its place in the grid, where along its length it starts, and how far it stands over
        /// towards its neighbour.
        /// </summary>
        private struct Cell
        {
            public int Row;
            public int Column;
            public double Start;
            public double Over;

            public bool InFirstSet => (Row + Column) % 2 == 0;

            /// <summary>The box of the cell, laid along X.</summary>
            public GeoAabb3 Box => new GeoAabb3(
                new GeoPoint3(Start, Row * Pitch + Over, Column * Pitch),
                new GeoPoint3(Start + Length, Row * Pitch + Over + Section, Column * Pitch + Section));
        }

        /// <summary>
        /// The bundle, laid along X, from a fixed seed.
        /// </summary>
        private static List<Cell> Bundle()
        {
            var random = new Random(4900);
            var cells = new List<Cell>(Side * Side);

            for (int row = 0; row < Side; row++)
            {
                for (int column = 0; column < Side; column++)
                {
                    double start = random.Next(2001) * 0.5;
                    int pick = random.Next(100);
                    double over = pick == 0 ? 12.0 : pick == 1 ? 10.0 : pick == 2 ? 7.0 : 0.0;
                    cells.Add(new Cell { Row = row, Column = column, Start = start, Over = over });
                }
            }

            return cells;
        }

        /// <summary>
        /// A point of the bundle laid along an axis: as it is for X, X and Y swapped for Y, X and Z swapped for Z.
        /// </summary>
        private static GeoPoint3 Along(char axis, GeoPoint3 p)
        {
            switch (axis)
            {
                case 'X': return p;
                case 'Y': return new GeoPoint3(p.Y, p.X, p.Z);
                default: return new GeoPoint3(p.Z, p.Y, p.X);
            }
        }

        private static GeoAabb3 Along(char axis, GeoAabb3 box) => new GeoAabb3(Along(axis, box.Min), Along(axis, box.Max));

        private static GeoPreparedSolid3 Part(GeoAabb3 box) => new GeoPreparedSolid3(box.ToObb().ToSolid(), Within);

        private static Fixture OneSet(char axis)
        {
            GeoPreparedSolid3[] parts = Bundle().Select(c => Part(Along(axis, c.Box))).ToArray();
            string note = Pairs(parts.Select(p => p.Box).ToList(), Clearance + Within.EqualPoint);

            return Fixture.Of(() => Clash3.Find(parts, Options, Within), Sign, Describe, note);
        }

        private static Fixture TwoSets(char axis)
        {
            List<Cell> cells = Bundle();
            GeoPreparedSolid3[] first = cells.Where(c => c.InFirstSet).Select(c => Part(Along(axis, c.Box))).ToArray();
            GeoPreparedSolid3[] second = cells.Where(c => !c.InFirstSet).Select(c => Part(Along(axis, c.Box))).ToArray();
            string note = Pairs(first.Concat(second).Select(p => p.Box).ToList(), Clearance + Within.EqualPoint);

            return Fixture.Of(() => Clash3.Find(first, second, Options, Within), Sign, Describe, note);
        }

        /// <summary>
        /// The bundle with the cells of the first set as bars 20 thick along their middles, and the rest as parts.
        /// </summary>
        private static Fixture Bars(char axis)
        {
            List<Cell> cells = Bundle();
            double radius = Section * 0.5;
            var bars = new List<ClashBar>();
            var boxes = new List<GeoAabb3>();

            foreach (Cell c in cells.Where(c => c.InFirstSet))
            {
                GeoAabb3 box = c.Box;
                double y = box.Min.Y + radius, z = box.Min.Z + radius;
                GeoPoint3 start = Along(axis, new GeoPoint3(box.Min.X, y, z));
                GeoPoint3 end = Along(axis, new GeoPoint3(box.Max.X, y, z));
                bars.Add(new ClashBar(new GeoPolyline3(start, end), radius));

                // The box a bar is swept by: its centre line grown by its radius every way.
                boxes.Add(new GeoAabb3(start, end).Expand(radius));
            }

            GeoPreparedSolid3[] parts = cells.Where(c => !c.InFirstSet).Select(c => Part(Along(axis, c.Box))).ToArray();
            ClashBar[] all = bars.ToArray();
            string note = Pairs(boxes.Concat(parts.Select(p => p.Box)).ToList(), Clearance + Within.EqualPoint);

            return Fixture.Of(() => Clash3.Find(all, parts, Options, Within), Sign, Describe, note);
        }

        private const int NeutralCount = 1500;

        /// <summary>
        /// Blocks 40 to 80 on a side scattered through a cube 1 500 across, from a fixed seed, at whole millimetres so
        /// that some touch: no axis is any better to sweep than another.
        /// </summary>
        private static Fixture Neutral()
        {
            var random = new Random(1500);
            var parts = new GeoPreparedSolid3[NeutralCount];

            for (int i = 0; i < parts.Length; i++)
            {
                var low = new GeoPoint3(random.Next(1500), random.Next(1500), random.Next(1500));
                var size = new GeoVector3(40 + random.Next(41), 40 + random.Next(41), 40 + random.Next(41));
                parts[i] = Part(new GeoAabb3(low, low.Add(size)));
            }

            string note = Pairs(parts.Select(p => p.Box).ToList(), Clearance + Within.EqualPoint);

            return Fixture.Of(() => Clash3.Find(parts, Options, Within), Sign, Describe, note);
        }

        /// <summary>
        /// How many pairs of the boxes a sweep along each axis looks at, as the sweep of a clash check reaches.
        /// </summary>
        private static string Pairs(List<GeoAabb3> boxes, double reach)
        {
            long On(Func<GeoPoint3, double> axis)
                => SweepPairs.Count(boxes.Select(b => axis(b.Min)).ToList(), boxes.Select(b => axis(b.Max)).ToList(), reach);

            return SweepPairs.Describe(On(p => p.X), On(p => p.Y), On(p => p.Z));
        }

        /// <summary>
        /// Signs the clashes: for each in order, its pair and kind, every measure, where it is, the gap, every body it
        /// shares and every face of contact, and the type of the error of a pair that could not be checked.
        /// </summary>
        private static void Sign(ResultSignature signature, ClashResult[] results)
        {
            signature.Add(results.Length);

            foreach (ClashResult r in results)
            {
                signature.Add(r.First);
                signature.Add(r.Second);
                signature.Add((int)r.Kind);
                signature.Add(r.Location);
                signature.Add(r.Volume);
                signature.Add(r.Depth);
                signature.Add(r.LengthInside);
                signature.Add(r.ContactArea);
                signature.Add(r.Distance);
                signature.Add(r.Gap.HasValue);

                if (r.Gap.HasValue)
                {
                    signature.Add(r.Gap.Value);
                }

                signature.Add(r.Overlaps.Count);

                foreach (GeoSolid3 overlap in r.Overlaps)
                {
                    signature.Add(overlap);
                }

                signature.Add(r.Contact.Count);

                foreach (GeoFace3 face in r.Contact)
                {
                    signature.Add(face);
                }

                signature.Add(r.Error?.GetType().FullName);
            }
        }

        private static string Describe(ClashResult[] results)
        {
            int Of(ClashKind kind) => results.Count(r => r.Kind == kind);

            return Report.Count(results.Length) + " clashes: " + Of(ClashKind.Hard) + " hard, " + Of(ClashKind.Touch) + " touching, "
                + Of(ClashKind.Clearance) + " too near, " + Of(ClashKind.Unresolved) + " unresolved";
        }
    }
}
