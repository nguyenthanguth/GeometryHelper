using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Planar
{
    /// <summary>
    /// The plane algebra the internal offset and region code is written in, as shorthands over
    /// <see cref="GeoPoint2"/> and <see cref="GeoVector2"/>.
    /// <para>
    /// The engine used to carry its own point-and-vector struct, because the plane and solid halves of the
    /// library were separate assemblies and only one of them could see <see cref="GeoPoint2"/>. They are one
    /// assembly now, so the engine works in the library's own types; what is left here is the handful of
    /// operations it needs that the public types spell differently, or deliberately do not offer.
    /// </para>
    /// </summary>
    internal static class PlanarMath
    {
        /// <summary>
        /// The unit vector along a vector, or a vector of NaN when it has no length.
        /// </summary>
        /// <remarks>
        /// <see cref="GeoVector2.Normalize"/> throws on a zero-length vector, which is the right answer for a
        /// caller that can act on it. The engine cannot: it normalizes edge directions in bulk, and a
        /// degenerate edge has to travel through the arithmetic and be dropped later by the snapping and
        /// cleaning it feeds, exactly as it did before. Dividing through keeps that behaviour.
        /// </remarks>
        public static GeoVector2 Unit(this GeoVector2 vector)
        {
            double length = vector.Length;

            return new GeoVector2(vector.X / length, vector.Y / length);
        }

        /// <summary>
        /// The vector square to this one, a quarter turn counter-clockwise.
        /// </summary>
        public static GeoVector2 Left(this GeoVector2 vector) => new GeoVector2(-vector.Y, vector.X);

        /// <summary>
        /// The vector square to this one, a quarter turn clockwise.
        /// </summary>
        public static GeoVector2 Right(this GeoVector2 vector) => new GeoVector2(vector.Y, -vector.X);

        /// <summary>
        /// The dot product of two vectors.
        /// </summary>
        public static double Dot(this GeoVector2 vector, GeoVector2 other) => vector.DotProduct(other);

        /// <summary>
        /// The cross product of two vectors: positive when the second turns left of the first.
        /// </summary>
        public static double Cross(this GeoVector2 vector, GeoVector2 other) => vector.CrossProduct(other);

        /// <summary>
        /// The vector turned by an angle in radians, counter-clockwise.
        /// </summary>
        public static GeoVector2 Rotate(this GeoVector2 vector, double angle) => vector.RotateBy(angle);

        /// <summary>
        /// The point halfway between two points.
        /// </summary>
        public static GeoPoint2 Midpoint(GeoPoint2 a, GeoPoint2 b) => new GeoPoint2((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

        /// <summary>
        /// The square of the distance between two points, which compares like the distance without the root.
        /// </summary>
        public static double DistanceSquaredTo(this GeoPoint2 point, GeoPoint2 other) => point.GetDistanceSquaredTo(other);
    }
}
