using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents an elliptical disc in 3D space: a centre, the normal of its plane, the direction of its major axis, and
    /// its two radii.
    /// <para>
    /// Like a circle in space, this is a flat region rather than a curve: it has an area, and a point counts as inside it
    /// only when the point lies on its plane as well as within its rim. The rim is walked by the eccentric angle t, the
    /// point Center + a cos t MajorAxis + b sin t MinorAxis, counter-clockwise seen from the tip of the normal, from the end
    /// of the major axis. That is not the direction seen from the centre: on an ellipse 300 by 100, t = 45° is the point
    /// 212.1 along the major axis and 70.7 along the minor, which lies 18.4° from the axis seen from the centre.
    /// </para>
    /// <para>
    /// Everything within the plane is <see cref="GeoEllipse2"/>'s arithmetic, read in the ellipse's own frame. An ellipse
    /// whose radii are equal is a circle and answers as <see cref="GeoCircle3"/> does, but for where its angle starts, which
    /// follows its axis, and the point of the rim nearest its centre, which is the end of the minor axis as for any other
    /// ellipse. The operations live in <see cref="Ellipse3"/>; the members here ask it.
    /// </para>
    /// </summary>
    public readonly partial struct GeoEllipse3 : IEquatable<GeoEllipse3>
    {
        /// <summary>
        /// Gets the centre of the ellipse.
        /// </summary>
        public GeoPoint3 Center { get; }

        /// <summary>
        /// Gets the unit normal of the plane carrying the ellipse. The eccentric angle runs counter-clockwise seen from its tip.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets the direction of the major axis, of unit length and square to the normal. The eccentric angle starts at the
        /// end of the major axis this points to.
        /// </summary>
        public GeoVector3 MajorAxis { get; }

        /// <summary>
        /// Gets the major radius a: half the length of the major axis, never less than the minor radius.
        /// </summary>
        public double MajorRadius { get; }

        /// <summary>
        /// Gets the minor radius b: half the length of the minor axis, above nought.
        /// </summary>
        public double MinorRadius { get; }

        /// <summary>
        /// Gets the direction of the minor axis: the normal crossed with the major axis, the major axis turned a quarter
        /// counter-clockwise about the normal.
        /// </summary>
        public GeoVector3 MinorAxis => Normal.CrossProduct(MajorAxis);

        /// <summary>
        /// Gets the area of the disc, π a b: an ellipse 300 by 100 encloses 94 247.8.
        /// </summary>
        public double Area => Ellipse3.Flat(this).Area;

        /// <summary>
        /// Gets the eccentricity of the ellipse, the root of 1 - b²/a²: nought for a circle, nearer one the thinner it is;
        /// an ellipse 300 by 100 has 0.943.
        /// </summary>
        public double Eccentricity => Ellipse3.Flat(this).Eccentricity;

        /// <summary>
        /// Gets the length of the rim, the perimeter: an ellipse 300 by 100 measures 1 336.489 round.
        /// </summary>
        /// <remarks>
        /// Worked out by the arithmetic-geometric mean, to the last digit, not summed over pieces.
        /// </remarks>
        public double Length => Ellipse3.GetLength(this);

        /// <summary>
        /// Initializes an ellipse in space from its centre, the normal of its plane, the direction of its major axis and its
        /// two radii.
        /// </summary>
        /// <param name="center">The centre.</param>
        /// <param name="normal">The normal of the carrying plane, of any length but nought; it is normalized.</param>
        /// <param name="majorAxis">
        /// The direction of the major axis, of any length but nought. An axis tilted out of the plane is laid into it: its
        /// part along the normal is taken off, and what is left is normalized, so (1, 0, 1) on a plane with the normal along
        /// Z is the axis along X.
        /// </param>
        /// <param name="majorRadius">Half the length of the major axis; a positive number, no less than the minor radius.</param>
        /// <param name="minorRadius">Half the length of the minor axis; a positive number.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the normal has no length or is not a finite vector; or when the axis has no length, is not a finite
        /// vector, or runs along the normal, what is left of it in the plane no more than 1E-12 of its length.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a radius is not a positive number, or the minor radius is larger than the major one. The two are not
        /// swapped: an ellipse longer across than along is given by an axis a quarter turned.
        /// </exception>
        public GeoEllipse3(GeoPoint3 center, GeoVector3 normal, GeoVector3 majorAxis, double majorRadius, double minorRadius)
        {
            double normalLength = normal.Length;

            if (!(normalLength > 0.0) || double.IsInfinity(normalLength))
            {
                throw new ArgumentException("An ellipse needs a normal: a finite vector of some length.", nameof(normal));
            }

            GeoVector3 unit = new GeoVector3(normal.X / normalLength, normal.Y / normalLength, normal.Z / normalLength);
            double axisLength = majorAxis.Length;

            if (!(axisLength > 0.0) || double.IsInfinity(axisLength))
            {
                throw new ArgumentException("The major axis needs a direction: a finite vector of some length.", nameof(majorAxis));
            }

            GeoVector3 inPlane = majorAxis.Subtract(unit.Multiply(majorAxis.DotProduct(unit)));
            double left = inPlane.Length;

            if (!(left > 1E-12 * axisLength))
            {
                throw new ArgumentException("The major axis runs along the normal, so it gives no direction in the plane.", nameof(majorAxis));
            }

            Guard.Positive(majorRadius, nameof(majorRadius), "A radius has to be a positive number.");
            Guard.Positive(minorRadius, nameof(minorRadius), "A radius has to be a positive number.");

            if (minorRadius > majorRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(minorRadius), minorRadius, "The minor radius cannot be larger than the major one.");
            }

            Center = center;
            Normal = unit;
            MajorAxis = new GeoVector3(inPlane.X / left, inPlane.Y / left, inPlane.Z / left);
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
        }

        /// <summary>
        /// Initializes an ellipse from values already checked, the normal and the axis already of unit length and square.
        /// </summary>
        internal GeoEllipse3(GeoPoint3 center, GeoVector3 unitNormal, GeoVector3 unitAxis, double majorRadius, double minorRadius, bool trusted)
        {
            Center = center;
            Normal = unitNormal;
            MajorAxis = unitAxis;
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
        }

        /// <summary>
        /// Creates the ellipse that is a circle in space: both radii the circle's, the normal the circle's, and the major
        /// axis where the circle's angle starts, so that the eccentric angle is the circle's angle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <returns>The ellipse drawing the same circle, whose <see cref="GetPointAtAngle(double)"/> gives the circle's <see cref="GeoCircle3.GetPointAtAngle(double)"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the circle has no radius.</exception>
        /// <remarks>
        /// A circle can be stretched into an ellipse this way: a circle of radius 100 lying in the plan made an ellipse and
        /// transformed by a scaling of 3 along X is the ellipse 300 by 100, which
        /// <see cref="GeoCircle3.TransformBy(GeoTransform3)"/> refuses.
        /// </remarks>
        public static GeoEllipse3 FromCircle(GeoCircle3 circle)
        {
            Guard.Positive(circle.Radius, nameof(circle), "A circle has to have a positive radius to be an ellipse.");

            circle.GetPlane().GetAxes(out GeoVector3 uAxis, out _);

            return new GeoEllipse3(circle.Center, circle.Normal, uAxis, circle.Radius, circle.Radius, true);
        }

        /// <summary>
        /// Creates a copy of this ellipse.
        /// </summary>
        /// <remarks>
        /// Ellipse is a readonly struct, so plain assignment already produces an independent copy and this method is not
        /// needed to avoid sharing. It exists so that every geometry type offers the same way to ask for a copy.
        /// </remarks>
        /// <returns>A new ellipse with the same centre, normal, axis and radii.</returns>
        public GeoEllipse3 Clone() => new GeoEllipse3(Center, Normal, MajorAxis, MajorRadius, MinorRadius, true);

        /// <summary>
        /// Determines whether this ellipse is a circle, its two radii the same within the default tolerance.
        /// </summary>
        public bool IsCircle() => Ellipse3.IsCircle(this);

        /// <summary>
        /// Determines whether this ellipse is a circle, its two radii the same within the point tolerance.
        /// </summary>
        public bool IsCircle(Tolerance tolerance) => Ellipse3.IsCircle(this, tolerance);

        /// <summary>
        /// Gets the plane carrying the ellipse, through its centre along its normal.
        /// </summary>
        public GeoPlane3 GetPlane() => new GeoPlane3(Center, Normal);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this ellipse, exactly: an ellipse 300 by 100 in the plan with its
        /// major axis along X reaches 300 either way along X, 100 along Y and nothing along Z.
        /// </summary>
        public GeoAabb3 GetAabb() => Ellipse3.GetAabb(this);

        #region Angles, parameters and distances along the rim

        /// <summary>
        /// Gets the point of the rim at an eccentric angle t, counter-clockwise about the normal from the end of the major
        /// axis; not the direction seen from the centre. On an ellipse 300 by 100, t = 45° is the point 212.1 along the major
        /// axis and 70.7 along the minor, which lies 18.4° from the axis seen from the centre.
        /// </summary>
        /// <param name="angleRad">The eccentric angle in radians.</param>
        public GeoPoint3 GetPointAtAngle(double angleRad) => Ellipse3.GetPointAtAngle(this, angleRad);

        /// <summary>
        /// Gets the eccentric angle of the point of the rim nearest a point, from nought up to a full turn; not the direction
        /// seen from the centre. A point off the plane is read at its foot on it.
        /// </summary>
        public double GetAngleAtPoint(GeoPoint3 point) => Ellipse3.GetAngleAtPoint(this, point);

        /// <summary>
        /// Gets the point at a normalized parameter along the rim, uniform in the eccentric angle: p is t = 2πp, 0 the end
        /// of the major axis and 1 the same point after a whole turn. The parameter wraps, so 1.25 gives the same point as
        /// 0.25.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => Ellipse3.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point of the rim nearest a point: its eccentric angle as a share of a turn.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Ellipse3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the normalized parameter of the point of the rim nearest a point, within a tolerance: a point whose foot lies
        /// within the vector tolerance of the centre is read at the centre, which gives 0.25.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) => Ellipse3.GetParameterAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets the point at a true length along the rim from the end of the major axis; the length wraps.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => Ellipse3.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the true length along the rim from the end of the major axis to the point of the rim nearest a point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Ellipse3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the true length along the rim from the end of the major axis to the point of the rim nearest a point, within
        /// a tolerance: a point whose foot lies within the vector tolerance of the centre is read at the centre, which gives a
        /// quarter of the perimeter.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) => Ellipse3.GetDistanceAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets the true length along the rim from the end of the major axis to a normalized parameter, which wraps as a
        /// circle's does; 0.25 is exactly a quarter of the perimeter, though the parameter is uniform in the eccentric angle
        /// and not in length.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Ellipse3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at a true length along the rim from the end of the major axis; the length wraps,
        /// as a circle's does.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Ellipse3.GetParameterAtDistance(this, distance);

        #endregion

        #region Queries

        /// <summary>
        /// Locates a point relative to this disc, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Ellipse3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this disc, within a tolerance: outside when it stands off the plane by more than the
        /// planar tolerance, and otherwise as its foot lies to the rim, on it within the point tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Ellipse3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this disc holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Ellipse3.Contains(this, point);

        /// <summary>
        /// Checks whether this disc holds a point, within a tolerance; a point on the rim is held.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Ellipse3.Contains(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies on the rim, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Ellipse3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on the rim, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Ellipse3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Gets the point of this disc closest to a target point, read as a filled surface: the foot of the point on the
        /// plane where it lies within the rim, and the nearest point of the rim where it does not.
        /// </summary>
        public GeoPoint3 GetClosestPointOnSurface(GeoPoint3 point) => Ellipse3.GetClosestPointOnSurface(this, point);

        #endregion

        #region Moving

        /// <summary>
        /// Moves the ellipse by a vector, keeping its normal, its axis and its radii exactly.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The ellipse in its new place.</returns>
        public GeoEllipse3 Translate(GeoVector3 vector) => Ellipse3.Translate(this, vector);

        /// <summary>
        /// Applies a transformation to this ellipse. Every transformation that does not flatten its plane gives an ellipse,
        /// an uneven scaling and a shear among them.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed ellipse.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation cannot be undone, where a circle's plane cannot be carried either.</exception>
        /// <remarks>
        /// The normal goes where a circle's would, as a plane's does, so under a mirror the eccentric angle reaches the images
        /// of the old rim's points in the opposite order. See <see cref="Ellipse3.TransformBy(GeoEllipse3, GeoTransform3)"/>.
        /// </remarks>
        public GeoEllipse3 TransformBy(GeoTransform3 transform) => Ellipse3.TransformBy(this, transform);

        /// <summary>
        /// Gets the ellipse seen square onto a plane: every point of this one projected along the plane's normal. A circle
        /// of radius 300 tilted 60° from the plan is, in the plan, an ellipse 300 by 150.
        /// </summary>
        /// <param name="plane">The plane to project onto.</param>
        /// <returns>The ellipse in that plane, its normal the plane's.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse stands square to the plane, where it would flatten into a segment rather than an ellipse.</exception>
        /// <remarks>See <see cref="Ellipse3.ProjectToPlane(GeoEllipse3, GeoPlane3)"/>.</remarks>
        public GeoEllipse3 ProjectToPlane(GeoPlane3 plane) => Ellipse3.ProjectToPlane(this, plane);

        /// <summary>
        /// Lays this ellipse out in the plane of a frame, projecting it square onto that plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The ellipse in the plane of the frame.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse stands square to the plane, where it would flatten into a segment rather than an ellipse.</exception>
        public GeoEllipse2 ProjectToEllipse2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToEllipse2(frame, this);

        #endregion

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the rim as a polygon, cut finely enough that it strays no further from the rim than
        /// <see cref="Internal.Tessellation.AutomaticChordRatio"/> of the minor radius.
        /// </summary>
        /// <returns>A polygon inscribed in the ellipse, counter-clockwise about the normal from the end of the major axis, symmetric about both axes.</returns>
        /// <remarks>
        /// The corners are those of <see cref="GeoEllipse2.ToPolygon()"/>, put into the ellipse's plane: an ellipse 1 000 by 1
        /// takes 84 edges, where an even step in the angle would need about 1 571.
        /// </remarks>
        public GeoPolygon3 ToPolygon() => ToPolygonByChordTolerance(0.0);

        /// <summary>
        /// Approximates the rim as a polygon that strays no further from it than a chord tolerance, its corners spaced by how
        /// sharply the rim bends, symmetric about both axes with a corner at each end of each.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between an edge and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>A polygon inscribed in the ellipse.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 corners.</exception>
        public GeoPolygon3 ToPolygonByChordTolerance(double chordTolerance) => Ellipse3.ToPolygonByChordTolerance(this, chordTolerance);

        /// <summary>
        /// Approximates the rim as a polygon whose corners are evenly spread along it, no two further apart along the rim
        /// than a spacing.
        /// </summary>
        /// <param name="spacing">The largest length allowed along the rim between two corners.</param>
        /// <returns>A polygon inscribed in the ellipse.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolygon3 ToPolygonBySpacing(double spacing) => Ellipse3.ToPolygonBySpacing(this, spacing);

        /// <summary>
        /// Approximates the rim as a polygon with a given number of edges, its corners evenly spread in the eccentric angle.
        /// </summary>
        /// <param name="segmentCount">How many edges the polygon should have; at least three.</param>
        /// <returns>A polygon inscribed in the ellipse.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three edges are asked for.</exception>
        public GeoPolygon3 ToPolygon(int segmentCount) => Ellipse3.ToPolygon(this, segmentCount);

        /// <summary>
        /// Approximates the rim as a chain, cut by the automatic chord tolerance.
        /// </summary>
        /// <returns>A chain from the end of the major axis all the way round, its first point repeated at the end.</returns>
        /// <remarks>
        /// A chain is always open, so the first point is repeated at the end to close the loop: that is what it takes for a
        /// <see cref="GeoPolyline3"/> to trace something closed.
        /// </remarks>
        public GeoPolyline3 ToPolyline() => ToPolylineByChordTolerance(0.0);

        /// <summary>
        /// Approximates the rim as a chain that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 corners.</exception>
        public GeoPolyline3 ToPolylineByChordTolerance(double chordTolerance) => Ellipse3.ToPolylineByChordTolerance(this, chordTolerance);

        /// <summary>
        /// Approximates the rim as a chain whose points are no further apart along it than a spacing.
        /// </summary>
        /// <param name="spacing">The largest length allowed along the rim between two points.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolyline3 ToPolylineBySpacing(double spacing) => Ellipse3.ToPolylineBySpacing(this, spacing);

        /// <summary>
        /// Approximates the rim as a chain with a given number of pieces, evenly spread in the eccentric angle.
        /// </summary>
        /// <param name="segmentCount">How many pieces the chain should have; at least three.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three pieces are asked for.</exception>
        public GeoPolyline3 ToPolyline(int segmentCount) => Ellipse3.ToPolyline(this, segmentCount);

        /// <summary>
        /// Breaks the disc into faces of a kind, its grid level, using the default tolerance. A grid needs the size of its
        /// cells, which <see cref="ToMesh(Meshing.MeshOptions)"/> takes.
        /// </summary>
        /// <param name="kind">The kind of faces.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        /// <exception cref="ArgumentException">Thrown for <see cref="Meshing.MeshKind.Grid"/>, which needs the size of its cells.</exception>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshKind kind) => Meshing.Mesh3.ToMesh(this, Meshing.Mesh2.OptionsFor(kind), Meshing.MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks the disc into faces as the options say, its grid level, with the second axis up the slope, using the
        /// default tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options) => Meshing.Mesh3.ToMesh(this, options, Meshing.MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks the disc into faces as the options say, its grid level, with the second axis up the slope, within a
        /// tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Tolerance tolerance) => Meshing.Mesh3.ToMesh(this, options, Meshing.MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks the disc into faces as the options say, its grid standing as the placement says, using the default
        /// tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the plane, and where a cell starts.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Meshing.MeshPlacement3 placement) => Meshing.Mesh3.ToMesh(this, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks the disc into faces as the options say, its grid standing as the placement says, within a tolerance:
        /// triangles, the cells of a grid, strips or convex pieces, each a simple polygon with no hole, counter-clockwise about
        /// the normal, meeting its neighbours edge to edge.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the minor radius is no more than the point tolerance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        /// <remarks>
        /// The rim is flattened by the options' chord tolerance first, as <see cref="GeoEllipse2.ToPolygonByChordTolerance(double)"/>
        /// flattens it, and the triangles fanned from the centre. The mesh's normal is the ellipse's.
        /// </remarks>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Meshing.MeshPlacement3 placement, Tolerance tolerance) => Meshing.Mesh3.ToMesh(this, options, placement, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Indicates whether the current ellipse holds exactly the same centre, normal, axis and radii as another.
        /// </summary>
        public bool Equals(GeoEllipse3 other)
        {
            return Center.Equals(other.Center)
                && Normal.Equals(other.Normal)
                && MajorAxis.Equals(other.MajorAxis)
                && MajorRadius.Equals(other.MajorRadius)
                && MinorRadius.Equals(other.MinorRadius);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoEllipse3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = hash * 397 ^ Normal.GetHashCode();
                hash = hash * 397 ^ MajorAxis.GetHashCode();
                hash = hash * 397 ^ MajorRadius.GetHashCode();
                hash = hash * 397 ^ MinorRadius.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Compares two GeoEllipse3 instances for equality.
        /// </summary>
        public static bool operator ==(GeoEllipse3 left, GeoEllipse3 right) => left.Equals(right);

        /// <summary>
        /// Compares two GeoEllipse3 instances for inequality.
        /// </summary>
        public static bool operator !=(GeoEllipse3 left, GeoEllipse3 right) => !left.Equals(right);

        /// <summary>
        /// Determines whether another ellipse is the same shape in the same place, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoEllipse3 other) => Ellipse3.IsEqualTo(this, other);

        /// <summary>
        /// Determines whether another ellipse is the same shape in the same place, within a tolerance: the centres and both
        /// radii within the point tolerance, the normals the same way within the vector tolerance, as a circle's are, and the
        /// major axes along the same line, either way round, within the vector tolerance. Two circles are not asked about
        /// their axes.
        /// </summary>
        /// <param name="other">The ellipse to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ellipses are the same within tolerance; otherwise, false.</returns>
        public bool IsEqualTo(GeoEllipse3 other, Tolerance tolerance) => Ellipse3.IsEqualTo(this, other, tolerance);

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: the centre a valid point, both radii positive numbers
        /// with the major no less than the minor, and the normal and the axis finite vectors of unit length, square to each
        /// other.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one — an element of a new array, or what the <c>out</c>
        /// of a <c>Try</c> method holds when the method said false — is not: it has no radii, no normal and no axis, and
        /// answers questions without complaint all the same.
        /// </remarks>
        public bool IsValid => Center.IsValid
            && MinorRadius > 0.0 && Guard.IsFinite(MajorRadius) && MajorRadius >= MinorRadius
            && Normal.IsValid && Guard.IsUnit(Normal)
            && MajorAxis.IsValid && Guard.IsUnit(MajorAxis)
            && Math.Abs(Normal.DotProduct(MajorAxis)) <= 1E-9;

        /// <summary>
        /// Returns the string representation of the ellipse.
        /// </summary>
        public override string ToString() => $"GeoEllipse3[Center:{Center}, Normal:{Normal}, MajorAxis:{MajorAxis}, MajorRadius:{MajorRadius:0.###}, MinorRadius:{MinorRadius:0.###}]";

        #endregion
    }
}
