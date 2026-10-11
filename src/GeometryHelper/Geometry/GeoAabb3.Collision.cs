using GeometryHelper.Core;
using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an axis-aligned box touches another shape.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        /// <summary>
        /// Checks whether this box overlaps another one, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this box overlaps another one, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 other, Tolerance tolerance)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return false;
            }

            double t = tolerance.EqualPoint;

            return Min.X - t <= other.Max.X && Max.X + t >= other.Min.X &&
                   Min.Y - t <= other.Max.Y && Max.Y + t >= other.Min.Y &&
                   Min.Z - t <= other.Max.Z && Max.Z + t >= other.Min.Z;
        }

        /// <summary>
        /// Checks whether this box touches an oriented box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Core.Collision3.CollidesWith(box, this);

        /// <summary>
        /// Checks whether this box touches an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Core.Collision3.CollidesWith(box, this, tolerance);

        /// <summary>
        /// Checks whether this box touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Core.Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this box touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Core.Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray starts inside this box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Core.Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray starts inside this box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Core.Collision3.CollidesWith(ray, this, tolerance);

        /// <summary>
        /// Checks whether this box touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Core.Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this box touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Core.Collision3.CollidesWith(this, solid, tolerance);
        /// <summary>
        /// Checks whether this axis-aligned box touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this axis-aligned box touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches a straight chain: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoAabb3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches a straight chain, within a tolerance: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance)
            => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box and a triangle touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) cross at four points at height 50; a triangle wholly inside the box touches it and crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(triangle, this);

        /// <summary>
        /// Checks whether this axis-aligned box and a triangle touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) cross at four points at height 50; a triangle wholly inside the box touches it and crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(triangle, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box and a polygon touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPolygon3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(polygon, this);

        /// <summary>
        /// Checks whether this axis-aligned box and a polygon touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPolygon3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(polygon, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box and the material of a face touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this axis-aligned box and the material of a face touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches a plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(plane, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches a plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(plane, this, tolerance);
    }
}
