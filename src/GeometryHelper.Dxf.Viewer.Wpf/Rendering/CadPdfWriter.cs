using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The presenter's plotter, GeometryHelper PDF: writes a plot as one page of a PDF through SkiaSharp's
    /// <c>SKDocument.CreatePdf</c>, in vectors, from the display list the canvas draws. Lines keep their lineweights, hatches
    /// and solids are filled, and the text of a TrueType font is written as text with its font embedded, so that a reader
    /// finds and copies it; the text of an SHX font is its strokes, as the canvas draws it, and is not found unless the
    /// settings carry PDFSHX (<see cref="CadPlotSettings.PdfShx"/>), which writes the words again over the strokes in letters
    /// no eye sees. A plot stamp the settings carry (<see cref="CadPlotSettings.PlotStamp"/>) is drawn in a corner of the sheet
    /// as text, and its line added to the stamp's log file.
    /// </summary>
    /// <remarks>
    /// The page is the sheet as it lies, to a ten-thousandth of a point: ISO A3 on its side is 1 190.5512 by 841.8898 points
    /// (420 by 297 mm), where SkiaSharp alone writes 1 191 by 842. A plot whose area is empty writes no file.
    /// </remarks>
    public static class CadPdfWriter
    {
        private static readonly object Writing = new object();

        /// <summary>Writes a plot of a space to a PDF file of one page, replacing a file of the name.</summary>
        /// <param name="path">The file.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted.</param>
        /// <param name="display">What the view shows, in drawing units, for a plot of the display; empty for the extents.</param>
        /// <returns>The plot as it was laid on its sheet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when there is nothing to plot.</exception>
        public static CadPlotPage Write(
            string path,
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display
        )
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            byte[] pdf = Make(
                document,
                space,
                settings,
                display,
                out CadPlotPage page,
                out IReadOnlyList<string> stamped
            );
            File.WriteAllBytes(path, pdf);
            LogStamp(settings.PlotStamp, stamped, path);
            GeometryHelperLog.Info(
                "CadPdfWriter: "
                    + space.Name
                    + " plotted to "
                    + path
                    + ", "
                    + Mm(page.SheetWidth)
                    + " by "
                    + Mm(page.SheetHeight)
                    + " mm at 1:"
                    + (1.0 / page.Scale).ToString("0.####", CultureInfo.InvariantCulture)
                    + "."
            );
            return page;
        }

        /// <summary>Writes a plot of a space as a PDF of one page to a stream.</summary>
        /// <param name="stream">The stream, written from where it stands.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted.</param>
        /// <param name="display">What the view shows, in drawing units, for a plot of the display; empty for the extents.</param>
        /// <returns>The plot as it was laid on its sheet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when there is nothing to plot.</exception>
        public static CadPlotPage Write(
            Stream stream,
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display
        )
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            byte[] pdf = Make(document, space, settings, display, out CadPlotPage page, out _);
            stream.Write(pdf, 0, pdf.Length);
            return page;
        }

        /// <summary>
        /// The words of the plot stamp added to its log file, a line each, where the stamp writes one; a log that cannot be
        /// written is logged and does not fail the plot.
        /// </summary>
        private static void LogStamp(CadPlotStamp stamp, IReadOnlyList<string> stamped, string plotFile)
        {
            if (stamp == null || !stamp.WriteLog || stamped.Count == 0)
            {
                return;
            }

            string log = stamp.LogPath(plotFile);

            try
            {
                File.AppendAllText(log, CadPlotStamp.LogLine(stamped) + "\n");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn("CadPdfWriter: the plot stamp was not written to " + log + ".", exception);
            }
        }

        private static byte[] Make(
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display,
            out CadPlotPage page,
            out IReadOnlyList<string> stamped
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

            stamped =
                settings.PlotStamp == null
                    ? Array.Empty<string>()
                    : settings.PlotStamp.Lines(document, space, settings, page, DateTime.Now, Environment.UserName);
            double width = page.SheetWidthPoints;
            double height = page.SheetHeightPoints;

            // SkiaSharp rounds a page to whole points and turns its Y over about the rounded height: the plot is drawn that
            // much lower, so that it stands where it should once the page is given its exact size.
            float roundedWidth = (float)Math.Floor(width + 0.5);
            float roundedHeight = (float)Math.Floor(height + 0.5);
            byte[] written;

            // One PDF at a time: SkiaSharp's PDF writer shares the typefaces its font manager caches, and two pages written at
            // once lost the map from their glyphs to letters in 3 to 6 of 64, their text no longer found.
            lock (Writing)
                using (var memory = new MemoryStream())
                {
                    var metadata = new SKDocumentPdfMetadata(SKDocumentPdfMetadata.DefaultRasterDpi)
                    {
                        Title = document.Title + " - " + space.Name,
                        Creator = CadPlotSettings.PdfDevice,
                        Producer = CadPlotSettings.PdfDevice,
                    };

                    using (SKDocument pdf = SKDocument.CreatePdf(memory, metadata))
                    using (var renderer = new SkiaCanvasRenderer())
                    {
                        SKCanvas canvas = pdf.BeginPage(roundedWidth, roundedHeight);
                        canvas.Translate(0f, (float)(roundedHeight - height));
                        renderer.RenderPlot(
                            canvas,
                            document,
                            space,
                            page,
                            settings,
                            SheetToPoints(page),
                            shxText: settings.PdfShx
                        );

                        if (settings.PlotStamp != null)
                        {
                            CadPlotStampPainter.Draw(canvas, settings.PlotStamp, stamped, page, SheetToPoints(page));
                        }

                        pdf.EndPage();
                        pdf.Close();
                    }

                    written = memory.ToArray();
                }

            return ExactPage(written, roundedWidth, roundedHeight, width, height);
        }

        /// <summary>From the sheet's millimetres, Y upward, to a PDF page's points, Y downward from its top.</summary>
        internal static Affine2 SheetToPoints(CadPlotPage page) =>
            new Affine2(
                CadPlotPage.PointsPerMillimetre,
                0.0,
                0.0,
                -CadPlotPage.PointsPerMillimetre,
                0.0,
                page.SheetHeightPoints
            );

        /// <summary>
        /// The PDF with each page's <c>/MediaBox [0 0 1191 842]</c> given its exact size, as <c>[0 0 1190.5512 841.8898]</c>, the
        /// offsets of its cross-reference table moved with the bytes added. The box is looked for only in the dictionaries of
        /// the objects the table lists, never in a stream; a file not laid out as SkiaSharp writes one is left as it is.
        /// </summary>
        internal static byte[] ExactPage(
            byte[] pdf,
            double roundedWidth,
            double roundedHeight,
            double width,
            double height
        )
        {
            Encoding latin = Encoding.GetEncoding(28591);
            string text = latin.GetString(pdf);
            string was = "/MediaBox [0 0 " + Whole(roundedWidth) + " " + Whole(roundedHeight) + "]";
            string now = "/MediaBox [0 0 " + Points(width) + " " + Points(height) + "]";
            int startxref = text.LastIndexOf("startxref", StringComparison.Ordinal);

            if (startxref < 0 || was == now)
            {
                return pdf;
            }

            int numberStart = startxref + "startxref".Length;
            int numberEnd = text.IndexOf("%%EOF", numberStart, StringComparison.Ordinal);

            if (
                numberEnd < 0
                || !int.TryParse(
                    text.Substring(numberStart, numberEnd - numberStart).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int xref
                )
                || xref <= 0
                || xref >= text.Length
                || string.CompareOrdinal(text, xref, "xref", 0, 4) != 0
            )
            {
                return pdf;
            }

            // The entries of the table: each 20 bytes, an offset of 10 digits, a generation of 5, n or f.
            var entries = new List<int>();
            var offsets = new List<int>();
            int at = text.IndexOf('\n', xref) + 1;

            while (at > 0 && at < startxref && string.CompareOrdinal(text, at, "trailer", 0, 7) != 0)
            {
                int lineEnd = text.IndexOf('\n', at);
                string[] head = text.Substring(at, lineEnd - at).Trim().Split(' ');

                if (
                    head.Length != 2
                    || !int.TryParse(head[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                )
                {
                    return pdf;
                }

                at = lineEnd + 1;

                for (int i = 0; i < count; i++, at += 20)
                {
                    if (
                        at + 20 > text.Length
                        || !int.TryParse(
                            text.Substring(at, 10),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int offset
                        )
                    )
                    {
                        return pdf;
                    }

                    entries.Add(at);
                    offsets.Add(text[at + 17] == 'n' ? offset : -1);
                }
            }

            // The boxes in the dictionaries of the objects, before a stream or the end of the object.
            var places = new List<int>();

            foreach (int offset in offsets)
            {
                if (offset <= 0 || offset >= xref)
                {
                    continue;
                }

                int end = text.IndexOf("endobj", offset, StringComparison.Ordinal);
                int stream = text.IndexOf("stream", offset, StringComparison.Ordinal);
                end = stream >= 0 && (end < 0 || stream < end) ? stream : end;
                int box = end < 0 ? -1 : text.IndexOf(was, offset, end - offset, StringComparison.Ordinal);

                if (box >= 0)
                {
                    places.Add(box);
                }
            }

            if (places.Count == 0)
            {
                return pdf;
            }

            places.Sort();
            int grow = now.Length - was.Length;
            var built = new StringBuilder(text.Length + places.Count * grow);
            int from = 0;

            foreach (int place in places)
            {
                built.Append(text, from, place - from).Append(now);
                from = place + was.Length;
            }

            built.Append(text, from, text.Length - from);

            // Each offset after a box moves by what the boxes before it grew, the table's own entries and startxref with them.
            int Moved(int offset)
            {
                int before = 0;

                foreach (int place in places)
                {
                    before += place < offset ? grow : 0;
                }

                return offset + before;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (offsets[i] > 0)
                {
                    built
                        .Remove(Moved(entries[i]), 10)
                        .Insert(Moved(entries[i]), Moved(offsets[i]).ToString("D10", CultureInfo.InvariantCulture));
                }
            }

            int numberAt = Moved(numberStart);
            built
                .Remove(numberAt, numberEnd - numberStart)
                .Insert(numberAt, "\n" + Moved(xref).ToString(CultureInfo.InvariantCulture) + "\n");
            return latin.GetBytes(built.ToString());
        }

        private static string Whole(double value) => value.ToString("0", CultureInfo.InvariantCulture);

        private static string Points(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static string Mm(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
