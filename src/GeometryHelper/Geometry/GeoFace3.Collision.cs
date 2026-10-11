using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a face touches another shape.
    /// </summary>
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Checks whether this face touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this face touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this face touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this face touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray runs into this face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray runs into this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
        /// <summary>
        /// Checks whether this face touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this face touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this face touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this face touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this face touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this face touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this face touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this face touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this face touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this face touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this face touches a straight chain: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoFace3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this face touches a straight chain, within a tolerance: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance)
            => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether the material of this face and a triangle touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoFace3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(triangle, this);

        /// <summary>
        /// Checks whether the material of this face and a triangle touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(triangle, this, tolerance);

        /// <summary>
        /// Checks whether the material of this face and a polygon touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether the material of this face and a polygon touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether the material of this face and of another touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoFace3)"/>.
        /// </summary>
        /// <param name="other">The face, read by its material.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20 about
        /// (50, 50) lying in its hole, and is by one about (15, 50); a face standing wholly in the hole crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other face is null.</exception>
        public bool CollidesWith(GeoFace3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether the material of this face and of another touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="other">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20 about
        /// (50, 50) lying in its hole, and is by one about (15, 50); a face standing wholly in the hole crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other face is null.</exception>
        public bool CollidesWith(GeoFace3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether the material of this face touches a plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoFace3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(plane, this);

        /// <summary>
        /// Checks whether the material of this face touches a plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(plane, this, tolerance);

        /// <summary>
        /// Checks whether the material of this face and an oriented box touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoObb3)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether the material of this face and an oriented box touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether the material of this face and an axis-aligned box touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether the material of this face and an axis-aligned box touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);
    }
}
