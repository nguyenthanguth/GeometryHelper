using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Where a side of a chain, a polyline or a loop meets another shape, as the public members met any segment before a segment
    // you give no longer than the point tolerance was read as its start (see Stub3): a side is the side it is however short,
    // so the chains and the cuts walk their sides through these and keep their answers to the bit.
    public static partial class Intersection3
    {
        /// <summary>
        /// Gets where a side meets a segment.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoLine3 line, Tolerance tolerance)
            => TryCross(side, line, out GeoPoint3 crossing, tolerance) ? new[] { crossing } : new GeoPoint3[0];

        /// <summary>
        /// Gets where a side meets a ray.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoRay3 ray, Tolerance tolerance) => Crossings(side, ray, tolerance);

        /// <summary>
        /// Gets where a side meets an arc.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, side, tolerance, out _);

        /// <summary>
        /// Gets where a side meets the rim of a circle.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoCircle3 circle, Tolerance tolerance)
            => Arc3.GetIntersections(Arc3.AsArc(circle), side, tolerance, out _);

        /// <summary>
        /// Gets where a side meets an axis-aligned box.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoAabb3 box, Tolerance tolerance)
            => box.IsEmpty ? new GeoPoint3[0] : SlabCrossings(side, box.ToObb(), tolerance, true);

        /// <summary>
        /// Gets where a side meets an oriented box.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoObb3 box, Tolerance tolerance) => SlabCrossings(side, box, tolerance, true);

        /// <summary>
        /// Gets where a side meets the surface of a solid.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoSolid3 solid, Tolerance tolerance) => Crossings(side, solid, false, tolerance);

        /// <summary>
        /// Gets where a side meets a triangle.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoTriangle3 triangle, Tolerance tolerance) => Meeting(side, triangle, true, tolerance);

        /// <summary>
        /// Gets where a side meets a polygon.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoPolygon3 polygon, Tolerance tolerance) => Meeting(side, polygon, true, tolerance);

        /// <summary>
        /// Gets where a side meets the material of a face.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoLine3 side, GeoFace3 face, Tolerance tolerance) => Meeting(side, face, true, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain, straight or an arc, meets a triangle.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoTriangle3 triangle, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(triangle, tolerance) : OfSide(side.ToLine(), triangle, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets a polygon.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoPolygon3 polygon, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(polygon, tolerance) : OfSide(side.ToLine(), polygon, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets the material of a face.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoFace3 face, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(face, tolerance) : OfSide(side.ToLine(), face, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets an axis-aligned box.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoAabb3 box, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(box, tolerance) : OfSide(side.ToLine(), box, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets an oriented box.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoObb3 box, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(box, tolerance) : OfSide(side.ToLine(), box, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets the surface of a solid.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoSolid3 solid, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(solid, tolerance) : OfSide(side.ToLine(), solid, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets an arc.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoArc3 arc, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(arc, tolerance) : OfSide(side.ToLine(), arc, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets the rim of a circle.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoCircle3 circle, Tolerance tolerance)
            => side.IsArc ? side.ToArc().GetIntersections(circle, tolerance) : OfSide(side.ToLine(), circle, tolerance);

        /// <summary>
        /// Gets where a side of a curved chain meets a side of another, either straight or an arc.
        /// </summary>
        internal static GeoPoint3[] OfSide(GeoEdge3 side, GeoEdge3 other, Tolerance tolerance)
        {
            if (other.IsArc)
            {
                return OfSide(side, other.ToArc(), tolerance);
            }

            return side.IsArc
                ? Arc3.GetIntersections(side.ToArc(), other.ToLine(), tolerance, out _)
                : OfSide(side.ToLine(), other.ToLine(), tolerance);
        }
    }
}
