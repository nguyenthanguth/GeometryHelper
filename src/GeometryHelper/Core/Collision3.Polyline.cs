using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether a straight chain in space touches another shape.
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
    public static partial class Collision3
    {

        /// <summary>
        /// Determines whether a straight chain touches a plane.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoPlane3 plane) => CollidesWith(polyline, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a plane, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="plane">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoPlane3 plane, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(plane, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoLine3 line) => CollidesWith(polyline, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a segment, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="line">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoLine3 line, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(line, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a ray.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoRay3 ray) => CollidesWith(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a ray, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="ray">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoRay3 ray, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(ray, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches an arc.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoArc3 arc) => CollidesWith(polyline, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches an arc, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="arc">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoArc3 arc, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(arc, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a circle.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoCircle3 circle) => CollidesWith(polyline, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a circle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="circle">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoCircle3 circle, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(circle, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a triangle.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoTriangle3 triangle) => CollidesWith(polyline, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="triangle">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoTriangle3 triangle, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(triangle, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoPolygon3 polygon) => CollidesWith(polyline, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="polygon">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoPolygon3 polygon, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(polygon, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a face.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoFace3 face) => CollidesWith(polyline, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a face, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="face">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoFace3 face, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(face, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a box.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoObb3 box) => CollidesWith(polyline, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a box, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="box">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoObb3 box, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(box, tolerance));

        /// <summary>
        /// Determines whether a straight chain touches a square box.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoAabb3 box) => CollidesWith(polyline, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a straight chain touches a square box, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="box">The shape to test against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any of its segments touches; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoAabb3 box, Tolerance tolerance)
            => TouchedBySegment(polyline, segment => segment.CollidesWith(box, tolerance));

        /// <summary>
        /// Determines whether any segment of a chain touches a shape, stopping at the first that does.
        /// </summary>
        private static bool TouchedBySegment(GeoPolyline3 polyline, Func<GeoLine3, bool> of)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (of(polyline.GetEdgeAt(i)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether two straight chains touch.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoPolyline3 other) => CollidesWith(polyline, other, Tolerance.Global);

        /// <summary>
        /// Determines whether two straight chains touch, within a tolerance.
        /// </summary>
        /// <param name="polyline">The first chain.</param>
        /// <param name="other">The second chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <remarks>
        /// Two segments lying along each other meet along a length and cross nowhere, so this is not the
        /// crossing test with the place thrown away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either chain is null.</exception>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoPolyline3 other, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            for (int s = 0; s < polyline.EdgeCount; s++)
            {
                GeoLine3 segment = polyline.GetEdgeAt(s);
                GeoAabb3 box = segment.GetAabb();

                for (int i = 0; i < other.EdgeCount; i++)
                {
                    GeoLine3 against = other.GetEdgeAt(i);

                    if (box.CollidesWith(against.GetAabb(), tolerance) && segment.CollidesWith(against, tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
