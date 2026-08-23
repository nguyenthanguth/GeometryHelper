using System;
using CommonGeometry;
using CommonGeometry.Enums;
using PlaneGeometry.Core;

namespace PlaneGeometry.Geometry
{
    /// <summary>
    /// Represents a 2D point with double precision coordinates.
    /// </summary>
    public readonly struct GeoPoint2 : IEquatable<GeoPoint2>
    {
        /// <summary>
        /// Gets the X coordinate of the point.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Gets the Y coordinate of the point.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Initializes a new point at the origin (0, 0).
        /// </summary>
        public GeoPoint2()
        {
            X = 0.0;
            Y = 0.0;
        }

        /// <summary>
        /// Initializes a new point.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        public GeoPoint2(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Initializes a new point from another point.
        /// </summary>
        /// <param name="geoPoint">Source point.</param>
        public GeoPoint2(GeoPoint2 geoPoint)
        {
            X = geoPoint.X;
            Y = geoPoint.Y;
        }

        /// <summary>
        /// Creates a copy of this point.
        /// </summary>
        /// <remarks>
        /// Point is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        /// <returns>A new point with the same coordinates.</returns>
        public GeoPoint2 Clone() => new GeoPoint2(X, Y);

        /// <summary>
        /// Adds a GeoVector2 to the point for translation.
        /// </summary>
        public GeoPoint2 Add(GeoVector2 geoVector) => new GeoPoint2(X + geoVector.X, Y + geoVector.Y);

        /// <summary>
        /// Subtracts a GeoVector2 from the point.
        /// </summary>
        public GeoPoint2 Subtract(GeoVector2 geoVector) => new GeoPoint2(X - geoVector.X, Y - geoVector.Y);

        /// <summary>
        /// Gets the GeoVector2 pointing from this point to another point.
        /// </summary>
        public GeoVector2 GetVectorTo(GeoPoint2 other) => new GeoVector2(other.X - X, other.Y - Y);

        /// <summary>
        /// Calculates the Euclidean distance to another point.
        /// </summary>
        public double DistanceTo(GeoPoint2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the squared Euclidean distance to another point.
        /// </summary>
        public double GetDistanceSquaredTo(GeoPoint2 other) => Distance2.GetDistanceSquaredTo(this, other);

        /// <summary>
        /// Calculates the Euclidean distance to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(line, this);

        /// <summary>
        /// Calculates the Euclidean distance to a circle boundary.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the Euclidean distance to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the Euclidean distance to a polygon boundary.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Calculates the Euclidean distance to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the closest point on a line segment to this point, clamped to its endpoints.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoLine2 line) => Projection2.ProjectToLine(line, this);

        /// <summary>
        /// Gets the closest point on the circumference of a circle to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoCircle2 circle) => Projection2.ProjectToCircle(circle, this);

        /// <summary>
        /// Gets the closest point on the boundary of a rectangle to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoRectangle2 rect) => Projection2.ProjectToRectangle(rect, this);

        /// <summary>
        /// Gets the closest point on the boundary of a polygon to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolygon2 poly) => Projection2.ProjectToPolygon(poly, this);

        /// <summary>
        /// Gets the closest point on the path of a polyline to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolyline2 polyline) => Projection2.ProjectToPolyline(polyline, this);

        /// <summary>
        /// Checks whether this point lies on the line segment using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoLine2 line) => Containment2.IsPointOn(line, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this point lies on the line segment within tolerance.
        /// </summary>
        public bool IsPointOn(GeoLine2 line, Tolerance tolerance) => Containment2.IsPointOn(line, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on the polyline using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPolyline2 polyline) => Containment2.IsPointOn(polyline, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this point lies on the polyline within tolerance.
        /// </summary>
        public bool IsPointOn(GeoPolyline2 polyline, Tolerance tolerance) => Containment2.IsPointOn(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on the circle circumference using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoCircle2 circle) => Containment2.IsPointOn(circle, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this point lies on the circle circumference within tolerance.
        /// </summary>
        public bool IsPointOn(GeoCircle2 circle, Tolerance tolerance) => Containment2.IsPointOn(circle, this, tolerance);

        /// <summary>
        /// Classifies the location of this point relative to a circle (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoCircle2 circle) => Containment2.Locate(circle, this, Tolerance.Global);

        /// <summary>
        /// Classifies the location of this point relative to a circle (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoCircle2 circle, Tolerance tolerance) => Containment2.Locate(circle, this, tolerance);

        /// <summary>
        /// Classifies the location of this point relative to a rectangle (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoRectangle2 rect) => Containment2.Locate(rect, this, Tolerance.Global);

        /// <summary>
        /// Classifies the location of this point relative to a rectangle (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoRectangle2 rect, Tolerance tolerance) => Containment2.Locate(rect, this, tolerance);

        /// <summary>
        /// Classifies the location of this point relative to a polygon (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoPolygon2 poly) => Containment2.Locate(poly, this, Tolerance.Global);

        /// <summary>
        /// Classifies the location of this point relative to a polygon (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoPolygon2 poly, Tolerance tolerance) => Containment2.Locate(poly, this, tolerance);

        /// <summary>
        /// Classifies the location of this point relative to a polyline (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoPolyline2 polyline) => Containment2.Locate(polyline, this, Tolerance.Global);

        /// <summary>
        /// Classifies the location of this point relative to a polyline (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoPolyline2 polyline, Tolerance tolerance) => Containment2.Locate(polyline, this, tolerance);

        /// <summary>
        /// Classifies the location of this point relative to a line segment (OnSide or OutSide) using default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoLine2 line) => Containment2.Locate(line, this, Tolerance.Global);

        /// <summary>
        /// Classifies the location of this point relative to a line segment (OnSide or OutSide) within tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoLine2 line, Tolerance tolerance) => Containment2.Locate(line, this, tolerance);

        /// <summary>
        /// Gets the midpoint between this point and another point.
        /// </summary>
        public GeoPoint2 GetMiddlePoint(GeoPoint2 other) => new GeoPoint2((X + other.X) * 0.5, (Y + other.Y) * 0.5);

        /// <summary>
        /// Rotates the point around a center with a specified rotation angle in radians (counter-clockwise).
        /// </summary>
        public GeoPoint2 RotateBy(double angleRad, GeoPoint2 center)
        {
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            double dx = X - center.X;
            double dy = Y - center.Y;

            return new GeoPoint2(
                center.X + dx * cos - dy * sin,
                center.Y + dx * sin + dy * cos);
        }

        /// <summary>
        /// Scales the distance from the center to this point by a factor.
        /// </summary>
        public GeoPoint2 ScaleBy(double factor, GeoPoint2 center)
        {
            return new GeoPoint2(
                center.X + (X - center.X) * factor,
                center.Y + (Y - center.Y) * factor);
        }

        /// <summary>
        /// Compares whether this point is coincident with another point within the allowed tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPoint2 other, Tolerance tolerance) => GetDistanceSquaredTo(other) <= tolerance.EqualPoint * tolerance.EqualPoint;

        /// <summary>
        /// Compares whether this point is coincident with another point using default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPoint2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Translates a point by a vector.
        /// </summary>
        /// <param name="p">The point to translate.</param>
        /// <param name="v">The vector to apply.</param>
        /// <returns>A new GeoPoint2 representing the translated position.</returns>
        public static GeoPoint2 operator +(GeoPoint2 p, GeoVector2 v) => p.Add(v);

        /// <summary>
        /// Translates a point back by a vector.
        /// </summary>
        /// <param name="p">The point to translate.</param>
        /// <param name="v">The vector to subtract.</param>
        /// <returns>A new GeoPoint2 representing the translated position.</returns>
        public static GeoPoint2 operator -(GeoPoint2 p, GeoVector2 v) => p.Subtract(v);

        /// <summary>
        /// Calculates the vector from one point to another.
        /// </summary>
        /// <param name="p2">The destination point.</param>
        /// <param name="p1">The start point.</param>
        /// <returns>A GeoVector2 representing the direction and distance from p1 to p2.</returns>
        public static GeoVector2 operator -(GeoPoint2 p2, GeoPoint2 p1) => p1.GetVectorTo(p2);

        /// <summary>
        /// Indicates whether the current point is equal to another point.
        /// </summary>
        /// <param name="other">A point to compare with this point.</param>
        /// <returns>true if the current point is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(GeoPoint2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoPoint2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two GeoPoint2 instances for equality.
        /// </summary>
        /// <param name="left">The first point.</param>
        /// <param name="right">The second point.</param>
        /// <returns>true if they are equal; otherwise, false.</returns>
        public static bool operator ==(GeoPoint2 left, GeoPoint2 right) => left.Equals(right);

        /// <summary>
        /// Compares two GeoPoint2 instances for inequality.
        /// </summary>
        /// <param name="left">The first point.</param>
        /// <param name="right">The second point.</param>
        /// <returns>true if they are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoPoint2 left, GeoPoint2 right) => !left.Equals(right);

        /// <summary>
        /// Returns the string representation of the point.
        /// </summary>
        /// <returns>A string representation formatted as (X, Y).</returns>
        public override string ToString() => $"({X:0.000}, {Y:0.000})";
    }
}
