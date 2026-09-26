using System;
using System.Linq;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a rectangle crosses another shape.
    /// </summary>
    public readonly partial struct GeoRectangle2
    {
        /// <summary>
        /// Gets the points where this rectangle meets an arc.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Intersection2.GetIntersections(this, arc);

        /// <summary>
        /// Gets the points where this rectangle meets an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Intersection2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets the points where this rectangle meets a curved loop.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Intersection2.GetIntersections(this, loop);

        /// <summary>
        /// Gets the points where this rectangle meets a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(this, loop, tolerance);

        /// <summary>
        /// Gets the points where this rectangle meets a curved chain.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Intersection2.GetIntersections(this, chain);

        /// <summary>
        /// Gets the points where this rectangle meets a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Intersection2.GetIntersections(this, chain, tolerance);

        /// <summary>
        /// Gets every point where this rectangle crosses the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Face2.GetIntersections(face, this);

        /// <summary>
        /// Gets every point where this rectangle crosses the boundary of a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a line segment using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Intersection2.GetIntersections(this, line, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a line segment within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Intersection2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets all intersection points with another rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 other) => Intersection2.GetIntersections(this, other, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with another rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 other, Tolerance tolerance) => Intersection2.GetIntersections(this, other, tolerance);

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
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(this, circle, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a circle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Gets every point where this rectangle crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this rectangle crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);
        /// <summary>
        /// Tries to find where this rectangle meets an arc.
        /// </summary>
        /// <remarks>
        /// This is <see cref="GetIntersections(GeoArc2, Tolerance)"/> read a second way -- the list, plus
        /// whether it is empty -- so a caller who only wants to know <i>whether</i> need not measure the array.
        /// It is built here rather than forwarded to <c>Core</c> because there is no arithmetic in it: the
        /// crossing itself is worked out in one place and this only reports on it.
        /// </remarks>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections)
            => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a circle.
        /// </summary>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections)
            => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets an edge.
        /// </summary>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections)
            => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(edge, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a face.
        /// </summary>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections)
            => TryIntersectWith(face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a face, within a tolerance.
        /// </summary>
        /// <param name="face">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a segment.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections)
            => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a polygon.
        /// </summary>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections)
            => TryIntersectWith(polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a loop that may curve.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections)
            => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <param name="loop">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections)
            => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a chain, within a tolerance.
        /// </summary>
        /// <param name="polyline">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a chain that may curve.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections)
            => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <param name="chain">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this rectangle meets a rectangle.
        /// </summary>
        public bool TryIntersectWith(GeoRectangle2 other, out GeoPoint2[] intersections)
            => TryIntersectWith(other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this rectangle meets a rectangle, within a tolerance.
        /// </summary>
        /// <param name="other">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoRectangle2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(other, tolerance);

            return intersections.Length > 0;
        }

    }
}
