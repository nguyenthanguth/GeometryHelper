using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where the rim of an ellipse in space crosses another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the circle in space's questions, asked of the rim as <see cref="Arc3"/> asks them of a circle: the rim
    /// is what counts, not the disc, so a segment through the middle of the ellipse that never reaches the rim crosses
    /// nothing. None of it is sampled. Two readings do all the work, as for the circle.
    /// </para>
    /// <para>
    /// <b>In the plane.</b> Where the other shape shares the ellipse's plane, it is laid out in the ellipse's own frame
    /// and <see cref="Ellipse2"/> answers: a segment edge by edge, a circle, an arc or another ellipse as a curve. Whether
    /// the two share a plane is the circle's test: the rim stands off the other plane by no more than the planar
    /// tolerance, and the centre lies on it.
    /// </para>
    /// <para>
    /// <b>Not in the plane.</b> Two planes meet in a line, and every point the rim shares with anything lying in the other
    /// plane is on that line, where the rim is crossed in closed form; the points the other shape holds are kept. A
    /// segment or a ray not lying in the plane meets it at one point, kept when it is on the rim.
    /// </para>
    /// <para>
    /// The order is the circle's: along the line the two planes share, along a segment or a ray, edge by edge for a flat
    /// shape, face by face for a body, and in the plane along the line square to the one through the two centres, as two
    /// circles' crossings lie along their radical line. A place two faces or two edges share is named once.
    /// </para>
    /// </remarks>
    public static partial class Ellipse3
    {
        #region Against a plane

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a plane, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPlane3 plane) => GetIntersections(ellipse, plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance: a rim standing off the plane by no more than its planar tolerance lies in it.</param>
        /// <returns>
        /// At most two points, in order along the line the two planes share. The ellipse 300 by 100 lying in the plan about
        /// the origin, its major axis along X, crosses the plane x = 150 at (150, -86.603, 0), then at (150, 86.603, 0). An
        /// ellipse lying in the plane crosses it nowhere and gives none, which
        /// <see cref="CollidesWith(GeoEllipse3, GeoPlane3, Tolerance)"/> reports instead.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, plane, tolerance))
            {
                return new GeoPoint3[0];
            }

            return PointsOnPlane(ellipse, plane, tolerance).ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a plane, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPlane3 plane, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, plane, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the plane; otherwise, false, an ellipse lying in it included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, plane, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a segment and a ray

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a segment, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoLine3 line) => GetIntersections(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance: a segment whose ends both lie within its planar tolerance of the plane lies in it.</param>
        /// <returns>
        /// At most two points, in order along the segment, and only ones the segment reaches. A segment lying in the plane is
        /// read in it: the one from (-400, 50, 0) to (400, 50, 0) crosses the ellipse 300 by 100 lying in the plan about the
        /// origin at (-259.808, 50, 0), then at (259.808, 50, 0). Any other segment meets the plane at one point, which counts
        /// when it is on the rim: the one from (300, 0, -10) to (300, 0, 10) gives (300, 0, 0), and one through the middle of
        /// the disc gives none.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoLine3 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnLine(ellipse, line.StartPoint, line.Direction, 1.0, tolerance))
            {
                if (Containment3.IsPointOn(line, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a segment, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment crosses or touches the rim; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a ray, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoRay3 ray) => GetIntersections(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// At most two points, in order along the ray, and only ones ahead of where it starts. The ray from the origin along
        /// X, in the plane of the ellipse 300 by 100 lying in the plan about the origin, crosses it at (300, 0, 0) only.
        /// </returns>
        /// <remarks>
        /// A ray lies in the plane when it does as far as it can reach the rim: its start, and the point past which it is
        /// further from the centre than the major radius, both within the planar tolerance of the plane.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoRay3 ray, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();
            GeoVector3 direction = ray.Direction;
            double speed = direction.LengthSquared;
            double reach = 0.0;

            if (speed > 0.0)
            {
                double nearest = ray.Origin.GetVectorTo(ellipse.Center).DotProduct(direction) / speed;
                reach = Math.Max(0.0, nearest + (ellipse.MajorRadius + tolerance.EqualPoint) / Math.Sqrt(speed));
            }

            foreach (GeoPoint3 candidate in PointsOnLine(ellipse, ray.Origin, direction, reach, tolerance))
            {
                if (Containment3.IsPointOn(ray, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a ray, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ray crosses or touches the rim; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, ray, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a curve

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses an arc, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoArc3 arc) => GetIntersections(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance: two crossings no further apart than twice its point tolerance are one.</param>
        /// <returns>
        /// The points of the arc on the rim, as the circle in space gives them against an arc, read from the arc: in order
        /// along the line the two planes share, or, in one plane, along the line square to the one from the arc's centre to
        /// the ellipse's. The half circle of radius 50 about (250, 0, 0), normal along Y, from (250, 0, 50) round through
        /// (300, 0, 0) to (250, 0, -50), meets the ellipse 300 by 100 lying in the plan about the origin at (300, 0, 0) only;
        /// the other half meets it nowhere.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoArc3 arc, Tolerance tolerance)
        {
            if (Arc3.IsCoplanar(arc, ellipse.GetPlane(), tolerance))
            {
                GeoPoint2[] onCircle = Ellipse2.GetIntersections(Flat(ellipse), LayOut(ellipse, arc.Center, arc.Radius), tolerance);

                return AlongRadicalLine(Kept(ellipse, onCircle, arc.IsPointOn, tolerance), arc.Normal, arc.Center, ellipse.Center, tolerance);
            }

            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(arc, ellipse, tolerance))
            {
                if (OnRim(ellipse, candidate, tolerance))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses an arc, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoArc3 arc, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arc crosses or touches the rim; otherwise, false, an arc lying on the rim included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a circle, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoCircle3 circle) => GetIntersections(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance: rims that come within its point tolerance of each other touch, and two crossings no further apart than twice it are one.</param>
        /// <returns>
        /// The points the two rims share: in order along the line the two planes share, or, in one plane, along the line
        /// square to the one from the ellipse's centre to the circle's, as two circles' lie along their radical line; with
        /// one centre on the other, in the order of the eccentric angle. The circle of radius 150 about the origin crosses the
        /// ellipse 300 by 100 lying in the plan about it at four points, the first at (118.585, 91.856, 0); the circle of
        /// radius 50 about (250, 0, 0) standing upright on the X axis meets it at (300, 0, 0) only. None when the two draw
        /// the same curve.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoCircle3 circle, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, circle.GetPlane(), tolerance))
            {
                GeoPoint2[] points = Ellipse2.GetIntersections(Flat(ellipse), LayOut(ellipse, circle.Center, circle.Radius), tolerance);
                GeoArc3 laidOver = Arc3.AsArc(circle);

                return AlongRadicalLine(Kept(ellipse, points, laidOver.IsPointOn, tolerance), ellipse.Normal, ellipse.Center, circle.Center, tolerance);
            }

            GeoArc3 rim = Arc3.AsArc(circle);
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(ellipse, circle.GetPlane(), tolerance))
            {
                if (rim.IsPointOn(candidate, tolerance))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a circle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoCircle3 circle, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rims cross or touch; otherwise, false, for one lying on the other included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rims of two ellipses in space cross, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEllipse3 other) => GetIntersections(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rims of two ellipses in space cross, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse, whose plane and centre order the points.</param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="tolerance">The tolerance: rims that come within its point tolerance of each other touch, and two crossings no further apart than twice it are one.</param>
        /// <returns>
        /// The points the two rims share, in the order the circle's crossings of a circle come in. The ellipse 300 by 100
        /// lying in the plan about the origin and the same ellipse turned a quarter cross at (94.868, 94.868, 0) first, then
        /// at the three points mirrored from it. None when the two are the same ellipse.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEllipse3 other, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, other.GetPlane(), tolerance))
            {
                GeoPoint2[] points = Ellipse2.GetIntersections(Flat(ellipse), LayOut(ellipse, other), tolerance);

                return AlongRadicalLine(Kept(ellipse, points, (point, within) => OnRim(other, point, within), tolerance), ellipse.Normal, ellipse.Center, other.Center, tolerance);
            }

            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(ellipse, other.GetPlane(), tolerance))
            {
                if (OnRim(other, candidate, tolerance))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rims of two ellipses in space cross, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEllipse3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rims of two ellipses in space cross, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rims cross or touch; otherwise, false, for the same ellipse included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEllipse3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, other, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a flat region

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a triangle, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoTriangle3 triangle) => GetIntersections(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim pierces the triangle, the points of the triangle it goes through, in order along the line the two
        /// planes share; where the triangle lies in the ellipse's plane, the points where the rim cuts one of its three edges,
        /// edge by edge. The triangle (150, 0, -10), (150, 200, -10), (150, 0, 10), standing upright on the line x = 150, is
        /// pierced by the ellipse 300 by 100 lying in the plan about the origin at (150, 86.603, 0) only.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoTriangle3 triangle, Tolerance tolerance)
        {
            GeoPlane3 plane = triangle.GetPlane();

            if (!IsCoplanar(ellipse, plane, tolerance))
            {
                return PiercingPoints(ellipse, plane, point => Containment3.Contains(triangle, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, ellipse, new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) }, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a triangle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the triangle; otherwise, false, an ellipse lying wholly within it included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygon3 polygon) => GetIntersections(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim pierces the polygon, the points of the region it goes through; where the polygon lies in the
        /// ellipse's plane, the points where the rim cuts the outline, edge by edge. The square 800 by 800 about the origin in
        /// the plan is cut by the ellipse 300 by 100 lying about the same centre at none of its edges; the rectangle 200 by
        /// 300, its first edge along y = -150, at four points, (100, -94.281, 0) on its second edge first.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoPlane3 plane = polygon.GetPlane();

            if (!IsCoplanar(ellipse, plane, tolerance))
            {
                return PiercingPoints(ellipse, plane, point => Containment3.Contains(polygon, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, ellipse, polygon.GetEdges(), tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a polygon, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the polygon; otherwise, false, an ellipse lying wholly within it included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoFace3 face) => GetIntersections(ellipse, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim pierces the face, the points of its material it goes through, so a rim passing down a hole reaches
        /// nothing; where the face lies in the ellipse's plane, the points where the rim cuts the outline, then the rim of each
        /// hole.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPlane3 plane = face.GetPlane();

            if (!IsCoplanar(ellipse, plane, tolerance))
            {
                return PiercingPoints(ellipse, plane, point => Containment3.Contains(face, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, ellipse, face.Boundary.GetEdges(), tolerance);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                AddCrossingsOfEdges(found, ellipse, hole.GetEdges(), tolerance);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a face, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the material of the face; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, face, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a body or a box

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoSolid3 solid) => GetIntersections(ellipse, solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim goes in and where it comes out, face by face, the walls of any opening counted as surface and a
        /// place two faces share named once. The ellipse 300 by 100 lying in the plan about the centre of a box 200 by 300 by
        /// 200 leaves it through the two faces square to its major axis, at (±100, ±94.281, 0). A rim lying wholly inside the
        /// material crosses nothing and gives none, which <see cref="CollidesWith(GeoEllipse3, GeoSolid3, Tolerance)"/>
        /// reports instead.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfBody(found, ellipse, solid, solid, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the surface; otherwise, false, a rim wholly inside included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an oriented box, using the default
        /// tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoObb3 box) => GetIntersections(ellipse, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The points where the rim meets one of the six faces, face by face, each corner or edge counted once: the ellipse
        /// 300 by 100 lying in the plan about the centre of a box 200 by 300 by 200 meets it at (±100, ±94.281, 0).
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoObb3 box, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPolygon3 face in box.GetFaces())
            {
                foreach (GeoPoint3 candidate in GetIntersections(ellipse, face, tolerance))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an oriented box, using the default
        /// tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches a face of the box; otherwise, false, a rim wholly inside included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an axis-aligned box, using the
        /// default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoAabb3 box) => GetIntersections(ellipse, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an axis-aligned box, within a
        /// tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box; an empty one holds nothing and is crossed nowhere.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The points the oriented box with the world's axes gives: the box 200 by 300 by 200 about the origin meets the ellipse 300 by 100 lying in the plan about it at (±100, ±94.281, 0).</returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(ellipse, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an axis-aligned box, using the default
        /// tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches a face of the box; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, box, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against an edge and a curved chain

        /// <summary>
        /// Gets every point where an edge crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEdge3 edge) => GetIntersections(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses the rim of an ellipse in space, within a tolerance: as a segment or as an
        /// arc, whichever the edge is.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(ellipse, edge.ToArc(), tolerance)
                : GetIntersections(ellipse, edge.ToLine(), tolerance);

        /// <summary>
        /// Tries to find where an edge crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEdge3 edge, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an edge crosses the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
            => edge.IsArc
                ? TryIntersectWith(ellipse, edge.ToArc(), out intersections, tolerance)
                : TryIntersectWith(ellipse, edge.ToLine(), out intersections, tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolylineArc3 chain) => GetIntersections(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>What each edge gives, edge by edge in the chain's order, a corner two edges share named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return CrossingsOfEdges(ellipse, chain.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets every point where a curved loop crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygonArc3 loop) => GetIntersections(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>What each edge gives, edge by edge in the loop's order, a corner two edges share named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return CrossingsOfEdges(ellipse, loop.GetEdges(), tolerance);
        }

        #endregion

        #region Crossing helpers

        /// <summary>
        /// Determines whether a plane is the very plane the ellipse lies in.
        /// </summary>
        /// <remarks>
        /// As the circle's test (<see cref="Arc3"/>): the rim stands off a plane through its centre by no more than the
        /// planar tolerance, and the centre lies on the plane. The rim stands off by up to the root of (a n·U)² + (b n·V)²,
        /// where n is the plane's normal: for a circle, its radius times the sine of the angle between the planes.
        /// </remarks>
        internal static bool IsCoplanar(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            GeoVector3 normal = plane.Normal;
            double major = ellipse.MajorRadius * ellipse.MajorAxis.DotProduct(normal);
            double minor = ellipse.MinorRadius * ellipse.MinorAxis.DotProduct(normal);

            return Math.Sqrt(major * major + minor * minor) <= tolerance.EqualPlanar
                && Containment3.IsPointOn(plane, ellipse.Center, tolerance);
        }

        /// <summary>
        /// Determines whether a point lies within the point tolerance of the rim, measured in space, as an arc measures it.
        /// </summary>
        private static bool OnRim(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            if (!(Math.Abs(height) <= tolerance.EqualPoint))
            {
                return false;
            }

            GeoEllipse2 flat = Flat(ellipse);

            if (Math.Abs(Ellipse2.Below(flat, along, across)) > Ellipse2.Slack(flat, along, across, tolerance))
            {
                return false;
            }

            double gap = Ellipse2.Gap(flat, along, across);

            return Math.Sqrt(height * height + gap * gap) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets the point of the rim where the eccentric angle starts, the end of the major axis: the circle's start.
        /// </summary>
        private static GeoPoint3 StartOf(GeoEllipse3 ellipse) => Lift(ellipse, new GeoPoint2(ellipse.MajorRadius, 0.0));

        /// <summary>
        /// Gets the line where the plane of a shape meets another plane, found from the shape's centre: its direction the
        /// first normal crossed with the second, its point the one of it nearest the centre.
        /// </summary>
        /// <param name="center">The centre of the shape lying in the first plane.</param>
        /// <param name="normal">The unit normal of the first plane.</param>
        /// <param name="reach">How far the shape reaches across its plane, its diameter.</param>
        /// <param name="plane">The other plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="offset">The point of the line nearest the centre, as an offset from it, lying in the first plane.</param>
        /// <param name="direction">The unit direction of the line.</param>
        /// <param name="rounding">How far rounding may have moved the line, a bound: a few parts in 1E16 of the distances it was found from.</param>
        /// <returns>false when the two planes are parallel as far as the shape reaches, as for the circle.</returns>
        /// <remarks>
        /// <see cref="Intersection3.TryGetMeetingLine(GeoPlane3, GeoPlane3, double, out GeoRay3, Tolerance)"/> finds the
        /// line from the planes' distances to the world origin, which planes a thousandth of a degree apart and 52 000 from
        /// the origin turn into an error of a millimetre. Here the only distance is the centre's from the other plane,
        /// measured from that plane's own point, and the rounding left is that distance's, divided by the sine of the
        /// angle between the planes.
        /// </remarks>
        private static bool TryMeetFrom(GeoPoint3 center, GeoVector3 normal, double reach, GeoPlane3 plane, Tolerance tolerance, out GeoVector3 offset, out GeoVector3 direction, out double rounding)
        {
            GeoVector3 other = plane.Normal;
            GeoVector3 cross = normal.CrossProduct(other);
            double sine = cross.Length;

            offset = default(GeoVector3);
            direction = default(GeoVector3);
            rounding = 0.0;

            if (sine * reach <= tolerance.EqualPlanar)
            {
                return false;
            }

            // The point C + beta (n2 - (n1.n2) n1) lies in the first plane, and in the second where beta sin^2 is minus
            // the centre's height above it.
            double beta = -plane.SignedDistanceTo(center) / (sine * sine);
            double cosine = normal.DotProduct(other);

            offset = other.Subtract(normal.Multiply(cosine)).Multiply(beta);
            direction = cross.Multiply(1.0 / sine);
            rounding = 1E-15 * (plane.Origin.DistanceTo(center) / sine + reach + offset.Length);

            return true;
        }

        /// <summary>
        /// Gets the points of the rim on the line where its plane meets another plane, in order along the line.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The workhorse of every pair not in one plane, as it is for the circle. The line is found from the ellipse's
        /// centre and read in the ellipse's own frame as the line u n_u + v n_v = p, n its unit normal there. With
        /// h = √(a² n_u² + b² n_v²), how far the rim reaches along n, the line cuts a chord whose half is
        /// (a b / h²) √(h² - p²) in drawing units; for a circle, √(r² - p²).
        /// </para>
        /// <para>
        /// A line grazing the rim is judged as the circle judges one, by four times the square of that half chord,
        /// D = 4 (a b)² (h² - p²) / h⁴, negative for a line passing outside: below minus the square of the point
        /// tolerance it misses; within that square either way it touches, at the middle of the chord; above it, it cuts at
        /// two points. For a circle D is the circle's own discriminant and the bounds are the circle's. Judged instead by
        /// how deep the chord cuts, as a segment lying in the plane is, a chord 1.66 long cutting 0.0008 into an ellipse of
        /// radius 431 was one point at its middle, 0.8 from either true crossing, and a circle standing on one of them was
        /// found to cross the ellipse nowhere. Judged in the frame scaled to the unit circle, the bound is stretched by a / b.
        /// </para>
        /// </remarks>
        private static List<GeoPoint3> PointsOnPlane(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>(2);
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;

            if (!TryMeetFrom(ellipse.Center, ellipse.Normal, 2.0 * a, plane, tolerance, out GeoVector3 offset, out GeoVector3 direction, out double rounding))
            {
                return found;
            }

            GeoVector3 major = ellipse.MajorAxis;
            GeoVector3 minor = ellipse.MinorAxis;
            double u0 = offset.DotProduct(major);
            double v0 = offset.DotProduct(minor);
            double du = direction.DotProduct(major);
            double dv = direction.DotProduct(minor);

            // The unit normal of the line in the plane, and how far the line and the rim reach along it.
            double speed = Math.Sqrt(du * du + dv * dv);
            double nu = -dv / speed;
            double nv = du / speed;
            double p = Math.Abs(u0 * nu + v0 * nv);
            double h = Math.Sqrt(a * a * nu * nu + b * b * nv * nv);
            double ab = a * b;
            double chord = 4.0 * ab * ab * ((h - p) * (h + p)) / (h * h * h * h);

            // The circle's band, widened by what rounding can do to D: where the rim runs flat, at the ends of the minor axis
            // of a thin ellipse, D grows by 8 (a b)^2 / h^3 for each unit the line moves, and on an ellipse 816 by 1.07 a line
            // moved by its rounding alone cut a chord past the tolerance where it touched.
            double graze = tolerance.EqualPoint * tolerance.EqualPoint + 8.0 * ab * ab * (p + h) / (h * h * h * h) * rounding;

            if (chord < -graze)
            {
                return found;
            }

            // The middle of the chord, where the line comes nearest the centre in the frame scaled to the unit circle.
            double middle = -(u0 * du / (a * a) + v0 * dv / (b * b)) / (du * du / (a * a) + dv * dv / (b * b));
            GeoPoint3 foot = Lift(ellipse, new GeoPoint2(u0, v0));

            if (chord <= graze)
            {
                found.Add(foot.Add(direction.Multiply(middle)));

                return found;
            }

            double half = 0.5 * Math.Sqrt(chord) / speed;

            found.Add(foot.Add(direction.Multiply(middle - half)));
            found.Add(foot.Add(direction.Multiply(middle + half)));

            return found;
        }

        /// <summary>
        /// Gets the points of an arc on the line where its plane meets the ellipse's, the line found from the arc's centre
        /// and the points read off it as <see cref="Arc3"/> reads them.
        /// </summary>
        private static List<GeoPoint3> PointsOnPlane(GeoArc3 arc, GeoEllipse3 ellipse, Tolerance tolerance)
        {
            if (!TryMeetFrom(arc.Center, arc.Normal, 2.0 * arc.Radius, ellipse.GetPlane(), tolerance, out GeoVector3 offset, out GeoVector3 direction, out _))
            {
                return new List<GeoPoint3>();
            }

            return Arc3.PointsOnLine(arc, arc.Center.Add(offset), direction, tolerance);
        }

        /// <summary>
        /// Gets the points of the rim on a segment, the segment read in the plane.
        /// </summary>
        /// <remarks>
        /// The segment is laid out in the ellipse's own frame and <see cref="Ellipse2"/> finds where it crosses the rim,
        /// touching it where it passes within the point tolerance. Each answer is put back on the segment itself, at the same
        /// place along it, as the circle's answers are points of the segment, and kept where it lies within the point
        /// tolerance of the rim in space.
        /// </remarks>
        private static List<GeoPoint3> AlongSegment(GeoEllipse3 ellipse, GeoPoint3 start, GeoVector3 direction, Tolerance tolerance)
        {
            Frame(ellipse, start, out double along, out double across, out _);

            double alongStep = direction.DotProduct(ellipse.MajorAxis);
            double acrossStep = direction.DotProduct(ellipse.MinorAxis);
            var laidOut = new GeoLine2(new GeoPoint2(along, across), new GeoPoint2(along + alongStep, across + acrossStep));

            GeoPoint2[] points = Ellipse2.GetIntersections(Flat(ellipse), laidOut, tolerance);
            var found = new List<GeoPoint3>(points.Length);
            double speed = alongStep * alongStep + acrossStep * acrossStep;

            foreach (GeoPoint2 point in points)
            {
                double at = speed > 0.0 ? ((point.X - along) * alongStep + (point.Y - across) * acrossStep) / speed : 0.0;
                GeoPoint3 onSegment = start.Add(direction.Multiply(at));

                // A segment lying in the plane may still stand off it by up to the planar tolerance, and the rim is asked in
                // space, as an arc asks.
                if (OnRim(ellipse, onSegment, tolerance))
                {
                    found.Add(onSegment);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets the candidates a segment or a ray gives against the rim: origin + t·direction for t from nought to a span.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A stretch whose two ends both lie within the planar tolerance of the plane lies in it, and is read there by
        /// <see cref="Ellipse2"/>. Any other meets the plane at one point, which counts when it is on the rim. A stretch
        /// wholly to one side of the plane, beyond the planar tolerance, gives nothing.
        /// </para>
        /// <para>
        /// The caller still asks its own shape whether it reaches each candidate, as the circle's do.
        /// </para>
        /// </remarks>
        private static List<GeoPoint3> PointsOnLine(GeoEllipse3 ellipse, GeoPoint3 origin, GeoVector3 direction, double span, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            if (direction.LengthSquared <= tolerance.EqualVector * tolerance.EqualVector)
            {
                // No direction to follow. The one point there is counts if the rim reaches it.
                if (OnRim(ellipse, origin, tolerance))
                {
                    found.Add(origin);
                }

                return found;
            }

            double start = ellipse.Center.GetVectorTo(origin).DotProduct(ellipse.Normal);
            double rise = direction.DotProduct(ellipse.Normal);
            double end = start + span * rise;
            double band = tolerance.EqualPlanar;

            if (Math.Abs(start) <= band && Math.Abs(end) <= band)
            {
                return AlongSegment(ellipse, origin, direction.Multiply(span), tolerance);
            }

            if ((start > band && end > band) || (start < -band && end < -band))
            {
                return found;
            }

            // One end beyond the band and the other not, or the two either side: the stretch rises, so it pierces.
            GeoPoint3 pierce = origin.Add(direction.Multiply(-start / rise));

            if (OnRim(ellipse, pierce, tolerance))
            {
                found.Add(pierce);
            }

            return found;
        }

        /// <summary>
        /// Gets the points where the rim pierces a flat region, the region's plane not the ellipse's.
        /// </summary>
        private static List<GeoPoint3> PiercingPoints(GeoEllipse3 ellipse, GeoPlane3 plane, Func<GeoPoint3, bool> holds, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(ellipse, plane, tolerance))
            {
                if (holds(candidate))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets the points where the rim cuts a run of edges lying in its plane, each read there.
        /// </summary>
        private static void AddCrossingsOfEdges(List<GeoPoint3> found, GeoEllipse3 ellipse, IEnumerable<GeoLine3> edges, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                foreach (GeoPoint3 crossing in AlongSegment(ellipse, edge.StartPoint, edge.Direction, tolerance))
                {
                    // Two edges share a corner, so a rim through a corner is found twice.
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        /// <summary>
        /// Gathers the points where the rim meets the boundary of a body, openings included, as an arc's are gathered.
        /// </summary>
        private static void AddCrossingsOfBody(List<GeoPoint3> found, GeoEllipse3 ellipse, GeoSolid3 body, GeoSolid3 part, Tolerance tolerance)
        {
            foreach (GeoFace3 face in part.Faces)
            {
                foreach (GeoPoint3 candidate in GetIntersections(ellipse, face, tolerance))
                {
                    if (Containment3.Locate(body, candidate, tolerance) == PointLocation.OnSide)
                    {
                        Arc3.AddOnce(found, candidate, tolerance);
                    }
                }
            }

            foreach (GeoSolid3 opening in part.Openings)
            {
                AddCrossingsOfBody(found, ellipse, body, opening, tolerance);
            }
        }

        /// <summary>
        /// Gathers what each edge of a curved chain or loop gives, naming each place once.
        /// </summary>
        private static GeoPoint3[] CrossingsOfEdges(GeoEllipse3 ellipse, IEnumerable<GeoEdge3> edges, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoEdge3 edge in edges)
            {
                foreach (GeoPoint3 crossing in GetIntersections(ellipse, edge, tolerance))
                {
                    // Two edges share a corner, so a crossing at a corner is found by both of them.
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Lays a circle of the ellipse's plane out in the ellipse's own frame.
        /// </summary>
        private static GeoCircle2 LayOut(GeoEllipse3 ellipse, GeoPoint3 center, double radius)
        {
            Frame(ellipse, center, out double along, out double across, out _);

            return new GeoCircle2(new GeoPoint2(along, across), radius);
        }

        /// <summary>
        /// Lays another ellipse of the ellipse's plane out in the ellipse's own frame, its radii kept and its axis read in
        /// the plane.
        /// </summary>
        private static GeoEllipse2 LayOut(GeoEllipse3 ellipse, GeoEllipse3 other)
        {
            Frame(ellipse, other.Center, out double along, out double across, out _);

            var axis = new GeoVector2(other.MajorAxis.DotProduct(ellipse.MajorAxis), other.MajorAxis.DotProduct(ellipse.MinorAxis));

            return new GeoEllipse2(new GeoPoint2(along, across), axis, other.MajorRadius, other.MinorRadius);
        }

        /// <summary>
        /// Puts the points found in the plane back into space, keeping the ones the other curve holds there and naming each
        /// place once.
        /// </summary>
        /// <remarks>
        /// The other curve is laid out flat to be read in the plane, but it may stand off it by up to the planar tolerance:
        /// a point the two share in the plane counts only where the curve itself, in space, passes within the point
        /// tolerance of it, as the circle keeps only what the other circle holds.
        /// </remarks>
        private static List<GeoPoint3> Kept(GeoEllipse3 ellipse, GeoPoint2[] points, Func<GeoPoint3, Tolerance, bool> holds, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>(points.Length);

            foreach (GeoPoint3 point in Lift(ellipse, points))
            {
                if (holds(point, tolerance))
                {
                    Arc3.AddOnce(found, point, tolerance);
                }
            }

            return found;
        }

        /// <summary>
        /// Orders the points two curves of one plane share as the circle orders two circles' crossings: along their radical
        /// line, the first curve's normal crossed with the line from its centre to the other's.
        /// </summary>
        /// <remarks>
        /// Where the centres are one, within the point tolerance, there is no such line and the points keep the order of the
        /// eccentric angle they came in. Ties keep the order they came in too, so the result never hangs on rounding alone.
        /// </remarks>
        private static GeoPoint3[] AlongRadicalLine(List<GeoPoint3> points, GeoVector3 normal, GeoPoint3 from, GeoPoint3 to, Tolerance tolerance)
        {
            GeoVector3 between = from.GetVectorTo(to);

            if (points.Count < 2 || between.Length <= tolerance.EqualPoint)
            {
                return points.ToArray();
            }

            GeoVector3 across = normal.CrossProduct(between);
            var keys = new double[points.Count];
            var order = new int[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                keys[i] = from.GetVectorTo(points[i]).DotProduct(across);
                order[i] = i;
            }

            Array.Sort(order, (x, y) =>
            {
                int byKey = keys[x].CompareTo(keys[y]);

                return byKey != 0 ? byKey : x.CompareTo(y);
            });

            var sorted = new GeoPoint3[points.Count];

            for (int i = 0; i < order.Length; i++)
            {
                sorted[i] = points[order[i]];
            }

            return sorted;
        }

        #endregion
    }
}
