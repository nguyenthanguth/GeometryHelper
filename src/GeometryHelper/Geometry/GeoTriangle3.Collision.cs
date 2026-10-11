using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a triangle touches another shape.
    /// </summary>
    public readonly partial struct GeoTriangle3
    {
        /// <summary>
        /// Checks whether this triangle touches another triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this triangle touches another triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this triangle touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray runs into this triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray runs into this triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
        /// <summary>
        /// Checks whether this triangle touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this triangle touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this triangle touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this triangle touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this triangle touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this triangle touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this triangle touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this triangle touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a straight chain: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoTriangle3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this triangle touches a straight chain, within a tolerance: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoTriangle3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance)
            => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this triangle and a polygon touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this triangle and a polygon touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this triangle and the material of a face touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoFace3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this triangle and the material of a face touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoTriangle3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(plane, this);

        /// <summary>
        /// Checks whether this triangle touches a plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoTriangle3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(plane, this, tolerance);

        /// <summary>
        /// Checks whether this triangle and the material of a solid touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this triangle and the material of a solid touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this triangle and an oriented box touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoObb3)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) cross at four points at height 50; a triangle wholly inside the box touches it and crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this triangle and an oriented box touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) cross at four points at height 50; a triangle wholly inside the box touches it and crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this triangle and an axis-aligned box touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) cross at four points at height 50; a triangle wholly inside the box touches it and crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this triangle and an axis-aligned box touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) cross at four points at height 50; a triangle wholly inside the box touches it and crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);
    }
}
