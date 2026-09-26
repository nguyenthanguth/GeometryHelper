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

        /// <summary>
        /// Gets every point where this edge crosses a segment.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(line, tolerance) : ToLine().GetIntersections(line, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses a segment.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            // A straight leg against a segment is not asked of GeoLine3 in this shape: giving it an array-form
            // TryIntersectWith would make every existing call to its single-point one ambiguous. The list comes
            // from GetIntersections instead, which is the same answer.
            intersections = GetIntersections(line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where this edge crosses a ray.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(ray, tolerance) : ToLine().GetIntersections(ray, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses a ray.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(ray, out intersections, tolerance) : ToLine().TryIntersectWith(ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses another edge.
        /// </summary>
        /// <remarks>
        /// Each side is read as whichever of the two it is, so this is a bend against a bend, a bend against a
        /// straight leg, or two straight legs, and every one of those has a closed form. Two edges lying along
        /// each other meet along a length and name no place.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoEdge3 other) => GetIntersections(other, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses another edge, within a tolerance.
        /// </summary>
        /// <param name="other">The other edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 other, Tolerance tolerance)
            => other.IsArc ? GetIntersections(other.ToArc(), tolerance) : GetIntersections(other.ToLine(), tolerance);

        /// <summary>
        /// Tries to find where this edge crosses another edge.
        /// </summary>
        /// <param name="other">The other edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses another edge, within a tolerance.
        /// </summary>
        /// <param name="other">The other edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(other, tolerance);

            return intersections.Length > 0;
        }

    }
}