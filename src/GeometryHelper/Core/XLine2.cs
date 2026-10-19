using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How an infinite line in the plane is cut into a segment to be asked of a shape with an end to it, and where a point,
    /// a ray or another line meets it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cut runs from the point at N - m to the point at F + m along the line, where N and F are the least and the
    /// greatest distance along it at which any point of the shape projects, and m = max(1, 100 · EqualPoint) is the margin.
    /// For a straight shape N and F are taken over its corners; for a circle, an arc or an ellipse they are the centre
    /// brought onto the line less and plus the radius, or the major radius. The origin of the line is not part of the cut:
    /// a shape a million along it is asked as the stretch round the shape.
    /// </para>
    /// <para>
    /// Every point of the shape projects between N and F, so every point of the line outside the cut is further from every
    /// point of the shape than the nearer end of the cut is. This is the ray's reasoning (see <see cref="Ray2"/>) with an end
    /// to cut at on both sides, and its faults are the ray's: where the segment code answers rightly, the ends of the cut are
    /// never a crossing, a touching point or an end of a shortest segment, and the cut answers what the line would; where it
    /// does not, the line shares the fault. A NaN anywhere is carried into the cut, and a line of no direction, a
    /// <c>default</c> one, is cut to the single point of its origin.
    /// </para>
    /// </remarks>
    internal static class XLine2
    {
        /// <summary>
        /// The sine below which two directions of unit length are parallel by rounding alone: a crossing a micrometre off
        /// would lie a million kilometres along.
        /// </summary>
        private const double RoundingSine = 1E-15;

        /// <summary>
        /// The relative rounding of a double: the gap between one and the next number above it.
        /// </summary>
        private const double Epsilon = 2.2204460492503131E-16;

        /// <summary>
        /// The sine up to which two lines may be parallel by rounding alone; above it they are at a real angle and cross.
        /// </summary>
        private const double NearlyParallel = 1E-9;

        /// <summary>
        /// Cuts a line from before the nearest to past the farthest distance along it.
        /// </summary>
        /// <param name="xline">The line.</param>
        /// <param name="nearest">A lower bound of how far along the line any point of the other shape projects.</param>
        /// <param name="farthest">An upper bound of how far along the line any point of the other shape projects.</param>
        /// <param name="tolerance">The tolerance whose point tolerance sets the margin outside the bounds.</param>
        /// <returns>The segment from the point at the nearest less the margin to the point at the farthest plus it.</returns>
        internal static GeoLine2 CutPast(GeoXLine2 xline, double nearest, double farthest, Tolerance tolerance)
        {
            double margin = Math.Max(1.0, 100.0 * tolerance.EqualPoint);

            return new GeoLine2(xline.GetPointAtDistance(nearest - margin), xline.GetPointAtDistance(farthest + margin));
        }

        /// <summary>
        /// Cuts a line past a segment.
        /// </summary>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoLine2 line, Tolerance tolerance)
        {
            double first = xline.GetDistanceAtPoint(line.StartPoint);
            double second = xline.GetDistanceAtPoint(line.EndPoint);

            return CutPast(xline, Math.Min(first, second), Math.Max(first, second), tolerance);
        }

        /// <summary>
        /// Cuts a line past an arc: its two ends, and the extreme of the circle along the line where the arc holds it.
        /// </summary>
        /// <remarks>
        /// Not the whole of its circle. An arc that is nearly straight lies on a circle of a radius of 1E11, and a cut that
        /// reached past that circle would be 1E11 long, with no digit left to tell the shape from its neighbours.
        /// </remarks>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoArc2 arc, Tolerance tolerance)
        {
            double first = xline.GetDistanceAtPoint(arc.StartPoint);
            double second = xline.GetDistanceAtPoint(arc.EndPoint);
            double nearest = Math.Min(first, second);
            double farthest = Math.Max(first, second);

            ArcReach(xline, arc, ref nearest, ref farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a circle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoCircle2 circle, Tolerance tolerance)
            => CutPastRound(xline, circle.Center, circle.Radius, tolerance);

        /// <summary>
        /// Cuts a line past an ellipse, read by the circle of its major radius.
        /// </summary>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoEllipse2 ellipse, Tolerance tolerance)
            => CutPastRound(xline, ellipse.Center, ellipse.MajorRadius, tolerance);

        /// <summary>
        /// Cuts a line past a rectangle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoRectangle2 rect, Tolerance tolerance)
        {
            Extent(xline, rect.GetVertices(), out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a triangle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoTriangle2 triangle, Tolerance tolerance)
        {
            double a = xline.GetDistanceAtPoint(triangle.A);
            double b = xline.GetDistanceAtPoint(triangle.B);
            double c = xline.GetDistanceAtPoint(triangle.C);

            return CutPast(xline, Math.Min(Math.Min(a, b), c), Math.Max(Math.Max(a, b), c), tolerance);
        }

        /// <summary>
        /// Cuts a line past a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            Extent(xline, poly.Vertices, out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            Extent(xline, polyline.Vertices, out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a face: its outline, and its holes too, though a hole lies within the outline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoFace2 face, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            Extent(xline, face.Boundary.Vertices, out double nearest, out double farthest);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                Extent(xline, hole.Vertices, out double holeNearest, out double holeFarthest);
                nearest = Math.Min(nearest, holeNearest);
                farthest = Math.Max(farthest, holeFarthest);
            }

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a curved loop: its corners, and the extreme along the line of every arc in it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            Extent(xline, loop.Vertices, out double nearest, out double farthest);

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                GeoEdge2 edge = loop.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.ToArc();
                    ArcReach(xline, arc, ref nearest, ref farthest);
                }
            }

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a curved chain: its corners, and the extreme along the line of every arc in it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        internal static GeoLine2 CutPast(GeoXLine2 xline, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            Extent(xline, chain.Vertices, out double nearest, out double farthest);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                GeoEdge2 edge = chain.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.ToArc();
                    ArcReach(xline, arc, ref nearest, ref farthest);
                }
            }

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Whether one line comes before another, by origin and then by direction: the order that lets a question about two
        /// lines be worked out once, from the one that comes first, whichever way round it is asked.
        /// </summary>
        internal static bool Precedes(GeoXLine2 first, GeoXLine2 second)
        {
            if (first.Origin.X != second.Origin.X) return first.Origin.X < second.Origin.X;
            if (first.Origin.Y != second.Origin.Y) return first.Origin.Y < second.Origin.Y;
            if (first.Direction.X != second.Direction.X) return first.Direction.X < second.Direction.X;

            return first.Direction.Y < second.Direction.Y;
        }

        /// <summary>
        /// Gets the point of a line nearest a point: its foot, the projection on the line.
        /// </summary>
        internal static GeoPoint2 Project(GeoXLine2 xline, GeoPoint2 point)
            => xline.GetPointAtDistance(xline.GetDistanceAtPoint(point));

        /// <summary>
        /// Finds where a line and a ray cross, at any angle, and how far along each.
        /// </summary>
        /// <param name="xline">The line.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance whose point tolerance is the slack behind the origin of the ray where it is asked for.</param>
        /// <param name="pastOrigin">Whether a crossing up to the point tolerance behind the origin of the ray counts.</param>
        /// <param name="alongLine">How far along the line the carriers cross.</param>
        /// <param name="alongRay">How far along the ray the carriers cross.</param>
        /// <remarks>
        /// Two directions cross unless they are parallel by rounding: a sine below 1E-15, or a sine of 1E-9 or less with a
        /// crossing further out than a double can place within the point tolerance (see <see cref="Placeable"/>); a NaN has none either. This asks where
        /// the two are, not whether the crossing is well placed: the line along X and the ray from (50, 30) along
        /// (1, -0.0000001) cross 300 million along and are nought apart, though
        /// <see cref="Intersection2.TryIntersectWith(GeoRay2, GeoXLine2, out GeoPoint2, Tolerance)"/> calls them parallel.
        /// </remarks>
        internal static bool TryCross(GeoXLine2 xline, GeoRay2 ray, Tolerance tolerance, bool pastOrigin, out double alongLine, out double alongRay)
        {
            alongLine = 0.0;
            alongRay = 0.0;

            // Both directions are of unit length, so their cross product is the sine of the angle between them.
            double sine = xline.Direction.CrossProduct(ray.Direction);

            // A sine this small is rounding, not an angle. Written as what has to hold, so that a NaN fails it.
            if (!(Math.Abs(sine) > RoundingSine))
            {
                return false;
            }

            GeoVector2 between = xline.Origin.GetVectorTo(ray.Origin);
            double t = between.CrossProduct(ray.Direction) / sine;
            double u = between.CrossProduct(xline.Direction) / sine;

            if (!Placeable(sine, Math.Max(Math.Abs(t), Math.Abs(u)), xline.Origin, ray.Origin, tolerance))
            {
                return false;
            }

            // Written as what has to hold, so that a NaN fails it.
            double slack = pastOrigin ? tolerance.EqualPoint : 0.0;

            if (!(u >= -slack))
            {
                return false;
            }

            alongLine = t;
            alongRay = u;
            return true;
        }

        /// <summary>
        /// Finds where two lines cross, at any angle, and how far along the first.
        /// </summary>
        /// <remarks>
        /// Two lines cross unless they are parallel by rounding: a sine below 1E-15, or a sine of 1E-9 or less with a crossing
        /// further out than a double can place within the point tolerance (see <see cref="Placeable"/>). A line rebuilt from two of the points of another,
        /// offset by 10, is a sine of 1E-14 off it at coordinates of 1E4, and crosses it 1E15 along; it is 10 from it, not
        /// nought. A NaN has no crossing either.
        /// </remarks>
        internal static bool TryCross(GeoXLine2 xline1, GeoXLine2 xline2, Tolerance tolerance, out double alongFirst)
        {
            alongFirst = 0.0;

            double sine = xline1.Direction.CrossProduct(xline2.Direction);

            if (!(Math.Abs(sine) > RoundingSine))
            {
                return false;
            }

            GeoVector2 between = xline1.Origin.GetVectorTo(xline2.Origin);
            double along = between.CrossProduct(xline2.Direction) / sine;
            double alongOther = between.CrossProduct(xline1.Direction) / sine;

            // An origin that is not a number places no crossing, and neither does one that cannot be placed, measured along the
            // line that is further from it, so that asked either way round the answer is the same. Written as what has to hold,
            // so that a NaN fails it.
            if (!Guard.IsFinite(along) || !Placeable(sine, Math.Max(Math.Abs(along), Math.Abs(alongOther)), xline1.Origin, xline2.Origin, tolerance))
            {
                return false;
            }

            alongFirst = along;
            return true;
        }

        /// <summary>
        /// Whether two lines that close to parallel have a crossing that can be placed: one that lies no further along than a
        /// double can put a point within the point tolerance, a point at that distance from a coordinate of that size being only
        /// known to a rounding of the larger.
        /// </summary>
        /// <remarks>
        /// The rule is for lines parallel by rounding, a sine of 1E-9 or less, such as a line rebuilt from two points of another;
        /// lines at any real angle cross, whatever the tolerance, even where it is 1E-12 or nought. With a point tolerance of a
        /// thousandth and coordinates near nought the limit is 4.5E12 along.
        /// </remarks>
        private static bool Placeable(double sine, double along, GeoPoint2 first, GeoPoint2 second, Tolerance tolerance)
        {
            if (Math.Abs(sine) > NearlyParallel)
            {
                return !double.IsNaN(along);
            }

            double reach = along + Math.Max(Math.Max(Math.Abs(first.X), Math.Abs(first.Y)), Math.Max(Math.Abs(second.X), Math.Abs(second.Y)));

            // Written as what has to hold, so that a NaN fails it.
            return reach * Epsilon <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets how far along a line the nearest and the farthest of a set of points project; a NaN among them is carried.
        /// </summary>
        private static void Extent(GeoXLine2 xline, IReadOnlyList<GeoPoint2> points, out double nearest, out double farthest)
        {
            nearest = double.PositiveInfinity;
            farthest = double.NegativeInfinity;

            if (points.Count == 0)
            {
                nearest = 0.0;
                farthest = 0.0;
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                double along = xline.GetDistanceAtPoint(points[i]);

                nearest = Math.Min(nearest, along);
                farthest = Math.Max(farthest, along);
            }
        }

        /// <summary>
        /// Cuts a line past a round shape: its centre brought onto the line, less and plus its radius.
        /// </summary>
        private static GeoLine2 CutPastRound(GeoXLine2 xline, GeoPoint2 center, double radius, Tolerance tolerance)
        {
            double along = xline.GetDistanceAtPoint(center);

            return CutPast(xline, along - radius, along + radius, tolerance);
        }

        /// <summary>
        /// Widens a stretch along a line to take in what an arc reaches between its ends: the point of its circle furthest
        /// ahead along the line, and the one furthest behind, each where the arc holds it.
        /// </summary>
        /// <remarks>
        /// The ends are the caller's: a loop and a chain have them as corners, and an arc made of a corner and a bulge of 1E-10
        /// has a centre a long way off that gives them only to within 1E-5.
        /// </remarks>
        private static void ArcReach(GeoXLine2 xline, GeoArc2 arc, ref double nearest, ref double farthest)
        {
            double along = xline.GetDistanceAtPoint(arc.Center);
            double heading = Math.Atan2(xline.Direction.Y, xline.Direction.X);

            if (Holds(arc, heading))
            {
                farthest = Math.Max(farthest, along + arc.Radius);
            }

            if (Holds(arc, heading + Math.PI))
            {
                nearest = Math.Min(nearest, along - arc.Radius);
            }
        }

        /// <summary>
        /// Whether an arc holds the point of its circle at an angle, reached from its start the way it sweeps.
        /// </summary>
        private static bool Holds(GeoArc2 arc, double angle)
        {
            double turn = 2.0 * Math.PI;
            double reached = (angle - arc.StartAngle) * Math.Sign(arc.SweptAngle);

            reached -= turn * Math.Floor(reached / turn);

            return reached <= Math.Abs(arc.SweptAngle);
        }
    }
}
