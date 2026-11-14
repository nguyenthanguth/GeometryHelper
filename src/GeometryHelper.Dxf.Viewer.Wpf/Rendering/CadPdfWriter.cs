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
    /// as text, and its line added to the stamp's log file. Several plots, as EXPORTPDF's All layouts, are written as one
    /// PDF of a page each (<see cref="WritePages"/>). Settings that carry <see cref="CadPlotSettings.PdfHyperlinks"/> make the
    /// first hyperlink of each object plotted a link of the page over the object's box.
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
        internal static void LogStamp(CadPlotStamp stamp, IReadOnlyList<string> stamped, string plotFile)
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

        /// <summary>
        /// Writes plots of several spaces to one PDF file of a page each, in their order, replacing a file of the name, as
        /// EXPORTPDF writes All layouts: each page the size of its own sheet, stamped by its own settings, the stamp's lines
        /// added to its log once for each page.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="document">The document.</param>
        /// <param name="pages">The plot of each page: its space, its settings and the display it was asked with.</param>
        /// <returns>The plots as they were laid on their sheets, in the order of the pages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when there is no page.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a page has nothing to plot.</exception>
        public static IReadOnlyList<CadPlotPage> WritePages(
            string path,
            CadDocument document,
            IReadOnlyList<CadPlotJob> pages
        )
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }

            if (pages.Count == 0)
            {
                throw new ArgumentException("A PDF has a page at least.", nameof(pages));
            }

            byte[] pdf = Make(document, pages, out IReadOnlyList<CadPlotPage> laid, out var stamped);
            File.WriteAllBytes(path, pdf);

            for (int i = 0; i < pages.Count; i++)
            {
                LogStamp(pages[i].Settings.PlotStamp, stamped[i], path);
            }

            GeometryHelperLog.Info(
                "CadPdfWriter: "
                    + pages.Count.ToString(CultureInfo.InvariantCulture)
                    + " pages plotted to "
                    + path
                    + "."
            );
            return laid;
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
            byte[] pdf = Make(
                document,
                new[]
                {
                    new CadPlotJob(
                        space ?? throw new ArgumentNullException(nameof(space)),
                        settings ?? throw new ArgumentNullException(nameof(settings)),
                        display,
                        null
                    ),
                },
                out IReadOnlyList<CadPlotPage> pages,
                out IReadOnlyList<IReadOnlyList<string>> lines
            );
            page = pages[0];
            stamped = lines[0];
            return pdf;
        }

        private static byte[] Make(
            CadDocument document,
            IReadOnlyList<CadPlotJob> jobs,
            out IReadOnlyList<CadPlotPage> pages,
            out IReadOnlyList<IReadOnlyList<string>> stamped
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var laid = new List<CadPlotPage>();
            var lines = new List<IReadOnlyList<string>>();

            foreach (CadPlotJob job in jobs)
            {
                CadPlotPage page = CadPlotPage.Compute(document, job.Space, job.Settings, job.Display);

                if (page.IsEmpty)
                {
                    throw new InvalidOperationException(
                        "Nothing to plot: the plot area of " + job.Space.Name + " is empty."
                    );
                }

                laid.Add(page);
                lines.Add(
                    job.Settings.PlotStamp == null
                        ? Array.Empty<string>()
                        : job.Settings.PlotStamp.Lines(
                            document,
                            job.Space,
                            job.Settings,
                            page,
                            DateTime.Now,
                            Environment.UserName
                        )
                );
            }

            pages = laid;
            stamped = lines;
            byte[] written;

            // One PDF at a time: SkiaSharp's PDF writer shares the typefaces its font manager caches, and two pages written at
            // once lost the map from their glyphs to letters in 3 to 6 of 64, their text no longer found.
            lock (Writing)
                using (var memory = new MemoryStream())
                {
                    var metadata = new SKDocumentPdfMetadata(SKDocumentPdfMetadata.DefaultRasterDpi)
                    {
                        Title = document.Title + " - " + (jobs.Count == 1 ? jobs[0].Space.Name : "Layouts"),
                        Creator = CadPlotSettings.PdfDevice,
                        Producer = CadPlotSettings.PdfDevice,
                    };

                    using (SKDocument pdf = SKDocument.CreatePdf(memory, metadata))
                    using (var renderer = new SkiaCanvasRenderer())
                    {
                        for (int i = 0; i < jobs.Count; i++)
                        {
                            CadPlotPage page = laid[i];
                            CadPlotSettings settings = jobs[i].Settings;
                            double height = page.SheetHeightPoints;

                            // SkiaSharp rounds a page to whole points and turns its Y over about the rounded height: the plot
                            // is drawn that much lower, so that it stands where it should once the page is given its exact size.
                            float roundedHeight = Rounded(height);
                            SKCanvas canvas = pdf.BeginPage(Rounded(page.SheetWidthPoints), roundedHeight);
                            canvas.Translate(0f, (float)(roundedHeight - height));
                            renderer.RenderPlot(
                                canvas,
                                document,
                                jobs[i].Space,
                                page,
                                settings,
                                SheetToPoints(page),
                                shxText: settings.PdfShx
                            );

                            if (settings.PdfHyperlinks)
                            {
                                DrawHyperlinks(canvas, document, jobs[i].Space, page);
                            }

                            if (settings.PlotStamp != null)
                            {
                                CadPlotStampPainter.Draw(
                                    canvas,
                                    settings.PlotStamp,
                                    lines[i],
                                    page,
                                    SheetToPoints(page),
                                    settings.UpsideDown
                                );
                            }

                            pdf.EndPage();
                        }

                        pdf.Close();
                    }

                    written = memory.ToArray();
                }

            // Each size of sheet given its exact box once; two sheets rounded alike are the same sheet.
            var done = new HashSet<string>(StringComparer.Ordinal);

            foreach (CadPlotPage page in laid)
            {
                if (done.Add(Whole(Rounded(page.SheetWidthPoints)) + " " + Whole(Rounded(page.SheetHeightPoints))))
                {
                    written = ExactPage(
                        written,
                        Rounded(page.SheetWidthPoints),
                        Rounded(page.SheetHeightPoints),
                        page.SheetWidthPoints,
                        page.SheetHeightPoints
                    );
                }
            }

            return written;
        }

        /// <summary>
        /// EXPORTPDF's Include hyperlinks: the first hyperlink of each object plotted, with an address, made a link of the page
        /// over the box of what is drawn of the object, within the printable area. An object of the space itself has one link;
        /// an object of model space seen through a layout's viewport has one in each viewport that shows it, over its box on the
        /// paper cut to the viewport's frame (or to the box of its clipped outline), and none where its layer is frozen in the
        /// viewport. A file's address is written as a file URI (D:\Docs\B-12.pdf as file:///D:/Docs/B-12.pdf), any other as it
        /// is.
        /// </summary>
        private static void DrawHyperlinks(SKCanvas canvas, CadDocument document, CadSpace space, CadPlotPage page)
        {
            var boxes = new Dictionary<string, DisplayBox>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            var addresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<DisplayItem2> items = document.GetDisplay(space).Items;

            foreach (DisplayItem2 item in items)
            {
                if (
                    item.Handle == null
                    || item is DisplayViewport2
                    || item.IsNotPlotted
                    || !item.Bounds.Intersects(page.Area)
                )
                {
                    continue;
                }

                if (!boxes.TryGetValue(item.Handle, out DisplayBox box))
                {
                    order.Add(item.Handle);
                    box = DisplayBox.Empty;
                }

                boxes[item.Handle] = box.Union(item.Bounds);
            }

            foreach (string handle in order)
            {
                string url = AddressOf(document, handle, addresses);

                if (url != null)
                {
                    DrawLink(canvas, page, boxes[handle], url);
                }
            }

            foreach (DisplayItem2 item in items)
            {
                if (item is DisplayViewport2 viewport && !viewport.IsNotPlotted)
                {
                    DrawViewportLinks(canvas, document, page, viewport, addresses);
                }
            }
        }

        /// <summary>
        /// The links of the objects of model space one viewport shows: each with an address over its box on the paper, cut to
        /// the frame of the viewport and, where it is clipped, to the box of the outline, in the order the objects are listed.
        /// </summary>
        private static void DrawViewportLinks(
            SKCanvas canvas,
            CadDocument document,
            CadPlotPage page,
            DisplayViewport2 viewport,
            Dictionary<string, string> addresses
        )
        {
            DisplayBox window = viewport.Bounds;

            if (viewport.Clip != null)
            {
                window = Cut(window, ClipBox(viewport.Clip));
            }

            if (window.IsEmpty || !window.Intersects(page.Area))
            {
                return;
            }

            Affine2 toPaper = viewport.ModelToPaper;
            var boxes = new Dictionary<string, DisplayBox>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (DisplayItem2 item in viewport.Model.Items)
            {
                if (
                    item.Handle == null
                    || item.IsNotPlotted
                    || item.Bounds.IsEmpty
                    || viewport.FrozenLayers.Contains(item.Layer ?? string.Empty)
                )
                {
                    continue;
                }

                DisplayBox onPaper = DisplayBox.Empty;

                foreach (double[] corner in Corners(item.Bounds))
                {
                    onPaper = onPaper.Add(toPaper.X(corner[0], corner[1]), toPaper.Y(corner[0], corner[1]));
                }

                DisplayBox shown = Cut(onPaper, window);

                if (shown.IsEmpty || AddressOf(document, item.Handle, addresses) == null)
                {
                    continue;
                }

                if (!boxes.TryGetValue(item.Handle, out DisplayBox box))
                {
                    order.Add(item.Handle);
                    box = DisplayBox.Empty;
                }

                boxes[item.Handle] = box.Union(shown);
            }

            foreach (string handle in order)
            {
                if (boxes[handle].Intersects(page.Area))
                {
                    DrawLink(canvas, page, boxes[handle], addresses[handle]);
                }
            }
        }

        /// <summary>The address of the first hyperlink of an object that has one, as a PDF link takes it; null for none. Kept once for each object.</summary>
        private static string AddressOf(CadDocument document, string handle, Dictionary<string, string> kept)
        {
            if (kept.TryGetValue(handle, out string known))
            {
                return known;
            }

            DxfEntity entity = document.FindEntity(handle);
            string url = null;

            for (int i = 0; entity != null && i < entity.Hyperlinks.Count && url == null; i++)
            {
                string name = entity.Hyperlinks[i].Name.Trim();
                url = name.Length == 0 ? null : Address(name);
            }

            kept[handle] = url;
            return url;
        }

        /// <summary>A link of the page over a box of the space, within the printable area; none where the box lies outside it.</summary>
        private static void DrawLink(SKCanvas canvas, CadPlotPage page, DisplayBox box, string url)
        {
            DisplayBox sheet = DisplayBox.Empty;

            foreach (double[] corner in Corners(box))
            {
                sheet = sheet.Add(
                    page.DrawingToSheet.X(corner[0], corner[1]),
                    page.DrawingToSheet.Y(corner[0], corner[1])
                );
            }

            DisplayBox shown = Cut(sheet, page.Printable);

            if (shown.IsEmpty)
            {
                return;
            }

            Affine2 toPoints = SheetToPoints(page);
            float x0 = (float)toPoints.X(shown.MinX, shown.MinY);
            float y0 = (float)toPoints.Y(shown.MinX, shown.MinY);
            float x1 = (float)toPoints.X(shown.MaxX, shown.MaxY);
            float y1 = (float)toPoints.Y(shown.MaxX, shown.MaxY);
            canvas.DrawUrlAnnotation(
                new SKRect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1)),
                url
            );
        }

        /// <summary>The part of a box inside another; an empty box where they share nothing.</summary>
        private static DisplayBox Cut(DisplayBox box, DisplayBox within) =>
            new DisplayBox(
                Math.Max(box.MinX, within.MinX),
                Math.Max(box.MinY, within.MinY),
                Math.Min(box.MaxX, within.MaxX),
                Math.Min(box.MaxY, within.MaxY)
            );

        /// <summary>The box of the outline of a clipped viewport, its arcs kept to a hundredth of a paper unit.</summary>
        private static DisplayBox ClipBox(DisplayClip2 clip)
        {
            DisplayBox box = DisplayBox.Empty;

            foreach (double[] ring in clip.FlattenAll(0.01))
            {
                for (int i = 0; i + 1 < ring.Length; i += 2)
                {
                    box = box.Add(ring[i], ring[i + 1]);
                }
            }

            return box;
        }

        /// <summary>The four corners of a box, as pairs of X and Y.</summary>
        private static IEnumerable<double[]> Corners(DisplayBox box) =>
            new[]
            {
                new[] { box.MinX, box.MinY },
                new[] { box.MaxX, box.MinY },
                new[] { box.MaxX, box.MaxY },
                new[] { box.MinX, box.MaxY },
            };

        /// <summary>A hyperlink's address as a PDF link takes it: a rooted file path as a file URI, anything else as it is.</summary>
        private static string Address(string name)
        {
            if (name.IndexOf("://", StringComparison.Ordinal) < 0 && Path.IsPathRooted(name))
            {
                try
                {
                    return new Uri(name).AbsoluteUri;
                }
                catch (UriFormatException)
                {
                    return name;
                }
            }

            return name;
        }

        /// <summary>A size in points rounded to whole points, as SkiaSharp gives a page.</summary>
        private static float Rounded(double points) => (float)Math.Floor(points + 0.5);

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
