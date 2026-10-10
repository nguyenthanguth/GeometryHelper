using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether the rim of an ellipse in space touches another shape.
    /// </summary>
    public readonly partial struct GeoEllipse3
    {
        /// <summary>
        /// Checks whether the rim of this ellipse touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// An ellipse lying in the plane touches it everywhere and crosses it nowhere, so this is true where
        /// GetIntersections is empty.
        /// </remarks>
        /// <param name="plane">The plane.</param>
        public bool CollidesWith(GeoPlane3 plane) => Ellipse3.CollidesWith(this, plane);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Ellipse3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a segment, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The rim is what counts, not the disc: the segment from (0, 0, -10) to (0, 0, 10) passes through the middle
        /// of the ellipse 300 by 100 lying in the plan about the origin and touches nothing.
        /// </remarks>
        /// <param name="line">The segment.</param>
        public bool CollidesWith(GeoLine3 line) => Ellipse3.CollidesWith(this, line);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Ellipse3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        public bool CollidesWith(GeoRay3 ray) => Ellipse3.CollidesWith(this, ray);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Ellipse3.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        public bool CollidesWith(GeoArc3 arc) => Ellipse3.CollidesWith(this, arc);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Ellipse3.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a circle, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The rims are what count, not the discs: the circle of radius 50 lying in the plan inside the ellipse 300 by
        /// 100 about the same centre touches nothing.
        /// </remarks>
        /// <param name="circle">The circle.</param>
        public bool CollidesWith(GeoCircle3 circle) => Ellipse3.CollidesWith(this, circle);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Ellipse3.CollidesWith(this, circle, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches another ellipse, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The rims are what count, not the discs: the ellipse 150 by 50 lying in the plan inside the ellipse 300 by
        /// 100 about the same centre touches nothing.
        /// </remarks>
        /// <param name="other">The other ellipse.</param>
        public bool CollidesWith(GeoEllipse3 other) => Ellipse3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether the rim of this ellipse touches another ellipse, within a tolerance.
        /// </summary>
        /// <param name="other">The other ellipse.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEllipse3 other, Tolerance tolerance) => Ellipse3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        public bool CollidesWith(GeoTriangle3 triangle) => Ellipse3.CollidesWith(this, triangle);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Ellipse3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A rim lying in the polygon's plane wholly within it crosses nothing and touches it: the square 800 by 800
        /// about the origin in the plan touches the ellipse 300 by 100 about the same centre.
        /// </remarks>
        /// <param name="polygon">The polygon.</param>
        public bool CollidesWith(GeoPolygon3 polygon) => Ellipse3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Ellipse3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        public bool CollidesWith(GeoFace3 face) => Ellipse3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Ellipse3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A rim lying inside the material crosses nothing and touches it: the ellipse 30 by 10 lying in the plan about
        /// the centre of a box 200 by 200 by 200 touches it. A rim inside an opening is not in the material.
        /// </remarks>
        /// <param name="solid">The solid.</param>
        public bool CollidesWith(GeoSolid3 solid) => Ellipse3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Ellipse3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A rim wholly inside the box crosses no face and touches it.
        /// </remarks>
        /// <param name="box">The oriented box.</param>
        public bool CollidesWith(GeoObb3 box) => Ellipse3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Ellipse3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        public bool CollidesWith(GeoAabb3 box) => Ellipse3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Ellipse3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        public bool CollidesWith(GeoEdge3 edge) => Ellipse3.CollidesWith(this, edge);

        /// <summary>
        /// Checks whether the rim of this ellipse touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance) => Ellipse3.CollidesWith(this, edge, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => Ellipse3.CollidesWith(this, chain);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => Ellipse3.CollidesWith(this, chain, tolerance);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => Ellipse3.CollidesWith(this, loop);

        /// <summary>
        /// Checks whether the rim of this ellipse touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => Ellipse3.CollidesWith(this, loop, tolerance);
    }
}
