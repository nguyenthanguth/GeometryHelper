using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public readonly partial struct GeoTriangle2
    {
        /// <summary>
        /// Gets every point where the edges of this triangle meet an arc, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Triangle2.GetIntersections(this, arc);

        /// <summary>
        /// Gets every point where the edges of this triangle meet an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Triangle2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a curved loop, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Triangle2.GetIntersections(this, loop);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Triangle2.GetIntersections(this, loop, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a curved chain, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Triangle2.GetIntersections(this, chain);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Triangle2.GetIntersections(this, chain, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a rectangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Triangle2.GetIntersections(this, rect);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a rectangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Triangle2.GetIntersections(this, rect, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a segment, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Triangle2.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a segment, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Triangle2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a polygon, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon) => Triangle2.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a polygon, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon, Tolerance tolerance) => Triangle2.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a circle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Triangle2.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a circle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Triangle2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a polyline, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Triangle2.GetIntersections(this, polyline);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a polyline, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Triangle2.GetIntersections(this, polyline, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a face, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Triangle2.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where the edges of this triangle meet a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Triangle2.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet an edge, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => Triangle2.GetIntersections(this, edge);

        /// <summary>
        /// Gets every point where the edges of this triangle meet an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance) => Triangle2.GetIntersections(this, edge, tolerance);

        /// <summary>
        /// Gets every point where the edges of this triangle meet another triangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 other) => Triangle2.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where the edges of this triangle meet another triangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 other, Tolerance tolerance) => Triangle2.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where the edges of this triangle meet an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet an arc, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a curved loop, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a curved chain, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a rectangle, using the default tolerance.
        /// </summary>
        /// <param name="rect">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections) => TryIntersectWith(rect, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(rect, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where the edges of this triangle meet a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a segment, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections) => TryIntersectWith(polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a polygon, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a circle, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a polyline, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections) => TryIntersectWith(face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet a face, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet an edge, within a tolerance.
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
        /// Tries to find where the edges of this triangle meet another triangle, using the default tolerance.
        /// </summary>
        /// <param name="other">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoTriangle2 other, out GeoPoint2[] intersections) => TryIntersectWith(other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of this triangle meet another triangle, within a tolerance.
        /// </summary>
        /// <param name="other">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoTriangle2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(other, tolerance);

            return intersections.Length > 0;
        }
    }
}
