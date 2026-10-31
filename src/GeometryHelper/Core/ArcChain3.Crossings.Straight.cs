using System;
using System.Collections.Generic;
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

            // A segment no longer than the tolerance is read as its start unless it truly meets the chain.
            if (Stub3.ReadsAsStart(line, tolerance))
            {
                GeoPoint3[] asItIs = Meetings(chain.GetEdges(), false, edge => Meeting(edge, line, Stub3.AsItIs(line, tolerance)), Stub3.AsItIs(line, tolerance));

                return Stub3.Meeting(line, asItIs, asItIs.Length == 0 && chain.DistanceTo(line.StartPoint, tolerance) <= tolerance.EqualPoint);
            }

            return Meetings(chain.GetEdges(), false, edge => Meeting(edge, line, tolerance), tolerance);
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

            return Touches(chain.GetEdges(), edge => IsShortStraight(edge, tolerance)
                ? ShortTouches(edge, line, tolerance)
                : edge.CollidesWith(line, tolerance));
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

            return Meetings(chain.GetEdges(), false, edge => Meeting(edge, ray, tolerance), tolerance);
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

            return Touches(chain.GetEdges(), edge => IsShortStraight(edge, tolerance)
                ? ShortTouches(edge, ray, tolerance)
                : edge.CollidesWith(ray, tolerance));
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

            // A segment no longer than the tolerance is read as its start unless it truly meets the outline.
            if (Stub3.ReadsAsStart(line, tolerance))
            {
                GeoPoint3[] asItIs = Meetings(loop.GetEdges(), true, edge => Meeting(edge, line, Stub3.AsItIs(line, tolerance)), Stub3.AsItIs(line, tolerance));

                return Stub3.Meeting(line, asItIs, asItIs.Length == 0 && loop.DistanceTo(line.StartPoint, tolerance) <= tolerance.EqualPoint);
            }

            return Meetings(loop.GetEdges(), true, edge => Meeting(edge, line, tolerance), tolerance);
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

            return Touches(loop.GetEdges(), edge => IsShortStraight(edge, tolerance)
                ? ShortTouches(edge, line, tolerance)
                : edge.CollidesWith(line, tolerance));
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

            return Meetings(loop.GetEdges(), true, edge => Meeting(edge, ray, tolerance), tolerance);
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

            return Touches(loop.GetEdges(), edge => IsShortStraight(edge, tolerance)
                ? ShortTouches(edge, ray, tolerance)
                : edge.CollidesWith(ray, tolerance));
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

        /// <summary>
        /// Gets where an edge meets a segment, and whether the point given is the point of a touch rather than a crossing.
        /// </summary>
        private static (GeoPoint3[] Points, bool Touch) Meeting(GeoEdge3 edge, GeoLine3 line, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return (Intersection3.OfSide(edge.ToLine(), line, tolerance), false);
            }

            GeoPoint3[] points = Arc3.GetIntersections(edge.ToArc(), line, tolerance, out bool touch);
            return (points, touch);
        }

        /// <summary>
        /// Gets where an edge meets a ray, and whether the point given is the point of a touch rather than a crossing.
        /// </summary>
        private static (GeoPoint3[] Points, bool Touch) Meeting(GeoEdge3 edge, GeoRay3 ray, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return (Intersection3.OfSide(edge.ToLine(), ray, tolerance), false);
            }

            GeoPoint3[] points = Arc3.GetIntersections(edge.ToArc(), ray, tolerance, out bool touch);
            return (points, touch);
        }

        /// <summary>
        /// Gets where the edges of a chain or a loop meet a straight piece, each place named once: two points within the point
        /// tolerance of each other are one place, and so are two the edges either side of a corner give, both within the point
        /// tolerance of the corner. Of two so taken for one, a crossing is kept over the point of a touch, and otherwise the
        /// first.
        /// </summary>
        /// <param name="edges">The edges, in order.</param>
        /// <param name="closed">Whether the last edge runs back into the first, as a loop's does.</param>
        /// <param name="of">Where an edge meets the piece, and whether that is a touch.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A segment passing a chain beside the corner where an arc meets a straight leg was given a place on each of them,
        /// 0.001 apart at a tolerance of a thousandth, both within it of the corner: one meeting named twice. Places further
        /// from the corner are kept however close the two legs run: a segment crossing both legs of a hairpin 0.0009 across,
        /// at 50 from its tip, crosses them 0.043 apart, and is given both.
        /// </remarks>
        private static GeoPoint3[] Meetings(IEnumerable<GeoEdge3> edges, bool closed, Func<GeoEdge3, (GeoPoint3[] Points, bool Touch)> of, Tolerance tolerance)
        {
            var list = new List<GeoEdge3>(edges);
            var found = new List<GeoPoint3>();
            var touches = new List<bool>();
            var owners = new List<int>();

            for (int k = 0; k < list.Count; k++)
            {
                (GeoPoint3[] points, bool touch) = of(list[k]);

                foreach (GeoPoint3 point in points)
                {
                    int same = Named(found, owners, list, k, closed, point, tolerance);

                    if (same < 0)
                    {
                        found.Add(point);
                        touches.Add(touch);
                        owners.Add(k);
                    }
                    else if (touches[same] && !touch)
                    {
                        found[same] = point;
                        touches[same] = false;
                        owners[same] = k;
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the place already named that a point an edge gives is: in the same place within the point tolerance, or, with
        /// both within it of the corner the edge shares with the edge before it, given by that edge; -1 where it is none.
        /// </summary>
        private static int Named(List<GeoPoint3> found, List<int> owners, List<GeoEdge3> edges, int edge, bool closed, GeoPoint3 point, Tolerance tolerance)
        {
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].IsEqualTo(point, tolerance))
                {
                    return i;
                }

                int other = owners[i];
                bool before = other == edge - 1;
                bool around = closed && edge == edges.Count - 1 && other == 0 && edges.Count > 2;

                if (before || around)
                {
                    GeoPoint3 corner = before ? edges[edge].StartPoint : edges[0].StartPoint;

                    if (corner.IsEqualTo(point, tolerance) && corner.IsEqualTo(found[i], tolerance))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
