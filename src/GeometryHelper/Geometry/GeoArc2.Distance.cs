using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an arc is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoArc2
    {
        /// <summary>
        /// Gets the distance from this arc to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint2 point) => Arc2.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this arc to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Arc2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance between this arc and a segment.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the global point tolerance is read as its start point, except where it crosses a side or an arc.
        /// </returns>
        public double DistanceTo(GeoLine2 line) => Arc2.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance between this arc and a segment, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the tolerance given is read as its start point, except where it crosses a side or an arc.
        /// </returns>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Arc2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Gets the distance between this arc and another.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 other) => Arc2.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance between this arc and another, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 other, Tolerance tolerance) => Arc2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a circle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle2 circle) => Arc2.DistanceTo(this, circle);

        /// <summary>
        /// Gets the distance from this arc to a circle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Arc2.DistanceTo(this, circle, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a polygon, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Gets the distance from this arc to a polygon, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygon2 poly, Tolerance tolerance) => Distance2.DistanceTo(poly, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Gets the distance from this arc to a curved loop, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this arc to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Distance2.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Gets the distance from this arc to a curved chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a rectangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Gets the distance from this arc to a rectangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.DistanceTo(rect, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to an edge.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the global point tolerance is read as its start point, except where it crosses a side or an arc.
        /// </returns>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this arc to an edge, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment or straight edge shorter
        /// than the tolerance given is read as its start point, except where it crosses a side or an arc.
        /// </returns>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => edge.DistanceTo(this, tolerance);

        #region Triangles

        /// <summary>
        /// Gets the distance from this arc to a triangle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle2 triangle) => Triangle2.DistanceTo(triangle, this);

        /// <summary>
        /// Gets the distance from this arc to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.DistanceTo(triangle, this, tolerance);

        #endregion

        /// <summary>
        /// Calculates the shortest distance from this arc to the material of a face: the same answer as
        /// <see cref="GeoFace2.DistanceTo(GeoArc2)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this arc is nought from it where the two
        /// meet, and as far as the nearest rim where it lies wholly in a hole. The upper half of the circle of
        /// radius 10 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40
        /// by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => Face2.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance from this arc to the material of a face, within a tolerance: the same
        /// answer as <see cref="GeoFace2.DistanceTo(GeoArc2, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this arc is nought from it where the two
        /// meet, and as far as the nearest rim where it lies wholly in a hole. The upper half of the circle of
        /// radius 10 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40
        /// by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Face2.DistanceTo(face, this, tolerance);

        #region Rays

        /// <summary>
        /// Gets the shortest distance from this arc to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray) => Arc2.DistanceTo(this, ray);

        /// <summary>
        /// Gets the shortest distance from this arc to a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Arc2.DistanceTo(this, ray, tolerance);

        #endregion

        #region XLines

        /// <summary>
        /// Calculates the shortest distance from this arc to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this arc to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline, Tolerance tolerance) => Arc2.DistanceTo(this, xline, tolerance);

        #endregion
    }
}
