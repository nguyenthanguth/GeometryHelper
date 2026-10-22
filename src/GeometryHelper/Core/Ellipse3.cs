using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What can be asked of an ellipse in space: how long its rim is, where a point falls on it, whether a point lies on
    /// its disc, how it moves, how it looks seen square onto a plane, and how it is cut into straight pieces.
    /// <para>
    /// An ellipse in space is a flat region, as a circle in space is. Everything within its plane is
    /// <see cref="Ellipse2"/>'s arithmetic, read in the ellipse's own frame: the centre, the major axis, the minor axis
    /// and the normal. A point is read as how far it lies along the major axis, along the minor, and above the plane; the
    /// answers in the plane are put back the same way. So every answer is exact or worked out to rounding, none of them
    /// sampled, and an ellipse whose radii are equal answers as a <see cref="GeoCircle3"/> does.
    /// </para>
    /// </summary>
    public static partial class Ellipse3
    {
        #region Measurements

        /// <summary>
        /// Gets the length of the rim of an ellipse in space: its perimeter.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <returns>The perimeter: an ellipse 300 by 100 measures 1 336.489 round, as <see cref="Ellipse2.GetLength(GeoEllipse2)"/> gives it.</returns>
        public static double GetLength(GeoEllipse3 ellipse) => Ellipse2.GetLength(Flat(ellipse));

        /// <summary>
        /// Determines whether an ellipse in space is a circle, its two radii the same within the default tolerance.
        /// </summary>
        public static bool IsCircle(GeoEllipse3 ellipse) => IsCircle(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse in space is a circle, its two radii the same within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance: the radii may differ by up to its point tolerance.</param>
        /// <returns>true if the radii differ by no more than the point tolerance; otherwise, false.</returns>
        public static bool IsCircle(GeoEllipse3 ellipse, Tolerance tolerance) => Ellipse2.IsCircle(Flat(ellipse), tolerance);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing an ellipse in space, exactly.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <returns>The smallest box along the world's axes holding the whole disc.</returns>
        /// <remarks>
        /// Along each world axis the rim reaches the root of (a Uₖ)² + (b Vₖ)² either side of the centre, U and V the major
        /// and the minor axis: an ellipse 300 by 100 in the plan with its major axis along X reaches 300 along X and 100
        /// along Y, and the same ellipse turned 45° about Z reaches 223.6 along both.
        /// </remarks>
        public static GeoAabb3 GetAabb(GeoEllipse3 ellipse)
        {
            GeoVector3 major = ellipse.MajorAxis.Multiply(ellipse.MajorRadius);
            GeoVector3 minor = ellipse.MinorAxis.Multiply(ellipse.MinorRadius);

            var extent = new GeoVector3(
                Math.Sqrt(major.X * major.X + minor.X * minor.X),
                Math.Sqrt(major.Y * major.Y + minor.Y * minor.Y),
                Math.Sqrt(major.Z * major.Z + minor.Z * minor.Z));

            return new GeoAabb3(ellipse.Center.Subtract(extent), ellipse.Center.Add(extent));
        }

        #endregion

        #region Angles, parameters and distances along the rim

        /// <summary>
        /// Gets the point of the rim of an ellipse in space at an eccentric angle.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="angleRad">
        /// The eccentric angle t in radians, counter-clockwise about the normal from the end of the major axis; not the
        /// direction seen from the centre. On an ellipse 300 by 100, t = 45° is the point 212.1 along the major axis and
        /// 70.7 along the minor, which lies 18.4° from the axis seen from the centre.
        /// </param>
        /// <returns>The point Center + a cos t MajorAxis + b sin t MinorAxis.</returns>
        public static GeoPoint3 GetPointAtAngle(GeoEllipse3 ellipse, double angleRad)
        {
            return Lift(ellipse, Ellipse2.GetPointAtAngle(Flat(ellipse), angleRad));
        }

        /// <summary>
        /// Gets the eccentric angle of the point of the rim of an ellipse in space nearest a point.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point; it does not have to lie on the rim, nor on the plane, where it is read at its foot.</param>
        /// <returns>
        /// The eccentric angle of the nearest point of the rim, in radians from nought up to a full turn; a quarter turn for
        /// a point above the centre, as <see cref="Ellipse2.GetAngleAtPoint(GeoEllipse2, GeoPoint2)"/> gives for the centre.
        /// </returns>
        public static double GetAngleAtPoint(GeoEllipse3 ellipse, GeoPoint3 point)
        {
            return Ellipse2.GetAngleAtPoint(Flat(ellipse), Foot(ellipse, point));
        }

        /// <summary>
        /// Gets the point of the rim of an ellipse in space at a normalized parameter, uniform in the eccentric angle, where
        /// 0 is the end of the major axis and 1 the same point after a whole turn. The parameter wraps, so 1.25 gives the
        /// same point as 0.25.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="parameter">The parameter: p is t = 2πp, which is not a share of the length round.</param>
        public static GeoPoint3 GetPointAtParameter(GeoEllipse3 ellipse, double parameter)
        {
            return Lift(ellipse, Ellipse2.GetPointAtParameter(Flat(ellipse), parameter));
        }

        /// <summary>
        /// Gets the normalized parameter of the point of the rim of an ellipse in space nearest a point, using the default
        /// tolerance.
        /// </summary>
        public static double GetParameterAtPoint(GeoEllipse3 ellipse, GeoPoint3 point) => GetParameterAtPoint(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point of the rim of an ellipse in space nearest a point, within a tolerance:
        /// its eccentric angle as a share of a whole turn, from 0 up to 1.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point; it does not have to lie on the rim, nor on the plane.</param>
        /// <param name="tolerance">The tolerance: a point whose foot lies within its vector tolerance of the centre is read at the centre, as a circle's is.</param>
        /// <returns>
        /// The parameter; 0.25, the end of the minor axis at t = 90°, for a point at the centre or above it, where a circle
        /// in space gives 0.
        /// </returns>
        public static double GetParameterAtPoint(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            return Ellipse2.GetParameterAtPoint(Flat(ellipse), Foot(ellipse, point, tolerance));
        }

        /// <summary>
        /// Gets the length along the rim of an ellipse in space from the end of its major axis to a normalized parameter.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="parameter">The parameter, uniform in the eccentric angle. It wraps, as a circle's in space does, so 1.25 measures as 0.25.</param>
        /// <returns>The true length along the rim, from nought up to the perimeter; 0.25 is exactly a quarter of it.</returns>
        public static double GetDistanceAtParameter(GeoEllipse3 ellipse, double parameter)
        {
            return Ellipse2.GetDistanceAtParameter(Flat(ellipse), Ellipse2.Wrap(parameter));
        }

        /// <summary>
        /// Gets the normalized parameter at a length measured along the rim of an ellipse in space from the end of its major
        /// axis.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="distance">The length along the rim. It wraps, as a circle's in space does, so a length and a quarter round gives the parameter of a quarter.</param>
        /// <returns>The parameter, uniform in the eccentric angle, from 0 up to 1.</returns>
        public static double GetParameterAtDistance(GeoEllipse3 ellipse, double distance)
        {
            return Ellipse2.Wrap(Ellipse2.GetParameterAtDistance(Flat(ellipse), distance));
        }

        /// <summary>
        /// Gets the point at a length measured along the rim of an ellipse in space from the end of its major axis. The
        /// length wraps, so a length and a quarter round is the same point as a quarter.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="distance">The length along the rim.</param>
        public static GeoPoint3 GetPointAtDistance(GeoEllipse3 ellipse, double distance)
        {
            return Lift(ellipse, Ellipse2.GetPointAtDistance(Flat(ellipse), distance));
        }

        /// <summary>
        /// Gets the length along the rim of an ellipse in space from the end of its major axis to the point of the rim
        /// nearest a point, using the default tolerance.
        /// </summary>
        public static double GetDistanceAtPoint(GeoEllipse3 ellipse, GeoPoint3 point) => GetDistanceAtPoint(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Gets the length along the rim of an ellipse in space from the end of its major axis to the point of the rim
        /// nearest a point, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point; it does not have to lie on the rim, nor on the plane.</param>
        /// <param name="tolerance">The tolerance: a point whose foot lies within its vector tolerance of the centre is read at the centre, as a circle's is.</param>
        /// <returns>The length, from nought up to the perimeter; a quarter of it for a point at the centre or above it.</returns>
        public static double GetDistanceAtPoint(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            return Ellipse2.GetDistanceAtPoint(Flat(ellipse), Foot(ellipse, point, tolerance));
        }

        #endregion

        #region The nearest point, and how far

        /// <summary>
        /// Gets the point of the rim of an ellipse in space nearest a point.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point, anywhere.</param>
        /// <returns>The nearest point of the rim.</returns>
        /// <remarks>
        /// Every point of the rim stands as high above the point's foot on the plane as the point does, so the nearest
        /// point of the rim to the point is the nearest to its foot, which <see cref="Ellipse2.GetClosestPointOnBoundary(GeoEllipse2, GeoPoint2)"/>
        /// finds by Eberly's method; and the gap is the root of the height squared and the gap in the plane squared. Where two
        /// points of the rim are equally near, one is picked as in the plane: a point above the centre gives the end of the
        /// minor axis at t = 90°, a circle's centre too.
        /// </remarks>
        public static GeoPoint3 GetClosestPointOnBoundary(GeoEllipse3 ellipse, GeoPoint3 point)
        {
            return Lift(ellipse, Ellipse2.GetClosestPointOnBoundary(Flat(ellipse), Foot(ellipse, point)));
        }

        /// <summary>
        /// Gets the point of the disc of an ellipse in space nearest a point, the disc read as a filled surface.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point, anywhere.</param>
        /// <returns>
        /// The foot of the point on the plane when it lies within the rim, as a point above the interior lands straight down
        /// on the surface; otherwise the nearest point of the rim.
        /// </returns>
        public static GeoPoint3 GetClosestPointOnSurface(GeoEllipse3 ellipse, GeoPoint3 point) => GetClosestPointOnSurface(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the disc of an ellipse in space nearest a point, the disc read as a filled surface, within a
        /// tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point, anywhere.</param>
        /// <param name="tolerance">
        /// The tolerance, taken as a circle's is. It changes nothing: whether the foot lies within the rim is read exactly,
        /// and the rim is only asked for a point whose foot lies beyond it, far from where a tolerance decides anything.
        /// </param>
        /// <returns>The foot of the point on the plane when it lies within the rim; otherwise the nearest point of the rim.</returns>
        public static GeoPoint3 GetClosestPointOnSurface(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            if (Within(ellipse, along, across))
            {
                return point.Subtract(ellipse.Normal.Multiply(height));
            }

            return Lift(ellipse, Ellipse2.GetClosestPointOnBoundary(Flat(ellipse), new GeoPoint2(along, across)));
        }

        /// <summary>
        /// Calculates the shortest distance from the disc of an ellipse in space to a point.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point, anywhere.</param>
        /// <returns>
        /// The height of the point above the plane where its foot lies within the rim; otherwise the root of the height
        /// squared and the gap from the foot to the rim squared. A point 50 above the centre of an ellipse 300 by 100 is 50
        /// from it; one 40 above the plane and 30 beyond the end of the major axis is 50 from it.
        /// </returns>
        public static double DistanceTo(GeoEllipse3 ellipse, GeoPoint3 point) => DistanceTo(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from the disc of an ellipse in space to a point, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point, anywhere.</param>
        /// <param name="tolerance">The tolerance, taken as a circle's is; it changes nothing, as for <see cref="GetClosestPointOnSurface(GeoEllipse3, GeoPoint3, Tolerance)"/>.</param>
        /// <returns>The height above the plane where the foot lies within the rim; otherwise the gap to the nearest point of the rim.</returns>
        public static double DistanceTo(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            if (Within(ellipse, along, across))
            {
                return Math.Abs(height);
            }

            GeoPoint2 rim = Ellipse2.GetClosestPointOnBoundary(Flat(ellipse), new GeoPoint2(along, across));
            double du = rim.X - along;
            double dv = rim.Y - across;

            return Math.Sqrt(height * height + du * du + dv * dv);
        }

        /// <summary>
        /// Gets the shortest segment joining the disc of an ellipse in space to a point, using the default tolerance.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoEllipse3 ellipse, GeoPoint3 point) => GetShortestLineTo(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the disc of an ellipse in space to a point.
        /// </summary>
        /// <param name="ellipse">The ellipse, read as the filled disc its distance reads.</param>
        /// <param name="point">The point, anywhere.</param>
        /// <param name="tolerance">The tolerance, taken as a circle's is; it changes nothing, as for <see cref="GetClosestPointOnSurface(GeoEllipse3, GeoPoint3, Tolerance)"/>.</param>
        /// <returns>
        /// A segment from the point <see cref="GetClosestPointOnSurface(GeoEllipse3, GeoPoint3, Tolerance)"/> gives to
        /// <paramref name="point"/>, as long as <see cref="DistanceTo(GeoEllipse3, GeoPoint3, Tolerance)"/>: straight down
        /// where the foot lies within the rim, of no length for a point on the disc. From the ellipse 300 by 100 about the
        /// origin on the XY plane, the point (0, 0, 50) gets the segment from the centre, 50 long, and the point (330, 0, 40)
        /// the one from the end of the major axis at (300, 0, 0), also 50 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(GetClosestPointOnSurface(ellipse, point, tolerance), point);

        #endregion

        #region Points on the disc and on the rim

        /// <summary>
        /// Says where a point sits relative to the disc of an ellipse in space, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoEllipse3 ellipse, GeoPoint3 point) => Locate(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to the disc of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance: its planar tolerance says how far off the plane a point still lies on it, and its point tolerance how far from the rim a point still lies on the rim.</param>
        /// <returns>
        /// <see cref="PointLocation.OutSide"/> for a point further from the plane than the planar tolerance, as a point
        /// hovering above the centre is; otherwise where its foot lies to the rim in the plane, as
        /// <see cref="Ellipse2.Locate(GeoEllipse2, GeoPoint2, Tolerance)"/> reads it.
        /// </returns>
        public static PointLocation Locate(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            if (Math.Abs(height) > tolerance.EqualPlanar)
            {
                return PointLocation.OutSide;
            }

            return Ellipse2.Locate(Flat(ellipse), new GeoPoint2(along, across), tolerance);
        }

        /// <summary>
        /// Checks whether the disc of an ellipse in space holds a point, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoEllipse3 ellipse, GeoPoint3 point) => Contains(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Checks whether the disc of an ellipse in space holds a point, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance, read as <see cref="Locate(GeoEllipse3, GeoPoint3, Tolerance)"/> reads it.</param>
        /// <returns>true if the point lies on the plane and inside the rim or on it; otherwise, false.</returns>
        public static bool Contains(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(ellipse, point, tolerance) != PointLocation.OutSide;
        }

        /// <summary>
        /// Checks whether a point lies on the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoEllipse3 ellipse, GeoPoint3 point) => IsPointOn(ellipse, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance, read as <see cref="Locate(GeoEllipse3, GeoPoint3, Tolerance)"/> reads it.</param>
        /// <returns>true if the point lies on the plane and on the rim; otherwise, false.</returns>
        public static bool IsPointOn(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(ellipse, point, tolerance) == PointLocation.OnSide;
        }

        #endregion

        #region Moving, and seen from a plane

        /// <summary>
        /// Moves an ellipse in space by a vector, keeping its normal, its axis and its radii exactly.
        /// </summary>
        public static GeoEllipse3 Translate(GeoEllipse3 ellipse, GeoVector3 vector)
        {
            return new GeoEllipse3(ellipse.Center.Add(vector), ellipse.Normal, ellipse.MajorAxis, ellipse.MajorRadius, ellipse.MinorRadius, true);
        }

        /// <summary>
        /// Applies a transformation to an ellipse in space. Every transformation that does not flatten the ellipse's plane
        /// gives an ellipse: a move, a turn, a mirror, a scaling even or uneven, and a shear.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="transform">The transformation.</param>
        /// <returns>The transformed ellipse.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation cannot be undone, as <see cref="GeoTransform3.Transform(GeoPlane3)"/> throws for a
        /// circle's plane: judged by <see cref="GeoTransform3.TryGetInverse(out GeoTransform3)"/>, within the default
        /// tolerance. One that can be undone, however hard it squashes, gives a thin ellipse.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The images of the two semi-axes span the new plane, and the radii and the major axis come from their singular
        /// values in it, as <see cref="Ellipse2.TransformBy(GeoEllipse2, GeoTransform2)"/> finds them: a circle of radius
        /// 100 in the plan scaled by 3 along X is the ellipse 300 by 100 along X. The zero of the eccentric angle goes to the
        /// end of the new major axis nearer where the old one went; when the two new radii agree to 1E-12 the result is a
        /// circle, and its major axis is where the old one went.
        /// </para>
        /// <para>
        /// The normal goes where <see cref="GeoTransform3.Transform(GeoPlane3)"/> sends a plane's, and so where a circle's
        /// goes: a mirror in the plan turns an ellipse lying in it over, its normal down. Under any mirror the eccentric angle
        /// then reaches the images of the old rim's points in the opposite order: the old point at 30° goes to the new point
        /// at -30°.
        /// </para>
        /// </remarks>
        public static GeoEllipse3 TransformBy(GeoEllipse3 ellipse, GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            // Refused where a circle's plane is, and nowhere else.
            if (!transform.TryGetInverse(out _))
            {
                throw new InvalidOperationException("This transformation collapses space and cannot carry an ellipse.");
            }

            GeoVector3 along = transform.Transform(ellipse.MajorAxis.Multiply(ellipse.MajorRadius));
            GeoVector3 across = transform.Transform(ellipse.MinorAxis.Multiply(ellipse.MinorRadius));
            GeoVector3 spanned = along.CrossProduct(across);
            double area = spanned.Length;

            // The images of the normal and of the two semi-axes span a volume of the sign of the determinant, negative where
            // the transformation turns space over; the normal is turned over there, which is where a plane's normal, carried
            // by the inverse transpose, goes.
            GeoVector3 normal = spanned.Multiply(1.0 / area);

            if (transform.Transform(ellipse.Normal).DotProduct(spanned) < 0.0)
            {
                normal = normal.Multiply(-1.0);
            }

            return FromSemiAxes(transform.Transform(ellipse.Center), normal, along, across, area);
        }

        /// <summary>
        /// Gets an ellipse in space as it is seen square onto a plane: every point of it projected along the plane's normal.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="plane">The plane to project onto.</param>
        /// <returns>The ellipse in that plane, its normal the plane's whichever way the ellipse faced.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the ellipse stands square to the plane, where it would flatten into a segment rather than an ellipse:
        /// when the projections of its two semi-axes span no more than 1E-12 of the area the semi-axes span, so that it leans
        /// less than 1E-12 of a radian off square. One leaning more than that is given, however thin.
        /// </exception>
        /// <remarks>
        /// A circle of radius 300 tilted 60° from the plan is, in the plan, an ellipse 300 by 150, its major axis along the
        /// line the tilt turns about. The projection is a transformation of the ellipse's plane, so the arithmetic is that of
        /// <see cref="TransformBy(GeoEllipse3, GeoTransform3)"/>: a circle parallel to the plane stays a circle, its angle
        /// starting where the circle's does.
        /// </remarks>
        public static GeoEllipse3 ProjectToPlane(GeoEllipse3 ellipse, GeoPlane3 plane)
        {
            GeoVector3 normal = plane.Normal;
            GeoVector3 major = ellipse.MajorAxis.Multiply(ellipse.MajorRadius);
            GeoVector3 minor = ellipse.MinorAxis.Multiply(ellipse.MinorRadius);
            GeoVector3 along = major.Subtract(normal.Multiply(major.DotProduct(normal)));
            GeoVector3 across = minor.Subtract(normal.Multiply(minor.DotProduct(normal)));
            double area = along.CrossProduct(across).Length;

            if (!(area > 1E-12 * ellipse.MajorRadius * ellipse.MinorRadius))
            {
                throw new InvalidOperationException("The ellipse stands square to the plane, so it projects onto a segment.");
            }

            return FromSemiAxes(Projection3.ProjectToPlane(plane, ellipse.Center), normal, along, across, area);
        }

        /// <summary>
        /// Gets a circle in space as it is seen square onto a plane: an ellipse, or a circle where it lies parallel to the
        /// plane.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="plane">The plane to project onto.</param>
        /// <returns>The ellipse in that plane, its normal the plane's; a circle's angle starts where the circle's does.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the circle stands square to the plane, where it would flatten into a segment rather than an ellipse.</exception>
        /// <remarks>
        /// The circle is read as <see cref="GeoEllipse3.FromCircle(GeoCircle3)"/> makes it an ellipse: a circle of radius 300
        /// tilted 60° from the plan is, in the plan, an ellipse 300 by 150.
        /// </remarks>
        public static GeoEllipse3 ProjectToPlane(GeoCircle3 circle, GeoPlane3 plane) => ProjectToPlane(GeoEllipse3.FromCircle(circle), plane);

        #endregion

        #region Straight pieces

        /// <summary>
        /// Approximates the rim of an ellipse in space as a polygon that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chordTolerance">The largest gap allowed between an edge and the rim, in drawing units. Zero picks <see cref="Internal.Tessellation.AutomaticChordRatio"/> of the minor radius.</param>
        /// <returns>
        /// The polygon <see cref="Ellipse2.ToPolygonByChordTolerance(GeoEllipse2, double)"/> gives, put into the ellipse's
        /// plane: counter-clockwise about the normal from the end of the major axis, symmetric about both axes.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 corners.</exception>
        public static GeoPolygon3 ToPolygonByChordTolerance(GeoEllipse3 ellipse, double chordTolerance)
        {
            return new GeoPolygon3(Lift(ellipse, Ellipse2.ToPolygonByChordTolerance(Flat(ellipse), chordTolerance).Vertices));
        }

        /// <summary>
        /// Approximates the rim of an ellipse in space as a polygon whose corners are evenly spread along it, no two further
        /// apart along the rim than a spacing.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="spacing">The largest length allowed along the rim between two corners.</param>
        /// <returns>The polygon <see cref="Ellipse2.ToPolygonBySpacing(GeoEllipse2, double)"/> gives, put into the ellipse's plane.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public static GeoPolygon3 ToPolygonBySpacing(GeoEllipse3 ellipse, double spacing)
        {
            return new GeoPolygon3(Lift(ellipse, Ellipse2.ToPolygonBySpacing(Flat(ellipse), spacing).Vertices));
        }

        /// <summary>
        /// Approximates the rim of an ellipse in space as a polygon with a given number of edges, its corners evenly spread
        /// in the eccentric angle.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="segmentCount">How many edges; at least three.</param>
        /// <returns>The polygon <see cref="Ellipse2.ToPolygon(GeoEllipse2, int)"/> gives, put into the ellipse's plane.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three edges are asked for.</exception>
        public static GeoPolygon3 ToPolygon(GeoEllipse3 ellipse, int segmentCount)
        {
            return new GeoPolygon3(Lift(ellipse, Ellipse2.ToPolygon(Flat(ellipse), segmentCount).Vertices));
        }

        /// <summary>
        /// Approximates the rim of an ellipse in space as a chain that strays no further from it than a chord tolerance,
        /// its first point repeated at the end.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>The corners of <see cref="ToPolygonByChordTolerance(GeoEllipse3, double)"/>, the first repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 corners.</exception>
        public static GeoPolyline3 ToPolylineByChordTolerance(GeoEllipse3 ellipse, double chordTolerance)
        {
            return new GeoPolyline3(Lift(ellipse, Ellipse2.ToPolylineByChordTolerance(Flat(ellipse), chordTolerance).Vertices));
        }

        /// <summary>
        /// Approximates the rim of an ellipse in space as a chain whose points are evenly spread along it, no two further
        /// apart along the rim than a spacing, its first point repeated at the end.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="spacing">The largest length allowed along the rim between two points.</param>
        /// <returns>The corners of <see cref="ToPolygonBySpacing(GeoEllipse3, double)"/>, the first repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public static GeoPolyline3 ToPolylineBySpacing(GeoEllipse3 ellipse, double spacing)
        {
            return new GeoPolyline3(Lift(ellipse, Ellipse2.ToPolylineBySpacing(Flat(ellipse), spacing).Vertices));
        }

        /// <summary>
        /// Approximates the rim of an ellipse in space as a chain with a given number of pieces, its points evenly spread in
        /// the eccentric angle and its first point repeated at the end.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="segmentCount">How many pieces; at least three.</param>
        /// <returns>The corners of <see cref="ToPolygon(GeoEllipse3, int)"/>, the first repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three pieces are asked for.</exception>
        public static GeoPolyline3 ToPolyline(GeoEllipse3 ellipse, int segmentCount)
        {
            return new GeoPolyline3(Lift(ellipse, Ellipse2.ToPolyline(Flat(ellipse), segmentCount).Vertices));
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether two ellipses in space are the same shape in the same place, within the default tolerance.
        /// </summary>
        public static bool IsEqualTo(GeoEllipse3 ellipse, GeoEllipse3 other) => IsEqualTo(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Determines whether two ellipses in space are the same shape in the same place, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The second ellipse.</param>
        /// <param name="tolerance">The tolerance: the point tolerance for the centres and the radii, the vector tolerance for the normals and the axes.</param>
        /// <returns>
        /// true if the centres and both radii agree within the point tolerance, the normals within the vector tolerance, and
        /// the major axes along the same line within the vector tolerance; otherwise, false.
        /// </returns>
        /// <remarks>
        /// The normals are compared the way round they point, as a circle's are: an ellipse facing down is not the one facing
        /// up, its angle running the other way. An axis pointing the other way draws the same ellipse, so the axes are
        /// compared either way round; when both ellipses are circles they are not compared at all.
        /// </remarks>
        public static bool IsEqualTo(GeoEllipse3 ellipse, GeoEllipse3 other, Tolerance tolerance)
        {
            if (!ellipse.Center.IsEqualTo(other.Center, tolerance)
                || !ellipse.Normal.IsEqualTo(other.Normal, tolerance)
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
        /// Gets the ellipse in its own frame, in the plane: centred on the origin, its major axis along X.
        /// </summary>
        internal static GeoEllipse2 Flat(GeoEllipse3 ellipse)
        {
            return new GeoEllipse2(new GeoPoint2(0.0, 0.0), GeoVector2.XAxis, ellipse.MajorRadius, ellipse.MinorRadius, true);
        }

        /// <summary>
        /// Gets how far a point lies from the centre along the major axis, along the minor, and above the plane.
        /// </summary>
        private static void Frame(GeoEllipse3 ellipse, GeoPoint3 point, out double along, out double across, out double height)
        {
            GeoVector3 offset = ellipse.Center.GetVectorTo(point);

            along = offset.DotProduct(ellipse.MajorAxis);
            across = offset.DotProduct(ellipse.MinorAxis);
            height = offset.DotProduct(ellipse.Normal);
        }

        /// <summary>
        /// Gets the foot of a point on the plane, in the ellipse's own frame, a coordinate within rounding of an axis read
        /// on it.
        /// </summary>
        /// <remarks>
        /// A point on the line through the centre along the normal, or above the major axis, is read with the rounding of
        /// its coordinates and of the axes: its foot lands a few parts in 1E16 of the size of the numbers on one side of the
        /// axis or the other. The rule that picks the nearest point of the rim where two are equally near then followed the
        /// rounding: a point 50 above the centre of a tilted ellipse 300 by 100 gave the end of the minor axis at t = 270°
        /// one time in two, and above a tilted circle any angle. Read on the axis, it gives t = 90° as in the plane.
        /// </remarks>
        private static GeoPoint2 Foot(GeoEllipse3 ellipse, GeoPoint3 point)
        {
            return Foot(ellipse, point, 0.0);
        }

        /// <summary>
        /// Gets the foot of a point on the plane, in the ellipse's own frame, read at the centre within the vector tolerance
        /// of it, as a circle reads a point on its axis.
        /// </summary>
        private static GeoPoint2 Foot(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            return Foot(ellipse, point, tolerance.EqualVector);
        }

        private static GeoPoint2 Foot(GeoEllipse3 ellipse, GeoPoint3 point, double centre)
        {
            GeoPoint3 center = ellipse.Center;
            GeoVector3 offset = center.GetVectorTo(point);
            double along = offset.DotProduct(ellipse.MajorAxis);
            double across = offset.DotProduct(ellipse.MinorAxis);
            double rounding = 1E-14 * (Math.Abs(center.X) + Math.Abs(center.Y) + Math.Abs(center.Z) + Math.Abs(offset.X) + Math.Abs(offset.Y) + Math.Abs(offset.Z));

            if (along * along + across * across <= centre * centre)
            {
                return new GeoPoint2(0.0, 0.0);
            }

            return new GeoPoint2(Math.Abs(along) <= rounding ? 0.0 : along, Math.Abs(across) <= rounding ? 0.0 : across);
        }

        /// <summary>
        /// Gets whether a point of the plane, in the ellipse's own frame, lies within the rim or on it, read exactly.
        /// </summary>
        private static bool Within(GeoEllipse3 ellipse, double along, double across)
        {
            double u = along / ellipse.MajorRadius;
            double v = across / ellipse.MinorRadius;

            return u * u + v * v <= 1.0;
        }

        /// <summary>
        /// Puts a point of the ellipse's own frame back into space, on the plane.
        /// </summary>
        private static GeoPoint3 Lift(GeoEllipse3 ellipse, GeoPoint2 point)
        {
            GeoVector3 major = ellipse.MajorAxis;
            GeoVector3 minor = ellipse.MinorAxis;
            GeoPoint3 center = ellipse.Center;

            return new GeoPoint3(
                center.X + (point.X * major.X + point.Y * minor.X),
                center.Y + (point.X * major.Y + point.Y * minor.Y),
                center.Z + (point.X * major.Z + point.Y * minor.Z));
        }

        private static List<GeoPoint3> Lift(GeoEllipse3 ellipse, IReadOnlyList<GeoPoint2> points)
        {
            var lifted = new List<GeoPoint3>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                lifted.Add(Lift(ellipse, points[i]));
            }

            return lifted;
        }

        /// <summary>
        /// Gets the ellipse drawn by two conjugate semi-axes lying in the plane of a normal: the images of the major and the
        /// minor semi-axis under a map of the plane, read in a square frame of the new plane by <see cref="Ellipse2"/>.
        /// </summary>
        internal static GeoEllipse3 FromSemiAxes(GeoPoint3 center, GeoVector3 normal, GeoVector3 along, GeoVector3 across, double area)
        {
            GeoVector3 first = normal.GetPerpendicularVector();
            GeoVector3 second = normal.CrossProduct(first);

            GeoEllipse2 flat = Ellipse2.FromSemiAxes(
                new GeoPoint2(0.0, 0.0),
                new GeoVector2(along.DotProduct(first), along.DotProduct(second)),
                new GeoVector2(across.DotProduct(first), across.DotProduct(second)),
                area,
                out bool circle);

            // A circle keeps its angle starting where the old one went, as in the plane.
            GeoVector3 axis = circle
                ? along.Multiply(1.0 / along.Length)
                : first.Multiply(flat.MajorAxis.X).Add(second.Multiply(flat.MajorAxis.Y));

            return new GeoEllipse3(center, normal, axis, flat.MajorRadius, flat.MinorRadius, true);
        }

        #endregion
    }
}
