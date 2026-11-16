using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The background mask of an MTEXT, as AutoCAD 2021's Background Mask dialog sets it: whether the words stand on a
    /// filled box, how far the box reaches past them (the border offset factor, a multiple of the text height from 1 to 5,
    /// 1.5 by default), and its colour, the drawing's background colour or one of its own. An MTEXT 5 high with the mask on
    /// at the factor 1.5 stands on a box 2.5 past its words on every side. It is written as the flags of group 90 (1 the mask,
    /// 2 the drawing's background colour), 45 the factor and 63, 421 the colour.
    /// </summary>
    public sealed class CadMTextMask : IEquatable<CadMTextMask>
    {
        /// <summary>The factor AutoCAD gives a mask the dialog has not set: 1.5.</summary>
        public const double DefaultOffsetFactor = 1.5;

        /// <summary>Makes a mask.</summary>
        /// <param name="useMask">Whether the words stand on a mask.</param>
        /// <param name="offsetFactor">How far the mask reaches past the words, a multiple of the text height from 1 to 5.</param>
        /// <param name="useDrawingBackground">Whether the mask takes the drawing's background colour, rather than <paramref name="fillColor"/>.</param>
        /// <param name="fillColor">The colour of the mask, an index or a true colour; null for the colour the record gives none, by layer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor is not a number from 1 to 5.</exception>
        public CadMTextMask(bool useMask, double offsetFactor, bool useDrawingBackground, DxfColor fillColor)
        {
            if (!(offsetFactor >= 1.0 && offsetFactor <= 5.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offsetFactor),
                    offsetFactor,
                    "A border offset factor runs from 1 to 5, as AutoCAD takes it."
                );
            }

            UseMask = useMask;
            OffsetFactor = offsetFactor;
            UseDrawingBackground = useDrawingBackground;
            FillColor = fillColor;
        }

        /// <summary>Gets whether the words stand on a mask.</summary>
        public bool UseMask { get; }

        /// <summary>Gets how far the mask reaches past the words, a multiple of the text height.</summary>
        public double OffsetFactor { get; }

        /// <summary>Gets whether the mask takes the drawing's background colour.</summary>
        public bool UseDrawingBackground { get; }

        /// <summary>Gets the colour of the mask; null where the MTEXT gives none (by layer).</summary>
        public DxfColor FillColor { get; }

        /// <summary>Reads the mask an MTEXT has: none for one with no background fill.</summary>
        /// <param name="text">The MTEXT.</param>
        /// <returns>The mask.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the MTEXT is null.</exception>
        public static CadMTextMask Of(DxfMText text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            double factor = text.BackgroundScaleFactor;
            return new CadMTextMask(
                text.BackgroundFill,
                factor >= 1.0 && factor <= 5.0 ? factor : DefaultOffsetFactor,
                text.UseBackgroundColor,
                text.BackgroundFillColor
            );
        }

        /// <summary>
        /// Gives an MTEXT this mask: the fill and the drawing's background colour switched as it says, the factor and, where
        /// the mask takes a colour of its own, the colour written; an MTEXT with the mask off is left with the record it
        /// has but for the two flags. What the MTEXT already has is not written again.
        /// </summary>
        /// <param name="text">The MTEXT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the MTEXT is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is in a file older than R2004, which has no background.</exception>
        public void ApplyTo(DxfMText text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (Equals(Of(text)))
            {
                return;
            }

            if (!UseMask)
            {
                text.BackgroundFill = false;
                text.UseBackgroundColor = false;
                return;
            }

            text.BackgroundFill = true;
            text.UseBackgroundColor = UseDrawingBackground;
            text.BackgroundScaleFactor = OffsetFactor;

            if (!UseDrawingBackground && FillColor != null)
            {
                text.BackgroundFillColor = FillColor;
            }
        }

        /// <inheritdoc/>
        public bool Equals(CadMTextMask other) =>
            other != null
            && UseMask == other.UseMask
            && Math.Abs(OffsetFactor - other.OffsetFactor) <= 1E-9
            && UseDrawingBackground == other.UseDrawingBackground
            && Equals(FillColor, other.FillColor);

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CadMTextMask);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            (UseMask ? 1 : 0) + (UseDrawingBackground ? 2 : 0) + (int)(OffsetFactor * 1000.0) * 4;
    }
}
