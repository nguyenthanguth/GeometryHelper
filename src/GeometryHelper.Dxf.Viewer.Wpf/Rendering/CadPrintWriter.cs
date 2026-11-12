using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Draws a plot for a printer of Windows: the page the PDF writer writes and the preview shows, drawn by
    /// <see cref="SkiaCanvasRenderer"/> as a plotter draws it (every item, colours through the plot style table, each line at its
    /// lineweight on the sheet), at up to 300 dots to the inch, in bands, and set into a WPF visual that WPF prints. The
    /// printer gets pixels, not vectors: SkiaSharp's XPS writer stops the process with "Must set a PNG encoder" and so is not
    /// used, and WPF takes no Skia canvas.
    /// </summary>
    /// <remarks>
    /// A sheet of A3 (420 by 297) is 4 961 by 3 508 pixels at 300 dots to the inch, 17.4 million; a sheet of A0 (1 189 by 841)
    /// would be 139 million, so a page is drawn at the resolution that keeps it under <see cref="MaxPixels"/>, 40 million:
    /// 160 dots to the inch for A0. Each band is at most 4 million pixels.
    /// </remarks>
    public static class CadPrintWriter
    {
        /// <summary>The dots to the inch a page is drawn at where it fits the budget: 300.</summary>
        public const double MaxDotsPerInch = 300.0;

        /// <summary>The most pixels a page is drawn with, 40 million: a larger sheet is drawn at a lower resolution.</summary>
        public const double MaxPixels = 40000000.0;

        private const double BandPixels = 4000000.0;

        /// <summary>
        /// Gets the dots to the inch a sheet is drawn at: 300, or fewer, whole, where the sheet at 300 would take more than
        /// <see cref="MaxPixels"/>. The turn of the sheet changes nothing.
        /// </summary>
        /// <param name="widthMm">The width of the sheet in millimetres.</param>
        /// <param name="heightMm">The height of the sheet in millimetres.</param>
        /// <returns>The dots to the inch.</returns>
        public static double DotsPerInch(double widthMm, double heightMm)
        {
            double squareInches = widthMm / 25.4 * (heightMm / 25.4);

            if (!(squareInches > 0.0))
            {
                return MaxDotsPerInch;
            }

            return Math.Min(MaxDotsPerInch, Math.Floor(Math.Sqrt(MaxPixels / squareInches)));
        }

        /// <summary>
        /// Draws a plot of a space on its sheet as a visual to print: units of 1/96 inch, the origin the sheet's upper left
        /// corner, white paper, the plot laid as <see cref="CadPlotPage.Compute"/> lays it. The visual is owned by the calling
        /// thread, the one that prints it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted.</param>
        /// <param name="display">What the view shows, in drawing units, for a plot of the display; empty for the extents.</param>
        /// <param name="page">The plot as it was laid on its sheet.</param>
        /// <returns>The page.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the space or the settings are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when there is nothing to plot.</exception>
        public static DrawingVisual CreatePage(
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display,
            out CadPlotPage page
        )
        {
            page = CadPlotPage.Compute(
                document ?? throw new ArgumentNullException(nameof(document)),
                space ?? throw new ArgumentNullException(nameof(space)),
                settings ?? throw new ArgumentNullException(nameof(settings)),
                display
            );

            if (page.IsEmpty)
            {
                throw new InvalidOperationException("Nothing to plot: the plot area of " + space.Name + " is empty.");
            }

            double dpi = DotsPerInch(page.SheetWidth, page.SheetHeight);
            double perMillimetre = dpi / 25.4;
            int width = (int)Math.Ceiling(page.SheetWidth * perMillimetre);
            int height = (int)Math.Ceiling(page.SheetHeight * perMillimetre);
            int bandRows = Math.Max(64, (int)(BandPixels / width));
            double unit = 96.0 / dpi;
            var visual = new DrawingVisual();

            using (DrawingContext context = visual.RenderOpen())
            {
                for (int top = 0; top < height; top += bandRows)
                {
                    int rows = Math.Min(bandRows, height - top);
                    BitmapSource band = Draw(document, space, page, settings, width, rows, top, perMillimetre);
                    context.DrawImage(band, new Rect(0.0, top * unit, width * unit, rows * unit));
                }
            }

            GeometryHelperLog.Info(
                "CadPrintWriter: "
                    + space.Name
                    + " drawn for a printer, "
                    + width.ToString(CultureInfo.InvariantCulture)
                    + " by "
                    + height.ToString(CultureInfo.InvariantCulture)
                    + " pixels at "
                    + dpi.ToString("0", CultureInfo.InvariantCulture)
                    + " dots to the inch."
            );
            return visual;
        }

        /// <summary>One band of the page, its rows from <paramref name="top"/>, drawn and handed to WPF.</summary>
        private static BitmapSource Draw(
            CadDocument document,
            CadSpace space,
            CadPlotPage page,
            CadPlotSettings settings,
            int width,
            int rows,
            int top,
            double perMillimetre
        )
        {
            using (var picture = new SKBitmap(new SKImageInfo(width, rows, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                using (SKSurface surface = SKSurface.Create(picture.Info, picture.GetPixels(), picture.RowBytes))
                using (var renderer = new SkiaCanvasRenderer())
                {
                    surface.Canvas.Clear(SKColors.White);

                    // The sheet's millimetres, Y upward from its lower left corner, to the band's rows from its top.
                    renderer.RenderPlot(
                        surface.Canvas,
                        document,
                        space,
                        page,
                        settings,
                        new Affine2(
                            perMillimetre,
                            0.0,
                            0.0,
                            -perMillimetre,
                            0.0,
                            page.SheetHeight * perMillimetre - top
                        )
                    );
                    surface.Canvas.Flush();
                }

                using (SKImage image = SKImage.FromBitmap(picture))
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = new MemoryStream(data.ToArray()))
                {
                    BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    frame.Freeze();
                    return frame;
                }
            }
        }
    }
}
