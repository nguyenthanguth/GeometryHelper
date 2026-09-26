using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a circle touches another shape.
    /// </summary>
    public readonly partial struct GeoCircle3
    {
        /// <summary>
        /// Checks whether this circle touches a plane, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => Arc3.CollidesWith(this, plane);

        /// <summary>
        /// Checks whether this circle touches a plane, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Arc3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Checks whether this circle touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Arc3.CollidesWith(this, line);

        /// <summary>
        /// Checks whether this circle touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Arc3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this circle touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Arc3.CollidesWith(this, ray);

        /// <summary>
        /// Checks whether this circle touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Arc3.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Checks whether this circle touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this circle touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this circle touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 other) => Arc3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this circle touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 other, Tolerance tolerance) => Arc3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this circle touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => Arc3.CollidesWith(this, triangle);

        /// <summary>
        /// Checks whether this circle touches a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Arc3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Checks whether this circle touches a polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Arc3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this circle touches a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Arc3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this circle touches a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => Arc3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this circle touches a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Arc3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this circle touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Arc3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this circle touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Arc3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this circle touches an oriented box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Arc3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this circle touches an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Arc3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this circle touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Arc3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this circle touches an axis-aligned box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Arc3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this circle touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this circle touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether this circle touches a curved chain.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this circle touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this circle touches a curved loop.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this circle touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);

    }
}
