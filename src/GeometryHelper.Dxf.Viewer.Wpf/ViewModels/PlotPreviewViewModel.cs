using System;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What a drag does in the plot preview, as the buttons of its toolbar and its shortcut menu choose.</summary>
    public enum PlotPreviewMode
    {
        /// <summary>A drag moves the sheet.</summary>
        Pan,

        /// <summary>A drag up zooms in, down zooms out, about the middle of the window.</summary>
        Zoom,

        /// <summary>A drag frames a window that is then zoomed to.</summary>
        ZoomWindow,
    }

    /// <summary>
    /// The plot preview, which PREVIEW and the Plot dialog's Preview… show over the whole window: the sheet as it will be
    /// plotted on grey, its printable area dashed, panned and zoomed in real time, by a window, or back to the whole sheet,
    /// as AutoCAD's preview. Its view is the sheet's millimetres at <see cref="PixelsPerMillimetre"/> about
    /// (<see cref="CenterX"/>, <see cref="CenterY"/>).
    /// </summary>
    public sealed class PlotPreviewViewModel : ObservableObject
    {
        private PlotPreviewMode _mode = PlotPreviewMode.Pan;

        /// <summary>Makes the preview of a plot.</summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted; a copy is kept.</param>
        /// <param name="display">What the view showed, in drawing units, for a plot of the display.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document, the space or the settings are null.</exception>
        public PlotPreviewViewModel(CadDocument document, CadSpace space, CadPlotSettings settings, DisplayBox display)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Space = space ?? throw new ArgumentNullException(nameof(space));
            Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            Display = display;
            Page = CadPlotPage.Compute(document, space, Settings, display);
            CenterX = Page.SheetWidth / 2.0;
            CenterY = Page.SheetHeight / 2.0;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the space plotted.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets how it is plotted.</summary>
        public CadPlotSettings Settings { get; }

        /// <summary>Gets what the view showed, for a plot of the display.</summary>
        public DisplayBox Display { get; }

        /// <summary>Gets the plot laid on its sheet.</summary>
        public CadPlotPage Page { get; }

        /// <summary>Gets what the toolbar says of the plot: "GeometryHelper PDF · ISO A3 landscape · 1:20 · Monochrome".</summary>
        public string Summary
        {
            get
            {
                string scale;

                if (Settings.FitToPaper)
                {
                    scale = Page.IsEmpty ? "Fit" : "1:" + (1.0 / Page.Scale).ToString("0.##", CultureInfo.InvariantCulture);
                }
                else
                {
                    scale = Settings.PaperUnits.ToString("0.####", CultureInfo.InvariantCulture) + ":" + Settings.DrawingUnits.ToString("0.####", CultureInfo.InvariantCulture);
                }

                string table = Settings.EffectiveStyleTable == CadPlotStyleTable.None ? "No plot styles" : Settings.EffectiveStyleTable.ToString();
                return CadPlotSettings.PdfDevice + " · " + Settings.Paper.Name + " " + (Settings.Landscape ? "landscape" : "portrait") + " · " + scale + " · " + table;
            }
        }

        /// <summary>Gets what the status bar says of the sheet: "Paper 420 × 297 mm · printable area dashed".</summary>
        public string PaperText => "Paper " + Mm(Page.SheetWidth) + " × " + Mm(Page.SheetHeight) + " mm · printable area dashed";

        /// <summary>Gets the words of the status bar: how to leave the preview.</summary>
        public string HelpText => "Press ESC or ENTER to exit, or right-click to display shortcut menu.";

        /// <summary>Gets or sets what a drag does: Pan until another is chosen.</summary>
        public PlotPreviewMode Mode
        {
            get => _mode;
            set
            {
                if (Set(ref _mode, value))
                {
                    Raise(nameof(IsPan));
                    Raise(nameof(IsZoom));
                    Raise(nameof(IsZoomWindow));
                }
            }
        }

        /// <summary>Gets or sets whether a drag pans.</summary>
        public bool IsPan
        {
            get => _mode == PlotPreviewMode.Pan;
            set => Mode = value ? PlotPreviewMode.Pan : _mode;
        }

        /// <summary>Gets or sets whether a drag zooms in real time.</summary>
        public bool IsZoom
        {
            get => _mode == PlotPreviewMode.Zoom;
            set => Mode = value ? PlotPreviewMode.Zoom : _mode;
        }

        /// <summary>Gets or sets whether a drag frames a window to zoom to.</summary>
        public bool IsZoomWindow
        {
            get => _mode == PlotPreviewMode.ZoomWindow;
            set => Mode = value ? PlotPreviewMode.ZoomWindow : _mode;
        }

        /// <summary>Gets the pixels of the window to a millimetre of the sheet; nought until the preview is first sized.</summary>
        public double PixelsPerMillimetre { get; private set; }

        /// <summary>Gets the point of the sheet at the middle of the window, in millimetres from its lower left corner.</summary>
        public double CenterX { get; private set; }

        /// <summary>Gets the point of the sheet at the middle of the window, upward.</summary>
        public double CenterY { get; private set; }

        /// <summary>Gets or sets whether Plot was chosen, closing the preview to plot.</summary>
        public bool PlotChosen { get; set; }

        /// <summary>
        /// Shows the whole sheet, as Zoom Original does: within 88 per cent of the window either way, about its middle. A
        /// sheet of A3 on its side in a window 1 400 by 800 is 2.37 pixels to the millimetre, 996 by 704.
        /// </summary>
        /// <param name="width">The width of the window, in pixels.</param>
        /// <param name="height">The height of the window, in pixels.</param>
        public void ZoomOriginal(double width, double height)
        {
            if (!(width > 0.0) || !(height > 0.0))
            {
                return;
            }

            PixelsPerMillimetre = 0.88 * Math.Min(width / Page.SheetWidth, height / Page.SheetHeight);
            CenterX = Page.SheetWidth / 2.0;
            CenterY = Page.SheetHeight / 2.0;
        }

        /// <summary>Zooms by a factor about a point of the window, which stays where it is, as the wheel does; held between a hundredth and a thousand times the whole sheet's zoom.</summary>
        /// <param name="x">The point's X, in pixels from the window's left.</param>
        /// <param name="y">The point's Y, in pixels from its top.</param>
        /// <param name="factor">The factor: above 1 zooms in.</param>
        /// <param name="width">The width of the window.</param>
        /// <param name="height">The height of the window.</param>
        public void ZoomAt(double x, double y, double factor, double width, double height)
        {
            if (!(PixelsPerMillimetre > 0.0) || !(factor > 0.0))
            {
                return;
            }

            double whole = 0.88 * Math.Min(width / Page.SheetWidth, height / Page.SheetHeight);
            double next = Math.Max(whole / 100.0, Math.Min(whole * 1000.0, PixelsPerMillimetre * factor));
            double mx = CenterX + (x - width / 2.0) / PixelsPerMillimetre;
            double my = CenterY - (y - height / 2.0) / PixelsPerMillimetre;
            PixelsPerMillimetre = next;
            CenterX = mx - (x - width / 2.0) / next;
            CenterY = my + (y - height / 2.0) / next;
        }

        /// <summary>Moves the sheet with the cursor, by pixels to the right and down.</summary>
        /// <param name="dx">Pixels to the right.</param>
        /// <param name="dy">Pixels down.</param>
        public void PanBy(double dx, double dy)
        {
            if (PixelsPerMillimetre > 0.0)
            {
                CenterX -= dx / PixelsPerMillimetre;
                CenterY += dy / PixelsPerMillimetre;
            }
        }

        /// <summary>Zooms to a window framed on the screen, as Zoom Window does; a frame under 3 pixels either way is taken as no frame.</summary>
        /// <param name="x0">One corner's X, in pixels.</param>
        /// <param name="y0">Its Y.</param>
        /// <param name="x1">The opposite corner's X.</param>
        /// <param name="y1">Its Y.</param>
        /// <param name="width">The width of the window.</param>
        /// <param name="height">The height of the window.</param>
        public void ZoomWindow(double x0, double y0, double x1, double y1, double width, double height)
        {
            double w = Math.Abs(x1 - x0);
            double h = Math.Abs(y1 - y0);

            if (!(PixelsPerMillimetre > 0.0) || w < 3.0 || h < 3.0)
            {
                return;
            }

            double cx = CenterX + ((x0 + x1) / 2.0 - width / 2.0) / PixelsPerMillimetre;
            double cy = CenterY - ((y0 + y1) / 2.0 - height / 2.0) / PixelsPerMillimetre;
            PixelsPerMillimetre *= Math.Min(width / w, height / h);
            CenterX = cx;
            CenterY = cy;
        }

        /// <summary>Gets the transform from the sheet's millimetres, Y upward, to the window's pixels, Y downward.</summary>
        /// <param name="width">The width of the window.</param>
        /// <param name="height">The height of the window.</param>
        /// <returns>The transform.</returns>
        public Affine2 SheetToScreen(double width, double height)
            => new Affine2(PixelsPerMillimetre, 0.0, 0.0, -PixelsPerMillimetre, width / 2.0 - CenterX * PixelsPerMillimetre, height / 2.0 + CenterY * PixelsPerMillimetre);

        private static string Mm(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
