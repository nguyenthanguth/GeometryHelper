using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Core
{
    /// <summary>
    /// A shape asked about by a spline in the plane, taken to pieces the spline's own maths can answer: the segments and arcs
    /// of its outline, and for a closed shape, the way to tell whether a point lies in its material.
    /// </summary>
    internal sealed class SplineShape
    {
        /// <summary>The pieces of the shape's boundary, or the one of a shape that has none.</summary>
        internal readonly List<Partner2> Parts = new List<Partner2>();

        /// <summary>Says whether a point lies in the material of a closed shape; null for a curve, which has no inside.</summary>
        internal Func<GeoPoint2, bool> Holds;
    }

    /// <summary>
    /// The queries of a spline in the plane against every other shape in the plane: how far apart they are, where they
    /// meet, and the shortest segment between them. Each shape is cut to its boundary and the spline works out its
    /// meetings with each piece, one maths for all, so that the spline and the shape agree to the bit asked from either side.
    /// </summary>
    /// <remarks>
    /// A closed shape is a region, as every curve reads it: a spline inside a polygon, inside a circle or in the material of a
    /// face is nought from it, and one in a hole is the gap away from the hole's rim. The shortest segment and the crossings are
    /// those of the boundary.
    /// </remarks>
    internal static class Spline2
    {
        private static Tolerance Exactly => default(Tolerance);

        #region Building the shape

        /// <summary>
        /// Gets the stretch of a segment as the spline's partner.
        /// </summary>
        private static LinePartner Segment(GeoPoint2 start, GeoPoint2 end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            // A segment that is one point, to the bit, is that point: it has no direction, and is none along any.
            if (!(length > 0.0))
            {
                return new LinePartner(start.X, start.Y, 1.0, 0.0, 0.0, 0.0, true, true);
            }

            return new LinePartner(start.X, start.Y, dx / length, dy / length, 0.0, length, true, true);
        }

        /// <summary>
        /// Gets the range along a line, from its origin, that the control hull of a spline covers.
        /// </summary>
        private static void Cover(GeoSpline2 spline, GeoPoint2 origin, GeoVector2 direction, out double lowest, out double highest)
        {
            var min = new double[2];
            var max = new double[2];
            spline.Curve.Index.Whole(min, max);
            lowest = double.PositiveInfinity;
            highest = double.NegativeInfinity;

            for (int i = 0; i < 4; i++)
            {
                double x = (i & 1) == 0 ? min[0] : max[0];
                double y = (i & 2) == 0 ? min[1] : max[1];
                double along = (x - origin.X) * direction.X + (y - origin.Y) * direction.Y;
                lowest = Math.Min(lowest, along);
                highest = Math.Max(highest, along);
            }
        }

        private static LinePartner Ray(GeoSpline2 spline, GeoRay2 ray)
        {
            Cover(spline, ray.Origin, ray.Direction, out _, out double highest);
            double stop = Math.Max(highest, 0.0) + Math.Max(1.0, highest);

            return new LinePartner(ray.Origin.X, ray.Origin.Y, ray.Direction.X, ray.Direction.Y, 0.0, stop, true, false);
        }

        private static LinePartner Endless(GeoSpline2 spline, GeoXLine2 xline)
        {
            Cover(spline, xline.Origin, xline.Direction, out double lowest, out double highest);
            double pad = Math.Max(1.0, highest - lowest);

            return new LinePartner(xline.Origin.X, xline.Origin.Y, xline.Direction.X, xline.Direction.Y, lowest - pad, highest + pad, false, false);
        }

        private static ArcPartner Round(GeoArc2 arc)
        {
            GeoPoint2 start = arc.StartPoint;
            GeoPoint2 end = arc.EndPoint;
            bool whole = Math.Abs(arc.SweptAngle) >= 2.0 * Math.PI;

            return new ArcPartner(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.SweptAngle, whole, start.X, start.Y, end.X, end.Y);
        }

        private static ArcPartner Round(GeoCircle2 circle)
        {
            return new ArcPartner(circle.Center.X, circle.Center.Y, circle.Radius, 0.0, 2.0 * Math.PI, true, circle.Center.X + circle.Radius, circle.Center.Y, circle.Center.X + circle.Radius, circle.Center.Y);
        }

        private static void Add(SplineShape shape, GeoEdge2 edge)
        {
            shape.Parts.Add(ArcChain2.Straight(edge, out GeoLine2 straight) ? (Partner2)Segment(straight.StartPoint, straight.EndPoint) : Round(edge.ToArc()));
        }

        private static void AddAll(SplineShape shape, IEnumerable<GeoLine2> edges)
        {
            foreach (GeoLine2 edge in edges)
            {
                shape.Parts.Add(Segment(edge.StartPoint, edge.EndPoint));
            }
        }

        private static SplineShape Required(GeoPolygon2 polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return new SplineShape();
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoLine2 line)
        {
            var shape = new SplineShape();
            shape.Parts.Add(Segment(line.StartPoint, line.EndPoint));

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoRay2 ray)
        {
            var shape = new SplineShape();
            shape.Parts.Add(Ray(spline, ray));

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoXLine2 xline)
        {
            var shape = new SplineShape();
            shape.Parts.Add(Endless(spline, xline));

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoPolyline2 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            var shape = new SplineShape();
            AddAll(shape, polyline.GetEdges());

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoPolygon2 polygon)
        {
            SplineShape shape = Required(polygon);
            AddAll(shape, polygon.GetEdges());
            shape.Holds = point => Containment2.Contains(polygon, point, Exactly);

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoRectangle2 rect)
        {
            var shape = new SplineShape();
            AddAll(shape, rect.GetEdges());
            shape.Holds = point => Containment2.Contains(rect, point, Exactly);

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoTriangle2 triangle)
        {
            var shape = new SplineShape();

            if (Triangle2.TryAsPolygonByItsSides(triangle, out GeoPolygon2 polygon, out GeoLine2 hull))
            {
                AddAll(shape, polygon.GetEdges());
                shape.Holds = point => Containment2.Contains(polygon, point, Exactly);
            }
            else
            {
                shape.Parts.Add(Segment(hull.StartPoint, hull.EndPoint));
            }

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoFace2 face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            var shape = new SplineShape();
            AddAll(shape, face.Boundary.GetEdges());

            foreach (GeoPolygon2 hole in face.Holes)
            {
                AddAll(shape, hole.GetEdges());
            }

            // The material is the outline less the inside of each hole; a point on a hole's rim is on the material's edge.
            shape.Holds = point =>
            {
                if (!Containment2.Contains(face.Boundary, point, Exactly))
                {
                    return false;
                }

                foreach (GeoPolygon2 hole in face.Holes)
                {
                    if (Containment2.Contains(hole, point, Exactly) && !Containment2.IsPointOn(hole, point, Exactly))
                    {
                        return false;
                    }
                }

                return true;
            };

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoArc2 arc)
        {
            var shape = new SplineShape();
            shape.Parts.Add(Round(arc));

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoCircle2 circle)
        {
            var shape = new SplineShape();
            shape.Parts.Add(Round(circle));
            shape.Holds = point => Containment2.Contains(circle, point, Exactly);

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoEllipse2 ellipse)
        {
            var shape = new SplineShape();
            shape.Parts.Add(new EllipsePartner(ellipse));

            // The same test the ellipse's own distances make: inside or on the rim, exactly.
            shape.Holds = point =>
            {
                double dx = point.X - ellipse.Center.X;
                double dy = point.Y - ellipse.Center.Y;
                double u = (dx * ellipse.MajorAxis.X + dy * ellipse.MajorAxis.Y) / ellipse.MajorRadius;
                double v = (dy * ellipse.MajorAxis.X - dx * ellipse.MajorAxis.Y) / ellipse.MinorRadius;

                return u * u + v * v <= 1.0;
            };

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoEdge2 edge)
        {
            var shape = new SplineShape();
            Add(shape, edge);

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoPolylineArc2 chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            var shape = new SplineShape();

            foreach (GeoEdge2 edge in chain.GetEdges())
            {
                Add(shape, edge);
            }

            return shape;
        }

        internal static SplineShape Of(GeoSpline2 spline, GeoPolygonArc2 loop)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            var shape = new SplineShape();

            foreach (GeoEdge2 edge in loop.GetEdges())
            {
                Add(shape, edge);
            }

            shape.Holds = point => Containment2.Contains(loop, point, Exactly);

            return shape;
        }

        #endregion

        #region Two splines

        /// <summary>
        /// Says whether a spline comes before another in a fixed order: by degree, then number of control points, then each
        /// control point, weight and knot in turn. Two splines asked about each other are worked out from the one that comes
        /// first, so that each says the same to the bit whichever is asked.
        /// </summary>
        private static bool Precedes(GeoSpline2 a, GeoSpline2 b)
        {
            if (a.Degree != b.Degree)
            {
                return a.Degree < b.Degree;
            }

            if (a.ControlPointCount != b.ControlPointCount)
            {
                return a.ControlPointCount < b.ControlPointCount;
            }

            for (int i = 0; i < a.ControlPointCount; i++)
            {
                if (a.ControlPoints[i].X != b.ControlPoints[i].X)
                {
                    return a.ControlPoints[i].X < b.ControlPoints[i].X;
                }

                if (a.ControlPoints[i].Y != b.ControlPoints[i].Y)
                {
                    return a.ControlPoints[i].Y < b.ControlPoints[i].Y;
                }
            }

            for (int i = 0; i < a.ControlPointCount; i++)
            {
                if (a.Weights[i] != b.Weights[i])
                {
                    return a.Weights[i] < b.Weights[i];
                }
            }

            for (int i = 0; i < a.Knots.Count; i++)
            {
                if (a.Knots[i] != b.Knots[i])
                {
                    return a.Knots[i] < b.Knots[i];
                }
            }

            return false;
        }

        private static List<Partner2> Against(GeoSpline2 second)
        {
            return new List<Partner2> { new SplinePartner(second.Curve) };
        }

        /// <summary>
        /// Gets the distance from a spline to another, the same from either.
        /// </summary>
        internal static double DistanceTo(GeoSpline2 spline, GeoSpline2 other)
        {
            GeoSpline2 first = Precedes(other, spline) ? other : spline;
            GeoSpline2 second = ReferenceEquals(first, spline) ? other : spline;

            return first.Curve.TryGetNearest(Against(second), out Contact nearest) ? nearest.Gap : double.PositiveInfinity;
        }

        /// <summary>
        /// Gets the shortest segment from a spline to another, the same from either but turned round.
        /// </summary>
        internal static GeoLine2 GetShortestLineTo(GeoSpline2 spline, GeoSpline2 other)
        {
            bool turned = Precedes(other, spline);
            GeoSpline2 first = turned ? other : spline;
            GeoSpline2 second = turned ? spline : other;

            if (!first.Curve.TryGetNearest(Against(second), out Contact nearest))
            {
                throw new InvalidOperationException("The splines have nothing to measure between.");
            }

            var line = new GeoLine2(new GeoPoint2(nearest.Px, nearest.Py), new GeoPoint2(nearest.Qx, nearest.Qy));

            return turned ? line.Reverse() : line;
        }

        /// <summary>
        /// Gets the places two splines meet, the same points from either, in order along the one asked.
        /// </summary>
        internal static GeoPoint2[] GetIntersections(GeoSpline2 spline, GeoSpline2 other, Tolerance tolerance)
        {
            bool turned = Precedes(other, spline);
            GeoSpline2 first = turned ? other : spline;
            GeoSpline2 second = turned ? spline : other;
            List<Contact> meetings = first.Curve.GetMeetings(Against(second), tolerance.EqualPoint);

            // Worked out along the first, they are given along the one asked: the same places, the same bits.
            if (turned)
            {
                meetings.Sort((x, y) => x.Other != y.Other ? x.Other.CompareTo(y.Other) : x.Knot.CompareTo(y.Knot));
            }

            var points = new GeoPoint2[meetings.Count];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint2(meetings[i].Px, meetings[i].Py);
            }

            return points;
        }

        #endregion

        #region Asking

        /// <summary>
        /// Gets the distance from a spline to a shape: nought where a point of the spline lies in the material of a closed shape
        /// or the two meet, otherwise the gap, measured exactly.
        /// </summary>
        internal static double DistanceTo(GeoSpline2 spline, SplineShape shape)
        {
            if (shape.Holds != null && shape.Holds(spline.StartPoint))
            {
                return 0.0;
            }

            return spline.Curve.TryGetNearest(shape.Parts, out Contact nearest) ? nearest.Gap : double.PositiveInfinity;
        }

        /// <summary>
        /// Gets the places where a spline meets the boundary of a shape.
        /// </summary>
        internal static GeoPoint2[] GetIntersections(GeoSpline2 spline, SplineShape shape, Tolerance tolerance)
        {
            List<Contact> meetings = spline.Curve.GetMeetings(shape.Parts, tolerance.EqualPoint);
            var points = new GeoPoint2[meetings.Count];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint2(meetings[i].Px, meetings[i].Py);
            }

            return points;
        }

        /// <summary>
        /// Gets the shortest segment from a spline to the boundary of a shape: boundary to boundary, so that of no length where
        /// they meet.
        /// </summary>
        internal static GeoLine2 GetShortestLineTo(GeoSpline2 spline, SplineShape shape)
        {
            if (!spline.Curve.TryGetNearest(shape.Parts, out Contact nearest))
            {
                throw new InvalidOperationException("The shape has no boundary to measure to.");
            }

            return new GeoLine2(new GeoPoint2(nearest.Px, nearest.Py), new GeoPoint2(nearest.Qx, nearest.Qy));
        }

        #endregion
    }
}
