using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// A point of the model a drawing element is tied to, in model millimetres, with the object it lies on: one end of a
    /// dimension, the point a mark's leader starts from.
    /// </summary>
    /// <remarks>
    /// The overall dimension of a beam B-12 3 000 long measures from <c>new DrawingLinkPoint3("B-12", new GeoPoint3(0, 0, 0))</c>
    /// to <c>new DrawingLinkPoint3("B-12", new GeoPoint3(3000, 0, 0))</c>. Two link points are equal when their object ids are
    /// the same, character by character, and their coordinates the same to the bit, as a document saved and opened again
    /// gives them back: they are a record of what was linked, not a measure, so no tolerance is applied.
    /// </remarks>
    public readonly struct DrawingLinkPoint3 : IEquatable<DrawingLinkPoint3>
    {
        /// <summary>Makes a link to a point of the model.</summary>
        /// <param name="objectId">The id of the object the point lies on; null for a point on no object, as a grid point.</param>
        /// <param name="point">The point, in model millimetres.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate of the point is not a finite number.</exception>
        public DrawingLinkPoint3(string objectId, GeoPoint3 point)
        {
            if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
            {
                throw new ArgumentException("A linked point has finite coordinates.", nameof(point));
            }

            ObjectId = objectId;
            Point = point;
        }

        /// <summary>Gets the id of the object the point lies on; null where it lies on none.</summary>
        public string ObjectId { get; }

        /// <summary>Gets the point, in model millimetres.</summary>
        public GeoPoint3 Point { get; }

        /// <summary>Whether two link points are the same: the same object id and the same coordinates to the bit.</summary>
        /// <param name="left">One link point.</param>
        /// <param name="right">The other.</param>
        /// <returns>Whether they are the same.</returns>
        public static bool operator ==(DrawingLinkPoint3 left, DrawingLinkPoint3 right) => left.Equals(right);

        /// <summary>Whether two link points differ in their object id or a bit of a coordinate.</summary>
        /// <param name="left">One link point.</param>
        /// <param name="right">The other.</param>
        /// <returns>Whether they differ.</returns>
        public static bool operator !=(DrawingLinkPoint3 left, DrawingLinkPoint3 right) => !left.Equals(right);

        /// <summary>Whether this link point is the same as another: the same object id and the same coordinates to the bit.</summary>
        /// <param name="other">The other link point.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DrawingLinkPoint3 other)
            => string.Equals(ObjectId, other.ObjectId, StringComparison.Ordinal)
               && DrawingRecordGroups.SameBits(Point.X, other.Point.X)
               && DrawingRecordGroups.SameBits(Point.Y, other.Point.Y)
               && DrawingRecordGroups.SameBits(Point.Z, other.Point.Z);

        /// <summary>Whether an object is a link point the same as this one.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => obj is DrawingLinkPoint3 other && Equals(other);

        /// <summary>Gives a hash code that agrees with <see cref="Equals(DrawingLinkPoint3)"/>.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ObjectId == null ? 0 : StringComparer.Ordinal.GetHashCode(ObjectId);
                hash = (hash * 397) ^ BitConverter.DoubleToInt64Bits(Point.X).GetHashCode();
                hash = (hash * 397) ^ BitConverter.DoubleToInt64Bits(Point.Y).GetHashCode();
                return (hash * 397) ^ BitConverter.DoubleToInt64Bits(Point.Z).GetHashCode();
            }
        }

        /// <summary>Gives the link point as a line for debugging.</summary>
        /// <returns>The line, such as <c>B-12 (3000, 0, 0)</c>.</returns>
        public override string ToString() => (ObjectId ?? "(no object)") + " " + Point;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
