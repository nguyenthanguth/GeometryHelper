using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// What became of one label: how far it moves, and whether where it ends up is clear.
    /// </summary>
    public readonly struct ArrangeResult : IEquatable<ArrangeResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArrangeResult"/> struct.
        /// </summary>
        /// <param name="translation">How far the label moves.</param>
        /// <param name="placed">Whether the label, moved, overlaps nothing.</param>
        public ArrangeResult(GeoVector2 translation, bool placed)
        {
            Translation = translation;
            Placed = placed;
        }

        /// <summary>
        /// Gets how far the label moves: translate its box by this. Zero when it stays where it is.
        /// </summary>
        public GeoVector2 Translation { get; }

        /// <summary>
        /// Gets whether the label, moved, overlaps nothing: no other label, and nothing any label keeps clear of.
        /// </summary>
        /// <remarks>
        /// False for a label that could not be arranged at all, its box smaller than
        /// <see cref="ArrangeOptions.MinimumBoxSize"/> or its leader of no length, which stays where it is; and for one
        /// that found no clear place, which is left where it overlaps something. It is judged on the final layout as a
        /// whole, so a label that another one fell back onto is not reported clear.
        /// </remarks>
        public bool Placed { get; }

        /// <summary>
        /// Indicates whether this result is the same as another: the same translation, exactly, and the same verdict.
        /// </summary>
        /// <param name="other">The result to compare with.</param>
        /// <returns>true if the two are the same; otherwise, false.</returns>
        public bool Equals(ArrangeResult other) => Translation.Equals(other.Translation) && Placed == other.Placed;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ArrangeResult other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Translation.GetHashCode() * 397) ^ Placed.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two results for equality.
        /// </summary>
        /// <param name="left">The first result.</param>
        /// <param name="right">The second result.</param>
        /// <returns>true if they are the same; otherwise, false.</returns>
        public static bool operator ==(ArrangeResult left, ArrangeResult right) => left.Equals(right);

        /// <summary>
        /// Compares two results for inequality.
        /// </summary>
        /// <param name="left">The first result.</param>
        /// <param name="right">The second result.</param>
        /// <returns>true if they differ; otherwise, false.</returns>
        public static bool operator !=(ArrangeResult left, ArrangeResult right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "ArrangeResult[{0}, moved ({1:0.###}, {2:0.###})]",
                Placed ? "placed" : "not placed", Translation.X, Translation.Y);
        }
    }
}
