using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;

namespace GeometryHelper.Clash
{
    /// <summary>
    /// Checking many parts against each other at once: which run into each other, which touch, and which come
    /// nearer than a clearance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check asks, of every pair, the questions a clash report is made of, cheapest first. Every part is
    /// prepared once — its openings cut, its surface indexed — and the boxes are swept along one axis so that
    /// only pairs whose boxes come within the clearance of each other are looked at, which in a model is a
    /// small share of all the pairs. Those are checked in parallel: the surfaces through their indexes, then,
    /// for a pair that touches, the volume the two share, one body per region, and where there is none, the
    /// patches where they lie against each other.
    /// </para>
    /// <para>
    /// Openings are honoured throughout: a bolt through its hole is no clash. The results come back in the
    /// order of the pairs' indexes, whatever order the threads finished in. A pair whose check throws is
    /// reported as <see cref="ClashKind.Unresolved"/>, with the error, and logged, so that one part the
    /// library cannot read does not cost the report for the rest of the model.
    /// </para>
    /// </remarks>
    public static class Clash3
    {
        #region One set of parts

        /// <summary>
        /// Checks every part of a set against every other, using the default options and tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoSolid3> parts) => Find(parts, ClashOptions.Default, Tolerance.Global);

        /// <summary>
        /// Checks every part of a set against every other, using the default tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoSolid3> parts, ClashOptions options) => Find(parts, options, Tolerance.Global);

        /// <summary>
        /// Checks every part of a set against every other, within a tolerance.
        /// </summary>
        /// <param name="parts">The parts.</param>
        /// <param name="options">What to look for, and how many threads to use.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One result per pair that clashes, in the order of their indexes; <see cref="ClashResult.First"/> is the lower.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parts, one of them, or the options are null.</exception>
        public static ClashResult[] Find(IReadOnlyList<GeoSolid3> parts, ClashOptions options, Tolerance tolerance)
            => Find(Prepare(parts, nameof(parts), options, tolerance), options, tolerance);

        /// <summary>
        /// Checks every prepared part of a set against every other, using the default options and tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoPreparedSolid3> parts) => Find(parts, ClashOptions.Default, Tolerance.Global);

        /// <summary>
        /// Checks every prepared part of a set against every other, using the default tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoPreparedSolid3> parts, ClashOptions options) => Find(parts, options, Tolerance.Global);

        /// <summary>
        /// Checks every prepared part of a set against every other, within a tolerance.
        /// </summary>
        /// <param name="parts">The parts, already prepared: a model checked more than once is prepared once.</param>
        /// <param name="options">What to look for, and how many threads to use.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One result per pair that clashes, in the order of their indexes; <see cref="ClashResult.First"/> is the lower.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parts, one of them, or the options are null.</exception>
        public static ClashResult[] Find(IReadOnlyList<GeoPreparedSolid3> parts, ClashOptions options, Tolerance tolerance)
        {
            GeoPreparedSolid3[] all = Checked(parts, nameof(parts));

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return Run(all, all, true, options, tolerance);
        }

        #endregion

        #region One set against another

        /// <summary>
        /// Checks every part of one set against every part of another, using the default options and tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoSolid3> first, IReadOnlyList<GeoSolid3> second)
            => Find(first, second, ClashOptions.Default, Tolerance.Global);

        /// <summary>
        /// Checks every part of one set against every part of another, using the default tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoSolid3> first, IReadOnlyList<GeoSolid3> second, ClashOptions options)
            => Find(first, second, options, Tolerance.Global);

        /// <summary>
        /// Checks every part of one set against every part of another, within a tolerance.
        /// </summary>
        /// <param name="first">The first set, such as the reinforcement.</param>
        /// <param name="second">The second set, such as the embeds; a part is never checked against its own set.</param>
        /// <param name="options">What to look for, and how many threads to use.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One result per pair that clashes, in the order of their indexes, <see cref="ClashResult.First"/> indexing
        /// the first set and <see cref="ClashResult.Second"/> the second.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either set, a part of it, or the options are null.</exception>
        public static ClashResult[] Find(IReadOnlyList<GeoSolid3> first, IReadOnlyList<GeoSolid3> second, ClashOptions options, Tolerance tolerance)
            => Find(Prepare(first, nameof(first), options, tolerance), Prepare(second, nameof(second), options, tolerance), options, tolerance);

