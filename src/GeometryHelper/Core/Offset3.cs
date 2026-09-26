using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Internal.Planar;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods that offset shapes in space: move a curve sideways to a parallel copy, or grow
    /// and shrink a flat region within its own plane.
    /// <para>
    /// A line in space has a whole circle of directions square to it, so "sideways" has to be pinned down.
    /// Each method takes what does that: a plane to offset within (as AutoCAD's
    /// <c>GetOffsetCurvesGivenPlaneNormal</c> does), a direction to offset towards, or an up direction that
    /// fixes a left and an up for the segment, as Tekla Structures positions a part relative to its
    /// reference line. Within a plane the conventions of <c>Offset2</c> hold: seen from the side the plane
    /// normal points to, a positive distance moves a curve to the left of its direction of travel.
    /// </para>
    /// </summary>
    public static partial class Offset3
    {
        #region Line

        /// <summary>
        /// Gets the segment parallel to a segment at a distance to its left within a plane, using the default
        /// tolerance. A negative distance moves it to the right.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far to move it, to its left when positive.</param>
        /// <param name="planeNormal">The normal of the plane to offset within; the left is seen from its tip.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentException">Thrown when the normal has no length or runs along the segment.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoLine3 OffsetInPlane(GeoLine3 line, double distance, GeoVector3 planeNormal) => OffsetInPlane(line, distance, planeNormal, Tolerance.Global);

        /// <summary>
        /// Gets the segment parallel to a segment at a distance to its left within a plane, within a tolerance.
        /// A negative distance moves it to the right.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far to move it, to its left when positive.</param>
        /// <param name="planeNormal">The normal of the plane to offset within; the left is seen from its tip.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <remarks>
        /// The plane is the one through the segment whose normal is the part of <paramref name="planeNormal"/>
        /// square to the segment, so a normal that leans along the segment is straightened rather than
        /// refused. With the world Z axis as the normal, a segment drawn in the XY plane moves exactly as
        /// <c>GeoLine2.Offset</c> would move it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentException">Thrown when the normal has no length or runs along the segment.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoLine3 OffsetInPlane(GeoLine3 line, double distance, GeoVector3 planeNormal, Tolerance tolerance)
        {
            RequireFinite(distance, nameof(distance));
            GeoVector3 unit = GetUnitDirection(line, tolerance);
            GeoVector3 up = GetSquareUnit(unit, planeNormal, nameof(planeNormal), tolerance);

            // Left of the direction seen from the tip of the normal: n x u, as Z x X is Y.
            GeoVector3 shift = up.CrossProduct(unit).Multiply(distance);

            return new GeoLine3(line.StartPoint.Add(shift), line.EndPoint.Add(shift));
        }

        /// <summary>
        /// Gets the segment parallel to a segment at a distance towards a direction, using the default
        /// tolerance. A negative distance moves it the opposite way.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far to move it.</param>
        /// <param name="direction">The side to move it to; only its part square to the segment counts.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentException">Thrown when the direction has no length or runs along the segment.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoLine3 Offset(GeoLine3 line, double distance, GeoVector3 direction) => Offset(line, distance, direction, Tolerance.Global);

        /// <summary>
        /// Gets the segment parallel to a segment at a distance towards a direction, within a tolerance. A
        /// negative distance moves it the opposite way.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far to move it.</param>
        /// <param name="direction">The side to move it to; only its part square to the segment counts.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The parallel segment, of the same length and running the same way, exactly
        /// <paramref name="distance"/> away from the original.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentException">Thrown when the direction has no length or runs along the segment.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoLine3 Offset(GeoLine3 line, double distance, GeoVector3 direction, Tolerance tolerance)
        {
            RequireFinite(distance, nameof(distance));
            GeoVector3 unit = GetUnitDirection(line, tolerance);
            GeoVector3 shift = GetSquareUnit(unit, direction, nameof(direction), tolerance).Multiply(distance);

            return new GeoLine3(line.StartPoint.Add(shift), line.EndPoint.Add(shift));
        }

        /// <summary>
        /// Gets the segment parallel to a segment, moved sideways and upward in the frame an up direction
        /// fixes for it, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="lateral">How far to move it to its left, seen from above; negative moves it to the right.</param>
        /// <param name="vertical">How far to move it up; negative moves it down.</param>
        /// <param name="up">Which way is up; only its part square to the segment counts.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentException">Thrown when the up direction has no length or runs along the segment.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance is not a finite number.</exception>
        public static GeoLine3 Offset(GeoLine3 line, double lateral, double vertical, GeoVector3 up) => Offset(line, lateral, vertical, up, Tolerance.Global);

        /// <summary>
        /// Gets the segment parallel to a segment, moved sideways and upward in the frame an up direction
        /// fixes for it, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="lateral">How far to move it to its left, seen from above; negative moves it to the right.</param>
        /// <param name="vertical">How far to move it up; negative moves it down.</param>
        /// <param name="up">Which way is up; only its part square to the segment counts.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <remarks>
        /// The frame is the one Tekla Structures positions a beam in: along the segment, up (the part of
        /// <paramref name="up"/> square to it), and left, which is up crossed with the direction. A segment
        /// running straight up has no such frame, since every direction square to it is level, so a vertical
        /// segment needs an <paramref name="up"/> chosen to lie across it, the way a column needs its
        /// rotation set; it is refused rather than given one at random.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentException">Thrown when the up direction has no length or runs along the segment.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance is not a finite number.</exception>
        public static GeoLine3 Offset(GeoLine3 line, double lateral, double vertical, GeoVector3 up, Tolerance tolerance)
        {
            RequireFinite(lateral, nameof(lateral));
            RequireFinite(vertical, nameof(vertical));
            GeoVector3 unit = GetUnitDirection(line, tolerance);
            GeoVector3 upward = GetSquareUnit(unit, up, nameof(up), tolerance);
            GeoVector3 left = upward.CrossProduct(unit);
            GeoVector3 shift = left.Multiply(lateral).Add(upward.Multiply(vertical));

            return new GeoLine3(line.StartPoint.Add(shift), line.EndPoint.Add(shift));
        }

        /// <summary>
        /// Gets the segment parallel to a segment that passes through a point, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">A point the parallel segment passes through.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        public static GeoLine3 OffsetThrough(GeoLine3 line, GeoPoint3 point) => OffsetThrough(line, point, Tolerance.Global);

        /// <summary>
        /// Gets the segment parallel to a segment that passes through a point, within a tolerance. The segment
        /// is moved square to itself only, so the point need not lie between its ends.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">A point the parallel segment passes through.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The parallel segment, of the same length and running the same way.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        public static GeoLine3 OffsetThrough(GeoLine3 line, GeoPoint3 point, Tolerance tolerance)
        {
            GeoVector3 unit = GetUnitDirection(line, tolerance);
            GeoVector3 toPoint = line.StartPoint.GetVectorTo(point);
            GeoVector3 shift = toPoint.Subtract(unit.Multiply(toPoint.DotProduct(unit)));

            return new GeoLine3(line.StartPoint.Add(shift), line.EndPoint.Add(shift));
        }

        #endregion

        #region Polygon

        /// <summary>
        /// Grows a polygon within its own plane by a distance, or shrinks it when the distance is negative, with
        /// sharp corners, using the default tolerance.
        /// </summary>
        public static GeoPolygon3[] Offset(GeoPolygon3 polygon, double distance) => Offset(polygon, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Grows a polygon within its own plane by a distance, or shrinks it when the distance is negative, with
        /// sharp corners, within a tolerance.
        /// </summary>
        public static GeoPolygon3[] Offset(GeoPolygon3 polygon, double distance, Tolerance tolerance) => Offset(polygon, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Grows a polygon within its own plane by a distance, or shrinks it when the distance is negative, with
        /// the given corners, using the default tolerance.
        /// </summary>
        public static GeoPolygon3[] Offset(GeoPolygon3 polygon, double distance, OffsetJoin join) => Offset(polygon, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Grows a polygon within its own plane by a distance, or shrinks it when the distance is negative, with
        /// the given corners, within a tolerance.
        /// </summary>
        public static GeoPolygon3[] Offset(GeoPolygon3 polygon, double distance, OffsetJoin join, Tolerance tolerance) => Offset(polygon, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Grows a polygon within its own plane by a distance, or shrinks it when the distance is negative, as the
        /// options say, using the default tolerance.
        /// </summary>
        public static GeoPolygon3[] Offset(GeoPolygon3 polygon, double distance, OffsetOptions options) => Offset(polygon, distance, options, Tolerance.Global);

        /// <summary>
        /// Grows a polygon within its own plane by a distance, or shrinks it when the distance is negative, as the
        /// options say, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <param name="options">The corner shape.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The boundary loops of the offset region, in the polygon's plane, as <c>Offset2</c> returns them for a
        /// polygon in the plane: usually one; none when it shrinks away; one per piece when it pinches off; and,
        /// where a growing polygon closes a gap around empty space, that space as a hole following its outer
        /// loop. Outer loops keep the polygon's normal and holes face the other way, so their signed areas add
        /// up. Use <see cref="Offset(GeoFace3, double, OffsetOptions, Tolerance)"/> on a face to get the holes
        /// attached to their boundary.
        /// </returns>
        /// <remarks>
        /// The polygon is laid out in a frame of its own plane and offset there, so the result lies exactly in
        /// that plane. A piece too small for <see cref="GeoPolygon3"/> to accept under the tolerance, one
        /// enclosing less area than <see cref="Tolerance.EqualVector"/>, is left out, as its constructor would
        /// refuse it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the options are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoPolygon3[] Offset(GeoPolygon3 polygon, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (options == null) throw new ArgumentNullException(nameof(options));
            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { polygon.Clone() };
            }

            PlaneFrame frame = PlaneFrame.Of(polygon.Vertices, polygon.Normal);
            List<GeoPoint2> loop = frame.ToLocal(polygon.Vertices);
            double snap = PlanarRegion3.GetSnap(tolerance, PlanarRegion3.Extent(new[] { loop }) + Math.Abs(distance));

            List<List<GeoPoint2>> region = PlanarRegion3.Resolve(new[] { loop }, FillRule.EvenOdd, tolerance, snap);
            List<LoopGroup> groups = PlanarRegion3.OffsetRegion(region, distance, options, tolerance, snap);
            List<GeoPolygon3> result = new List<GeoPolygon3>();

            foreach (LoopGroup group in groups)
            {
                if (!TryBuildPolygon(frame, group.Outer, tolerance, out GeoPolygon3 outer))
                {
                    continue;
                }

                result.Add(outer);

                foreach (List<GeoPoint2> hole in group.Holes)
                {
                    if (TryBuildPolygon(frame, hole, tolerance, out GeoPolygon3 ring))
                    {
                        result.Add(ring);
                    }
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Face

        /// <summary>
        /// Grows a face within its own plane by a distance, or shrinks it when the distance is negative, with
        /// sharp corners, using the default tolerance.
        /// </summary>
        public static GeoFace3[] Offset(GeoFace3 face, double distance) => Offset(face, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Grows a face within its own plane by a distance, or shrinks it when the distance is negative, with
        /// sharp corners, within a tolerance.
        /// </summary>
        public static GeoFace3[] Offset(GeoFace3 face, double distance, Tolerance tolerance) => Offset(face, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Grows a face within its own plane by a distance, or shrinks it when the distance is negative, with the
        /// given corners, using the default tolerance.
        /// </summary>
        public static GeoFace3[] Offset(GeoFace3 face, double distance, OffsetJoin join) => Offset(face, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Grows a face within its own plane by a distance, or shrinks it when the distance is negative, with the
        /// given corners, within a tolerance.
        /// </summary>
        public static GeoFace3[] Offset(GeoFace3 face, double distance, OffsetJoin join, Tolerance tolerance) => Offset(face, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Grows a face within its own plane by a distance, or shrinks it when the distance is negative, as the
        /// options say, using the default tolerance.
        /// </summary>
        public static GeoFace3[] Offset(GeoFace3 face, double distance, OffsetOptions options) => Offset(face, distance, options, Tolerance.Global);

        /// <summary>
        /// Grows a face within its own plane by a distance, or shrinks it when the distance is negative, as the
        /// options say, within a tolerance: the plate of a Tekla contour grown for a clearance, or shrunk for the
        /// line a weld runs along.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="distance">How far the material grows: its boundary moves out and its holes shrink when positive.</param>
        /// <param name="options">The corner shape.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The faces of the offset region, largest first, each with the holes inside it and with the face's
        /// normal; the holes face the other way. Shrinking can split a face; growing can close its holes.
        /// </returns>
        /// <remarks>
        /// The material is what lies inside the boundary and outside every hole, so holes that overlap or reach
        /// past the boundary take away only what they cover.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the options are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoFace3[] Offset(GeoFace3 face, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (options == null) throw new ArgumentNullException(nameof(options));
            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { face.Clone() };
            }

            PlaneFrame frame = PlaneFrame.Of(face.Boundary.Vertices, face.Normal);
            List<GeoPoint2> boundary = frame.ToLocal(face.Boundary.Vertices);
            List<List<GeoPoint2>> holes = new List<List<GeoPoint2>>();
            double extent = PlanarRegion3.Extent(new[] { boundary });

            foreach (GeoPolygon3 hole in face.Holes)
            {
                List<GeoPoint2> ring = frame.ToLocal(hole.Vertices);
                holes.Add(ring);
                extent = Math.Max(extent, PlanarRegion3.Extent(new[] { ring }));
            }

            double snap = PlanarRegion3.GetSnap(tolerance, extent + Math.Abs(distance));

            // The boundary and each hole read under the even-odd rule, the holes' loops reversed, so that under
            // the positive rule a point counts once for the boundary and loses one for every hole it lies in.
            List<List<GeoPoint2>> loops = PlanarRegion3.Resolve(new[] { boundary }, FillRule.EvenOdd, tolerance, snap);

            foreach (List<GeoPoint2> ring in holes)
            {
                foreach (List<GeoPoint2> loop in PlanarRegion3.Resolve(new[] { ring }, FillRule.EvenOdd, tolerance, snap))
                {
                    loop.Reverse();
                    loops.Add(loop);
                }
            }

            List<List<GeoPoint2>> material = PlanarRegion3.Resolve(loops, FillRule.Positive, tolerance, snap);
            List<LoopGroup> groups = PlanarRegion3.OffsetRegion(material, distance, options, tolerance, snap);
            List<GeoFace3> result = new List<GeoFace3>();

            foreach (LoopGroup group in groups)
            {
                if (!TryBuildPolygon(frame, group.Outer, tolerance, out GeoPolygon3 outer))
                {
                    continue;
                }

                List<GeoPolygon3> rings = new List<GeoPolygon3>();

                foreach (List<GeoPoint2> hole in group.Holes)
                {
                    if (TryBuildPolygon(frame, hole, tolerance, out GeoPolygon3 ring))
                    {
                        rings.Add(ring);
                    }
                }

                result.Add(new GeoFace3(outer, rings, tolerance));
            }

            return result.ToArray();
        }

        #endregion

        #region Polyline

        /// <summary>
        /// Gets the curve parallel to a flat polyline at a distance to its left within its plane, with sharp
        /// corners, using the default tolerance. A negative distance goes to the right.
        /// </summary>
        public static GeoPolyline3[] OffsetInPlane(GeoPolyline3 polyline, double distance, GeoVector3 planeNormal) => OffsetInPlane(polyline, distance, planeNormal, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a flat polyline at a distance to its left within its plane, with sharp
        /// corners, within a tolerance.
        /// </summary>
        public static GeoPolyline3[] OffsetInPlane(GeoPolyline3 polyline, double distance, GeoVector3 planeNormal, Tolerance tolerance) => OffsetInPlane(polyline, distance, planeNormal, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Gets the curve parallel to a flat polyline at a distance to its left within its plane, with the given
        /// corners, using the default tolerance.
        /// </summary>
        public static GeoPolyline3[] OffsetInPlane(GeoPolyline3 polyline, double distance, GeoVector3 planeNormal, OffsetJoin join) => OffsetInPlane(polyline, distance, planeNormal, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a flat polyline at a distance to its left within its plane, with the given
        /// corners, within a tolerance.
        /// </summary>
        public static GeoPolyline3[] OffsetInPlane(GeoPolyline3 polyline, double distance, GeoVector3 planeNormal, OffsetJoin join, Tolerance tolerance) => OffsetInPlane(polyline, distance, planeNormal, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Gets the curve parallel to a flat polyline at a distance to its left within its plane, as the options
        /// say, using the default tolerance.
        /// </summary>
        public static GeoPolyline3[] OffsetInPlane(GeoPolyline3 polyline, double distance, GeoVector3 planeNormal, OffsetOptions options) => OffsetInPlane(polyline, distance, planeNormal, options, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a flat polyline at a distance to its left within its plane, as the options
        /// say, within a tolerance. A negative distance goes to the right.
        /// </summary>
        /// <param name="polyline">The polyline, which must lie in one plane.</param>
        /// <param name="distance">How far to move it, to its left seen from the tip of the normal when positive.</param>
        /// <param name="planeNormal">
        /// Which side of the polyline's plane it is seen from, and so which way is left. For a straight polyline,
        /// which lies in every plane through it, it also picks the plane: the one square to its part across the
        /// polyline.
        /// </param>
        /// <param name="options">The shape of the corners on the outside of its turns.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The pieces of the parallel curve, in order along the polyline and running the same way; usually one.
        /// Loops the parallel curve would make at turns tighter than the distance are cut away, as in
        /// <c>Offset2</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the polyline does not lie in one plane, when the normal has no length, or when it lies in
        /// the polyline's plane and so gives no side.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoPolyline3[] OffsetInPlane(GeoPolyline3 polyline, double distance, GeoVector3 planeNormal, OffsetOptions options, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (options == null) throw new ArgumentNullException(nameof(options));
            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { polyline.Clone() };
            }

            GeoVector3 normal = GetChainNormal(polyline, planeNormal, tolerance);
            PlaneFrame frame = PlaneFrame.Of(polyline.Vertices, normal);
            List<GeoPoint2> local = frame.ToLocal(polyline.Vertices);
            double snap = PlanarRegion3.GetSnap(tolerance, PlanarRegion3.Extent(new[] { local }) + Math.Abs(distance));
            List<GeoPoint2> chain = LoopTools.CleanChain(local, tolerance.EqualPoint, snap);

            if (chain.Count < 2)
            {
                return Array.Empty<GeoPolyline3>();
            }

            List<GeoPolyline3> result = new List<GeoPolyline3>();

            foreach (List<GeoPoint2> run in PlanarRegion3.OffsetChain(chain, distance, options, snap))
            {
                List<GeoPoint2> cleaned = LoopTools.CleanChain(run, tolerance.EqualPoint, snap);

                if (cleaned.Count >= 2 && ChainLength(cleaned) > tolerance.EqualPoint)
                {
                    result.Add(new GeoPolyline3(frame.ToWorld(cleaned), true));
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Circle

        /// <summary>
        /// Grows a circle within its plane by a distance, or shrinks it when the distance is negative, using the
        /// default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="distance">How far the circumference moves: outward when positive.</param>
        /// <param name="result">The concentric circle, or the circle unchanged when the method returns false.</param>
        /// <returns>true if a circle is left; false if it shrinks to nothing.</returns>
        public static bool TryOffset(GeoCircle3 circle, double distance, out GeoCircle3 result) => TryOffset(circle, distance, out result, Tolerance.Global);

        /// <summary>
        /// Grows a circle within its plane by a distance, or shrinks it when the distance is negative, within a
        /// tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="distance">How far the circumference moves: outward when positive.</param>
        /// <param name="result">The concentric circle, or the circle unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance deciding when a radius is too small to keep.</param>
        /// <returns>true if a circle is left; false if it shrinks to nothing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static bool TryOffset(GeoCircle3 circle, double distance, out GeoCircle3 result, Tolerance tolerance)
        {
            RequireFinite(distance, nameof(distance));
            result = circle;
            double radius = circle.Radius + distance;

            if (radius <= tolerance.EqualPoint)
            {
                return false;
            }

            result = new GeoCircle3(circle.Center, circle.Normal, radius);
            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Builds a polygon from a loop in a frame, through the public constructor so that everything a
        /// <see cref="GeoPolygon3"/> promises is checked. A piece the constructor refuses under the tolerance,
        /// being too small to enclose an area it accepts, is reported and left out.
        /// </summary>
        private static bool TryBuildPolygon(PlaneFrame frame, List<GeoPoint2> loop, Tolerance tolerance, out GeoPolygon3 polygon)
        {
            try
            {
                polygon = new GeoPolygon3(frame.ToWorld(loop), tolerance);
                return true;
            }
            catch (ArgumentException exception)
            {
                GeometryHelperLog.Debug("Offset: a piece of " + loop.Count + " vertices was too small to keep as a polygon.", exception);
                polygon = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the unit normal of the plane a polyline lies in, turned to face the way the reference normal says.
        /// </summary>
        private static GeoVector3 GetChainNormal(GeoPolyline3 polyline, GeoVector3 planeNormal, Tolerance tolerance)
        {
            double referenceLength = planeNormal.Length;

            if (referenceLength <= tolerance.EqualVector)
            {
                throw new ArgumentException("The plane normal has no length.", nameof(planeNormal));
            }

            GeoVector3 reference = planeNormal.Divide(referenceLength);

            if (polyline.TryGetPlane(out GeoPlane3 plane, tolerance))
            {
                double facing = plane.Normal.DotProduct(reference);

                if (Math.Abs(facing) <= tolerance.EqualAngleSin)
                {
                    throw new ArgumentException("The plane normal lies in the plane of the polyline, so it gives no side to offset to.", nameof(planeNormal));
                }

                return facing > 0.0 ? plane.Normal : plane.Normal.Negate();
            }

            // A straight chain lies in every plane through it: the one square to the reference's part across the
            // chain is taken, as a segment is offset within a plane.
            if (TryGetStraightDirection(polyline, tolerance, out GeoVector3 along))
            {
                GeoVector3 square = reference.Subtract(along.Multiply(reference.DotProduct(along)));

                if (square.Length <= tolerance.EqualAngleSin)
                {
                    throw new ArgumentException("The plane normal runs along the polyline, so it gives no side to offset to.", nameof(planeNormal));
                }

                return square.Divide(square.Length);
            }

            // Newell's normal vanishes for a chain whose closing edge cancels its area, a figure eight for one;
            // the plane square to the reference is the one to try then.
            if (new GeoPlane3(polyline.StartPoint, reference).ContainsAll(polyline.Vertices, tolerance))
            {
                return reference;
            }

            throw new ArgumentException("The polyline does not lie in one plane, so it has no side to offset to.", nameof(polyline));
        }

        /// <summary>
        /// Checks whether every vertex of a polyline lies on the line through its start and its farthest vertex.
        /// </summary>
        private static bool TryGetStraightDirection(GeoPolyline3 polyline, Tolerance tolerance, out GeoVector3 direction)
        {
            direction = GeoVector3.Zero;
            GeoPoint3 start = polyline.StartPoint;
            GeoPoint3 far = start;
            double farthest = 0.0;

            foreach (GeoPoint3 vertex in polyline.Vertices)
            {
                double distance = start.DistanceTo(vertex);

                if (distance > farthest)
                {
                    farthest = distance;
                    far = vertex;
                }
            }

            if (farthest <= tolerance.EqualPoint)
            {
                return false;
            }

            GeoVector3 unit = start.GetVectorTo(far).Divide(farthest);

            foreach (GeoPoint3 vertex in polyline.Vertices)
            {
                if (start.GetVectorTo(vertex).CrossProduct(unit).Length > tolerance.EqualPoint)
                {
                    return false;
                }
            }

            direction = unit;
            return true;
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

        /// <summary>
        /// Gets the unit direction of a segment, refusing one too short to have a direction.
        /// </summary>
        private static GeoVector3 GetUnitDirection(GeoLine3 line, Tolerance tolerance)
        {
            if (line.IsDegenerate(tolerance))
            {
                throw new InvalidOperationException("The segment is too short to have a direction to offset from.");
            }

            return line.Direction.Divide(line.Length);
        }

        /// <summary>
        /// Gets the unit vector along the part of a direction square to a unit axis, refusing a direction
        /// that has no length or lies along the axis within the angular tolerance.
        /// </summary>
        private static GeoVector3 GetSquareUnit(GeoVector3 axis, GeoVector3 direction, string parameterName, Tolerance tolerance)
        {
            double length = direction.Length;

            if (length <= tolerance.EqualVector)
            {
                throw new ArgumentException("The direction has no length.", parameterName);
            }

            GeoVector3 square = direction.Subtract(axis.Multiply(direction.DotProduct(axis)));

            // The square part measures the length times the sine of the angle to the axis, so this is the
            // angular test Parallel3 applies: a direction along the segment says nothing about a side.
            if (square.Length <= tolerance.EqualAngleSin * length)
            {
                throw new ArgumentException("The direction runs along the segment, so it gives no side to move to.", parameterName);
            }

            return square.Divide(square.Length);
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
