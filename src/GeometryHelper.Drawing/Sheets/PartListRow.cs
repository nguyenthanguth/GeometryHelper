using System;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// One row of a drawing's part list: the parts of one mark, profile and material, how many there are and how long.
    /// </summary>
    /// <remarks>
    /// Four stiffeners S1 of PL10 in S355, 200 long, are one row: S1, 4, PL10, S355, 200, STIFFENER. The length is the
    /// part's reach along its own X, rounded to the millimetre; parts of one mark but of different lengths are rows of their own.
    /// </remarks>
    public sealed class PartListRow
    {
        internal PartListRow(string mark, int quantity, string profile, string material, double length, string name)
        {
            Mark = mark;
            Quantity = quantity;
            Profile = profile;
            Material = material;
            Length = length;
            Name = name;
        }

        /// <summary>Gets the position mark.</summary>
        public string Mark { get; }

        /// <summary>Gets how many parts the row stands for.</summary>
        public int Quantity { get; }

        /// <summary>Gets the profile.</summary>
        public string Profile { get; }

        /// <summary>Gets the material grade.</summary>
        public string Material { get; }

        /// <summary>Gets the length of each part, in millimetres, rounded to the millimetre: its reach along its own X.</summary>
        public double Length { get; }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"{Mark} {Quantity} {Profile} {Material} {Length} {Name}");
    }
}
