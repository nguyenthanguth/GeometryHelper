using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a circle nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoCircle2
    {
        /// <summary>
        /// Gets the shortest segment joining this circle to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Projection2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment joining this circle to an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this circle to a curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Projection2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment joining this circle to a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this circle to a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment joining this circle to a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the closest point on the circumference of this circle to a target point, including for
        /// points inside the circle.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToCircle(this, point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a line segment using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Projection2.GetShortestLineTo(this, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on another circle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 other) => Projection2.GetShortestLineTo(this, other, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on another circle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a rectangle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Projection2.GetShortestLineTo(this, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a rectangle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a polyline using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(this, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a polyline within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a polygon using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly) => Projection2.GetShortestLineTo(this, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a polygon within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetShortestLineTo(this, poly, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this circle and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this circle and landing on the boundary of a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the edge of a polygon nearest this circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPolygon2 poly) => ClosestEdge2.GetClosestEdge(poly, this);

        /// <summary>
        /// Gets the edge of a polyline nearest this circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPolyline2 polyline) => ClosestEdge2.GetClosestEdge(polyline, this);

        /// <summary>
        /// Gets the edge of a rectangle nearest this circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoRectangle2 rect) => ClosestEdge2.GetClosestEdge(rect, this);

        /// <summary>
        /// Gets the edge of a curved loop nearest this circle, using the default tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop) => ClosestEdge2.GetClosestEdge(loop, this);

        /// <summary>
        /// Gets the edge of a curved loop nearest this circle, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(loop, this, tolerance);

        /// <summary>
        /// Gets the edge of a curved chain nearest this circle, using the default tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain) => ClosestEdge2.GetClosestEdge(chain, this);

        /// <summary>
        /// Gets the edge of a curved chain nearest this circle, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(chain, this, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this circle and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this circle and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);
    }
}
