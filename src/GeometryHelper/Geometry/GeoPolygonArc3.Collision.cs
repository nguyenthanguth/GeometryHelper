using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a curved loop touches another shape.
    /// </summary>
    public sealed partial class GeoPolygonArc3
    {
        /// <summary>
        /// Checks whether this curved loop touches a plane.
        /// </summary>
        /// <param name="plane">The plane.</param>
        public bool CollidesWith(GeoPlane3 plane) => ArcChain3.CollidesWith(this, plane);

        /// <summary>
        /// Checks whether this curved loop touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => ArcChain3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches a triangle.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        public bool CollidesWith(GeoTriangle3 triangle) => ArcChain3.CollidesWith(this, triangle);

        /// <summary>
        /// Checks whether this curved loop touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => ArcChain3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches a polygon.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        public bool CollidesWith(GeoPolygon3 polygon) => ArcChain3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this curved loop touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => ArcChain3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches a face.
        /// </summary>
        /// <param name="face">The face.</param>
        public bool CollidesWith(GeoFace3 face) => ArcChain3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this curved loop touches a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => ArcChain3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches an axis-aligned box.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        public bool CollidesWith(GeoAabb3 box) => ArcChain3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this curved loop touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => ArcChain3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches an oriented box.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        public bool CollidesWith(GeoObb3 box) => ArcChain3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this curved loop touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => ArcChain3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches a solid.
        /// </summary>
        /// <param name="solid">The solid.</param>
        public bool CollidesWith(GeoSolid3 solid) => ArcChain3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this curved loop touches a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => ArcChain3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches an arc.
        /// </summary>
        /// <param name="arc">The arc.</param>
        public bool CollidesWith(GeoArc3 arc) => ArcChain3.CollidesWith(this, arc);

        /// <summary>
        /// Checks whether this curved loop touches an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => ArcChain3.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Checks whether this curved loop touches a circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        public bool CollidesWith(GeoCircle3 circle) => ArcChain3.CollidesWith(this, circle);

        /// <summary>
        /// Checks whether this curved loop touches a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => ArcChain3.CollidesWith(this, circle, tolerance);

    }
}