        /// <summary>
        /// Checks every prepared part of one set against every prepared part of another, using the default options
        /// and tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoPreparedSolid3> first, IReadOnlyList<GeoPreparedSolid3> second)
            => Find(first, second, ClashOptions.Default, Tolerance.Global);

        /// <summary>
        /// Checks every prepared part of one set against every prepared part of another, using the default tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<GeoPreparedSolid3> first, IReadOnlyList<GeoPreparedSolid3> second, ClashOptions options)
            => Find(first, second, options, Tolerance.Global);

        /// <summary>
        /// Checks every prepared part of one set against every prepared part of another, within a tolerance.
        /// </summary>
        /// <param name="first">The first set.</param>
        /// <param name="second">The second set; a part is never checked against its own set.</param>
        /// <param name="options">What to look for, and how many threads to use.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One result per pair that clashes, in the order of their indexes, <see cref="ClashResult.First"/> indexing
        /// the first set and <see cref="ClashResult.Second"/> the second.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either set, a part of it, or the options are null.</exception>
        public static ClashResult[] Find(IReadOnlyList<GeoPreparedSolid3> first, IReadOnlyList<GeoPreparedSolid3> second, ClashOptions options, Tolerance tolerance)
        {
            GeoPreparedSolid3[] one = Checked(first, nameof(first));
            GeoPreparedSolid3[] other = Checked(second, nameof(second));

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return Run(one, other, false, options, tolerance);
        }

        #endregion

        #region Running the check

