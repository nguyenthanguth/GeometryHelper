using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a circle crosses another shape.
    /// </summary>
    public readonly partial struct GeoCircle3
    {
        /// <summary>
        /// Gets every point where this circle crosses a plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Arc3.GetIntersections(this, plane);

        /// <summary>
        /// Gets every point where this circle crosses a plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Arc3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a plane, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, plane, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, plane, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses a segment, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Arc3.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where this circle crosses a segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Arc3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses a ray, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Arc3.GetIntersections(this, ray);

        /// <summary>
        /// Gets every point where this circle crosses a ray, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Arc3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, ray, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses an arc, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where this circle crosses an arc, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses a circle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 other) => Arc3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this circle crosses a circle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 other, Tolerance tolerance) => Arc3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="other">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 other, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="other">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 other, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, other, out intersections, tolerance);

    }
}
