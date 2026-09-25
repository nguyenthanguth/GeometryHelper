using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D region with holes: an outer boundary polygon with polygons cut out of it.
    /// <para>
    /// A face is what a polygon becomes once it has to carry openings: a plate with bolt holes, a slab with a
    /// shaft through it, or the area an outward offset leaves round a courtyard it has closed off. The outer
    /// loop says where the material ends and each hole says where it is missing. It is the plane counterpart
    /// of <c>GeoFace3</c>.
    /// </para>
    /// <para>
    /// The holes are expected to lie inside the boundary and apart from each other, but this is not checked,
    /// for the same reason <see cref="GeoPolygon2"/> does not check that it is simple. Point location reads a
    /// point inside any hole as outside the face.
    /// </para>
    /// </summary>
    public sealed class GeoFace2 : IEquatable<GeoFace2>
    {
        private readonly GeoPolygon2[] _holes;

        /// <summary>
        /// Initializes a face with no holes.
        /// </summary>
        /// <param name="boundary">The outer boundary.</param>
        /// <exception cref="ArgumentNullException">Thrown when the boundary is null.</exception>
        public GeoFace2(GeoPolygon2 boundary)
            : this(boundary, null)
        {
        }

        /// <summary>
        /// Initializes a face with holes.
        /// </summary>
        /// <param name="boundary">The outer boundary.</param>
        /// <param name="holes">The holes; null is read as none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the boundary is null.</exception>
        /// <exception cref="ArgumentException">Thrown when one of the holes is null.</exception>
        public GeoFace2(GeoPolygon2 boundary, IEnumerable<GeoPolygon2> holes)
        {
            Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));

            List<GeoPolygon2> kept = new List<GeoPolygon2>();

            if (holes != null)
            {
                foreach (GeoPolygon2 hole in holes)
                {
                    kept.Add(hole ?? throw new ArgumentException("A face cannot carry a null hole.", nameof(holes)));
                }
            }

            _holes = kept.ToArray();

            double area = boundary.Area;
            double length = boundary.Length;

            foreach (GeoPolygon2 hole in _holes)
            {
                area -= hole.Area;
                length += hole.Length;
            }

            // A hole reaching outside the boundary, or two holes overlapping, would drive this below zero.
            // Neither is a face this type promises to handle, and clamping keeps the value usable.
            Area = Math.Max(0.0, area);
            Length = length;
        }

        /// <summary>
        /// Gets the outer boundary of the face.
        /// </summary>
        public GeoPolygon2 Boundary { get; }

        /// <summary>
        /// Gets the read-only list of holes cut out of the face.
        /// </summary>
        public IReadOnlyList<GeoPolygon2> Holes => _holes;

        /// <summary>
        /// Gets the area of the face, with the area of every hole taken off.
        /// </summary>
        public double Area { get; }

        /// <summary>
        /// Gets the total length of the outline: the boundary and the rim of every hole.
        /// </summary>
        public double Length { get; }

        /// <summary>
        /// Creates a copy of this face, boundary and holes included.
        /// </summary>
        public GeoFace2 Clone()
        {
            GeoPolygon2[] copies = new GeoPolygon2[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                copies[i] = _holes[i].Clone();
            }

            return new GeoFace2(Boundary.Clone(), copies);
        }

        /// <summary>
        /// Translates the face by a displacement vector.
        /// </summary>
        /// <param name="vector">The displacement vector.</param>
        /// <returns>A new translated face.</returns>
        public GeoFace2 Translate(GeoVector2 vector)
        {
            GeoPolygon2[] moved = new GeoPolygon2[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                moved[i] = _holes[i].Translate(vector);
            }

            return new GeoFace2(Boundary.Translate(vector), moved);
        }

        /// <summary>
        /// Rotates the face around a center point by an angle in radians (counter-clockwise).
        /// </summary>
        /// <param name="angleRad">Rotation angle in radians.</param>
        /// <param name="center">Center of rotation.</param>
        /// <returns>A new rotated face.</returns>
        public GeoFace2 RotateBy(double angleRad, GeoPoint2 center)
        {
            GeoPolygon2[] turned = new GeoPolygon2[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                turned[i] = _holes[i].RotateBy(angleRad, center);
            }

            return new GeoFace2(Boundary.RotateBy(angleRad, center), turned);
        }

        #region Queries

        /// <summary>
        /// Locates a point relative to this face using default tolerance: inside the material, on the boundary
        /// or the rim of a hole, or outside, which includes inside a hole.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to this face within tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this face holds a point, on its outline included, using default tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point) => Containment2.Contains(this, point, Tolerance.Global);

        /// <summary>
        /// Checks whether this face holds a point, on its outline included, within tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point, Tolerance tolerance) => Containment2.Contains(this, point, tolerance);

        #endregion

        #region Offsetting

        /// <summary>
        /// Grows the face by a distance, or shrinks it when the distance is negative, with sharp corners, using
        /// default tolerance. The holes shrink as the boundary grows. See <see cref="Offset2"/>.
        /// </summary>
        public GeoFace2[] Offset(double distance) => Offset2.Offset(this, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Grows the face by a distance, or shrinks it when the distance is negative, with sharp corners, within
        /// tolerance.
        /// </summary>
        public GeoFace2[] Offset(double distance, Tolerance tolerance) => Offset2.Offset(this, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Grows the face by a distance, or shrinks it when the distance is negative, with the given corners,
        /// using default tolerance.
        /// </summary>
        public GeoFace2[] Offset(double distance, OffsetJoin join) => Offset2.Offset(this, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Grows the face by a distance, or shrinks it when the distance is negative, with the given corners,
        /// within tolerance.
        /// </summary>
        public GeoFace2[] Offset(double distance, OffsetJoin join, Tolerance tolerance) => Offset2.Offset(this, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Grows the face by a distance, or shrinks it when the distance is negative, as the options say, using
        /// default tolerance.
        /// </summary>
        public GeoFace2[] Offset(double distance, OffsetOptions options) => Offset2.Offset(this, distance, options, Tolerance.Global);

        /// <summary>
        /// Grows the face by a distance, or shrinks it when the distance is negative, as the options say, within
        /// tolerance.
        /// </summary>
        public GeoFace2[] Offset(double distance, OffsetOptions options, Tolerance tolerance) => Offset2.Offset(this, distance, options, tolerance);

        #endregion

        #region Combining

        /// <summary>
        /// Gets the region covered by this face or another, using default tolerance. See <see cref="Boolean2"/>.
        /// </summary>
        public GeoFace2[] Union(GeoFace2 other) => Boolean2.Union(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by this face or another, within tolerance.
        /// </summary>
        public GeoFace2[] Union(GeoFace2 other, Tolerance tolerance) => Boolean2.Union(this, other, tolerance);

        /// <summary>
        /// Gets the region covered by both this face and another, using default tolerance.
        /// </summary>
        public GeoFace2[] Intersect(GeoFace2 other) => Boolean2.Intersect(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by both this face and another, within tolerance.
        /// </summary>
        public GeoFace2[] Intersect(GeoFace2 other, Tolerance tolerance) => Boolean2.Intersect(this, other, tolerance);

        /// <summary>
        /// Gets the region of this face with another taken out of it, using default tolerance.
        /// </summary>
        public GeoFace2[] Subtract(GeoFace2 tool) => Boolean2.Subtract(this, tool, Tolerance.Global);

        /// <summary>
        /// Gets the region of this face with another taken out of it, within tolerance.
        /// </summary>
        public GeoFace2[] Subtract(GeoFace2 tool, Tolerance tolerance) => Boolean2.Subtract(this, tool, tolerance);

        /// <summary>
        /// Gets the region of this face with every one of a set of faces taken out of it, using default tolerance.
        /// </summary>
        public GeoFace2[] Subtract(IEnumerable<GeoFace2> tools) => Boolean2.Subtract(this, tools, Tolerance.Global);

        /// <summary>
        /// Gets the region of this face with every one of a set of faces taken out of it, within tolerance.
        /// </summary>
        public GeoFace2[] Subtract(IEnumerable<GeoFace2> tools, Tolerance tolerance) => Boolean2.Subtract(this, tools, tolerance);

        /// <summary>
        /// Gets the region covered by exactly one of this face and another, using default tolerance.
        /// </summary>
        public GeoFace2[] Xor(GeoFace2 other) => Boolean2.Xor(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by exactly one of this face and another, within tolerance.
        /// </summary>
        public GeoFace2[] Xor(GeoFace2 other, Tolerance tolerance) => Boolean2.Xor(this, other, tolerance);

        #endregion

        /// <summary>
        /// Calculates the shortest distance from this face to a point.
        /// </summary>
        /// <remarks>
        /// The face is read as filled, so a point on the material is nought away and a point in one of
        /// its holes is measured to the rim it sits in.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this face to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the distance from this face to a point, negative for a point on the material.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the boundary, whichever side of it the point is on, and the sign
        /// says which side: negative on the material, nought on an edge, positive off it.
        /// <see cref="DistanceTo(GeoPoint2)"/> reads the face as filled and so answers nothing at all for a
        /// point on the material, which is the one place the two part company. The boundary of a face is its
        /// outline and the rim of every hole, so a point in a hole is off the material.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this face to a point, negative for a point on the material, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);

        #region Equality

        /// <summary>
        /// Determines whether another face has exactly the same boundary and holes, in the same order.
        /// </summary>
        public bool Equals(GeoFace2 other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!Boundary.Equals(other.Boundary) || _holes.Length != other._holes.Length)
            {
                return false;
            }

            for (int i = 0; i < _holes.Length; i++)
            {
                if (!_holes[i].Equals(other._holes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is an equal face.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoFace2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Boundary.GetHashCode();

                foreach (GeoPolygon2 hole in _holes)
                {
                    hash = hash * 31 + hole.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Compares two faces for equality.
        /// </summary>
        public static bool operator ==(GeoFace2 left, GeoFace2 right) => Equals(left, right);

        /// <summary>
        /// Compares two faces for inequality.
        /// </summary>
        public static bool operator !=(GeoFace2 left, GeoFace2 right) => !Equals(left, right);

        #endregion

        /// <summary>
        /// Returns the string representation of the face.
        /// </summary>
        public override string ToString() => $"GeoFace2[Area:{Area:0.###}, Holes:{_holes.Length}]";

        /// <summary>
        /// Puts this face of the plane back into space, holes and all, on the plane of a frame.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <returns>The face in space.</returns>
        public GeoFace3 ToFace3(GeoCoordinateSystem3 frame) => PlanarMap.ToFace3(frame, this);

        /// <summary>
        /// Gets the centroid of the face: the centroid of its boundary with its holes taken out, so a plate
        /// balances at this point whatever has been cut from it.
        /// </summary>
        /// <remarks>
        /// A face whose holes cancel its boundary has no centroid to give, and its boundary centroid is
        /// returned instead rather than a division by zero.
        /// </remarks>
        public GeoPoint2 Centroid
        {
            get
            {
                double area = Boundary.Area;
                double x = Boundary.Centroid.X * area;
                double y = Boundary.Centroid.Y * area;

                foreach (GeoPolygon2 hole in Holes)
                {
                    double holeArea = hole.Area;
                    area -= holeArea;
                    x -= hole.Centroid.X * holeArea;
                    y -= hole.Centroid.Y * holeArea;
                }

                if (Math.Abs(area) <= Tolerance.Global.EqualPoint * Tolerance.Global.EqualPoint)
                {
                    return Boundary.Centroid;
                }

                return new GeoPoint2(x / area, y / area);
            }
        }

        /// <summary>
        /// Applies a transformation to this face. Its holes come with it.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed face.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoFace2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Determines whether another face covers the same region, within the default tolerance.
        /// </summary>
        /// <param name="other">The face to compare with.</param>
        /// <returns>true if the faces are the same within tolerance; otherwise, false.</returns>
        public bool IsEqualTo(GeoFace2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another face covers the same region, within a tolerance.
        /// </summary>
        /// <param name="other">The face to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the faces are the same within tolerance; otherwise, false.</returns>
        /// <remarks>
        /// The order the holes are held in is not part of the shape, so each hole of this face is matched
        /// against an unclaimed hole of the other.
        /// </remarks>
        public bool IsEqualTo(GeoFace2 other, Tolerance tolerance)
        {
            if (other is null || Holes.Count != other.Holes.Count)
            {
                return false;
            }

            if (!Boundary.IsEqualTo(other.Boundary, tolerance))
            {
                return false;
            }

            bool[] matched = new bool[Holes.Count];

            foreach (GeoPolygon2 hole in Holes)
            {
                bool found = false;

                for (int i = 0; i < other.Holes.Count; i++)
                {
                    if (!matched[i] && hole.IsEqualTo(other.Holes[i], tolerance))
                    {
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
