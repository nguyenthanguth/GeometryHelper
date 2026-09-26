using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a curved loop nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything but the nearest point to a point is worked out in the loop's own plane: the probe is laid out
    /// there, the plane library answers, and the answer is lifted back, so it is exact.
    /// </para>
    /// <para>
    /// <b>A point needs no coplanarity and every other shape does.</b> That is not a compromise: a point off the
    /// plane stands at the same height above every point of the boundary, so the nearest place to it is the
    /// nearest place to its shadow. Two shapes in different planes have no such shortcut, so they are refused,
    /// as they are for the booleans -- SharesPlaneWith asks beforehand.
    /// </para>
    /// <para>
    /// GetClosestEdge takes a <b>primitive</b> probe only -- a point, a segment, an arc, a circle or an edge.
    /// The nearest edge of one many-edged shape to another is a <i>pair</i> of edges, which is a different
    /// question and not this one.
    /// </para>
    /// </remarks>
    public sealed partial class GeoPolygonArc3
    {
        /// <summary>
        /// Gets the point of the outline nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the outline nearest another point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.ClosestPoint(Edges(), point, tolerance);

        #region The joining line

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a point.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The line leaving the boundary and landing on the point.</returns>
        /// <remarks>
        /// A point off the plane stands at the same height above every point of the boundary, so the nearest
        /// place to it is the nearest place to its shadow. That makes this exact without any coplanarity.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => new GeoLine3(GetClosestPointOnBoundary(point, tolerance), point);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a segment in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a segment in the same plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the segment does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = FrameFor(new[] { line.StartPoint, line.EndPoint }, tolerance, nameof(line));

            return Lift(frame, Flat(frame).GetShortestLineTo(PlanarMap.ProjectToLine2(frame, line), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to an arc in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc) => GetShortestLineTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to an arc in the same plane, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the arc does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, Flat(frame).GetShortestLineTo(Flatten(frame, arc, tolerance, nameof(arc)), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a circle in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle) => GetShortestLineTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a circle in the same plane, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the circle does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, Flat(frame).GetShortestLineTo(Flatten(frame, circle, tolerance, nameof(circle)), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to an edge in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to an edge in the same plane, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the edge does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, Flat(frame).GetShortestLineTo(Flatten(frame, edge, tolerance, nameof(edge)), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a straight chain in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a straight chain in the same plane, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            GeoCoordinateSystem3 frame = FrameFor(polyline.Vertices, tolerance, nameof(polyline));

            return Lift(frame, Flat(frame).GetShortestLineTo(PlanarMap.ProjectToPolyline2(frame, polyline), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a curved chain in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain) => GetShortestLineTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a curved chain in the same plane, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            GeoCoordinateSystem3 frame = GetFrame();

            if (!PlanarMap.TryToPolylineArc2(frame, chain, out GeoPolylineArc2 flat, tolerance))
            {
                throw new ArgumentException(OutOfPlane("chain"), nameof(chain));
            }

            return Lift(frame, Flat(frame).GetShortestLineTo(flat, tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a polygon in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => GetShortestLineTo(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a polygon in the same plane, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the polygon does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoCoordinateSystem3 frame = FrameFor(polygon.Vertices, tolerance, nameof(polygon));

            return Lift(frame, Flat(frame).GetShortestLineTo(PlanarMap.ProjectToPolygon2(frame, polygon), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to another curved loop in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to another curved loop in the same plane, within a tolerance.
        /// </summary>
        /// <param name="other">The other loop; it has to lie in this one's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the other loop lies in a different plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, Flat(frame).GetShortestLineTo(Flatten(other, frame, tolerance, nameof(other)), tolerance));
        }

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a face in the same plane.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest line from this loop's boundary to a face in the same plane, within a tolerance.
        /// </summary>
        /// <param name="face">The face; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the face does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoCoordinateSystem3 frame = FrameFor(face.Boundary.Vertices, tolerance, nameof(face));

            return Lift(frame, Flat(frame).GetShortestLineTo(PlanarMap.ProjectToFace2(frame, face), tolerance));
        }

        #endregion

        #region The nearest edge

        /// <summary>
        /// Gets the edge of this loop that comes nearest a point.
        /// </summary>
        public GeoEdge3 GetClosestEdge(GeoPoint3 point) => GetClosestEdge(point, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this loop that comes nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge, as a straight leg or a bend.</returns>
        /// <remarks>
        /// A point off the plane stands at the same height above every edge, so the nearest edge to it is the
        /// nearest edge to its shadow.
        /// </remarks>
        public GeoEdge3 GetClosestEdge(GeoPoint3 point, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return PlanarMap.ToEdge3(frame, Flat(frame).GetClosestEdge(PlanarMap.ProjectToPoint2(frame, point), tolerance));
        }

        /// <summary>
        /// Gets the edge of this loop that comes nearest a segment in the same plane.
        /// </summary>
        public GeoEdge3 GetClosestEdge(GeoLine3 line) => GetClosestEdge(line, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this loop that comes nearest a segment in the same plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the segment does not lie in the loop's plane.</exception>
        public GeoEdge3 GetClosestEdge(GeoLine3 line, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = FrameFor(new[] { line.StartPoint, line.EndPoint }, tolerance, nameof(line));

            return PlanarMap.ToEdge3(frame, Flat(frame).GetClosestEdge(PlanarMap.ProjectToLine2(frame, line), tolerance));
        }

        /// <summary>
        /// Gets the edge of this loop that comes nearest an arc in the same plane.
        /// </summary>
        public GeoEdge3 GetClosestEdge(GeoArc3 arc) => GetClosestEdge(arc, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this loop that comes nearest an arc in the same plane, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the arc does not lie in the loop's plane.</exception>
        public GeoEdge3 GetClosestEdge(GeoArc3 arc, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return PlanarMap.ToEdge3(frame, Flat(frame).GetClosestEdge(Flatten(frame, arc, tolerance, nameof(arc)), tolerance));
        }

        /// <summary>
        /// Gets the edge of this loop that comes nearest a circle in the same plane.
        /// </summary>
        public GeoEdge3 GetClosestEdge(GeoCircle3 circle) => GetClosestEdge(circle, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this loop that comes nearest a circle in the same plane, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the circle does not lie in the loop's plane.</exception>
        public GeoEdge3 GetClosestEdge(GeoCircle3 circle, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return PlanarMap.ToEdge3(frame, Flat(frame).GetClosestEdge(Flatten(frame, circle, tolerance, nameof(circle)), tolerance));
        }

        /// <summary>
        /// Gets the edge of this loop that comes nearest an edge in the same plane.
        /// </summary>
        public GeoEdge3 GetClosestEdge(GeoEdge3 edge) => GetClosestEdge(edge, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this loop that comes nearest an edge in the same plane, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge; it has to lie in the loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentException">Thrown when the edge does not lie in the loop's plane.</exception>
        public GeoEdge3 GetClosestEdge(GeoEdge3 edge, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return PlanarMap.ToEdge3(frame, Flat(frame).GetClosestEdge(Flatten(frame, edge, tolerance, nameof(edge)), tolerance));
        }

        #endregion

        #region Laying the probe out in the loop's plane

        /// <summary>
        /// This loop laid out in its own frame.
        /// </summary>
        private GeoPolygonArc2 Flat(GeoCoordinateSystem3 frame) => ProjectToPolygonArc2(frame);

        /// <summary>
        /// The loop's frame, after checking that every point given lies in its plane.
        /// </summary>
        private GeoCoordinateSystem3 FrameFor(IEnumerable<GeoPoint3> points, Tolerance tolerance, string name)
        {
            GeoPlane3 plane = GetPlane();

            foreach (GeoPoint3 point in points)
            {
                if (!Containment3.IsPointOn(plane, point, tolerance))
                {
                    throw new ArgumentException(OutOfPlane(name), name);
                }
            }

            return GetFrame();
        }

        /// <summary>
        /// Lays an arc out in the loop's frame, refusing it where it lies anywhere else.
        /// </summary>
        private GeoArc2 Flatten(GeoCoordinateSystem3 frame, GeoArc3 arc, Tolerance tolerance, string name)
        {
            if (!PlanarMap.TryToArc2(frame, arc, out GeoArc2 flat, tolerance))
            {
                throw new ArgumentException(OutOfPlane(name), name);
            }

            return flat;
        }

        /// <summary>
        /// Lays a circle out in the loop's frame, refusing it where it lies anywhere else.
        /// </summary>
        /// <remarks>
        /// A circle is an arc of a whole turn, so it goes the same way as one; what comes back is a circle again
        /// rather than an arc, because that is what the plane library wants to be handed.
        /// </remarks>
        private GeoCircle2 Flatten(GeoCoordinateSystem3 frame, GeoCircle3 circle, Tolerance tolerance, string name)
        {
            if (!SharesPlaneWith(new GeoPlane3(circle.Center, circle.Normal), tolerance))
            {
                throw new ArgumentException(OutOfPlane(name), name);
            }

            return new GeoCircle2(PlanarMap.ProjectToPoint2(frame, circle.Center), circle.Radius);
        }

        /// <summary>
        /// Lays an edge out in the loop's frame, refusing it where it lies anywhere else.
        /// </summary>
        private GeoEdge2 Flatten(GeoCoordinateSystem3 frame, GeoEdge3 edge, Tolerance tolerance, string name)
        {
            if (!PlanarMap.TryToEdge2(frame, edge, out GeoEdge2 flat, tolerance))
            {
                throw new ArgumentException(OutOfPlane(name), name);
            }

            return flat;
        }

        /// <summary>
        /// Lifts a joining line the plane gave back into the loop's plane in space.
        /// </summary>
        private static GeoLine3 Lift(GeoCoordinateSystem3 frame, GeoLine2 line) => PlanarMap.ToLine3(frame, line);

        private static string OutOfPlane(string what)
            => "The " + what + " does not lie in the loop's plane, so there is no plane to work in. Bring them into one plane first.";

        #endregion
    }
}
