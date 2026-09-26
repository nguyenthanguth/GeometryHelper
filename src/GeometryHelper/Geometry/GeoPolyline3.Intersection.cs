using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a polyline crosses another shape.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Gets every point where this chain crosses a curved chain.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 other) => Core.ArcChain3.GetIntersections(other, this);

        /// <summary>
        /// Gets every point where this chain crosses a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(other, this, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a curved chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a curved chain, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a curved loop.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 other) => Core.ArcChain3.GetIntersections(other, this);

        /// <summary>
        /// Gets every point where this chain crosses a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(other, this, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a curved loop.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a curved loop, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a plane.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Intersection3.GetIntersections(this, plane);

        /// <summary>
        /// Gets every point where this chain crosses a plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance)
            => Intersection3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a plane.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, plane, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, plane, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a segment.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Intersection3.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where this chain crosses a segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance)
            => Intersection3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a segment.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a segment, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a ray.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(this, ray);

        /// <summary>
        /// Gets every point where this chain crosses a ray, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance)
            => Intersection3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a ray.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, ray, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a ray, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses an arc.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Intersection3.GetIntersections(this, arc);

        /// <summary>
        /// Gets every point where this chain crosses an arc, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance)
            => Intersection3.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses an arc.
        /// </summary>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, arc, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses an arc, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, arc, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a circle.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Intersection3.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where this chain crosses a circle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance)
            => Intersection3.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a circle.
        /// </summary>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, circle, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a circle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, circle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a triangle.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Intersection3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets every point where this chain crosses a triangle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance)
            => Intersection3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a triangle.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, triangle, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, triangle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a polygon.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Intersection3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where this chain crosses a polygon, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance)
            => Intersection3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a polygon.
        /// </summary>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a polygon, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a face.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Intersection3.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where this chain crosses a face, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance)
            => Intersection3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a face.
        /// </summary>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, face, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a face, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, face, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a box.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this chain crosses a box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance)
            => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a box.
        /// </summary>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a box, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a square box.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this chain crosses a square box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance)
            => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a square box.
        /// </summary>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a square box, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a body.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this chain crosses a body, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance)
            => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a body.
        /// </summary>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, solid, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a body, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, solid, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses another straight chain.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolyline3 other) => Intersection3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this chain crosses another straight chain, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolyline3 other, Tolerance tolerance)
            => Intersection3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses another straight chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolyline3 other, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses another straight chain, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolyline3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, other, out intersections, tolerance);

    }
}