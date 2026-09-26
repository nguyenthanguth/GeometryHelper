using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where two straight pieces in space cross, read as a list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two straight pieces meet at one point, so the answer was offered as one point:
    /// <see cref="GetIntersection(GeoLine3, GeoLine3)"/> gives a nullable point and nothing gave a list. That is
    /// the better shape when a segment is what is in hand, and the wrong one when it is not: a
    /// <see cref="GeoEdge3"/> is a segment or a bend and can only offer a direction where both answer with the
    /// same shape of call, so a segment could be asked about every flat and solid shape in the library and not
    /// about another segment. The plane has the same exception written out for the same reason.
    /// </para>
    /// <para>
    /// So this is the one-point answer as a list of length one. Nothing new is worked out, and the reading for
    /// pieces lying along each other is the one two arcs of one circle already get: they meet along a length
    /// rather than at a place, so no place is named and <c>CollidesWith</c> is what says they touch.
    /// </para>
    /// </remarks>
    public static partial class Intersection3
    {
        #region Two segments

        /// <summary>
        /// Gets where two segments cross, as a list.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line1, GeoLine3 line2) => GetIntersections(line1, line2, Tolerance.Global);

        /// <summary>
        /// Gets where two segments cross, as a list, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The one place they cross, or nothing: two segments that miss each other, that are skew, or that lie
        /// along each other all name no place.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            GeoPoint3? crossing = GetIntersection(line1, line2, tolerance);

            return crossing.HasValue ? new[] { crossing.Value } : new GeoPoint3[0];
        }

        #endregion

        #region A segment and a ray

        /// <summary>
        /// Gets where a segment crosses a ray.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoRay3 ray) => GetIntersections(line, ray, Tolerance.Global);

        /// <summary>
        /// Gets where a segment crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The one place they cross, or nothing.</returns>
        /// <remarks>
        /// Two lines in space usually miss each other altogether, so the question is answered by the shortest
        /// line between the two: they cross where that line has no length, and the reach of each piece is
        /// already built into it. A segment lying along the ray meets it along a length and names no place.
        /// <para>
        /// A ray is a value type and cannot be refused for being absent, so a ray with no direction is read as
        /// its own origin and nothing more: the question becomes whether the segment holds that point.
        /// </para>
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoRay3 ray, Tolerance tolerance)
        {
            GeoLine3 join = Projection3.GetShortestLineTo(ray, line, tolerance);

            if (join.Length > tolerance.EqualPoint || ray.IsParallelTo(line, tolerance))
            {
                return new GeoPoint3[0];
            }

            return new[] { join.StartPoint };
        }

        /// <summary>
        /// Gets where a ray crosses a segment.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoLine3 line) => GetIntersections(line, ray, Tolerance.Global);

        /// <summary>
        /// Gets where a ray crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The one place they cross, or nothing.</returns>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoLine3 line, Tolerance tolerance) => GetIntersections(line, ray, tolerance);

        /// <summary>
        /// Tries to find where a segment crosses a ray.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(line, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a segment crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The one place they cross when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine3 line, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(line, ray, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where a ray crosses a segment.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(line, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The one place they cross when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoRay3 ray, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(line, ray, out intersections, tolerance);

        #endregion
    }
}
