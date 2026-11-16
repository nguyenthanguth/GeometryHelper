namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What a cell style gives the cells that take it, as <see cref="DxfTableStyle.GetCellStyleLook"/> reads it: where the
    /// content stands, the height, style and colour of its text, the fill of the background and the six lines (top, inside
    /// across, bottom, left, inside down, right) each with its colour, lineweight and whether it is drawn.
    /// </summary>
    public sealed class DxfCellStyleLook
    {
        /// <summary>The six lines of a cell style, in the order of the arrays: top, inside across, bottom, left, inside down, right.</summary>
        public static readonly DxfGridLineType[] Lines =
        {
            DxfGridLineType.HorizontalTop,
            DxfGridLineType.HorizontalInside,
            DxfGridLineType.HorizontalBottom,
            DxfGridLineType.VerticalLeft,
            DxfGridLineType.VerticalInside,
            DxfGridLineType.VerticalRight,
        };

        /// <summary>Gets or sets where the content stands.</summary>
        public DxfCellAlignment Alignment { get; set; } = DxfCellAlignment.MiddleCenter;

        /// <summary>Gets or sets the height of the text; nought where the style gives none.</summary>
        public double TextHeight { get; set; }

        /// <summary>Gets or sets the name of the text style of the text; null where the style names none the drawing has.</summary>
        public string TextStyleName { get; set; }

        /// <summary>Gets or sets the colour of the content; null where the style gives none.</summary>
        public DxfColor ContentColor { get; set; }

        /// <summary>Gets or sets the colour of the background; null where the style leaves it unfilled.</summary>
        public DxfColor BackgroundColor { get; set; }

        /// <summary>Gets the colour of each of the six lines; null where the style gives none.</summary>
        public DxfColor[] LineColors { get; } = new DxfColor[6];

        /// <summary>Gets the lineweight of each of the six lines; null where the style gives none.</summary>
        public DxfLineWeight?[] LineWeights { get; } = new DxfLineWeight?[6];

        /// <summary>Gets whether each of the six lines is drawn; null where the style says nothing.</summary>
        public bool?[] LineShown { get; } = new bool?[6];
    }
}
