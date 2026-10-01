using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public readonly partial struct GeoTriangle2
    {
        /// <summary>
        /// Checks whether this triangle touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Triangle2.CollidesWith(this, arc);

        /// <summary>
        /// Checks whether this triangle touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Triangle2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a curved loop, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => Triangle2.CollidesWith(this, loop);

        /// <summary>
        /// Checks whether this triangle touches a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Triangle2.CollidesWith(this, loop, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a curved chain, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain) => Triangle2.CollidesWith(this, chain);

        /// <summary>
        /// Checks whether this triangle touches a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Triangle2.CollidesWith(this, chain, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a rectangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Triangle2.CollidesWith(this, rect);

        /// <summary>
        /// Checks whether this triangle touches a rectangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Triangle2.CollidesWith(this, rect, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Triangle2.CollidesWith(this, line);

        /// <summary>
        /// Checks whether this triangle touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Triangle2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 polygon) => Triangle2.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this triangle touches a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 polygon, Tolerance tolerance) => Triangle2.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Triangle2.CollidesWith(this, circle);

        /// <summary>
        /// Checks whether this triangle touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Triangle2.CollidesWith(this, circle, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a polyline, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Triangle2.CollidesWith(this, polyline);

        /// <summary>
        /// Checks whether this triangle touches a polyline, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Triangle2.CollidesWith(this, polyline, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face) => Triangle2.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this triangle touches a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Triangle2.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this triangle touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => Triangle2.CollidesWith(this, edge);

        /// <summary>
        /// Checks whether this triangle touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance) => Triangle2.CollidesWith(this, edge, tolerance);

        /// <summary>
        /// Checks whether this triangle touches another triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 other) => Triangle2.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this triangle touches another triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 other, Tolerance tolerance) => Triangle2.CollidesWith(this, other, tolerance);
    }
}
