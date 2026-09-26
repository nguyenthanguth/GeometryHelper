using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a plane crosses another shape.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Tries to find the line where this plane meets another plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 other, out GeoRay3 intersection) => Intersection3.TryIntersectWith(this, other, out intersection);

        /// <summary>
        /// Tries to find the line where this plane meets another plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 other, out GeoRay3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Gets every point where the edges of a solid cross this plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where the edges of a solid cross this plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find the point where a segment crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a segment crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);
    }
}
