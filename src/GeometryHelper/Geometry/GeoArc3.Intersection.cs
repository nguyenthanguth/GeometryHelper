using System;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an arc crosses another shape.
    /// </summary>
    public readonly partial struct GeoArc3
    {
        /// <summary>
        /// Gets every point where this arc crosses a plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Arc3.GetIntersections(this, plane);

        /// <summary>
        /// Gets every point where this arc crosses a plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Arc3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a plane, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, plane, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, plane, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a segment, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Arc3.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where this arc crosses a segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Arc3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a ray, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Arc3.GetIntersections(this, ray);

        /// <summary>
        /// Gets every point where this arc crosses a ray, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Arc3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, ray, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses an arc, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 other) => Arc3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this arc crosses an arc, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 other, Tolerance tolerance) => Arc3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="other">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 other, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="other">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 other, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a circle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where this arc crosses a circle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, circle, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, circle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Arc3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets every point where this arc crosses a triangle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Arc3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, triangle, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, triangle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a polygon, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Arc3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where this arc crosses a polygon, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Arc3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a face, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Arc3.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where this arc crosses a face, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Arc3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, face, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, face, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Arc3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this arc crosses the surface of a solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Arc3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, solid, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, solid, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Arc3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this arc crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Arc3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Arc3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this arc crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Arc3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, box, out intersections, tolerance);

    }
}
