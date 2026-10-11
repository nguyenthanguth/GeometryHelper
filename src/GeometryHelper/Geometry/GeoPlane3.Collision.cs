using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a plane touches another shape.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Checks whether this plane touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this plane touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this plane touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this plane touches a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches an edge.
        /// </summary>
        /// <param name="edge">The edge.</param>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this plane touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this plane touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this plane touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this plane touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches a straight chain: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoPlane3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this plane touches a straight chain, within a tolerance: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance)
            => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches a triangle: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoTriangle3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(this, triangle);

        /// <summary>
        /// Checks whether this plane touches a triangle, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoTriangle3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Checks whether this plane touches a polygon: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this plane touches a polygon, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this plane touches the material of a face: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoFace3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this plane touches the material of a face, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this plane touches a body: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 standing on the floor is touched by the plane z = 50 and by the plane z = 100 through
        /// its top, and missed by the plane z = 101; a body in two pieces either side of a plane does not touch it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this plane touches a body, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 standing on the floor is touched by the plane z = 50 and by the plane z = 100 through
        /// its top, and missed by the plane z = 101; a body in two pieces either side of a plane does not touch it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether a ray touches this plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoPlane3)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// touches it everywhere and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray touches this plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// touches it everywhere and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);

        /// <summary>
        /// Checks whether this plane touches an oriented box: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoObb3)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this plane touches an oriented box, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this plane touches an axis-aligned box: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this plane touches an axis-aligned box, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);
    }
}
