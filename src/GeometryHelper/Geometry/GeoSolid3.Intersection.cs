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

        /// <summary>
        /// Gets every point where a curved chain crosses this solid.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where a curved chain crosses this solid, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where a curved loop crosses this solid.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where a curved loop crosses this solid, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this solid crosses a straight chain: the same points, in the same order, as
        /// <see cref="GeoPolyline3.GetIntersections(GeoSolid3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => Intersection3.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this solid crosses a straight chain, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolyline3.GetIntersections(GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
            => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Tries to find where this solid crosses a straight chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolyline3.TryIntersectWith(GeoSolid3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this solid crosses a straight chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolyline3.TryIntersectWith(GeoSolid3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the surface of this solid and a triangle cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoTriangle3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Intersection3.GetIntersections(triangle, this);

        /// <summary>
        /// Gets the points where the surface of this solid and a triangle cross, within a tolerance: the same points, in
        /// the same order, as <see cref="Intersection3.GetIntersections(GeoTriangle3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Intersection3.GetIntersections(triangle, this, tolerance);

        /// <summary>
        /// Tries to find where the surface of this solid and a triangle cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoTriangle3, GeoSolid3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(triangle, this, out intersections);

        /// <summary>
        /// Tries to find where the surface of this solid and a triangle cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoTriangle3, GeoSolid3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(triangle, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the surface of this solid and a polygon cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Intersection3.GetIntersections(polygon, this);

        /// <summary>
        /// Gets the points where the surface of this solid and a polygon cross, within a tolerance: the same points, in the
        /// same order, as <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Intersection3.GetIntersections(polygon, this, tolerance);

        /// <summary>
        /// Tries to find where the surface of this solid and a polygon cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polygon, this, out intersections);

        /// <summary>
        /// Tries to find where the surface of this solid and a polygon cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polygon, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the surface of this solid and the material of a face cross: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Intersection3.GetIntersections(face, this);

        /// <summary>
        /// Gets the points where the surface of this solid and the material of a face cross, within a tolerance: the same
        /// points, in the same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Intersection3.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Tries to find where the surface of this solid and the material of a face cross: the same answer, and the same
        /// points in the same order, as <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(face, this, out intersections);

        /// <summary>
        /// Tries to find where the surface of this solid and the material of a face cross, within a tolerance: the same
        /// answer, and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(face, this, out intersections, tolerance);
    }
}
