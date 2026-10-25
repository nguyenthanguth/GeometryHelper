using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A box of the plane by its two corners, as AutoCAD's <c>Extents2d</c>: the limits of a layout, the margins of a sheet,
    /// the window of a plot. The corners are as the file gives them, not sorted: a box from (0, 0) to (420, 297) is a sheet of
    /// A3 laid on its side.
    /// </summary>
    public readonly struct DxfExtents2d : IEquatable<DxfExtents2d>
    {
        /// <summary>Initializes a box of its two corners, as AutoCAD's <c>Extents2d(min, max)</c>.</summary>
        /// <param name="min">The corner of the least x and y.</param>
        /// <param name="max">The corner of the greatest x and y.</param>
        public DxfExtents2d(GeoPoint2 min, GeoPoint2 max)
        {
            MinPoint = min;
            MaxPoint = max;
        }

        /// <summary>Gets the corner of the least x and y, as AutoCAD's <c>MinPoint</c>.</summary>
        public GeoPoint2 MinPoint { get; }

        /// <summary>Gets the corner of the greatest x and y, as AutoCAD's <c>MaxPoint</c>.</summary>
        public GeoPoint2 MaxPoint { get; }

        /// <summary>Gets whether two boxes have the same corners, to the bit.</summary>
        /// <param name="other">The other box.</param>
        /// <returns>Whether they have.</returns>
        public bool Equals(DxfExtents2d other)
            => Same(MinPoint.X, other.MinPoint.X) && Same(MinPoint.Y, other.MinPoint.Y) && Same(MaxPoint.X, other.MaxPoint.X) && Same(MaxPoint.Y, other.MaxPoint.Y);

        /// <summary>Gets whether an object is a box of the same corners, to the bit.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => obj is DxfExtents2d other && Equals(other);

        /// <summary>Gets a hash of the corners, the same for boxes of the same corners.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode()
            => (((BitConverter.DoubleToInt64Bits(MinPoint.X).GetHashCode() * 31) + BitConverter.DoubleToInt64Bits(MinPoint.Y).GetHashCode()) * 31
                + BitConverter.DoubleToInt64Bits(MaxPoint.X).GetHashCode()) * 31 + BitConverter.DoubleToInt64Bits(MaxPoint.Y).GetHashCode();

        /// <summary>Gives the box as a line for debugging: its two corners.</summary>
        /// <returns>The line, such as <c>(0, 0) to (420, 297)</c>.</returns>
        public override string ToString() => MinPoint + " to " + MaxPoint;

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
