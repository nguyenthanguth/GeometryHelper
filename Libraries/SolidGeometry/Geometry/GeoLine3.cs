using System;
using CommonGeometry;
using SolidGeometry.Core;

namespace SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D line segment defined by a start point and an end point.
    /// </summary>
    public readonly struct GeoLine3 : IEquatable<GeoLine3>
    {
        /// <summary>
        /// Gets the start point of the line segment.
        /// </summary>
        public GeoPoint3 StartPoint { get; }

        /// <summary>
        /// Gets the end point of the line segment.
        /// </summary>
        public GeoPoint3 EndPoint { get; }

        /// <summary>
        /// Initializes a new line segment from its endpoints.
        /// </summary>
        /// <param name="startPoint">Start point.</param>
        /// <param name="endPoint">End point.</param>
        public GeoLine3(GeoPoint3 startPoint, GeoPoint3 endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        /// <summary>
        /// Initializes a new line segment from the coordinates of its endpoints.
        /// </summary>
        public GeoLine3(double startX, double startY, double startZ, double endX, double endY, double endZ)
            : this(new GeoPoint3(startX, startY, startZ), new GeoPoint3(endX, endY, endZ))
        {
        }

        /// <summary>
        /// Creates a copy of this line segment.
        /// </summary>
        /// <remarks>
        /// Line segment is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        public GeoLine3 Clone() => new GeoLine3(StartPoint, EndPoint);

        /// <summary>
        /// Gets the vector pointing from the start point to the end point. Its length is the length of
        /// the segment.
        /// </summary>
        public GeoVector3 Direction => StartPoint.GetVectorTo(EndPoint);

        /// <summary>
        /// Gets the length of the line segment.
        /// </summary>
        public double Length => StartPoint.DistanceTo(EndPoint);

        /// <summary>
        /// Gets the squared length of the line segment.
        /// </summary>
        public double LengthSquared => StartPoint.GetDistanceSquaredTo(EndPoint);

        /// <summary>
        /// Gets the midpoint of the line segment.
        /// </summary>
        public GeoPoint3 MidPoint => StartPoint.GetMiddlePoint(EndPoint);

        /// <summary>
        /// Gets the line segment running the other way.
        /// </summary>
        public GeoLine3 Reverse() => new GeoLine3(EndPoint, StartPoint);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this segment.
        /// </summary>
        public GeoAabb3 GetAabb() => new GeoAabb3(StartPoint, EndPoint);

        /// <summary>
        /// Applies a transformation to this segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoLine3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the infinite plane through this segment carrying a given in-plane direction.
        /// </summary>
        /// <param name="inPlaneDirection">A second direction the plane must contain.</param>
        /// <returns>The plane through the start point spanned by this segment and the supplied direction.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the segment is degenerate or the two directions are parallel, since neither case
        /// pins down a plane.
        /// </exception>
        public GeoPlane3 GetPlaneWith(GeoVector3 inPlaneDirection)
        {
            return new GeoPlane3(StartPoint, Direction.CrossProduct(inPlaneDirection));
        }

        #region Parametrization

        /// <summary>
        /// Gets the point at a normalized parameter along the segment. 0 is the start point and 1 the end
        /// point; values outside that range extrapolate along the infinite line carrying the segment.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => Parametrization3.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on the infinite line carrying this segment that is
        /// closest to the supplied point. The point need not lie on the segment, so the result may fall
        /// outside [0, 1].
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Parametrization3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from the start point.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => Parametrization3.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the start point to the point on this segment closest to the supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Parametrization3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the start point to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start point.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization3.GetParameterAtDistance(this, distance);

        #endregion

        #region Distance and projection

        /// <summary>
        /// Calculates the shortest distance from this segment to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance between this segment and another segment.
        /// </summary>
        public double DistanceTo(GeoLine3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between this segment and a ray.
        /// </summary>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this segment and a plane.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this segment and a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance between this segment and a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this segment and a solid. A segment reaching into the
        /// body is at distance zero.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(this, solid);

        /// <summary>
        /// Gets the closest point on this segment to a target point, clamped to the endpoints.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToLine(this, point);

        /// <summary>
        /// Finds the shortest segment connecting a point on this segment to a point on another segment,
        /// using the default tolerance.
        /// </summary>
        public GeoLine3 GetClosestOnBoundary(GeoLine3 other) => Projection3.GetClosestSegment(this, other);

        /// <summary>
        /// Finds the shortest segment connecting a point on this segment to a point on another segment,
        /// within a tolerance.
        /// </summary>
        public GeoLine3 GetClosestOnBoundary(GeoLine3 other, Tolerance tolerance) => Projection3.GetClosestSegment(this, other, tolerance);

        #endregion

        #region Predicates and intersection

        /// <summary>
        /// Checks whether the segment is shorter than the default tolerance, so it has no usable direction.
        /// </summary>
        public bool IsDegenerate() => IsDegenerate(Tolerance.Global);

        /// <summary>
        /// Checks whether the segment is shorter than a tolerance, so it has no usable direction.
        /// </summary>
        public bool IsDegenerate(Tolerance tolerance) => Direction.IsZeroLength(tolerance);

        /// <summary>
        /// Checks whether a point lies on this segment using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on this segment within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Checks whether this segment is parallel to another segment using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 other) => Parallel3.IsParallel(this, other);

        /// <summary>
        /// Checks whether this segment is parallel to another segment within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 other, Tolerance tolerance) => Parallel3.IsParallel(this, other, tolerance);

        /// <summary>
        /// Checks whether this segment is perpendicular to another segment using the default tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoLine3 other) => Parallel3.IsPerpendicular(this, other);

        /// <summary>
        /// Checks whether this segment is perpendicular to another segment within a tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoLine3 other, Tolerance tolerance) => Parallel3.IsPerpendicular(this, other, tolerance);

        /// <summary>
        /// Checks whether this segment runs parallel to a plane using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane) => Parallel3.IsParallel(this, plane);

        /// <summary>
        /// Checks whether this segment runs parallel to a plane within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane, Tolerance tolerance) => Parallel3.IsParallel(this, plane, tolerance);

        /// <summary>
        /// Checks whether this segment and another segment lie on a common plane, using the default tolerance.
        /// </summary>
        public bool IsCoplanarWith(GeoLine3 other) => Parallel3.IsCoplanar(this, other);

        /// <summary>
        /// Checks whether this segment and another segment lie on a common plane, within a tolerance.
        /// </summary>
        public bool IsCoplanarWith(GeoLine3 other, Tolerance tolerance) => Parallel3.IsCoplanar(this, other, tolerance);

        /// <summary>
        /// Tries to find the single point where this segment meets another segment, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 other, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, other, out intersection);

        /// <summary>
        /// Tries to find the single point where this segment meets another segment, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 other, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this segment crosses a plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, plane, out intersection);

        /// <summary>
        /// Tries to find the point where this segment crosses a plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, plane, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this segment crosses a triangle, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, triangle, out intersection);

        /// <summary>
        /// Tries to find the point where this segment crosses a triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, triangle, out intersection, tolerance);

        #endregion

        #region Splitting

        /// <summary>
        /// Splits this segment at an arc length from its start, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// Splitting sits on the subject rather than on the cutter, since <c>plane.Split(line)</c> would
        /// not say which of the two comes back in pieces.
        /// </remarks>
        public bool TrySplitAtDistance(double distance, out GeoLine3[] pieces) => Splition3.TrySplitAtDistance(this, distance, out pieces);

        /// <summary>
        /// Splits this segment at an arc length from its start, within a tolerance.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitAtDistance(this, distance, out pieces, tolerance);

        /// <summary>
        /// Splits this segment at a point on it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint3 point, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, point, out pieces);

        /// <summary>
        /// Splits this segment at a point on it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint3 point, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, point, out pieces, tolerance);

        /// <summary>
        /// Splits this segment where a plane crosses it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this segment where a plane crosses it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this segment at several arc lengths at once, using the default tolerance.
        /// </summary>
        public GeoLine3[] SplitAtDistances(System.Collections.Generic.IEnumerable<double> distances) => Splition3.SplitAtDistances(this, distances);

        /// <summary>
        /// Splits this segment at several arc lengths at once, within a tolerance.
        /// </summary>
        public GeoLine3[] SplitAtDistances(System.Collections.Generic.IEnumerable<double> distances, Tolerance tolerance) => Splition3.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Splits this segment by a solid, sorting the pieces into those inside it and those outside,
        /// using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this segment by a solid, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by an oriented box, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this segment by an oriented box, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this segment by an axis-aligned box, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by a plane and sorts the pieces by side, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] above, out GeoLine3[] below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this segment by a plane and sorts the pieces by side, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] above, out GeoLine3[] below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Splits this segment wherever it passes through a polygon, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolygon3 cutter, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this segment wherever it passes through a polygon, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolygon3 cutter, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this segment wherever it passes through a face, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoFace3 cutter, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this segment wherever it passes through a face, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoFace3 cutter, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another segment has exactly the same endpoints, in the same order.
        /// </summary>
        public bool Equals(GeoLine3 other) => StartPoint.Equals(other.StartPoint) && EndPoint.Equals(other.EndPoint);

        /// <summary>
        /// Determines whether the specified object is equal to the current segment.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoLine3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (StartPoint.GetHashCode() * 397) ^ EndPoint.GetHashCode();
            }
        }

        /// <summary>
        /// Compares whether this segment equals another segment using the default tolerance, ignoring
        /// which way round the endpoints are given.
        /// </summary>
        public bool IsEqualTo(GeoLine3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this segment equals another segment within a tolerance, ignoring which way
        /// round the endpoints are given.
        /// </summary>
        public bool IsEqualTo(GeoLine3 other, Tolerance tolerance)
        {
            return (StartPoint.IsEqualTo(other.StartPoint, tolerance) && EndPoint.IsEqualTo(other.EndPoint, tolerance)) ||
                   (StartPoint.IsEqualTo(other.EndPoint, tolerance) && EndPoint.IsEqualTo(other.StartPoint, tolerance));
        }

        /// <summary>
        /// Checks if two segments have exactly the same endpoints, in the same order.
        /// </summary>
        public static bool operator ==(GeoLine3 left, GeoLine3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two segments differ in either endpoint.
        /// </summary>
        public static bool operator !=(GeoLine3 left, GeoLine3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current line segment.
        /// </summary>
        public override string ToString() => $"{StartPoint} -> {EndPoint}";
    }
}
