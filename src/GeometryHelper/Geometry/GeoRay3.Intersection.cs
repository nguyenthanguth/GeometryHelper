using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a ray crosses another shape.
    /// </summary>
    public readonly partial struct GeoRay3
    {
        /// <summary>
        /// Tries to find the point where this ray crosses a plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, plane, out intersection);

        /// <summary>
        /// Tries to find the point where this ray crosses a plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, plane, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this ray crosses a triangle, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, triangle, out intersection);

        /// <summary>
        /// Tries to find the point where this ray crosses a triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, triangle, out intersection, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this ray crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this ray crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this ray crosses the surface of a solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find the point where this ray crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, face, out intersection);

        /// <summary>
        /// Tries to find the point where this ray crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, face, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this ray crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, polygon, out intersection);

        /// <summary>
        /// Tries to find the point where this ray crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, polygon, out intersection, tolerance);
    }
}
