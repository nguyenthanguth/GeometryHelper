using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a polyline is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Calculates the shortest distance from this polyline to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this chain to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from this chain to a line segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to another chain.
        /// </summary>
        public double DistanceTo(GeoPolyline3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this chain to another chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance from this chain to a triangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance from this chain to a polygon, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a plane.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(this, plane);

        /// <summary>
        /// Calculates the shortest distance from this chain to a plane, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(this, plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to an oriented box.
        /// </summary>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance from this chain to an oriented box, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to an axis-aligned box.
        /// </summary>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance from this chain to an axis-aligned box, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a solid.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Calculates the shortest distance from this chain to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);
    }
}
