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

        /// <summary>
        /// Gets every point where this circle crosses a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Arc3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets every point where this circle crosses a triangle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Arc3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, triangle, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, triangle, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses a polygon, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Arc3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where this circle crosses a polygon, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Arc3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses a face, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Arc3.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where this circle crosses a face, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Arc3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, face, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, face, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Arc3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this circle crosses the surface of a solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Arc3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, solid, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, solid, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Arc3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this circle crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Arc3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this circle crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Arc3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this circle crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Arc3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this circle crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this circle crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where an edge crosses this circle.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses this circle, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Tries to find where an edge crosses this circle.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an edge crosses this circle, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
            => edge.IsArc
                ? TryIntersectWith(edge.ToArc(), out intersections, tolerance)
                : TryIntersectWith(edge.ToLine(), out intersections, tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses this circle.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where a curved chain crosses this circle, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where a curved loop crosses this circle.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where a curved loop crosses this circle, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.GetIntersections(loop, this, tolerance);

    }
}
