using System;
using System.Collections.Generic;
using CommonGeometry;
using CommonGeometry.Enums;

namespace SolidGeometry.Geometry
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB): the smallest box with edges along the world axes
    /// that holds a set of geometry.
    /// <para>
    /// This is the cheap test that comes before the expensive one. Two shapes whose bounding boxes do not
    /// overlap cannot possibly touch, and that is decided by six comparisons, so a scan over many shapes
    /// rejects nearly all of them here and only pays for real geometry on the few that survive.
    /// <see cref="GeoObb3"/> is the other kind of box: it carries its own orientation, describes a shape
    /// rather than a bound, and costs far more to test.
    /// </para>
    /// <para>
    /// An empty box is a real value here rather than a null: it holds nothing, has no corners, and grows
    /// into a proper box as soon as a point is added to it. <c>default(GeoAabb3)</c> is empty,
    /// which makes it the right seed for a loop that unions boxes together.
    /// </para>
    /// </summary>
    public readonly struct GeoAabb3 : IEquatable<GeoAabb3>
    {
        /// <summary>
        /// Gets the corner with the smallest coordinate on every axis.
        /// </summary>
        /// <remarks>Meaningless when <see cref="IsEmpty"/> is true.</remarks>
        public GeoPoint3 Min { get; }

        /// <summary>
        /// Gets the corner with the largest coordinate on every axis.
        /// </summary>
        /// <remarks>Meaningless when <see cref="IsEmpty"/> is true.</remarks>
        public GeoPoint3 Max { get; }

        /// <summary>
        /// Gets whether the box holds anything at all.
        /// </summary>
        public bool IsEmpty => !HasValue;

        /// <summary>
        /// Backing flag for <see cref="IsEmpty"/>, phrased positively so that the default value of the
        /// struct is the empty box rather than a zero-sized box at the world origin.
        /// </summary>
        private bool HasValue { get; }

        /// <summary>
        /// Gets the empty box, which holds nothing.
        /// </summary>
        public static GeoAabb3 Empty => default;

        /// <summary>
        /// Initializes a box spanning two opposite corners, in either order.
        /// </summary>
        /// <param name="corner1">One corner.</param>
        /// <param name="corner2">The opposite corner.</param>
        public GeoAabb3(GeoPoint3 corner1, GeoPoint3 corner2)
        {
            Min = new GeoPoint3(
                Math.Min(corner1.X, corner2.X),
                Math.Min(corner1.Y, corner2.Y),
                Math.Min(corner1.Z, corner2.Z));

            Max = new GeoPoint3(
                Math.Max(corner1.X, corner2.X),
                Math.Max(corner1.Y, corner2.Y),
                Math.Max(corner1.Z, corner2.Z));

            HasValue = true;
        }

        /// <summary>
        /// Creates the smallest box holding every one of a set of points.
        /// </summary>
        /// <param name="points">The points to enclose; an empty sequence gives <see cref="Empty"/>.</param>
        public static GeoAabb3 FromPoints(IEnumerable<GeoPoint3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            GeoAabb3 box = Empty;

            foreach (GeoPoint3 point in points)
            {
                box = box.Union(point);
            }

            return box;
        }

        /// <summary>
        /// Creates a copy of this box.
        /// </summary>
        /// <remarks>
        /// Bounding box is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        public GeoAabb3 Clone() => this;

        /// <summary>
        /// Applies a transformation to this box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoAabb3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        #region Measurements

        /// <summary>
        /// Gets the size of the box along the X axis, or zero when it is empty.
        /// </summary>
        public double SizeX => IsEmpty ? 0.0 : Max.X - Min.X;

        /// <summary>
        /// Gets the size of the box along the Y axis, or zero when it is empty.
        /// </summary>
        public double SizeY => IsEmpty ? 0.0 : Max.Y - Min.Y;

        /// <summary>
        /// Gets the size of the box along the Z axis, or zero when it is empty.
        /// </summary>
        public double SizeZ => IsEmpty ? 0.0 : Max.Z - Min.Z;

        /// <summary>
        /// Gets the diagonal of the box as a vector from <see cref="Min"/> to <see cref="Max"/>.
        /// </summary>
        public GeoVector3 Diagonal => new GeoVector3(SizeX, SizeY, SizeZ);

        /// <summary>
        /// Gets the centre of the box, or the world origin when it is empty.
        /// </summary>
        public GeoPoint3 Center => IsEmpty ? GeoPoint3.Origin : Min.GetMiddlePoint(Max);

        /// <summary>
        /// Gets the volume of the box.
        /// </summary>
        public double Volume => SizeX * SizeY * SizeZ;

        /// <summary>
        /// Gets the total area of the six faces of the box.
        /// </summary>
        public double SurfaceArea => 2.0 * (SizeX * SizeY + SizeY * SizeZ + SizeZ * SizeX);

        /// <summary>
        /// Gets the eight corners of the box, or an empty array when it is empty.
        /// </summary>
        public GeoPoint3[] GetCorners()
        {
            if (IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return new[]
            {
                new GeoPoint3(Min.X, Min.Y, Min.Z),
                new GeoPoint3(Max.X, Min.Y, Min.Z),
                new GeoPoint3(Max.X, Max.Y, Min.Z),
                new GeoPoint3(Min.X, Max.Y, Min.Z),
                new GeoPoint3(Min.X, Min.Y, Max.Z),
                new GeoPoint3(Max.X, Min.Y, Max.Z),
                new GeoPoint3(Max.X, Max.Y, Max.Z),
                new GeoPoint3(Min.X, Max.Y, Max.Z)
            };
        }

        #endregion

        #region Combination

        /// <summary>
        /// Gets the smallest box holding both this box and a point.
        /// </summary>
        public GeoAabb3 Union(GeoPoint3 point)
        {
            return IsEmpty ? new GeoAabb3(point, point) : new GeoAabb3(
                new GeoPoint3(Math.Min(Min.X, point.X), Math.Min(Min.Y, point.Y), Math.Min(Min.Z, point.Z)),
                new GeoPoint3(Math.Max(Max.X, point.X), Math.Max(Max.Y, point.Y), Math.Max(Max.Z, point.Z)));
        }

        /// <summary>
        /// Gets the smallest box holding both this box and another one.
        /// </summary>
        public GeoAabb3 Union(GeoAabb3 other)
        {
            if (IsEmpty)
            {
                return other;
            }

            if (other.IsEmpty)
            {
                return this;
            }

            return Union(other.Min).Union(other.Max);
        }

        /// <summary>
        /// Gets the box shared by this box and another one, or <see cref="Empty"/> when they do not overlap.
        /// </summary>
        public GeoAabb3 Intersect(GeoAabb3 other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return Empty;
            }

            double minX = Math.Max(Min.X, other.Min.X);
            double minY = Math.Max(Min.Y, other.Min.Y);
            double minZ = Math.Max(Min.Z, other.Min.Z);
            double maxX = Math.Min(Max.X, other.Max.X);
            double maxY = Math.Min(Max.Y, other.Max.Y);
            double maxZ = Math.Min(Max.Z, other.Max.Z);

            if (minX > maxX || minY > maxY || minZ > maxZ)
            {
                return Empty;
            }

            return new GeoAabb3(new GeoPoint3(minX, minY, minZ), new GeoPoint3(maxX, maxY, maxZ));
        }

        /// <summary>
        /// Gets this box grown outwards by a margin on every side.
        /// </summary>
        /// <param name="margin">How far to push each face out; negative shrinks the box.</param>
        /// <remarks>
        /// Shrinking further than the box is wide gives the empty box rather than one turned inside out.
        /// </remarks>
        public GeoAabb3 Expand(double margin)
        {
            if (IsEmpty)
            {
                return Empty;
            }

            GeoVector3 offset = new GeoVector3(margin, margin, margin);
            GeoPoint3 min = Min.Subtract(offset);
            GeoPoint3 max = Max.Add(offset);

            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                return Empty;
            }

            return new GeoAabb3(min, max);
        }

        #endregion

        #region Queries

        /// <summary>
        /// Locates a point relative to this box, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to this box, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance)
        {
            if (IsEmpty)
            {
                return PointLocation.OutSide;
            }

            double t = tolerance.EqualPoint;

            if (point.X < Min.X - t || point.X > Max.X + t ||
                point.Y < Min.Y - t || point.Y > Max.Y + t ||
                point.Z < Min.Z - t || point.Z > Max.Z + t)
            {
                return PointLocation.OutSide;
            }

            bool onSurface =
                Math.Abs(point.X - Min.X) <= t || Math.Abs(point.X - Max.X) <= t ||
                Math.Abs(point.Y - Min.Y) <= t || Math.Abs(point.Y - Max.Y) <= t ||
                Math.Abs(point.Z - Min.Z) <= t || Math.Abs(point.Z - Max.Z) <= t;

            return onSurface ? PointLocation.OnSide : PointLocation.Inside;
        }

        /// <summary>
        /// Checks whether this box holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Contains(point, Tolerance.Global);

        /// <summary>
        /// Checks whether this box holds a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Locate(point, tolerance) != PointLocation.OutSide;

        /// <summary>
        /// Checks whether this box holds another box entirely, using the default tolerance.
        /// </summary>
        public bool Contains(GeoAabb3 other) => Contains(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this box holds another box entirely, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The empty box holds nothing and is held by everything, following the usual reading of an empty
        /// set.
        /// </remarks>
        public bool Contains(GeoAabb3 other, Tolerance tolerance)
        {
            if (other.IsEmpty)
            {
                return true;
            }

            if (IsEmpty)
            {
                return false;
            }

            double t = tolerance.EqualPoint;

            return other.Min.X >= Min.X - t && other.Max.X <= Max.X + t &&
                   other.Min.Y >= Min.Y - t && other.Max.Y <= Max.Y + t &&
                   other.Min.Z >= Min.Z - t && other.Max.Z <= Max.Z + t;
        }

        /// <summary>
        /// Gets the point of this box closest to a target point. A point inside the box is already on it
        /// under that reading and comes back unchanged.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point)
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("An empty bounding box has no point to return.");
            }

            return new GeoPoint3(
                Math.Max(Min.X, Math.Min(Max.X, point.X)),
                Math.Max(Min.Y, Math.Min(Max.Y, point.Y)),
                Math.Max(Min.Z, Math.Min(Max.Z, point.Z)));
        }

        /// <summary>
        /// Calculates the shortest distance from this box to a point. A point inside is at distance zero,
        /// and an empty box is infinitely far from everything.
        /// </summary>
        /// <remarks>
        /// This is what prunes a search through a tree of boxes: a box already farther away than the best
        /// answer so far cannot hold anything nearer, so the whole branch below it can be skipped without
        /// being looked at.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point)
        {
            if (IsEmpty)
            {
                return double.PositiveInfinity;
            }

            return GetClosestPointOnBoundary(point).DistanceTo(point);
        }

        /// <summary>
        /// Calculates the shortest distance between this box and another one. Boxes that overlap are at
        /// distance zero, and an empty box is infinitely far from everything.
        /// </summary>
        /// <remarks>
        /// The gap between two axis-aligned boxes separates on each axis independently, so the distance is
        /// the length of the vector of per-axis gaps. This is what prunes a traversal of two trees at once:
        /// a pair of boxes already farther apart than the best answer so far cannot hold a nearer pair.
        /// </remarks>
        public double DistanceTo(GeoAabb3 other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return double.PositiveInfinity;
            }

            double gapX = Math.Max(0.0, Math.Max(Min.X - other.Max.X, other.Min.X - Max.X));
            double gapY = Math.Max(0.0, Math.Max(Min.Y - other.Max.Y, other.Min.Y - Max.Y));
            double gapZ = Math.Max(0.0, Math.Max(Min.Z - other.Max.Z, other.Min.Z - Max.Z));

            return Math.Sqrt(gapX * gapX + gapY * gapY + gapZ * gapZ);
        }

        /// <summary>
        /// Checks whether this box overlaps another one, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this box overlaps another one, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 other, Tolerance tolerance)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return false;
            }

            double t = tolerance.EqualPoint;

            return Min.X - t <= other.Max.X && Max.X + t >= other.Min.X &&
                   Min.Y - t <= other.Max.Y && Max.Y + t >= other.Min.Y &&
                   Min.Z - t <= other.Max.Z && Max.Z + t >= other.Min.Z;
        }

        /// <summary>
        /// Gets this box as an oriented box aligned with the world axes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoObb3 ToObb()
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("An empty bounding box has no shape to convert.");
            }

            return new GeoObb3(Center, SizeX, SizeY, SizeZ);
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another box has exactly the same corners, with two empty boxes counting as equal.
        /// </summary>
        public bool Equals(GeoAabb3 other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return IsEmpty && other.IsEmpty;
            }

            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current box.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoAabb3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            if (IsEmpty)
            {
                return 0;
            }

            unchecked
            {
                return (Min.GetHashCode() * 397) ^ Max.GetHashCode();
            }
        }

        /// <summary>
        /// Compares whether this box equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoAabb3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this box equals another within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoAabb3 other, Tolerance tolerance)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return IsEmpty && other.IsEmpty;
            }

            return Min.IsEqualTo(other.Min, tolerance) && Max.IsEqualTo(other.Max, tolerance);
        }

        /// <summary>
        /// Checks if two boxes have exactly the same corners.
        /// </summary>
        public static bool operator ==(GeoAabb3 left, GeoAabb3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two boxes differ in either corner.
        /// </summary>
        public static bool operator !=(GeoAabb3 left, GeoAabb3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current box.
        /// </summary>
        public override string ToString() => IsEmpty ? "BoundingBox3(Empty)" : $"BoundingBox3({Min} .. {Max})";
    }
}
