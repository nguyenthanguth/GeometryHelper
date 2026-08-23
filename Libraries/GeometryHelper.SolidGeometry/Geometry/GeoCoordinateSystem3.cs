using System;
using GeometryHelper.CommonGeometry;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D local coordinate system: an origin and three orthonormal axes.
    /// <para>
    /// The axes are always orthonormal, whatever is passed in. The supplied X direction is kept, the Z
    /// axis is taken as X × Y, and the Y axis is then rebuilt as Z × X. That third step is what makes the
    /// system square: keeping the supplied Y unchanged would leave a skewed frame in which
    /// <see cref="ToLocal(GeoPoint3)"/> and <see cref="ToGlobal(GeoPoint3)"/> stop being inverses of each
    /// other, and every measurement taken through it would be quietly wrong.
    /// </para>
    /// </summary>
    public readonly struct GeoCoordinateSystem3 : IEquatable<GeoCoordinateSystem3>
    {
        /// <summary>
        /// Gets the origin point of the coordinate system.
        /// </summary>
        public GeoPoint3 Origin { get; }

        /// <summary>
        /// Gets the local X axis, a unit vector.
        /// </summary>
        public GeoVector3 XAxis { get; }

        /// <summary>
        /// Gets the local Y axis, a unit vector perpendicular to the other two.
        /// </summary>
        public GeoVector3 YAxis { get; }

        /// <summary>
        /// Gets the local Z axis, a unit vector perpendicular to the other two.
        /// </summary>
        public GeoVector3 ZAxis { get; }

        /// <summary>
        /// Gets the global coordinate system: origin at (0, 0, 0) with the world axes.
        /// </summary>
        public static GeoCoordinateSystem3 Global => new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

        /// <summary>
        /// Initializes a new coordinate system from an origin and two directions.
        /// </summary>
        /// <param name="origin">The origin point.</param>
        /// <param name="xAxis">The direction the local X axis should follow.</param>
        /// <param name="yAxis">A direction on the positive Y side of the local XY plane.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when either direction has zero length, or when the two are parallel, since parallel
        /// directions do not span a plane and leave the third axis undefined.
        /// </exception>
        public GeoCoordinateSystem3(GeoPoint3 origin, GeoVector3 xAxis, GeoVector3 yAxis)
        {
            if (!xAxis.TryGetNormal(out GeoVector3 unitX))
            {
                throw new ArgumentException("A coordinate system needs an X direction of non-zero length.", nameof(xAxis));
            }

            if (!xAxis.CrossProduct(yAxis).TryGetNormal(out GeoVector3 unitZ))
            {
                throw new ArgumentException("The X and Y directions must not be parallel.", nameof(yAxis));
            }

            Origin = origin;
            XAxis = unitX;
            ZAxis = unitZ;
            YAxis = unitZ.CrossProduct(unitX);
        }

        /// <summary>
        /// Initializes a coordinate system whose XY plane is a given plane.
        /// </summary>
        /// <param name="plane">The plane; its normal becomes the local Z axis.</param>
        /// <remarks>
        /// A plane fixes only one axis, so the in-plane pair comes from
        /// <see cref="GeoPlane3.GetAxes"/> and is unspecified beyond being orthonormal and right-handed.
        /// </remarks>
        public GeoCoordinateSystem3(GeoPlane3 plane)
        {
            plane.GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            Origin = plane.Origin;
            XAxis = uAxis;
            YAxis = vAxis;
            ZAxis = plane.Normal;
        }

        /// <summary>
        /// Initializes a coordinate system from axes that are already orthonormal.
        /// </summary>
        /// <remarks>
        /// Re-orthonormalizing a frame that is already square is not the identity in floating point: two
        /// cross products and two normalizations shift the last digits of every axis. A copy must not
        /// drift like that, so it skips the work rather than repeating it.
        /// </remarks>
        private GeoCoordinateSystem3(GeoPoint3 origin, GeoVector3 xAxis, GeoVector3 yAxis, GeoVector3 zAxis)
        {
            Origin = origin;
            XAxis = xAxis;
            YAxis = yAxis;
            ZAxis = zAxis;
        }

        /// <summary>
        /// Creates a copy of this coordinate system.
        /// </summary>
        /// <remarks>
        /// Coordinate system is a readonly struct, so plain assignment already produces an independent
        /// copy and this method is not needed to avoid sharing. It exists so that every geometry type
        /// offers the same way to ask for a copy.
        /// </remarks>
        public GeoCoordinateSystem3 Clone() => new GeoCoordinateSystem3(Origin, XAxis, YAxis, ZAxis);

        /// <summary>
        /// Gets the XY plane of this coordinate system, oriented along the local Z axis.
        /// </summary>
        public GeoPlane3 GetPlane() => new GeoPlane3(Origin, ZAxis);

        /// <summary>
        /// Applies a transformation to this coordinate system.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoCoordinateSystem3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Converts a point given in world coordinates into this system's local coordinates.
        /// </summary>
        public GeoPoint3 ToLocal(GeoPoint3 point)
        {
            GeoVector3 offset = Origin.GetVectorTo(point);

            return new GeoPoint3(
                offset.DotProduct(XAxis),
                offset.DotProduct(YAxis),
                offset.DotProduct(ZAxis));
        }

        /// <summary>
        /// Converts a point given in this system's local coordinates into world coordinates.
        /// </summary>
        public GeoPoint3 ToGlobal(GeoPoint3 point)
        {
            return Origin
                .Add(XAxis.Multiply(point.X))
                .Add(YAxis.Multiply(point.Y))
                .Add(ZAxis.Multiply(point.Z));
        }

        /// <summary>
        /// Converts a vector given in world coordinates into this system's local coordinates.
        /// </summary>
        /// <remarks>
        /// A vector carries a direction and a length but no position, so the origin plays no part here.
        /// This is what separates the vector overload from the point one.
        /// </remarks>
        public GeoVector3 ToLocal(GeoVector3 vector)
        {
            return new GeoVector3(
                vector.DotProduct(XAxis),
                vector.DotProduct(YAxis),
                vector.DotProduct(ZAxis));
        }

        /// <summary>
        /// Converts a vector given in this system's local coordinates into world coordinates.
        /// </summary>
        public GeoVector3 ToGlobal(GeoVector3 vector)
        {
            return XAxis.Multiply(vector.X)
                .Add(YAxis.Multiply(vector.Y))
                .Add(ZAxis.Multiply(vector.Z));
        }

        /// <summary>
        /// Gets the transformation that takes local coordinates to world coordinates.
        /// </summary>
        public GeoTransform3 ToTransform() => GeoTransform3.FromCoordinateSystem(this);

        #region Equality

        /// <summary>
        /// Determines whether another coordinate system has exactly the same origin and axes.
        /// </summary>
        public bool Equals(GeoCoordinateSystem3 other)
        {
            return Origin.Equals(other.Origin) &&
                   XAxis.Equals(other.XAxis) &&
                   YAxis.Equals(other.YAxis) &&
                   ZAxis.Equals(other.ZAxis);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current coordinate system.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoCoordinateSystem3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Origin.GetHashCode();
                hashCode = (hashCode * 397) ^ XAxis.GetHashCode();
                hashCode = (hashCode * 397) ^ YAxis.GetHashCode();
                hashCode = (hashCode * 397) ^ ZAxis.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compares whether this coordinate system equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCoordinateSystem3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this coordinate system equals another within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCoordinateSystem3 other, Tolerance tolerance)
        {
            return Origin.IsEqualTo(other.Origin, tolerance) &&
                   XAxis.IsEqualTo(other.XAxis, tolerance) &&
                   YAxis.IsEqualTo(other.YAxis, tolerance) &&
                   ZAxis.IsEqualTo(other.ZAxis, tolerance);
        }

        /// <summary>
        /// Checks if two coordinate systems have exactly the same origin and axes.
        /// </summary>
        public static bool operator ==(GeoCoordinateSystem3 left, GeoCoordinateSystem3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two coordinate systems differ in origin or any axis.
        /// </summary>
        public static bool operator !=(GeoCoordinateSystem3 left, GeoCoordinateSystem3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current coordinate system.
        /// </summary>
        public override string ToString() => $"LCS(Origin: {Origin}, X: {XAxis}, Y: {YAxis}, Z: {ZAxis})";
    }
}
