using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;
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
    /// <para>
    /// Reinforcement can be checked without building its bodies: a <see cref="ClashBar"/> is a centre line and a
    /// radius, and a bar runs into a part where the part comes nearer its centre line than the radius. The two ways
    /// stand side by side; see <see cref="Find(IReadOnlyList{ClashBar}, IReadOnlyList{GeoPreparedSolid3}, ClashOptions, Tolerance)"/>.
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

        #region Bars by their centre lines

        /// <summary>
        /// Checks every bar against every part, reading the bars by their centre lines, using the default options and
        /// tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<ClashBar> bars, IReadOnlyList<GeoSolid3> parts)
            => Find(bars, parts, ClashOptions.Default, Tolerance.Global);

        /// <summary>
        /// Checks every bar against every part, reading the bars by their centre lines, using the default tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<ClashBar> bars, IReadOnlyList<GeoSolid3> parts, ClashOptions options)
            => Find(bars, parts, options, Tolerance.Global);

        /// <summary>
        /// Checks every bar against every part, reading the bars by their centre lines, within a tolerance.
        /// </summary>
        /// <param name="bars">The bars.</param>
        /// <param name="parts">The parts, such as the steel the bars pass.</param>
        /// <param name="options">What to look for, and how many threads to use.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One result per pair that clashes, in the order of their indexes, <see cref="ClashResult.First"/> indexing the
        /// bars and <see cref="ClashResult.Second"/> the parts.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the bars, the parts, one of either, or the options are null.</exception>
        public static ClashResult[] Find(IReadOnlyList<ClashBar> bars, IReadOnlyList<GeoSolid3> parts, ClashOptions options, Tolerance tolerance)
        {
            ClashBar[] checkedBars = Checked(bars, nameof(bars));
            return Find(checkedBars, Prepare(parts, nameof(parts), options, tolerance), options, tolerance);
        }

        /// <summary>
        /// Checks every bar against every prepared part, reading the bars by their centre lines, using the default
        /// options and tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<ClashBar> bars, IReadOnlyList<GeoPreparedSolid3> parts)
            => Find(bars, parts, ClashOptions.Default, Tolerance.Global);

        /// <summary>
        /// Checks every bar against every prepared part, reading the bars by their centre lines, using the default
        /// tolerance.
        /// </summary>
        public static ClashResult[] Find(IReadOnlyList<ClashBar> bars, IReadOnlyList<GeoPreparedSolid3> parts, ClashOptions options)
            => Find(bars, parts, options, Tolerance.Global);

        /// <summary>
        /// Checks every bar against every prepared part, reading the bars by their centre lines, within a tolerance.
        /// </summary>
        /// <param name="bars">The bars.</param>
        /// <param name="parts">The parts, already prepared: the same parts can be checked against bodies and against bars.</param>
        /// <param name="options">What to look for, and how many threads to use.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One result per pair that clashes, in the order of their indexes, <see cref="ClashResult.First"/> indexing the
        /// bars and <see cref="ClashResult.Second"/> the parts.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the bars, the parts, one of either, or the options are null.</exception>
        /// <remarks>
        /// <para>
        /// A bar runs into a part where its centre line runs inside the part, or comes nearer the part's surface than
        /// the radius; it touches where it comes exactly the radius away, and comes too near where it comes within the
        /// clearance of that. Openings are honoured, as for bodies: a bar through a hole it clears is no clash.
        /// </para>
        /// <para>
        /// What a clash between bodies measures as a volume, a bar measures as a depth and a length: how far the part
        /// reaches into the bar (<see cref="ClashResult.Depth"/>) and how much of the centre line runs inside it
        /// (<see cref="ClashResult.LengthInside"/>). No body is built, so <see cref="ClashResult.Overlaps"/> is empty
        /// and <see cref="ClashResult.Volume"/> nought, and <see cref="ClashOptions.MinimumVolume"/> does not apply;
        /// <see cref="ClashOptions.MinimumDepth"/> does. The gap of a bar too near a part runs from the bar's surface.
        /// </para>
        /// <para>
        /// The ends are read rounded (see <see cref="ClashBar"/>), so right at an end a part up to a radius beyond it
        /// is found to clash.
        /// </para>
        /// </remarks>
        public static ClashResult[] Find(IReadOnlyList<ClashBar> bars, IReadOnlyList<GeoPreparedSolid3> parts, ClashOptions options, Tolerance tolerance)
        {
            ClashBar[] checkedBars = Checked(bars, nameof(bars));
            GeoPreparedSolid3[] checkedParts = Checked(parts, nameof(parts));

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var barBoxes = new GeoAabb3[checkedBars.Length];

            for (int i = 0; i < barBoxes.Length; i++)
            {
                barBoxes[i] = checkedBars[i].Box;
            }

            List<(int, int)> pairs = SweepBoxes(barBoxes, Boxes(checkedParts), false, options.Clearance + tolerance.EqualPoint);
            var found = new ClashResult[pairs.Count];

            Parallel.For(0, pairs.Count, Threads(options), k =>
            {
                (int i, int j) = pairs[k];
                found[k] = CheckBar(i, checkedBars[i], j, checkedParts[j], options, tolerance);
            });

            return Ordered(found);
        }

        private static ClashBar[] Checked(IReadOnlyList<ClashBar> bars, string name)
        {
            if (bars == null)
            {
                throw new ArgumentNullException(name);
            }

            var copy = new ClashBar[bars.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = bars[i] ?? throw new ArgumentNullException(name, "Bar " + i + " is null.");
            }

            return copy;
        }

        /// <summary>
        /// Checks one bar against one part by the bar's centre line.
        /// </summary>
        private static ClashResult CheckBar(int i, ClashBar bar, int j, GeoPreparedSolid3 part, ClashOptions options, Tolerance tolerance)
        {
            try
            {
                double radius = bar.Radius;
                double reach = radius + options.Clearance + tolerance.EqualPoint;
                GeoPoint3[] axis = bar.Points;

                // Where the centre line runs inside the part: each chord cut where it crosses the surface, and each piece
                // judged by its middle. A chord whose box, grown by the reach, misses the part's box is neither inside nor
                // near it.
                var inside = new List<(GeoPoint3 From, GeoPoint3 To)>();
                double lengthInside = 0.0;

                for (int k = 0; k + 1 < axis.Length; k++)
                {
                    GeoPoint3 from = axis[k];
                    GeoPoint3 to = axis[k + 1];
                    double length = from.DistanceTo(to);

                    if (length <= tolerance.EqualPoint || !Near(from, to, reach, part.Box, tolerance))
                    {
                        continue;
                    }

                    var ray = new GeoRay3(from, to);
                    var stops = new List<double> { 0.0 };

                    foreach (GeoPoint3 crossing in part.GetIntersections(ray, tolerance))
                    {
                        double at = ray.GetDistanceAtPoint(crossing);

                        if (at > tolerance.EqualPoint && at < length - tolerance.EqualPoint)
                        {
                            stops.Add(at);
                        }
                    }

                    stops.Add(length);

                    for (int s = 0; s + 1 < stops.Count; s++)
                    {
                        if (stops[s + 1] - stops[s] <= tolerance.EqualPoint)
                        {
                            continue;
                        }

                        GeoPoint3 middle = ray.GetPointAtDistance((stops[s] + stops[s + 1]) * 0.5);

                        if (part.Locate(middle, tolerance) == PointLocation.Inside)
                        {
                            inside.Add((ray.GetPointAtDistance(stops[s]), ray.GetPointAtDistance(stops[s + 1])));
                            lengthInside += stops[s + 1] - stops[s];
                        }
                    }
                }

                if (lengthInside > tolerance.EqualPoint)
                {
                    return Graded(ClashResult.BarHard(i, j, LongestMiddle(inside), DepthWithin(inside, part, radius, tolerance), lengthInside), options, false);
                }

                // The centre line stays outside: its nearest approach to the surface, measured a piece at a time, each
                // piece a few radii long so that the box it is judged by is close round it.
                bool any = false;
                GeoLine3 nearest = default(GeoLine3);
                double best = reach;
                double step = Math.Max(8.0 * radius, tolerance.EqualPoint);

                for (int k = 0; k + 1 < axis.Length; k++)
                {
                    GeoPoint3 from = axis[k];
                    GeoPoint3 to = axis[k + 1];
                    double length = from.DistanceTo(to);

                    if (length <= tolerance.EqualPoint || !Near(from, to, best, part.Box, tolerance))
                    {
                        continue;
                    }

                    int pieces = Math.Max(1, (int)Math.Ceiling(length / step));
                    GeoPoint3 start = from;

                    for (int p = 1; p <= pieces; p++)
                    {
                        GeoPoint3 end = p == pieces ? to : Along(from, to, (double)p / pieces);

                        if (part.Index.TryGetShortestLineTo(new GeoLine3(start, end), best, tolerance, out GeoLine3 line))
                        {
                            best = line.Length;
                            nearest = line;
                            any = true;
                        }

                        start = end;
                    }
                }

                if (!any)
                {
                    return null;
                }

                double distance = nearest.Length;
                GeoPoint3 onPart = nearest.EndPoint;
                GeoPoint3 onBar = distance > tolerance.EqualPoint
                    ? nearest.StartPoint.Add(nearest.StartPoint.GetVectorTo(onPart).Multiply(radius / distance))
                    : nearest.StartPoint;

                if (distance < radius - tolerance.EqualPoint)
                {
                    // The part reaches into the bar: from its nearest point, inside the bar, out to the bar's surface.
                    return Graded(ClashResult.BarHard(i, j, Midway(onPart, onBar), radius - distance, 0.0), options, false);
                }

                if (distance <= radius + tolerance.EqualPoint)
                {
                    return options.IncludeTouching ? ClashResult.BarTouch(i, j, onPart) : null;
                }

                if (options.Clearance > 0.0 && distance - radius <= options.Clearance)
                {
                    return ClashResult.Near(i, j, new GeoLine3(onBar, onPart));
                }

                return null;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("Clash check: bar " + i + " and part " + j + " could not be checked and are reported unresolved.", exception);

                return ClashResult.Unresolved(i, j, Middle(bar.Box, part.Box), exception);
            }
        }

        /// <summary>
        /// Whether a chord, grown by a reach, can come near a box.
        /// </summary>
        private static bool Near(GeoPoint3 from, GeoPoint3 to, double reach, GeoAabb3 box, Tolerance tolerance)
            => GeoAabb3.FromPoints(new[] { from, to }).Expand(reach).CollidesWith(box, tolerance);

        /// <summary>
        /// How deep a part reaches into a bar whose centre line runs inside it: the radius, and as far again as the centre
        /// line runs beneath the surface, at most the diameter.
        /// </summary>
        /// <remarks>
        /// How far beneath the surface the centre line runs is measured along each piece inside, at both its ends and at
        /// points an eighth of a radius apart between them, at most 4096 spans to a piece, and the measuring stops once it
        /// passes the radius: the depth can be no more than the diameter. A distance to the surface changes no faster than
        /// the point moves, so between two points it can exceed what they measure by a sixteenth of a radius at most; where
        /// the centre line turns inside the part, the turn is the end of a piece, and measured as it is.
        /// </remarks>
        private static double DepthWithin(List<(GeoPoint3 From, GeoPoint3 To)> inside, GeoPreparedSolid3 part, double radius, Tolerance tolerance)
        {
            double deepest = 0.0;

            foreach ((GeoPoint3 from, GeoPoint3 to) in inside)
            {
                int spans = (int)Math.Min(4096.0, Math.Max(1.0, Math.Ceiling(from.DistanceTo(to) / (0.125 * radius))));

                for (int s = 0; s <= spans && deepest < radius; s++)
                {
                    // To the surface: the body itself calls a point inside it nought away.
                    deepest = Math.Max(deepest, part.Index.DistanceTo(Along(from, to, (double)s / spans), tolerance));
                }

                if (deepest >= radius)
                {
                    break;
                }
            }

            return Math.Min(2.0 * radius, radius + deepest);
        }

        /// <summary>
        /// The middle of the longest piece of a centre line inside a part.
        /// </summary>
        private static GeoPoint3 LongestMiddle(List<(GeoPoint3 From, GeoPoint3 To)> inside)
        {
            (GeoPoint3 From, GeoPoint3 To) longest = inside[0];

            foreach ((GeoPoint3 From, GeoPoint3 To) piece in inside)
            {
                if (piece.From.DistanceTo(piece.To) > longest.From.DistanceTo(longest.To))
                {
                    longest = piece;
                }
            }

            return Midway(longest.From, longest.To);
        }

        private static GeoPoint3 Along(GeoPoint3 from, GeoPoint3 to, double fraction)
            => new GeoPoint3(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction, from.Z + (to.Z - from.Z) * fraction);

        private static GeoPoint3 Midway(GeoPoint3 a, GeoPoint3 b) => Along(a, b, 0.5);

        /// <summary>
        /// A hard clash the options take as touching when it is too shallow, or for bodies too small, reported as
        /// touching when touching is asked for and not at all otherwise.
        /// </summary>
        private static ClashResult Graded(ClashResult hard, ClashOptions options, bool measuresVolume)
        {
            bool tooShallow = options.MinimumDepth > 0.0 && hard.Depth < options.MinimumDepth;
            bool tooSmall = measuresVolume && options.MinimumVolume > 0.0 && hard.Volume < options.MinimumVolume;

            if (!tooShallow && !tooSmall)
            {
                return hard;
            }

            return options.IncludeTouching ? ClashResult.Shallow(hard) : null;
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
            List<(int, int)> pairs = SweepBoxes(Boxes(first), Boxes(second), oneSet, options.Clearance + tolerance.EqualPoint);
            var found = new ClashResult[pairs.Count];

            Parallel.For(0, pairs.Count, Threads(options), k =>
            {
                (int i, int j) = pairs[k];
                found[k] = Check(i, first[i], j, second[j], options, tolerance);
            });

            return Ordered(found);
        }

        private static GeoAabb3[] Boxes(GeoPreparedSolid3[] parts)
        {
            var boxes = new GeoAabb3[parts.Length];

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i] = parts[i].Box;
            }

            return boxes;
        }

        /// <summary>
        /// The results found, in the order of the pairs' indexes, whatever order the threads finished in.
        /// </summary>
        private static ClashResult[] Ordered(ClashResult[] found)
        {
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
        /// The pairs whose boxes come within a reach of each other, found by sweeping the boxes along the axis that
        /// leaves the fewest pairs to look at.
        /// </summary>
        /// <remarks>
        /// Sorted by their low coordinate along that axis, a box can only meet the boxes still open when it starts —
        /// those whose high coordinate has not been passed — so each is compared with a handful rather than with every
        /// other. Along an axis the boxes all overlap along, that handful is all of them: 4 900 bars 6 000 long laid side
        /// by side, swept along their length, were compared 12 002 550 times, and 169 050 times swept across. The other
        /// two axes are compared each way round, so the pairs found are the same along any axis, and only the order they
        /// are found in changes; the results are put in the order of their pairs.
        /// </remarks>
        internal static List<(int, int)> SweepBoxes(GeoAabb3[] first, GeoAabb3[] second, bool oneSet, double reach)
        {
            // Both sets swept together, each entry knowing which set it is from; one set is swept against itself.
            var entries = new List<(GeoAabb3 Box, int Index, bool FromFirst)>();

            for (int i = 0; i < first.Length; i++)
            {
                if (!first[i].IsEmpty)
                {
                    entries.Add((first[i], i, true));
                }
            }

            if (!oneSet)
            {
                for (int j = 0; j < second.Length; j++)
                {
                    if (!second[j].IsEmpty)
                    {
                        entries.Add((second[j], j, false));
                    }
                }
            }

            int axis = SweepAxis(entries, reach);
            int across = axis == 0 ? 1 : 0, along = axis == 2 ? 1 : 2;
            int count = entries.Count;

            // The low and high coordinate of each box along X, Y and Z, six to a box, read without copying the boxes.
            var extents = new double[6 * count];
            var order = new int[count];

            for (int e = 0; e < count; e++)
            {
                GeoAabb3 box = entries[e].Box;
                extents[6 * e] = box.Min.X;
                extents[(6 * e) + 1] = box.Max.X;
                extents[(6 * e) + 2] = box.Min.Y;
                extents[(6 * e) + 3] = box.Max.Y;
                extents[(6 * e) + 4] = box.Min.Z;
                extents[(6 * e) + 5] = box.Max.Z;
                order[e] = e;
            }

            // Boxes starting at the same place are taken in the order they were entered.
            Array.Sort(order, (a, b) =>
            {
                int byLow = extents[(6 * a) + (2 * axis)].CompareTo(extents[(6 * b) + (2 * axis)]);
                return byLow != 0 ? byLow : a.CompareTo(b);
            });

            var pairs = new List<(int, int)>();
            var open = new List<int>();

            foreach (int e in order)
            {
                double low = extents[(6 * e) + (2 * axis)];

                open.RemoveAll(o => extents[(6 * o) + (2 * axis) + 1] + reach < low);

                foreach (int o in open)
                {
                    if (!oneSet && entries[o].FromFirst == entries[e].FromFirst)
                    {
                        continue;
                    }

                    if (Apart(extents, e, o, across, reach) || Apart(extents, e, o, along, reach))
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

        /// <summary>
        /// The axis a sweep of the boxes looks at the fewest pairs along, 0 for X, 1 for Y and 2 for Z, the first of
        /// them where two look at as many.
        /// </summary>
        /// <param name="entries">The boxes to sweep.</param>
        /// <param name="reach">How far apart two boxes may stand and still be a pair.</param>
        internal static int SweepAxis(List<(GeoAabb3 Box, int Index, bool FromFirst)> entries, double reach)
        {
            int count = entries.Count;
            var lows = new double[count];
            var highs = new double[count];
            int axis = 0;
            long fewest = long.MaxValue;

            for (int candidate = 0; candidate < 3; candidate++)
            {
                for (int i = 0; i < count; i++)
                {
                    lows[i] = Along(entries[i].Box.Min, candidate);
                    highs[i] = Along(entries[i].Box.Max, candidate) + reach;
                }

                Array.Sort(lows);
                long tried = Sweeps.Tried(lows, highs);

                if (tried < fewest)
                {
                    fewest = tried;
                    axis = candidate;
                }
            }

            return axis;
        }

        /// <summary>A coordinate of a point: 0 for X, 1 for Y and 2 for Z.</summary>
        private static double Along(GeoPoint3 point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;

        /// <summary>
        /// Whether two boxes stand further apart than a reach along an axis, 0 for X, 1 for Y and 2 for Z, either way
        /// round.
        /// </summary>
        /// <param name="extents">The low and high coordinate of each box along X, Y and Z, six to a box.</param>
        /// <param name="box">One box, by index.</param>
        /// <param name="other">The other.</param>
        /// <param name="axis">The axis.</param>
        /// <param name="reach">How far apart the two may stand and still be a pair.</param>
        private static bool Apart(double[] extents, int box, int other, int axis, double reach)
        {
            int a = (6 * box) + (2 * axis), b = (6 * other) + (2 * axis);
            return extents[a] > extents[b + 1] + reach || extents[b] > extents[a + 1] + reach;
        }

        private static ClashResult Check(int i, GeoPreparedSolid3 a, int j, GeoPreparedSolid3 b, ClashOptions options, Tolerance tolerance)
        {
            try
            {
                if (a.CollidesWith(b, tolerance))
                {
                    // Two convex parts that plainly share a region have it clipped out at once. Otherwise parts that
                    // lie against each other across a face of one of them share no volume, and the boolean would only
                    // have cut them into cells to find that out; what is left is the boolean's to decide.
                    if (!a.TryIntersectConvex(b, tolerance, out GeoSolid3[] overlaps))
                    {
                        overlaps = a.IsPartedFrom(b, tolerance) ? new GeoSolid3[0] : Boolean3.Intersect(a.Material, b.Material, tolerance);
                    }

                    if (overlaps.Length > 0)
                    {
                        return Graded(ClashResult.Hard(i, j, overlaps, tolerance), options, true);
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
