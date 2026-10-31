using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a segment crosses another shape.
    /// </summary>
    public readonly partial struct GeoLine3
    {
        /// <summary>
        /// Tries to find the single point where this segment meets another segment, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 other, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, other, out intersection);

        /// <summary>
        /// Tries to find the single point where this segment meets another segment, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 other, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Tries to find the single point where this segment meets another, reading this one, the other or
        /// both as the infinite line carrying it, using the default tolerance.
        /// </summary>
        /// <param name="other">The other segment.</param>
        /// <param name="extension">Which segment may be reached past its endpoints: First is this one, Second the other.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoLine3 other, LineExtension extension, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, other, extension, out intersection);

        /// <summary>
        /// Tries to find the single point where this segment meets another, reading this one, the other or
        /// both as the infinite line carrying it, within a tolerance.
        /// </summary>
        /// <param name="other">The other segment.</param>
        /// <param name="extension">Which segment may be reached past its endpoints: First is this one, Second the other.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 other, LineExtension extension, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, extension, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this segment crosses a plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, plane, out intersection);

        /// <summary>
        /// Tries to find the point where this segment crosses a plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, plane, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this segment crosses a triangle, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, triangle, out intersection);

        /// <summary>
        /// Tries to find the point where this segment crosses a triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, triangle, out intersection, tolerance);

        /// <summary>
        /// Gets the single point where this segment meets another, or null where they do not meet, using the default tolerance.
        /// </summary>
        public GeoPoint3? GetIntersection(GeoLine3 other) => Intersection3.GetIntersection(this, other);

        /// <summary>
        /// Gets the single point where this segment meets another, or null where they do not meet, within a tolerance.
        /// </summary>
        public GeoPoint3? GetIntersection(GeoLine3 other, Tolerance tolerance) => Intersection3.GetIntersection(this, other, tolerance);

        /// <summary>
        /// Gets the single point where this segment meets another, reading either of them as the infinite line carrying it, or null where they still do not meet, using the default tolerance.
        /// </summary>
        public GeoPoint3? GetIntersection(GeoLine3 other, LineExtension extension) => Intersection3.GetIntersection(this, other, extension);

        /// <summary>
        /// Gets the single point where this segment meets another, reading either of them as the infinite line carrying it, or null where they still do not meet, within a tolerance.
        /// </summary>
        public GeoPoint3? GetIntersection(GeoLine3 other, LineExtension extension, Tolerance tolerance) => Intersection3.GetIntersection(this, other, extension, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses the surface of an axis-aligned box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this segment crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses the surface of an oriented box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this segment crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this segment crosses the surface of a solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find the point where this segment crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, face, out intersection);

        /// <summary>
        /// Tries to find the point where this segment crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, face, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this segment crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, polygon, out intersection);

        /// <summary>
        /// Tries to find the point where this segment crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, polygon, out intersection, tolerance);
        /// <summary>
        /// Gets every point where an arc crosses this segment, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where an arc crosses this segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where an arc crosses this segment, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where an arc crosses this segment, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses this segment, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(circle, this);

        /// <summary>
        /// Gets every point where a circle crosses this segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses this segment, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses this segment, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses a plane.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => GetIntersections(plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where this segment crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A segment meets a plane at a single point, and this is that point wrapped so that it reads
        /// like every other crossing. It is what lets a <see cref="GeoEdge3"/> be handed on as
        /// whichever of a segment or an arc it is, since an arc can meet one twice. Where the segment only
        /// touches the plane, as <see cref="CollidesWith(GeoPlane3, Tolerance)"/> reads a touch, the point is
        /// the one of it nearest the plane, as a triangle and a polygon give one: the segment from (-50, 0, 0.5)
        /// to (50, 0, 0.0005) touches the plane z = 0 at its end. A segment lying in the plane, both ends within
        /// the planar tolerance of it, meets it along its length and names no place.
        /// A segment no longer than the point tolerance is read as its start unless it truly crosses the plane: at a thousandth,
        /// the one from (5, 5, 0.0015) to (5, 5, 0.0006) meets the floor nowhere, where it was given its end, and the one from
        /// (5, 5, 0.0008) to (5, 5, 0.0002) meets it at its start.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance)
        {
            return Intersection3.MeetingOfGiven(this, plane, tolerance);
        }

        /// <summary>
        /// Gets every point where this segment crosses a triangle.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where this segment crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A segment meets a triangle at a single point, and this is that point wrapped so that it reads
        /// like every other crossing. It is what lets a <see cref="GeoEdge3"/> be handed on as
        /// whichever of a segment or an arc it is, since an arc can meet one twice. The crossing is given where it stands within
        /// the point tolerance of both and the two touch, as <see cref="CollidesWith(GeoTriangle3, Tolerance)"/> reads it. A segment
        /// that touches the triangle within the point tolerance without crossing it is given the point of it nearest the triangle, and
        /// none where it lies level over the material, within the planar tolerance of its plane at both ends:
        /// the segment from (20, 20, 0.0005) to (20, 20, 5), ending 0.0005 over the triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0), is given (20, 20, 0.0005), where it collided and was given nothing.
        /// A segment no longer than the point tolerance is read as its start unless it truly crosses: given its start where that
        /// lies within the point tolerance, as a point lying that close meets the shape, and nothing where it does not.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Intersection3.MeetingOfGiven(this, triangle, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses a polygon.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => GetIntersections(polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where this segment crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A segment meets a polygon at a single point, and this is that point wrapped so that it reads
        /// like every other crossing. It is what lets a <see cref="GeoEdge3"/> be handed on as
        /// whichever of a segment or an arc it is, since an arc can meet one twice. The crossing is given where it stands within
        /// the point tolerance of both and the two touch, as <see cref="CollidesWith(GeoPolygon3, Tolerance)"/> reads it. A segment
        /// that touches the polygon within the point tolerance without crossing it is given the point of it nearest the polygon, and
        /// none where it lies level over the material, within the planar tolerance of its plane at both ends:
        /// the segment from (20, 20, 0.0005) to (20, 20, 5), ending 0.0005 over the square 100 by 100 in the floor with a corner at the origin, is given (20, 20, 0.0005), where it collided and was given nothing.
        /// A segment no longer than the point tolerance is read as its start unless it truly crosses: given its start where that
        /// lies within the point tolerance, as a point lying that close meets the shape, and nothing where it does not.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Intersection3.MeetingOfGiven(this, polygon, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses a face.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets every point where this segment crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A segment meets a face at a single point, and this is that point wrapped so that it reads
        /// like every other crossing. It is what lets a <see cref="GeoEdge3"/> be handed on as
        /// whichever of a segment or an arc it is, since an arc can meet one twice. The crossing is given where it stands within
        /// the point tolerance of both and the two touch, as <see cref="CollidesWith(GeoFace3, Tolerance)"/> reads it. A segment
        /// that touches the face within the point tolerance without crossing it is given the point of it nearest the face, and
        /// none where it lies level over the material, within the planar tolerance of its plane at both ends:
        /// the segment from (20, 20, 0.0005) to (20, 20, 5), ending 0.0005 over the material of the square 100 by 100 in the floor with a hole 40 by 40 in its middle, is given (20, 20, 0.0005), where it collided and was given nothing.
        /// A segment no longer than the point tolerance is read as its start unless it truly crosses: given its start where that
        /// lies within the point tolerance, as a point lying that close meets the shape, and nothing where it does not.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Intersection3.MeetingOfGiven(this, face, tolerance);

        /// <summary>
        /// Gets where this segment crosses a segment, as a list.
        /// </summary>
        /// <remarks>
        /// Two straight pieces meet at one place, so the list is one long or empty. It is a list because a
        /// <see cref="GeoEdge3"/> has to be able to ask the same question of a segment and of a bend.
        /// <para>
        /// There is deliberately no <c>TryIntersectWith</c> twin taking an array here: this type already has one
        /// handing back a single point, and a second one differing only in the shape of its <c>out</c> would make
        /// every existing call to it ambiguous. <see cref="GetIntersection(GeoLine3)"/> is the single-point
        /// reading and this is the list reading of the same answer.
        /// </para>
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Intersection3.GetIntersections(this, line);

        /// <summary>
        /// Gets where this segment crosses a segment, as a list, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets where this segment crosses a ray, as a list.
        /// </summary>
        /// <remarks>
        /// Two straight pieces meet at one place, so the list is one long or empty. It is a list because a
        /// <see cref="GeoEdge3"/> has to be able to ask the same question of a segment and of a bend.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(this, ray);

        /// <summary>
        /// Gets where this segment crosses a ray, as a list, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a ray.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, ray, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a ray, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses an edge: the same points, in the same order, as
        /// <see cref="GeoEdge3.GetIntersections(GeoLine3)"/>.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => edge.GetIntersections(this);

        /// <summary>
        /// Gets every point where this segment crosses an edge, within a tolerance: the same points, in the same order,
        /// as <see cref="GeoEdge3.GetIntersections(GeoLine3, Tolerance)"/>.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.GetIntersections(this, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses a curved loop: the same points, in the same order, as
        /// <see cref="GeoPolygonArc3.GetIntersections(GeoLine3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where this segment crosses a curved loop, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolygonArc3.GetIntersections(GeoLine3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance)
            => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses a straight chain: the same points, in the same order, as
        /// <see cref="GeoPolyline3.GetIntersections(GeoLine3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => Intersection3.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this segment crosses a straight chain, within a tolerance: the same points, in the
        /// same order, as <see cref="GeoPolyline3.GetIntersections(GeoLine3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
            => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Gets every point where this segment crosses a curved chain: the same points, in the same order, as
        /// <see cref="GeoPolylineArc3.GetIntersections(GeoLine3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where this segment crosses a curved chain, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolylineArc3.GetIntersections(GeoLine3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance)
            => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses an edge: the same answer, and the same points in the same order, as
        /// <see cref="GeoEdge3.TryIntersectWith(GeoLine3, out GeoPoint3[])"/>.
        /// </summary>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections)
            => edge.TryIntersectWith(this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses an edge, within a tolerance: the same answer, and the same points
        /// in the same order, as <see cref="GeoEdge3.TryIntersectWith(GeoLine3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
            => edge.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a curved loop: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolygonArc3.TryIntersectWith(GeoLine3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections)
            => ArcChain3.TryIntersectWith(loop, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a curved loop, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolygonArc3.TryIntersectWith(GeoLine3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections, Tolerance tolerance)
            => ArcChain3.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a straight chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolyline3.TryIntersectWith(GeoLine3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a straight chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolyline3.TryIntersectWith(GeoLine3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this segment crosses a curved chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolylineArc3.TryIntersectWith(GeoLine3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections)
            => ArcChain3.TryIntersectWith(chain, this, out intersections);

        /// <summary>
        /// Tries to find where this segment crosses a curved chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolylineArc3.TryIntersectWith(GeoLine3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections, Tolerance tolerance)
            => ArcChain3.TryIntersectWith(chain, this, out intersections, tolerance);
    }
}
