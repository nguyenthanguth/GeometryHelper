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
        /// <summary>
        /// Gets every point where an arc crosses this ray, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where an arc crosses this ray, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where an arc crosses this ray, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where an arc crosses this ray, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses this ray, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(circle, this);

        /// <summary>
        /// Gets every point where a circle crosses this ray, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses this ray, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses this ray, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Gets where this ray crosses a segment, as a list.
        /// </summary>
        /// <remarks>
        /// Two straight pieces meet at one place, so the list is one long or empty. It is a list because a
        /// <see cref="GeoEdge3"/> has to be able to ask the same question of a segment and of a bend.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Intersection3.GetIntersections(this, line);

        /// <summary>
        /// Gets where this ray crosses a segment, as a list, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a segment.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a segment, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses an edge: the same points, in the same order, as
        /// <see cref="GeoEdge3.GetIntersections(GeoRay3)"/>.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => edge.GetIntersections(this);

        /// <summary>
        /// Gets every point where this ray crosses an edge, within a tolerance: the same points, in the same order, as
        /// <see cref="GeoEdge3.GetIntersections(GeoRay3, Tolerance)"/>.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.GetIntersections(this, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses a curved loop: the same points, in the same order, as
        /// <see cref="GeoPolygonArc3.GetIntersections(GeoRay3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where this ray crosses a curved loop, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolygonArc3.GetIntersections(GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance)
            => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses a straight chain: the same points, in the same order, as
        /// <see cref="GeoPolyline3.GetIntersections(GeoRay3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => Intersection3.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this ray crosses a straight chain, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolyline3.GetIntersections(GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
            => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses a curved chain: the same points, in the same order, as
        /// <see cref="GeoPolylineArc3.GetIntersections(GeoRay3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where this ray crosses a curved chain, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolylineArc3.GetIntersections(GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance)
            => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses an edge: the same answer, and the same points in the same order, as
        /// <see cref="GeoEdge3.TryIntersectWith(GeoRay3, out GeoPoint3[])"/>.
        /// </summary>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections)
            => edge.TryIntersectWith(this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses an edge, within a tolerance: the same answer, and the same points in
        /// the same order, as <see cref="GeoEdge3.TryIntersectWith(GeoRay3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
            => edge.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a curved loop: the same answer, and the same points in the same order,
        /// as <see cref="GeoPolygonArc3.TryIntersectWith(GeoRay3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections)
            => ArcChain3.TryIntersectWith(loop, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a curved loop, within a tolerance: the same answer, and the same points
        /// in the same order, as <see cref="GeoPolygonArc3.TryIntersectWith(GeoRay3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections, Tolerance tolerance)
            => ArcChain3.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a straight chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolyline3.TryIntersectWith(GeoRay3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a straight chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolyline3.TryIntersectWith(GeoRay3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a curved chain: the same answer, and the same points in the same order,
        /// as <see cref="GeoPolylineArc3.TryIntersectWith(GeoRay3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections)
            => ArcChain3.TryIntersectWith(chain, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a curved chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolylineArc3.TryIntersectWith(GeoRay3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections, Tolerance tolerance)
            => ArcChain3.TryIntersectWith(chain, this, out intersections, tolerance);
    }
}
