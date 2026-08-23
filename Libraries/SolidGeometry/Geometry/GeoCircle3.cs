using System;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry.Core;

namespace SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a circular disc in 3D space, defined by a centre, a normal and a radius.
    /// <para>
    /// Like a polygon, this is a planar region rather than a curve: it has an area, and a point counts as
    /// inside it only when the point lies on the carrier plane as well as within the radius.
    /// </para>
    /// </summary>
    public readonly struct GeoCircle3 : IEquatable<GeoCircle3>
    {
        /// <summary>
        /// Gets the centre of the circle.
        /// </summary>
        public GeoPoint3 Center { get; }

        /// <summary>
        /// Gets the unit normal of the plane carrying the circle.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets the radius of the circle.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Initializes a new circle.
        /// </summary>
        /// <param name="center">The centre point.</param>
        /// <param name="normal">The normal of the carrying plane; it is normalized on construction.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has zero length.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not positive.</exception>
        public GeoCircle3(GeoPoint3 center, GeoVector3 normal, double radius)
        {
            if (!normal.TryGetNormal(out GeoVector3 unit))
            {
                throw new ArgumentException("A circle needs a normal of non-zero length.", nameof(normal));
            }

            if (radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "A circle must have a positive radius.");
            }

            Center = center;
            Normal = unit;
            Radius = radius;
        }

        /// <summary>
        /// Initializes a new circle lying in a given plane.
        /// </summary>
        /// <param name="plane">The carrying plane; the circle is centred on its origin.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        public GeoCircle3(GeoPlane3 plane, double radius)
            : this(plane.Origin, plane.Normal, radius)
        {
        }

        /// <summary>
        /// Initializes a circle from a normal that is already normalized.
        /// </summary>
        /// <remarks>
        /// Re-normalizing a unit vector shifts its last digits rather than leaving it alone, so a copy
        /// taken through the public constructor would come back unequal to its original. See
        /// <see cref="GeoPlane3"/> for the same reasoning at more length.
        /// </remarks>
        private GeoCircle3(GeoPoint3 center, GeoVector3 unitNormal, double radius, bool alreadyNormalized)
        {
            Center = center;
            Normal = unitNormal;
            Radius = radius;
        }

        /// <summary>
        /// Creates a copy of this circle.
        /// </summary>
        /// <remarks>
        /// Circle is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same
        /// way to ask for a copy.
        /// </remarks>
        public GeoCircle3 Clone() => new GeoCircle3(Center, Normal, Radius, true);

        /// <summary>
        /// Applies a transformation to this circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoCircle3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the diameter of the circle.
        /// </summary>
        public double Diameter => Radius * 2.0;

        /// <summary>
        /// Gets the area of the disc.
        /// </summary>
        public double Area => Math.PI * Radius * Radius;

        /// <summary>
        /// Gets the length of the circumference.
        /// </summary>
        public double Length => 2.0 * Math.PI * Radius;

        /// <summary>
        /// Gets the plane carrying the circle.
        /// </summary>
        public GeoPlane3 GetPlane() => new GeoPlane3(Center, Normal);

        /// <summary>
        /// Gets the point on the circumference at a given angle.
        /// </summary>
        /// <param name="angleRad">
        /// The angle in radians, measured counter-clockwise around the normal from the reference direction
        /// the carrying plane supplies.
        /// </param>
        /// <remarks>
        /// A circle in space has no natural place to start measuring from, so the zero angle sits on the
        /// first of the two axes <see cref="GeoPlane3.GetAxes"/> returns. Which direction that is stays
        /// stable for a given circle but is not otherwise specified.
        /// </remarks>
        public GeoPoint3 GetPointAtAngle(double angleRad)
        {
            GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            return Center
                .Add(uAxis.Multiply(Radius * Math.Cos(angleRad)))
                .Add(vAxis.Multiply(Radius * Math.Sin(angleRad)));
        }

        /// <summary>
        /// Gets the point on the circumference at a normalized parameter, where 0 and 1 are the same point.
        /// The parameter wraps, so 1.25 gives the same point as 0.25.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => Parametrization3.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the point on the circumference at an arc length measured from the zero parameter.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => Parametrization3.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the zero parameter to a normalized parameter around the circumference.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured around the circumference.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization3.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Gets the normalized parameter of the point on the circumference closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Parametrization3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the normalized parameter of the point on the circumference closest to the supplied point,
        /// within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) => Parametrization3.GetParameterAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets the arc length from the zero parameter to the point on the circumference closest to the
        /// supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Parametrization3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the zero parameter to the point on the circumference closest to the
        /// supplied point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) => Parametrization3.GetDistanceAtPoint(this, point, tolerance);

        /// <summary>
        /// Approximates the circle as a polygon.
        /// </summary>
        /// <param name="segmentCount">How many edges the polygon should have; at least three.</param>
        /// <returns>A polygon inscribed in the circle, sharing its orientation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than three segments are asked for.</exception>
        public GeoPolygon3 ToPolygon(int segmentCount)
        {
            if (segmentCount < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentCount), "A polygon needs at least 3 edges.");
            }

            GeoPoint3[] vertices = new GeoPoint3[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                vertices[i] = GetPointAtParameter((double)i / segmentCount);
            }

            return new GeoPolygon3(vertices);
        }

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this circle.
        /// </summary>
        /// <remarks>
        /// The extent along each world axis is the radius times the sine of the angle between that axis
        /// and the circle normal, which is what the identity below computes without any trigonometry: a
        /// circle seen edge-on is flat along the normal and full width across it.
        /// </remarks>
        public GeoAabb3 GetAabb()
        {
            double dx = Radius * Math.Sqrt(Math.Max(0.0, 1.0 - Normal.X * Normal.X));
            double dy = Radius * Math.Sqrt(Math.Max(0.0, 1.0 - Normal.Y * Normal.Y));
            double dz = Radius * Math.Sqrt(Math.Max(0.0, 1.0 - Normal.Z * Normal.Z));

            GeoVector3 extent = new GeoVector3(dx, dy, dz);

            return new GeoAabb3(Center.Subtract(extent), Center.Add(extent));
        }

        #region Queries

        /// <summary>
        /// Locates a point relative to this disc, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this disc, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Containment3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this disc holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Containment3.Contains(this, point);

        /// <summary>
        /// Checks whether this disc holds a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Containment3.Contains(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies on the circumference, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on the circumference, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Gets the point on the circumference closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToCircle(this, point);

        /// <summary>
        /// Gets the point of this disc closest to a target point, read as a filled surface.
        /// </summary>
        public GeoPoint3 GetClosestPointOnSurface(GeoPoint3 point) => Projection3.ProjectToDisc(this, point);

        /// <summary>
        /// Calculates the shortest distance from this disc to a point.
        /// </summary>
        /// <remarks>
        /// The disc counts as a filled surface, so a point directly above the centre is measured straight
        /// down to the surface rather than out to the circumference.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another circle has exactly the same centre, normal and radius.
        /// </summary>
        public bool Equals(GeoCircle3 other)
        {
            return Center.Equals(other.Center) && Normal.Equals(other.Normal) && Radius.Equals(other.Radius);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current circle.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoCircle3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Center.GetHashCode();
                hashCode = (hashCode * 397) ^ Normal.GetHashCode();
                hashCode = (hashCode * 397) ^ Radius.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compares whether this circle equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCircle3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this circle equals another within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCircle3 other, Tolerance tolerance)
        {
            return Center.IsEqualTo(other.Center, tolerance) &&
                   Normal.IsEqualTo(other.Normal, tolerance) &&
                   Math.Abs(Radius - other.Radius) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks if two circles have exactly the same centre, normal and radius.
        /// </summary>
        public static bool operator ==(GeoCircle3 left, GeoCircle3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two circles differ in centre, normal or radius.
        /// </summary>
        public static bool operator !=(GeoCircle3 left, GeoCircle3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current circle.
        /// </summary>
        public override string ToString() => $"Circle3(Center: {Center}, Normal: {Normal}, Radius: {Radius:0.###})";
    }
}
