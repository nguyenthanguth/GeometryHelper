using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a curved loop is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolygonArc2
    {
        /// <summary>
        /// Gets the distance from the loop to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this loop to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the outline, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint2)"/>
        /// reads this loop as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this loop to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a straight segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance from the loop to a straight segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Distance2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Gets the distance from the loop to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Gets the distance from the loop to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a straight chain.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(this, polyline);

        /// <summary>
        /// Gets the distance from the loop to a straight chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Distance2.DistanceTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a straight loop.
        /// </summary>
        public double DistanceTo(GeoPolygon2 polygon) => Distance2.DistanceTo(this, polygon);

        /// <summary>
        /// Gets the distance from the loop to a straight loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygon2 polygon, Tolerance tolerance) => Distance2.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a chain that may curve.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(this, chain);

        /// <summary>
        /// Gets the distance from the loop to a chain that may curve, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(this, chain, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a loop that may curve.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from the loop to a loop that may curve, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 other, Tolerance tolerance) => Distance2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Gets the distance from the loop to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(this, circle);

        /// <summary>
        /// Gets the distance from the loop to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Distance2.DistanceTo(this, circle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this curved loop to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the shortest distance from this curved loop to a rectangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.DistanceTo(rect, this, tolerance);

        /// <summary>
        /// Gets the distance from this curved loop to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this curved loop to an edge, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? DistanceTo(edge.ToArc(), tolerance)
                : DistanceTo(edge.ToLine(), tolerance);

        #region Triangles

        /// <summary>
        /// Gets the distance from this curved loop to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle) => Triangle2.DistanceTo(triangle, this);

        /// <summary>
        /// Gets the distance from this curved loop to a triangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.DistanceTo(triangle, this, tolerance);

        #endregion

        /// <summary>
        /// Calculates the shortest distance from this loop to the material of a face: the same answer as
        /// <see cref="GeoFace2.DistanceTo(GeoPolygonArc2)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this loop is nought from it where the two
        /// collide, and as far as the nearest rim where it lies wholly in a hole. The loop with corners (40, 40), (60,
        /// 40), (60, 60), (40, 60) and straight edges lies in the hole of the square 100 by 100 with a corner at the
        /// origin and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => Face2.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance from this loop to the material of a face, within a tolerance: the same
        /// answer as <see cref="GeoFace2.DistanceTo(GeoPolygonArc2, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this loop is nought from it where the two
        /// collide, and as far as the nearest rim where it lies wholly in a hole. The loop with corners (40, 40), (60,
        /// 40), (60, 60), (40, 60) and straight edges lies in the hole of the square 100 by 100 with a corner at the
        /// origin and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Face2.DistanceTo(face, this, tolerance);

        #region Rays

        /// <summary>
        /// Calculates the shortest distance from this curved loop to a ray, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoRay2 ray) => Distance2.DistanceTo(this, ray);

        /// <summary>
        /// Calculates the shortest distance from this curved loop to a ray, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Distance2.DistanceTo(this, ray, tolerance);

        #endregion
    }
}
