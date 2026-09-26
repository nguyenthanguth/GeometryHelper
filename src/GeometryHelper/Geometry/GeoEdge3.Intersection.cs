using System;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an edge crosses another shape.
    /// </summary>
    public readonly partial struct GeoEdge3
    {
        /// <summary>
        /// Gets every point where this edge crosses an axis-aligned box.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => GetIntersections(box, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(box, tolerance) : ToLine().GetIntersections(box, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses an arc.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(arc, tolerance) : ToLine().GetIntersections(arc, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a circle.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(circle, tolerance) : ToLine().GetIntersections(circle, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a face.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(face, tolerance) : ToLine().GetIntersections(face, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses an oriented box.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => GetIntersections(box, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(box, tolerance) : ToLine().GetIntersections(box, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a plane.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => GetIntersections(plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(plane, tolerance) : ToLine().GetIntersections(plane, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a polygon.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => GetIntersections(polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(polygon, tolerance) : ToLine().GetIntersections(polygon, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => GetIntersections(solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(solid, tolerance) : ToLine().GetIntersections(solid, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a triangle.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(triangle, tolerance) : ToLine().GetIntersections(triangle, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses an arc.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(arc, out intersections, tolerance) : ToLine().TryIntersectWith(arc, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses a circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(circle, out intersections, tolerance) : ToLine().TryIntersectWith(circle, out intersections, tolerance);

    }
}
