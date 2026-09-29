using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Packing
{
    /// <summary>
    /// One sheet as it lies: which it is, the whole of it, and the part inside its offsets that boxes are packed into.
    /// </summary>
    /// <remarks>
    /// Both are rectangles in the units of the boxes, so every corner is there to be read:
    /// <c>frame.Bounds.LowerLeft</c>, <c>frame.Bounds.UpperRight</c>, <c>frame.UsableArea.Center</c> and the rest.
    /// </remarks>
    public readonly struct SheetFrame : IEquatable<SheetFrame>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SheetFrame"/> struct.
        /// </summary>
        /// <param name="index">Which sheet it is, the first nought.</param>
        /// <param name="bounds">The whole sheet.</param>
        /// <param name="usableArea">The part of the sheet inside its offsets.</param>
        public SheetFrame(int index, GeoRectangle2 bounds, GeoRectangle2 usableArea)
        {
            Index = index;
            Bounds = bounds;
            UsableArea = usableArea;
        }

        /// <summary>
        /// Gets which sheet it is: nought for the first, the one <see cref="Sheet.Origin"/> places, and one more for
        /// each sheet after it.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the whole sheet: its paper size times its scale, its lower left corner where the sheet lies.
        /// </summary>
        public GeoRectangle2 Bounds { get; }

        /// <summary>
        /// Gets the part of the sheet inside its four offsets, which the boxes are packed into.
        /// </summary>
        public GeoRectangle2 UsableArea { get; }

        /// <summary>
        /// Indicates whether this frame is the same as another: the same sheet, exactly where it is.
        /// </summary>
        /// <param name="other">The frame to compare with.</param>
        /// <returns>true if the two are the same; otherwise, false.</returns>
        public bool Equals(SheetFrame other) => Index == other.Index && Bounds.Equals(other.Bounds) && UsableArea.Equals(other.UsableArea);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SheetFrame other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (((Index * 397) ^ Bounds.GetHashCode()) * 397) ^ UsableArea.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two frames for equality.
        /// </summary>
        /// <param name="left">The first frame.</param>
        /// <param name="right">The second frame.</param>
        /// <returns>true if they are the same; otherwise, false.</returns>
        public static bool operator ==(SheetFrame left, SheetFrame right) => left.Equals(right);

        /// <summary>
        /// Compares two frames for inequality.
        /// </summary>
        /// <param name="left">The first frame.</param>
        /// <param name="right">The second frame.</param>
        /// <returns>true if they differ; otherwise, false.</returns>
        public static bool operator !=(SheetFrame left, SheetFrame right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString()
        {
            GeoPoint2 corner = Bounds.LowerLeft;
            return string.Format(CultureInfo.InvariantCulture, "SheetFrame[{0}, at ({1:0.###}, {2:0.###}), {3:0.###} x {4:0.###}]",
                Index, corner.X, corner.Y, Bounds.Width, Bounds.Height);
        }
    }
}
