using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A scale of a plot as a ratio, as AutoCAD's <c>CustomScale</c>: so many units of the paper to so many of the drawing,
    /// 1 to 50 for a plan of millimetres drawn at 1:50.
    /// </summary>
    public readonly struct DxfCustomScale : IEquatable<DxfCustomScale>
    {
        /// <summary>Initializes a scale, as AutoCAD's <c>CustomScale(numerator, denominator)</c>.</summary>
        /// <param name="numerator">The units of the paper (group 142).</param>
        /// <param name="denominator">The units of the drawing they stand for (group 143).</param>
        public DxfCustomScale(double numerator, double denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        /// <summary>Gets the units of the paper, as AutoCAD's <c>Numerator</c>.</summary>
        public double Numerator { get; }

        /// <summary>Gets the units of the drawing they stand for, as AutoCAD's <c>Denominator</c>.</summary>
        public double Denominator { get; }

        /// <summary>Gets whether two scales have the same numbers, to the bit.</summary>
        /// <param name="other">The other scale.</param>
        /// <returns>Whether they have.</returns>
        public bool Equals(DxfCustomScale other)
            => BitConverter.DoubleToInt64Bits(Numerator) == BitConverter.DoubleToInt64Bits(other.Numerator) && BitConverter.DoubleToInt64Bits(Denominator) == BitConverter.DoubleToInt64Bits(other.Denominator);

        /// <summary>Gets whether an object is a scale of the same numbers, to the bit.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => obj is DxfCustomScale other && Equals(other);

        /// <summary>Gets a hash of the numbers, the same for scales of the same numbers.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => BitConverter.DoubleToInt64Bits(Numerator).GetHashCode() * 31 + BitConverter.DoubleToInt64Bits(Denominator).GetHashCode();

        /// <summary>Gives the scale as a line for debugging.</summary>
        /// <returns>The line, such as <c>1:50</c>.</returns>
        public override string ToString() => Numerator.ToString("R", CultureInfo.InvariantCulture) + ":" + Denominator.ToString("R", CultureInfo.InvariantCulture);
    }
}
