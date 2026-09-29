using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Packing
{
    /// <summary>
    /// Where one box goes: which sheet, how far it moves, and the box moved there.
    /// </summary>
    /// <remarks>
    /// Boxes are only moved, never turned or resized. The move is the same for every point of what the box stands for,
    /// so adding <see cref="Translation"/> to any point of it, the origin of a drawing view or the insertion point of
    /// a block, puts it where its box goes, as long as the box was given in the same coordinates as that point.
    /// </remarks>
    public readonly struct PackPlacement : IEquatable<PackPlacement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PackPlacement"/> struct.
        /// </summary>
        /// <param name="sheetIndex">Which sheet the box goes on, the first nought; -1 when it goes on none.</param>
        /// <param name="translation">How far the box moves.</param>
        /// <param name="viewBox">The box, moved.</param>
        /// <param name="placed">Whether the box found a place.</param>
        public PackPlacement(int sheetIndex, GeoVector2 translation, GeoRectangle2 viewBox, bool placed)
        {
            SheetIndex = sheetIndex;
            Translation = translation;
            ViewBox = viewBox;
            Placed = placed;
        }

        /// <summary>
        /// Gets which sheet the box goes on: nought for the first, one more for each sheet after it, as
        /// <see cref="Sheet.GetFrame(int)"/> counts them; -1 for a box that goes on none.
        /// </summary>
        public int SheetIndex { get; }

        /// <summary>
        /// Gets how far the box moves, in the units of the boxes, the sheets lying where <see cref="Sheet.Origin"/> and
        /// <see cref="Sheet.NewSheet"/> lay them; zero for a box that goes on none.
        /// </summary>
        public GeoVector2 Translation { get; }

        /// <summary>
        /// Gets the box moved by <see cref="Translation"/>, the same size and as turned as it was given; the box as
        /// given for one that goes on none.
        /// </summary>
        public GeoRectangle2 ViewBox { get; }

        /// <summary>
        /// Gets whether the box found a place: inside the usable area of its sheet and clear of every other box, but the
        /// boxes of its own group kept as they stood. False for a box larger than the usable area of a sheet, or not at
        /// finite coordinates.
        /// </summary>
        public bool Placed { get; }

        /// <summary>
        /// Indicates whether this placement is the same as another: the same sheet, move, box and verdict, exactly.
        /// </summary>
        /// <param name="other">The placement to compare with.</param>
        /// <returns>true if the two are the same; otherwise, false.</returns>
        public bool Equals(PackPlacement other)
            => SheetIndex == other.SheetIndex && Translation.Equals(other.Translation) && ViewBox.Equals(other.ViewBox) && Placed == other.Placed;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is PackPlacement other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (((((SheetIndex * 397) ^ Translation.GetHashCode()) * 397) ^ ViewBox.GetHashCode()) * 397) ^ Placed.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two placements for equality.
        /// </summary>
        /// <param name="left">The first placement.</param>
        /// <param name="right">The second placement.</param>
        /// <returns>true if they are the same; otherwise, false.</returns>
        public static bool operator ==(PackPlacement left, PackPlacement right) => left.Equals(right);

        /// <summary>
        /// Compares two placements for inequality.
        /// </summary>
        /// <param name="left">The first placement.</param>
        /// <param name="right">The second placement.</param>
        /// <returns>true if they differ; otherwise, false.</returns>
        public static bool operator !=(PackPlacement left, PackPlacement right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString()
        {
            return Placed
                ? string.Format(CultureInfo.InvariantCulture, "PackPlacement[sheet {0}, moved ({1:0.###}, {2:0.###})]", SheetIndex, Translation.X, Translation.Y)
                : "PackPlacement[not placed]";
        }
    }
}
