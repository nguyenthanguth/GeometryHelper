using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a polygon touches another shape.
    /// </summary>
    public sealed partial class GeoPolygon3
    {
        /// <summary>
        /// Checks whether this polygon touches an oriented box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this polygon touches an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this polygon touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this polygon touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray runs into this polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray runs into this polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
        /// <summary>
        /// Checks whether this polygon touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this polygon touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this polygon touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this polygon touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this polygon touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this polygon touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this polygon touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a straight chain: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoPolygon3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this polygon touches a straight chain, within a tolerance: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance)
            => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this polygon and a triangle touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(triangle, this);

        /// <summary>
        /// Checks whether this polygon and a triangle touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(triangle, this, tolerance);

        /// <summary>
        /// Checks whether this polygon and another touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPolygon3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="other">The polygon.</param>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along y =
        /// 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0); two squares in the floor, the second moved 50 along
        /// X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this polygon and another touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPolygon3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="other">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along y =
        /// 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0); two squares in the floor, the second moved 50 along
        /// X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this polygon and the material of a face touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this polygon and the material of a face touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(plane, this);

        /// <summary>
        /// Checks whether this polygon touches a plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(plane, this, tolerance);

        /// <summary>
        /// Checks whether this polygon and an axis-aligned box touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPolygon3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this polygon and an axis-aligned box touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPolygon3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);
    }
}
