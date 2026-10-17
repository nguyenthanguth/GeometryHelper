using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether an ellipse touches another shape, and whether it holds one whole.
    /// </summary>
    /// <remarks>
    /// The rule is the circle's: two shapes collide when they come within the point tolerance of each other, the rims
    /// meeting or one lying inside the other. The ellipse and every closed shape are regions; a segment, a polyline, an arc
    /// and a curved chain are curves, which collide only by reaching the region.
    /// </remarks>
    public static partial class Ellipse2
    {
        #region Touching

        /// <summary>
        /// Determines whether an ellipse touches a segment, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoLine2 line) => CollidesWith(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment comes within the point tolerance of the region, crossing the rim or lying inside; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoLine2 line, Tolerance tolerance)
        {
            return DistanceTo(ellipse, line, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolyline2 polyline) => CollidesWith(ellipse, polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches a polyline, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if any edge comes within the point tolerance of the region; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return DistanceTo(ellipse, polyline, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolygon2 polygon) => CollidesWith(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon holds the centre, or an edge comes within the point tolerance of the region; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return DistanceTo(ellipse, polygon, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a rectangle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoRectangle2 rect) => CollidesWith(ellipse, rect, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a rectangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rectangle holds the centre, or a side comes within the point tolerance of the region; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoRectangle2 rect, Tolerance tolerance)
        {
            return DistanceTo(ellipse, rect, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a triangle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoTriangle2 triangle) => CollidesWith(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle; one whose corners coincide exactly is read as its longest edge, a short side being the side it is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two come within the point tolerance of each other; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoTriangle2 triangle, Tolerance tolerance)
        {
            return Triangle2.TryAsPolygonByItsSides(triangle, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? CollidesWith(ellipse, polygon, tolerance)
                : CollidesWith(ellipse, hull, tolerance);
        }

        /// <summary>
        /// Determines whether an ellipse reaches the material of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoFace2 face) => CollidesWith(ellipse, face, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse reaches the material of a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ellipse comes within the point tolerance of the material, as <see cref="DistanceTo(GeoEllipse2, GeoFace2, Tolerance)"/> says; otherwise, false.</returns>
        /// <remarks>
        /// An ellipse wholly inside a hole, or holding it within itself, is told apart from one reaching the material by the
        /// exact distance to the rims, so a collision and a distance never disagree. A rim edge whose box lies outside the
        /// ellipse's own axis-aligned box, widened by the point tolerance, cannot be within it and is not measured, and the
        /// search stops at the first edge within the point tolerance: the answer is the same as the distance's, found sooner.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoFace2 face, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            double reach = tolerance.EqualPoint;
            GeoVector2 u = ellipse.MajorAxis;
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double cx = ellipse.Center.X;
            double cy = ellipse.Center.Y;
            double slack = reach + 1E-14 * (Math.Abs(cx) + Math.Abs(cy) + a);
            double hx = Math.Sqrt(a * u.X * a * u.X + b * u.Y * b * u.Y) + slack;
            double hy = Math.Sqrt(a * u.Y * a * u.Y + b * u.X * b * u.X) + slack;

            return Face2.Apart(
                face,
                rim => Math.Min(rim.StartPoint.X, rim.EndPoint.X) > cx + hx || Math.Max(rim.StartPoint.X, rim.EndPoint.X) < cx - hx
                    || Math.Min(rim.StartPoint.Y, rim.EndPoint.Y) > cy + hy || Math.Max(rim.StartPoint.Y, rim.EndPoint.Y) < cy - hy
                    ? double.PositiveInfinity
                    : DistanceTo(ellipse, rim, tolerance),
                GetPointAtAngle(ellipse, 0.0),
                reach) <= reach;
        }

        /// <summary>
        /// Determines whether an ellipse touches an edge, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoEdge2 edge) => CollidesWith(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches an edge, within a tolerance: as a segment or as an arc, whichever the edge is.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoEdge2 edge, Tolerance tolerance)
        {
            return DistanceTo(ellipse, edge, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a circle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoCircle2 circle) => CollidesWith(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two come within the point tolerance of each other, one inside the other included; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoCircle2 circle, Tolerance tolerance)
        {
            return DistanceTo(ellipse, circle, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches an arc, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoArc2 arc) => CollidesWith(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arc comes within the point tolerance of the region, crossing the rim or lying inside; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoArc2 arc, Tolerance tolerance)
        {
            return DistanceTo(ellipse, arc, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether two ellipses touch or overlap, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoEllipse2 other) => CollidesWith(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Determines whether two ellipses touch or overlap, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The second ellipse.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two come within the point tolerance of each other, one inside the other included; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoEllipse2 other, Tolerance tolerance)
        {
            // Two whose circles round the major axes lie apart cannot touch, and need no search.
            if (ellipse.Center.DistanceTo(other.Center) > ellipse.MajorRadius + other.MajorRadius + tolerance.EqualPoint)
            {
                return false;
            }

            return DistanceTo(ellipse, other, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolylineArc2 chain) => CollidesWith(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if any edge comes within the point tolerance of the region; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return DistanceTo(ellipse, chain, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolygonArc2 loop) => CollidesWith(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches or overlaps a curved loop, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the loop holds the centre, or an edge comes within the point tolerance of the region; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return DistanceTo(ellipse, loop, tolerance) <= tolerance.EqualPoint;
        }

        #endregion

        #region Holding whole

        /// <summary>
        /// Checks whether an ellipse holds a segment whole, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoEllipse2 ellipse, GeoLine2 line) => Contains(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Checks whether an ellipse holds a segment whole, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if both ends are contained, which for a region with no dent is the whole segment; otherwise, false.</returns>
        public static bool Contains(GeoEllipse2 ellipse, GeoLine2 line, Tolerance tolerance)
        {
            return Contains(ellipse, line.StartPoint, tolerance) && Contains(ellipse, line.EndPoint, tolerance);
        }

        /// <summary>
        /// Checks whether an ellipse holds a circle whole, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoEllipse2 ellipse, GeoCircle2 circle) => Contains(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Checks whether an ellipse holds a circle whole, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance: the circle may reach past the rim by up to its point tolerance.</param>
        /// <returns>
        /// true if the circle's centre lies inside, at least its radius less the point tolerance from the rim; otherwise,
        /// false. The ellipse 300 by 100 holds the circle of radius 100 about its centre, which touches it at the ends of the
        /// minor axis, and not one of radius 101.
        /// </returns>
        public static bool Contains(GeoEllipse2 ellipse, GeoCircle2 circle, Tolerance tolerance)
        {
            return Signed(ellipse, circle.Center) + circle.Radius <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether an ellipse holds another whole, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoEllipse2 ellipse, GeoEllipse2 other) => Contains(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Checks whether an ellipse holds another whole, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The outer ellipse.</param>
        /// <param name="other">The inner ellipse.</param>
        /// <param name="tolerance">The tolerance: the inner one may reach past the rim by up to its point tolerance.</param>
        /// <returns>
        /// true if no point of the other's rim lies further outside than the point tolerance; otherwise, false. One touching
        /// the rim from inside is held, as a circle holds a circle touching it from inside.
        /// </returns>
        /// <remarks>
        /// The other lies wholly inside when the first's equation along its rim is nowhere above nought, read exactly at the
        /// turning points. Otherwise the furthest it reaches out is found by Brent's method from 36 starts round its rim,
        /// as the distance between two ellipses is.
        /// </remarks>
        public static bool Contains(GeoEllipse2 ellipse, GeoEllipse2 other, Tolerance tolerance)
        {
            if (!Contains(ellipse, other.Center, tolerance))
            {
                return false;
            }

            Wave wave = Along(other.Center, other.MajorAxis, other.MajorRadius, other.MinorRadius, ellipse);
            var turns = new double[4];
            int count = Turns(wave, turns);
            bool inside = !(wave.At(0.0) > 0.0);

            for (int i = 0; i < count && inside; i++)
            {
                inside = !(wave.At(turns[i]) > 0.0);
            }

            if (inside)
            {
                return true;
            }

            return FurthestOut(ellipse, other, tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets how far the rim of the other reaches outside the ellipse at most, stopping early once it is past a limit.
        /// </summary>
        private static double FurthestOut(GeoEllipse2 ellipse, GeoEllipse2 other, double limit)
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
                reach[k] = Signed(ellipse, GetPointAtAngle(other, starts[k]));

                if (reach[k] > limit)
                {
                    return reach[k];
                }
            }

            double furthest = double.NegativeInfinity;

            for (int k = 0; k < Starts; k++)
            {
                int before = (k + Starts - 1) % Starts;
                int after = (k + 1) % Starts;

                if (reach[k] < reach[before] || reach[k] < reach[after])
                {
                    continue;
                }

                double low = starts[k] - Positive(starts[k] - starts[before]);
                double high = starts[k] + Positive(starts[after] - starts[k]);

                // Each side on its own, as for the distance between two rims.
                for (int side = 0; side < 2; side++)
                {
                    double from = side == 0 ? low : starts[k];
                    double to = side == 0 ? starts[k] : high;
                    Least(t => -Signed(ellipse, GetPointAtAngle(other, t)), from, 0.5 * (from + to), to, out double value);

                    furthest = Math.Max(furthest, Math.Max(reach[k], -value));
                }

                if (furthest > limit)
                {
                    break;
                }
            }

            return furthest;
        }

        #endregion
    }
}
