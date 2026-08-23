using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents an oriented bounding box (OBB): a cuboid that carries its own orientation rather than
    /// lining up with the world axes.
    /// <para>
    /// This is a shape, not a bound. A beam running at an angle is described tightly by an oriented box
    /// and only loosely by an axis-aligned one, which is what <see cref="GeoAabb3"/> is for. The
    /// price is that testing two oriented boxes against each other needs the separating axis theorem
    /// rather than six comparisons, so the usual pattern is to reject with bounding boxes first and reach
    /// for this only on what survives.
    /// </para>
    /// </summary>
    public sealed class GeoObb3 : IEquatable<GeoObb3>
    {
        /// <summary>
        /// Gets the local coordinate system of the box: its centre and its three orthonormal axes.
        /// </summary>
        public GeoCoordinateSystem3 CoordinateSystem { get; }

        /// <summary>
        /// Gets the half-size of the box along its local X axis.
        /// </summary>
        public double ExtentX { get; }

        /// <summary>
        /// Gets the half-size of the box along its local Y axis.
        /// </summary>
        public double ExtentY { get; }

        /// <summary>
        /// Gets the half-size of the box along its local Z axis.
        /// </summary>
        public double ExtentZ { get; }

        /// <summary>
        /// Gets the centre point of the box.
        /// </summary>
        public GeoPoint3 Center => CoordinateSystem.Origin;

        /// <summary>
        /// Gets the local X axis, a unit vector.
        /// </summary>
        public GeoVector3 AxisX => CoordinateSystem.XAxis;

        /// <summary>
        /// Gets the local Y axis, a unit vector.
        /// </summary>
        public GeoVector3 AxisY => CoordinateSystem.YAxis;

        /// <summary>
        /// Gets the local Z axis, a unit vector.
        /// </summary>
        public GeoVector3 AxisZ => CoordinateSystem.ZAxis;

        /// <summary>
        /// Gets the full size of the box along its local X axis.
        /// </summary>
        public double SizeX => ExtentX * 2.0;

        /// <summary>
        /// Gets the full size of the box along its local Y axis.
        /// </summary>
        public double SizeY => ExtentY * 2.0;

        /// <summary>
        /// Gets the full size of the box along its local Z axis.
        /// </summary>
        public double SizeZ => ExtentZ * 2.0;

        /// <summary>
        /// Gets the volume of the box.
        /// </summary>
        public double Volume => SizeX * SizeY * SizeZ;

        /// <summary>
        /// Gets the total area of the six faces.
        /// </summary>
        public double SurfaceArea => 2.0 * (SizeX * SizeY + SizeY * SizeZ + SizeZ * SizeX);

        /// <summary>
        /// Initializes a box aligned with the world axes.
        /// </summary>
        /// <param name="center">Centre point of the box.</param>
        /// <param name="sizeX">Full size along the X axis.</param>
        /// <param name="sizeY">Full size along the Y axis.</param>
        /// <param name="sizeZ">Full size along the Z axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any size is negative.</exception>
        public GeoObb3(GeoPoint3 center, double sizeX, double sizeY, double sizeZ)
            : this(new GeoCoordinateSystem3(center, GeoVector3.XAxis, GeoVector3.YAxis), sizeX, sizeY, sizeZ)
        {
        }

        /// <summary>
        /// Initializes a box with its own orientation.
        /// </summary>
        /// <param name="center">Centre point of the box.</param>
        /// <param name="sizeX">Full size along the local X axis.</param>
        /// <param name="sizeY">Full size along the local Y axis.</param>
        /// <param name="sizeZ">Full size along the local Z axis.</param>
        /// <param name="axisX">The direction the local X axis should follow.</param>
        /// <param name="axisY">A direction on the positive Y side of the local XY plane.</param>
        /// <exception cref="ArgumentException">Thrown when the two directions are degenerate or parallel.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any size is negative.</exception>
        /// <remarks>
        /// The axes are made orthonormal on the way in, so a Y direction that is not quite square to X is
        /// corrected rather than producing a skewed box. See <see cref="GeoCoordinateSystem3"/> for what
        /// that costs and why it is done.
        /// </remarks>
        public GeoObb3(GeoPoint3 center, double sizeX, double sizeY, double sizeZ, GeoVector3 axisX, GeoVector3 axisY)
            : this(new GeoCoordinateSystem3(center, axisX, axisY), sizeX, sizeY, sizeZ)
        {
        }

        /// <summary>
        /// Initializes a box from a local coordinate system and its sizes.
        /// </summary>
        /// <param name="coordinateSystem">The centre and orientation of the box.</param>
        /// <param name="sizeX">Full size along the local X axis.</param>
        /// <param name="sizeY">Full size along the local Y axis.</param>
        /// <param name="sizeZ">Full size along the local Z axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any size is negative.</exception>
        public GeoObb3(GeoCoordinateSystem3 coordinateSystem, double sizeX, double sizeY, double sizeZ)
        {
            if (sizeX < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeX), "A box cannot have a negative size.");
            }

            if (sizeY < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeY), "A box cannot have a negative size.");
            }

            if (sizeZ < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeZ), "A box cannot have a negative size.");
            }

            CoordinateSystem = coordinateSystem;
            ExtentX = sizeX * 0.5;
            ExtentY = sizeY * 0.5;
            ExtentZ = sizeZ * 0.5;
        }

        /// <summary>
        /// Creates a copy of this box.
        /// </summary>
        public GeoObb3 Clone() => new GeoObb3(CoordinateSystem, SizeX, SizeY, SizeZ);

        /// <summary>
        /// Gets the half-size along one of the local axes, counted from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0, 1 or 2.</exception>
        public double GetExtentAt(int index)
        {
            switch (index)
            {
                case 0: return ExtentX;
                case 1: return ExtentY;
                case 2: return ExtentZ;
                default: throw new ArgumentOutOfRangeException(nameof(index), "A box has three axes.");
            }
        }

        /// <summary>
        /// Gets one of the local axes, counted from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0, 1 or 2.</exception>
        public GeoVector3 GetAxisAt(int index)
        {
            switch (index)
            {
                case 0: return AxisX;
                case 1: return AxisY;
                case 2: return AxisZ;
                default: throw new ArgumentOutOfRangeException(nameof(index), "A box has three axes.");
            }
        }

        /// <summary>
        /// Gets the eight corners of the box.
        /// </summary>
        /// <remarks>
        /// The order follows the local axes: the first four run around the bottom face counter-clockwise
        /// seen from +Z, and the last four are the same four lifted to the top. That matches the order
        /// <see cref="GeoAabb3.GetCorners"/> uses, so the two can be compared position by position.
        /// </remarks>
        public GeoPoint3[] GetCorners()
        {
            GeoVector3 dx = AxisX.Multiply(ExtentX);
            GeoVector3 dy = AxisY.Multiply(ExtentY);
            GeoVector3 dz = AxisZ.Multiply(ExtentZ);

            return new[]
            {
                Center.Subtract(dx).Subtract(dy).Subtract(dz),
                Center.Add(dx).Subtract(dy).Subtract(dz),
                Center.Add(dx).Add(dy).Subtract(dz),
                Center.Subtract(dx).Add(dy).Subtract(dz),
                Center.Subtract(dx).Subtract(dy).Add(dz),
                Center.Add(dx).Subtract(dy).Add(dz),
                Center.Add(dx).Add(dy).Add(dz),
                Center.Subtract(dx).Add(dy).Add(dz)
            };
        }

        /// <summary>
        /// Gets the six faces of the box as polygons, each wound so its normal points outwards.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is flat on any axis.</exception>
        public GeoPolygon3[] GetFaces()
        {
            if (IsDegenerate())
            {
                throw new InvalidOperationException("A box with a zero size on some axis has no faces to build.");
            }

            GeoPoint3[] c = GetCorners();

            return new[]
            {
                new GeoPolygon3(c[0], c[3], c[2], c[1]), // bottom, normal along -Z
                new GeoPolygon3(c[4], c[5], c[6], c[7]), // top, normal along +Z
                new GeoPolygon3(c[0], c[1], c[5], c[4]), // front, normal along -Y
                new GeoPolygon3(c[2], c[3], c[7], c[6]), // back, normal along +Y
                new GeoPolygon3(c[1], c[2], c[6], c[5]), // right, normal along +X
                new GeoPolygon3(c[3], c[0], c[4], c[7])  // left, normal along -X
            };
        }

        /// <summary>
        /// Gets this box as a solid bounded by its six faces.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is flat on any axis.</exception>
        public GeoSolid3 ToSolid()
        {
            GeoPolygon3[] faces = GetFaces();
            GeoFace3[] solidFaces = new GeoFace3[faces.Length];

            for (int i = 0; i < faces.Length; i++)
            {
                solidFaces[i] = new GeoFace3(faces[i]);
            }

            return new GeoSolid3(solidFaces);
        }

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this oriented box.
        /// </summary>
        public GeoAabb3 GetAabb() => GeoAabb3.FromPoints(GetCorners());

        /// <summary>
        /// Applies a transformation to the box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the transformation collapses the box orientation, which a projection does.
        /// </exception>
        /// <remarks>
        /// A box stays a box only under a transformation that keeps right angles right. A shear turns it
        /// into a parallelepiped, which this type cannot represent, and what comes back then is the box
        /// spanned by the transformed axes rather than the true sheared shape.
        /// </remarks>
        public GeoObb3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoVector3 newX = transform.Transform(AxisX.Multiply(ExtentX));
            GeoVector3 newY = transform.Transform(AxisY.Multiply(ExtentY));
            GeoVector3 newZ = transform.Transform(AxisZ.Multiply(ExtentZ));

            GeoCoordinateSystem3 system = new GeoCoordinateSystem3(transform.Transform(Center), newX, newY);

            return new GeoObb3(system, newX.Length * 2.0, newY.Length * 2.0, newZ.Length * 2.0);
        }

        #region Queries

        /// <summary>
        /// Checks whether the box has zero size on any axis, using the default tolerance.
        /// </summary>
        public bool IsDegenerate() => IsDegenerate(Tolerance.Global);

        /// <summary>
        /// Checks whether the box has zero size on any axis, within a tolerance.
        /// </summary>
        public bool IsDegenerate(Tolerance tolerance)
        {
            return ExtentX <= tolerance.EqualPoint || ExtentY <= tolerance.EqualPoint || ExtentZ <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Locates a point relative to this box, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this box, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Containment3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this box holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Containment3.Contains(this, point);

        /// <summary>
        /// Checks whether this box holds a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Containment3.Contains(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this box to a point. A point inside the box is at
        /// distance zero.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the point of this box closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToObb(this, point);

        /// <summary>
        /// Checks whether this box overlaps another one, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this box overlaps another one, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another box has exactly the same placement and sizes.
        /// </summary>
        public bool Equals(GeoObb3 other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return CoordinateSystem.Equals(other.CoordinateSystem) &&
                   ExtentX.Equals(other.ExtentX) &&
                   ExtentY.Equals(other.ExtentY) &&
                   ExtentZ.Equals(other.ExtentZ);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current box.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoObb3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = CoordinateSystem.GetHashCode();
                hashCode = (hashCode * 397) ^ ExtentX.GetHashCode();
                hashCode = (hashCode * 397) ^ ExtentY.GetHashCode();
                hashCode = (hashCode * 397) ^ ExtentZ.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compares whether this box equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoObb3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this box equals another within a tolerance.
        /// </summary>
        /// <remarks>
        /// The comparison is on the stored placement, so two boxes describing the same cuboid through
        /// differently named axes — the same shape rotated a quarter turn about Z with X and Y sizes
        /// swapped — are not equal here.
        /// </remarks>
        public bool IsEqualTo(GeoObb3 other, Tolerance tolerance)
        {
            if (other is null)
            {
                return false;
            }

            return CoordinateSystem.IsEqualTo(other.CoordinateSystem, tolerance) &&
                   Math.Abs(ExtentX - other.ExtentX) <= tolerance.EqualPoint &&
                   Math.Abs(ExtentY - other.ExtentY) <= tolerance.EqualPoint &&
                   Math.Abs(ExtentZ - other.ExtentZ) <= tolerance.EqualPoint;
        }

        #endregion

        /// <summary>
        /// Returns a string that represents the current box.
        /// </summary>
        public override string ToString() => $"Box3(Center: {Center}, Size: ({SizeX:0.###}, {SizeY:0.###}, {SizeZ:0.###}))";
    }
}
