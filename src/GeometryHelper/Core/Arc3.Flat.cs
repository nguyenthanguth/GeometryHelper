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
                foreach (GeoPoint3 crossing in GetIntersections(arc, edge, tolerance, out _))
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

        /// <summary>
        /// Keeps the crossings found only where the arc comes within the point tolerance of the region, as
        /// <c>CollidesWith</c> reads it: rule R. A crossing within half the tolerance of the arc and of the region put
        /// together settles it without asking the gap of the whole arc, since the two then stand no further apart than that.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="found">The crossings found.</param>
        /// <param name="region">The region, read for the gap.</param>
        /// <param name="box">The box of the region.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static GeoPoint3[] WhereTouching(GeoArc3 arc, List<GeoPoint3> found, Gap3.Region region, GeoAabb3 box, Tolerance tolerance)
        {
            foreach (GeoPoint3 point in found)
            {
                if (arc.DistanceTo(point, tolerance) + region.PointGap(point) <= tolerance.EqualPoint * 0.5)
                {
                    return found.ToArray();
                }
            }

            Gap3.Curve curve = Gap3.Curve.Of(arc);

            bool touches = curve.Box.DistanceTo(box) <= Collision3.Reach(tolerance)
                && curve.GapToRegion(region, tolerance.EqualPoint) <= tolerance.EqualPoint;

            return touches ? found.ToArray() : new GeoPoint3[0];
        }

        /// <summary>
        /// Gets the point where an arc that crosses nothing of a region touches it on its own, as <c>CollidesWith</c> reads a
        /// touch: the point of the arc nearest the region, and none where they do not come within the point tolerance of each
        /// other or the arc lies level over the material, as a curve lying on a surface crosses it nowhere.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="plane">The plane of the region, as its crossings read it; none for a triangle with no area, which no arc lies level in.</param>
        /// <param name="region">The region, read for the gap.</param>
        /// <param name="box">The box of the region.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static GeoPoint3[] TouchAlone(GeoArc3 arc, GeoPlane3? plane, Gap3.Region region, GeoAabb3 box, Tolerance tolerance)
        {
            Gap3.Curve curve = Gap3.Curve.Of(arc);

            if (curve.Box.DistanceTo(box) > Collision3.Reach(tolerance) || curve.GapToRegion(region, tolerance.EqualPoint) > tolerance.EqualPoint)
            {
                return new GeoPoint3[0];
            }

            GeoPoint3 nearest = curve.NearestToRegion(region);

            return plane.HasValue && LiesLevelOver(arc, plane.Value, region, nearest, tolerance) ? new GeoPoint3[0] : new[] { nearest };
        }

        /// <summary>
        /// Determines whether an arc may come within the point tolerance of what a box holds: no point of it stands further from
        /// its centre than its radius, so a box further than that and the reach of the tolerance from the centre holds nothing
        /// it touches. It spares a shape plainly clear of the arc the reading of the arc for its gap.
        /// </summary>
        internal static bool MayTouch(GeoArc3 arc, GeoAabb3 box, Tolerance tolerance)
            => !(box.DistanceTo(arc.Center) - arc.Radius > Collision3.Reach(tolerance));

        /// <summary>
        /// Determines whether an arc lies level over a region where it comes nearest it: in the region's plane, as
        /// <see cref="IsCoplanar"/> reads it, rising off it by no more than the planar tolerance anywhere round its circle, its
        /// nearest point standing over the material. Tilted further, an arc of radius 300 tilted 9E-6 rises 0.0027 off the
        /// plane round its circle, and touches the material where it dips nearest it.
        /// </summary>
        internal static bool LiesLevelOver(GeoArc3 arc, GeoPlane3 plane, Gap3.Region region, GeoPoint3 nearest, Tolerance tolerance)
            => IsCoplanar(arc, plane, tolerance) && region.Holds(nearest);

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
        /// the triangle's plane, the points where it cuts one of the three edges. None where the arc does not come within
        /// the point tolerance of the triangle, as <see cref="CollidesWith(GeoArc3, GeoTriangle3, Tolerance)"/> reads it.
        /// Where it touches the triangle without crossing it, the point of the arc nearest the triangle, unless the arc lies
        /// level over it within the tolerance. A triangle with no area is the segment it has collapsed to, crossed where its
        /// sides are.
        /// </returns>
        /// <remarks>
        /// A point of the arc is read as on the triangle within the point tolerance of its plane and within the tolerance of
        /// its outline in that plane, and the two together reach further than the tolerance off a side: an arc starting 0.00107
        /// beside a side of the triangle and 0.0007 over its plane, and running away from it, is 0.0013 from the triangle, and
        /// is given no point now, where it was given one.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoTriangle3 triangle, Tolerance tolerance)
        {
            List<GeoPoint3> found;

            // A triangle with no area is the segment it has collapsed to, crossed where its sides are, and touched alone where
            // it is not: it has no plane for an arc to lie level in.
            if (triangle.IsDegenerate(tolerance))
            {
                found = new List<GeoPoint3>();

                AddCrossingsOfEdges(found, arc, Intersection3.SidesOfThin(triangle), tolerance);

                if (found.Count > 0)
                {
                    return WhereTouching(arc, found, Gap3.Region.Of(triangle), triangle.GetAabb(), tolerance);
                }

                return MayTouch(arc, triangle.GetAabb(), tolerance) ? TouchAlone(arc, null, Gap3.Region.Of(triangle), triangle.GetAabb(), tolerance) : found.ToArray();
            }

            if (!IsCoplanar(arc, triangle.GetPlane(), tolerance))
            {
                found = PiercingPoints(arc, triangle.GetPlane(), point => Containment3.Contains(triangle, point, tolerance), tolerance);
            }
            else
            {
                found = new List<GeoPoint3>();

                AddCrossingsOfEdges(found, arc, new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) }, tolerance);
            }

            if (found.Count > 0)
            {
                return WhereTouching(arc, found, Gap3.Region.Of(triangle), triangle.GetAabb(), tolerance);
            }

            return MayTouch(arc, triangle.GetAabb(), tolerance) ? TouchAlone(arc, triangle.GetPlane(), Gap3.Region.Of(triangle), triangle.GetAabb(), tolerance) : found.ToArray();
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
        /// The triangle is read filled, and they touch where the exact gap between them is no more than the point tolerance:
        /// an arc lying in the triangle's plane and wholly within it touches, one 0.0005 off its side in the plane
        /// touches, and one hovering 0.0003 over it touches. A triangle with no area is the segment it has collapsed to, and
        /// touches as its distance says.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoTriangle3 triangle, Tolerance tolerance)
        {
            Gap3.Curve curve = Gap3.Curve.Of(arc);

            return curve.Box.DistanceTo(triangle.GetAabb()) <= Collision3.Reach(tolerance)
                && curve.GapToRegion(Gap3.Region.Of(triangle), tolerance.EqualPoint) <= tolerance.EqualPoint;
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
        /// the polygon's plane, the points where it cuts the outline. None where the arc does not come within the point
        /// tolerance of the polygon, as <see cref="CollidesWith(GeoArc3, GeoPolygon3, Tolerance)"/> reads it.
        /// </returns>
        /// <remarks>
        /// A point of the arc is read as on the polygon within the point tolerance of its plane and within the tolerance of
        /// its outline in that plane, as for a triangle, and is kept only where the arc touches the polygon.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            List<GeoPoint3> found = CrossingsOf(arc, polygon, tolerance);

            if (found.Count > 0)
            {
                return WhereTouching(arc, found, Gap3.Region.Of(polygon), polygon.GetAabb(), tolerance);
            }

            return MayTouch(arc, polygon.GetAabb(), tolerance) ? TouchAlone(arc, polygon.GetPlane(), Gap3.Region.Of(polygon), polygon.GetAabb(), tolerance) : found.ToArray();
        }

        /// <summary>
        /// Gets the points where an arc crosses a polygon, each read on its own, as a box reads each of its faces before it
        /// asks whether the arc touches the box at all.
        /// </summary>
        internal static List<GeoPoint3> CrossingsOf(GeoArc3 arc, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (!IsCoplanar(arc, polygon.GetPlane(), tolerance))
            {
                return PiercingPoints(arc, polygon.GetPlane(), point => Containment3.Contains(polygon, point, tolerance), tolerance);
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, arc, polygon.GetEdges(), tolerance);

            return found;
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
        /// <remarks>
        /// The polygon is read filled, and they touch where the exact gap between them is no more than the point tolerance,
        /// as for a triangle.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoArc3 arc, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            Gap3.Curve curve = Gap3.Curve.Of(arc);

            return curve.Box.DistanceTo(polygon.GetAabb()) <= Collision3.Reach(tolerance)
                && curve.GapToRegion(Gap3.Region.Of(polygon), tolerance.EqualPoint) <= tolerance.EqualPoint;
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
        /// outline or the rim of a hole. None where the arc does not come within the point tolerance of the material, as
        /// <see cref="CollidesWith(GeoArc3, GeoFace3, Tolerance)"/> reads it.
        /// </returns>
        /// <remarks>
        /// A point of the arc is read as on the face within the point tolerance of its plane and within the tolerance of its
        /// outline and of the rim of each hole, as for a triangle, and is kept only where the arc touches the material: an
        /// arc in a hole starting 0.0012 off its rim and 0.0003 over the plane, and running away from it, is given no point.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPoint3[] crossings = CutsOf(arc, face, tolerance);

            if (crossings.Length > 0 || !MayTouch(arc, face.GetAabb(), tolerance))
            {
                return crossings;
            }

            return TouchAlone(arc, face.GetPlane(), Gap3.Region.Of(face), face.GetAabb(), tolerance);
        }

        /// <summary>
        /// Gets where an arc crosses a face, and nothing where it only touches it: the places the cutting of a curved chain by
        /// a face cuts at, since a touch cuts nothing.
        /// </summary>
        internal static GeoPoint3[] CutsOf(GeoArc3 arc, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            List<GeoPoint3> found = CrossingsOf(arc, face, tolerance);

            return found.Count == 0 ? found.ToArray() : WhereTouching(arc, found, Gap3.Region.Of(face), face.GetAabb(), tolerance);
        }

        /// <summary>
        /// Gets the points where an arc crosses a face, each read on its own, as a body reads each of its faces before it
        /// asks whether a point found lies on its surface.
        /// </summary>
        internal static List<GeoPoint3> CrossingsOf(GeoArc3 arc, GeoFace3 face, Tolerance tolerance)
        {
            if (!IsCoplanar(arc, face.GetPlane(), tolerance))
            {
                return PiercingPoints(arc, face.GetPlane(), point => Containment3.Contains(face, point, tolerance), tolerance);
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, arc, face.Boundary.GetEdges(), tolerance);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                AddCrossingsOfEdges(found, arc, hole.GetEdges(), tolerance);
            }

            return found;
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
        /// where there is no face, and touches nothing. They touch where the exact gap between them is no more than the
        /// point tolerance, as for a triangle.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoArc3 arc, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            Gap3.Curve curve = Gap3.Curve.Of(arc);

            return curve.Box.DistanceTo(face.GetAabb()) <= Collision3.Reach(tolerance)
                && curve.GapToRegion(Gap3.Region.Of(face), tolerance.EqualPoint) <= tolerance.EqualPoint;
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
