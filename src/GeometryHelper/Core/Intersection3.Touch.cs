using System.Collections.Generic;
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
                : Meeting(line, Gap3.Region.Of(triangle), box, triangle, crossed, crossing, readShortAsPoint, tolerance, endless);
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
                : Meeting(line, Gap3.Region.Of(polygon), box, null, crossed, crossing, readShortAsPoint, tolerance, endless);
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
                : Meeting(line, Gap3.Region.Of(face), box, null, crossed, crossing, readShortAsPoint, tolerance, endless);
        }

        /// <summary>
        /// Gets where a ray meets a triangle, as a list: the crossing, or the point of a touch, the ray read as far as the
        /// corner of the triangle farthest from its origin, as <see cref="Collision3.CollidesWith(GeoRay3, GeoTriangle3, Tolerance)"/>
        /// reads it, and only where the line without ends along it meets it (see <see cref="AsItsLine"/>).
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
        {
            bool flat = !triangle.IsDegenerate(tolerance);
            Gap3.Region region = Gap3.Region.Of(triangle);
            bool crossed = TryCrossTriangle(ray, triangle, out GeoPoint3 crossing, tolerance, false);
            GeoPoint3[] own = crossed && flat && OnPlaneAndPiece(crossing, triangle.GetPlane(), ray)
                ? new[] { crossing }
                : Meeting(ray, region, triangle, crossed, crossing, tolerance);
            GeoLine3 line = XLine3.CutPast(new GeoXLine3(ray.Origin, ray.Direction), triangle, tolerance);

            return AsItsLine(ray, Meeting(line, triangle, true, tolerance, true), own, line, region, flat, tolerance);
        }

        /// <summary>
        /// Gets where a ray meets a polygon, as a list: the crossing, or the point of a touch, only where the line without ends
        /// along it meets it (see <see cref="AsItsLine"/>).
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
        {
            bool crossed = TryCrossPolygon(ray, polygon, out GeoPoint3 crossing, tolerance);
            Gap3.Region region = Gap3.Region.Of(polygon);
            GeoPoint3[] own = crossed && OnPlaneAndPiece(crossing, polygon.GetPlane(), ray)
                ? new[] { crossing }
                : Meeting(ray, region, null, crossed, crossing, tolerance);
            GeoLine3 line = XLine3.CutPast(new GeoXLine3(ray.Origin, ray.Direction), polygon, tolerance);

            return AsItsLine(ray, Meeting(line, polygon, true, tolerance, true), own, line, region, true, tolerance);
        }

        /// <summary>
        /// Gets where a ray meets the material of a face, as a list: the crossing, or the point of a touch, only where the line
        /// without ends along it meets it (see <see cref="AsItsLine"/>).
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
        {
            bool crossed = TryCrossFace(ray, face, out GeoPoint3 crossing, tolerance, false);
            Gap3.Region region = Gap3.Region.Of(face);
            GeoPoint3[] own = crossed && OnPlaneAndPiece(crossing, face.GetPlane(), ray)
                ? new[] { crossing }
                : Meeting(ray, region, null, crossed, crossing, tolerance);
            GeoLine3 line = XLine3.CutPast(new GeoXLine3(ray.Origin, ray.Direction), face, tolerance);

            return AsItsLine(ray, Meeting(line, face, true, tolerance, true), own, line, region, true, tolerance);
        }

        /// <summary>
        /// Reads a ray as the line without ends along it is read, by how far that line climbs across the region, and keeps what
        /// it meets at the origin or ahead of it: a ray meets the region only where its line does. Where the ray's own reading,
        /// by its crossing of the plane and the part of it over the region, gives a point within the point tolerance of the
        /// line's, that point is given, so a ray the two read alike, as one crossing the plane at a slant, is given what it
        /// always was. Where the line meets the region only behind the origin, or lies level over it only behind the origin,
        /// the ray running away from it, and the origin itself touches the region, the ray is given its origin, the one place
        /// of that meeting it holds. The list is held to <c>CollidesWith</c> (<see cref="Collision3.RayTouches(Gap3.Region, GeoRay3, Tolerance)"/>): a ray that
        /// touches the region and holds no more than the point tolerance of a part of its line lying level over it is given
        /// its own point, and a point given that the test does not say touches is not.
        /// </summary>
        /// <remarks>
        /// A ray from 0.0005 over a square 100 by 100 in the floor, falling 1E-5 for each unit along it, climbs 0.001 across the
        /// square, more than the planar tolerance of a thousandth, and is given its crossing 50 along, as its line is; one from
        /// 0.0002 over it falling 4E-6 climbs 0.0004, lies level over the square as its line does, and is given nothing. One
        /// rising a hundredth from 0.0005 under the square crosses the floor 0.05 behind its origin, and is given its origin;
        /// so is one 0.0003 over the floor and 0.0006 beside the rim of a triangle running away from it, whose line lies level
        /// over the triangle behind it.
        /// </remarks>
        private static GeoPoint3[] AsItsLine(
            GeoRay3 ray, GeoPoint3[] onLine, GeoPoint3[] own, GeoLine3 line, Gap3.Region region, bool flat, Tolerance tolerance)
        {
            GeoPoint3 origin = ray.Origin;
            double rounding = 1E-12 * (1.0 + System.Math.Max(System.Math.Abs(origin.X), System.Math.Max(System.Math.Abs(origin.Y), System.Math.Abs(origin.Z))));
            var ahead = new List<GeoPoint3>(onLine.Length);

            foreach (GeoPoint3 point in onLine)
            {
                if (origin.GetVectorTo(point).DotProduct(ray.Direction) < -rounding)
                {
                    continue;
                }

                GeoPoint3 given = point;

                foreach (GeoPoint3 mine in own)
                {
                    if (!(mine.DistanceTo(point) > tolerance.EqualPoint))
                    {
                        given = mine;
                        break;
                    }
                }

                ahead.Add(given);
            }

            if (ahead.Count == 0 && own.Length > 0 && (onLine.Length > 0 || RunsAway(ray, region, flat, tolerance)))
            {
                foreach (GeoPoint3 mine in own)
                {
                    if (!(mine.DistanceTo(origin) > tolerance.EqualPoint))
                    {
                        ahead.Add(mine);
                    }
                }
            }

            // Touching the region as CollidesWith reads it, and holding no part of the line that lies level over it, the ray meets
            // it at a place: the one of its own reading, or else its point nearest the region. So is a ray running away from the
            // region its line lies level over behind it, whose own part over the region was read beside a side and found to run
            // along it, and one whose line names the one place nearest of two it touches, behind the origin.
            if (ahead.Count == 0 && (own.Length > 0 || Near(ray, region.Box, tolerance))
                && Collision3.RayTouches(region, ray, tolerance) && !HoldsLevelPart(ray, line, region, flat, tolerance))
            {
                ahead.AddRange(own.Length > 0
                    ? own
                    : new[] { region.Nearest(origin, Parametrization3.GetPointAtDistance(ray, region.Reach(origin))) });
            }

            // The point given, one at most, is given only where the ray touches the region as CollidesWith reads it, asked where it
            // does not plainly stand within the point tolerance of both: at a tolerance of nought the line's crossing a hair behind
            // the origin, which the line keeps to the rounding of its numbers, was given where the ray touches nothing.
            if (ahead.Count > 0 && !Plainly(ray, ahead, region, rounding, tolerance) && !Collision3.RayTouches(region, ray, tolerance))
            {
                ahead.Clear();
            }

            return ahead.ToArray();
        }

        /// <summary>
        /// Determines whether a ray holds more than the point tolerance of the part of the line along it that lies level over a
        /// region, as the line is read (<see cref="LiesLevelOver"/>): the two touch along a length there and name no place.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="line">The line without ends along the ray, cut past the region.</param>
        /// <param name="region">The region.</param>
        /// <param name="flat">Whether the region has a plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool HoldsLevelPart(GeoRay3 ray, GeoLine3 line, Gap3.Region region, bool flat, Tolerance tolerance)
        {
            GeoPoint3 nearest = region.Nearest(line.StartPoint, line.EndPoint);

            if (!region.TryGetPartOver(line.StartPoint, line.EndPoint, nearest, flat, tolerance.EqualPoint, out GeoPoint3 start, out GeoPoint3 end)
                || !region.LiesLevel(start, end, nearest, tolerance.EqualPlanar, flat))
            {
                return false;
            }

            // The line runs along the ray, so the part runs from its start to its end along the ray too.
            double from = System.Math.Max(0.0, Parametrization3.GetDistanceAtPoint(ray, start));

            return Parametrization3.GetDistanceAtPoint(ray, end) - from > tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether every point stands within the point tolerance of both a ray and a region with the rounding of their
        /// numbers to spare, so that the two plainly touch.
        /// </summary>
        private static bool Plainly(GeoRay3 ray, List<GeoPoint3> points, Gap3.Region region, double rounding, Tolerance tolerance)
        {
            foreach (GeoPoint3 point in points)
            {
                GeoVector3 offset = ray.Origin.GetVectorTo(point);
                double along = offset.DotProduct(ray.Direction);
                double off = along > 0.0 ? offset.Subtract(ray.Direction.Multiply(along)).Length : offset.Length;

                if (region.PointGap(point) + off + rounding > tolerance.EqualPoint)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the part of a ray over a region is no longer than the point tolerance: it only passes a corner or
        /// runs away from the rim.
        /// </summary>
        private static bool RunsAway(GeoRay3 ray, Gap3.Region region, bool flat, Tolerance tolerance)
        {
            GeoPoint3 end = Parametrization3.GetPointAtDistance(ray, region.Reach(ray.Origin));

            return !region.TryGetPartOver(ray.Origin, end, region.Nearest(ray.Origin, end), flat, tolerance.EqualPoint, out _, out _);
        }

        /// <summary>
        /// Gets where a ray meets a region, read as far as the corner of the region farthest from its origin, past which it
        /// only draws away from it; one plainly clear of the region's box as far as the box's farthest corner is not read.
        /// </summary>
        private static GeoPoint3[] Meeting(GeoRay3 ray, Gap3.Region region, GeoTriangle3? triangle, bool crossed, GeoPoint3 crossing, Tolerance tolerance)
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

            return Meeting(line, region, box, triangle, crossed, crossing, false, tolerance, true);
        }

        /// <summary>
        /// Determines whether a ray comes within the reach of the tolerance of a region's box, as far as the box's farthest corner:
        /// the test a ray plainly clear of the region is passed over by (<see cref="Clear"/>).
        /// </summary>
        private static bool Near(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
        {
            double far = 0.0;

            foreach (GeoPoint3 corner in box.GetCorners())
            {
                far = System.Math.Max(far, ray.Origin.DistanceTo(corner));
            }

            return !Clear(new GeoLine3(ray.Origin, Parametrization3.GetPointAtDistance(ray, far)), box, false, tolerance);
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
        /// <param name="crossing">The crossing, where one was found.</param>
        /// <param name="readShortAsPoint">Whether a piece no longer than the point tolerance is read as its start.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="endless">
        /// Whether the piece is a ray or a line without ends cut to reach past the region: it lies level as a segment lying over the
        /// region does, by the ends of the part of it over the region (<see cref="Gap3.Region.TryGetPartOver"/>), not by where it was cut, so
        /// that the line without ends 0.0009 over a polygon and rising 1E-6, cut a millimetre past it, lies level over it.
        /// </param>
        private static GeoPoint3[] Meeting(
            GeoLine3 line, Gap3.Region region, GeoAabb3 box, GeoTriangle3? triangle, bool crossed, GeoPoint3 crossing, bool readShortAsPoint, Tolerance tolerance,
            bool endless = false)
        {
            GeoPoint3 from = line.StartPoint;
            GeoPoint3 to = line.EndPoint;
            double near = tolerance.EqualPoint;
            bool flat = triangle == null || !triangle.Value.IsDegenerate(tolerance);

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
