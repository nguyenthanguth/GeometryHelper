using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How a ray in the plane is cut into a segment to be asked of a shape with an end to it, and where a point falls on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cut runs from the origin to the point at reach R = max(0, t) + max(1, 100 · EqualPoint), where t is an upper
    /// bound of how far along the ray any point of the shape projects. For a straight shape t is taken over its corners;
    /// for a circle, an arc or an ellipse it is the centre brought onto the ray plus the radius, or the major radius.
    /// </para>
    /// <para>
    /// Every point of the shape projects to at most t, so every point of the ray past R is further from every point of
    /// the shape than the end of the cut is. Where the segment code answers rightly, the end of the cut is never a
    /// crossing, a touching point or an end of a shortest segment, and the cut answers what the ray would. Where it does
    /// not, the ray shares the fault: a segment starting just inside a circle's rim and running nearly along it misses
    /// where it leaves, and its shortest segment runs from its far end. A NaN anywhere is carried into the cut, and a ray
    /// of no direction, a <c>default</c> one, is cut to the single point of its origin.
    /// </para>
    /// </remarks>
    internal static class Ray2
    {
        /// <summary>
        /// The sine below which two directions of unit length are parallel by rounding alone: a crossing a micrometre off
        /// would lie a million kilometres along.
        /// </summary>
        private const double RoundingSine = 1E-15;

        /// <summary>
        /// Cuts a ray past the given reach along it.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="farthest">An upper bound of how far along the ray any point of the other shape projects.</param>
        /// <param name="tolerance">The tolerance whose point tolerance sets the margin past the reach.</param>
        /// <returns>The segment from the origin to the point at the reach plus the margin.</returns>
        internal static GeoLine2 CutPast(GeoRay2 ray, double farthest, Tolerance tolerance)
        {
            double reach = Math.Max(0.0, farthest) + Math.Max(1.0, 100.0 * tolerance.EqualPoint);

            return new GeoLine2(ray.Origin, ray.GetPointAtDistance(reach));
        }

        /// <summary>
        /// Cuts a ray past a segment.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoLine2 line, Tolerance tolerance)
            => CutPast(ray, Math.Max(ray.GetDistanceAtPoint(line.StartPoint), ray.GetDistanceAtPoint(line.EndPoint)), tolerance);

        /// <summary>
        /// Cuts a ray past an arc, read by the whole of its circle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoArc2 arc, Tolerance tolerance)
            => CutPast(ray, Round(ray, arc.Center, arc.Radius), tolerance);

        /// <summary>
        /// Cuts a ray past a circle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoCircle2 circle, Tolerance tolerance)
            => CutPast(ray, Round(ray, circle.Center, circle.Radius), tolerance);

        /// <summary>
        /// Cuts a ray past an ellipse, read by the circle of its major radius.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoEllipse2 ellipse, Tolerance tolerance)
            => CutPast(ray, Round(ray, ellipse.Center, ellipse.MajorRadius), tolerance);

        /// <summary>
        /// Cuts a ray past a rectangle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoRectangle2 rect, Tolerance tolerance)
            => CutPast(ray, Farthest(ray, rect.GetVertices()), tolerance);

        /// <summary>
        /// Cuts a ray past a triangle.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoTriangle2 triangle, Tolerance tolerance)
        {
            double farthest = Math.Max(Math.Max(ray.GetDistanceAtPoint(triangle.A), ray.GetDistanceAtPoint(triangle.B)), ray.GetDistanceAtPoint(triangle.C));

            return CutPast(ray, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a ray past a polygon.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoPolygon2 poly, Tolerance tolerance)
            => CutPast(ray, Farthest(ray, poly.Vertices), tolerance);

        /// <summary>
        /// Cuts a ray past a polyline.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoPolyline2 polyline, Tolerance tolerance)
            => CutPast(ray, Farthest(ray, polyline.Vertices), tolerance);

        /// <summary>
        /// Cuts a ray past a face: its outline, and its holes too, though a hole lies within the outline.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoFace2 face, Tolerance tolerance)
        {
            double farthest = Farthest(ray, face.Boundary.Vertices);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                farthest = Math.Max(farthest, Farthest(ray, hole.Vertices));
            }

            return CutPast(ray, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a ray past a curved loop: its corners, and the circle of every arc in it.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            double farthest = Farthest(ray, loop.Vertices);

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                GeoEdge2 edge = loop.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.ToArc();
                    farthest = Math.Max(farthest, Round(ray, arc.Center, arc.Radius));
                }
            }

            return CutPast(ray, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a ray past a curved chain: its corners, and the circle of every arc in it.
        /// </summary>
        internal static GeoLine2 CutPast(GeoRay2 ray, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            double farthest = Farthest(ray, chain.Vertices);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                GeoEdge2 edge = chain.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.ToArc();
                    farthest = Math.Max(farthest, Round(ray, arc.Center, arc.Radius));
                }
            }

            return CutPast(ray, farthest, tolerance);
        }

        /// <summary>
        /// Gets the point of a ray nearest a point: its projection on the carrier line, stopped at the origin.
        /// </summary>
        internal static GeoPoint2 Project(GeoRay2 ray, GeoPoint2 point)
            => ray.GetPointAtDistance(Math.Max(0.0, ray.GetDistanceAtPoint(point)));

        /// <summary>
        /// Finds where two rays cross, at any angle, placed on the first ray and never behind its origin.
        /// </summary>
        /// <remarks>
        /// Two rays cross unless they are parallel by rounding, by the rule of the line without ends (see <see cref="XLine2"/>):
        /// a sine below 1E-15, or a sine of 1E-9 or less with a crossing further along than a double can place within the
        /// point tolerance, measured by the larger of the two distances along, so that both ways round agree; a NaN has none
        /// either. A ray rebuilt through two points a hundred apart of another moved 10 to its side is a sine of some 1E-14 off
        /// it at coordinates of ten thousand, and crosses it nowhere: the crossing would lie 1E15 along. Two rays at half a
        /// degree that cross a thousand along cross there, though <see cref="Intersection2.TryIntersectWith(GeoRay2, GeoRay2, out GeoPoint2, Tolerance)"/>
        /// calls them parallel by angle: distance, collision and the shortest line ask where the rays are, not whether the
        /// crossing is well placed.
        /// <para>
        /// With <paramref name="pastEnds"/> a crossing up to the point tolerance behind either origin counts, as it does for
        /// that method, which places the point. Without it the crossing has to be at or ahead of both origins, which is how
        /// the distance, the collision and the shortest line read two rays: the ray from (0.0009, 0) along X and the one
        /// leaving 0.0009 along 120 degrees from the origin, whose carriers cross 0.0009 behind each, cross nowhere and are
        /// 0.00156 apart.
        /// </para>
        /// </remarks>
        internal static bool TryCross(GeoRay2 ray1, GeoRay2 ray2, Tolerance tolerance, bool pastEnds, out GeoPoint2 crossing)
        {
            crossing = new GeoPoint2(0, 0);

            // Both directions are of unit length, so their cross product is the sine of the angle between them.
            double sine = ray1.Direction.CrossProduct(ray2.Direction);

            // A sine this small is rounding, not an angle: two directions built a rounding apart, one of them the other
            // normalized again, give a few 1E-17, and the crossing it would place lies some 1E+18 along. Written as what has
            // to hold, so that a NaN fails it.
            if (!(Math.Abs(sine) > RoundingSine))
            {
                return false;
            }

            GeoVector2 between = ray1.Origin.GetVectorTo(ray2.Origin);
            double t = between.CrossProduct(ray2.Direction) / sine;
            double u = between.CrossProduct(ray1.Direction) / sine;

            // The distances along are lengths, so the slack behind each origin, where it is asked for, is the point tolerance
            // itself. Written as what has to hold, so that a NaN fails it.
            double slack = pastEnds ? tolerance.EqualPoint : 0.0;

            if (!(t >= -slack && u >= -slack))
            {
                return false;
            }

            // Nearly parallel, a crossing further out than a double can place is rounding too.
            if (!XLine2.Placeable(sine, Math.Max(Math.Abs(t), Math.Abs(u)), ray1.Origin, ray2.Origin, tolerance))
            {
                return false;
            }

            crossing = ray1.GetPointAtDistance(Math.Max(0.0, t));
            return true;
        }

        /// <summary>
        /// Gets how far along a ray the farthest of a set of points projects; a NaN among them is carried.
        /// </summary>
        private static double Farthest(GeoRay2 ray, IReadOnlyList<GeoPoint2> points)
        {
            double farthest = double.NegativeInfinity;

            for (int i = 0; i < points.Count; i++)
            {
                farthest = Math.Max(farthest, ray.GetDistanceAtPoint(points[i]));
            }

            return farthest;
        }

        /// <summary>
        /// Gets how far along a ray a round shape can reach: its centre brought onto the ray, plus its radius.
        /// </summary>
        private static double Round(GeoRay2 ray, GeoPoint2 center, double radius) => ray.GetDistanceAtPoint(center) + radius;
    }
}
