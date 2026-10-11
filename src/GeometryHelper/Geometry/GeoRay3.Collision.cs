using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a ray touches another shape.
    /// </summary>
    public readonly partial struct GeoRay3
    {
        /// <summary>
        /// Checks whether this ray touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this ray touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this ray runs into a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(this, triangle);

        /// <summary>
        /// Checks whether this ray runs into a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Checks whether this ray runs into a polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this ray runs into a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this ray runs into a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this ray runs into a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside an axis-aligned box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this ray starts inside an axis-aligned box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside an oriented box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this ray starts inside an oriented box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);
        /// <summary>
        /// Checks whether this ray touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this ray touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this ray touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Determines whether this ray touches a segment.
        /// </summary>
        /// <remarks>
        /// Two pieces lying along each other touch along a length and cross nowhere, so this is not the crossing
        /// test with the place thrown away: it is the shortest line between the two having no length.
        /// </remarks>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(this, line);

        /// <summary>
        /// Determines whether this ray touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this ray touches an edge: the same answer as <see cref="GeoEdge3.CollidesWith(GeoRay3)"/>.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => edge.CollidesWith(this);

        /// <summary>
        /// Checks whether this ray touches an edge, within a tolerance: the same answer as
        /// <see cref="GeoEdge3.CollidesWith(GeoRay3, Tolerance)"/>.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance) => edge.CollidesWith(this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a curved loop: the same answer as
        /// <see cref="GeoPolygonArc3.CollidesWith(GeoRay3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop) => ArcChain3.CollidesWith(loop, this);

        /// <summary>
        /// Checks whether this ray touches a curved loop, within a tolerance: the same answer as
        /// <see cref="GeoPolygonArc3.CollidesWith(GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance)
            => ArcChain3.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a straight chain: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoRay3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Checks whether this ray touches a straight chain, within a tolerance: the same answer as
        /// <see cref="GeoPolyline3.CollidesWith(GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance)
            => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a curved chain: the same answer as
        /// <see cref="GeoPolylineArc3.CollidesWith(GeoRay3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain) => ArcChain3.CollidesWith(chain, this);

        /// <summary>
        /// Checks whether this ray touches a curved chain, within a tolerance: the same answer as
        /// <see cref="GeoPolylineArc3.CollidesWith(GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance)
            => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a plane: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoPlane3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// touches it everywhere and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane) => Collision3.CollidesWith(this, plane);

        /// <summary>
        /// Checks whether this ray touches a plane, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// touches it everywhere and crosses it nowhere.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Checks whether this ray and another touch: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoRay3)"/>.
        /// </summary>
        /// <param name="other">The ray.</param>
        /// <remarks>
        /// A ray from the origin along X and one from (50, -50, 0) along Y cross at (50, 0, 0); two rays along the X axis
        /// from 0 and from 100, facing each other, touch along the stretch between them and name no place.
        /// </remarks>
        public bool CollidesWith(GeoRay3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this ray and another touch, within a tolerance: the same answer as
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <param name="other">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray from the origin along X and one from (50, -50, 0) along Y cross at (50, 0, 0); two rays along the X axis
        /// from 0 and from 100, facing each other, touch along the stretch between them and name no place.
        /// </remarks>
        public bool CollidesWith(GeoRay3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);
    }
}
