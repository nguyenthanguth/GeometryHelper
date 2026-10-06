using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How far an ellipse is from another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ellipse is a filled region here, as a circle is: a shape that reaches into it, crosses its rim or lies inside it
    /// is nought away. A polygon, a rectangle, a triangle, a circle, another ellipse and a curved loop are regions too, so
    /// one holding the ellipse is nought away as well; a segment, a polyline, an arc and a curved chain are curves.
    /// </para>
    /// <para>
    /// Whether the two reach each other is decided exactly, in the ellipse's own frame, and the distance between two that do
    /// not is worked out to rounding: from a segment, where its ends or the rim point whose tangent runs along it are
    /// nearest; from a circle or an arc, along the normals of the rim through its centre; from another ellipse, by Brent's
    /// method on the distance from the rim of the one to the other, started from 36 points round it: 32 spread evenly half
    /// a step off the axes and the four ends of the axes.
    /// </para>
    /// </remarks>
    public static partial class Ellipse2
    {
        #region Points

        /// <summary>
        /// Gets the distance from an ellipse to a point, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPoint2 point) => DistanceTo(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a point.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged, as a circle measures it.</param>
        /// <returns>Nought for a point inside or on the rim; otherwise its distance to the nearest point of the rim. The point (400, 0) is 100 from the ellipse 300 by 100 centred on the origin.</returns>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPoint2 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across);

            return Holds(ellipse, along, across) ? 0.0 : Gap(ellipse, along, across);
        }

        /// <summary>
        /// Gets the distance from an ellipse to a point, negative for a point inside it, using the default tolerance.
        /// </summary>
        public static double SignedDistanceTo(GeoEllipse2 ellipse, GeoPoint2 point) => SignedDistanceTo(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance <see cref="Locate(GeoEllipse2, GeoPoint2, Tolerance)"/> reads the side with.</param>
        /// <returns>
        /// The distance to the nearest point of the rim, negative where <see cref="Locate(GeoEllipse2, GeoPoint2, Tolerance)"/>
        /// says inside and nought where it says on the rim. The centre of the ellipse 300 by 100 is -100.
        /// </returns>
        public static double SignedDistanceTo(GeoEllipse2 ellipse, GeoPoint2 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across);
            double reach = Gap(ellipse, along, across);

            switch (Locate(ellipse, point, tolerance))
            {
                case PointLocation.Inside:
                    return -reach;
                case PointLocation.OnSide:
                    return 0.0;
                default:
                    return reach;
            }
        }

        #endregion

        #region Segments

        /// <summary>
        /// Gets the distance from an ellipse to a segment, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoLine2 line) => DistanceTo(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a segment.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance; not read, as a circle reads none for a segment.</param>
        /// <returns>Nought when the segment crosses the rim or lies inside; otherwise the shortest gap. The segment from (-100, 150) to (100, 150) is 50 from the ellipse 300 by 100 centred on the origin.</returns>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoLine2 line, Tolerance tolerance)
        {
            return Reaches(ellipse, line) ? 0.0 : Nearest(ellipse, line, out _, out _);
        }

        /// <summary>
        /// Gets the distance from an ellipse to a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolyline2 polyline) => DistanceTo(ellipse, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a polyline, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polyline">The polyline, a curve.</param>
        /// <param name="tolerance">The tolerance: a polyline that comes within its point tolerance touches, and is nought away, as it is from a circle.</param>
        /// <returns>The distance from the nearest of its edges.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double best = ToEdges(ellipse, polyline.GetEdges());
            return best <= tolerance.EqualPoint ? 0.0 : best;
        }

        /// <summary>
        /// Gets the distance from an ellipse to a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolygon2 polygon) => DistanceTo(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon, a region.</param>
        /// <param name="tolerance">The tolerance the polygon is asked whether it holds the centre with.</param>
        /// <returns>Nought when the two overlap, either holding the other; otherwise the distance from the nearest edge.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return Containment2.Contains(polygon, ellipse.Center, tolerance) ? 0.0 : ToEdges(ellipse, polygon.GetEdges());
        }

        /// <summary>
        /// Gets the distance from an ellipse to a rectangle, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoRectangle2 rect) => DistanceTo(ellipse, rect, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a rectangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="rect">The rectangle, a region.</param>
        /// <param name="tolerance">The tolerance the rectangle is asked whether it holds the centre with.</param>
        /// <returns>Nought when the two overlap; otherwise the distance from the nearest side.</returns>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoRectangle2 rect, Tolerance tolerance)
        {
            return Containment2.Contains(rect, ellipse.Center, tolerance) ? 0.0 : ToEdges(ellipse, rect.GetEdges());
        }

        /// <summary>
        /// Gets the distance from an ellipse to a triangle, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoTriangle2 triangle) => DistanceTo(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle, a region; one with corners on each other is read as its longest edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Nought when the two overlap; otherwise the distance from the nearest edge.</returns>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoTriangle2 triangle, Tolerance tolerance)
        {
            return Triangle2.TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? DistanceTo(ellipse, polygon, tolerance)
                : DistanceTo(ellipse, hull, tolerance);
        }

        /// <summary>
        /// Gets the distance from an ellipse to an edge, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoEdge2 edge) => DistanceTo(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to an edge, within a tolerance: as a segment or as an arc, whichever the edge is.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoEdge2 edge, Tolerance tolerance)
        {
            return edge.IsArc
                ? DistanceTo(ellipse, edge.ToArc(), tolerance)
                : DistanceTo(ellipse, edge.ToLine(), tolerance);
        }

        #endregion

        #region Curves

        /// <summary>
        /// Gets the distance from an ellipse to a circle, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoCircle2 circle) => DistanceTo(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a circle.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle, a region.</param>
        /// <param name="tolerance">The tolerance; not read, as two circles read none.</param>
        /// <returns>
        /// Nought when the two overlap; otherwise how far the circle's centre is from the rim, less its radius. The circle of
        /// radius 50 about (0, 300) is 150 from the ellipse 300 by 100 centred on the origin.
        /// </returns>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoCircle2 circle, Tolerance tolerance)
        {
            Frame(ellipse, circle.Center, out double along, out double across);

            return Holds(ellipse, along, across) ? 0.0 : Math.Max(0.0, Gap(ellipse, along, across) - circle.Radius);
        }

        /// <summary>
        /// Gets the distance from an ellipse to an arc, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoArc2 arc) => DistanceTo(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc, a curve.</param>
        /// <param name="tolerance">The tolerance the nearest points of the arc are found with.</param>
        /// <returns>Nought when the arc crosses the rim or lies inside; otherwise the shortest gap.</returns>
        /// <remarks>
        /// The nearest pair is at an end of the arc, or on a normal of the rim that runs through the arc's centre, since there
        /// the gap runs square to both. Whether any of the arc lies inside is read exactly, from the turning points of the
        /// ellipse's equation along the arc.
        /// </remarks>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoArc2 arc, Tolerance tolerance)
        {
            return Reaches(ellipse, arc) ? 0.0 : NearestToArc(ellipse, arc, tolerance, out _, out _);
        }

        /// <summary>
        /// Gets the distance between two ellipses, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoEllipse2 other) => DistanceTo(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Gets the distance between two ellipses.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The second ellipse.</param>
        /// <param name="tolerance">The tolerance; not read, as two circles read none.</param>
        /// <returns>
        /// Nought when the two overlap, one inside the other included; otherwise the shortest gap between the rims. The
        /// ellipse 300 by 100 centred on the origin and the one 100 by 50 about (0, 250) along Y are 50 apart.
        /// </returns>
        /// <remarks>
        /// Whether they overlap is read exactly, from the turning points of the equation of the one along the rim of the
        /// other. The gap is the least distance from a point of the first rim to the second, found by Brent's method from
        /// each least of 36 starting points round the first, each side of it on its own: 32 spread evenly half a step off
        /// the axes, so that none of them is an end of one, and the four ends of the axes, where a thin ellipse turns
        /// fastest. Checked against a dense independent search on 80 000 pairs apart, 20 000 from each of four seeds, as thin
        /// as a thousand to one: the worst relative error was 3E-13.
        /// </remarks>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoEllipse2 other, Tolerance tolerance)
        {
            return Reaches(ellipse, other) ? 0.0 : RimToRim(ellipse, other, out _);
        }

        /// <summary>
        /// Gets the distance from an ellipse to a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolylineArc2 chain) => DistanceTo(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The chain, a curve.</param>
        /// <param name="tolerance">The tolerance: a chain that comes within its point tolerance touches, and is nought away.</param>
        /// <returns>The distance from the nearest of its edges, measured on the arcs themselves.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            double best = ToEdges(ellipse, chain.GetEdges(), tolerance);
            return best <= tolerance.EqualPoint ? 0.0 : best;
        }

        /// <summary>
        /// Gets the distance from an ellipse to a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolygonArc2 loop) => DistanceTo(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The loop, a region.</param>
        /// <param name="tolerance">The tolerance: a loop that comes within its point tolerance touches, and is nought away.</param>
        /// <returns>Nought when the two overlap, either holding the other; otherwise the distance from the nearest edge.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            if (Containment2.Contains(loop, ellipse.Center, tolerance))
            {
                return 0.0;
            }

            double best = ToEdges(ellipse, loop.GetEdges(), tolerance);
            return best <= tolerance.EqualPoint ? 0.0 : best;
        }

        #endregion

        #region Distance helpers

        /// <summary>
        /// Says whether a point given in the ellipse's own frame lies inside it or on its rim, exactly.
        /// </summary>
        private static bool Holds(GeoEllipse2 ellipse, double along, double across)
        {
            double u = along / ellipse.MajorRadius;
            double v = across / ellipse.MinorRadius;

            return u * u + v * v <= 1.0;
        }

        /// <summary>
        /// Gets the distance from a point to the rim, negative inside, read exactly rather than by a tolerance.
        /// </summary>
        private static double Signed(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            Frame(ellipse, point, out double along, out double across);
            double reach = Gap(ellipse, along, across);

            return Holds(ellipse, along, across) ? -reach : reach;
        }

        /// <summary>
        /// Says whether a segment reaches into the region at all: its point nearest the centre, in the frame where the
        /// ellipse is the unit circle, lies inside.
        /// </summary>
        private static bool Reaches(GeoEllipse2 ellipse, GeoLine2 line)
        {
            Frame(ellipse, line.StartPoint, out double along0, out double across0);
            Frame(ellipse, line.EndPoint, out double along1, out double across1);

            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double u0 = along0 / a;
            double v0 = across0 / b;
            double du = (along1 - along0) / a;
            double dv = (across1 - across0) / b;
            double speed = du * du + dv * dv;
            double s = speed > 0.0 ? Math.Max(0.0, Math.Min(1.0, -(u0 * du + v0 * dv) / speed)) : 0.0;
            double u = u0 + s * du;
            double v = v0 + s * dv;

            return u * u + v * v <= 1.0;
        }

        /// <summary>
        /// Says whether an arc reaches into the region at all: an end lies inside, or the arc crosses the rim.
        /// </summary>
        /// <remarks>
        /// An arc with both ends outside reaches in only by crossing the rim, where the circle carrying it does. Those are the
        /// changes of sign of the circle's equation along the rim, |P(t) - c|² - r², which is read in drawing units however
        /// the two compare in size; each is kept when the arc sweeps over it.
        /// </remarks>
        private static bool Reaches(GeoEllipse2 ellipse, GeoArc2 arc)
        {
            Frame(ellipse, arc.StartPoint, out double along, out double across);

            if (Holds(ellipse, along, across))
            {
                return true;
            }

            Frame(ellipse, arc.EndPoint, out along, out across);

            if (Holds(ellipse, along, across))
            {
                return true;
            }

            Wave wave = AlongCircle(ellipse, arc.Center, arc.Radius);
            var turns = new double[4];
            int count = Turns(wave, turns);

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                double low = turns[i];
                double high = next > i ? turns[next] : turns[next] + FullTurn;
                double atLow = wave.At(low);
                double atHigh = wave.At(high);

                if ((atLow < 0.0) == (atHigh < 0.0) || atLow == 0.0 || atHigh == 0.0)
                {
                    continue;
                }

                GeoPoint2 crossing = GetPointAtAngle(ellipse, Root(wave, low, high, atLow, atHigh));

                if (Sweeps(arc, Math.Atan2(crossing.Y - arc.Center.Y, crossing.X - arc.Center.X)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Says whether two ellipses overlap at all: either centre lies inside the other, or the rim of one reaches into the
        /// other.
        /// </summary>
        /// <remarks>
        /// The rim read is the one that keeps the reading well scaled: the equation of an ellipse grows as the square of
        /// distances over its radii, so the larger one's equation is read along the smaller one's rim, and a hair-thin ellipse
        /// a millionth of a unit from a large one is not taken for one touching it by rounding.
        /// </remarks>
        private static bool Reaches(GeoEllipse2 ellipse, GeoEllipse2 other)
        {
            Frame(other, ellipse.Center, out double along, out double across);

            if (Holds(other, along, across))
            {
                return true;
            }

            Frame(ellipse, other.Center, out along, out across);

            if (Holds(ellipse, along, across))
            {
                return true;
            }

            double apart = ellipse.Center.DistanceTo(other.Center);
            bool alongFirst = (ellipse.MajorRadius + apart) / other.MinorRadius <= (other.MajorRadius + apart) / ellipse.MinorRadius;
            GeoEllipse2 rim = alongFirst ? ellipse : other;
            GeoEllipse2 read = alongFirst ? other : ellipse;

            Wave wave = Along(rim.Center, rim.MajorAxis, rim.MajorRadius, rim.MinorRadius, read);
            var turns = new double[4];
            int count = Turns(wave, turns);

            if (!(wave.At(0.0) > 0.0))
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (!(wave.At(turns[i]) > 0.0))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Says whether an angle about an arc's centre falls within its sweep.
        /// </summary>
        private static bool Sweeps(GeoArc2 arc, double angle)
        {
            double sweep = arc.SweptAngle;

            if (Math.Abs(sweep) >= FullTurn)
            {
                return true;
            }

            double from = sweep >= 0.0 ? angle - arc.StartAngle : arc.StartAngle - angle;
            from -= FullTurn * Math.Floor(from / FullTurn);

            return from <= Math.Abs(sweep);
        }

        /// <summary>
        /// Gets the least distance from the rim to a run of straight edges, each read as a segment.
        /// </summary>
        private static double ToEdges(GeoEllipse2 ellipse, IEnumerable<GeoLine2> edges)
        {
            double best = double.MaxValue;

            foreach (GeoLine2 edge in edges)
            {
                double reach = DistanceTo(ellipse, edge, Tolerance.Global);

                if (reach < best)
                {
                    best = reach;

                    if (!(best > 0.0))
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the least distance from the region to a run of edges that may curve.
        /// </summary>
        private static double ToEdges(GeoEllipse2 ellipse, IEnumerable<GeoEdge2> edges, Tolerance tolerance)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double reach = DistanceTo(ellipse, edge, tolerance);

                if (reach < best)
                {
                    best = reach;

                    if (!(best > 0.0))
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest pair joining the rim to a segment that does not cross it.
        /// </summary>
        /// <remarks>
        /// The pair is at an end of the segment, or at the rim point whose tangent runs along the segment and its foot on it,
        /// where the gap runs square to both. Inside the ellipse the gap to the rim bends down along any segment, so there
        /// the nearest is always an end. The start is weighed first and ties keep the first.
        /// </remarks>
        private static double Nearest(GeoEllipse2 ellipse, GeoLine2 line, out GeoPoint2 onRim, out GeoPoint2 onLine)
        {
            onLine = line.StartPoint;
            onRim = GetClosestPointOnBoundary(ellipse, onLine);
            double best = onRim.DistanceTo(onLine);

            GeoPoint2 end = line.EndPoint;
            GeoPoint2 endRim = GetClosestPointOnBoundary(ellipse, end);
            double endReach = endRim.DistanceTo(end);

            if (endReach < best)
            {
                best = endReach;
                onRim = endRim;
                onLine = end;
            }

            double length = line.Length;

            if (length > 0.0)
            {
                Across(ellipse, line, length, out double along0, out double across0, out double alongStep, out double acrossStep, out double sideAlong, out double sideAcross, out _);

                double foot = ((sideAlong - along0) * alongStep + (sideAcross - across0) * acrossStep) / (length * length);

                if (foot > 0.0 && foot < 1.0)
                {
                    GeoPoint2 rim = OnRim(ellipse, sideAlong, sideAcross);
                    GeoPoint2 atFoot = line.GetPointAtParameter(foot);
                    double reach = rim.DistanceTo(atFoot);

                    if (reach < best)
                    {
                        best = reach;
                        onRim = rim;
                        onLine = atFoot;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest pair joining the rim to the circumference of a circle that does not cross it.
        /// </summary>
        /// <remarks>
        /// The nearest point of a circle to a point of the rim lies straight out from its centre, |P - c| - r away, so the
        /// pair is on a normal of the rim through the centre: at the rim point nearest the centre, or at another foot of a
        /// normal from it, the furthest among them when the ellipse lies inside the circle.
        /// </remarks>
        private static double NearestToCircle(GeoEllipse2 ellipse, GeoPoint2 center, double radius, out GeoPoint2 onRim, out GeoPoint2 onCircle)
        {
            onRim = GetClosestPointOnBoundary(ellipse, center);
            onCircle = Outward(center, radius, onRim);
            double best = Math.Abs(center.DistanceTo(onRim) - radius);

            var turns = new double[4];
            int count = Turns(AlongCircle(ellipse, center, 0.0), turns);

            for (int i = 0; i < count; i++)
            {
                GeoPoint2 rim = GetPointAtAngle(ellipse, turns[i]);
                double reach = Math.Abs(center.DistanceTo(rim) - radius);

                if (reach < best)
                {
                    best = reach;
                    onRim = rim;
                    onCircle = Outward(center, radius, rim);
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the point of a circle straight out from its centre towards a point, or the end of its radius along X when the
        /// point is the centre.
        /// </summary>
        private static GeoPoint2 Outward(GeoPoint2 center, double radius, GeoPoint2 toward)
        {
            double dx = toward.X - center.X;
            double dy = toward.Y - center.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            return length > 0.0
                ? new GeoPoint2(center.X + dx * (radius / length), center.Y + dy * (radius / length))
                : new GeoPoint2(center.X + radius, center.Y);
        }

        /// <summary>
        /// Gets the shortest pair joining the rim to an arc that does not cross it.
        /// </summary>
        /// <remarks>
        /// The pair is at an end of the arc, or on a normal of the rim through the arc's centre, on either side of the centre;
        /// each candidate is clamped onto the arc and joined to the rim point nearest it, so every pair weighed is one the
        /// two shapes really hold.
        /// </remarks>
        private static double NearestToArc(GeoEllipse2 ellipse, GeoArc2 arc, Tolerance tolerance, out GeoPoint2 onRim, out GeoPoint2 onArc)
        {
            onArc = arc.StartPoint;
            onRim = GetClosestPointOnBoundary(ellipse, onArc);
            double best = onRim.DistanceTo(onArc);

            Weigh(ellipse, arc.EndPoint, ref best, ref onRim, ref onArc);

            GeoPoint2 center = arc.Center;
            var turns = new double[4];
            int count = Turns(AlongCircle(ellipse, center, 0.0), turns);

            for (int i = -1; i < count; i++)
            {
                GeoPoint2 rim = i < 0 ? GetClosestPointOnBoundary(ellipse, center) : GetPointAtAngle(ellipse, turns[i]);
                GeoPoint2 out1 = Outward(center, arc.Radius, rim);
                GeoPoint2 out2 = new GeoPoint2(2.0 * center.X - out1.X, 2.0 * center.Y - out1.Y);

                Weigh(ellipse, Arc2.ProjectToArc(arc, out1, tolerance), ref best, ref onRim, ref onArc);
                Weigh(ellipse, Arc2.ProjectToArc(arc, out2, tolerance), ref best, ref onRim, ref onArc);
            }

            return best;
        }

        /// <summary>
        /// Keeps a point of the other shape, with the rim point nearest it, when the pair is shorter than the one in hand.
        /// </summary>
        private static void Weigh(GeoEllipse2 ellipse, GeoPoint2 point, ref double best, ref GeoPoint2 onRim, ref GeoPoint2 onOther)
        {
            GeoPoint2 rim = GetClosestPointOnBoundary(ellipse, point);
            double reach = rim.DistanceTo(point);

            if (reach < best)
            {
                best = reach;
                onRim = rim;
                onOther = point;
            }
        }

        /// <summary>
        /// Gets the distance from the point of one rim at an angle to the rim of another ellipse.
        /// </summary>
        private static double GapAlong(GeoEllipse2 ellipse, GeoEllipse2 other, double angle)
        {
            Frame(other, GetPointAtAngle(ellipse, angle), out double along, out double across);
            return Gap(other, along, across);
        }

        /// <summary>
        /// Gets the least distance between two rims that do not cross, and the eccentric angle on the first where it is.
        /// </summary>
        /// <remarks>
        /// The distance from the first rim to the second is read at 32 angles half a step off the axes and at the four ends of
        /// the axes, where a thin ellipse turns within a sliver of angle; each that is no further than its two neighbours
        /// starts Brent's method between them, and the least of what those find is the answer.
        /// </remarks>
        private static double RimToRim(GeoEllipse2 ellipse, GeoEllipse2 other, out double angle)
        {
            const int Even = 32;
            const int Starts = Even + 4;

            var starts = new double[Starts];

            for (int k = 0; k < Even; k++)
            {
                starts[k] = (k + 0.5) * FullTurn / Even;
            }

            starts[Even] = 0.0;
            starts[Even + 1] = HalfPi;
            starts[Even + 2] = Math.PI;
            starts[Even + 3] = 1.5 * Math.PI;
            Array.Sort(starts);

            var reach = new double[Starts];

            for (int k = 0; k < Starts; k++)
            {
                reach[k] = GapAlong(ellipse, other, starts[k]);
            }

            double best = double.PositiveInfinity;
            angle = 0.0;

            for (int k = 0; k < Starts; k++)
            {
                int before = (k + Starts - 1) % Starts;
                int after = (k + 1) % Starts;

                if (reach[k] > reach[before] || reach[k] > reach[after])
                {
                    continue;
                }

                // Each side on its own: by the end of a thin ellipse the rim turns within a sliver of angle, and a shape near
                // it can be nearest on both sides at once, the start lying between the two.
                double low = starts[k] - Positive(starts[k] - starts[before]);
                double high = starts[k] + Positive(starts[after] - starts[k]);

                for (int side = 0; side < 2; side++)
                {
                    double from = side == 0 ? low : starts[k];
                    double to = side == 0 ? starts[k] : high;
                    double found = Least(t => GapAlong(ellipse, other, t), from, 0.5 * (from + to), to, out double value);

                    if (value < best)
                    {
                        best = value;
                        angle = found;
                    }
                }

                if (reach[k] < best)
                {
                    best = reach[k];
                    angle = starts[k];
                }
            }

            return best;
        }

        #endregion
    }
}
