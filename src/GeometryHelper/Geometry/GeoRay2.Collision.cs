using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a ray touches another shape.
    /// </summary>
    /// <remarks>
    /// A closed shape is read as filled: a ray reaching its material collides with it, and so does one starting inside
    /// it. The ray from (50, 50) along X starts inside the square 100 by 100 with a corner at the origin and collides
    /// with it; the ray from (-50, 150) along X passes above it and does not.
    /// </remarks>
    public readonly partial struct GeoRay2
    {
        /// <summary>
        /// Determines whether this ray touches or overlaps an arc.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Collision2.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether this ray touches or overlaps an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Collision2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether this ray touches or overlaps a curved loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop) => Collision2.CollidesWith(loop, this);

        /// <summary>
        /// Determines whether this ray touches or overlaps a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Collision2.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Determines whether this ray touches or overlaps a curved chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain) => Collision2.CollidesWith(chain, this);

        /// <summary>
        /// Determines whether this ray touches or overlaps a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Collision2.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a line segment using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Collision2.CollidesWith(this, line);

        /// <summary>
        /// Checks whether this ray touches a line segment within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying along the ray touches it along a length and crosses it nowhere, and still collides with it.
        /// </remarks>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Collision2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this ray touches another ray using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay2 other) => Collision2.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this ray touches another ray within a tolerance.
        /// </summary>
        /// <remarks>
        /// The ray from the origin along X and the one from (50, -50) along Y cross at (50, 0); two rays along the X axis
        /// from 0 and from 100, facing each other, touch along the stretch between them.
        /// </remarks>
        public bool CollidesWith(GeoRay2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside a rectangle or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Collision2.CollidesWith(rect, this);

        /// <summary>
        /// Checks whether this ray starts inside a rectangle or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(rect, this, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside a polygon or runs into it, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 poly) => Collision2.CollidesWith(poly, this);

        /// <summary>
        /// Checks whether this ray starts inside a polygon or runs into it, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(poly, this, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside a circle or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this ray starts inside a circle or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a polyline using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this ray touches a polyline within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this ray reaches the material of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face) => Face2.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this ray reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Over the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the ray from (50, 50)
        /// along X starts in the hole and runs into the material at (70, 50). A ray from (50, 50) reaches the material
        /// whichever way it points, as the hole is closed all round.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Face2.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this ray touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        #region Triangles

        /// <summary>
        /// Checks whether this ray starts inside a triangle or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle) => Triangle2.CollidesWith(triangle, this);

        /// <summary>
        /// Checks whether this ray starts inside a triangle or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.CollidesWith(triangle, this, tolerance);

        #endregion
    }
}
