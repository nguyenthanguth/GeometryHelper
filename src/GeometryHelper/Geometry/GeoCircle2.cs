using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D circle with a center point and radius.
    /// </summary>
    public readonly partial struct GeoCircle2 : IEquatable<GeoCircle2>
    {
        /// <summary>
        /// Gets the center point of the circle.
        /// </summary>
        public GeoPoint2 Center { get; }

        /// <summary>
        /// Gets the radius of the circle.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Gets the diameter of the circle.
        /// </summary>
        public double Diameter => Radius * 2.0;

        /// <summary>
        /// Gets the length of the circle: its circumference. Named as it is on every other curve in the
        /// library, so that a length is asked for the same way whatever the shape.
        /// </summary>
        public double Length => 2.0 * Math.PI * Radius;

        /// <summary>
        /// Gets the area of the circle.
        /// </summary>
        public double Area => Math.PI * Radius * Radius;

        /// <summary>
        /// Initializes a new GeoCircle2 instance from a center point and radius.
        /// </summary>
        /// <param name="center">Center point of the circle.</param>
        /// <param name="radius">Radius of the circle (must be non-negative).</param>
        public GeoCircle2(GeoPoint2 center, double radius)
        {
            if (radius < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius cannot be negative.");
            }
            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// Initializes a new GeoCircle2 instance from center coordinates and radius.
        /// </summary>
        /// <param name="centerX">X coordinate of the center point.</param>
        /// <param name="centerY">Y coordinate of the center point.</param>
        /// <param name="radius">Radius of the circle (must be non-negative).</param>
        public GeoCircle2(double centerX, double centerY, double radius)
            : this(new GeoPoint2(centerX, centerY), radius)
        {
        }

        /// <summary>
        /// Creates a copy of this circle.
        /// </summary>
        /// <remarks>
        /// Circle is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        /// <returns>A new circle with the same center and radius.</returns>
        public GeoCircle2 Clone() => new GeoCircle2(Center, Radius);

        /// <summary>
        /// Converts this circle into an oriented bounding GeoRectangle2 with the specified rotation angle.
        /// </summary>
        /// <param name="angleRad">The rotation angle of the resulting rectangle in radians.</param>
        /// <returns>A new GeoRectangle2 instance representing the oriented bounding box of this circle.</returns>
        public GeoRectangle2 ToRectangle(double angleRad) => new GeoRectangle2(Center, Diameter, Diameter, angleRad);

        /// <summary>
        /// Gets the point at a normalized parameter along this circle, where 0 is angle zero and 1 is the end.
        /// Values outside [0, 1] wrap around, so 1.25 is the same position as 0.25.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on this circle closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from angle zero of this circle.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from angle zero of this circle to the point on it closest to the supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from angle zero of this circle to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from angle zero of this circle.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Translates the circle by a displacement vector.
        /// </summary>
        /// <param name="vector">The displacement vector.</param>
        /// <returns>A new translated GeoCircle2.</returns>
        public GeoCircle2 Translate(GeoVector2 vector) => new GeoCircle2(Center.Add(vector), Radius);

        /// <summary>
        /// Grows this circle by a distance, or shrinks it when the distance is negative, using default tolerance.
        /// </summary>
        /// <param name="distance">How far the circumference moves: outward when positive.</param>
        /// <param name="result">The concentric circle, or this circle when the method returns false.</param>
        /// <returns>true if a circle is left; false if it shrinks to nothing.</returns>
        public bool TryOffset(double distance, out GeoCircle2 result) => Offset2.TryOffset(this, distance, out result, Tolerance.Global);

        /// <summary>
        /// Grows this circle by a distance, or shrinks it when the distance is negative, within tolerance.
        /// </summary>
        /// <param name="distance">How far the circumference moves: outward when positive.</param>
        /// <param name="result">The concentric circle, or this circle when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if a circle is left; false if it shrinks to nothing.</returns>
        public bool TryOffset(double distance, out GeoCircle2 result, Tolerance tolerance) => Offset2.TryOffset(this, distance, out result, tolerance);

        /// <summary>
        /// Checks whether the circle contains a point using default tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point) => Containment2.Contains(this, point, Tolerance.Global);

        /// <summary>
        /// Checks whether the circle contains a point within tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point, Tolerance tolerance) => Containment2.Contains(this, point, tolerance);

        /// <summary>
        /// Classifies the location of a point relative to this circle (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Classifies the location of a point relative to this circle (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether the circle entirely contains another circle using default tolerance.
        /// </summary>
        public bool Contains(GeoCircle2 other) => Containment2.Contains(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether the circle entirely contains another circle within tolerance.
        /// </summary>
        public bool Contains(GeoCircle2 other, Tolerance tolerance) => Containment2.Contains(this, other, tolerance);

        /// <summary>
        /// Checks whether the circle entirely contains a line segment using default tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line) => Containment2.Contains(this, line, Tolerance.Global);

        /// <summary>
        /// Checks whether the circle entirely contains a line segment within tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line, Tolerance tolerance) => Containment2.Contains(this, line, tolerance);

        /// <summary>
        /// Checks whether a point lies on the circle circumference using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Containment2.IsPointOn(this, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the circle circumference within tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Containment2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Translates a circle by a vector.
        /// </summary>
        public static GeoCircle2 operator +(GeoCircle2 circle, GeoVector2 vector) => circle.Translate(vector);

        /// <summary>
        /// Translates a circle backwards by a vector.
        /// </summary>
        public static GeoCircle2 operator -(GeoCircle2 circle, GeoVector2 vector) => circle.Translate(-vector);

        /// <summary>
        /// Indicates whether the current circle is equal to another circle.
        /// </summary>
        public bool Equals(GeoCircle2 other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoCircle2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Center.GetHashCode() * 397) ^ Radius.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two GeoCircle2 instances for equality.
        /// </summary>
        public static bool operator ==(GeoCircle2 left, GeoCircle2 right) => left.Equals(right);

        /// <summary>
        /// Compares two GeoCircle2 instances for inequality.
        /// </summary>
        public static bool operator !=(GeoCircle2 left, GeoCircle2 right) => !left.Equals(right);

        /// <summary>
        /// Returns the string representation of the circle.
        /// </summary>
        public override string ToString() => $"GeoCircle2[Center:{Center}, Radius:{Radius:0.###}]";

        /// <summary>
        /// Applies a transformation to this circle.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would stretch the circle into an ellipse.</exception>
        /// <summary>
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed circle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoCircle2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the circle as a polygon, cut finely enough that it strays no further from the
        /// circle than <see cref="Internal.Tessellation.AutomaticChordRatio"/> of its radius.
        /// </summary>
        /// <returns>A polygon inscribed in the circle, running the way the circle is parametrized.</returns>
        /// <remarks>
        /// The vertices lie on the circle, so the polygon is inscribed and encloses slightly less than the
        /// circle does: about 0.26 % less at the fifty edges the automatic tolerance gives.
        /// </remarks>
        public GeoPolygon2 ToPolygon() => ToPolygonByChordTolerance(0.0);

        /// <summary>
        /// Approximates the circle as a polygon that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between an edge and the circle, in drawing units. Zero picks the automatic share of the radius.</param>
        /// <returns>A polygon inscribed in the circle.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolygon2 ToPolygonByChordTolerance(double chordTolerance)
        {
            return ToPolygon(Math.Max(3, Internal.Tessellation.SegmentsForChordTolerance(Radius, Math.PI * 2.0, chordTolerance)));
        }

        /// <summary>
        /// Approximates the circle as a polygon whose vertices are no further apart than a spacing.
        /// </summary>
        /// <param name="spacing">The largest distance allowed between two vertices, measured along the circumference.</param>
        /// <returns>A polygon inscribed in the circle, its vertices evenly spread.</returns>
        /// <remarks>
        /// The circumference rarely divides by the spacing exactly, so the spacing is a limit rather than a
        /// step: the vertices are spread evenly and no two are further apart than asked, which leaves no
        /// short edge at the end.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolygon2 ToPolygonBySpacing(double spacing)
        {
            return ToPolygon(Math.Max(3, Internal.Tessellation.SegmentsForSpacing(Length, Math.PI * 2.0, spacing)));
        }

        /// <summary>
        /// Approximates the circle as a polygon with a given number of edges.
        /// </summary>
        /// <param name="segmentCount">How many edges the polygon should have; at least three.</param>
        /// <returns>A polygon inscribed in the circle.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three edges are asked for.</exception>
        public GeoPolygon2 ToPolygon(int segmentCount)
        {
            Internal.Tessellation.RequireSegmentCount(segmentCount, 3);

            GeoPoint2[] vertices = new GeoPoint2[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                vertices[i] = GetPointAtParameter((double)i / segmentCount);
            }

            return new GeoPolygon2(vertices);
        }

        /// <summary>
        /// Approximates the circle as a chain, cut by the automatic chord tolerance.
        /// </summary>
        /// <returns>A chain from the zero parameter all the way round, its first point repeated at the end.</returns>
        /// <remarks>
        /// A chain is always open, so the first point is repeated at the end to close the loop: that is what
        /// it takes for a <see cref="GeoPolyline2"/> to trace something closed.
        /// </remarks>
        public GeoPolyline2 ToPolyline() => ToPolylineByChordTolerance(0.0);

        /// <summary>
        /// Approximates the circle as a chain that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the circle, in drawing units. Zero picks the automatic share of the radius.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolyline2 ToPolylineByChordTolerance(double chordTolerance)
        {
            return ToPolyline(Math.Max(3, Internal.Tessellation.SegmentsForChordTolerance(Radius, Math.PI * 2.0, chordTolerance)));
        }

        /// <summary>
        /// Approximates the circle as a chain whose points are no further apart than a spacing.
        /// </summary>
        /// <param name="spacing">The largest distance allowed between two points, measured along the circumference.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolyline2 ToPolylineBySpacing(double spacing)
        {
            return ToPolyline(Math.Max(3, Internal.Tessellation.SegmentsForSpacing(Length, Math.PI * 2.0, spacing)));
        }

        /// <summary>
        /// Approximates the circle as a chain with a given number of pieces.
        /// </summary>
        /// <param name="segmentCount">How many pieces the chain should have; at least three.</param>
        /// <returns>A chain with its first point repeated at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three pieces are asked for.</exception>
        public GeoPolyline2 ToPolyline(int segmentCount)
        {
            Internal.Tessellation.RequireSegmentCount(segmentCount, 3);

            GeoPoint2[] points = new GeoPoint2[segmentCount + 1];

            for (int i = 0; i < segmentCount; i++)
            {
                points[i] = GetPointAtParameter((double)i / segmentCount);
            }

            points[segmentCount] = points[0];

            return new GeoPolyline2(points);
        }

        #endregion

        /// <summary>
        /// Determines whether another circle has the same centre and radius, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCircle2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another circle has the same centre and radius, within a tolerance.
        /// </summary>
        /// <param name="other">The circle to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the circles are the same within tolerance; otherwise, false.</returns>
        public bool IsEqualTo(GeoCircle2 other, Tolerance tolerance)
        {
            return Center.IsEqualTo(other.Center, tolerance) && Math.Abs(Radius - other.Radius) <= tolerance.EqualPoint;
        }
    }
}
