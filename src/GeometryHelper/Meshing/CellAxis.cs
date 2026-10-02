using System;
using System.Globalization;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// How one axis of a grid of cells is divided: by a size, by a count, or not at all.
    /// <para>
    /// A body cut into cells is divided along each of its grid's three axes on its own, so a slab can be cut into bays along
    /// X and Y and left whole through its thickness, and a wall into lifts up Z and left whole along its length. The value
    /// is immutable; <see cref="Whole"/>, the default, leaves the axis whole.
    /// </para>
    /// </summary>
    public readonly struct CellAxis : IEquatable<CellAxis>
    {
        private CellAxis(double size, int count, GridAlignment alignment)
        {
            Size = size;
            Count = count;
            Alignment = alignment;
        }

        /// <summary>
        /// Not divided: one cell along the axis, from the body's first side to its far one.
        /// </summary>
        public static CellAxis Whole => default;

        /// <summary>
        /// Cells of a size along the axis, a joint apart, standing as the alignment says, or with one starting at the
        /// placement's origin when it gives one.
        /// </summary>
        /// <param name="size">The size of a cell along the axis.</param>
        /// <param name="alignment">Where the cells stand against the body, measured between its furthest corners along the axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the size is not a finite number above nought, or the alignment is not one of <see cref="GridAlignment"/>.
        /// </exception>
        public static CellAxis BySize(double size, GridAlignment alignment = GridAlignment.Start)
        {
            Guard.Positive(size, nameof(size), "A cell has to have a size: a finite number above nought.");

            if (alignment != GridAlignment.Start && alignment != GridAlignment.End && alignment != GridAlignment.CenterCell && alignment != GridAlignment.CenterJoint)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown grid alignment.");
            }

            return new CellAxis(size, 0, alignment);
        }

        /// <summary>
        /// As many equal cells along the axis as asked, a joint apart, from the body's first side to its far one.
        /// </summary>
        /// <param name="count">How many cells; one leaves the axis whole.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is below one.</exception>
        public static CellAxis ByCount(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "An axis divides into one cell at the least.");
            }

            return new CellAxis(0.0, count, GridAlignment.Start);
        }

        /// <summary>
        /// Gets the size of a cell, for an axis divided by size; nought otherwise.
        /// </summary>
        public double Size { get; }

        /// <summary>
        /// Gets how many cells, for an axis divided by count; nought otherwise.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets where the cells stand, for an axis divided by size when the placement gives no origin.
        /// </summary>
        public GridAlignment Alignment { get; }

        /// <summary>
        /// Gets whether the axis is left whole, as <see cref="Whole"/> leaves it and <see cref="ByCount"/> of one does.
        /// </summary>
        public bool IsWhole => Size == 0.0 && Count <= 1;

        /// <summary>
        /// Determines whether another axis is divided the same way.
        /// </summary>
        public bool Equals(CellAxis other) => Size.Equals(other.Size) && Count == other.Count && Alignment == other.Alignment;

        /// <summary>
        /// Determines whether the specified object is an axis divided the same way.
        /// </summary>
        public override bool Equals(object obj) => obj is CellAxis other && Equals(other);

        /// <summary>
        /// Returns the hash code for this axis.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Size.GetHashCode() * 397 ^ Count) * 397 ^ (int)Alignment;
            }
        }

        /// <summary>
        /// Determines whether two axes are divided the same way.
        /// </summary>
        public static bool operator ==(CellAxis left, CellAxis right) => left.Equals(right);

        /// <summary>
        /// Determines whether two axes are divided differently.
        /// </summary>
        public static bool operator !=(CellAxis left, CellAxis right) => !left.Equals(right);

        /// <summary>
        /// Describes the axis.
        /// </summary>
        public override string ToString()
        {
            if (Size > 0.0)
            {
                return string.Format(CultureInfo.InvariantCulture, "Size {0} {1}", Size, Alignment);
            }

            return Count > 1 ? string.Format(CultureInfo.InvariantCulture, "Count {0}", Count) : "Whole";
        }
    }
}
