using System;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry.Core;

namespace SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D point with double precision coordinates.
    /// </summary>
    public readonly struct GeoPoint3 : IEquatable<GeoPoint3>
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
        /// Gets the Z coordinate of the point.
        /// </summary>
        public double Z { get; }

        /// <summary>
        /// Gets the point at the global origin (0, 0, 0).
        /// </summary>
        public static GeoPoint3 Origin => new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>
        /// Initializes a new point.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="z">Z coordinate.</param>
        public GeoPoint3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Initializes a new point from another point.
        /// </summary>
        /// <param name="point">Source point.</param>
        public GeoPoint3(GeoPoint3 point)
            : this(point.X, point.Y, point.Z)
        {
        }

        /// <summary>
        /// Creates a copy of this point.
        /// </summary>
        /// <remarks>
        /// Point is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same
        /// way to ask for a copy.
        /// </remarks>
        /// <returns>A new point with the same coordinates.</returns>
        public GeoPoint3 Clone() => new GeoPoint3(X, Y, Z);

        #region Arithmetic

        /// <summary>
        /// Translates the point by a vector.
        /// </summary>
        public GeoPoint3 Add(GeoVector3 vector) => new GeoPoint3(X + vector.X, Y + vector.Y, Z + vector.Z);

        /// <summary>
        /// Translates the point by the negation of a vector.
        /// </summary>
        public GeoPoint3 Subtract(GeoVector3 vector) => new GeoPoint3(X - vector.X, Y - vector.Y, Z - vector.Z);

        /// <summary>
        /// Gets the vector pointing from this point to another point.
        /// </summary>
        public GeoVector3 GetVectorTo(GeoPoint3 other) => new GeoVector3(other.X - X, other.Y - Y, other.Z - Z);

        /// <summary>
        /// Gets the point halfway between this point and another point.
        /// </summary>
        public GeoPoint3 GetMiddlePoint(GeoPoint3 other)
        {
            return new GeoPoint3((X + other.X) * 0.5, (Y + other.Y) * 0.5, (Z + other.Z) * 0.5);
        }

        /// <summary>
        /// Gets this point read as a vector from the global origin.
        /// </summary>
        public GeoVector3 ToVector() => new GeoVector3(X, Y, Z);

        /// <summary>
        /// Applies a transformation to this point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoPoint3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        #endregion

        #region Distance

        /// <summary>
        /// Calculates the Euclidean distance to another point.
        /// </summary>
        public double DistanceTo(GeoPoint3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the squared Euclidean distance to another point.
        /// </summary>
        public double GetDistanceSquaredTo(GeoPoint3 other) => Distance3.GetDistanceSquaredTo(this, other);

        /// <summary>
        /// Calculates the shortest distance to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Calculates the shortest distance to a ray.
        /// </summary>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the perpendicular distance to a plane.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(triangle, this);

        /// <summary>
        /// Calculates the shortest distance to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest distance to a polygon, read as a filled surface.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(polygon, this);

        /// <summary>
        /// Calculates the shortest distance to a face, holes respected.
        /// </summary>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance to a circular disc, read as a filled surface.
        /// </summary>
        public double DistanceTo(GeoCircle3 circle) => Distance3.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest distance to an oriented box. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(box, this);

        /// <summary>
        /// Calculates the shortest distance to an axis-aligned box. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(box, this);

        /// <summary>
        /// Calculates the shortest distance to a solid. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        #endregion

        #region Projection

        /// <summary>
        /// Gets the closest point on a line segment to this point, clamped to its endpoints.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoLine3 line) => Projection3.ProjectToLine(line, this);

        /// <summary>
        /// Gets the closest point on a ray to this point, clamped to its origin.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoRay3 ray) => Projection3.ProjectToRay(ray, this);

        /// <summary>
        /// Gets the closest point on a plane to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPlane3 plane) => Projection3.ProjectToPlane(plane, this);

        /// <summary>
        /// Gets the closest point on a triangle to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoTriangle3 triangle) => Projection3.ProjectToTriangle(triangle, this);

        /// <summary>
        /// Gets the closest point on a polyline to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPolyline3 polyline) => Projection3.ProjectToPolyline(polyline, this);

        /// <summary>
        /// Gets the closest point on a polygon to this point, read as a filled surface.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPolygon3 polygon) => Projection3.ProjectToPolygon(polygon, this);

        /// <summary>
        /// Gets the closest point on the circumference of a circle to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoCircle3 circle) => Projection3.ProjectToCircle(circle, this);

        /// <summary>
        /// Gets the closest point of an oriented box to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoObb3 box) => Projection3.ProjectToObb(box, this);

        /// <summary>
        /// Gets the closest point of an axis-aligned box to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoAabb3 box) => box.GetClosestPointOnBoundary(this);

        /// <summary>
        /// Gets the closest point on the surface of a solid to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoSolid3 solid) => Projection3.ProjectToSolid(solid, this);

        #endregion

        #region Predicates

        /// <summary>
        /// Compares whether this point equals another point using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPoint3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this point equals another point within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPoint3 other, Tolerance tolerance)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz <= tolerance.EqualPoint * tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether this point lies on a line segment using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoLine3 line) => Containment3.IsPointOn(line, this);

        /// <summary>
        /// Checks whether this point lies on a line segment within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoLine3 line, Tolerance tolerance) => Containment3.IsPointOn(line, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on a ray using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoRay3 ray) => Containment3.IsPointOn(ray, this);

        /// <summary>
        /// Checks whether this point lies on a ray within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoRay3 ray, Tolerance tolerance) => Containment3.IsPointOn(ray, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on a plane using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPlane3 plane) => Containment3.IsPointOn(plane, this);

        /// <summary>
        /// Checks whether this point lies on a plane within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPlane3 plane, Tolerance tolerance) => Containment3.IsPointOn(plane, this, tolerance);

        /// <summary>
        /// Determines which side of a plane this point lies on, using the default tolerance.
        /// </summary>
        public PlaneSide GetSideOf(GeoPlane3 plane) => Containment3.GetSide(plane, this);

        /// <summary>
        /// Determines which side of a plane this point lies on, within a tolerance.
        /// </summary>
        public PlaneSide GetSideOf(GeoPlane3 plane, Tolerance tolerance) => Containment3.GetSide(plane, this, tolerance);

        /// <summary>
        /// Locates this point relative to a triangle, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoTriangle3 triangle) => Containment3.Locate(triangle, this);

        /// <summary>
        /// Locates this point relative to a triangle, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoTriangle3 triangle, Tolerance tolerance) => Containment3.Locate(triangle, this, tolerance);

        /// <summary>
        /// Locates this point relative to a polygon, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoPolygon3 polygon) => Containment3.Locate(polygon, this);

        /// <summary>
        /// Locates this point relative to a polygon, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoPolygon3 polygon, Tolerance tolerance) => Containment3.Locate(polygon, this, tolerance);

        /// <summary>
        /// Locates this point relative to a face, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoFace3 face) => Containment3.Locate(face, this);

        /// <summary>
        /// Locates this point relative to a face, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoFace3 face, Tolerance tolerance) => Containment3.Locate(face, this, tolerance);

        /// <summary>
        /// Locates this point relative to a circular disc, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoCircle3 circle) => Containment3.Locate(circle, this);

        /// <summary>
        /// Locates this point relative to a circular disc, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoCircle3 circle, Tolerance tolerance) => Containment3.Locate(circle, this, tolerance);

        /// <summary>
        /// Locates this point relative to an oriented box, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoObb3 box) => Containment3.Locate(box, this);

        /// <summary>
        /// Locates this point relative to an oriented box, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoObb3 box, Tolerance tolerance) => Containment3.Locate(box, this, tolerance);

        /// <summary>
        /// Locates this point relative to an axis-aligned box, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoAabb3 box) => box.Locate(this);

        /// <summary>
        /// Locates this point relative to an axis-aligned box, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoAabb3 box, Tolerance tolerance) => box.Locate(this, tolerance);

        /// <summary>
        /// Locates this point relative to a solid, using the default tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoSolid3 solid) => Containment3.Locate(solid, this);

        /// <summary>
        /// Locates this point relative to a solid, within a tolerance.
        /// </summary>
        public PointLocation LocateIn(GeoSolid3 solid, Tolerance tolerance) => Containment3.Locate(solid, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on a polyline, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPolyline3 polyline) => Containment3.IsPointOn(polyline, this);

        /// <summary>
        /// Checks whether this point lies on a polyline, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPolyline3 polyline, Tolerance tolerance) => Containment3.IsPointOn(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on the boundary of a polygon, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPolygon3 polygon) => Containment3.IsPointOn(polygon, this);

        /// <summary>
        /// Checks whether this point lies on the boundary of a polygon, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPolygon3 polygon, Tolerance tolerance) => Containment3.IsPointOn(polygon, this, tolerance);

        /// <summary>
        /// Checks whether this point lies on the circumference of a circle, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoCircle3 circle) => Containment3.IsPointOn(circle, this);

        /// <summary>
        /// Checks whether this point lies on the circumference of a circle, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoCircle3 circle, Tolerance tolerance) => Containment3.IsPointOn(circle, this, tolerance);

        #endregion

        #region Equality and operators

        /// <summary>
        /// Determines whether the specified point has exactly the same coordinates as this point.
        /// </summary>
        public bool Equals(GeoPoint3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        /// <summary>
        /// Determines whether the specified object is equal to the current point.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPoint3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Translates a point by a vector.
        /// </summary>
        public static GeoPoint3 operator +(GeoPoint3 point, GeoVector3 vector) => point.Add(vector);

        /// <summary>
        /// Translates a point by the negation of a vector.
        /// </summary>
        public static GeoPoint3 operator -(GeoPoint3 point, GeoVector3 vector) => point.Subtract(vector);

        /// <summary>
        /// Calculates the vector pointing from start to end.
        /// </summary>
        public static GeoVector3 operator -(GeoPoint3 end, GeoPoint3 start) => start.GetVectorTo(end);

        /// <summary>
        /// Checks if two points have exactly the same coordinates.
        /// </summary>
        public static bool operator ==(GeoPoint3 left, GeoPoint3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two points differ in any coordinate.
        /// </summary>
        public static bool operator !=(GeoPoint3 left, GeoPoint3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current point.
        /// </summary>
        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }
}
