using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D ellipse: a centre, the direction of its major axis, and its two radii.
    /// <para>
    /// An ellipse is a region, as a circle is: it has an area, and a point inside it is in it. Its rim is walked by the
    /// eccentric angle t, the point Center + a cos t MajorAxis + b sin t MinorAxis, counter-clockwise from the end of the
    /// major axis. That is not the direction seen from the centre: on an ellipse 300 by 100, t = 45° is the point
    /// (212.1, 70.7), which lies 18.4° from the axis seen from the centre. An ellipse whose radii are equal is a circle and
    /// answers every question as <see cref="GeoCircle2"/> does, but for where its angle starts, which follows its axis;
    /// the point of the rim nearest its centre, which is the end of the minor axis as for any other ellipse; and where it
    /// touches a circle or another ellipse without crossing, which is midway between the two rims, where two circles of
    /// different radii touch at a point nearer the larger.
    /// </para>
    /// <para>
    /// The operations live in <see cref="Ellipse2"/>; the members here ask it.
    /// </para>
    /// </summary>
    public readonly partial struct GeoEllipse2 : IEquatable<GeoEllipse2>
    {
        /// <summary>
        /// Gets the centre of the ellipse.
        /// </summary>
        public GeoPoint2 Center { get; }

        /// <summary>
        /// Gets the direction of the major axis, of unit length. The eccentric angle starts at the end of the major axis
        /// this points to.
        /// </summary>
        public GeoVector2 MajorAxis { get; }

        /// <summary>
        /// Gets the major radius a: half the length of the major axis, never less than the minor radius.
        /// </summary>
        public double MajorRadius { get; }

        /// <summary>
        /// Gets the minor radius b: half the length of the minor axis, above nought.
        /// </summary>
        public double MinorRadius { get; }

        /// <summary>
        /// Gets the direction of the minor axis: the major axis turned a quarter counter-clockwise.
        /// </summary>
        public GeoVector2 MinorAxis => MajorAxis.GetPerpendicularVector();

        /// <summary>
        /// Gets the area of the ellipse, π a b: an ellipse 300 by 100 encloses 94 247.8.
        /// </summary>
        public double Area => Math.PI * MajorRadius * MinorRadius;

        /// <summary>
        /// Gets the eccentricity of the ellipse, the root of 1 - b²/a²: nought for a circle, nearer one the thinner it is;
        /// an ellipse 300 by 100 has 0.943.
        /// </summary>
        public double Eccentricity => Math.Sqrt((MajorRadius - MinorRadius) * (MajorRadius + MinorRadius)) / MajorRadius;

        /// <summary>
        /// Gets the length of the rim, the perimeter: an ellipse 300 by 100 measures 1 336.489 round. Named as it is on
        /// every other curve in the library, so that a length is asked for the same way whatever the shape.
        /// </summary>
        /// <remarks>
        /// Worked out by the arithmetic-geometric mean, to the last digit, not summed over pieces.
        /// </remarks>
        public double Length => Ellipse2.GetLength(this);

        /// <summary>
        /// Initializes an ellipse from its centre, the direction of its major axis and its two radii.
        /// </summary>
        /// <param name="center">The centre.</param>
        /// <param name="majorAxis">The direction of the major axis, of any length but nought; it is normalized.</param>
        /// <param name="majorRadius">Half the length of the major axis; a positive number, no less than the minor radius.</param>
        /// <param name="minorRadius">Half the length of the minor axis; a positive number.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a radius is not a positive number, or the minor radius is larger than the major one. The two are not
        /// swapped: an ellipse longer across than along is given by an axis a quarter turned.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when the axis has no length, or is not a finite vector.</exception>
        public GeoEllipse2(GeoPoint2 center, GeoVector2 majorAxis, double majorRadius, double minorRadius)
        {
            Guard.Positive(majorRadius, nameof(majorRadius), "A radius has to be a positive number.");
            Guard.Positive(minorRadius, nameof(minorRadius), "A radius has to be a positive number.");

            if (minorRadius > majorRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(minorRadius), minorRadius, "The minor radius cannot be larger than the major one.");
            }

            double length = majorAxis.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The major axis needs a direction: a finite vector of some length.", nameof(majorAxis));
            }

            Center = center;
            MajorAxis = new GeoVector2(majorAxis.X / length, majorAxis.Y / length);
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
        }

        /// <summary>
        /// Initializes an ellipse from values already checked, the axis already of unit length.
        /// </summary>
        internal GeoEllipse2(GeoPoint2 center, GeoVector2 unitAxis, double majorRadius, double minorRadius, bool trusted)
        {
            Center = center;
            MajorAxis = unitAxis;
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
        }

        /// <summary>
        /// Creates the ellipse that is a circle: both radii the circle's, the major axis along X, so that its eccentric
        /// angle is the circle's angle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <returns>The ellipse drawing the same circle.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the circle has no radius.</exception>
        /// <remarks>
        /// A circle can be stretched into an ellipse this way: a circle of radius 100 made an ellipse and transformed by a
        /// scaling of 3 along X is the ellipse 300 by 100, which <see cref="GeoCircle2.TransformBy(GeoTransform2)"/> refuses.
        /// </remarks>
        public static GeoEllipse2 FromCircle(GeoCircle2 circle) => new GeoEllipse2(circle.Center, GeoVector2.XAxis, circle.Radius, circle.Radius);

        /// <summary>
        /// Creates a copy of this ellipse.
        /// </summary>
        /// <remarks>
        /// Ellipse is a readonly struct, so plain assignment already produces an independent copy and this method is not
        /// needed to avoid sharing. It exists so that every geometry type offers the same way to ask for a copy.
        /// </remarks>
        /// <returns>A new ellipse with the same centre, axis and radii.</returns>
        public GeoEllipse2 Clone() => new GeoEllipse2(Center, MajorAxis, MajorRadius, MinorRadius, true);

        /// <summary>
        /// Determines whether this ellipse is a circle, its two radii the same within the default tolerance.
        /// </summary>
        public bool IsCircle() => Ellipse2.IsCircle(this);

        /// <summary>
        /// Determines whether this ellipse is a circle, its two radii the same within the point tolerance.
        /// </summary>
        public bool IsCircle(Tolerance tolerance) => Ellipse2.IsCircle(this, tolerance);

        #region Angles, parameters and distances along the rim

        /// <summary>
        /// Gets the point of the rim at an eccentric angle t, counter-clockwise from the end of the major axis; not the
        /// direction seen from the centre. On an ellipse 300 by 100 centred on the origin along X, t = 45° is the point
        /// (212.1, 70.7), which lies 18.4° from the axis seen from the centre.
        /// </summary>
        /// <param name="angleRad">The eccentric angle in radians.</param>
        public GeoPoint2 GetPointAtAngle(double angleRad) => Ellipse2.GetPointAtAngle(this, angleRad);

        /// <summary>
        /// Gets the eccentric angle of the point of the rim nearest a point, from nought up to a full turn; not the direction
        /// seen from the centre. On an ellipse 300 by 100 the point (212.1, 70.7) gives 45°, though it lies 18.4° from the
        /// axis seen from the centre.
        /// </summary>
        public double GetAngleAtPoint(GeoPoint2 point) => Ellipse2.GetAngleAtPoint(this, point);

        /// <summary>
        /// Gets the point at a normalized parameter along the rim, uniform in the eccentric angle: p is t = 2πp, 0 the end
        /// of the major axis and 1 the same point after a whole turn counter-clockwise. Values outside [0, 1] wrap around,
        /// so 1.25 is the same position as 0.25.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Ellipse2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point of the rim nearest a point: its eccentric angle as a share of a turn.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Ellipse2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at a true length along the rim from the end of the major axis, counter-clockwise; the length wraps.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Ellipse2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the true length along the rim from the end of the major axis to the point of the rim nearest a point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Ellipse2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the true length along the rim from the end of the major axis to a normalized parameter; 0.25 is exactly a
        /// quarter of the perimeter, though the parameter is uniform in the eccentric angle and not in length.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Ellipse2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at a true length along the rim from the end of the major axis.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Ellipse2.GetParameterAtDistance(this, distance);

        #endregion

        #region Points inside and on the rim

        /// <summary>
        /// Checks whether the ellipse contains a point using default tolerance; a point on the rim is contained.
        /// </summary>
        public bool Contains(GeoPoint2 point) => Ellipse2.Contains(this, point);

        /// <summary>
        /// Checks whether the ellipse contains a point within tolerance; a point within the point tolerance of the rim is contained.
        /// </summary>
        public bool Contains(GeoPoint2 point, Tolerance tolerance) => Ellipse2.Contains(this, point, tolerance);

        /// <summary>
        /// Classifies the location of a point relative to this ellipse (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Ellipse2.Locate(this, point);

        /// <summary>
        /// Classifies the location of a point relative to this ellipse (Inside, OutSide, or OnSide) within tolerance: OnSide
        /// within the point tolerance of the rim, measured as a distance to its nearest point.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Ellipse2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies on the rim using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Ellipse2.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on the rim within tolerance: within the point tolerance of its nearest point.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Ellipse2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Checks whether this ellipse holds a segment whole, using the default tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line) => Ellipse2.Contains(this, line);

        /// <summary>
        /// Checks whether this ellipse holds a segment whole, within a tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line, Tolerance tolerance) => Ellipse2.Contains(this, line, tolerance);

        /// <summary>
        /// Checks whether this ellipse holds a circle whole, using the default tolerance.
        /// </summary>
        public bool Contains(GeoCircle2 circle) => Ellipse2.Contains(this, circle);

        /// <summary>
        /// Checks whether this ellipse holds a circle whole, within a tolerance.
        /// </summary>
        public bool Contains(GeoCircle2 circle, Tolerance tolerance) => Ellipse2.Contains(this, circle, tolerance);

        /// <summary>
        /// Checks whether this ellipse holds another ellipse whole, using the default tolerance.
        /// </summary>
        public bool Contains(GeoEllipse2 other) => Ellipse2.Contains(this, other);

        /// <summary>
        /// Checks whether this ellipse holds another ellipse whole, within a tolerance.
        /// </summary>
        public bool Contains(GeoEllipse2 other, Tolerance tolerance) => Ellipse2.Contains(this, other, tolerance);

        #endregion

        #region Moving

        /// <summary>
        /// Translates the ellipse by a displacement vector, keeping its radii and its axis exactly.
        /// </summary>
        /// <param name="vector">The displacement vector.</param>
        /// <returns>A new translated GeoEllipse2.</returns>
        public GeoEllipse2 Translate(GeoVector2 vector) => Ellipse2.Translate(this, vector);

        /// <summary>
        /// Turns the ellipse about a point, keeping its radii exactly; the zero of its eccentric angle turns with it.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn about.</param>
        public GeoEllipse2 RotateBy(double angleRad, GeoPoint2 center) => Ellipse2.RotateBy(this, angleRad, center);

        /// <summary>
        /// Applies a transformation to this ellipse. Every transformation that does not flatten the plane gives an ellipse,
        /// an uneven scaling and a shear among them; a mirror reverses the way the eccentric angle runs round the rim as seen
        /// in the old frame.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed ellipse.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation flattens the plane onto a line or a point.</exception>
        /// <remarks>
        /// The zero of the eccentric angle goes to the end of the new major axis nearer where the old one went; when the
        /// result is a circle it goes where the old one went. See <see cref="Ellipse2.TransformBy(GeoEllipse2, GeoTransform2)"/>.
        /// </remarks>
        public GeoEllipse2 TransformBy(GeoTransform2 transform) => Ellipse2.TransformBy(this, transform);

        /// <summary>
        /// Translates an ellipse by a vector.
        /// </summary>
        public static GeoEllipse2 operator +(GeoEllipse2 ellipse, GeoVector2 vector) => ellipse.Translate(vector);

        /// <summary>
        /// Translates an ellipse backwards by a vector.
        /// </summary>
        public static GeoEllipse2 operator -(GeoEllipse2 ellipse, GeoVector2 vector) => ellipse.Translate(-vector);

        #endregion

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the rim as a polygon, cut finely enough that it strays no further from the rim than
        /// <see cref="Internal.Tessellation.AutomaticChordRatio"/> of the minor radius.
        /// </summary>
        /// <returns>A polygon inscribed in the ellipse, counter-clockwise from the end of the major axis, symmetric about both axes.</returns>
        /// <remarks>
        /// The corners are spaced by how sharply the rim bends: an ellipse 1 000 by 1 takes 84 edges, where an even step in
        /// the angle would need about 1 571; a circle takes the fifty or so <see cref="GeoCircle2.ToPolygon()"/> does, made a
        /// multiple of four.
        /// </remarks>
        public GeoPolygon2 ToPolygon() => ToPolygonByChordTolerance(0.0);

        /// <summary>
        /// Approximates the rim as a polygon that strays no further from it than a chord tolerance, its corners spaced by how
        /// sharply the rim bends, symmetric about both axes with a corner at each end of each.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between an edge and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>A polygon inscribed in the ellipse.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolygon2 ToPolygonByChordTolerance(double chordTolerance) => Ellipse2.ToPolygonByChordTolerance(this, chordTolerance);

        /// <summary>
        /// Approximates the rim as a polygon whose corners are evenly spread along it, no two further apart along the rim
        /// than a spacing.
        /// </summary>
        /// <param name="spacing">The largest length allowed along the rim between two corners.</param>
        /// <returns>A polygon inscribed in the ellipse.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolygon2 ToPolygonBySpacing(double spacing) => Ellipse2.ToPolygonBySpacing(this, spacing);

        /// <summary>
        /// Approximates the rim as a polygon with a given number of edges, its corners evenly spread in the eccentric angle.
        /// </summary>
        /// <param name="segmentCount">How many edges the polygon should have; at least three.</param>
        /// <returns>A polygon inscribed in the ellipse.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three edges are asked for.</exception>
        public GeoPolygon2 ToPolygon(int segmentCount) => Ellipse2.ToPolygon(this, segmentCount);

        /// <summary>
        /// Approximates the rim as a chain, cut by the automatic chord tolerance.
        /// </summary>
        /// <returns>A chain from the end of the major axis all the way round, its first point repeated at the end.</returns>
        /// <remarks>
        /// A chain is always open, so the first point is repeated at the end to close the loop: that is what it takes for a
        /// <see cref="GeoPolyline2"/> to trace something closed.
        /// </remarks>
        public GeoPolyline2 ToPolyline() => ToPolylineByChordTolerance(0.0);

        /// <summary>
        /// Approximates the rim as a chain that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolyline2 ToPolylineByChordTolerance(double chordTolerance) => Ellipse2.ToPolylineByChordTolerance(this, chordTolerance);

        /// <summary>
        /// Approximates the rim as a chain whose points are no further apart along it than a spacing.
        /// </summary>
        /// <param name="spacing">The largest length allowed along the rim between two points.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolyline2 ToPolylineBySpacing(double spacing) => Ellipse2.ToPolylineBySpacing(this, spacing);

        /// <summary>
        /// Approximates the rim as a chain with a given number of pieces, evenly spread in the eccentric angle.
        /// </summary>
        /// <param name="segmentCount">How many pieces the chain should have; at least three.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three pieces are asked for.</exception>
        public GeoPolyline2 ToPolyline(int segmentCount) => Ellipse2.ToPolyline(this, segmentCount);

        /// <summary>
        /// Breaks the region into triangles fanned from its centre, as many as <see cref="ToPolygon()"/> has edges, using the
        /// default tolerance.
        /// </summary>
        /// <returns>The triangles, counter-clockwise.</returns>
        public GeoTriangle2[] TriangulateSurface() => Ellipse2.TriangulateSurface(this, 0.0);

        /// <summary>
        /// Breaks the region into triangles fanned from its centre, as many as <see cref="ToPolygon()"/> has edges, within a
        /// tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance deciding what counts as no area.</param>
        /// <returns>The triangles, counter-clockwise; none when the minor radius is no more than the point tolerance.</returns>
        public GeoTriangle2[] TriangulateSurface(Tolerance tolerance) => Ellipse2.TriangulateSurface(this, 0.0, tolerance);

        /// <summary>
        /// Breaks the region into triangles fanned from its centre, the rim cut so that it strays from the ellipse no
        /// further than a chord tolerance, using the default tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a chord and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <returns>The triangles, counter-clockwise.</returns>
        public GeoTriangle2[] TriangulateSurface(double chordTolerance) => Ellipse2.TriangulateSurface(this, chordTolerance);

        /// <summary>
        /// Breaks the region into triangles fanned from its centre, the rim cut so that it strays from the ellipse no
        /// further than a chord tolerance, within a tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a chord and the rim, in drawing units. Zero picks the automatic share of the minor radius.</param>
        /// <param name="tolerance">The tolerance deciding what counts as no area.</param>
        /// <returns>The triangles, counter-clockwise; none when the minor radius is no more than the point tolerance.</returns>
        /// <remarks>
        /// Fanned from the centre, as a circle is: the rim's polygon is convex, so every triangle is whole, and the triangles
        /// cover the polygon <see cref="ToPolygonByChordTolerance(double)"/> gives, which lies within the ellipse.
        /// </remarks>
        public GeoTriangle2[] TriangulateSurface(double chordTolerance, Tolerance tolerance) => Ellipse2.TriangulateSurface(this, chordTolerance, tolerance);

        /// <summary>
        /// Breaks the region into faces of a kind, using the default tolerance. A grid needs the size of its cells, which
        /// <see cref="ToMesh(Meshing.MeshOptions)"/> takes.
        /// </summary>
        /// <param name="kind">The kind of faces.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        /// <exception cref="ArgumentException">Thrown for <see cref="Meshing.MeshKind.Grid"/>, which needs the size of its cells.</exception>
        public Meshing.GeoMesh2 ToMesh(Meshing.MeshKind kind) => Meshing.Mesh2.ToMesh(this, Meshing.Mesh2.OptionsFor(kind), Tolerance.Global);

        /// <summary>
        /// Breaks the region into faces as the options say, using the default tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh2 ToMesh(Meshing.MeshOptions options) => Meshing.Mesh2.ToMesh(this, options, Tolerance.Global);

        /// <summary>
        /// Breaks the region into faces as the options say, within a tolerance: triangles, the cells of a grid, strips or
        /// convex pieces, each a simple polygon with no hole, counter-clockwise, meeting its neighbours edge to edge.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the minor radius is no more than the point tolerance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a grid's cell is no larger than the point tolerance, or the grid would lay more cells over the shape
        /// than a mesh may have.
        /// </exception>
        /// <remarks>
        /// The rim is flattened first, by the options' chord tolerance, as <see cref="ToPolygonByChordTolerance(double)"/>
        /// flattens it; its triangles are fanned from the centre.
        /// </remarks>
        public Meshing.GeoMesh2 ToMesh(Meshing.MeshOptions options, Tolerance tolerance) => Meshing.Mesh2.ToMesh(this, options, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Indicates whether the current ellipse holds exactly the same centre, axis and radii as another.
        /// </summary>
        public bool Equals(GeoEllipse2 other)
        {
            return Center.Equals(other.Center)
                && MajorAxis.Equals(other.MajorAxis)
                && MajorRadius.Equals(other.MajorRadius)
                && MinorRadius.Equals(other.MinorRadius);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoEllipse2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = hash * 397 ^ MajorAxis.GetHashCode();
                hash = hash * 397 ^ MajorRadius.GetHashCode();
                hash = hash * 397 ^ MinorRadius.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Compares two GeoEllipse2 instances for equality.
        /// </summary>
        public static bool operator ==(GeoEllipse2 left, GeoEllipse2 right) => left.Equals(right);

        /// <summary>
        /// Compares two GeoEllipse2 instances for inequality.
        /// </summary>
        public static bool operator !=(GeoEllipse2 left, GeoEllipse2 right) => !left.Equals(right);

        /// <summary>
        /// Determines whether another ellipse is the same shape in the same place, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoEllipse2 other) => Ellipse2.IsEqualTo(this, other);

        /// <summary>
        /// Determines whether another ellipse is the same shape in the same place, within a tolerance: the centres and both
        /// radii within the point tolerance, and the major axes along the same line, either way round, within the vector
        /// tolerance. Two circles are not asked about their axes.
        /// </summary>
        /// <param name="other">The ellipse to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ellipses are the same within tolerance; otherwise, false.</returns>
        public bool IsEqualTo(GeoEllipse2 other, Tolerance tolerance) => Ellipse2.IsEqualTo(this, other, tolerance);

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: the centre a valid point, both radii positive numbers
        /// with the major no less than the minor, and the axis a finite vector of unit length.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one — an element of a new array, or what the <c>out</c>
        /// of a <c>Try</c> method holds when the method said false — is not: it has no radii and no axis, and answers
        /// questions without complaint all the same.
        /// </remarks>
        public bool IsValid => Center.IsValid
            && MinorRadius > 0.0 && Guard.IsFinite(MajorRadius) && MajorRadius >= MinorRadius
            && MajorAxis.IsValid && Guard.IsUnit(MajorAxis);

        /// <summary>
        /// Returns the string representation of the ellipse.
        /// </summary>
        public override string ToString() => $"GeoEllipse2[Center:{Center}, MajorAxis:{MajorAxis}, MajorRadius:{MajorRadius:0.###}, MinorRadius:{MinorRadius:0.###}]";

        #endregion
    }
}
