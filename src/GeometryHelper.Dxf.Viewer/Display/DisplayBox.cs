using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>An axis-aligned box in drawing coordinates, as the extents of a display item or of a whole space.</summary>
    public readonly struct DisplayBox
    {
        /// <summary>Gets the box that holds nothing: its minimum is above its maximum, so the first point added makes it that point.</summary>
        public static DisplayBox Empty =>
            new DisplayBox(
                double.PositiveInfinity,
                double.PositiveInfinity,
                double.NegativeInfinity,
                double.NegativeInfinity
            );

        /// <summary>Makes a box from its corners.</summary>
        /// <param name="minX">The least X.</param>
        /// <param name="minY">The least Y.</param>
        /// <param name="maxX">The greatest X.</param>
        /// <param name="maxY">The greatest Y.</param>
        public DisplayBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>Gets the least X.</summary>
        public double MinX { get; }

        /// <summary>Gets the least Y.</summary>
        public double MinY { get; }

        /// <summary>Gets the greatest X.</summary>
        public double MaxX { get; }

        /// <summary>Gets the greatest Y.</summary>
        public double MaxY { get; }

        /// <summary>Gets whether the box holds nothing.</summary>
        public bool IsEmpty => MinX > MaxX || MinY > MaxY;

        /// <summary>Gets the width, nought for an empty box.</summary>
        public double Width => IsEmpty ? 0.0 : MaxX - MinX;

        /// <summary>Gets the height, nought for an empty box.</summary>
        public double Height => IsEmpty ? 0.0 : MaxY - MinY;

        /// <summary>Gets the box grown to hold a point.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>The grown box.</returns>
        public DisplayBox Add(double x, double y) =>
            new DisplayBox(Math.Min(MinX, x), Math.Min(MinY, y), Math.Max(MaxX, x), Math.Max(MaxY, y));

        /// <summary>Gets the box grown to hold another box.</summary>
        /// <param name="other">The other box; an empty one changes nothing.</param>
        /// <returns>The union.</returns>
        public DisplayBox Union(DisplayBox other)
        {
            if (other.IsEmpty)
            {
                return this;
            }

            return IsEmpty
                ? other
                : new DisplayBox(
                    Math.Min(MinX, other.MinX),
                    Math.Min(MinY, other.MinY),
                    Math.Max(MaxX, other.MaxX),
                    Math.Max(MaxY, other.MaxY)
                );
        }

        /// <summary>Gets the box grown by a margin on every side.</summary>
        /// <param name="margin">The margin.</param>
        /// <returns>The grown box.</returns>
        public DisplayBox Inflate(double margin) =>
            IsEmpty ? this : new DisplayBox(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);

        /// <summary>Gets whether this box and another share a point.</summary>
        /// <param name="other">The other box.</param>
        /// <returns>True when they touch or overlap.</returns>
        public bool Intersects(DisplayBox other) =>
            !IsEmpty
            && !other.IsEmpty
            && MinX <= other.MaxX
            && MaxX >= other.MinX
            && MinY <= other.MaxY
            && MaxY >= other.MinY;

        /// <summary>Gets whether this box holds another wholly.</summary>
        /// <param name="other">The other box.</param>
        /// <returns>True when the other lies inside.</returns>
        public bool Contains(DisplayBox other) =>
            !IsEmpty
            && !other.IsEmpty
            && MinX <= other.MinX
            && MaxX >= other.MaxX
            && MinY <= other.MinY
            && MaxY >= other.MaxY;

        /// <summary>Gets whether this box holds a point.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>True when the point is inside or on the edge.</returns>
        public bool Contains(double x, double y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }
}
