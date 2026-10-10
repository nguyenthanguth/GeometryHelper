using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where the rim of an ellipse in space crosses another shape.
    /// </summary>
    public readonly partial struct GeoEllipse3
    {
        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The ellipse 300 by 100 lying in the plan about the origin crosses the plane x = 150 at (150, -86.603, 0),
        /// then at (150, 86.603, 0). An ellipse lying in the plane crosses it nowhere, and touches it.
        /// </remarks>
        /// <param name="plane">The plane.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Ellipse3.GetIntersections(this, plane);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Ellipse3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a plane, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, plane, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, plane, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a segment, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The rim is what counts, not the disc: a segment through the middle of the ellipse that never reaches the rim
        /// crosses nothing.
        /// </remarks>
        /// <param name="line">The segment.</param>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Ellipse3.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Ellipse3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Ellipse3.GetIntersections(this, ray);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Ellipse3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, ray, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Ellipse3.GetIntersections(this, arc);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Ellipse3.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, arc, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, arc, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a circle, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The circle of radius 150 about the origin crosses the ellipse 300 by 100 lying in the plan about it at four
        /// points, the first at (118.585, 91.856, 0).
        /// </remarks>
        /// <param name="circle">The circle.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Ellipse3.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Ellipse3.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, circle, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, circle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses another ellipse, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The ellipse 300 by 100 lying in the plan about the origin and the same ellipse turned a quarter cross at
        /// (94.868, 94.868, 0) first, then at the three points mirrored from it.
        /// </remarks>
        /// <param name="other">The other ellipse.</param>
        public GeoPoint3[] GetIntersections(GeoEllipse3 other) => Ellipse3.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses another ellipse, within a tolerance.
        /// </summary>
        /// <param name="other">The other ellipse.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEllipse3 other, Tolerance tolerance) => Ellipse3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses another ellipse, using the default tolerance.
        /// </summary>
        /// <param name="other">The other ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse3 other, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses another ellipse, within a tolerance.
        /// </summary>
        /// <param name="other">The other ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse3 other, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Ellipse3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Ellipse3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, triangle, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, triangle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Ellipse3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Ellipse3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Ellipse3.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Ellipse3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, face, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, face, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The ellipse 300 by 100 lying in the plan about the centre of a box 200 by 300 by 200 leaves it at (±100,
        /// ±94.281, 0).
        /// </remarks>
        /// <param name="solid">The solid.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Ellipse3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Ellipse3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, solid, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, solid, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Ellipse3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Ellipse3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Ellipse3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Ellipse3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => Ellipse3.GetIntersections(this, edge);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance) => Ellipse3.GetIntersections(this, edge, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections) => Ellipse3.TryIntersectWith(this, edge, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, edge, out intersections, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => Ellipse3.GetIntersections(this, chain);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => Ellipse3.GetIntersections(this, chain, tolerance);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => Ellipse3.GetIntersections(this, loop);

        /// <summary>
        /// Gets every point where the rim of this ellipse crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => Ellipse3.GetIntersections(this, loop, tolerance);
    }
}
