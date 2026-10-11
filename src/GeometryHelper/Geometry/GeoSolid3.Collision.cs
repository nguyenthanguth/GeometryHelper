using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a solid touches another shape.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Determines whether this solid touches or overlaps a line segment.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a line segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polyline.
        /// </summary>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polyline, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance) => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polygon.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(polygon, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(polygon, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps a face.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(face, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps an oriented box.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(box, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(box, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps another solid.
        /// </summary>
        public bool CollidesWith(GeoSolid3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Determines whether this solid touches or overlaps another solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One body sitting wholly inside the other counts as touching, even where no two faces meet.
        /// </remarks>
        public bool CollidesWith(GeoSolid3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Determines whether this solid is reached by a ray.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Determines whether this solid is reached by a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);

        /// <summary>
        /// Checks whether this solid touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(box, this);

        /// <summary>
        /// Checks whether this solid touches an axis-aligned box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(box, this, tolerance);
        /// <summary>
        /// Checks whether this solid touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this solid touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this solid touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this solid touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this solid touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this solid touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this solid touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this solid touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this solid touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this solid touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this solid touches a plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 standing on the floor is touched by the plane z = 50 and by the plane z = 100 through
        /// its top, and missed by the plane z = 101; a body in two pieces either side of a plane does not touch it.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(plane, this);

        /// <summary>
        /// Checks whether this solid touches a plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 standing on the floor is touched by the plane z = 50 and by the plane z = 100 through
        /// its top, and missed by the plane z = 101; a body in two pieces either side of a plane does not touch it.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(plane, this, tolerance);

        /// <summary>
        /// Checks whether this solid and a triangle touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(triangle, this);

        /// <summary>
        /// Checks whether this solid and a triangle touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50), (300, -50, 50)
        /// and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0,
        /// 100, 50); a triangle wholly inside the box crosses nothing and touches it.
        /// </remarks>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(triangle, this, tolerance);
    }
}
