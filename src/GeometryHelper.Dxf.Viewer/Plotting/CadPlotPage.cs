using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A plot laid on its sheet: the sheet as it lies, the printable area inside its margins, the part of the space plotted,
    /// the scale and where the plot sits, and the transform from the drawing to the sheet in millimetres, the sheet's lower
    /// left corner at (0, 0) and its Y upward. A PDF page, the preview and a printer draw through it alike.
    /// </summary>
    /// <remarks>
    /// The window from (120, 60) to (7 620, 5 360) of model space on ISO A3 on its side, margins 5, at 1:20 and centred: the
    /// sheet 420 by 297, the printable area (5, 5) to (415, 292), the plot 375 by 265 moved (17.5, 11) from the printable
    /// area's corner, so (120, 60) lands at (22.5, 16) and (7 620, 5 360) at (397.5, 281). Fitted, the same window is
    /// 1 mm to 18.4669 units (410 / 7 500 against 287 / 5 300, the smaller), 406.13 by 287 moved (1.93, 0). Turned upside down,
    /// the sheet is turned half round about its middle after the plot is laid: (120, 60) at (397.5, 281).
    /// </remarks>
    public sealed class CadPlotPage
    {
        /// <summary>The points of a PDF in a millimetre: 72 to the inch of 25.4 mm.</summary>
        public const double PointsPerMillimetre = 72.0 / 25.4;

        private CadPlotPage(
            double width,
            double height,
            DisplayBox printable,
            DisplayBox area,
            double scale,
            double offsetX,
            double offsetY,
            Affine2 drawingToSheet
        )
        {
            SheetWidth = width;
            SheetHeight = height;
            Printable = printable;
            Area = area;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            DrawingToSheet = drawingToSheet;
        }

        /// <summary>Gets the width of the sheet as it lies, in millimetres: 420 for ISO A3 on its side, 297 upright.</summary>
        public double SheetWidth { get; }

        /// <summary>Gets the height of the sheet as it lies, in millimetres.</summary>
        public double SheetHeight { get; }

        /// <summary>Gets the width of the sheet in the points of a PDF: 1 190.55 for ISO A3 on its side.</summary>
        public double SheetWidthPoints => SheetWidth * PointsPerMillimetre;

        /// <summary>Gets the height of the sheet in the points of a PDF: 841.89 for ISO A3 on its side.</summary>
        public double SheetHeightPoints => SheetHeight * PointsPerMillimetre;

        /// <summary>Gets the printable area on the sheet, in millimetres from its lower left corner: (5, 5) to (415, 292) for ISO A3 on its side.</summary>
        public DisplayBox Printable { get; }

        /// <summary>Gets the part of the space plotted, in drawing units; empty where there is nothing to plot.</summary>
        public DisplayBox Area { get; }

        /// <summary>Gets the scale, millimetres of the sheet per drawing unit: 0.05 at 1:20.</summary>
        public double Scale { get; }

        /// <summary>Gets how far right of the printable area's corner the plot lies, in millimetres, before it is turned upside down.</summary>
        public double OffsetX { get; }

        /// <summary>Gets how far up of the printable area's corner the plot lies, in millimetres.</summary>
        public double OffsetY { get; }

        /// <summary>Gets the transform from the drawing to the sheet, in millimetres from its lower left corner, Y upward.</summary>
        public Affine2 DrawingToSheet { get; }

        /// <summary>Gets whether there is nothing to plot: an area of no width and no height, or none.</summary>
        public bool IsEmpty => Area.IsEmpty || !(Scale > 0.0);

        /// <summary>
        /// Gets the transform from the drawing to the points of a PDF page, its Y downward from the top as a canvas draws: the
        /// sheet's transform then millimetres to points, turned over.
        /// </summary>
        public Affine2 DrawingToPoints =>
            DrawingToSheet.Then(
                new Affine2(PointsPerMillimetre, 0.0, 0.0, -PointsPerMillimetre, 0.0, SheetHeight * PointsPerMillimetre)
            );

        /// <summary>
        /// Lays a plot of a space on its sheet, as AutoCAD's plot places it. The sheet lies on its side where the settings are
        /// landscape, its margins turning with it; the area is what the view shows (<paramref name="display"/>), the extents
        /// of the space's display list, the limits (LIMMIN and LIMMAX of model space, a layout's own), the window, or a layout's
        /// sheet. Fitted, the scale is the larger that keeps the area in the printable area; else the settings' own. The plot
        /// is centred in the printable area where asked, else moved by the offset from its corner; a layout's sheet plotted as
        /// the layout is placed by its origin, the paper's (0, 0) at the printable corner plus the offset. Upside down turns the
        /// sheet half round about its middle last.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted.</param>
        /// <param name="display">What the view shows, in drawing units, for <see cref="CadPlotArea.Display"/>; the extents are taken where it is empty.</param>
        /// <returns>The plot laid on its sheet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the space or the settings are null.</exception>
        public static CadPlotPage Compute(
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            CadPaper paper = settings.Paper;
            bool wide = paper.Width >= paper.Height;
            bool turned = settings.Landscape != wide;
            double width = turned ? paper.Height : paper.Width;
            double height = turned ? paper.Width : paper.Height;

            // A sheet turned a quarter has its margins turn with it, as the layout's paper is drawn: its bottom margin at the left.
            double left = turned ? paper.MarginBottom : paper.MarginLeft;
            double bottom = turned ? paper.MarginLeft : paper.MarginBottom;
            double right = turned ? paper.MarginTop : paper.MarginRight;
            double top = turned ? paper.MarginRight : paper.MarginTop;
            var printable = new DisplayBox(left, bottom, width - right, height - top);
            bool layout = settings.Area == CadPlotArea.Layout && !space.IsModel;
            DisplayBox area = AreaOf(document, space, settings, display);
            double scale = settings.Scale;

            if (settings.FitToPaper)
            {
                double byWidth = area.Width > 0.0 ? printable.Width / area.Width : double.PositiveInfinity;
                double byHeight = area.Height > 0.0 ? printable.Height / area.Height : double.PositiveInfinity;
                scale = Math.Min(byWidth, byHeight);
                scale = double.IsInfinity(scale) ? 0.0 : scale;
            }

            double offsetX;
            double offsetY;
            double e;
            double f;

            if (layout && !settings.FitToPaper)
            {
                // The paper's origin at the printable area's corner, moved by the offset, as AutoCAD plots a layout.
                offsetX = settings.OffsetX;
                offsetY = settings.OffsetY;
                e = printable.MinX + offsetX;
                f = printable.MinY + offsetY;
            }
            else
            {
                bool centred = settings.Centered || (layout && settings.FitToPaper);
                offsetX = centred ? (printable.Width - area.Width * scale) / 2.0 : settings.OffsetX;
                offsetY = centred ? (printable.Height - area.Height * scale) / 2.0 : settings.OffsetY;
                double minX = area.IsEmpty ? 0.0 : area.MinX;
                double minY = area.IsEmpty ? 0.0 : area.MinY;
                e = printable.MinX + offsetX - minX * scale;
                f = printable.MinY + offsetY - minY * scale;
            }

            Affine2 toSheet = settings.UpsideDown
                ? new Affine2(-scale, 0.0, 0.0, -scale, width - e, height - f)
                : new Affine2(scale, 0.0, 0.0, scale, e, f);
            return new CadPlotPage(width, height, printable, area, scale, offsetX, offsetY, toSheet);
        }

        /// <summary>The part of a space a plot takes, in drawing units, its corners in order.</summary>
        private static DisplayBox AreaOf(
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display
        )
        {
            DisplayBox extents = document.GetDisplay(space).Bounds;

            switch (settings.Area)
            {
                case CadPlotArea.Display:
                    return display.IsEmpty ? extents : display;
                case CadPlotArea.Window:
                    return settings.Window.IsEmpty ? extents : settings.Window;
                case CadPlotArea.Layout:
                    return space.IsModel ? extents : space.PaperBounds;
                case CadPlotArea.Limits:
                    if (space.IsModel)
                    {
                        GeoPoint2 min = document.Database.Limmin;
                        GeoPoint2 max = document.Database.Limmax;
                        return new DisplayBox(
                            Math.Min(min.X, max.X),
                            Math.Min(min.Y, max.Y),
                            Math.Max(min.X, max.X),
                            Math.Max(min.Y, max.Y)
                        );
                    }

                    DxfExtents2d limits = space.Layout.Limits;
                    return new DisplayBox(
                        Math.Min(limits.MinPoint.X, limits.MaxPoint.X),
                        Math.Min(limits.MinPoint.Y, limits.MaxPoint.Y),
                        Math.Max(limits.MinPoint.X, limits.MaxPoint.X),
                        Math.Max(limits.MinPoint.Y, limits.MaxPoint.Y)
                    );
                default:
                    return extents;
            }
        }
    }

    /// <summary>
    /// The plot style tables the presenter makes in code, as AutoCAD's monochrome.ctb and grayscale.ctb colour a plot: no
    /// table file is read. White, AutoCAD's colour 7 on a dark screen, plots black on the white sheet with every table.
    /// </summary>
    public static class CadPlotStyles
    {
        /// <summary>
        /// Gets the colour an object of a colour plots in: black with <see cref="CadPlotStyleTable.Monochrome"/>; with
        /// <see cref="CadPlotStyleTable.Grayscale"/> the grey of its brightness, 0.299 red, 0.587 green and 0.114 blue,
        /// so pure red plots #4C4C4C, green #969696 and blue #1D1D1D; with <see cref="CadPlotStyleTable.None"/> its own.
        /// White, and colours within 2 per cent of it, plot black with each.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="rgb">The colour as 0xRRGGBB.</param>
        /// <returns>The colour plotted, as 0xRRGGBB.</returns>
        public static int Map(CadPlotStyleTable table, int rgb)
        {
            rgb &= 0xFFFFFF;
            int r = (rgb >> 16) & 0xFF;
            int g = (rgb >> 8) & 0xFF;
            int b = rgb & 0xFF;
            double luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;

            if (table == CadPlotStyleTable.Monochrome || luminance >= 0.98)
            {
                return 0x000000;
            }

            if (table == CadPlotStyleTable.Grayscale)
            {
                int grey = (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b, MidpointRounding.AwayFromZero);
                return (grey << 16) | (grey << 8) | grey;
            }

            return rgb;
        }
    }
}
