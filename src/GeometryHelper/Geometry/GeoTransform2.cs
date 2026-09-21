using System;
using System.Collections.Generic;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D transformation as a 3x3 homogeneous matrix, covering translation, rotation, scaling
    /// and mirroring.
    /// <para>
    /// The matrix is stored row-major and applied on the left, so <c>a.Multiply(b)</c> means "apply b, then
    /// a", the usual convention for column vectors. Instances are immutable: every operation returns a new
    /// transformation rather than changing this one. It is the counterpart in the plane of
    /// <see cref="GeoTransform3"/>, and the two are built and read the same way.
    /// </para>
    /// </summary>
    public sealed class GeoTransform2 : IEquatable<GeoTransform2>
    {
        private readonly double[,] _m;

        /// <summary>
        /// Initializes an identity transformation.
        /// </summary>
        public GeoTransform2()
        {
            _m = new double[3, 3];
            _m[0, 0] = 1.0;
            _m[1, 1] = 1.0;
            _m[2, 2] = 1.0;
        }

        /// <summary>
        /// Initializes a transformation from a 3x3 matrix.
        /// </summary>
        /// <param name="matrix">The matrix, row-major, copied on construction.</param>
        /// <exception cref="ArgumentNullException">Thrown when the matrix is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the array is not 3x3.</exception>
        public GeoTransform2(double[,] matrix)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            if (matrix.GetLength(0) != 3 || matrix.GetLength(1) != 3)
            {
                throw new ArgumentException("A 2D transformation needs a 3x3 matrix.", nameof(matrix));
            }

            _m = new double[3, 3];
            Array.Copy(matrix, _m, matrix.Length);
        }

        /// <summary>
        /// Gets the identity transformation, which leaves everything where it is.
        /// </summary>
        public static GeoTransform2 Identity => new GeoTransform2();

        /// <summary>
        /// Gets the matrix entry at a row and column, counted from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is outside 0 to 2.</exception>
        public double this[int row, int column]
        {
            get
            {
                if (row < 0 || row > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(row));
                }

                if (column < 0 || column > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(column));
                }

                return _m[row, column];
            }
        }

        /// <summary>
        /// Creates a copy of this transformation.
        /// </summary>
        public GeoTransform2 Clone() => new GeoTransform2(_m);

        #region Factories

        /// <summary>
        /// Creates a translation.
        /// </summary>
        /// <param name="vector">How far to move, and in which direction.</param>
        public static GeoTransform2 Translation(GeoVector2 vector)
        {
            GeoTransform2 t = new GeoTransform2();
            t._m[0, 2] = vector.X;
            t._m[1, 2] = vector.Y;
            return t;
        }

        /// <summary>
        /// Creates a rotation about the origin.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise.</param>
        public static GeoTransform2 Rotation(double angleRad)
        {
            GeoTransform2 t = new GeoTransform2();
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            t._m[0, 0] = cos; t._m[0, 1] = -sin;
            t._m[1, 0] = sin; t._m[1, 1] = cos;
            return t;
        }

        /// <summary>
        /// Creates a rotation about a point.
        /// </summary>
        /// <param name="center">The point that stays where it is.</param>
        /// <param name="angleRad">The angle in radians, counter-clockwise.</param>
        public static GeoTransform2 Rotation(GeoPoint2 center, double angleRad)
        {
            GeoVector2 toCenter = new GeoVector2(center.X, center.Y);

            return Translation(toCenter)
                .Multiply(Rotation(angleRad))
                .Multiply(Translation(toCenter.Multiply(-1.0)));
        }

        /// <summary>
        /// Creates a uniform scaling about the origin.
        /// </summary>
        /// <param name="factor">How much to stretch by; a negative factor also turns the shape round.</param>
        public static GeoTransform2 Scaling(double factor) => Scaling(factor, factor);

        /// <summary>
        /// Creates a scaling about the origin with a factor per axis.
        /// </summary>
        /// <param name="factorX">How much to stretch along X.</param>
        /// <param name="factorY">How much to stretch along Y.</param>
        public static GeoTransform2 Scaling(double factorX, double factorY)
        {
            GeoTransform2 t = new GeoTransform2();
            t._m[0, 0] = factorX;
            t._m[1, 1] = factorY;
            return t;
        }

        /// <summary>
        /// Creates a uniform scaling about a point.
        /// </summary>
        /// <param name="center">The point that stays where it is.</param>
        /// <param name="factor">How much to stretch by.</param>
        public static GeoTransform2 Scaling(GeoPoint2 center, double factor)
        {
            GeoVector2 toCenter = new GeoVector2(center.X, center.Y);

            return Translation(toCenter)
                .Multiply(Scaling(factor))
                .Multiply(Translation(toCenter.Multiply(-1.0)));
        }

        /// <summary>
        /// Creates a reflection across the line carrying a segment.
        /// </summary>
        /// <param name="line">The segment, read as the infinite line carrying it.</param>
        /// <returns>The transformation that mirrors everything across that line.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <remarks>
        /// A reflection turns a shape round: a polygon that ran counter-clockwise comes back clockwise, and
        /// its signed area changes sign while its area does not.
        /// </remarks>
        public static GeoTransform2 Mirror(GeoLine2 line)
        {
            GeoVector2 along = line.StartPoint.GetVectorTo(line.EndPoint);

            if (!along.TryGetNormal(out GeoVector2 unit))
            {
                throw new InvalidOperationException("A segment with no length carries no line to mirror across.");
            }

            // Reflection across the line through the origin along (ux, uy), then carried to the line itself.
            GeoTransform2 t = new GeoTransform2();
            double xx = unit.X * unit.X;
            double yy = unit.Y * unit.Y;
            double xy = 2.0 * unit.X * unit.Y;
            t._m[0, 0] = xx - yy; t._m[0, 1] = xy;
            t._m[1, 0] = xy; t._m[1, 1] = yy - xx;

            GeoVector2 toStart = new GeoVector2(line.StartPoint.X, line.StartPoint.Y);

            return Translation(toStart)
                .Multiply(t)
                .Multiply(Translation(toStart.Multiply(-1.0)));
        }

        /// <summary>
        /// Creates the transformation that takes the world frame to a local frame: the origin to
        /// <paramref name="origin"/> and the X axis along <paramref name="xAxis"/>.
        /// </summary>
        /// <param name="origin">Where the origin lands.</param>
        /// <param name="xAxis">Which way the X axis points; its length does not matter.</param>
        /// <exception cref="InvalidOperationException">Thrown when the axis has no length.</exception>
        /// <remarks>
        /// This is what to use to place a drawing: build its geometry about the origin, then move the whole
        /// of it into place with one transformation. Its inverse reads a placed drawing back into local
        /// coordinates.
        /// </remarks>
        public static GeoTransform2 FromFrame(GeoPoint2 origin, GeoVector2 xAxis)
        {
            if (!xAxis.TryGetNormal(out GeoVector2 unit))
            {
                throw new InvalidOperationException("A frame needs an X axis with a length.");
            }

            GeoTransform2 t = new GeoTransform2();
            t._m[0, 0] = unit.X; t._m[0, 1] = -unit.Y; t._m[0, 2] = origin.X;
            t._m[1, 0] = unit.Y; t._m[1, 1] = unit.X; t._m[1, 2] = origin.Y;
            return t;
        }

        /// <summary>
        /// Gets the transformation that places geometry built about the origin into a local coordinate
        /// system.
        /// </summary>
        /// <param name="system">The system to place it in.</param>
        /// <returns>The transformation; its inverse reads placed geometry back into that system.</returns>
        /// <remarks>
        /// The counterpart of <see cref="GeoTransform3.FromCoordinateSystem"/> in the plane. For reading
        /// points back and forth, <see cref="GeoCoordinateSystem2.ToLocal(GeoPoint2)"/> is cheaper than
        /// inverting what this gives.
        /// </remarks>
        public static GeoTransform2 FromCoordinateSystem(GeoCoordinateSystem2 system) => FromFrame(system.Origin, system.XAxis);

        #endregion

        #region Combining and inverting

        /// <summary>
        /// Multiplies this transformation by another: the result applies <paramref name="other"/> first,
        /// then this one.
        /// </summary>
        /// <param name="other">The transformation applied first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other transformation is null.</exception>
        public GeoTransform2 Multiply(GeoTransform2 other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            double[,] result = new double[3, 3];

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < 3; k++)
                    {
                        sum += _m[row, k] * other._m[k, column];
                    }
                    result[row, column] = sum;
                }
            }

            return new GeoTransform2(result);
        }

        /// <summary>
        /// Multiplies two transformations: the result applies <paramref name="right"/> first, then
        /// <paramref name="left"/>.
        /// </summary>
        public static GeoTransform2 operator *(GeoTransform2 left, GeoTransform2 right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));

            return left.Multiply(right);
        }

        /// <summary>
        /// Gets the determinant of the linear part: the factor the transformation multiplies areas by,
        /// negative when it also turns shapes round.
        /// </summary>
        public double GetDeterminant() => _m[0, 0] * _m[1, 1] - _m[0, 1] * _m[1, 0];

        /// <summary>
        /// Gets the product of the lengths the basis vectors land at, which is how large an area the
        /// transformation would cover if its axes were square to each other.
        /// </summary>
        /// <remarks>
        /// The columns are where the basis vectors land. Dividing the determinant by this gives the sine of
        /// the angle between them, which is what "nearly degenerate" means, and it is the same reading
        /// <see cref="GeoTransform3"/> takes.
        /// </remarks>
        private double GetAxisScale()
        {
            return Math.Sqrt(_m[0, 0] * _m[0, 0] + _m[1, 0] * _m[1, 0])
                 * Math.Sqrt(_m[0, 1] * _m[0, 1] + _m[1, 1] * _m[1, 1]);
        }

        /// <summary>
        /// Gets the transformation that undoes this one, using the default tolerance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the transformation cannot be undone.</exception>
        public GeoTransform2 Inverse() => Inverse(Tolerance.Global);

        /// <summary>
        /// Gets the transformation that undoes this one, within a tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance the determinant is judged against, relative to the size of the transformation.</param>
        /// <exception cref="InvalidOperationException">Thrown when the transformation cannot be undone.</exception>
        public GeoTransform2 Inverse(Tolerance tolerance)
        {
            if (!TryGetInverse(out GeoTransform2 inverse, tolerance))
            {
                throw new InvalidOperationException("This transformation flattens the plane, so nothing undoes it.");
            }

            return inverse;
        }

        /// <summary>
        /// Tries to get the transformation that undoes this one, using the default tolerance.
        /// </summary>
        /// <param name="inverse">The inverse, or the identity when the method returns false.</param>
        /// <returns>true if the transformation can be undone; otherwise, false.</returns>
        public bool TryGetInverse(out GeoTransform2 inverse) => TryGetInverse(out inverse, Tolerance.Global);

        /// <summary>
        /// Tries to get the transformation that undoes this one, within a tolerance.
        /// </summary>
        /// <param name="inverse">The inverse, or the identity when the method returns false.</param>
        /// <param name="tolerance">The tolerance the determinant is judged against, relative to the size of the transformation.</param>
        /// <returns>true if the transformation can be undone; otherwise, false.</returns>
        /// <remarks>
        /// The determinant is judged against the size of the transformation rather than against zero: two
        /// axes of unit length turned almost onto each other give a determinant near zero, and inverting
        /// that yields numbers so large they mean nothing. Scaling both axes by a thousandth gives a
        /// determinant just as small while being perfectly invertible, which is why the test is relative.
        /// </remarks>
        public bool TryGetInverse(out GeoTransform2 inverse, Tolerance tolerance)
        {
            double determinant = GetDeterminant();

            if (Math.Abs(determinant) <= tolerance.EqualVector * GetAxisScale())
            {
                inverse = Identity;
                return false;
            }

            double inverseDeterminant = 1.0 / determinant;
            double[,] result = new double[3, 3];

            result[0, 0] = _m[1, 1] * inverseDeterminant;
            result[0, 1] = -_m[0, 1] * inverseDeterminant;
            result[1, 0] = -_m[1, 0] * inverseDeterminant;
            result[1, 1] = _m[0, 0] * inverseDeterminant;

            // The translation moves by the inverse of the linear part applied to it, negated.
            result[0, 2] = -(result[0, 0] * _m[0, 2] + result[0, 1] * _m[1, 2]);
            result[1, 2] = -(result[1, 0] * _m[0, 2] + result[1, 1] * _m[1, 2]);
            result[2, 2] = 1.0;

            inverse = new GeoTransform2(result);
            return true;
        }

        #endregion

        #region Transforming shapes

        /// <summary>
        /// Transforms a point.
        /// </summary>
        public GeoPoint2 Transform(GeoPoint2 point)
        {
            return new GeoPoint2(
                _m[0, 0] * point.X + _m[0, 1] * point.Y + _m[0, 2],
                _m[1, 0] * point.X + _m[1, 1] * point.Y + _m[1, 2]);
        }

        /// <summary>
        /// Transforms a vector, which the translation does not touch.
        /// </summary>
        public GeoVector2 Transform(GeoVector2 vector)
        {
            return new GeoVector2(
                _m[0, 0] * vector.X + _m[0, 1] * vector.Y,
                _m[1, 0] * vector.X + _m[1, 1] * vector.Y);
        }

        /// <summary>
        /// Transforms a segment.
        /// </summary>
        public GeoLine2 Transform(GeoLine2 line) => new GeoLine2(Transform(line.StartPoint), Transform(line.EndPoint));

        /// <summary>
        /// Transforms a chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPolyline2 Transform(GeoPolyline2 polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return new GeoPolyline2(TransformAll(polyline.Vertices));
        }

        /// <summary>
        /// Transforms a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <remarks>
        /// A transformation that turns shapes round, a mirror or a negative scaling, reverses the winding:
        /// a polygon that ran counter-clockwise comes back clockwise.
        /// </remarks>
        public GeoPolygon2 Transform(GeoPolygon2 polygon)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return new GeoPolygon2(TransformAll(polygon.Vertices));
        }

        /// <summary>
        /// Transforms a face and its holes.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoFace2 Transform(GeoFace2 face)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var holes = new List<GeoPolygon2>(face.Holes.Count);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                holes.Add(Transform(hole));
            }

            return new GeoFace2(Transform(face.Boundary), holes);
        }

        /// <summary>
        /// Transforms a circle.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation would turn the circle into an ellipse.
        /// </exception>
        /// <remarks>
        /// A circle survives a transformation only when every direction is stretched by the same amount.
        /// Under a scaling that differs between the axes it becomes an ellipse, which this library has no
        /// type for, so the attempt is refused rather than answered with a circle of some averaged radius.
        /// </remarks>
        public GeoCircle2 Transform(GeoCircle2 circle)
        {
            double alongX = Transform(GeoVector2.XAxis).Length;
            double alongY = Transform(GeoVector2.YAxis).Length;

            if (Math.Abs(alongX - alongY) > Tolerance.Global.EqualVector)
            {
                throw new InvalidOperationException("This transformation stretches the circle into an ellipse.");
            }

            return new GeoCircle2(Transform(circle.Center), circle.Radius * alongX);
        }

        /// <summary>
        /// Transforms a rectangle.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation would turn the rectangle into a parallelogram.
        /// </exception>
        /// <remarks>
        /// A rectangle stays a rectangle under a move, a turn, a mirror and a uniform scaling. A scaling
        /// that differs between the axes, or a shear, leaves a parallelogram instead, which this library
        /// has no type for: transform <c>rectangle.ToPolygon()</c> when that is what you want.
        /// </remarks>
        public GeoRectangle2 Transform(GeoRectangle2 rectangle)
        {
            GeoVector2 alongX = Transform(GeoVector2.XAxis);
            GeoVector2 alongY = Transform(GeoVector2.YAxis);

            if (Math.Abs(alongX.Length - alongY.Length) > Tolerance.Global.EqualVector ||
                Math.Abs(alongX.DotProduct(alongY)) > Tolerance.Global.EqualVector)
            {
                throw new InvalidOperationException("This transformation turns the rectangle into a parallelogram.");
            }

            // The angle the rectangle now sits at is the angle its own X axis comes back at, and a mirror
            // reverses which way that turns.
            GeoVector2 edge = Transform(new GeoVector2(Math.Cos(rectangle.AngleRad), Math.Sin(rectangle.AngleRad)));

            return new GeoRectangle2(
                Transform(rectangle.Center),
                rectangle.Width * alongX.Length,
                rectangle.Height * alongY.Length,
                Math.Atan2(edge.Y, edge.X));
        }

        /// <summary>
        /// Transforms a sequence of points.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the sequence is null.</exception>
        public IEnumerable<GeoPoint2> Transform(IEnumerable<GeoPoint2> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            foreach (GeoPoint2 point in points)
            {
                yield return Transform(point);
            }
        }

        private List<GeoPoint2> TransformAll(IReadOnlyList<GeoPoint2> points)
        {
            var moved = new List<GeoPoint2>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                moved.Add(Transform(points[i]));
            }

            return moved;
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another transformation holds exactly the same matrix.
        /// </summary>
        public bool Equals(GeoTransform2 other)
        {
            if (other == null)
            {
                return false;
            }

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    if (!_m[row, column].Equals(other._m[row, column]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is an equal transformation.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoTransform2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this transformation.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        hash = hash * 397 ^ _m[row, column].GetHashCode();
                    }
                }
                return hash;
            }
        }

        /// <summary>
        /// Determines whether another transformation is the same within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoTransform2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another transformation is the same within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoTransform2 other, Tolerance tolerance)
        {
            if (other == null)
            {
                return false;
            }

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    if (Math.Abs(_m[row, column] - other._m[row, column]) > tolerance.EqualVector)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Describes the transformation.
        /// </summary>
        public override string ToString()
        {
            return $"GeoTransform2[[{_m[0, 0]:0.###}, {_m[0, 1]:0.###}, {_m[0, 2]:0.###}], " +
                   $"[{_m[1, 0]:0.###}, {_m[1, 1]:0.###}, {_m[1, 2]:0.###}]]";
        }

        #endregion
    }
}
