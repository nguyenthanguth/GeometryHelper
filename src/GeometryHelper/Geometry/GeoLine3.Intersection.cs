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
        /// whichever of a segment or an arc it is, since an arc can meet one twice.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance)
        {
            return Intersection3.TryIntersectWith(this, plane, out GeoPoint3 crossing, tolerance)
                ? new[] { crossing }
                : new GeoPoint3[0];
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
        /// whichever of a segment or an arc it is, since an arc can meet one twice.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Intersection3.TryIntersectWith(this, triangle, out GeoPoint3 crossing, tolerance)
                ? new[] { crossing }
                : new GeoPoint3[0];
        }

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
        /// whichever of a segment or an arc it is, since an arc can meet one twice.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance)
        {
            return Intersection3.TryIntersectWith(this, polygon, out GeoPoint3 crossing, tolerance)
                ? new[] { crossing }
                : new GeoPoint3[0];
        }

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
        /// whichever of a segment or an arc it is, since an arc can meet one twice.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance)
        {
            return Intersection3.TryIntersectWith(this, face, out GeoPoint3 crossing, tolerance)
                ? new[] { crossing }
                : new GeoPoint3[0];
        }

    }
}
