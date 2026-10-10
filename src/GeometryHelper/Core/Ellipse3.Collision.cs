using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether the rim of an ellipse in space touches another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As the circle in space is asked: touching is crossing, read from the rim, except where the other shape holds the
    /// whole rim without crossing it. A plane the ellipse lies in, a flat region of its plane that holds it wholly, and a
    /// body or a box it lies inside all touch it and cross it nowhere; so do a curve lying on the rim along its length.
    /// Those are told by where the rim starts, the end of its major axis, as the circle tells them by where it starts.
    /// </para>
    /// <para>
    /// The disc is not read. A segment through the middle of the ellipse that never reaches the rim touches nothing here,
    /// as it touches nothing of a circle; <see cref="Contains(GeoEllipse3, GeoPoint3, Tolerance)"/> is the one that reads
    /// the ellipse as filled.
    /// </para>
    /// </remarks>
    public static partial class Ellipse3
    {
        #region Against a plane, a segment and a ray

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a plane, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPlane3 plane) => CollidesWith(ellipse, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a plane, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rim crosses or touches the plane, or lies in it; otherwise, false. The ellipse 300 by 100 lying in the
        /// plan touches the plan everywhere and crosses it nowhere, so this is true where
        /// <see cref="GetIntersections(GeoEllipse3, GeoPlane3, Tolerance)"/> is empty; the plane z = 1 it does not touch.
        /// </returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            return IsCoplanar(ellipse, plane, tolerance) || PointsOnPlane(ellipse, plane, tolerance).Count > 0;
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a segment, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoLine3 line) => CollidesWith(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the segment crosses or touches the rim; otherwise, false. The rim is what counts, not the disc: the
        /// segment from (0, 0, -10) to (0, 0, 10) passes through the middle of the ellipse 300 by 100 lying in the plan about
        /// the origin and touches nothing.
        /// </returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoLine3 line, Tolerance tolerance)
            => GetIntersections(ellipse, line, tolerance).Length > 0;

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a ray, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoRay3 ray) => CollidesWith(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a ray, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ray crosses or touches the rim; otherwise, false, a ray through the disc alone included.</returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoRay3 ray, Tolerance tolerance)
            => GetIntersections(ellipse, ray, tolerance).Length > 0;

        #endregion

        #region Against a curve

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an arc, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoArc3 arc) => CollidesWith(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the arc crosses or touches the rim, or lies along it; otherwise, false. A quarter of the circle of radius
        /// 100 about the origin lies along the ellipse 100 by 99.9995 about the same centre, within the point tolerance of a
        /// thousandth, crosses it nowhere, and touches it.
        /// </returns>
        /// <remarks>
        /// An arc lying on the rim crosses it nowhere and touches it all along, so where the ellipse is the arc's circle,
        /// within the tolerance, the two touch when either holds an end of the other, as two arcs of one circle do.
        /// </remarks>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoArc3 arc, Tolerance tolerance)
        {
            if (Arc3.IsCoplanar(arc, ellipse.GetPlane(), tolerance)
                && arc.Center.IsEqualTo(ellipse.Center, tolerance)
                && Math.Abs(ellipse.MajorRadius - arc.Radius) <= tolerance.EqualPoint
                && Math.Abs(ellipse.MinorRadius - arc.Radius) <= tolerance.EqualPoint)
            {
                return arc.IsPointOn(StartOf(ellipse), tolerance)
                    || OnRim(ellipse, arc.StartPoint, tolerance)
                    || OnRim(ellipse, arc.EndPoint, tolerance);
            }

            return GetIntersections(ellipse, arc, tolerance).Length > 0;
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a circle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoCircle3 circle) => CollidesWith(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rims cross or touch, or draw the same curve; otherwise, false. The circle of radius 50 lying in the
        /// plan about the origin is inside the ellipse 300 by 100 about it and touches nothing: the rims are what count, not
        /// the discs.
        /// </returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoCircle3 circle, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, circle.GetPlane(), tolerance)
                && ellipse.Center.IsEqualTo(circle.Center, tolerance)
                && Math.Abs(ellipse.MajorRadius - circle.Radius) <= tolerance.EqualPoint
                && Math.Abs(ellipse.MinorRadius - circle.Radius) <= tolerance.EqualPoint)
            {
                GeoArc3 rim = Arc3.AsArc(circle);

                return OnRim(ellipse, rim.StartPoint, tolerance) || rim.IsPointOn(StartOf(ellipse), tolerance);
            }

            return GetIntersections(ellipse, circle, tolerance).Length > 0;
        }

        /// <summary>
        /// Determines whether the rims of two ellipses in space touch, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoEllipse3 other) => CollidesWith(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Determines whether the rims of two ellipses in space touch, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rims cross or touch, or the two are the same ellipse; otherwise, false. The ellipse 150 by 50 lying in
        /// the plan inside the ellipse 300 by 100 about the same centre touches nothing.
        /// </returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoEllipse3 other, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, other.GetPlane(), tolerance)
                && Ellipse2.IsEqualTo(Flat(ellipse), LayOut(ellipse, other), tolerance))
            {
                return OnRim(ellipse, StartOf(other), tolerance) || OnRim(other, StartOf(ellipse), tolerance);
            }

            return GetIntersections(ellipse, other, tolerance).Length > 0;
        }

        #endregion

        #region Against a flat region

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a triangle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoTriangle3 triangle) => CollidesWith(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rim crosses or touches the triangle, or lies in its plane wholly within it; otherwise, false. The
        /// ellipse 300 by 100 lying in the plan about the origin touches the triangle (-1000, -1000, 0), (1000, -1000, 0),
        /// (0, 1000, 0) and cuts none of its edges.
        /// </returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (GetIntersections(ellipse, triangle, tolerance).Length > 0)
            {
                return true;
            }

            return IsCoplanar(ellipse, triangle.GetPlane(), tolerance)
                && Containment3.Contains(triangle, StartOf(ellipse), tolerance);
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPolygon3 polygon) => CollidesWith(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rim crosses or touches the polygon, or lies in its plane wholly within it; otherwise, false. The
        /// square 800 by 800 about the origin in the plan holds the ellipse 300 by 100 about the same centre, and touches it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (GetIntersections(ellipse, polygon, tolerance).Length > 0)
            {
                return true;
            }

            return IsCoplanar(ellipse, polygon.GetPlane(), tolerance)
                && Containment3.Contains(polygon, StartOf(ellipse), tolerance);
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoFace3 face) => CollidesWith(ellipse, face, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rim crosses or touches the material, or lies in the face's plane wholly within it; otherwise, false.
        /// The material is what counts: a rim lying wholly in a hole, or passing down one, touches nothing.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoFace3 face, Tolerance tolerance)
        {
            if (GetIntersections(ellipse, face, tolerance).Length > 0)
            {
                return true;
            }

            return IsCoplanar(ellipse, face.GetPlane(), tolerance)
                && Containment3.Contains(face, StartOf(ellipse), tolerance);
        }

        #endregion

        #region Against a body or a box

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoSolid3 solid) => CollidesWith(ellipse, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a solid, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the rim lies in the material or crosses its surface; otherwise, false. The ellipse 30 by 10 lying in the
        /// plan about the centre of a box 200 by 200 by 200 crosses no face and touches it.
        /// </returns>
        /// <remarks>
        /// Where the rim starts is asked before any face is tried, because a rim inside the body crosses its surface nowhere
        /// and is in it all the same. The same question keeps a rim inside an opening out: an opening is not material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Containment3.Contains(solid, StartOf(ellipse), tolerance)
                || GetIntersections(ellipse, solid, tolerance).Length > 0;
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an oriented box, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoObb3 box) => CollidesWith(ellipse, box, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim lies inside the box or meets one of its faces; otherwise, false.</returns>
        /// <remarks>
        /// A rim wholly inside the box crosses no face and is in it all the same, so where it starts is asked before any face
        /// is tried.
        /// </remarks>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoObb3 box, Tolerance tolerance)
        {
            return Containment3.Contains(box, StartOf(ellipse), tolerance)
                || GetIntersections(ellipse, box, tolerance).Length > 0;
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoAabb3 box) => CollidesWith(ellipse, box, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box; an empty one is touched by nothing.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim lies inside the box or meets one of its faces; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            return CollidesWith(ellipse, box.ToObb(), tolerance);
        }

        #endregion

        #region Against an edge and a curved chain

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an edge, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoEdge3 edge) => CollidesWith(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches an edge, within a tolerance: as a segment or as an arc,
        /// whichever the edge is.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(ellipse, edge.ToArc(), tolerance)
                : CollidesWith(ellipse, edge.ToLine(), tolerance);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPolylineArc3 chain) => CollidesWith(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if any edge of the chain touches the rim; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            foreach (GeoEdge3 edge in chain.GetEdges())
            {
                if (CollidesWith(ellipse, edge, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPolygonArc3 loop) => CollidesWith(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of an ellipse in space touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if any edge of the loop touches the rim; otherwise, false: the loop is read as its edges, as a circle reads it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoEllipse3 ellipse, GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            foreach (GeoEdge3 edge in loop.GetEdges())
            {
                if (CollidesWith(ellipse, edge, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
