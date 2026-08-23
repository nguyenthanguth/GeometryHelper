using System;
using CommonGeometry;
using CommonGeometry.Enums;
using PlaneGeometry.Core;

namespace PlaneGeometry.Geometry
{
    /// <summary>
    /// Represents a 2D circle with a center point and radius.
    /// </summary>
    public readonly struct GeoCircle2 : IEquatable<GeoCircle2>
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
        /// Gets the circumference (perimeter) of the circle.
        /// </summary>
        public double Circumference => 2.0 * Math.PI * Radius;

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
        /// Calculates the shortest boundary distance from this circle to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest boundary distance from this circle to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest boundary distance from this circle to another circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest boundary distance from this circle to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(this, rect);

        /// <summary>
        /// Calculates the shortest boundary distance from this circle to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(this, poly);

        /// <summary>
        /// Calculates the shortest boundary distance from this circle to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the closest point on the circumference of this circle to a target point, including for
        /// points inside the circle.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToCircle(this, point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a line segment using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoLine2 line) => Projection2.GetClosestSegment(this, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoLine2 line, Tolerance tolerance) => Projection2.GetClosestSegment(this, line, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on another circle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoCircle2 other) => Projection2.GetClosestSegment(this, other, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on another circle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoCircle2 other, Tolerance tolerance) => Projection2.GetClosestSegment(this, other, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a rectangle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoRectangle2 rect) => Projection2.GetClosestSegment(this, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a rectangle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetClosestSegment(this, rect, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a polyline using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolyline2 polyline) => Projection2.GetClosestSegment(this, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on a polyline within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetClosestSegment(this, polyline, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a polygon using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolygon2 poly) => Projection2.GetClosestSegment(this, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of this circle to a point on the boundary of a polygon within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetClosestSegment(this, poly, tolerance);

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
        /// Checks whether this circle collides with another circle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 other) => Collision2.CollidesWith(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this circle collides with another circle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this circle collides with a line segment using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Collision2.CollidesWith(this, line, Tolerance.Global);

        /// <summary>
        /// Checks whether this circle collides with a line segment within tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Collision2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this circle collides with a rotated rectangle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Collision2.CollidesWith(this, rect, Tolerance.Global);

        /// <summary>
        /// Checks whether this circle collides with a rotated rectangle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(this, rect, tolerance);

        /// <summary>
        /// Checks whether this circle collides with a polygon using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly) => Collision2.CollidesWith(this, poly, Tolerance.Global);

        /// <summary>
        /// Checks whether this circle collides with a polygon within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(this, poly, tolerance);

        /// <summary>
        /// Checks whether this circle collides with a polyline using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(this, polyline, Tolerance.Global);

        /// <summary>
        /// Checks whether this circle collides with a polyline within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(this, polyline, tolerance);

        /// <summary>
        /// Gets all intersection points with another circle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 other) => Intersection2.GetIntersections(this, other, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with another circle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 other, Tolerance tolerance) => Intersection2.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Gets all intersection points with a line segment using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Intersection2.GetIntersections(this, line, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a line segment within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Intersection2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets all intersection points with a rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(rect, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polygon using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => Intersection2.GetIntersections(poly, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polygon within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance) => Intersection2.GetIntersections(poly, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

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
        public override string ToString() => $"GeoCircle2[Center:{Center}, Radius:{Radius:0.000}]";

    }
}
