using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a point is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoPoint3
    {
        /// <summary>
        /// Calculates the Euclidean distance to another point.
        /// </summary>
        public double DistanceTo(GeoPoint3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Calculates the shortest distance to a ray.
        /// </summary>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the perpendicular distance to a plane.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(triangle, this);

        /// <summary>
        /// Calculates the shortest distance to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest distance to a polygon, read as a filled surface.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(polygon, this);

        /// <summary>
        /// Calculates the shortest distance to a face, holes respected.
        /// </summary>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance to a face, holes respected, within a tolerance: the same answer as
        /// <see cref="GeoFace3.DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(face, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance to a circular disc, read as a filled surface.
        /// </summary>
        public double DistanceTo(GeoCircle3 circle) => Distance3.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest distance to an oriented box. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(box, this);

        /// <summary>
        /// Calculates the shortest distance to an axis-aligned box. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(box, this);

        /// <summary>
        /// Calculates the shortest distance to a solid. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Calculates the shortest distance to a solid, within a tolerance: the same answer as
        /// <see cref="GeoSolid3.DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the surface of a axis-aligned box, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoAabb3 box) => Distance3.SignedDistanceTo(box, this);

        /// <summary>
        /// Gets the signed distance from this point to the surface of a axis-aligned box, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.SignedDistanceTo(box, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the surface of a oriented box, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoObb3 box) => Distance3.SignedDistanceTo(box, this);

        /// <summary>
        /// Gets the signed distance from this point to the surface of a oriented box, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.SignedDistanceTo(box, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the surface of a solid, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoSolid3 solid) => Distance3.SignedDistanceTo(solid, this);

        /// <summary>
        /// Gets the signed distance from this point to the surface of a solid, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.SignedDistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to an arc: the same answer as <see cref="GeoArc3.DistanceTo(GeoPoint3)"/>.
        /// </summary>
        public double DistanceTo(GeoArc3 arc) => arc.DistanceTo(this);

        /// <summary>
        /// Gets the distance from this point to an arc, within a tolerance: the same answer as
        /// <see cref="GeoArc3.DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoArc3 arc, Tolerance tolerance) => arc.DistanceTo(this, tolerance);

        /// <summary>
        /// Gets the distance from this point to an edge: the same answer as
        /// <see cref="GeoEdge3.DistanceTo(GeoPoint3)"/>.
        /// </summary>
        public double DistanceTo(GeoEdge3 edge) => edge.DistanceTo(this);

        /// <summary>
        /// Gets the distance from this point to an edge, within a tolerance: the same answer as
        /// <see cref="GeoEdge3.DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoEdge3 edge, Tolerance tolerance) => edge.DistanceTo(this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a curved loop: the same answer as
        /// <see cref="GeoPolygonArc3.DistanceTo(GeoPoint3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).DistanceTo(this);

        /// <summary>
        /// Gets the distance from this point to a curved loop, within a tolerance: the same answer as
        /// <see cref="GeoPolygonArc3.DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).DistanceTo(this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a curved chain: the same answer as
        /// <see cref="GeoPolylineArc3.DistanceTo(GeoPoint3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc3 chain)
            => (chain ?? throw new ArgumentNullException(nameof(chain))).DistanceTo(this);

        /// <summary>
        /// Gets the distance from this point to a curved chain, within a tolerance: the same answer as
        /// <see cref="GeoPolylineArc3.DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc3 chain, Tolerance tolerance)
            => (chain ?? throw new ArgumentNullException(nameof(chain))).DistanceTo(this, tolerance);
    }
}
