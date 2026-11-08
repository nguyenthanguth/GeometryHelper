namespace GeometryHelper.Drawing
{
    /// <summary>
    /// How a drawing is laid out in its DXF (D10): its views as viewports of a layout, as AutoCAD's MVIEW makes them, or the
    /// whole sheet drawn at 1:1 in model space.
    /// </summary>
    public enum DrawingExportMode
    {
        /// <summary>
        /// The default. The lines of each view at 1:1 in model space, each view in a place of its own, and the sheet a layout
        /// of paper space with one viewport per view at its scale: a beam 3 000 long in a view at 1:20 is 3 000 long in model
        /// space and 150 long on the sheet. Dimensions in model space read true lengths.
        /// </summary>
        LayoutAndViewports = 0,

        /// <summary>
        /// The sheet at 1:1 in model space, in paper millimetres, each view drawn at its scale where the sheet puts it: the beam
        /// above is 150 long in model space, and its dimensions read 3000 through a dimension style whose DIMLFAC is 20. For
        /// viewers that show model space only.
        /// </summary>
        SheetInModelSpace = 1,
    }
}
