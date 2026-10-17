using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a ray is from another shape.
    /// </summary>
    /// <remarks>
    /// A closed shape is read as filled, as <see cref="GeometryHelper.Core.Distance2"/> reads it: a ray starting inside a
    /// polygon is nought from it. The ray from (-50, 150) along X passes 50 above the square 100 by 100 with a corner at
    /// the origin; turned round to run along -X from (150, 150), it is 50 from it too.
    /// </remarks>
    public readonly partial struct GeoRay2
    {
        /// <summary>
        /// Calculates the distance from a point to the closest point on this ray, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from a point to the closest point on this ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The point is brought onto the ray and stopped at the origin: from the ray leaving the origin along X, the point
        /// (50, 40) is 40 away and the point (-30, 40) is 50 away. The tolerance is taken so that this reads like every
        /// other shape; it changes nothing here.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Arc2.DistanceTo(arc, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Arc2.DistanceTo(arc, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a curved loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a curved chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a line segment using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from this ray to a line segment within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Distance2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to another ray using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoRay2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this ray to another ray within a tolerance.
        /// </summary>
        /// <remarks>
        /// Nought where the two cross; otherwise the nearer of the two origins to the other ray. The ray from the origin
        /// along X and the one from (50, 30) along Y are 30 apart, the second origin standing over the first ray.
        /// </remarks>
        public double DistanceTo(GeoRay2 other, Tolerance tolerance) => Distance2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to a rectangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.DistanceTo(rect, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 poly, Tolerance tolerance) => Distance2.DistanceTo(poly, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Distance2.DistanceTo(circle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this ray to a polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Distance2.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this ray to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this ray to an edge, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => edge.DistanceTo(this, tolerance);

        #region Triangles

        /// <summary>
        /// Gets the distance from this ray to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle) => Triangle2.DistanceTo(triangle, this);

        /// <summary>
        /// Gets the distance from this ray to a triangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.DistanceTo(triangle, this, tolerance);

        #endregion

        /// <summary>
        /// Calculates the shortest distance from this ray to the material of a face: the same answer as
        /// <see cref="GeoFace2.DistanceTo(GeoRay2)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => Face2.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance from this ray to the material of a face, within a tolerance: the same answer
        /// as <see cref="GeoFace2.DistanceTo(GeoRay2, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this ray is nought from it where the two
        /// meet. The ray from (-50, 150) along X passes 50 above the square 100 by 100 with a corner at the origin and
        /// a hole 40 by 40 in its middle; the one from (-50, 50) along X runs into the material and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Face2.DistanceTo(face, this, tolerance);
    }
}
