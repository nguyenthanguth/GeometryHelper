using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where an arc in space crosses a flat region, and whether it touches one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flat region has a plane and a boundary, and which of the two the answer comes from depends on how the
    /// arc lies.
    /// </para>
    /// <para>
    /// <b>Not coplanar.</b> The arc meets the region's plane in at most two points, and each of those counts
    /// only if the region holds it. That is a crossing of the region's <i>material</i>, and it is the case a
    /// bar leaving a plate is.
    /// </para>
    /// <para>
    /// <b>Coplanar.</b> The arc runs in the plane of the region, so it crosses nothing by piercing it and the
    /// answer is where it cuts the <i>boundary</i> — the outline, and for a face the rim of every hole. An arc
    /// lying wholly within the material crosses nothing and still touches, which is the one place the two
    /// questions part, as it is for a plane.
    /// </para>
    /// </remarks>
    public static partial class Arc3
    {
        #region Reading a boundary

        /// <summary>
        /// Gets the points where an arc cuts a run of edges.
        /// </summary>
        private static void AddCrossingsOfEdges(List<GeoPoint3> found, GeoArc3 arc, IEnumerable<GeoLine3> edges, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                foreach (GeoPoint3 crossing in GetIntersections(arc, edge, tolerance))
                {
                    // Two edges share a corner, so an arc through a corner is found twice.
                    AddOnce(found, crossing, tolerance);
                }
            }
        }

        /// <summary>
        /// Gets the points of the arc that a flat region holds, where the arc does not lie in its plane.
        /// </summary>
        private static List<GeoPoint3> PiercingPoints(GeoArc3 arc, GeoPlane3 plane, Func<GeoPoint3, bool> holds, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(arc, plane, tolerance))
            {
                if (holds(candidate))
                {
                    AddOnce(found, candidate, tolerance);
                }
            }

            return found;
        }

        #endregion

        #region Against a triangle

        /// <summary>
        /// Gets every point where an arc crosses a triangle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoTriangle3 triangle)
            => GetIntersections(arc, triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the arc pierces the triangle, the points of the face it goes through; where the arc lies in
        /// the triangle's plane, the points where it cuts one of the three edges.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (!IsCoplanar(arc, triangle.GetPlane(), tolerance))
            {
                return PiercingPoints(arc, triangle.GetPlane(),
                    point => Containment3.Contains(triangle, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, arc, new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) }, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a triangle.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoTriangle3 triangle) => CollidesWith(arc, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// An arc lying in the triangle's plane and wholly within it cuts no edge and still touches, so where
        /// the two are coplanar the arc's own start is asked about as well.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (GetIntersections(arc, triangle, tolerance).Length > 0)
            {
                return true;
            }

            return IsCoplanar(arc, triangle.GetPlane(), tolerance)
                && Containment3.Contains(triangle, arc.StartPoint, tolerance);
        }

        /// <summary>
        /// Tries to find where an arc crosses a triangle.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, triangle, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a polygon

        /// <summary>
        /// Gets every point where an arc crosses a polygon.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoPolygon3 polygon)
            => GetIntersections(arc, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the arc pierces the polygon, the points of the region it goes through; where the arc lies in
        /// the polygon's plane, the points where it cuts the outline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!IsCoplanar(arc, polygon.GetPlane(), tolerance))
            {
                return PiercingPoints(arc, polygon.GetPlane(),
                    point => Containment3.Contains(polygon, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, arc, polygon.GetEdges(), tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoPolygon3 polygon) => CollidesWith(arc, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoArc3 arc, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (GetIntersections(arc, polygon, tolerance).Length > 0)
            {
                return true;
            }

            return IsCoplanar(arc, polygon.GetPlane(), tolerance)
                && Containment3.Contains(polygon, arc.StartPoint, tolerance);
        }

        /// <summary>
        /// Tries to find where an arc crosses a polygon.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, polygon, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a face

        /// <summary>
        /// Gets every point where an arc crosses a face.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoFace3 face) => GetIntersections(arc, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses a face, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the arc pierces the face, the points of its <i>material</i> it goes through, so a bar passing
        /// down a hole reaches nothing; where the arc lies in the face's plane, the points where it cuts the
        /// outline or the rim of a hole.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!IsCoplanar(arc, face.GetPlane(), tolerance))
            {
                return PiercingPoints(arc, face.GetPlane(),
                    point => Containment3.Contains(face, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, arc, face.Boundary.GetEdges(), tolerance);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                AddCrossingsOfEdges(found, arc, hole.GetEdges(), tolerance);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a face.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoFace3 face) => CollidesWith(arc, face, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a face, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The material is what counts. An arc running down the middle of a hole pierces the plane of the face
        /// where there is no face, and touches nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoArc3 arc, GeoFace3 face, Tolerance tolerance)
        {
            if (GetIntersections(arc, face, tolerance).Length > 0)
            {
                return true;
            }

            return IsCoplanar(arc, face.GetPlane(), tolerance)
                && Containment3.Contains(face, arc.StartPoint, tolerance);
        }

        /// <summary>
        /// Tries to find where an arc crosses a face.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses a face, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, face, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region A circle against the same

        /// <summary>
        /// Gets every point where a circle crosses a triangle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoTriangle3 triangle)
            => GetIntersections(AsArc(circle), triangle);

        /// <summary>
        /// Gets every point where a circle crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoTriangle3 triangle, Tolerance tolerance)
            => GetIntersections(AsArc(circle), triangle, tolerance);

        /// <summary>
        /// Determines whether a circle touches a triangle.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoTriangle3 triangle) => CollidesWith(AsArc(circle), triangle);

        /// <summary>
        /// Determines whether a circle touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 circle, GeoTriangle3 triangle, Tolerance tolerance)
            => CollidesWith(AsArc(circle), triangle, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses a triangle.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), triangle, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), triangle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses a polygon.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoPolygon3 polygon)
            => GetIntersections(AsArc(circle), polygon);

        /// <summary>
        /// Gets every point where a circle crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoPolygon3 polygon, Tolerance tolerance)
            => GetIntersections(AsArc(circle), polygon, tolerance);

        /// <summary>
        /// Determines whether a circle touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoPolygon3 polygon) => CollidesWith(AsArc(circle), polygon);

        /// <summary>
        /// Determines whether a circle touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 circle, GeoPolygon3 polygon, Tolerance tolerance)
            => CollidesWith(AsArc(circle), polygon, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses a polygon.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), polygon, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), polygon, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses a face.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoFace3 face) => GetIntersections(AsArc(circle), face);

        /// <summary>
        /// Gets every point where a circle crosses a face, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoFace3 face, Tolerance tolerance)
            => GetIntersections(AsArc(circle), face, tolerance);

        /// <summary>
        /// Determines whether a circle touches a face.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoFace3 face) => CollidesWith(AsArc(circle), face);

        /// <summary>
        /// Determines whether a circle touches a face, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 circle, GeoFace3 face, Tolerance tolerance)
            => CollidesWith(AsArc(circle), face, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses a face.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), face, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses a face, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), face, out intersections, tolerance);

        #endregion
    }
}
