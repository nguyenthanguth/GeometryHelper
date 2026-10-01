using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a triangle in the plane, defined by three vertices.
    /// <para>
    /// A polygon or a face of the plane broken into triangles comes back as these, from
    /// <see cref="GeoFace2.TriangulateSurface()"/> and its kin, and a triangle answers the questions every other shape of
    /// the plane does. It is <see cref="GeoTriangle3"/> laid flat: three points always make a triangle, even one of no
    /// area, so nothing is checked at construction.
    /// </para>
    /// </summary>
    public readonly partial struct GeoTriangle2 : IEquatable<GeoTriangle2>
    {
        /// <summary>
        /// Gets the first vertex.
        /// </summary>
        public GeoPoint2 A { get; }

        /// <summary>
        /// Gets the second vertex.
        /// </summary>
        public GeoPoint2 B { get; }

        /// <summary>
        /// Gets the third vertex.
        /// </summary>
        public GeoPoint2 C { get; }

        /// <summary>
        /// Initializes a new triangle from three vertices.
        /// </summary>
        /// <param name="a">First vertex.</param>
        /// <param name="b">Second vertex.</param>
        /// <param name="c">Third vertex.</param>
        /// <remarks>
        /// The vertices are kept in the order given, so the triangle runs whichever way they do; <see cref="IsClockwise"/>
        /// says which. Degenerate input is accepted rather than rejected: three collinear or coincident points still
        /// make a triangle, whose <see cref="Area"/> is nought, and <see cref="IsDegenerate()"/> says so.
        /// </remarks>
        public GeoTriangle2(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c)
        {
            A = a;
            B = b;
            C = c;
        }

        /// <summary>
        /// Creates a copy of this triangle.
        /// </summary>
        /// <remarks>
        /// The triangle is a readonly struct, so plain assignment already copies it. This exists so that every geometry
        /// type offers the same way to ask for a copy.
        /// </remarks>
        public GeoTriangle2 Clone() => new GeoTriangle2(A, B, C);

        /// <summary>
        /// Gets the vertex at a given index, counted from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0, 1 or 2.</exception>
        public GeoPoint2 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return A;
                    case 1: return B;
                    case 2: return C;
                    default: throw new ArgumentOutOfRangeException(nameof(index), "A triangle has three vertices.");
                }
            }
        }

        /// <summary>
        /// Gets the edge at a given index: 0 is A to B, 1 is B to C, 2 is C to A.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0, 1 or 2.</exception>
        public GeoLine2 GetEdgeAt(int index)
        {
            switch (index)
            {
                case 0: return new GeoLine2(A, B);
                case 1: return new GeoLine2(B, C);
                case 2: return new GeoLine2(C, A);
                default: throw new ArgumentOutOfRangeException(nameof(index), "A triangle has three edges.");
            }
        }

        /// <summary>
        /// Gets the three edges, A to B, B to C and C to A.
        /// </summary>
        public GeoLine2[] GetEdges() => new[] { new GeoLine2(A, B), new GeoLine2(B, C), new GeoLine2(C, A) };

        /// <summary>
        /// Gets the three vertices, in order.
        /// </summary>
        public GeoPoint2[] GetVertices() => new[] { A, B, C };

        /// <summary>
        /// Gets twice the signed area: the cross product of A to B with A to C.
        /// </summary>
        private double Cross => (B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X);

        /// <summary>
        /// Gets the signed area of the triangle: positive when A, B, C run counter-clockwise, negative when they run
        /// clockwise.
        /// </summary>
        public double SignedArea => Cross * 0.5;

        /// <summary>
        /// Gets the area of the triangle, whichever way it runs.
        /// </summary>
        public double Area => Math.Abs(SignedArea);

        /// <summary>
        /// Gets a value indicating whether the vertices run clockwise.
        /// </summary>
        public bool IsClockwise => SignedArea < 0.0;

        /// <summary>
        /// Gets the perimeter of the triangle.
        /// </summary>
        public double Length => A.DistanceTo(B) + B.DistanceTo(C) + C.DistanceTo(A);

        /// <summary>
        /// Gets the centroid of the triangle, where its three medians meet.
        /// </summary>
        public GeoPoint2 Centroid => new GeoPoint2((A.X + B.X + C.X) / 3.0, (A.Y + B.Y + C.Y) / 3.0);

        /// <summary>
        /// Gets the angles at A, B and C, in radians.
        /// </summary>
        /// <returns>The three interior angles, which add up to π; on a triangle of no area two of them are nought.</returns>
        /// <remarks>
        /// Each angle is read from the cross and dot products of the two edges leaving its vertex, so a vertex standing
        /// on another, whose edges have no direction, has an angle of nought rather than a number that is not one.
        /// </remarks>
        public double[] GetAngles() => new[] { AngleAt(A, B, C), AngleAt(B, C, A), AngleAt(C, A, B) };

        /// <summary>
        /// Gets the smallest of the three angles, in radians: the one that says how thin a triangle is.
        /// </summary>
        public double MinAngle => Math.Min(AngleAt(A, B, C), Math.Min(AngleAt(B, C, A), AngleAt(C, A, B)));

        private static double AngleAt(GeoPoint2 vertex, GeoPoint2 next, GeoPoint2 previous)
        {
            double ux = next.X - vertex.X, uy = next.Y - vertex.Y;
            double vx = previous.X - vertex.X, vy = previous.Y - vertex.Y;

            return Math.Atan2(Math.Abs(ux * vy - uy * vx), ux * vx + uy * vy);
        }

        /// <summary>
        /// Gets the circle through all three vertices.
        /// </summary>
        /// <param name="circle">The circumcircle, or the default circle when the triangle is degenerate.</param>
        /// <returns>false when the triangle is degenerate, so that the three vertices lie on no one circle.</returns>
        public bool TryGetCircumcircle(out GeoCircle2 circle)
        {
            if (IsDegenerate())
            {
                circle = default;
                return false;
            }

            // Measured from A, so that a triangle far from the origin costs no precision.
            double bx = B.X - A.X, by = B.Y - A.Y;
            double cx = C.X - A.X, cy = C.Y - A.Y;
            double d = 2.0 * (bx * cy - by * cx);
            double b2 = bx * bx + by * by;
            double c2 = cx * cx + cy * cy;
            double ux = (cy * b2 - by * c2) / d;
            double uy = (bx * c2 - cx * b2) / d;

            circle = new GeoCircle2(new GeoPoint2(A.X + ux, A.Y + uy), Math.Sqrt(ux * ux + uy * uy));
            return true;
        }

        /// <summary>
        /// Gets the largest circle inside the triangle, touching all three edges.
        /// </summary>
        /// <param name="circle">The incircle, or the default circle when the triangle is degenerate.</param>
        /// <returns>false when the triangle is degenerate and has no inside for a circle to fit.</returns>
        public bool TryGetIncircle(out GeoCircle2 circle)
        {
            double a = B.DistanceTo(C);
            double b = C.DistanceTo(A);
            double c = A.DistanceTo(B);
            double perimeter = a + b + c;

            if (IsDegenerate() || !(perimeter > 0.0))
            {
                circle = default;
                return false;
            }

            var center = new GeoPoint2((a * A.X + b * B.X + c * C.X) / perimeter, (a * A.Y + b * B.Y + c * C.Y) / perimeter);
            circle = new GeoCircle2(center, 2.0 * Area / perimeter);
            return true;
        }

        /// <summary>
        /// Checks whether the triangle has no area, using the default tolerance.
        /// </summary>
        public bool IsDegenerate() => IsDegenerate(Tolerance.Global);

        /// <summary>
        /// Checks whether the triangle has no area, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The test is on twice the area, the cross product of two edges, against the vector tolerance, as
        /// <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/> tests its area vector. A sliver whose vertices are far apart
        /// but almost in a row is caught by the same threshold as three coincident points.
        /// </remarks>
        public bool IsDegenerate(Tolerance tolerance) => !(Math.Abs(Cross) > tolerance.EqualVector);

        #region Barycentric coordinates

        /// <summary>
        /// Gets the barycentric coordinates of a point with respect to this triangle.
        /// </summary>
        /// <param name="point">The point to express; it need not lie within the triangle.</param>
        /// <param name="u">Weight of vertex <see cref="A"/>.</param>
        /// <param name="v">Weight of vertex <see cref="B"/>.</param>
        /// <param name="w">Weight of vertex <see cref="C"/>.</param>
        /// <returns>false when the triangle is degenerate, in which case all three weights are nought.</returns>
        /// <remarks>
        /// The three weights always add up to one, and all three are positive or nought exactly when the point lies
        /// within the triangle.
        /// </remarks>
        public bool TryGetBarycentric(GeoPoint2 point, out double u, out double v, out double w)
        {
            double cross = Cross;

            if (IsDegenerate())
            {
                u = 0.0;
                v = 0.0;
                w = 0.0;
                return false;
            }

            double px = point.X - A.X, py = point.Y - A.Y;
            double bx = B.X - A.X, by = B.Y - A.Y;
            double cx = C.X - A.X, cy = C.Y - A.Y;

            v = (px * cy - py * cx) / cross;
            w = (bx * py - by * px) / cross;
            u = 1.0 - v - w;
            return true;
        }

        /// <summary>
        /// Gets the point at given barycentric coordinates.
        /// </summary>
        /// <param name="u">Weight of vertex <see cref="A"/>.</param>
        /// <param name="v">Weight of vertex <see cref="B"/>.</param>
        /// <param name="w">Weight of vertex <see cref="C"/>.</param>
        public GeoPoint2 GetPointAtBarycentric(double u, double v, double w)
        {
            return new GeoPoint2(u * A.X + v * B.X + w * C.X, u * A.Y + v * B.Y + w * C.Y);
        }

        #endregion

        #region Moving and turning

        /// <summary>
        /// Gets the triangle running the other way round: A, C, B.
        /// </summary>
        public GeoTriangle2 Reverse() => new GeoTriangle2(A, C, B);

        /// <summary>
        /// Moves the triangle by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The triangle in its new place.</returns>
        public GeoTriangle2 Translate(GeoVector2 vector) => new GeoTriangle2(A.Add(vector), B.Add(vector), C.Add(vector));

        /// <summary>
        /// Rotates the triangle around a point, counter-clockwise by an angle in radians.
        /// </summary>
        /// <param name="angleRad">Rotation angle in radians.</param>
        /// <param name="center">Center of rotation.</param>
        /// <returns>The rotated triangle.</returns>
        public GeoTriangle2 RotateBy(double angleRad, GeoPoint2 center)
            => new GeoTriangle2(A.RotateBy(angleRad, center), B.RotateBy(angleRad, center), C.RotateBy(angleRad, center));

        /// <summary>
        /// Applies a transformation to this triangle. A transformation that turns shapes round, a mirror or a negative
        /// scaling, reverses the winding.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed triangle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoTriangle2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Translates a triangle by a vector.
        /// </summary>
        public static GeoTriangle2 operator +(GeoTriangle2 triangle, GeoVector2 vector) => triangle.Translate(vector);

        /// <summary>
        /// Translates a triangle backwards by a vector.
        /// </summary>
        public static GeoTriangle2 operator -(GeoTriangle2 triangle, GeoVector2 vector) => triangle.Translate(-vector);

        #endregion

        #region Conversions

        /// <summary>
        /// Reads the triangle as a three-sided polygon, running the way the triangle does.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when two of the vertices coincide, within the default tolerance: a polygon has three distinct
        /// vertices at the least.
        /// </exception>
        public GeoPolygon2 ToPolygon() => new GeoPolygon2(A, B, C);

        /// <summary>
        /// Reads the boundary of the triangle as a closed chain, A to B to C and back to A.
        /// </summary>
        public GeoPolyline2 ToPolyline() => new GeoPolyline2(A, B, C, A);

        /// <summary>
        /// Puts this triangle of the plane into space, on the plane of a frame.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <returns>The triangle in space.</returns>
        public GeoTriangle3 ToTriangle3(GeoCoordinateSystem3 frame) => PlanarMap.ToTriangle3(frame, this);

        #endregion

        #region Where a point is

        /// <summary>
        /// Locates a point relative to this triangle, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to this triangle, within a tolerance: on its edges, inside it, or outside.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies inside this triangle or on its edges, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point) => Containment2.Contains(this, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies inside this triangle or on its edges, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point, Tolerance tolerance) => Containment2.Contains(this, point, tolerance);

        #endregion

        #region Holding, parallel, and walking along the edges

        /// <summary>
        /// Checks whether this triangle holds a segment whole, its edges included, using the default tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line) => Triangle2.Contains(this, line);

        /// <summary>
        /// Checks whether this triangle holds a segment whole, its edges included, within a tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line, Tolerance tolerance) => Triangle2.Contains(this, line, tolerance);

        /// <summary>
        /// Checks whether this triangle holds a polyline whole, its edges included, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPolyline2 polyline) => Triangle2.Contains(this, polyline);

        /// <summary>
        /// Checks whether this triangle holds a polyline whole, its edges included, within a tolerance.
        /// </summary>
        public bool Contains(GeoPolyline2 polyline, Tolerance tolerance) => Triangle2.Contains(this, polyline, tolerance);

        /// <summary>
        /// Checks whether an edge of this triangle runs parallel to a segment, using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 line) => Triangle2.IsParallel(this, line);

        /// <summary>
        /// Checks whether an edge of this triangle runs parallel to a segment, within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 line, Tolerance tolerance) => Triangle2.IsParallel(this, line, tolerance);

        /// <summary>
        /// Gets the point at a normalized parameter along the edges, from A, where 1 is all the way round; values outside
        /// [0, 1] wrap round, so 1.25 is the same place as 0.25.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Triangle2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter along the edges of the point on them closest to a point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Triangle2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at a length walked along the edges from A; lengths past the perimeter wrap round.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Triangle2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the length walked along the edges from A to the point on them closest to a point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Triangle2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the length walked along the edges from A to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Triangle2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at a length walked along the edges from A.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Triangle2.GetParameterAtDistance(this, distance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another triangle has exactly the same vertices, in the same order.
        /// </summary>
        public bool Equals(GeoTriangle2 other) => A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C);

        /// <summary>
        /// Determines whether the specified object is equal to the current triangle.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoTriangle2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = A.GetHashCode();
                hashCode = (hashCode * 397) ^ B.GetHashCode();
                hashCode = (hashCode * 397) ^ C.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compares whether this triangle equals another triangle using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoTriangle2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this triangle equals another triangle within a tolerance.
        /// </summary>
        /// <remarks>
        /// The vertices must correspond in order, though the starting vertex may differ: A, B, C matches B, C, A but not
        /// A, C, B, because the reversed winding runs the other way round.
        /// </remarks>
        public bool IsEqualTo(GeoTriangle2 other, Tolerance tolerance)
        {
            for (int shift = 0; shift < 3; shift++)
            {
                if (A.IsEqualTo(other[shift], tolerance) &&
                    B.IsEqualTo(other[(shift + 1) % 3], tolerance) &&
                    C.IsEqualTo(other[(shift + 2) % 3], tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if two triangles have exactly the same vertices, in the same order.
        /// </summary>
        public static bool operator ==(GeoTriangle2 left, GeoTriangle2 right) => left.Equals(right);

        /// <summary>
        /// Checks if two triangles differ in any vertex.
        /// </summary>
        public static bool operator !=(GeoTriangle2 left, GeoTriangle2 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: every corner is a valid point.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one, an element of a new array or what the <c>out</c>
        /// of a <c>Try</c> method holds when the method said false, may not be, and answers questions all the same.
        /// </remarks>
        public bool IsValid => A.IsValid && B.IsValid && C.IsValid;

        /// <summary>
        /// Returns a string that represents the current triangle.
        /// </summary>
        public override string ToString() => $"GeoTriangle2[{A}, {B}, {C}]";
    }
}
