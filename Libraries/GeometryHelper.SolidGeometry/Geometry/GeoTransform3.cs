using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D transformation as a 4x4 homogeneous matrix, covering translation, rotation,
    /// scaling and mirroring.
    /// <para>
    /// The matrix is stored row-major and applied on the left, so <c>a.Multiply(b)</c> means "apply b,
    /// then a", the usual convention for column vectors. Instances are immutable: every operation returns
    /// a new transformation rather than changing this one.
    /// </para>
    /// </summary>
    public sealed class GeoTransform3 : IEquatable<GeoTransform3>
    {
        private readonly double[,] _m;

        /// <summary>
        /// Initializes an identity transformation.
        /// </summary>
        public GeoTransform3()
        {
            _m = new double[4, 4];
            _m[0, 0] = 1.0;
            _m[1, 1] = 1.0;
            _m[2, 2] = 1.0;
            _m[3, 3] = 1.0;
        }

        /// <summary>
        /// Initializes a transformation from a 4x4 matrix.
        /// </summary>
        /// <param name="matrix">The matrix, row-major, copied on construction.</param>
        /// <exception cref="ArgumentException">Thrown when the array is not 4x4.</exception>
        public GeoTransform3(double[,] matrix)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            if (matrix.GetLength(0) != 4 || matrix.GetLength(1) != 4)
            {
                throw new ArgumentException("A 3D transformation needs a 4x4 matrix.", nameof(matrix));
            }

            _m = new double[4, 4];
            Array.Copy(matrix, _m, matrix.Length);
        }

        /// <summary>
        /// Gets the identity transformation, which leaves everything where it is.
        /// </summary>
        public static GeoTransform3 Identity => new GeoTransform3();

        /// <summary>
        /// Gets the matrix entry at a row and column, counted from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is outside 0 to 3.</exception>
        public double this[int row, int column]
        {
            get
            {
                if (row < 0 || row > 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(row));
                }

                if (column < 0 || column > 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(column));
                }

                return _m[row, column];
            }
        }

        /// <summary>
        /// Creates a copy of this transformation.
        /// </summary>
        public GeoTransform3 Clone() => new GeoTransform3(_m);

        #region Factories

        /// <summary>
        /// Creates a translation.
        /// </summary>
        /// <param name="vector">How far to move, and in which direction.</param>
        public static GeoTransform3 Translation(GeoVector3 vector)
        {
            GeoTransform3 t = new GeoTransform3();
            t._m[0, 3] = vector.X;
            t._m[1, 3] = vector.Y;
            t._m[2, 3] = vector.Z;
            return t;
        }

        /// <summary>
        /// Creates a rotation around the world X axis.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise seen from +X towards the origin.</param>
        public static GeoTransform3 RotationX(double angleRad)
        {
            GeoTransform3 t = new GeoTransform3();
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            t._m[1, 1] = cos; t._m[1, 2] = -sin;
            t._m[2, 1] = sin; t._m[2, 2] = cos;
            return t;
        }

        /// <summary>
        /// Creates a rotation around the world Y axis.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise seen from +Y towards the origin.</param>
        public static GeoTransform3 RotationY(double angleRad)
        {
            GeoTransform3 t = new GeoTransform3();
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            t._m[0, 0] = cos; t._m[0, 2] = sin;
            t._m[2, 0] = -sin; t._m[2, 2] = cos;
            return t;
        }

        /// <summary>
        /// Creates a rotation around the world Z axis.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise seen from +Z towards the origin.</param>
        public static GeoTransform3 RotationZ(double angleRad)
        {
            GeoTransform3 t = new GeoTransform3();
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            t._m[0, 0] = cos; t._m[0, 1] = -sin;
            t._m[1, 0] = sin; t._m[1, 1] = cos;
            return t;
        }

        /// <summary>
        /// Creates a rotation around an arbitrary axis through the world origin.
        /// </summary>
        /// <param name="axis">The rotation axis; it need not be normalized.</param>
        /// <param name="angleRad">The angle in radians, counter-clockwise seen from the axis tip.</param>
        /// <exception cref="ArgumentException">Thrown when the axis has zero length.</exception>
        public static GeoTransform3 RotationAxis(GeoVector3 axis, double angleRad)
        {
            if (!axis.TryGetNormal(out GeoVector3 k))
            {
                throw new ArgumentException("A rotation needs an axis of non-zero length.", nameof(axis));
            }

            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            double t1 = 1.0 - cos;

            GeoTransform3 t = new GeoTransform3();

            t._m[0, 0] = cos + k.X * k.X * t1;
            t._m[0, 1] = k.X * k.Y * t1 - k.Z * sin;
            t._m[0, 2] = k.X * k.Z * t1 + k.Y * sin;

            t._m[1, 0] = k.Y * k.X * t1 + k.Z * sin;
            t._m[1, 1] = cos + k.Y * k.Y * t1;
            t._m[1, 2] = k.Y * k.Z * t1 - k.X * sin;

            t._m[2, 0] = k.Z * k.X * t1 - k.Y * sin;
            t._m[2, 1] = k.Z * k.Y * t1 + k.X * sin;
            t._m[2, 2] = cos + k.Z * k.Z * t1;

            return t;
        }

        /// <summary>
        /// Creates a rotation around an arbitrary axis line, so the rotation is not restricted to axes
        /// passing through the world origin.
        /// </summary>
        /// <param name="axisOrigin">A point on the rotation axis.</param>
        /// <param name="axisDirection">The direction of the axis; it need not be normalized.</param>
        /// <param name="angleRad">The angle in radians, counter-clockwise seen from the axis tip.</param>
        public static GeoTransform3 RotationAxis(GeoPoint3 axisOrigin, GeoVector3 axisDirection, double angleRad)
        {
            GeoVector3 offset = axisOrigin.ToVector();

            return Translation(offset)
                .Multiply(RotationAxis(axisDirection, angleRad))
                .Multiply(Translation(offset.Negate()));
        }

        /// <summary>
        /// Creates a uniform scaling about the world origin.
        /// </summary>
        public static GeoTransform3 Scaling(double factor) => Scaling(factor, factor, factor);

        /// <summary>
        /// Creates a scaling about the world origin with a separate factor per axis.
        /// </summary>
        public static GeoTransform3 Scaling(double factorX, double factorY, double factorZ)
        {
            GeoTransform3 t = new GeoTransform3();
            t._m[0, 0] = factorX;
            t._m[1, 1] = factorY;
            t._m[2, 2] = factorZ;
            return t;
        }

        /// <summary>
        /// Creates a uniform scaling about a given centre.
        /// </summary>
        public static GeoTransform3 Scaling(GeoPoint3 center, double factor)
        {
            GeoVector3 offset = center.ToVector();

            return Translation(offset)
                .Multiply(Scaling(factor))
                .Multiply(Translation(offset.Negate()));
        }

        /// <summary>
        /// Creates a mirroring across a plane.
        /// </summary>
        /// <param name="plane">The mirror plane.</param>
        /// <remarks>
        /// Mirroring reverses handedness, so its determinant is negative and shapes come back with their
        /// winding flipped: a polygon normal points the other way afterwards.
        /// </remarks>
        public static GeoTransform3 Mirror(GeoPlane3 plane)
        {
            GeoVector3 n = plane.Normal;
            double d = plane.DistanceFromWorldOrigin;

            GeoTransform3 t = new GeoTransform3();

            t._m[0, 0] = 1.0 - 2.0 * n.X * n.X;
            t._m[0, 1] = -2.0 * n.X * n.Y;
            t._m[0, 2] = -2.0 * n.X * n.Z;
            t._m[0, 3] = 2.0 * d * n.X;

            t._m[1, 0] = -2.0 * n.Y * n.X;
            t._m[1, 1] = 1.0 - 2.0 * n.Y * n.Y;
            t._m[1, 2] = -2.0 * n.Y * n.Z;
            t._m[1, 3] = 2.0 * d * n.Y;

            t._m[2, 0] = -2.0 * n.Z * n.X;
            t._m[2, 1] = -2.0 * n.Z * n.Y;
            t._m[2, 2] = 1.0 - 2.0 * n.Z * n.Z;
            t._m[2, 3] = 2.0 * d * n.Z;

            return t;
        }

        /// <summary>
        /// Creates the transformation that takes local coordinates of a coordinate system into world
        /// coordinates.
        /// </summary>
        public static GeoTransform3 FromCoordinateSystem(GeoCoordinateSystem3 system)
        {
            GeoTransform3 t = new GeoTransform3();

            t._m[0, 0] = system.XAxis.X; t._m[0, 1] = system.YAxis.X; t._m[0, 2] = system.ZAxis.X; t._m[0, 3] = system.Origin.X;
            t._m[1, 0] = system.XAxis.Y; t._m[1, 1] = system.YAxis.Y; t._m[1, 2] = system.ZAxis.Y; t._m[1, 3] = system.Origin.Y;
            t._m[2, 0] = system.XAxis.Z; t._m[2, 1] = system.YAxis.Z; t._m[2, 2] = system.ZAxis.Z; t._m[2, 3] = system.Origin.Z;

            return t;
        }

        #endregion

        #region Composition

        /// <summary>
        /// Combines this transformation with another one.
        /// </summary>
        /// <param name="other">The transformation applied first.</param>
        /// <returns>The transformation that applies <paramref name="other"/> and then this one.</returns>
        public GeoTransform3 Multiply(GeoTransform3 other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            GeoTransform3 result = new GeoTransform3();

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    double sum = 0.0;
                    for (int i = 0; i < 4; i++)
                    {
                        sum += _m[r, i] * other._m[i, c];
                    }

                    result._m[r, c] = sum;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the determinant of the matrix.
        /// </summary>
        /// <remarks>
        /// The determinant is the factor by which the transformation multiplies volume. It is negative
        /// for a transformation that reverses handedness, and zero for one that flattens space, which is
        /// exactly the case <see cref="TryGetInverse(out GeoTransform3)"/> cannot undo. That method judges
        /// this value against the size of the transformation rather than against zero, so a transformation
        /// it refuses need not have a determinant of exactly zero.
        /// </remarks>
        public double GetDeterminant()
        {
            BuildCofactors(out double[,] cofactors);

            double determinant = 0.0;
            for (int c = 0; c < 4; c++)
            {
                determinant += _m[0, c] * cofactors[0, c];
            }

            return determinant;
        }

        /// <summary>
        /// Gets the transformation that undoes this one, using the default tolerance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the transformation is not invertible.</exception>
        public GeoTransform3 Inverse() => Inverse(Tolerance.Global);

        /// <summary>
        /// Gets the transformation that undoes this one, within a tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance deciding when the transformation has collapsed.</param>
        /// <exception cref="InvalidOperationException">Thrown when the transformation is not invertible.</exception>
        public GeoTransform3 Inverse(Tolerance tolerance)
        {
            if (!TryGetInverse(out GeoTransform3 inverse, tolerance))
            {
                throw new InvalidOperationException("This transformation collapses space and cannot be inverted.");
            }

            return inverse;
        }

        /// <summary>
        /// Gets the product of the lengths of the three transformed axes, which is the size a determinant
        /// of this transformation is measured against.
        /// </summary>
        /// <remarks>
        /// For a transformation that keeps its axes square this is exactly the determinant, so the test it
        /// feeds asks how far the three axes have been flattened towards a common plane rather than how
        /// large they are.
        /// </remarks>
        private double GetAxisScale()
        {
            double scale = 1.0;

            // The columns are where the basis vectors land, as FromCoordinateSystem lays them out. Reading
            // rows here would measure something else entirely and would not answer the question.
            for (int c = 0; c < 3; c++)
            {
                scale *= Math.Sqrt(_m[0, c] * _m[0, c] + _m[1, c] * _m[1, c] + _m[2, c] * _m[2, c]);
            }

            return scale;
        }

        /// <summary>
        /// Tries to get the transformation that undoes this one, without throwing, using the default
        /// tolerance.
        /// </summary>
        /// <param name="inverse">The inverse when the method returns true; otherwise the identity.</param>
        /// <returns>false when the transformation collapses space and cannot be undone.</returns>
        public bool TryGetInverse(out GeoTransform3 inverse) => TryGetInverse(out inverse, Tolerance.Global);

        /// <summary>
        /// Tries to get the transformation that undoes this one, without throwing, within a tolerance.
        /// </summary>
        /// <param name="inverse">The inverse when the method returns true; otherwise the identity.</param>
        /// <param name="tolerance">The tolerance deciding when the transformation has collapsed.</param>
        /// <returns>false when the transformation collapses space and cannot be undone.</returns>
        /// <remarks>
        /// The determinant is judged against the size of the transformation rather than against zero. A
        /// determinant of exactly zero is the only case a bare <c>==</c> catches, and it is not the case
        /// that hurts: three axes that are very nearly coplanar give a determinant around 1e-16 while each
        /// axis is still of unit length, and dividing the cofactors by that yields a matrix of enormous
        /// numbers reported as a valid inverse. Scaling the threshold by the product of the axis lengths
        /// is what makes the test mean "collapsed" rather than "small", so a model in millimetres and the
        /// same model in metres are judged alike.
        /// </remarks>
        public bool TryGetInverse(out GeoTransform3 inverse, Tolerance tolerance)
        {
            BuildCofactors(out double[,] cofactors);

            double determinant = 0.0;
            for (int c = 0; c < 4; c++)
            {
                determinant += _m[0, c] * cofactors[0, c];
            }

            if (Math.Abs(determinant) <= tolerance.EqualVector * GetAxisScale())
            {
                inverse = Identity;
                return false;
            }

            inverse = new GeoTransform3();

            // The inverse is the transposed cofactor matrix over the determinant, so the indices swap here.
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    inverse._m[r, c] = cofactors[c, r] / determinant;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds the matrix of signed cofactors, which both the determinant and the inverse are read from.
        /// </summary>
        private void BuildCofactors(out double[,] cofactors)
        {
            cofactors = new double[4, 4];

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    cofactors[r, c] = ((r + c) % 2 == 0 ? 1.0 : -1.0) * Minor(r, c);
                }
            }
        }

        /// <summary>
        /// Gets the determinant of the 3x3 matrix left after removing one row and one column.
        /// </summary>
        private double Minor(int skipRow, int skipColumn)
        {
            double[,] sub = new double[3, 3];
            int subRow = 0;

            for (int r = 0; r < 4; r++)
            {
                if (r == skipRow)
                {
                    continue;
                }

                int subColumn = 0;
                for (int c = 0; c < 4; c++)
                {
                    if (c == skipColumn)
                    {
                        continue;
                    }

                    sub[subRow, subColumn] = _m[r, c];
                    subColumn++;
                }

                subRow++;
            }

            return sub[0, 0] * (sub[1, 1] * sub[2, 2] - sub[1, 2] * sub[2, 1])
                 - sub[0, 1] * (sub[1, 0] * sub[2, 2] - sub[1, 2] * sub[2, 0])
                 + sub[0, 2] * (sub[1, 0] * sub[2, 1] - sub[1, 1] * sub[2, 0]);
        }

        #endregion

        #region Application

        /// <summary>
        /// Transforms a point.
        /// </summary>
        public GeoPoint3 Transform(GeoPoint3 point)
        {
            return new GeoPoint3(
                _m[0, 0] * point.X + _m[0, 1] * point.Y + _m[0, 2] * point.Z + _m[0, 3],
                _m[1, 0] * point.X + _m[1, 1] * point.Y + _m[1, 2] * point.Z + _m[1, 3],
                _m[2, 0] * point.X + _m[2, 1] * point.Y + _m[2, 2] * point.Z + _m[2, 3]);
        }

        /// <summary>
        /// Transforms a vector, ignoring the translation part.
        /// </summary>
        /// <remarks>
        /// A vector has a direction and a length but no position, so moving the whole of space must leave
        /// it unchanged. That is why the translation column takes no part here and why transforming a
        /// point and transforming the vector between two points give different answers.
        /// </remarks>
        public GeoVector3 Transform(GeoVector3 vector)
        {
            return new GeoVector3(
                _m[0, 0] * vector.X + _m[0, 1] * vector.Y + _m[0, 2] * vector.Z,
                _m[1, 0] * vector.X + _m[1, 1] * vector.Y + _m[1, 2] * vector.Z,
                _m[2, 0] * vector.X + _m[2, 1] * vector.Y + _m[2, 2] * vector.Z);
        }

        /// <summary>
        /// Transforms a line segment by transforming both of its endpoints.
        /// </summary>
        public GeoLine3 Transform(GeoLine3 line) => new GeoLine3(Transform(line.StartPoint), Transform(line.EndPoint));

        /// <summary>
        /// Transforms a ray.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation flattens the ray direction to nothing.
        /// </exception>
        public GeoRay3 Transform(GeoRay3 ray)
        {
            GeoVector3 direction = Transform(ray.Direction);

            if (direction.IsZeroLength())
            {
                throw new InvalidOperationException("This transformation collapses the ray direction.");
            }

            return new GeoRay3(Transform(ray.Origin), direction);
        }

        /// <summary>
        /// Transforms a triangle by transforming its three vertices.
        /// </summary>
        public GeoTriangle3 Transform(GeoTriangle3 triangle)
        {
            return new GeoTriangle3(Transform(triangle.A), Transform(triangle.B), Transform(triangle.C));
        }

        /// <summary>
        /// Transforms a plane.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the transformation is not invertible.</exception>
        /// <remarks>
        /// A plane normal is not transformed like a direction. Under a non-uniform scaling a direction in
        /// the plane and the normal are stretched differently, and applying the same matrix to both would
        /// leave them no longer perpendicular. The normal follows the inverse transpose instead, which is
        /// the matrix that preserves that right angle.
        /// </remarks>
        public GeoPlane3 Transform(GeoPlane3 plane)
        {
            if (!TryGetInverse(out GeoTransform3 inverse))
            {
                throw new InvalidOperationException("This transformation collapses space and cannot carry a plane.");
            }

            GeoVector3 n = plane.Normal;

            GeoVector3 transformedNormal = new GeoVector3(
                inverse._m[0, 0] * n.X + inverse._m[1, 0] * n.Y + inverse._m[2, 0] * n.Z,
                inverse._m[0, 1] * n.X + inverse._m[1, 1] * n.Y + inverse._m[2, 1] * n.Z,
                inverse._m[0, 2] * n.X + inverse._m[1, 2] * n.Y + inverse._m[2, 2] * n.Z);

            return new GeoPlane3(Transform(plane.Origin), transformedNormal);
        }

        /// <summary>
        /// Transforms a local coordinate system.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the transformation flattens the frame so that its axes no longer span space.
        /// </exception>
        public GeoCoordinateSystem3 Transform(GeoCoordinateSystem3 system)
        {
            return new GeoCoordinateSystem3(
                Transform(system.Origin),
                Transform(system.XAxis),
                Transform(system.YAxis));
        }

        /// <summary>
        /// Transforms an oriented box.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the transformation collapses the box orientation.</exception>
        /// <remarks>
        /// A box stays a box only under a transformation that keeps right angles right. A shear turns it
        /// into a parallelepiped, which this type cannot represent, and what comes back then is the box
        /// spanned by the transformed axes rather than the true sheared shape.
        /// </remarks>
        public GeoObb3 Transform(GeoObb3 box)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return box.TransformBy(this);
        }

        /// <summary>
        /// Transforms an axis-aligned box.
        /// </summary>
        /// <remarks>
        /// A box aligned with the world axes does not stay aligned once it is rotated, so what comes back
        /// is the smallest aligned box holding the transformed corners. That is larger than the true
        /// transformed shape whenever the transformation turns it, which is the price of insisting on
        /// alignment; transform a <see cref="GeoObb3"/> instead to keep the shape exactly.
        /// </remarks>
        public GeoAabb3 Transform(GeoAabb3 box)
        {
            if (box.IsEmpty)
            {
                return GeoAabb3.Empty;
            }

            GeoAabb3 moved = GeoAabb3.Empty;

            foreach (GeoPoint3 corner in box.GetCorners())
            {
                moved = moved.Union(Transform(corner));
            }

            return moved;
        }

        /// <summary>
        /// Transforms a circle.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation would turn the circle into an ellipse.
        /// </exception>
        /// <remarks>
        /// A circle survives a transformation only when every direction in its plane is stretched by the
        /// same amount. Under a non-uniform scaling it becomes an ellipse, which this library has no type
        /// for, so the attempt is refused rather than answered with a circle of some averaged radius.
        /// </remarks>
        public GeoCircle3 Transform(GeoCircle3 circle)
        {
            circle.GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            double alongU = Transform(uAxis).Length;
            double alongV = Transform(vAxis).Length;

            if (Math.Abs(alongU - alongV) > Tolerance.Global.EqualVector)
            {
                throw new InvalidOperationException("This transformation stretches the circle into an ellipse.");
            }

            GeoPlane3 movedPlane = Transform(circle.GetPlane());

            return new GeoCircle3(Transform(circle.Center), movedPlane.Normal, circle.Radius * alongU);
        }

        /// <summary>
        /// Transforms a sequence of points.
        /// </summary>
        public IEnumerable<GeoPoint3> Transform(IEnumerable<GeoPoint3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return points.Select(Transform);
        }

        #endregion

        #region Equality and operators

        /// <summary>
        /// Multiplies two transformations, so the right one is applied first.
        /// </summary>
        public static GeoTransform3 operator *(GeoTransform3 left, GeoTransform3 right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            return left.Multiply(right);
        }

        /// <summary>
        /// Determines whether another transformation has exactly the same matrix entries.
        /// </summary>
        public bool Equals(GeoTransform3 other)
        {
            if (other is null)
            {
                return false;
            }

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    if (!_m[r, c].Equals(other._m[r, c]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current transformation.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoTransform3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        hash = hash * 31 + _m[r, c].GetHashCode();
                    }
                }

                return hash;
            }
        }

        /// <summary>
        /// Compares whether this transformation equals another within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoTransform3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this transformation equals another within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoTransform3 other, Tolerance tolerance)
        {
            if (other is null)
            {
                return false;
            }

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    if (Math.Abs(_m[r, c] - other._m[r, c]) > tolerance.EqualVector)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether this transformation leaves everything where it is, within a tolerance.
        /// </summary>
        public bool IsIdentity() => IsIdentity(Tolerance.Global);

        /// <summary>
        /// Checks whether this transformation leaves everything where it is, within a tolerance.
        /// </summary>
        public bool IsIdentity(Tolerance tolerance) => IsEqualTo(Identity, tolerance);

        #endregion

        /// <summary>
        /// Returns a string that represents the current transformation.
        /// </summary>
        public override string ToString()
        {
            return $"Transform3[{_m[0, 0]:0.###} {_m[0, 1]:0.###} {_m[0, 2]:0.###} {_m[0, 3]:0.###}; " +
                   $"{_m[1, 0]:0.###} {_m[1, 1]:0.###} {_m[1, 2]:0.###} {_m[1, 3]:0.###}; " +
                   $"{_m[2, 0]:0.###} {_m[2, 1]:0.###} {_m[2, 2]:0.###} {_m[2, 3]:0.###}; " +
                   $"{_m[3, 0]:0.###} {_m[3, 1]:0.###} {_m[3, 2]:0.###} {_m[3, 3]:0.###}]";
        }
    }
}
