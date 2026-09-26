using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a solid crosses another shape.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Gets the points where a line segment passes through the surface of this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Intersection3.GetIntersections(line, this);

        /// <summary>
        /// Gets the points where a line segment passes through the surface of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.GetIntersections(line, this, tolerance);

        /// <summary>
        /// Gets the points where a plane cuts the edges of this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Intersection3.GetIntersections(plane, this);

        /// <summary>
        /// Gets the points where a plane cuts the edges of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Intersection3.GetIntersections(plane, this, tolerance);

        /// <summary>
        /// Gets the points where a ray passes through the surface of this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets the points where a ray passes through the surface of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(ray, this, tolerance);
        /// <summary>
        /// Gets every point where an arc crosses the surface of this solid, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where an arc crosses the surface of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where an arc crosses the surface of this solid, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where an arc crosses the surface of this solid, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses the surface of this solid, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(circle, this);

        /// <summary>
        /// Gets every point where a circle crosses the surface of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses the surface of this solid, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses the surface of this solid, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where an edge crosses this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses this solid, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

    }
}
