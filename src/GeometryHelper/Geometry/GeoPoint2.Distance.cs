using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a point is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoPoint2
    {
        /// <summary>
        /// Calculates the Euclidean distance to another point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the Euclidean distance to a line segment.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the global point tolerance is read as its start point.
        /// </returns>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(line, this);

        /// <summary>
        /// Calculates the Euclidean distance to a circle boundary.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the Euclidean distance to a rectangle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the Euclidean distance to a polygon boundary.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Calculates the Euclidean distance to a polyline.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this point to a arc, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 arc) => Arc2.DistanceTo(arc, this);

        /// <summary>
        /// Gets the distance from this point to a arc, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Arc2.DistanceTo(arc, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a face, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoFace2 face) => Distance2.DistanceTo(face, this);

        /// <summary>
        /// Gets the distance from this point to a face, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Distance2.DistanceTo(face, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Gets the distance from this point to a curved loop, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Gets the distance from this point to a curved chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a circle, negative inside it, using the default tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the circumference, measured exactly, negative when the point lies inside the circle
        /// and nought when the global point tolerance calls it on the circumference.
        /// </returns>
        public double SignedDistanceTo(GeoCircle2 circle) => Distance2.SignedDistanceTo(circle, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a circle, negative inside it, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the circumference, measured exactly, negative when the point lies inside the circle
        /// and nought when the tolerance given calls it on the circumference.
        /// </returns>
        public double SignedDistanceTo(GeoCircle2 circle, Tolerance tolerance) => Distance2.SignedDistanceTo(circle, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a rectangle, negative inside it, using the default tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest side, measured exactly, negative when the point lies inside the rectangle
        /// and nought when the global point tolerance calls it on a side.
        /// </returns>
        public double SignedDistanceTo(GeoRectangle2 rect) => Distance2.SignedDistanceTo(rect, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a rectangle, negative inside it, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest side, measured exactly, negative when the point lies inside the rectangle
        /// and nought when the tolerance given calls it on a side.
        /// </returns>
        public double SignedDistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.SignedDistanceTo(rect, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a polygon, negative inside it, using the default tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, measured exactly, negative when the point lies inside the polygon
        /// and nought when the global point tolerance calls it on its outline.
        /// </returns>
        public double SignedDistanceTo(GeoPolygon2 poly) => Distance2.SignedDistanceTo(poly, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a polygon, negative inside it, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, measured exactly, negative when the point lies inside the polygon
        /// and nought when the tolerance given calls it on its outline.
        /// </returns>
        public double SignedDistanceTo(GeoPolygon2 poly, Tolerance tolerance) => Distance2.SignedDistanceTo(poly, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a curved loop, negative inside it, using the default tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, measured exactly on the arcs, negative when the point lies inside the
        /// loop and nought when the global point tolerance calls it on its outline.
        /// </returns>
        public double SignedDistanceTo(GeoPolygonArc2 loop) => Distance2.SignedDistanceTo(loop, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a curved loop, negative inside it, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, measured exactly on the arcs, negative when the point lies inside the
        /// loop and nought when the tolerance given calls it on its outline.
        /// </returns>
        public double SignedDistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.SignedDistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a face, negative inside it, using the default tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, its outline or the rim of a hole, measured exactly, negative when the
        /// point lies on the material and nought when the global point tolerance calls it on an edge.
        /// </returns>
        public double SignedDistanceTo(GeoFace2 face) => Distance2.SignedDistanceTo(face, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a face, negative inside it, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, its outline or the rim of a hole, measured exactly, negative when the
        /// point lies on the material and nought when the tolerance given calls it on an edge.
        /// </returns>
        public double SignedDistanceTo(GeoFace2 face, Tolerance tolerance) => Distance2.SignedDistanceTo(face, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to an edge.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the global point tolerance is read as its start point.
        /// </returns>
        public double DistanceTo(GeoEdge2 edge)
            => edge.IsArc
                ? DistanceTo(edge.ToArc())
                : DistanceTo(edge.ToLine());

        /// <summary>
        /// Gets the distance from this point to an edge, within a tolerance: the same answer as
        /// <see cref="GeoEdge2.DistanceTo(GeoPoint2, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the tolerance given is read as its start point.
        /// </returns>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => edge.DistanceTo(this, tolerance);

        #region Triangles

        /// <summary>
        /// Gets the distance from this point to a triangle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle2 triangle) => Triangle2.DistanceTo(triangle, this);

        /// <summary>
        /// Gets the distance from this point to a triangle, negative when the point lies within it.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest side, measured exactly, negative when the point lies inside the triangle
        /// and nought when the global point tolerance calls it on a side.
        /// </returns>
        public double SignedDistanceTo(GeoTriangle2 triangle) => Triangle2.SignedDistanceTo(triangle, this);

        /// <summary>
        /// Gets the distance from this point to a triangle, negative when the point lies within it, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest side, measured exactly, negative when the point lies inside the triangle
        /// and nought when the tolerance given calls it on a side.
        /// </returns>
        public double SignedDistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.SignedDistanceTo(triangle, this, tolerance);

        #endregion

        #region Rays

        /// <summary>
        /// Calculates the Euclidean distance to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray) => Distance2.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the Euclidean distance to a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The point is measured to its projection on the ray, stopped at the origin: the point (-30, 40) is 50 from
        /// the ray leaving the origin along X. The tolerance is taken so that this reads like every other shape; it changes
        /// nothing here.
        /// </remarks>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Distance2.DistanceTo(ray, this, tolerance);

        #endregion

        #region XLines

        /// <summary>
        /// Calculates the shortest distance from this point to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this point to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline, Tolerance tolerance) => Distance2.DistanceTo(xline, this, tolerance);

        #endregion
    }
}
