using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a triangle in 3D space, defined by three vertices.
    /// <para>
    /// The triangle is the unit every other surface in this library is measured through. A polygon
    /// answers containment, area and intersection by fanning into triangles, and a solid does the same
    /// through its faces, so the hard cases live here once instead of once per shape. Three points are
    /// always coplanar, which is why a triangle needs no flatness check while a polygon does.
    /// </para>
    /// </summary>
    public readonly struct GeoTriangle3 : IEquatable<GeoTriangle3>
    {
        /// <summary>
        /// Gets the first vertex.
        /// </summary>
        public GeoPoint3 A { get; }

        /// <summary>
        /// Gets the second vertex.
        /// </summary>
        public GeoPoint3 B { get; }

        /// <summary>
        /// Gets the third vertex.
        /// </summary>
        public GeoPoint3 C { get; }

        /// <summary>
        /// Initializes a new triangle from three vertices.
        /// </summary>
        /// <param name="a">First vertex.</param>
        /// <param name="b">Second vertex.</param>
        /// <param name="c">Third vertex.</param>
        /// <remarks>
        /// Degenerate input is accepted rather than rejected: three collinear or coincident points still
        /// make a usable triangle whose <see cref="Area"/> is zero, and callers that care can ask
        /// <see cref="IsDegenerate()"/>. Rejecting it here would make triangulating a polygon fail on the
        /// slivers that triangulation naturally produces.
        /// </remarks>
        public GeoTriangle3(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c)
        {
            A = a;
            B = b;
            C = c;
        }

        /// <summary>
        /// Creates a copy of this triangle.
        /// </summary>
        /// <remarks>
        /// Triangle is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        public GeoTriangle3 Clone() => new GeoTriangle3(A, B, C);

        /// <summary>
        /// Gets the vertex at a given index, counted from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0, 1 or 2.</exception>
        public GeoPoint3 this[int index]
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
        public GeoLine3 GetEdgeAt(int index)
        {
            switch (index)
            {
                case 0: return new GeoLine3(A, B);
                case 1: return new GeoLine3(B, C);
                case 2: return new GeoLine3(C, A);
                default: throw new ArgumentOutOfRangeException(nameof(index), "A triangle has three edges.");
            }
        }

        /// <summary>
        /// Gets twice the area of the triangle as a vector normal to it, following the right-hand rule
        /// around A, B, C.
        /// </summary>
        /// <remarks>
        /// Both <see cref="Area"/> and <see cref="Normal"/> read this, because the cross product carries
        /// the answer to each: its length is twice the area and its direction is the normal. Exposing it
        /// lets callers that need both pay for the cross product once.
        /// </remarks>
        public GeoVector3 GetAreaVector() => A.GetVectorTo(B).CrossProduct(A.GetVectorTo(C));

        /// <summary>
        /// Gets the area of the triangle.
        /// </summary>
        public double Area => GetAreaVector().Length * 0.5;

        /// <summary>
        /// Gets the perimeter of the triangle.
        /// </summary>
        public double Perimeter => A.DistanceTo(B) + B.DistanceTo(C) + C.DistanceTo(A);

        /// <summary>
        /// Gets the unit normal of the triangle, following the right-hand rule around A, B, C.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the triangle is degenerate.</exception>
        public GeoVector3 Normal => GetAreaVector().Normalize();

        /// <summary>
        /// Gets the centroid of the triangle.
        /// </summary>
        public GeoPoint3 Centroid => new GeoPoint3(
            (A.X + B.X + C.X) / 3.0,
            (A.Y + B.Y + C.Y) / 3.0,
            (A.Z + B.Z + C.Z) / 3.0);

        /// <summary>
        /// Gets the plane carrying the triangle.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the triangle is degenerate.</exception>
        public GeoPlane3 GetPlane() => new GeoPlane3(A, Normal);

        /// <summary>
        /// Gets the triangle with its vertices in the opposite order, so its normal reverses.
        /// </summary>
        public GeoTriangle3 Flip() => new GeoTriangle3(A, C, B);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this triangle.
        /// </summary>
        public GeoAabb3 GetAabb() => GeoAabb3.FromPoints(new[] { A, B, C });

        /// <summary>
        /// Applies a transformation to this triangle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoTriangle3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Checks whether the triangle has no area, using the default tolerance.
        /// </summary>
        public bool IsDegenerate() => IsDegenerate(Tolerance.Global);

        /// <summary>
        /// Checks whether the triangle has no area, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The test is on the area vector rather than on the area, so a sliver whose vertices are far
        /// apart but almost collinear is caught by the same threshold that catches three coincident points.
        /// </remarks>
        public bool IsDegenerate(Tolerance tolerance) => GetAreaVector().IsZeroLength(tolerance);

        #region Barycentric coordinates

        /// <summary>
        /// Gets the barycentric coordinates of a point with respect to this triangle.
        /// </summary>
        /// <param name="point">The point to express; it need not lie on the triangle.</param>
        /// <param name="u">Weight of vertex <see cref="A"/>.</param>
        /// <param name="v">Weight of vertex <see cref="B"/>.</param>
        /// <param name="w">Weight of vertex <see cref="C"/>.</param>
        /// <returns>false when the triangle is degenerate, in which case all three weights are zero.</returns>
        /// <remarks>
        /// The point is projected onto the triangle plane first, so a point off the plane gets the
        /// coordinates of its projection. The three weights always sum to one, and all three are
        /// non-negative exactly when the projection falls inside the triangle.
        /// </remarks>
        public bool TryGetBarycentric(GeoPoint3 point, out double u, out double v, out double w)
        {
            GeoVector3 ab = A.GetVectorTo(B);
            GeoVector3 ac = A.GetVectorTo(C);
            GeoVector3 ap = A.GetVectorTo(point);

            double d00 = ab.DotProduct(ab);
            double d01 = ab.DotProduct(ac);
            double d11 = ac.DotProduct(ac);
            double d20 = ap.DotProduct(ab);
            double d21 = ap.DotProduct(ac);

            double denominator = d00 * d11 - d01 * d01;

            // The denominator is the squared length of the area vector, so it vanishes for exactly the
            // triangles IsDegenerate reports, and comparing it against zero here would let slivers through
            // that the rest of the library treats as degenerate.
            if (Math.Abs(denominator) <= double.Epsilon || IsDegenerate())
            {
                u = 0.0;
                v = 0.0;
                w = 0.0;
                return false;
            }

            v = (d11 * d20 - d01 * d21) / denominator;
            w = (d00 * d21 - d01 * d20) / denominator;
            u = 1.0 - v - w;
            return true;
        }

        /// <summary>
        /// Gets the point at given barycentric coordinates.
        /// </summary>
        /// <param name="u">Weight of vertex <see cref="A"/>.</param>
        /// <param name="v">Weight of vertex <see cref="B"/>.</param>
        /// <param name="w">Weight of vertex <see cref="C"/>.</param>
        public GeoPoint3 GetPointAtBarycentric(double u, double v, double w)
        {
            return new GeoPoint3(
                u * A.X + v * B.X + w * C.X,
                u * A.Y + v * B.Y + w * C.Y,
                u * A.Z + v * B.Z + w * C.Z);
        }

        #endregion

        #region Queries

        /// <summary>
        /// Calculates the shortest distance from this triangle to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the closest point on this triangle to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToTriangle(this, point);

        /// <summary>
        /// Locates a point relative to this triangle, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this triangle, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Containment3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies inside this triangle or on its edges, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Containment3.Contains(this, point);

        /// <summary>
        /// Checks whether a point lies inside this triangle or on its edges, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Containment3.Contains(this, point, tolerance);

        /// <summary>
        /// Tries to find the point where a line segment crosses this triangle, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a line segment crosses this triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this triangle, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another triangle has exactly the same vertices, in the same order.
        /// </summary>
        public bool Equals(GeoTriangle3 other) => A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C);

        /// <summary>
        /// Determines whether the specified object is equal to the current triangle.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoTriangle3 other && Equals(other);

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
        public bool IsEqualTo(GeoTriangle3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this triangle equals another triangle within a tolerance.
        /// </summary>
        /// <remarks>
        /// The vertices must correspond in order, though the starting vertex may differ: A, B, C matches
        /// B, C, A but not A, C, B, because the reversed winding describes the opposite orientation.
        /// </remarks>
        public bool IsEqualTo(GeoTriangle3 other, Tolerance tolerance)
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
        public static bool operator ==(GeoTriangle3 left, GeoTriangle3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two triangles differ in any vertex.
        /// </summary>
        public static bool operator !=(GeoTriangle3 left, GeoTriangle3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current triangle.
        /// </summary>
        public override string ToString() => $"Triangle3({A}, {B}, {C})";
    }
}
