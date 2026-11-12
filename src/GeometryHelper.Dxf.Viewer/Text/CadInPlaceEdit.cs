using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a text edited in place is.</summary>
    public enum CadInPlaceKind
    {
        /// <summary>A single-line text (TEXT, an attribute definition, a dimension's text override): one line in an edit box.</summary>
        Text,

        /// <summary>An MTEXT, edited in the MTEXT editor with the Text Editor tab.</summary>
        MText,
    }

    /// <summary>
    /// A text a command asks to be edited in place on the canvas (<see cref="CadPrompt.InPlace"/>): what it is, the words as
    /// written, and where and how large it stands, so that the window can put its editor over it in the text's own place,
    /// height and angle.
    /// </summary>
    public sealed class CadInPlaceEdit
    {
        /// <summary>Makes the description of a text edited in place.</summary>
        /// <param name="kind">What it is.</param>
        /// <param name="handle">The handle of the entity edited; null for an MTEXT being made by MTEXT.</param>
        /// <param name="value">The words as written: a TEXT's value, an MTEXT's words with their codes.</param>
        /// <param name="location">Where the text stands: a TEXT's insertion point, an MTEXT's attachment point.</param>
        /// <param name="height">The height of its letters.</param>
        /// <param name="rotation">The angle of its baseline in radians.</param>
        /// <param name="width">The width of an MTEXT's box; nought for none or a TEXT.</param>
        /// <param name="styleName">The text style.</param>
        /// <param name="attachment">How an MTEXT is attached at its location, 1 top left to 9 bottom right; 1 for a TEXT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public CadInPlaceEdit(
            CadInPlaceKind kind,
            string handle,
            string value,
            GeoPoint2 location,
            double height,
            double rotation,
            double width,
            string styleName,
            int attachment
        )
        {
            Kind = kind;
            Handle = handle;
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Location = location;
            Height = height;
            Rotation = rotation;
            Width = width;
            StyleName = styleName;
            Attachment = attachment >= 1 && attachment <= 9 ? attachment : 1;
        }

        /// <summary>Gets what the text is.</summary>
        public CadInPlaceKind Kind { get; }

        /// <summary>Gets the handle of the entity edited; null for an MTEXT being made.</summary>
        public string Handle { get; }

        /// <summary>Gets the words as written.</summary>
        public string Value { get; }

        /// <summary>Gets where the text stands.</summary>
        public GeoPoint2 Location { get; }

        /// <summary>Gets the height of its letters.</summary>
        public double Height { get; }

        /// <summary>Gets the angle of its baseline in radians.</summary>
        public double Rotation { get; }

        /// <summary>Gets the width of an MTEXT's box; nought for none.</summary>
        public double Width { get; }

        /// <summary>Gets the text style.</summary>
        public string StyleName { get; }

        /// <summary>Gets how an MTEXT is attached at its location, 1 top left to 9 bottom right.</summary>
        public int Attachment { get; }

        /// <summary>
        /// Gets or sets the width an MTEXT's frame was dragged to in the editor, which the command writes with the words; null
        /// where it was not dragged. The window sets it before it answers the prompt.
        /// </summary>
        public double? NewWidth { get; set; }

        /// <summary>Gets or sets the attachment an MTEXT was justified to in the editor (Justification, 1 top left to 9 bottom right); null where it was not changed.</summary>
        public int? NewAttachment { get; set; }

        /// <summary>Gets or sets the line spacing factor an MTEXT was given in the editor (Line Spacing, 1 single); null where it was not changed.</summary>
        public double? NewLineSpacing { get; set; }

        /// <summary>Gets whether the editor changed the box: its width, attachment, line spacing, defined height or columns.</summary>
        public bool ChangesBox =>
            (NewWidth.HasValue && NewWidth.Value >= 0.0 && NewWidth.Value != Width)
            || NewAttachment.HasValue
            || NewLineSpacing.HasValue
            || NewFrame != null;

        /// <summary>
        /// Gets or sets the box an MTEXT was given in the editor where its defined height or columns changed (the ruler's
        /// height grips, Set Mtext Height, Column Settings), which the command writes with the words; null where they did not.
        /// </summary>
        public CadMTextFrame NewFrame { get; set; }

        /// <summary>
        /// Gets the box an MTEXT being made opens in the editor with, where MTEXT's Columns option gave it columns: two static
        /// columns 93.75 wide with a gutter of 12.5, 200 in all and 25 high, for an MTEXT 2.5 high given the Static defaults; null where the
        /// editor opens with <see cref="Width"/> alone. What the editor makes of it is <see cref="NewFrame"/>.
        /// </summary>
        public CadMTextFrame Frame { get; internal set; }

        /// <summary>Gets the row of the table cell edited, from nought, where <see cref="Handle"/> is a table's; -1 for any other text.</summary>
        public int TableRow { get; internal set; } = -1;

        /// <summary>Gets the column of the table cell edited, from nought; -1 for any other text.</summary>
        public int TableColumn { get; internal set; } = -1;

        /// <summary>Gets whether a table cell is edited.</summary>
        public bool IsTableCell => TableRow >= 0 && TableColumn >= 0;

        /// <summary>
        /// Gets or sets where the editor of a table cell goes once the words are written, as AutoCAD's keys move it: Tab to the
        /// next cell, Shift+Tab to the one before, Enter to the cell below; none where the edit ends. The window sets it before
        /// it answers the prompt.
        /// </summary>
        public CadTableCellMove Move { get; set; }
    }

    /// <summary>Where the editor of a table cell goes after the words are written.</summary>
    public enum CadTableCellMove
    {
        /// <summary>Nowhere: the edit ends.</summary>
        None,

        /// <summary>The next cell along the row, then the first of the next row (Tab).</summary>
        Next,

        /// <summary>The cell before, then the last of the row above (Shift+Tab).</summary>
        Previous,

        /// <summary>The cell below (Enter).</summary>
        Down,
    }
}
