using System;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// How DCL's units become pixels: widths are counted in character widths and heights in character heights, as DCL counts
    /// them. The defaults are AutoCAD 2021's, measured in its window (D337, D345): a character of 6 by 13, a button 22 high,
    /// an edit box 19, a popup list 21, a toggle 15, a radio button 19, a slider 17, a list line of 13. The window sets the
    /// text width of its dialog font. Every size the layout adds to the counted ones is here and nowhere else.
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

        /// <summary>Gets or sets the room a button adds to the width of its label, both sides together (8 each side).</summary>
        internal double ButtonPaddingX { get; set; } = 16;

        /// <summary>Gets or sets the height of a button.</summary>
        internal double ButtonHeight { get; set; } = 22;

        /// <summary>Gets or sets the height of an edit box.</summary>
        internal double EditHeight { get; set; } = 19;

        /// <summary>Gets or sets the height of a popup list.</summary>
        internal double PopupHeight { get; set; } = 21;

        /// <summary>Gets or sets the height of a toggle.</summary>
        internal double ToggleHeight { get; set; } = 15;

        /// <summary>Gets or sets the height of a radio button.</summary>
        internal double RadioHeight { get; set; } = 19;

        /// <summary>Gets or sets the width of the box of a toggle or a radio button with the gap before its label.</summary>
        internal double CheckBoxWidth { get; set; } = 20;

        /// <summary>
        /// Gets or sets the room an edit box or a popup list adds to its <c>edit_width</c> characters: its frame, so that
        /// <c>edit_width = 20</c> is a box of 132.
        /// </summary>
        internal double EditBoxPadding { get; set; } = 12;

        /// <summary>Gets or sets the gap between the label of an edit box or a popup list and the box.</summary>
        internal double LabelGap { get; set; } = 0;

        /// <summary>Gets or sets the height of one line of a list box.</summary>
        internal double ListLineHeight { get; set; } = 13;

        /// <summary>
        /// Gets or sets how much a list box's tile is short of its lines: its height is <c>height</c> lines less this, so that
        /// <c>height = 6</c> is a tile of 68 with its label inside it.
        /// </summary>
        internal double ListTrim { get; set; } = 10;

        /// <summary>Gets or sets the thickness of a slider across its direction.</summary>
        internal double SliderThickness { get; set; } = 17;

        /// <summary>
        /// Gets or sets the room between the frame of a boxed cluster and its children, at the sides and the bottom; above, the
        /// label's line of <see cref="CharHeight"/> and half of this are added.
        /// </summary>
        internal double BoxBorder { get; set; } = 8;

        /// <summary>Gets or sets the margin of the dialog, on each of its four sides.</summary>
        internal double DialogMargin { get; set; } = 3;

        /// <summary>
        /// Gets or sets the room on the left and the right of a tile that has no <c>horizontal_margin</c> of <c>none</c>, a
        /// spacer's too: two tiles side by side are twice this apart.
        /// </summary>
        internal double TileMarginX { get; set; } = 4;

        /// <summary>
        /// Gets or sets the room above and below a tile that has no <c>vertical_margin</c> of <c>none</c>: two tiles one over the
        /// other are twice this apart.
        /// </summary>
        internal double TileMarginY { get; set; } = 4;
    }
}
