using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a face touches another shape.
    /// </summary>
    public sealed partial class GeoFace2
    {
        /// <summary>
        /// Checks whether a segment reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Face2.CollidesWith(this, line);

        /// <summary>
        /// Checks whether a segment reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Face2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether a arc reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Face2.CollidesWith(this, arc);

        /// <summary>
        /// Checks whether a arc reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Face2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Checks whether a circle reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Face2.CollidesWith(this, circle);

        /// <summary>
        /// Checks whether a circle reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Face2.CollidesWith(this, circle, tolerance);

        /// <summary>
        /// Checks whether a rectangle reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Face2.CollidesWith(this, rect);

        /// <summary>
        /// Checks whether a rectangle reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Face2.CollidesWith(this, rect, tolerance);

        /// <summary>
        /// Checks whether a polyline reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Face2.CollidesWith(this, polyline);

        /// <summary>
        /// Checks whether a polyline reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Face2.CollidesWith(this, polyline, tolerance);

        /// <summary>
        /// Checks whether a polygon reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 polygon) => Face2.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether a polygon reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 polygon, Tolerance tolerance) => Face2.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether a curved loop reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => Face2.CollidesWith(this, loop);

        /// <summary>
        /// Checks whether a curved loop reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Face2.CollidesWith(this, loop, tolerance);

        /// <summary>
        /// Checks whether a curved chain reaches the material of this face.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain) => Face2.CollidesWith(this, chain);

        /// <summary>
        /// Checks whether a curved chain reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Face2.CollidesWith(this, chain, tolerance);

        /// <summary>
        /// Checks whether this face touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this face touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        #region Triangles

        /// <summary>
        /// Checks whether this face touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle) => Triangle2.CollidesWith(triangle, this);

        /// <summary>
        /// Checks whether this face touches a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.CollidesWith(triangle, this, tolerance);

        #endregion

        /// <summary>
        /// Checks whether the material of this face meets the material of another.
        /// </summary>
        /// <remarks>
        /// The materials have to meet: a face lying wholly in a hole of this one does not collide with it, and one
        /// lying on its material does. A square 20 by 20 about (50, 50) does not collide with the square 100 by 100
        /// with a corner at the origin and a hole 40 by 40 in its middle; about (15, 50) it does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other face is null.</exception>
        public bool CollidesWith(GeoFace2 other) => Face2.CollidesWith(this, other);

        /// <summary>
        /// Checks whether the material of this face meets the material of another, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The materials have to meet: a face lying wholly in a hole of this one does not collide with it, and one
        /// lying on its material does. A square 20 by 20 about (50, 50) does not collide with the square 100 by 100
        /// with a corner at the origin and a hole 40 by 40 in its middle; about (15, 50) it does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other face is null.</exception>
        public bool CollidesWith(GeoFace2 other, Tolerance tolerance) => Face2.CollidesWith(this, other, tolerance);

        #region Rays

        /// <summary>
        /// Determines whether a ray reaches the material of this face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay2 ray) => Face2.CollidesWith(this, ray);

        /// <summary>
        /// Determines whether a ray reaches the material of this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay2 ray, Tolerance tolerance) => Face2.CollidesWith(this, ray, tolerance);

        #endregion
    }
}
