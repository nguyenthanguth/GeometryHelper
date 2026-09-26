using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Extension
{
    /// <summary>
    /// Provides extension methods that put curves which meet end to end back together.
    /// </summary>
    /// <remarks>
    /// <see cref="Merge2"/> and <see cref="Merge3"/> have done this work all along and no type reached it,
    /// so a caller had to name the core class to join a bag of segments. These put it where the collection
    /// is. Nothing here computes anything of its own.
    /// </remarks>
    public static partial class MergeExtension
    {
        /// <summary>
        /// Joins segments into chains by matching their endpoints, as a drafting program's JOIN does.
        /// </summary>
        public static GeoPolyline2[] Join(this IEnumerable<GeoLine2> lines) => Merge2.Join(lines);

        /// <summary>
        /// Joins segments into chains by matching their endpoints, within a tolerance.
        /// </summary>
        /// <param name="lines">The segments, in any order and pointing either way.</param>
        /// <param name="tolerance">The tolerance: two ends nearer than this count as meeting.</param>
        /// <returns>One chain for every unbroken run, with redundant junctions between collinear pieces removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        /// <remarks>
        /// Where three or more segments meet at a point, which one a run takes up is settled by input order:
        /// at a fork there is no geometric reason to prefer one branch. The answer is the same every time for
        /// a given input, but reordering the input can move a branch from one run to another.
        /// </remarks>
        public static GeoPolyline2[] Join(this IEnumerable<GeoLine2> lines, Tolerance tolerance)
            => Merge2.Join(lines, tolerance);

        /// <summary>
        /// Rejoins segments already in order along a run, where one ends and the next begins.
        /// </summary>
        public static GeoLine2[] MergeConsecutive(this IEnumerable<GeoLine2> segments) => Merge2.ConsecutiveLines(segments);

        /// <summary>
        /// Rejoins segments already in order along a run, where one ends and the next begins, within a tolerance.
        /// </summary>
        /// <param name="segments">The pieces, in order along the run they were cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One segment per unbroken stretch, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        /// <remarks>
        /// This is the counterpart of <see cref="Join(IEnumerable{GeoLine2}, Tolerance)"/> for pieces whose
        /// order is already known, which is what splitting hands back: it walks the list once instead of
        /// searching, and it will not reverse anything to make it fit.
        /// </remarks>
        public static GeoLine2[] MergeConsecutive(this IEnumerable<GeoLine2> segments, Tolerance tolerance)
            => Merge2.ConsecutiveLines(segments, tolerance);

        /// <summary>
        /// Rejoins chains already in order along a run, where one ends and the next begins.
        /// </summary>
        public static GeoPolyline2[] MergeConsecutive(this IEnumerable<GeoPolyline2> polylines)
            => Merge2.ConsecutivePolylines(polylines);

        /// <summary>
        /// Rejoins chains already in order along a run, where one ends and the next begins, within a tolerance.
        /// </summary>
        /// <param name="polylines">The pieces, in order along the run they were cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One chain per unbroken stretch, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polylines"/> is null.</exception>
        public static GeoPolyline2[] MergeConsecutive(this IEnumerable<GeoPolyline2> polylines, Tolerance tolerance)
            => Merge2.ConsecutivePolylines(polylines, tolerance);

        /// <summary>
        /// Joins this chain to another if this one ends where the other begins.
        /// </summary>
        public static GeoPolyline2 MergeWith(this GeoPolyline2 first, GeoPolyline2 second) => Merge2.Polylines(first, second);

        /// <summary>
        /// Joins this chain to another if this one ends where the other begins, within a tolerance.
        /// </summary>
        /// <param name="first">This chain.</param>
        /// <param name="second">The chain that may continue it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The joined chain, or <c>null</c> when the two do not meet end to start.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either chain is null.</exception>
        /// <remarks>
        /// Null rather than an exception, because not meeting is the ordinary case: walking the pieces of a
        /// cut run, this is exactly what tells one stretch from the next.
        /// </remarks>
        public static GeoPolyline2 MergeWith(this GeoPolyline2 first, GeoPolyline2 second, Tolerance tolerance)
            => Merge2.Polylines(first, second, tolerance);
    }
}
