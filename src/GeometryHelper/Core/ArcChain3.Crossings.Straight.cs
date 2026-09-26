using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a curved chain or loop in space crosses a segment or a ray.
    /// </summary>
    /// <remarks>
    /// These two probes were missing from the set, not refused: a bar could be asked about a plane, an arc, a
    /// triangle, a polygon, a face, either box and a body, and not about a segment. The reason was one step back
    /// — a <see cref="GeoEdge3"/> offers a direction only where a segment and a bend answer with the same shape
    /// of call, and a segment's crossing with another segment was offered as one point rather than as a list.
    /// With that written out as a list, these follow.
    /// </remarks>
    internal static partial class ArcChain3
    {

        /// <summary>
        /// Gets every point where a curved chain crosses a segment.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoLine3 line)
            => GetIntersections(chain, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoLine3 line, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(line, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoLine3 line) => CollidesWith(chain, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a segment, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoLine3 line, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Touches(chain.GetEdges(), edge => edge.CollidesWith(line, tolerance));
        }

        /// <summary>
        /// Tries to find where a curved chain crosses a segment.
        /// </summary>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(chain, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved chain crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a ray.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoRay3 ray)
            => GetIntersections(chain, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoRay3 ray, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(ray, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a ray.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoRay3 ray) => CollidesWith(chain, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a ray, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoRay3 ray, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Touches(chain.GetEdges(), edge => edge.CollidesWith(ray, tolerance));
        }

        /// <summary>
        /// Tries to find where a curved chain crosses a ray.
        /// </summary>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(chain, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved chain crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, ray, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a segment.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoLine3 line)
            => GetIntersections(loop, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoLine3 line, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(line, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoLine3 line) => CollidesWith(loop, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a segment, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoLine3 line, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Touches(loop.GetEdges(), edge => edge.CollidesWith(line, tolerance));
        }

        /// <summary>
        /// Tries to find where a curved loop crosses a segment.
        /// </summary>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(loop, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved loop crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a ray.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoRay3 ray)
            => GetIntersections(loop, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoRay3 ray, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(ray, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a ray.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoRay3 ray) => CollidesWith(loop, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a ray, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoRay3 ray, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Touches(loop.GetEdges(), edge => edge.CollidesWith(ray, tolerance));
        }

        /// <summary>
        /// Tries to find where a curved loop crosses a ray.
        /// </summary>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(loop, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved loop crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, ray, tolerance);

            return intersections.Length > 0;
        }
    }
}
