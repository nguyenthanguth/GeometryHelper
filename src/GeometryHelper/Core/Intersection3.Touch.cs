using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Where a segment, a ray, a line or a chain meets a triangle, a polygon or a face, as a list: the crossing where it passes
    // through, and where it only touches, as CollidesWith reads a touch, the point of it nearest the region, as an arc and a
    // box are given one.
    public static partial class Intersection3
    {
        /// <summary>
        /// Gets where a segment meets a triangle, as a list: the crossing, or the point of a touch.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="readShortAsPoint">
        /// true for a segment a caller gave, read as <see cref="Collision3.CollidesWith(GeoLine3, GeoTriangle3, Tolerance)"/>
        /// reads it; false for a side of a chain or a ray cut short, read as each side of a chain is.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="endless">
        /// true for a line without ends cut to reach past the triangle, which is read level by the part of it over the triangle, as a
        /// segment is by its ends; false for a segment.
        /// </param>
        internal static GeoPoint3[] Meeting(GeoLine3 line, GeoTriangle3 triangle, bool readShortAsPoint, Tolerance tolerance, bool endless = false)
        {
            bool crossed = TryIntersectWith(line, triangle, out GeoPoint3 crossing, tolerance);

            if (crossed && !triangle.IsDegenerate(tolerance) && OnPlaneAndPiece(crossing, triangle.GetPlane(), line))
            {
                return new[] { crossing };
            }

            GeoAabb3 box = triangle.GetAabb();

            return Clear(line, box, crossed, tolerance)
                ? new GeoPoint3[0]
                : Meeting(line, Gap3.Region.Of(triangle), box, triangle, crossed, false, crossing, readShortAsPoint, tolerance, endless);
        }

        /// <summary>
        /// Gets where a segment meets a polygon, as a list: the crossing, or the point of a touch; a line without ends cut to reach
        /// past it read level by the part of it over the polygon.
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoLine3 line, GeoPolygon3 polygon, bool readShortAsPoint, Tolerance tolerance, bool endless = false)
        {
            bool crossed = TryIntersectWith(line, polygon, out GeoPoint3 crossing, tolerance);

            if (crossed && OnPlaneAndPiece(crossing, polygon.GetPlane(), line))
            {
                return new[] { crossing };
            }

            GeoAabb3 box = polygon.GetAabb();

            return Clear(line, box, crossed, tolerance)
                ? new GeoPoint3[0]
                : Meeting(line, Gap3.Region.Of(polygon), box, null, crossed, false, crossing, readShortAsPoint, tolerance, endless);
        }

        /// <summary>
        /// Gets where a segment meets the material of a face, as a list: the crossing, or the point of a touch; a line without ends cut
        /// to reach past it read level by the part of it over the face.
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoLine3 line, GeoFace3 face, bool readShortAsPoint, Tolerance tolerance, bool endless = false)
        {
            bool crossed = TryIntersectWith(line, face, out GeoPoint3 crossing, tolerance);

            if (crossed && OnPlaneAndPiece(crossing, face.GetPlane(), line))
            {
                return new[] { crossing };
            }

            GeoAabb3 box = face.GetAabb();

            return Clear(line, box, crossed, tolerance)
                ? new GeoPoint3[0]
                : Meeting(line, Gap3.Region.Of(face), box, null, crossed, false, crossing, readShortAsPoint, tolerance, endless);
        }

        /// <summary>
        /// Gets where a ray meets a triangle, as a list: the crossing, or the point of a touch, the ray read as far as the
        /// corner of the triangle farthest from its origin, as <see cref="Collision3.CollidesWith(GeoRay3, GeoTriangle3, Tolerance)"/>
        /// reads it.
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
        {
            bool crossed = TryIntersectWith(ray, triangle, out GeoPoint3 crossing, tolerance);
            bool flat = !triangle.IsDegenerate(tolerance);
            bool pierces = crossed && flat && OnPlaneAndPiece(crossing, triangle.GetPlane(), ray);
            bool nearPlane = crossed && flat && NearPlane(ray, triangle.GetPlane(), crossing, tolerance);

            if (pierces && !nearPlane)
            {
                return new[] { crossing };
            }

            return Meeting(ray, Gap3.Region.Of(triangle), triangle, crossed, pierces, nearPlane, crossing, tolerance);
        }

        /// <summary>
        /// Gets where a ray meets a polygon, as a list: the crossing, or the point of a touch.
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
        {
            bool crossed = TryIntersectWith(ray, polygon, out GeoPoint3 crossing, tolerance);
            bool pierces = crossed && OnPlaneAndPiece(crossing, polygon.GetPlane(), ray);
            bool nearPlane = crossed && NearPlane(ray, polygon.GetPlane(), crossing, tolerance);

            if (pierces && !nearPlane)
            {
                return new[] { crossing };
            }

            return Meeting(ray, Gap3.Region.Of(polygon), null, crossed, pierces, nearPlane, crossing, tolerance);
        }

        /// <summary>
        /// Gets where a ray meets the material of a face, as a list: the crossing, or the point of a touch.
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
        {
            bool crossed = TryIntersectWith(ray, face, out GeoPoint3 crossing, tolerance);
            bool pierces = crossed && OnPlaneAndPiece(crossing, face.GetPlane(), ray);
            bool nearPlane = crossed && NearPlane(ray, face.GetPlane(), crossing, tolerance);

            if (pierces && !nearPlane)
            {
                return new[] { crossing };
            }

            return Meeting(ray, Gap3.Region.Of(face), null, crossed, pierces, nearPlane, crossing, tolerance);
        }

        /// <summary>
        /// Determines whether a ray starting within the planar tolerance of a region's plane has a crossing that is no crossing: the
        /// ray level with the plane within the vector tolerance, its crossing read as a touch at its origin or a hair ahead of it as
        /// the rounding of the origin falls, or its crossing within the point tolerance of its origin, as one rising off the plane is
        /// given. A ray from 0.0009 under the plane rising 1.5E-5 crosses it 60 along, and is given that crossing.
        /// </summary>
        private static bool NearPlane(GeoRay3 ray, GeoPlane3 plane, GeoPoint3 crossing, Tolerance tolerance)
            => !(System.Math.Abs(plane.SignedDistanceTo(ray.Origin)) > tolerance.EqualPlanar)
                && (!(System.Math.Abs(ray.Direction.DotProduct(plane.Normal)) > tolerance.EqualVector) || !(crossing.DistanceTo(ray.Origin) > tolerance.EqualPoint));

        /// <summary>
        /// Gets where a ray meets a region, read as far as the corner of the region farthest from its origin, past which it
        /// only draws away from it; one plainly clear of the region's box as far as the box's farthest corner is not read.
        /// </summary>
        /// <remarks>
        /// A ray starting within the planar tolerance of the region's plane, level with it within the vector tolerance or crossing it
        /// within the point tolerance of its origin, is read by the part of it over the region: where that lies level over the region it
        /// meets it nowhere, whatever its crossing.
        /// </remarks>
        private static GeoPoint3[] Meeting(GeoRay3 ray, Gap3.Region region, GeoTriangle3? triangle, bool crossed, bool pierces, bool nearPlane, GeoPoint3 crossing, Tolerance tolerance)
        {
            GeoAabb3 box = region.Box;

            if (!crossed)
            {
                double far = 0.0;

                foreach (GeoPoint3 corner in box.GetCorners())
                {
                    far = System.Math.Max(far, ray.Origin.DistanceTo(corner));
                }

                if (Clear(new GeoLine3(ray.Origin, Parametrization3.GetPointAtDistance(ray, far)), box, false, tolerance))
                {
                    return new GeoPoint3[0];
                }
            }

            var line = new GeoLine3(ray.Origin, Parametrization3.GetPointAtDistance(ray, region.Reach(ray.Origin)));

            return Meeting(line, region, box, triangle, crossed, pierces, crossing, false, tolerance, true, nearPlane);
        }

        /// <summary>
        /// Determines whether a crossing lies on the region's plane and on the piece, to the rounding of its numbers: the point
        /// of the plane the piece passes through, which the region holds within the point tolerance, as its crossing found it,
        /// so that the two touch there without the gap being asked.
        /// </summary>
        private static bool OnPlaneAndPiece(GeoPoint3 crossing, GeoPlane3 plane, GeoLine3 line)
            => OnPlane(crossing, plane, out double rounding) && !(Gap3.PointToSegment(crossing, line.StartPoint, line.EndPoint) > rounding);

        /// <summary>
        /// Determines whether a crossing lies on the region's plane and on the ray, to the rounding of its numbers.
        /// </summary>
        private static bool OnPlaneAndPiece(GeoPoint3 crossing, GeoPlane3 plane, GeoRay3 ray)
        {
            if (!OnPlane(crossing, plane, out double rounding))
            {
                return false;
            }

            GeoPoint3 past = Parametrization3.GetPointAtDistance(ray, ray.Origin.DistanceTo(crossing) + 1.0);

            return !(Gap3.PointToSegment(crossing, ray.Origin, past) > rounding);
        }

        private static bool OnPlane(GeoPoint3 point, GeoPlane3 plane, out double rounding)
        {
            rounding = 1E-12 * (1.0 + System.Math.Max(System.Math.Abs(point.X), System.Math.Max(System.Math.Abs(point.Y), System.Math.Abs(point.Z))));

            return !(System.Math.Abs(plane.SignedDistanceTo(point)) > rounding);
        }

        /// <summary>
        /// Determines whether a straight piece that crosses nothing stands plainly clear of a region's box, further than the
        /// reach of the tolerance, as <c>CollidesWith</c> reads it first.
        /// </summary>
        private static bool Clear(GeoLine3 line, GeoAabb3 box, bool crossed, Tolerance tolerance)
            => !crossed && !(Gap3.BoxGap(line.StartPoint, line.EndPoint, box) <= Collision3.Reach(tolerance));

        /// <summary>
        /// Gets where a straight piece meets a flat region: the crossing found, where it stands within the point tolerance of
        /// both and the two touch, as <c>CollidesWith</c> reads it (rule R); otherwise, where they touch, the point of the piece
        /// nearest the region, and none where it lies level over the material, within the planar tolerance of the region's
        /// plane at both its ends, or along a side of the rim, within that of the side's line, as a curve lying on a surface
        /// crosses it nowhere (see <see cref="Gap3.Region.LiesLevel"/>).
        /// </summary>
        /// <param name="line">The piece: a segment, or a ray or a line cut short.</param>
        /// <param name="region">The region, read for the gap.</param>
        /// <param name="box">The box of the region.</param>
        /// <param name="triangle">
        /// The triangle the region is, or null for a polygon or a face: a triangle with no area has no plane, and is read by its
        /// sides only.
        /// </param>
        /// <param name="crossed">Whether a crossing was found.</param>
        /// <param name="pierces">Whether the crossing lies on the region's plane and on the piece, to rounding, for a ray starting within the planar tolerance of the plane (any other piercing having been given).</param>
        /// <param name="crossing">The crossing, where one was found.</param>
        /// <param name="readShortAsPoint">Whether a piece no longer than the point tolerance is read as its start.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="endless">
        /// Whether the piece is a ray or a line without ends cut to reach past the region: it lies level as a segment lying over the
        /// region does, by the ends of the part of it over the region (<see cref="Gap3.Region.TryGetPartOver"/>), not by where it was cut, so
        /// that the line without ends 0.0009 over a polygon and rising 1E-6, cut a millimetre past it, lies level over it.
        /// </param>
        /// <param name="nearPlane">
        /// Whether the piece is a ray starting within the planar tolerance of the region's plane and given a crossing that is none: level
        /// with the plane within the vector tolerance, or crossing it within the point tolerance of its origin, as its crossing reads a
        /// ray rising off the plane. Where the ray lies level over the region it meets it nowhere: the ray from 0.0004 over a triangle
        /// rising 1E-6, and the one from the plane itself or 1E-12 under it.
        /// </param>
        private static GeoPoint3[] Meeting(
            GeoLine3 line, Gap3.Region region, GeoAabb3 box, GeoTriangle3? triangle, bool crossed, bool pierces, GeoPoint3 crossing, bool readShortAsPoint, Tolerance tolerance,
            bool endless = false, bool nearPlane = false)
        {
            GeoPoint3 from = line.StartPoint;
            GeoPoint3 to = line.EndPoint;
            double near = tolerance.EqualPoint;
            bool flat = triangle == null || !triangle.Value.IsDegenerate(tolerance);

            if (nearPlane)
            {
                if (Touches(region, box, from, to, readShortAsPoint, tolerance) && LiesLevelOver(region, from, to, region.Nearest(from, to), flat, tolerance))
                {
                    return new GeoPoint3[0];
                }

                if (pierces)
                {
                    return new[] { crossing };
                }
            }

            if (crossed)
            {
                double off = region.PointGap(crossing) + Gap3.PointToSegment(crossing, from, to);

                // Within half the tolerance of the two put together, they stand no further apart than that: it settles the touch.
                if (!(off > near * 0.5))
                {
                    return new[] { crossing };
                }

                if (!(region.PointGap(crossing) > near) && !(Gap3.PointToSegment(crossing, from, to) > near))
                {
                    return Touches(region, box, from, to, readShortAsPoint, tolerance) ? new[] { crossing } : new GeoPoint3[0];
                }
            }

            if (!Touches(region, box, from, to, readShortAsPoint, tolerance))
            {
                return new GeoPoint3[0];
            }

            GeoPoint3 nearest = region.Nearest(from, to);
            bool level = endless ? LiesLevelOver(region, from, to, nearest, flat, tolerance) : region.LiesLevel(from, to, nearest, tolerance.EqualPlanar, flat);

            return level ? new GeoPoint3[0] : new[] { nearest };
        }

        /// <summary>
        /// Determines whether a ray or a line without ends, cut to reach past a region, lies level over it: as a segment does, by the
        /// ends of the part of it over the region. A part no longer than the point tolerance, of one that only passes a corner or runs
        /// away from the rim, lies along nothing, and the two touch at a point.
        /// </summary>
        private static bool LiesLevelOver(Gap3.Region region, GeoPoint3 from, GeoPoint3 to, GeoPoint3 nearest, bool flat, Tolerance tolerance)
        {
            return region.TryGetPartOver(from, to, nearest, flat, tolerance.EqualPoint, out GeoPoint3 start, out GeoPoint3 end)
                && region.LiesLevel(start, end, nearest, tolerance.EqualPlanar, flat);
        }

        /// <summary>
        /// Determines whether a straight piece comes within the point tolerance of a flat region, as <c>CollidesWith</c> reads
        /// it.
        /// </summary>
        private static bool Touches(Gap3.Region region, GeoAabb3 box, GeoPoint3 from, GeoPoint3 to, bool readShortAsPoint, Tolerance tolerance)
            => Gap3.BoxGap(from, to, box) <= Collision3.Reach(tolerance)
                && Gap3.Segment(region, from, to, readShortAsPoint, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;
    }
}
