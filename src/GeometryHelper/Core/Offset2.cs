using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods that offset shapes: move a curve sideways to a parallel copy, or grow and
    /// shrink a region by a distance, as AutoCAD's OFFSET does.
    /// <para>
    /// A curve has no inside, so its offset goes to a side of its direction of travel: a positive distance
    /// moves it to the left, a negative one to the right, which for a segment drawn from left to right is up
    /// and down. The left is the side <see cref="GeoVector2.GetPerpendicularVector"/> points to.
    /// </para>
    /// <para>
    /// A region grows by a positive distance and shrinks by a negative one, whichever way round its vertices
    /// run. The result is the exact offset region for the corners asked for (<see cref="OffsetOptions"/>),
    /// not the edges moved one by one: where a shrinking region pinches off it comes back in several pieces,
    /// where it shrinks past its own width it comes back empty, and where a growing region closes a gap
    /// around empty space that space comes back as a hole. Round corners are drawn as short straight
    /// segments, since the plane library has no arc type.
    /// </para>
    /// <para>
    /// The work is done near the origin, so a drawing placed hundreds of kilometres out loses no precision,
    /// and a distance within the point tolerance returns the shape unchanged.
    /// </para>
    /// </summary>
    public static partial class Offset2
    {
        #region Line

        /// <summary>
        /// Gets the segment parallel to a segment at a distance to its left, using the default tolerance. A
        /// negative distance moves it to the right.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far to move it, to the left of its direction when positive.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoLine2 Offset(GeoLine2 line, double distance) => Offset(line, distance, Tolerance.Global);

        /// <summary>
        /// Gets the segment parallel to a segment at a distance to its left, within tolerance. A negative
        /// distance moves it to the right.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far to move it, to the left of its direction when positive.</param>
        /// <param name="tolerance">The tolerance deciding when the segment is too short.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoLine2 Offset(GeoLine2 line, double distance, Tolerance tolerance)
        {
            RequireFinite(distance, nameof(distance));

            GeoVector2 shift = GetUnitDirection(line, tolerance).GetPerpendicularVector().Multiply(distance);

            return new GeoLine2(line.StartPoint.Add(shift), line.EndPoint.Add(shift));
        }

        /// <summary>
        /// Gets the segment parallel to a segment that passes through a point, using the default tolerance.
        /// This is OFFSET with its Through option.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">A point the parallel segment passes through, on either side.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        public static GeoLine2 OffsetThrough(GeoLine2 line, GeoPoint2 point) => OffsetThrough(line, point, Tolerance.Global);

        /// <summary>
        /// Gets the segment parallel to a segment that passes through a point, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">A point the parallel segment passes through, on either side.</param>
        /// <param name="tolerance">The tolerance deciding when the segment is too short.</param>
        /// <returns>
        /// The parallel segment, of the same length and running the same way. It is moved square to the
        /// segment only, so the point need not lie between its ends.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        public static GeoLine2 OffsetThrough(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 unit = GetUnitDirection(line, tolerance);
            GeoVector2 toPoint = line.StartPoint.GetVectorTo(point);
            GeoVector2 shift = toPoint.Subtract(unit.Multiply(toPoint.DotProduct(unit)));

            return new GeoLine2(line.StartPoint.Add(shift), line.EndPoint.Add(shift));
        }

        #endregion

        #region Polygon

        /// <summary>
        /// Grows a polygon by a distance, or shrinks it when the distance is negative, with sharp corners,
        /// using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <returns>The boundary loops of the offset region; see <see cref="Offset(GeoPolygon2, double, OffsetOptions, Tolerance)"/>.</returns>
        public static GeoPolygon2[] Offset(GeoPolygon2 polygon, double distance) => Offset(polygon, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Grows a polygon by a distance, or shrinks it when the distance is negative, with sharp corners,
        /// within tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The boundary loops of the offset region; see <see cref="Offset(GeoPolygon2, double, OffsetOptions, Tolerance)"/>.</returns>
        public static GeoPolygon2[] Offset(GeoPolygon2 polygon, double distance, Tolerance tolerance) => Offset(polygon, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Grows a polygon by a distance, or shrinks it when the distance is negative, with the given corners,
        /// using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <param name="join">How the corners that open up are closed.</param>
        /// <returns>The boundary loops of the offset region; see <see cref="Offset(GeoPolygon2, double, OffsetOptions, Tolerance)"/>.</returns>
        public static GeoPolygon2[] Offset(GeoPolygon2 polygon, double distance, OffsetJoin join) => Offset(polygon, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Grows a polygon by a distance, or shrinks it when the distance is negative, with the given corners,
        /// within tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <param name="join">How the corners that open up are closed.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The boundary loops of the offset region; see <see cref="Offset(GeoPolygon2, double, OffsetOptions, Tolerance)"/>.</returns>
        public static GeoPolygon2[] Offset(GeoPolygon2 polygon, double distance, OffsetJoin join, Tolerance tolerance) => Offset(polygon, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Grows a polygon by a distance, or shrinks it when the distance is negative, as the options say,
        /// using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <param name="options">The corner shape.</param>
        /// <returns>The boundary loops of the offset region; see <see cref="Offset(GeoPolygon2, double, OffsetOptions, Tolerance)"/>.</returns>
        public static GeoPolygon2[] Offset(GeoPolygon2 polygon, double distance, OffsetOptions options) => Offset(polygon, distance, options, Tolerance.Global);

        /// <summary>
        /// Grows a polygon by a distance, or shrinks it when the distance is negative, as the options say,
        /// within tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <param name="options">The corner shape.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The boundary loops of the offset region, as AutoCAD's OFFSET draws them. Most offsets give one
        /// polygon. A shrinking polygon that pinches off gives one per piece, largest first, and one that
        /// shrinks away gives none. A growing polygon that closes a gap around empty space also gives that
        /// space: each outer loop is followed by the holes inside it, and a hole is wound the other way round,
        /// so its signed area counts against the total. Outer loops run the same way round as the polygon.
        /// Use <see cref="Offset(GeoFace2, double, OffsetOptions, Tolerance)"/> on a face to get the holes
        /// attached to their boundary.
        /// </returns>
        /// <remarks>
        /// A polygon whose edges cross is read under the even-odd rule first, as <see cref="Containment2"/>
        /// reads it, and a spike doubling back on itself is dropped, since it encloses nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the options are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoPolygon2[] Offset(GeoPolygon2 polygon, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (options == null) throw new ArgumentNullException(nameof(options));
            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { polygon.Clone() };
            }

            GeoPoint2 origin = polygon[0];
            int precision = ClipperRegion.GetPrecision(ClipperRegion.Extent(polygon.Vertices, origin));
            List<List<GeoPoint2>> region = ClipperRegion.RegionOf(polygon, origin, precision, tolerance);
            List<LoopGroup> groups = OffsetRegion(region, distance, options, tolerance);

            bool clockwise = polygon.SignedArea < 0.0;
            List<GeoPolygon2> result = new List<GeoPolygon2>();

            foreach (LoopGroup group in groups)
            {
                result.Add(ClipperRegion.ToPolygon(group.Outer, origin, clockwise));

                foreach (List<GeoPoint2> hole in group.Holes)
                {
                    result.Add(ClipperRegion.ToPolygon(hole, origin, clockwise));
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Face

        /// <summary>
        /// Grows a face by a distance, or shrinks it when the distance is negative, with sharp corners, using
        /// the default tolerance.
        /// </summary>
        public static GeoFace2[] Offset(GeoFace2 face, double distance) => Offset(face, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Grows a face by a distance, or shrinks it when the distance is negative, with sharp corners, within
        /// tolerance.
        /// </summary>
        public static GeoFace2[] Offset(GeoFace2 face, double distance, Tolerance tolerance) => Offset(face, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Grows a face by a distance, or shrinks it when the distance is negative, with the given corners,
        /// using the default tolerance.
        /// </summary>
        public static GeoFace2[] Offset(GeoFace2 face, double distance, OffsetJoin join) => Offset(face, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Grows a face by a distance, or shrinks it when the distance is negative, with the given corners,
        /// within tolerance.
        /// </summary>
        public static GeoFace2[] Offset(GeoFace2 face, double distance, OffsetJoin join, Tolerance tolerance) => Offset(face, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Grows a face by a distance, or shrinks it when the distance is negative, as the options say, using
        /// the default tolerance.
        /// </summary>
        public static GeoFace2[] Offset(GeoFace2 face, double distance, OffsetOptions options) => Offset(face, distance, options, Tolerance.Global);

        /// <summary>
        /// Grows a face by a distance, or shrinks it when the distance is negative, as the options say, within
        /// tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="distance">How far the material grows: its boundary moves out and its holes shrink when positive.</param>
        /// <param name="options">The corner shape.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The faces of the offset region, largest first, each with the holes inside it. Growing can close
        /// holes and merge nothing, since a face is one piece; shrinking can split it, and holes grown into each
        /// other or into the boundary open it up. The boundaries run the same way round as the face's.
        /// </returns>
        /// <remarks>
        /// The material is what lies inside the boundary and outside every hole, so holes that overlap or reach
        /// past the boundary take away only what they cover.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the options are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoFace2[] Offset(GeoFace2 face, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (options == null) throw new ArgumentNullException(nameof(options));
            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { face.Clone() };
            }

            GeoPoint2 origin = face.Boundary[0];
            double extent = ClipperRegion.Extent(face.Boundary.Vertices, origin);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                extent = Math.Max(extent, ClipperRegion.Extent(hole.Vertices, origin));
            }

            int precision = ClipperRegion.GetPrecision(extent);

            // The material: inside the boundary and outside every hole, resolved into clean loops first, so
            // that holes overlapping each other or the boundary are offset as the outline they actually leave.
            List<LoopGroup> material = ClipperRegion.Resolve(ClipperRegion.RegionOf(face, origin, precision, tolerance), Clipper2Lib.FillRule.Positive, precision, tolerance);
            List<LoopGroup> groups = OffsetRegion(ClipperRegion.Flatten(material), distance, options, tolerance);

            return ClipperRegion.ToFaces(groups, origin, face.Boundary.SignedArea < 0.0);
        }

        #endregion

        #region Polyline

        /// <summary>
        /// Gets the curve parallel to a polyline at a distance to its left, with sharp corners, using the
        /// default tolerance. A negative distance goes to the right.
        /// </summary>
        public static GeoPolyline2[] Offset(GeoPolyline2 polyline, double distance) => Offset(polyline, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a polyline at a distance to its left, with sharp corners, within
        /// tolerance. A negative distance goes to the right.
        /// </summary>
        public static GeoPolyline2[] Offset(GeoPolyline2 polyline, double distance, Tolerance tolerance) => Offset(polyline, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Gets the curve parallel to a polyline at a distance to its left, with the given corners, using the
        /// default tolerance. A negative distance goes to the right.
        /// </summary>
        public static GeoPolyline2[] Offset(GeoPolyline2 polyline, double distance, OffsetJoin join) => Offset(polyline, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a polyline at a distance to its left, with the given corners, within
        /// tolerance. A negative distance goes to the right.
        /// </summary>
        public static GeoPolyline2[] Offset(GeoPolyline2 polyline, double distance, OffsetJoin join, Tolerance tolerance) => Offset(polyline, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Gets the curve parallel to a polyline at a distance to its left, as the options say, using the
        /// default tolerance. A negative distance goes to the right.
        /// </summary>
        public static GeoPolyline2[] Offset(GeoPolyline2 polyline, double distance, OffsetOptions options) => Offset(polyline, distance, options, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a polyline at a distance to its left, as the options say, within
        /// tolerance. A negative distance goes to the right.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="distance">How far to move it, to the left of its direction when positive.</param>
        /// <param name="options">The shape of the corners on the outside of its turns.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The pieces of the parallel curve, in order along the polyline and running the same way. Usually
        /// one. Where the polyline turns tighter than the distance, the parallel curve would loop back over
        /// itself; those loops are cut away, as AutoCAD does, and a curve cut clean through comes back in
        /// several pieces.
        /// </returns>
        /// <remarks>
        /// The parallel curve is the edge of the band the polyline sweeps sideways, minus the polyline itself
        /// and the band's two ends. Its corners are joined on the outside of each turn, so it keeps the
        /// distance from every segment on the side it was offset to.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the options are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoPolyline2[] Offset(GeoPolyline2 polyline, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (options == null) throw new ArgumentNullException(nameof(options));
            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { polyline.Clone() };
            }

            GeoPoint2 origin = polyline[0];
            List<GeoPoint2> local = ClipperRegion.ToLocal(polyline.Vertices, origin);
            int precision = ClipperRegion.GetPrecision(ClipperRegion.Extent(new[] { local }) + Math.Abs(distance));
            double straight = ClipperRegion.GetStraightness(tolerance, precision);
            List<GeoPoint2> chain = LoopTools.CleanChain(local, tolerance.EqualPoint, straight);

            if (chain.Count < 2)
            {
                return Array.Empty<GeoPolyline2>();
            }

            List<List<GeoPoint2>> runs = OffsetChain(chain, distance, options, tolerance);
            List<GeoPolyline2> result = new List<GeoPolyline2>(runs.Count);

            foreach (List<GeoPoint2> run in runs)
            {
                List<GeoPoint2> cleaned = LoopTools.CleanChain(run, tolerance.EqualPoint, straight);

                if (cleaned.Count >= 2 && ChainLength(cleaned) > tolerance.EqualPoint)
                {
                    result.Add(ToPolyline(cleaned, origin));
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Circle and rectangle

        /// <summary>
        /// Grows a circle by a distance, or shrinks it when the distance is negative, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="distance">How far the circumference moves: outward when positive.</param>
        /// <param name="result">The concentric circle, or the circle unchanged when the method returns false.</param>
        /// <returns>true if a circle is left; false if it shrinks to nothing.</returns>
        public static bool TryOffset(GeoCircle2 circle, double distance, out GeoCircle2 result) => TryOffset(circle, distance, out result, Tolerance.Global);

        /// <summary>
        /// Grows a circle by a distance, or shrinks it when the distance is negative, within tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="distance">How far the circumference moves: outward when positive.</param>
        /// <param name="result">The concentric circle, or the circle unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance deciding when a radius is too small to keep.</param>
        /// <returns>true if a circle is left; false if it shrinks to nothing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static bool TryOffset(GeoCircle2 circle, double distance, out GeoCircle2 result, Tolerance tolerance)
        {
            RequireFinite(distance, nameof(distance));
            result = circle;
            double radius = circle.Radius + distance;

            if (radius <= tolerance.EqualPoint)
            {
                return false;
            }

            result = new GeoCircle2(circle.Center, radius);
            return true;
        }

        /// <summary>
        /// Grows a rectangle by a distance, or shrinks it when the distance is negative, keeping its corners
        /// square, using the default tolerance.
        /// </summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <param name="distance">How far each side moves: outward when positive.</param>
        /// <param name="result">The rectangle with the same center and angle, or the rectangle unchanged when the method returns false.</param>
        /// <returns>true if a rectangle is left; false if it shrinks to nothing.</returns>
        public static bool TryOffset(GeoRectangle2 rectangle, double distance, out GeoRectangle2 result) => TryOffset(rectangle, distance, out result, Tolerance.Global);

        /// <summary>
        /// Grows a rectangle by a distance, or shrinks it when the distance is negative, keeping its corners
        /// square, within tolerance. This is the sharp-cornered offset; round the corners with
        /// <see cref="Offset(GeoPolygon2, double, OffsetJoin, Tolerance)"/> on <see cref="GeoRectangle2.ToPolygon"/>.
        /// </summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <param name="distance">How far each side moves: outward when positive.</param>
        /// <param name="result">The rectangle with the same center and angle, or the rectangle unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance deciding when a side is too short to keep.</param>
        /// <returns>true if a rectangle is left; false if it shrinks to nothing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static bool TryOffset(GeoRectangle2 rectangle, double distance, out GeoRectangle2 result, Tolerance tolerance)
        {
            RequireFinite(distance, nameof(distance));
            result = rectangle;
            double width = rectangle.Width + 2.0 * distance;
            double height = rectangle.Height + 2.0 * distance;

            if (width <= tolerance.EqualPoint || height <= tolerance.EqualPoint)
            {
                return false;
            }

            result = new GeoRectangle2(rectangle.Center, width, height, rectangle.AngleRad);
            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Offsets a region given by clean loops with the region on their left, and groups the result: the
        /// corners are drawn here, and Clipper2 resolves the raw loops under the positive rule.
        /// </summary>
        private static List<LoopGroup> OffsetRegion(List<List<GeoPoint2>> region, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (region.Count == 0)
            {
                return new List<LoopGroup>();
            }

            CornerStyle style = new CornerStyle(options.Join, options.MiterLimit, options.GetArcTolerance(distance));
            List<List<GeoPoint2>> raw = new List<List<GeoPoint2>>(region.Count);

            foreach (List<GeoPoint2> loop in region)
            {
                raw.Add(OffsetOutline.BuildClosedLoop(loop, distance, style));
            }

            // Sharp corners can reach far out, so the rounding grid is chosen from the raw loops themselves.
            int precision = ClipperRegion.GetPrecision(ClipperRegion.Extent(raw));
            return ClipperRegion.Resolve(raw, Clipper2Lib.FillRule.Positive, precision, tolerance);
        }

        /// <summary>
        /// Offsets an open chain to the left by a distance (to the right when negative) and returns the pieces
        /// of the parallel curve that survive, running the same way as the chain.
        /// </summary>
        private static List<List<GeoPoint2>> OffsetChain(List<GeoPoint2> chain, double distance, OffsetOptions options, Tolerance tolerance)
        {
            bool toLeft = distance > 0.0;
            double width = Math.Abs(distance);
            List<GeoPoint2> path = new List<GeoPoint2>(chain);

            // Offsetting to the right is offsetting the reversed chain to its left.
            if (!toLeft)
            {
                path.Reverse();
            }

            CornerStyle style = new CornerStyle(options.Join, options.MiterLimit, options.GetArcTolerance(width));

            // The shared builder offsets to the right for a positive distance, so the left takes a negative one.
            List<GeoPoint2> raw = OffsetOutline.BuildOpenChain(path, -width, style, out List<bool> throughCorner);

            // The band the chain sweeps to its left, as one loop with the band on its left: along the chain, out
            // along the end, back along the raw offset, and in along the start.
            List<GeoPoint2> band = new List<GeoPoint2>(path.Count + raw.Count);
            band.AddRange(path);

            for (int i = raw.Count - 1; i >= 0; i--)
            {
                band.Add(raw[i]);
            }

            double extent = ClipperRegion.Extent(new[] { band });
            int precision = ClipperRegion.GetPrecision(extent);
            double match = ClipperRegion.GetStraightness(tolerance, precision);

            // What survives of the raw offset is its part of the band's edge. Clipper2 does not say which input
            // an output edge came from, so each edge of the band's outline is matched against the raw offset
            // edges: it survives when both its ends lie on one of them. The detours through inside corners are
            // left out of the index, since they are never part of the parallel curve.
            SegmentIndex offsetEdges = new SegmentIndex(extent);

            for (int i = 0; i + 1 < raw.Count; i++)
            {
                if (!throughCorner[i])
                {
                    offsetEdges.Add(raw[i], raw[i + 1], match);
                }
            }

            List<List<GeoPoint2>> runs = new List<List<GeoPoint2>>();

            foreach (List<GeoPoint2> loop in ClipperRegion.ResolveOutline(new[] { band }, Clipper2Lib.FillRule.Positive, precision))
            {
                CollectRuns(loop, offsetEdges, match, runs);
            }

            // The band lies on the left of its outline, so the surviving pieces run against the path: with the
            // chain for a right offset and against it for a left one.
            if (toLeft)
            {
                foreach (List<GeoPoint2> run in runs)
                {
                    run.Reverse();
                }
            }

            // In order along the parallel curve.
            List<GeoPoint2> forward = new List<GeoPoint2>(raw);

            if (!toLeft)
            {
                forward.Reverse();
            }

            runs.Sort((a, b) => PositionAlong(forward, a[0]).CompareTo(PositionAlong(forward, b[0])));
            return runs;
        }

        /// <summary>
        /// Collects the stretches of a band outline that lie along the raw offset, rather than along the chain,
        /// its ends, or the detours through inside corners.
        /// </summary>
        private static void CollectRuns(List<GeoPoint2> loop, SegmentIndex offsetEdges, double match, List<List<GeoPoint2>> runs)
        {
            int count = loop.Count;
            bool[] isOffset = new bool[count];
            int start = -1;

            for (int k = 0; k < count; k++)
            {
                isOffset[k] = offsetEdges.Covers(loop[k], loop[(k + 1) % count], match);

                if (!isOffset[k] && start < 0)
                {
                    start = k;
                }
            }

            if (start < 0)
            {
                // The whole loop is offset curve: a closed chain offset to where nothing else remains.
                List<GeoPoint2> closed = new List<GeoPoint2>(loop) { loop[0] };
                runs.Add(closed);
                return;
            }

            List<GeoPoint2> current = null;

            for (int step = 1; step <= count; step++)
            {
                int edge = (start + step) % count;

                if (isOffset[edge])
                {
                    if (current == null)
                    {
                        current = new List<GeoPoint2> { loop[edge] };
                    }

                    current.Add(loop[(edge + 1) % count]);
                }
                else if (current != null)
                {
                    runs.Add(current);
                    current = null;
                }
            }

            if (current != null)
            {
                runs.Add(current);
            }
        }

        /// <summary>
        /// Segments bucketed on a grid, to tell quickly whether an edge lies along one of them.
        /// </summary>
        private sealed class SegmentIndex
        {
            private const int CellsAcross = 64;

            private readonly List<GeoPoint2> _from = new List<GeoPoint2>();
            private readonly List<GeoPoint2> _to = new List<GeoPoint2>();
            private readonly Dictionary<(long, long), List<int>> _cells = new Dictionary<(long, long), List<int>>();
            private readonly double _cell;

            public SegmentIndex(double extent)
            {
                _cell = Math.Max(extent, 1e-9) * 2.0 / CellsAcross;
            }

            public void Add(GeoPoint2 a, GeoPoint2 b, double margin)
            {
                int id = _from.Count;
                _from.Add(a);
                _to.Add(b);

                long x0 = Cell(Math.Min(a.X, b.X) - margin);
                long x1 = Cell(Math.Max(a.X, b.X) + margin);
                long y0 = Cell(Math.Min(a.Y, b.Y) - margin);
                long y1 = Cell(Math.Max(a.Y, b.Y) + margin);

                for (long x = x0; x <= x1; x++)
                {
                    for (long y = y0; y <= y1; y++)
                    {
                        if (!_cells.TryGetValue((x, y), out List<int> list))
                        {
                            list = new List<int>();
                            _cells.Add((x, y), list);
                        }

                        list.Add(id);
                    }
                }
            }

            /// <summary>
            /// Whether an edge lies along one of the segments: both its ends within the margin of it.
            /// </summary>
            public bool Covers(GeoPoint2 p, GeoPoint2 q, double margin)
            {
                GeoPoint2 middle = PlanarMath.Midpoint(p, q);

                if (!_cells.TryGetValue((Cell(middle.X), Cell(middle.Y)), out List<int> list))
                {
                    return false;
                }

                foreach (int id in list)
                {
                    if (DistanceToSegment(p, _from[id], _to[id]) <= margin && DistanceToSegment(q, _from[id], _to[id]) <= margin)
                    {
                        return true;
                    }
                }

                return false;
            }

            private long Cell(double value) => (long)Math.Floor(value / _cell);

            private static double DistanceToSegment(GeoPoint2 point, GeoPoint2 a, GeoPoint2 b)
            {
                GeoVector2 direction = b - a;
                double lengthSquared = direction.LengthSquared;
                double t = lengthSquared > 0.0 ? Math.Max(0.0, Math.Min(1.0, (point - a).Dot(direction) / lengthSquared)) : 0.0;
                return point.DistanceTo(a + direction * t);
            }
        }

        /// <summary>
        /// The arc length along a chain to the point on it nearest a given point.
        /// </summary>
        private static double PositionAlong(List<GeoPoint2> chain, GeoPoint2 point)
        {
            double best = double.MaxValue;
            double position = 0.0;
            double travelled = 0.0;

            for (int i = 0; i + 1 < chain.Count; i++)
            {
                GeoPoint2 a = chain[i];
                GeoVector2 direction = chain[i + 1] - a;
                double length = direction.Length;
                double along = length > 0.0 ? Math.Max(0.0, Math.Min(length, (point - a).Dot(direction) / length)) : 0.0;
                GeoPoint2 nearest = length > 0.0 ? a + direction * (along / length) : a;
                double distance = nearest.DistanceSquaredTo(point);

                if (distance < best)
                {
                    best = distance;
                    position = travelled + along;
                }

                travelled += length;
            }

            return position;
        }

        private static double ChainLength(List<GeoPoint2> chain)
        {
            double length = 0.0;

            for (int i = 0; i + 1 < chain.Count; i++)
            {
                length += chain[i].DistanceTo(chain[i + 1]);
            }

            return length;
        }

        private static GeoPolyline2 ToPolyline(List<GeoPoint2> chain, GeoPoint2 origin)
        {
            GeoPoint2[] points = new GeoPoint2[chain.Count];

            for (int i = 0; i < chain.Count; i++)
            {
                points[i] = new GeoPoint2(chain[i].X + origin.X, chain[i].Y + origin.Y);
            }

            return new GeoPolyline2(points, points.Length);
        }

        /// <summary>
        /// Gets the unit direction of a segment, refusing one too short to have a direction.
        /// </summary>
        private static GeoVector2 GetUnitDirection(GeoLine2 line, Tolerance tolerance)
        {
            if (line.IsDegenerate(tolerance))
            {
                throw new InvalidOperationException("The segment is too short to have a direction to offset from.");
            }

            return line.Direction.Multiply(1.0 / line.Length);
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "The distance must be a finite number.");
            }
        }

        #endregion
    }
}
