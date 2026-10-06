using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What can be asked of an ellipse: how long its rim is, where a point falls on it, whether a point lies inside it,
    /// how it moves, and how it is cut into straight pieces.
    /// <para>
    /// An ellipse is a region, as a circle is: it has an area, and a point inside it is in it. Its rim is walked by the
    /// eccentric angle t, the point Center + a cos t MajorAxis + b sin t MinorAxis, counter-clockwise from the end of the
    /// major axis. That is not the direction seen from the centre: on an ellipse 300 by 100, t = 45° is the point
    /// (212.1, 70.7), which lies 18.4° from the axis seen from the centre.
    /// </para>
    /// <para>
    /// Every answer is exact or worked out to rounding, none of them sampled: the length of the rim by the
    /// arithmetic-geometric mean, a length part of the way round by Carlson's elliptic integrals, and the point of the rim
    /// nearest a point by Eberly's method. An ellipse whose radii are equal is a circle, and answers as one.
    /// </para>
    /// </summary>
    public static partial class Ellipse2
    {
        private const double FullTurn = Math.PI * 2.0;

        #region Measurements

        /// <summary>
        /// Gets the length of the rim of an ellipse: its perimeter.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <returns>The perimeter. An ellipse 300 by 100 measures 1 336.489 round; one 100 by 100 measures 200π, as the circle does.</returns>
        /// <remarks>
        /// Worked out by the arithmetic-geometric mean of Gauss and Kummer, which converges to the last digit in a handful
        /// of rounds however thin the ellipse is.
        /// </remarks>
        public static double GetLength(GeoEllipse2 ellipse)
        {
            return ellipse.MajorRadius * UnitPerimeter(ellipse.MinorRadius / ellipse.MajorRadius);
        }

        /// <summary>
        /// Determines whether an ellipse is a circle, its two radii the same within the default tolerance.
        /// </summary>
        public static bool IsCircle(GeoEllipse2 ellipse) => IsCircle(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse is a circle, its two radii the same within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance: the radii may differ by up to its point tolerance.</param>
        /// <returns>true if the radii differ by no more than the point tolerance; otherwise, false.</returns>
        public static bool IsCircle(GeoEllipse2 ellipse, Tolerance tolerance)
        {
            return ellipse.MajorRadius - ellipse.MinorRadius <= tolerance.EqualPoint;
        }

        #endregion

        #region Angles, parameters and distances along the rim

        /// <summary>
        /// Gets the point of the rim of an ellipse at an eccentric angle.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="angleRad">
        /// The eccentric angle t in radians, counter-clockwise from the end of the major axis; not the direction seen from
        /// the centre. On an ellipse 300 by 100 centred on the origin along X, t = 45° is the point (212.1, 70.7), which lies
        /// 18.4° from the axis seen from the centre.
        /// </param>
        /// <returns>The point Center + a cos t MajorAxis + b sin t MinorAxis.</returns>
        public static GeoPoint2 GetPointAtAngle(GeoEllipse2 ellipse, double angleRad)
        {
            return OnRim(ellipse, ellipse.MajorRadius * Math.Cos(angleRad), ellipse.MinorRadius * Math.Sin(angleRad));
        }

        /// <summary>
        /// Gets the eccentric angle of the point of the rim of an ellipse nearest a point.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point; it does not have to lie on the rim.</param>
        /// <returns>
        /// The eccentric angle t of the nearest point of the rim, in radians from nought up to a full turn, counter-clockwise
        /// from the end of the major axis; not the direction seen from the centre. On an ellipse 300 by 100 the point
        /// (212.1, 70.7) gives 45°, though it lies 18.4° from the axis seen from the centre.
        /// </returns>
        /// <remarks>
        /// The nearest point is the one <see cref="GetClosestPointOnBoundary(GeoEllipse2, GeoPoint2)"/> gives, so the
        /// centre gives a quarter turn, the end of the minor axis on its positive side, a circle's centre too; and a point
        /// on the major axis inside, which two points of the rim are equally near, gives the one on the positive side of it.
        /// </remarks>
        public static double GetAngleAtPoint(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            Closest(ellipse, point, out double cos, out double sin);

            double angle = Math.Atan2(sin, cos);

            if (angle < 0.0)
            {
                angle += FullTurn;
            }

            return angle < FullTurn ? angle : 0.0;
        }

        /// <summary>
        /// Gets the point of the rim of an ellipse at a normalized parameter, where 0 is the end of the major axis and 1
        /// the same point after a whole turn counter-clockwise. The parameter wraps, so 1.25 gives the same point as 0.25.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="parameter">
        /// The parameter, uniform in the eccentric angle: p is t = 2πp, which is not a share of the length round. On an
        /// ellipse 300 by 100, 0.125 is t = 45°, the point (212.1, 70.7), 18.4° from the axis seen from the centre.
        /// </param>
        public static GeoPoint2 GetPointAtParameter(GeoEllipse2 ellipse, double parameter)
        {
            return GetPointAtAngle(ellipse, Wrap(parameter) * FullTurn);
        }

        /// <summary>
        /// Gets the normalized parameter of the point of the rim of an ellipse nearest a point: its eccentric angle as a
        /// share of a whole turn, from 0 up to 1.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point; it does not have to lie on the rim.</param>
        public static double GetParameterAtPoint(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            return GetAngleAtPoint(ellipse, point) / FullTurn;
        }

        /// <summary>
        /// Gets the length along the rim of an ellipse from the end of its major axis, counter-clockwise, to a normalized
        /// parameter.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="parameter">The parameter, uniform in the eccentric angle. It does not wrap: 1.25 is a whole turn and a quarter, and a negative parameter gives a negative length, as a circle's does.</param>
        /// <returns>The true length along the rim. On an ellipse 300 by 100, 0.25 is a quarter of 1 336.489, 334.122, exactly a quarter of <see cref="GetLength(GeoEllipse2)"/>.</returns>
        /// <remarks>
        /// The whole quarter turns are taken out first, each adding exactly a quarter of the perimeter; what is left is an
        /// incomplete elliptic integral of the second kind, worked out by Carlson's R_F and R_D to the last digit.
        /// </remarks>
        public static double GetDistanceAtParameter(GeoEllipse2 ellipse, double parameter)
        {
            if (!Guard.IsFinite(parameter))
            {
                return parameter * GetLength(ellipse);
            }

            return DistanceAtQuarters(ellipse, 4.0 * parameter);
        }

        /// <summary>
        /// Gets the normalized parameter at a length measured along the rim of an ellipse from the end of its major axis,
        /// counter-clockwise.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="distance">The length along the rim. It does not wrap: a length and a half round gives 1.5, and a negative length a negative parameter, as a circle's does.</param>
        /// <returns>The parameter, uniform in the eccentric angle; 0 for an ellipse with no rim to measure along.</returns>
        /// <remarks>
        /// The whole quarter turns are taken out first; within the quarter left, Newton's method on the length, kept inside
        /// a bracket it halves whenever a step would leave it, finds the angle to the last digit.
        /// </remarks>
        public static double GetParameterAtDistance(GeoEllipse2 ellipse, double distance)
        {
            double length = GetLength(ellipse);

            if (!(length > 0.0))
            {
                return 0.0;
            }

            if (!Guard.IsFinite(distance))
            {
                return distance / length;
            }

            double quarter = 0.25 * length;
            double turns = Math.Floor(distance / quarter);
            double left = distance - turns * quarter;

            if (left < 0.0)
            {
                left = 0.0;
            }
            else if (left >= quarter)
            {
                turns += 1.0;
                left = Math.Max(0.0, left - quarter);
            }

            if (!(left > 0.0))
            {
                return 0.25 * turns;
            }

            double a = ellipse.MajorRadius;
            double ratio = ellipse.MinorRadius / a;

            // The odd quarters run back from the end of the minor axis, mirrored, as the even ones run out from the major.
            double share = IsOdd(turns)
                ? 1.0 - UnitAngleAtArc(ratio, (quarter - left) / a) / HalfPi
                : UnitAngleAtArc(ratio, left / a) / HalfPi;

            return 0.25 * (turns + share);
        }

        /// <summary>
        /// Gets the point at a length measured along the rim of an ellipse from the end of its major axis,
        /// counter-clockwise. The length wraps, so a length and a quarter round is the same point as a quarter.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="distance">The length along the rim.</param>
        public static GeoPoint2 GetPointAtDistance(GeoEllipse2 ellipse, double distance)
        {
            return GetPointAtParameter(ellipse, GetParameterAtDistance(ellipse, distance));
        }

        /// <summary>
        /// Gets the length along the rim of an ellipse from the end of its major axis, counter-clockwise, to the point of the
        /// rim nearest a point.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point; it does not have to lie on the rim.</param>
        /// <returns>The length, from nought up to the perimeter.</returns>
        public static double GetDistanceAtPoint(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            return DistanceAtQuarters(ellipse, GetAngleAtPoint(ellipse, point) / HalfPi);
        }

        #endregion

        #region The nearest point of the rim

        /// <summary>
        /// Gets the point of the rim of an ellipse nearest a point, including for points inside it.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <returns>The nearest point of the rim.</returns>
        /// <remarks>
        /// <para>
        /// Worked out by Eberly's robust method: the point is reflected into the first quadrant of the ellipse's own frame,
        /// the one root of the equation of the nearest point found there by halving a bracket, and the answer reflected
        /// back. It is right at the centre, on both axes, inside and outside, and for an ellipse a thousand times longer
        /// than it is wide.
        /// </para>
        /// <para>
        /// Where two points of the rim are equally near, one is picked. The centre of an ellipse 300 by 100 is 100 from the
        /// two ends of the minor axis, and the one at t = 90° is given. A point on the major axis inside, nearer the centre
        /// than the centre of curvature of the end of the axis, is nearest two points mirrored across the axis, and the one
        /// on the positive side of the minor axis is given. The centre of a circle is as near every point of the rim, and
        /// the end of the minor axis at t = 90° is given there too, so that the answer does not jump between radii equal
        /// and radii a hair apart; <see cref="GeoCircle2.GetClosestPointOnBoundary(GeoPoint2)"/> gives the point at angle
        /// nought instead.
        /// </para>
        /// </remarks>
        public static GeoPoint2 GetClosestPointOnBoundary(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            Closest(ellipse, point, out double cos, out double sin);

            return OnRim(ellipse, ellipse.MajorRadius * cos, ellipse.MinorRadius * sin);
        }

        #endregion

        #region Points inside and on the rim

        /// <summary>
        /// Checks whether an ellipse contains a point, using the default tolerance. A point on the rim is contained.
        /// </summary>
        public static bool Contains(GeoEllipse2 ellipse, GeoPoint2 point) => Contains(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Checks whether an ellipse contains a point, within a tolerance. A point on the rim, or outside it by no more than
        /// the point tolerance, is contained, as it is by a circle.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance: how far from the rim, in drawing units, a point still lies on it.</param>
        /// <returns>true if the point lies inside the ellipse or on its rim; otherwise, false.</returns>
        public static bool Contains(GeoEllipse2 ellipse, GeoPoint2 point, Tolerance tolerance)
        {
            return Locate(ellipse, point, tolerance) != PointLocation.OutSide;
        }

        /// <summary>
        /// Says where a point sits relative to an ellipse, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoEllipse2 ellipse, GeoPoint2 point) => Locate(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance: how far from the rim, in drawing units, a point still lies on it.</param>
        /// <returns><see cref="PointLocation.OnSide"/> within the point tolerance of the rim; otherwise <see cref="PointLocation.Inside"/> or <see cref="PointLocation.OutSide"/>.</returns>
        /// <remarks>
        /// The gap to the rim is measured as a distance, to the nearest point of the rim, so a point a thousandth off the
        /// rim is on it whether it lies by the end of the major axis or of the minor one. Reading how far the ellipse's own
        /// equation is from one instead would put a band round the rim as wide as the radius it is read along.
        /// </remarks>
        public static PointLocation Locate(GeoEllipse2 ellipse, GeoPoint2 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across);

            double bound = Below(ellipse, along, across);
            double slack = Slack(ellipse, along, across, tolerance);

            if (bound < -slack)
            {
                return PointLocation.Inside;
            }

            if (bound > slack)
            {
                return PointLocation.OutSide;
            }

            if (Gap(ellipse, along, across) <= tolerance.EqualPoint)
            {
                return PointLocation.OnSide;
            }

            return bound < 0.0 ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Checks whether a point lies on the rim of an ellipse, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoEllipse2 ellipse, GeoPoint2 point) => IsPointOn(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the rim of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance: how far from the rim, in drawing units, a point still lies on it.</param>
        /// <returns>true if the nearest point of the rim is no further than the point tolerance; otherwise, false.</returns>
        public static bool IsPointOn(GeoEllipse2 ellipse, GeoPoint2 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across);

            if (Math.Abs(Below(ellipse, along, across)) > Slack(ellipse, along, across, tolerance))
            {
                return false;
            }

            return Gap(ellipse, along, across) <= tolerance.EqualPoint;
        }

        #endregion

        #region Moving

        /// <summary>
        /// Moves an ellipse by a vector, keeping its radii and its axis exactly.
        /// </summary>
        public static GeoEllipse2 Translate(GeoEllipse2 ellipse, GeoVector2 vector)
        {
            return new GeoEllipse2(ellipse.Center.Add(vector), ellipse.MajorAxis, ellipse.MajorRadius, ellipse.MinorRadius, true);
        }

        /// <summary>
        /// Turns an ellipse about a point, keeping its radii exactly; the zero of its eccentric angle turns with it.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="angleRad">The angle in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn about.</param>
        public static GeoEllipse2 RotateBy(GeoEllipse2 ellipse, double angleRad, GeoPoint2 center)
        {
            return new GeoEllipse2(
                ellipse.Center.RotateBy(angleRad, center),
                ellipse.MajorAxis.RotateBy(angleRad),
                ellipse.MajorRadius,
                ellipse.MinorRadius,
                true);
        }

        /// <summary>
        /// Applies a transformation to an ellipse. Every transformation that does not flatten the plane gives an ellipse:
        /// a move, a turn, a mirror, a scaling even or uneven, and a shear.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="transform">The transformation.</param>
        /// <returns>The transformed ellipse.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation flattens the plane onto a line or a point: when the area it scales by is no more
        /// than 1E-12 of the square of its size.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The radii and the major axis come from the singular values of the transformation applied to the two semi-axes:
        /// a circle of radius 100 scaled by 3 along X and 1 along Y is the ellipse 300 by 100 along X. The zero of the
        /// eccentric angle goes to the end of the new major axis nearer where the old one went, so it is generally not the
        /// image of the old zero. When the two new radii agree to 1E-12 the result is a circle, and its major axis is where
        /// the old one went, so a turn carries the zero of the angle round with it.
        /// </para>
        /// <para>
        /// A transformation that turns the plane over, a mirror or a negative scaling, reverses the way the eccentric angle
        /// runs round the rim as seen in the old frame: the new ellipse still runs counter-clockwise, so the points the old
        /// one reached at increasing angles are reached by the new one at decreasing angles.
        /// </para>
        /// </remarks>
        public static GeoEllipse2 TransformBy(GeoEllipse2 ellipse, GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoVector2 columnX = transform.Transform(GeoVector2.XAxis);
            GeoVector2 columnY = transform.Transform(GeoVector2.YAxis);
            double determinant = transform.GetDeterminant();

            if (!(Math.Abs(determinant) > 1E-12 * (columnX.LengthSquared + columnY.LengthSquared)))
            {
                throw new InvalidOperationException("This transformation flattens the ellipse onto a line or a point.");
            }

            GeoVector2 along = transform.Transform(ellipse.MajorAxis.Multiply(ellipse.MajorRadius));
            GeoVector2 across = transform.Transform(ellipse.MinorAxis.Multiply(ellipse.MinorRadius));
            GeoPoint2 center = transform.Transform(ellipse.Center);

            // The closed form of the singular values of a 2 by 2 matrix, whose columns are the images of the semi-axes.
            double e = 0.5 * (along.X + across.Y);
            double f = 0.5 * (along.X - across.Y);
            double g = 0.5 * (along.Y + across.X);
            double h = 0.5 * (along.Y - across.X);
            double q = Math.Sqrt(e * e + h * h);
            double r = Math.Sqrt(f * f + g * g);
            double major = q + r;

            // The smaller is the area over the larger, which keeps its digits where q - r would cancel them away.
            double minor = Math.Min(major, Math.Abs(determinant) * ellipse.MajorRadius * ellipse.MinorRadius / major);

            if (major - minor <= 1E-12 * major)
            {
                return new GeoEllipse2(center, along.Multiply(1.0 / along.Length), major, major, true);
            }

            double angle = 0.5 * (Math.Atan2(h, e) + Math.Atan2(g, f));
            var axis = new GeoVector2(Math.Cos(angle), Math.Sin(angle));

            // The axis is a direction either way; the one is taken nearer where the old axis went, then the old minor.
            double onAlong = axis.DotProduct(along);

            if (onAlong < -1E-12 * along.Length || (!(onAlong > 1E-12 * along.Length) && axis.DotProduct(across) < 0.0))
            {
                axis = axis.Multiply(-1.0);
            }

            return new GeoEllipse2(center, axis, major, minor, true);
        }

        #endregion

        #region Straight pieces

        /// <summary>
        /// Approximates the rim of an ellipse as a polygon that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chordTolerance">The largest gap allowed between an edge and the rim, in drawing units. Zero picks <see cref="Internal.Tessellation.AutomaticChordRatio"/> of the minor radius, which is the circle's share when the radii are equal.</param>
        /// <returns>
        /// A polygon inscribed in the ellipse, counter-clockwise from the end of the major axis, its first point not
        /// repeated; symmetric about both axes, with a corner at each end of each.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        /// <remarks>
        /// The corners are spaced by the bend of the rim, close where it turns sharply at the ends of the major axis and
        /// far apart along the flat sides: an ellipse 1 000 by 1 at the automatic tolerance of 0.002 takes 84 edges, where
        /// an even step in the angle would need about 1 571. A circle takes as many as
        /// <see cref="GeoCircle2.ToPolygonByChordTolerance(double)"/> gives, or up to three more to make a multiple of four;
        /// no more than <see cref="Internal.Tessellation.MaxSegmentsPerTurn"/> in all, then spread evenly in the angle.
        /// </remarks>
        public static GeoPolygon2 ToPolygonByChordTolerance(GeoEllipse2 ellipse, double chordTolerance)
        {
            return new GeoPolygon2(ByChord(ellipse, chordTolerance, false));
        }

        /// <summary>
        /// Approximates the rim of an ellipse as a polygon whose corners are evenly spread along it, no two further apart
        /// along the rim than a spacing.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="spacing">The largest length allowed along the rim between two corners.</param>
        /// <returns>A polygon inscribed in the ellipse, counter-clockwise from the end of the major axis.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public static GeoPolygon2 ToPolygonBySpacing(GeoEllipse2 ellipse, double spacing)
        {
            return new GeoPolygon2(BySpacing(ellipse, spacing, false));
        }

        /// <summary>
        /// Approximates the rim of an ellipse as a polygon with a given number of edges, its corners evenly spread in the
        /// eccentric angle.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="segmentCount">How many edges; at least three.</param>
        /// <returns>A polygon inscribed in the ellipse, its corners at the parameters 0, 1/n, 2/n and so on.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three edges are asked for.</exception>
        public static GeoPolygon2 ToPolygon(GeoEllipse2 ellipse, int segmentCount)
        {
            return new GeoPolygon2(ByCount(ellipse, segmentCount, false));
        }

        /// <summary>
        /// Approximates the rim of an ellipse as a chain that strays no further from it than a chord tolerance, its first
        /// point repeated at the end.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>The corners of <see cref="ToPolygonByChordTolerance(GeoEllipse2, double)"/>, the first repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public static GeoPolyline2 ToPolylineByChordTolerance(GeoEllipse2 ellipse, double chordTolerance)
        {
            return new GeoPolyline2(ByChord(ellipse, chordTolerance, true));
        }

        /// <summary>
        /// Approximates the rim of an ellipse as a chain whose points are evenly spread along it, no two further apart along
        /// the rim than a spacing, its first point repeated at the end.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="spacing">The largest length allowed along the rim between two points.</param>
        /// <returns>The corners of <see cref="ToPolygonBySpacing(GeoEllipse2, double)"/>, the first repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public static GeoPolyline2 ToPolylineBySpacing(GeoEllipse2 ellipse, double spacing)
        {
            return new GeoPolyline2(BySpacing(ellipse, spacing, true));
        }

        /// <summary>
        /// Approximates the rim of an ellipse as a chain with a given number of pieces, its points evenly spread in the
        /// eccentric angle and its first point repeated at the end.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="segmentCount">How many pieces; at least three.</param>
        /// <returns>The corners of <see cref="ToPolygon(GeoEllipse2, int)"/>, the first repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three pieces are asked for.</exception>
        public static GeoPolyline2 ToPolyline(GeoEllipse2 ellipse, int segmentCount)
        {
            return new GeoPolyline2(ByCount(ellipse, segmentCount, true));
        }

        /// <summary>
        /// Breaks the region of an ellipse into triangles fanned from its centre, the rim cut by a chord tolerance, using
        /// the default tolerance.
        /// </summary>
        public static GeoTriangle2[] TriangulateSurface(GeoEllipse2 ellipse, double chordTolerance) => TriangulateSurface(ellipse, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Breaks the region of an ellipse into triangles fanned from its centre, the rim cut by a chord tolerance, within
        /// a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <param name="tolerance">The tolerance deciding what counts as no area: a minor radius no more than its point tolerance, or a triangle of none.</param>
        /// <returns>
        /// The triangles, counter-clockwise, one for each edge of <see cref="ToPolygonByChordTolerance(GeoEllipse2, double)"/>
        /// with any area; none when the minor radius is no more than the point tolerance, as <see cref="Meshing.Mesh2"/>
        /// leaves it.
        /// </returns>
        public static GeoTriangle2[] TriangulateSurface(GeoEllipse2 ellipse, double chordTolerance, Tolerance tolerance)
        {
            if (!(ellipse.MinorRadius > tolerance.EqualPoint))
            {
                return Array.Empty<GeoTriangle2>();
            }

            return Triangulation2.Fan(ellipse.Center, ToPolygonByChordTolerance(ellipse, chordTolerance).Vertices, tolerance);
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether two ellipses are the same shape in the same place, within the default tolerance.
        /// </summary>
        public static bool IsEqualTo(GeoEllipse2 ellipse, GeoEllipse2 other) => IsEqualTo(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Determines whether two ellipses are the same shape in the same place, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The second ellipse.</param>
        /// <param name="tolerance">The tolerance: the point tolerance for the centres and the radii, the vector tolerance for the axes.</param>
        /// <returns>true if the centres and both radii agree within the point tolerance and the major axes along the same line within the vector tolerance; otherwise, false.</returns>
        /// <remarks>
        /// An axis pointing the other way draws the same ellipse, so the axes are compared either way round; when both
        /// ellipses are circles they are not compared at all.
        /// </remarks>
        public static bool IsEqualTo(GeoEllipse2 ellipse, GeoEllipse2 other, Tolerance tolerance)
        {
            if (!ellipse.Center.IsEqualTo(other.Center, tolerance)
                || !(Math.Abs(ellipse.MajorRadius - other.MajorRadius) <= tolerance.EqualPoint)
                || !(Math.Abs(ellipse.MinorRadius - other.MinorRadius) <= tolerance.EqualPoint))
            {
                return false;
            }

            if (IsCircle(ellipse, tolerance) && IsCircle(other, tolerance))
            {
                return true;
            }

            return ellipse.MajorAxis.IsEqualTo(other.MajorAxis, tolerance) || ellipse.MajorAxis.IsEqualTo(other.MajorAxis.Multiply(-1.0), tolerance);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the point at a place in the ellipse's own frame: so far along the major axis and so far along the minor.
        /// </summary>
        private static GeoPoint2 OnRim(GeoEllipse2 ellipse, double along, double across)
        {
            GeoVector2 major = ellipse.MajorAxis;

            return new GeoPoint2(
                ellipse.Center.X + (along * major.X - across * major.Y),
                ellipse.Center.Y + (along * major.Y + across * major.X));
        }

        /// <summary>
        /// Gets how far a point lies from the centre along the major axis and along the minor.
        /// </summary>
        private static void Frame(GeoEllipse2 ellipse, GeoPoint2 point, out double along, out double across)
        {
            double dx = point.X - ellipse.Center.X;
            double dy = point.Y - ellipse.Center.Y;
            GeoVector2 major = ellipse.MajorAxis;

            along = dx * major.X + dy * major.Y;
            across = dy * major.X - dx * major.Y;
        }

        /// <summary>
        /// Gets the cosine and sine of the eccentric angle of the point of the rim nearest a point.
        /// </summary>
        private static void Closest(GeoEllipse2 ellipse, GeoPoint2 point, out double cos, out double sin)
        {
            Frame(ellipse, point, out double along, out double across);
            Closest(ellipse, along, across, out cos, out sin);
        }

        private static void Closest(GeoEllipse2 ellipse, double along, double across, out double cos, out double sin)
        {
            double a = ellipse.MajorRadius;

            UnitClosest(ellipse.MinorRadius / a, Math.Abs(along) / a, Math.Abs(across) / a, out cos, out sin);

            // Reflected back out of the first quadrant; a point on an axis stays on the positive side of it.
            if (along < 0.0)
            {
                cos = -cos;
            }

            if (across < 0.0)
            {
                sin = -sin;
            }
        }

        /// <summary>
        /// Gets the distance from a point, given in the ellipse's own frame, to the nearest point of the rim.
        /// </summary>
        private static double Gap(GeoEllipse2 ellipse, double along, double across)
        {
            Closest(ellipse, along, across, out double cos, out double sin);

            double dx = ellipse.MajorRadius * cos - along;
            double dy = ellipse.MinorRadius * sin - across;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Gets a bound the gap to the rim cannot be less than: negative inside, positive outside.
        /// </summary>
        /// <remarks>
        /// The ellipse scaled about its centre to pass through the point is q times the size, and no point of the one lies
        /// nearer the other than (q - 1) times the minor radius, the narrowest the ellipse is. So a point further from the
        /// rim by that measure than the tolerance is inside or outside without working out the nearest point.
        /// </remarks>
        private static double Below(GeoEllipse2 ellipse, double along, double across)
        {
            double scaled = along * (ellipse.MinorRadius / ellipse.MajorRadius);

            return Math.Sqrt(scaled * scaled + across * across) - ellipse.MinorRadius;
        }

        /// <summary>
        /// Gets how far the bound has to clear nought for the answer to be sure: the tolerance, and enough for rounding.
        /// </summary>
        private static double Slack(GeoEllipse2 ellipse, double along, double across, Tolerance tolerance)
        {
            return tolerance.EqualPoint + 1E-12 * (ellipse.MajorRadius + Math.Abs(along) + Math.Abs(across));
        }

        /// <summary>
        /// Gets the length along the rim from the end of the major axis to an eccentric angle given in quarter turns.
        /// </summary>
        private static double DistanceAtQuarters(GeoEllipse2 ellipse, double quarters)
        {
            double quarter = 0.25 * GetLength(ellipse);
            double turns = Math.Floor(quarters);
            double share = quarters - turns;

            if (!(share > 0.0))
            {
                return turns * quarter;
            }

            double a = ellipse.MajorRadius;
            double ratio = ellipse.MinorRadius / a;

            // The rim runs through the odd quarters as the even ones mirrored, so their length is read back from their end.
            return IsOdd(turns)
                ? (turns + 1.0) * quarter - a * UnitArc(ratio, (1.0 - share) * HalfPi)
                : turns * quarter + a * UnitArc(ratio, share * HalfPi);
        }

        private static bool IsOdd(double turns) => turns - 2.0 * Math.Floor(0.5 * turns) > 0.5;

        private static double Wrap(double parameter)
        {
            double wrapped = parameter % 1.0;
            if (wrapped < 0.0) wrapped += 1.0;
            if (wrapped >= 1.0) wrapped = 0.0;
            return wrapped;
        }

        /// <summary>
        /// Gets the corners of the rim spaced by its bend so that no chord strays further than a tolerance.
        /// </summary>
        /// <remarks>
        /// A chord over the eccentric angles t to t + h strays furthest from the rim at the middle angle, the point where
        /// the rim runs parallel to it, by exactly 2 sin²(h/4) ab / (a² sin² m + b² cos² m)^½ at the middle angle m. The
        /// first quarter is walked out from the end of the major axis, each step the longest that keeps that within the
        /// tolerance, and mirrored into the other three. The rim bends less and less along the first quarter, so a step
        /// worked out at its start is safe and is lengthened while it stays so.
        /// </remarks>
        private static GeoPoint2[] ByChord(GeoEllipse2 ellipse, double chordTolerance, bool closed)
        {
            // Through the circle's checks: the tolerance a distance or nought, the radius a positive number.
            Internal.Tessellation.SegmentsForChordTolerance(ellipse.MinorRadius, FullTurn, chordTolerance);

            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double error = chordTolerance > 0.0 ? chordTolerance : b * Internal.Tessellation.AutomaticChordRatio;
            int most = Internal.Tessellation.MaxSegmentsPerTurn / 4;
            var angles = new List<double> { 0.0 };
            double angle = 0.0;
            double last = 0.0;

            while (true)
            {
                double step = Step(a, b, error, angle);

                for (int round = 0; round < 3; round++)
                {
                    step = Step(a, b, error, Math.Min(angle + 0.5 * step, HalfPi));
                }

                // The step is safe but for rounding, which a circle's own step meets exactly; one found over by more is
                // shortened until it is not.
                while (Sag(a, b, angle, step) > error * (1.0 + 1E-12))
                {
                    step *= 0.999;
                }

                // What is left fits when its own chord does, which the step summed so far could miss by rounding.
                if (HalfPi - angle <= step || Sag(a, b, angle, HalfPi - angle) <= error * (1.0 + 1E-12))
                {
                    break;
                }

                if (angles.Count >= most)
                {
                    angles = null;
                    break;
                }

                angle += step;
                last = step;
                angles.Add(angle);
            }

            if (angles == null)
            {
                angles = new List<double>(most + 1);

                for (int i = 0; i < most; i++)
                {
                    angles.Add(HalfPi * i / most);
                }
            }
            else if (angles.Count > 1 && HalfPi - angles[angles.Count - 1] < 0.5 * last)
            {
                // A sliver of a last step is shared with the one before, so that no corner stands a hair from the end of
                // the minor axis; both halves are shorter than the step before, and so as safe.
                angles[angles.Count - 1] = 0.5 * (angles[angles.Count - 2] + HalfPi);
            }

            angles.Add(HalfPi);

            int n = angles.Count - 1;
            var along = new double[n + 1];
            var across = new double[n + 1];

            for (int i = 0; i <= n; i++)
            {
                along[i] = a * Math.Cos(angles[i]);
                across[i] = b * Math.Sin(angles[i]);
            }

            along[0] = a;
            across[0] = 0.0;
            along[n] = 0.0;
            across[n] = b;

            var corners = new List<GeoPoint2>(4 * n + 1);

            for (int i = 0; i <= n; i++)
            {
                corners.Add(OnRim(ellipse, along[i], across[i]));
            }

            for (int i = n - 1; i >= 0; i--)
            {
                corners.Add(OnRim(ellipse, -along[i], across[i]));
            }

            for (int i = 1; i <= n; i++)
            {
                corners.Add(OnRim(ellipse, -along[i], -across[i]));
            }

            for (int i = n - 1; i >= 1; i--)
            {
                corners.Add(OnRim(ellipse, along[i], -across[i]));
            }

            if (closed)
            {
                corners.Add(corners[0]);
            }

            return corners.ToArray();
        }

        /// <summary>
        /// Gets the step in the eccentric angle whose chord strays from the rim by the tolerance, read at an angle.
        /// </summary>
        private static double Step(double a, double b, double error, double at)
        {
            double sin = Math.Sin(at);
            double cos = Math.Cos(at);

            // ab / (a² sin² + b² cos²)^½ is the radius of the circle that would give the same sag; a circle's own radius.
            // The circle's step, 2 acos(1 - error / radius), written as the arc sine of the half-angle so that a tolerance
            // far below the radius keeps its digits: the arc cosine of a number a hair under one loses a third of them.
            double radius = a * b / Math.Sqrt(a * a * sin * sin + b * b * cos * cos);
            double share = error / (2.0 * radius);

            return share >= 1.0 ? FullTurn : 4.0 * Math.Asin(Math.Sqrt(share));
        }

        /// <summary>
        /// Gets how far the chord from an eccentric angle over a step strays from the rim at its middle.
        /// </summary>
        private static double Sag(double a, double b, double from, double step)
        {
            double middle = from + 0.5 * step;
            double sin = Math.Sin(middle);
            double cos = Math.Cos(middle);
            double quarter = Math.Sin(0.25 * step);

            return 2.0 * quarter * quarter * a * b / Math.Sqrt(a * a * sin * sin + b * b * cos * cos);
        }

        private static GeoPoint2[] BySpacing(GeoEllipse2 ellipse, double spacing, bool closed)
        {
            double length = GetLength(ellipse);
            int count = Math.Max(3, Internal.Tessellation.SegmentsForSpacing(length, FullTurn, spacing));
            var corners = new GeoPoint2[closed ? count + 1 : count];

            for (int i = 0; i < count; i++)
            {
                corners[i] = GetPointAtDistance(ellipse, length * i / count);
            }

            if (closed)
            {
                corners[count] = corners[0];
            }

            return corners;
        }

        private static GeoPoint2[] ByCount(GeoEllipse2 ellipse, int segmentCount, bool closed)
        {
            Internal.Tessellation.RequireSegmentCount(segmentCount, 3);

            var corners = new GeoPoint2[closed ? segmentCount + 1 : segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                corners[i] = GetPointAtParameter(ellipse, (double)i / segmentCount);
            }

            if (closed)
            {
                corners[segmentCount] = corners[0];
            }

            return corners;
        }

        #endregion
    }
}
