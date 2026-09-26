using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a curved loop crosses another shape.
    /// </summary>
    public sealed partial class GeoPolygonArc3
    {
        /// <summary>
        /// Gets every point where this curved loop crosses a plane.
        /// </summary>
        /// <param name="plane">The plane.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => ArcChain3.GetIntersections(this, plane);

        /// <summary>
        /// Gets every point where this curved loop crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => ArcChain3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses a triangle.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => ArcChain3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets every point where this curved loop crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => ArcChain3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses a polygon.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => ArcChain3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where this curved loop crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => ArcChain3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses a face.
        /// </summary>
        /// <param name="face">The face.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => ArcChain3.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where this curved loop crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => ArcChain3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses an axis-aligned box.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => ArcChain3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this curved loop crosses an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => ArcChain3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses an oriented box.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => ArcChain3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this curved loop crosses an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => ArcChain3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses a solid.
        /// </summary>
        /// <param name="solid">The solid.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => ArcChain3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this curved loop crosses a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => ArcChain3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses an arc.
        /// </summary>
        /// <param name="arc">The arc.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => ArcChain3.GetIntersections(this, arc);

        /// <summary>
        /// Gets every point where this curved loop crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => ArcChain3.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets every point where this curved loop crosses a circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => ArcChain3.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where this curved loop crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => ArcChain3.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets every point where this loop crosses a segment.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Core.ArcChain3.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where this loop crosses a segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Tries to find where this loop crosses a segment.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where this loop crosses a segment, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this loop crosses a ray.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Core.ArcChain3.GetIntersections(this, ray);

        /// <summary>
        /// Gets every point where this loop crosses a ray, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Tries to find where this loop crosses a ray.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(this, ray, out intersections);

        /// <summary>
        /// Tries to find where this loop crosses a ray, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this loop crosses a curved chain.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 other) => Core.ArcChain3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this loop crosses a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this loop crosses a curved chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this loop crosses a curved chain, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this loop crosses a curved loop.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 other) => Core.ArcChain3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this loop crosses a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this loop crosses a curved loop.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this loop crosses a curved loop, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this loop crosses a straight chain.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolyline3 other) => Core.ArcChain3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this loop crosses a straight chain, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolyline3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this loop crosses a straight chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolyline3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this loop crosses a straight chain, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolyline3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(this, other, out intersections, tolerance);

    }
}