        private static GeoPreparedSolid3[] Prepare(IReadOnlyList<GeoSolid3> parts, string name, ClashOptions options, Tolerance tolerance)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(name);
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null)
                {
                    throw new ArgumentNullException(name, "Part " + i + " is null.");
                }
            }

            var prepared = new GeoPreparedSolid3[parts.Count];

            Parallel.For(0, parts.Count, Threads(options), i => prepared[i] = new GeoPreparedSolid3(parts[i], tolerance));

            return prepared;
        }

        private static GeoPreparedSolid3[] Checked(IReadOnlyList<GeoPreparedSolid3> parts, string name)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(name);
            }

            var copy = new GeoPreparedSolid3[parts.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = parts[i] ?? throw new ArgumentNullException(name, "Part " + i + " is null.");
            }

            return copy;
        }

        private static ParallelOptions Threads(ClashOptions options) => new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };

        private static ClashResult[] Run(GeoPreparedSolid3[] first, GeoPreparedSolid3[] second, bool oneSet, ClashOptions options, Tolerance tolerance)
        {
            List<(int, int)> pairs = SweepBoxes(first, second, oneSet, options.Clearance + tolerance.EqualPoint);
            var found = new ClashResult[pairs.Count];

            Parallel.For(0, pairs.Count, Threads(options), k =>
            {
                (int i, int j) = pairs[k];
                found[k] = Check(i, first[i], j, second[j], options, tolerance);
            });

            var results = new List<ClashResult>();

            foreach (ClashResult result in found)
            {
                if (result != null)
                {
                    results.Add(result);
                }
            }

            results.Sort((a, b) => a.First != b.First ? a.First.CompareTo(b.First) : a.Second.CompareTo(b.Second));

            return results.ToArray();
        }

        /// <summary>
        /// The pairs whose boxes come within a reach of each other, found by sweeping the boxes along X.
        /// </summary>
        /// <remarks>
        /// Sorted by their low X, a box can only meet the boxes still open when it starts — those whose high X
        /// has not been passed — so each is compared with a handful rather than with every other.
        /// </remarks>
        private static List<(int, int)> SweepBoxes(GeoPreparedSolid3[] first, GeoPreparedSolid3[] second, bool oneSet, double reach)
        {
            // Both sets swept together, each entry knowing which set it is from; one set is swept against itself.
            var entries = new List<(GeoAabb3 Box, int Index, bool FromFirst)>();

            for (int i = 0; i < first.Length; i++)
            {
                if (!first[i].Box.IsEmpty)
                {
                    entries.Add((first[i].Box, i, true));
                }
            }

            if (!oneSet)
            {
                for (int j = 0; j < second.Length; j++)
                {
                    if (!second[j].Box.IsEmpty)
                    {
                        entries.Add((second[j].Box, j, false));
                    }
                }
            }

            entries.Sort((a, b) => a.Box.Min.X.CompareTo(b.Box.Min.X));

            var pairs = new List<(int, int)>();
            var open = new List<int>();

            for (int e = 0; e < entries.Count; e++)
            {
                GeoAabb3 box = entries[e].Box;

                open.RemoveAll(o => entries[o].Box.Max.X + reach < box.Min.X);

                foreach (int o in open)
                {
                    if (!oneSet && entries[o].FromFirst == entries[e].FromFirst)
                    {
                        continue;
                    }

                    GeoAabb3 other = entries[o].Box;

                    if (box.Min.Y > other.Max.Y + reach || other.Min.Y > box.Max.Y + reach
                        || box.Min.Z > other.Max.Z + reach || other.Min.Z > box.Max.Z + reach)
                    {
                        continue;
                    }

                    if (oneSet)
                    {
                        int a = entries[o].Index, b = entries[e].Index;
                        pairs.Add(a < b ? (a, b) : (b, a));
                    }
                    else
                    {
                        pairs.Add(entries[o].FromFirst ? (entries[o].Index, entries[e].Index) : (entries[e].Index, entries[o].Index));
                    }
                }

                open.Add(e);
            }

            return pairs;
        }

        private static ClashResult Check(int i, GeoPreparedSolid3 a, int j, GeoPreparedSolid3 b, ClashOptions options, Tolerance tolerance)
        {
            try
            {
                if (a.CollidesWith(b, tolerance))
                {
                    // Parts that lie against each other across a face of one of them share no volume, and the
                    // boolean would only have cut them into cells to find that out.
                    GeoSolid3[] overlaps = a.IsPartedFrom(b, tolerance) ? new GeoSolid3[0] : a.Intersect(b, tolerance);

                    if (overlaps.Length > 0)
                    {
                        return ClashResult.Hard(i, j, overlaps);
                    }

                    if (!options.IncludeTouching)
                    {
                        return null;
                    }

                    a.TryGetContact(b, out GeoFace3[] contact, tolerance);

                    return ClashResult.Touch(i, j, contact, contact.Length > 0 ? default(GeoPoint3) : a.GetShortestLineTo(b, tolerance).StartPoint);
                }

                // Apart, so one walk of the two indexes both measures the gap and finds where it is.
                if (options.Clearance > 0.0 && a.TryGetShortestLineWithin(b, options.Clearance, tolerance, out GeoLine3 gap))
                {
                    return ClashResult.Near(i, j, gap);
                }

                return null;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("Clash check: parts " + i + " and " + j + " could not be checked and are reported unresolved.", exception);

                return ClashResult.Unresolved(i, j, Middle(a.Box, b.Box), exception);
            }
        }

        /// <summary>
        /// The middle of the space two boxes share, or of the gap between them.
        /// </summary>
        private static GeoPoint3 Middle(GeoAabb3 a, GeoAabb3 b)
        {
            double Mid(double lowA, double highA, double lowB, double highB)
                => (Math.Max(lowA, lowB) + Math.Min(highA, highB)) * 0.5;

            return new GeoPoint3(
                Mid(a.Min.X, a.Max.X, b.Min.X, b.Max.X),
                Mid(a.Min.Y, a.Max.Y, b.Min.Y, b.Max.Y),
                Mid(a.Min.Z, a.Max.Z, b.Min.Z, b.Max.Z));
        }

        #endregion
    }
}
