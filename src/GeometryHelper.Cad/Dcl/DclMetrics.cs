using System;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// How DCL's units become pixels: widths are counted in character widths and heights in character heights, as DCL counts
    /// them. The window sets the numbers of its dialog font; the defaults are those of a dialog font of 8 points and are
    /// provisional until the design of the window fixes them. Every size the layout adds to the counted ones is here and
    /// nowhere else.
    /// </summary>
    internal sealed class DclMetrics
    {
        internal DclMetrics()
        {
            TextWidth = text => (text ?? string.Empty).Length * CharWidth;
        }

        /// <summary>Gets new metrics with the defaults, for a session with no window.</summary>
        internal static DclMetrics Default => new DclMetrics();

        /// <summary>Gets or sets the width of one character, in device-independent pixels.</summary>
        internal double CharWidth { get; set; } = 6;

        /// <summary>Gets or sets the height of one character, in device-independent pixels.</summary>
        internal double CharHeight { get; set; } = 13;

        /// <summary>Gets or sets how wide a text is in the dialog font; by default its character count times <see cref="CharWidth"/>.</summary>
        internal Func<string, double> TextWidth { get; set; }

        /// <summary>Gets or sets the room a button adds to the width of its label, both sides together.</summary>
        internal double ButtonPaddingX { get; set; } = 12;

        /// <summary>Gets or sets the height of a button, of an edit box and of a popup list, each one line.</summary>
        internal double ControlHeight { get; set; } = 23;

        /// <summary>Gets or sets the height of a toggle and of a radio button.</summary>
        internal double CheckHeight { get; set; } = 18;

        /// <summary>Gets or sets the width of the box of a toggle or a radio button with the gap before its label.</summary>
        internal double CheckBoxWidth { get; set; } = 20;

        /// <summary>Gets or sets the gap between the label of an edit box or a popup list and the box.</summary>
        internal double LabelGap { get; set; } = 6;

        /// <summary>Gets or sets the height of one line of a list box.</summary>
        internal double ListLineHeight { get; set; } = 15;

        /// <summary>Gets or sets the border of a list box, added above and below the lines and at each side.</summary>
        internal double ListBorder { get; set; } = 2;

        /// <summary>Gets or sets the thickness of a slider across its direction.</summary>
        internal double SliderThickness { get; set; } = 21;

        /// <summary>
        /// Gets or sets the room between the frame of a boxed cluster and its children, at the sides and the bottom; above, the
        /// label's line of <see cref="CharHeight"/> and half of this are added.
        /// </summary>
        internal double BoxBorder { get; set; } = 8;

        /// <summary>Gets or sets the margin of the dialog, on each of its four sides.</summary>
        internal double DialogMargin { get; set; } = 12;

        /// <summary>Gets or sets the room on the left and the right of a tile that has no <c>horizontal_margin</c> of <c>none</c>.</summary>
        internal double TileMarginX { get; set; } = 4;

        /// <summary>Gets or sets the room above and below a tile that has no <c>vertical_margin</c> of <c>none</c>.</summary>
        internal double TileMarginY { get; set; } = 3;
    }
}
