using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Extension
{
    public static partial class MergeExtension
    {
        /// <summary>
        /// Joins segments into chains by matching their endpoints.
        /// </summary>
        public static GeoPolyline3[] Join(this IEnumerable<GeoLine3> lines) => Merge3.Join(lines);

        /// <summary>
        /// Joins segments into chains by matching their endpoints, within a tolerance.
        /// </summary>
        /// <param name="lines">The segments, in any order and pointing either way.</param>
        /// <param name="tolerance">The tolerance: two ends nearer than this count as meeting.</param>
        /// <returns>One chain for every unbroken run.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        public static GeoPolyline3[] Join(this IEnumerable<GeoLine3> lines, Tolerance tolerance)
            => Merge3.Join(lines, tolerance);

        /// <summary>
        /// Joins chains into longer chains by matching their ends.
        /// </summary>
        public static GeoPolyline3[] Join(this IEnumerable<GeoPolyline3> polylines) => Merge3.Join(polylines);

        /// <summary>
        /// Joins chains into longer chains by matching their ends, within a tolerance.
        /// </summary>
        /// <param name="polylines">The chains, in any order and running either way.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One chain for every unbroken run.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polylines"/> is null.</exception>
        public static GeoPolyline3[] Join(this IEnumerable<GeoPolyline3> polylines, Tolerance tolerance)
            => Merge3.Join(polylines, tolerance);

        /// <summary>
        /// Rejoins segments already in order along a run, where one ends and the next begins.
        /// </summary>
        public static GeoLine3[] MergeConsecutive(this IEnumerable<GeoLine3> segments) => Merge3.ConsecutiveLines(segments);

        /// <summary>
        /// Rejoins segments already in order along a run, where one ends and the next begins, within a tolerance.
        /// </summary>
        /// <param name="segments">The pieces, in order along the run they were cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One segment per unbroken stretch, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        public static GeoLine3[] MergeConsecutive(this IEnumerable<GeoLine3> segments, Tolerance tolerance)
            => Merge3.ConsecutiveLines(segments, tolerance);

        /// <summary>
        /// Rejoins chains already in order along a run, where one ends and the next begins.
        /// </summary>
        public static GeoPolyline3[] MergeConsecutive(this IEnumerable<GeoPolyline3> polylines)
            => Merge3.ConsecutivePolylines(polylines);

        /// <summary>
        /// Rejoins chains already in order along a run, where one ends and the next begins, within a tolerance.
        /// </summary>
        /// <param name="polylines">The pieces, in order along the run they were cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One chain per unbroken stretch, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polylines"/> is null.</exception>
        public static GeoPolyline3[] MergeConsecutive(this IEnumerable<GeoPolyline3> polylines, Tolerance tolerance)
            => Merge3.ConsecutivePolylines(polylines, tolerance);

        /// <summary>
        /// Runs a set of chains together into one, end to end.
        /// </summary>
        public static GeoPolyline3 MergeIntoOne(this IEnumerable<GeoPolyline3> polylines) => Merge3.Polylines(polylines);

        /// <summary>
        /// Runs a set of chains together into one, end to end, within a tolerance.
        /// </summary>
        /// <param name="polylines">The chains to run together.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One chain holding all of them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polylines"/> is null.</exception>
        public static GeoPolyline3 MergeIntoOne(this IEnumerable<GeoPolyline3> polylines, Tolerance tolerance)
            => Merge3.Polylines(polylines, tolerance);

        /// <summary>
        /// Runs faces that share a plane and an edge together into single faces.
        /// </summary>
        public static GeoFace3[] MergeCoplanar(this IEnumerable<GeoFace3> faces) => Merge3.CoplanarFaces(faces);

        /// <summary>
        /// Runs faces that share a plane and an edge together into single faces, within a tolerance.
        /// </summary>
        /// <param name="faces">The faces to consider.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces with each coplanar, adjoining group replaced by one face.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="faces"/> is null.</exception>
        /// <remarks>
        /// This is what turns a triangulated skin back into the flat panels it stands for, which is why a body
        /// read out of a mesh format is worth passing through it before anything is measured against the faces
        /// one by one.
        /// </remarks>
        public static GeoFace3[] MergeCoplanar(this IEnumerable<GeoFace3> faces, Tolerance tolerance)
            => Merge3.CoplanarFaces(faces, tolerance);
    }
}
