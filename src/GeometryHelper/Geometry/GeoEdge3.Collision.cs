using System;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an edge touches another shape.
    /// </summary>
    public readonly partial struct GeoEdge3
    {
        /// <summary>
        /// Checks whether this edge touches an axis-aligned box.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(box, tolerance) : ToLine().CollidesWith(box, tolerance);

        /// <summary>
        /// Checks whether this edge touches an arc.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(arc, tolerance) : ToLine().CollidesWith(arc, tolerance);

        /// <summary>
        /// Checks whether this edge touches a circle.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(circle, tolerance) : ToLine().CollidesWith(circle, tolerance);

        /// <summary>
        /// Checks whether this edge touches a face.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(face, tolerance) : ToLine().CollidesWith(face, tolerance);

        /// <summary>
        /// Checks whether this edge touches an oriented box.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(box, tolerance) : ToLine().CollidesWith(box, tolerance);

        /// <summary>
        /// Checks whether this edge touches a polygon.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => CollidesWith(polygon, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(polygon, tolerance) : ToLine().CollidesWith(polygon, tolerance);

        /// <summary>
        /// Checks whether this edge touches a solid.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => CollidesWith(solid, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(solid, tolerance) : ToLine().CollidesWith(solid, tolerance);

        /// <summary>
        /// Checks whether this edge touches a triangle.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => CollidesWith(triangle, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(triangle, tolerance) : ToLine().CollidesWith(triangle, tolerance);

        /// <summary>
        /// Checks whether this edge touches a plane.
        /// </summary>
        /// <param name="plane">The plane.</param>
        public bool CollidesWith(GeoPlane3 plane) => CollidesWith(plane, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(plane, tolerance) : ToLine().CollidesWith(plane, tolerance);


        /// <summary>
        /// Determines whether this edge touches a segment.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Determines whether this edge touches a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(line, tolerance) : ToLine().CollidesWith(line, tolerance);

        /// <summary>
        /// Determines whether this edge touches a ray.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Determines whether this edge touches a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(ray, tolerance) : ToLine().CollidesWith(ray, tolerance);
        /// <summary>
        /// Determines whether this edge touches another edge.
        /// </summary>
        /// <remarks>
        /// Two edges lying along each other touch along a length and cross nowhere, so this is not the crossing
        /// test with the place thrown away.
        /// </remarks>
        public bool CollidesWith(GeoEdge3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this edge touches another edge, within a tolerance.
        /// </summary>
        /// <param name="other">The other edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 other, Tolerance tolerance)
            => other.IsArc ? CollidesWith(other.ToArc(), tolerance) : CollidesWith(other.ToLine(), tolerance);

    }
}
