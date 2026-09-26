using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a straight chain in space crosses another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoPolyline3"/> could say how far off nine kinds of shape were and whether it touched
    /// three. Everything the distance needed was there; what was missing was the shorter question. A chain is
    /// a run of segments, so every answer here is the union of what its segments answer, and a
    /// <see cref="GeoLine3"/> already answers all of it — nothing new is worked out.
    /// </para>
    /// <para>
    /// Two segments share a vertex, so a crossing at a vertex is found twice and named once. This is the walk
    /// <see cref="ArcChain3"/> keeps for a chain that curves; the two differ only in what a piece is.
    /// </para>
    /// </remarks>
    public static partial class Intersection3
    {

        /// <summary>
        /// Gets every point where a straight chain crosses a plane.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoPlane3 plane)
            => GetIntersections(polyline, plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="plane">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoPlane3 plane, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(plane, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a plane.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoPlane3 plane, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, plane, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="plane">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, plane, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a segment.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoLine3 line)
            => GetIntersections(polyline, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="line">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoLine3 line, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(line, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a segment.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="line">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a ray.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoRay3 ray)
            => GetIntersections(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="ray">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoRay3 ray, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(ray, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a ray.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="ray">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, ray, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses an arc.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoArc3 arc)
            => GetIntersections(polyline, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="arc">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoArc3 arc, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(arc, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses an arc.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoArc3 arc, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="arc">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a circle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoCircle3 circle)
            => GetIntersections(polyline, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="circle">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoCircle3 circle, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(circle, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a circle.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoCircle3 circle, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="circle">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a triangle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoTriangle3 triangle)
            => GetIntersections(polyline, triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="triangle">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoTriangle3 triangle, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(triangle, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a triangle.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="triangle">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a polygon.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoPolygon3 polygon)
            => GetIntersections(polyline, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="polygon">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoPolygon3 polygon, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(polygon, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a polygon.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="polygon">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a face.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoFace3 face)
            => GetIntersections(polyline, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a face, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="face">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoFace3 face, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(face, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a face.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a face, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="face">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, face, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoObb3 box)
            => GetIntersections(polyline, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a box, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="box">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoObb3 box, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(box, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a box.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a box, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="box">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a square box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoAabb3 box)
            => GetIntersections(polyline, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a square box, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="box">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoAabb3 box, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(box, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a square box.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a square box, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="box">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a straight chain crosses a body.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoSolid3 solid)
            => GetIntersections(polyline, solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where a straight chain crosses a body, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="solid">The shape to cross with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoSolid3 solid, Tolerance tolerance)
            => CrossingsOfSegments(polyline, segment => segment.GetIntersections(solid, tolerance), tolerance);

        /// <summary>
        /// Tries to find where a straight chain crosses a body.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a straight chain crosses a body, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="solid">The shape to cross with.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where two straight chains cross.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoPolyline3 other)
            => GetIntersections(polyline, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where two straight chains cross, within a tolerance.
        /// </summary>
        /// <param name="polyline">The first chain.</param>
        /// <param name="other">The second chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place they meet, each named once; two segments lying along each other name none.</returns>
        /// <remarks>
        /// Every pair of segments is asked, so the work grows with the two counts multiplied. Each segment
        /// carries a box round itself and a pair whose boxes cannot reach each other is dropped before any
        /// arithmetic, and the second chain's boxes are worked out once rather than once per segment.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoPolyline3 other, Tolerance tolerance)
        {
            RequireChain(polyline, nameof(polyline));
            RequireChain(other, nameof(other));

            GeoLine3[] others = SegmentsOf(other);
            var boxes = new GeoAabb3[others.Length];

            for (int i = 0; i < others.Length; i++)
            {
                boxes[i] = others[i].GetAabb();
            }

            var found = new List<GeoPoint3>();

            for (int s = 0; s < polyline.EdgeCount; s++)
            {
                GeoLine3 segment = polyline.GetEdgeAt(s);
                GeoAabb3 box = segment.GetAabb();

                for (int i = 0; i < others.Length; i++)
                {
                    if (!box.CollidesWith(boxes[i], tolerance))
                    {
                        continue;
                    }

                    foreach (GeoPoint3 crossing in segment.GetIntersections(others[i], tolerance))
                    {
                        Arc3.AddOnce(found, crossing, tolerance);
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where two straight chains cross.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoPolyline3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(polyline, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where two straight chains cross, within a tolerance.
        /// </summary>
        /// <param name="polyline">The first chain.</param>
        /// <param name="other">The second chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either chain is null.</exception>
        public static bool TryIntersectWith(GeoPolyline3 polyline, GeoPolyline3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gathers what each segment of a chain answers, naming each place once.
        /// </summary>
        private static GeoPoint3[] CrossingsOfSegments(GeoPolyline3 polyline, Func<GeoLine3, GeoPoint3[]> of, Tolerance tolerance)
        {
            RequireChain(polyline, nameof(polyline));

            var found = new List<GeoPoint3>();

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                foreach (GeoPoint3 crossing in of(polyline.GetEdgeAt(i)))
                {
                    // Two segments share a vertex, so a crossing there is found by both of them.
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }

            return found.ToArray();
        }

        private static GeoLine3[] SegmentsOf(GeoPolyline3 polyline)
        {
            var segments = new GeoLine3[polyline.EdgeCount];

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                segments[i] = polyline.GetEdgeAt(i);
            }

            return segments;
        }

        private static void RequireChain(GeoPolyline3 polyline, string name)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
