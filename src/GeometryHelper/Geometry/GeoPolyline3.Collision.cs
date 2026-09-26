using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a polyline touches another shape.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Determines whether this chain touches or overlaps a solid.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Determines whether this chain touches or overlaps a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Determines whether this chain touches a curved chain.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc3 other) => Core.ArcChain3.CollidesWith(other, this);

        /// <summary>
        /// Determines whether this chain touches a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc3 other, Tolerance tolerance) => Core.ArcChain3.CollidesWith(other, this, tolerance);

        /// <summary>
        /// Determines whether this chain touches a curved loop.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc3 other) => Core.ArcChain3.CollidesWith(other, this);

        /// <summary>
        /// Determines whether this chain touches a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc3 other, Tolerance tolerance) => Core.ArcChain3.CollidesWith(other, this, tolerance);
        /// <summary>
        /// Determines whether this chain touches a plane.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(this, plane);

        /// <summary>
        /// Determines whether this chain touches a plane, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Determines whether this chain touches a segment.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(this, line);

        /// <summary>
        /// Determines whether this chain touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Determines whether this chain touches a ray.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(this, ray);

        /// <summary>
        /// Determines whether this chain touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Determines whether this chain touches an arc.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Collision3.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether this chain touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Collision3.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether this chain touches a circle.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Collision3.CollidesWith(this, circle);

        /// <summary>
        /// Determines whether this chain touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Collision3.CollidesWith(this, circle, tolerance);

        /// <summary>
        /// Determines whether this chain touches a triangle.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(this, triangle);

        /// <summary>
        /// Determines whether this chain touches a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Determines whether this chain touches a polygon.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(this, polygon);

        /// <summary>
        /// Determines whether this chain touches a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Determines whether this chain touches a face.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(this, face);

        /// <summary>
        /// Determines whether this chain touches a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Determines whether this chain touches a box.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Determines whether this chain touches a box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Determines whether this chain touches a square box.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Determines whether this chain touches a square box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Determines whether this chain touches another straight chain.
        /// </summary>
        public bool CollidesWith(GeoPolyline3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Determines whether this chain touches another straight chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

    }
}
