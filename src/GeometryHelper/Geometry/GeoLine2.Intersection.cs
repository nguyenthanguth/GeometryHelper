using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a segment crosses another shape.
    /// </summary>
    public readonly partial struct GeoLine2
    {
        /// <summary>
        /// Gets the points where this segment meets an arc.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Intersection2.GetIntersections(this, arc);

        /// <summary>
        /// Gets the points where this segment meets an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Intersection2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets the points where this segment meets a curved loop.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Intersection2.GetIntersections(loop, this);

        /// <summary>
        /// Gets the points where this segment meets a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets the points where this segment meets a curved chain.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Intersection2.GetIntersections(chain, this);

        /// <summary>
        /// Gets the points where this segment meets a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Intersection2.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Face2.GetIntersections(face, this);

        /// <summary>
        /// Gets every point where this segment crosses the boundary of a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Tries to calculate the intersection with another line segment using default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 other, out GeoPoint2 intersection) => TryIntersectWith(other, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to calculate the intersection with another line segment within tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 other, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Gets the intersection point with another line segment using default tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public GeoPoint2? GetIntersection(GeoLine2 other) => Intersection2.GetIntersection(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the intersection point with another line segment within tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public GeoPoint2? GetIntersection(GeoLine2 other, Tolerance tolerance) => Intersection2.GetIntersection(this, other, tolerance);

        /// <summary>
        /// Tries to calculate the intersection with another line segment, reading this segment, the other or
        /// both as the infinite line carrying it, using default tolerance.
        /// </summary>
        /// <param name="other">The other line segment.</param>
        /// <param name="extension">Which segment may be reached past its endpoints: First is this one, Second the other.</param>
        /// <param name="intersection">The intersection point if successful.</param>
        public bool TryIntersectWith(GeoLine2 other, LineExtension extension, out GeoPoint2 intersection) => Intersection2.TryIntersectWith(this, other, extension, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to calculate the intersection with another line segment, reading this segment, the other or
        /// both as the infinite line carrying it, within tolerance.
        /// </summary>
        /// <param name="other">The other line segment.</param>
        /// <param name="extension">Which segment may be reached past its endpoints: First is this one, Second the other.</param>
        /// <param name="intersection">The intersection point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine2 other, LineExtension extension, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(this, other, extension, out intersection, tolerance);

        /// <summary>
        /// Gets the intersection point with another line segment, reading this segment, the other or both as
        /// the infinite line carrying it, using default tolerance. Returns null if they do not meet.
        /// </summary>
        public GeoPoint2? GetIntersection(GeoLine2 other, LineExtension extension) => Intersection2.GetIntersection(this, other, extension, Tolerance.Global);

        /// <summary>
        /// Gets the intersection point with another line segment, reading this segment, the other or both as
        /// the infinite line carrying it, within tolerance. Returns null if they do not meet.
        /// </summary>
        public GeoPoint2? GetIntersection(GeoLine2 other, LineExtension extension, Tolerance tolerance) => Intersection2.GetIntersection(this, other, extension, tolerance);

        /// <summary>
        /// Gets all intersection points with a rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(rect, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polygon using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => Intersection2.GetIntersections(poly, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polygon within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance) => Intersection2.GetIntersections(poly, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a circle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(circle, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a circle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => Arc2.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance) => Arc2.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(loop, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(chain, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(chain, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this segment crosses an edge, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The one direction here that is not simply the edge read as whichever shape it is: two straight
        /// pieces meet at a single point rather than at a list of them, so that answer is wrapped to match
        /// the curved case. <see cref="GeoEdge2.GetIntersections(GeoLine2, Tolerance)"/> does the same the
        /// other way round.
        /// </remarks>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
        {
            if (edge.IsArc)
            {
                return GetIntersections(edge.ToArc(), tolerance);
            }

            GeoPoint2? meeting = Intersection2.GetIntersection(this, edge.ToLine(), tolerance);

            return meeting.HasValue ? new[] { meeting.Value } : new GeoPoint2[0];
        }
    }
}
