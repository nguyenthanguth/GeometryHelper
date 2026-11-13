using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// EXPORT and -EXPORT, as AutoCAD 2021's. EXPORT asks the file by the window's Export Data dialog, of the types AutoCAD's
    /// help lists for it (<see cref="CadExportJob.ExportFileTypes"/>), and writes Bitmap through BMPOUT's image; the other
    /// types have no writer here and are refused. -EXPORT asks AutoCAD's prompts, word for word as AutoCAD 2021's console
    /// writes them, and writes its Pdf through the PDF writer of -PLOT; Dwf and dwfX are refused.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string WritersNote =
            "PNGOUT, JPGOUT and BMPOUT write images, PLOT and -EXPORT write a PDF, SAVEAS writes a DXF.";

        internal static void AddExportTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("EXPORT", Export));
            registry.Add(new CadCommand("-EXPORT", ExportTyped));
        }

        /// <summary>
        /// EXPORT: the file and its type by the window's Export Data dialog or, with no window or FILEDIA 0, "Enter file
        /// name &lt;file&gt;:", the type then read from the name's extension, and the last type taken for a name of none. The
        /// type chosen is kept for the next EXPORT of the session, as AutoCAD keeps it. Bitmap goes on as BMPOUT does, with
        /// "Select objects or &lt;all objects and viewports&gt;:"; any other type is refused, nothing written.
        /// </summary>
        private static IEnumerable<CadPrompt> Export(CadCommandContext c)
        {
            IReadOnlyList<string> types = CadExportJob.ExportFileTypes;
            int kept = (int)Kept(c.Session, "export.type", types.ToList().IndexOf(CadExportJob.Bitmap));
            string type = types[kept >= 0 && kept < types.Count ? kept : types.ToList().IndexOf(CadExportJob.Bitmap)];
            string offered = ImageFileName(c.Document, CadExportJob.ExtensionsOf(type)[0]);
            var job = new CadExportJob("Export Data", types, type, offered);
            string file;

            if (c.Session.FileDialogs && c.Session.Request("EXPORT", c.Document, job))
            {
                if (job.FileName == null || !types.Contains(job.FileType))
                {
                    yield break;
                }

                file = job.FileName;
                type = job.FileType;
            }
            else
            {
                while (true)
                {
                    yield return CadPrompt.ForString("Enter file name", offered);
                    file =
                        c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0
                            ? c.Input.Text.Trim().Trim('"')
                            : offered;

                    if (file.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                    {
                        break;
                    }

                    c.Write("Invalid file name.");
                }

                if (!Path.IsPathRooted(file))
                {
                    file = Path.Combine(Path.GetDirectoryName(offered) ?? string.Empty, file);
                }

                // A name of a type's extension takes that type (the first listing it); any other has the last type's added.
                string extension = Path.GetExtension(file);
                string named = types.FirstOrDefault(t =>
                    CadExportJob.ExtensionsOf(t).Contains(extension, StringComparer.OrdinalIgnoreCase)
                );

                if (named != null)
                {
                    type = named;
                }
                else
                {
                    file += CadExportJob.ExtensionsOf(type)[0];
                }
            }

            c.Session.Remembered["export.type"] = types.ToList().IndexOf(type);

            if (type != CadExportJob.Bitmap)
            {
                c.Write(type + " has no writer in the presenter: nothing was exported. " + WritersNote);
                yield break;
            }

            if (!file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                file += ".bmp";
            }

            CadImageJob image = ImageJob(c, CadImageFormat.Bmp);
            image.FileName = file;

            foreach (CadPrompt prompt in WriteImage(c, "EXPORT", image))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -EXPORT, as AutoCAD 2021's console asks it: "Enter file format [Dwf/dwfX/Pdf] &lt;dwfX&gt;", its default the
        /// format last answered (as AutoCAD keeps EXPORTEPLOTFORMAT, whose first value is dwfX); "Enter plot area
        /// [Display/Extents/Window] &lt;Display&gt;:" on the Model tab, "[Current layout/All layouts]" on a layout;
        /// "Detailed plot configuration? [Yes/No] &lt;No&gt;:", Yes asking the paper size, its units, the orientation, the
        /// scale, the plot styles and the lineweights in -PLOT's words; then the file, by the window's Save As PDF dialog or
        /// "Enter file name &lt;file&gt;:", and the PDF written by -PLOT's writer. Dwf and dwfX are refused; All layouts
        /// writes a page for each layout.
        /// </summary>
        private static IEnumerable<CadPrompt> ExportTyped(CadCommandContext c)
        {
            string[] formats = { "Dwf", "dwfX", "Pdf" };

            // EXPORTEPLOTFORMAT: 0 PDF, 1 DWF, 2 DWFx, as AutoCAD numbers it; 2 until a format is answered.
            double kept = Kept(c.Session, "exporteplotformat", 2.0);
            string format =
                kept == 0.0 ? "Pdf"
                : kept == 1.0 ? "Dwf"
                : "dwfX";
            yield return CadPrompt.ForKeyword("Enter file format", formats, format).WithoutColon();

            if (!c.Input.IsNone)
            {
                format =
                    c.Input.Is("Pdf") ? "Pdf"
                    : c.Input.Is("Dwf") ? "Dwf"
                    : "dwfX";
            }

            c.Session.Remembered["exporteplotformat"] =
                format == "Pdf" ? 0.0
                : format == "Dwf" ? 1.0
                : 2.0;

            if (format != "Pdf")
            {
                c.Write(
                    (format == "Dwf" ? "DWF" : "DWFx")
                        + " files have no writer in the presenter: nothing was exported. Answer Pdf for a PDF."
                );
                yield break;
            }

            foreach (CadPrompt prompt in ExportPdf(c, "-EXPORT"))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// The questions of a PDF export on the command line, -EXPORT's after its format and EXPORTPDF's with FILEDIA 0: the plot
        /// area, the detailed configuration, then the file and the writing. <paramref name="request"/> is the name the window is
        /// asked the file's dialog by. All layouts writes one PDF of a page for each layout, in the order of the tabs, each by
        /// its own page setup, or by the detailed configuration answered where it is Yes; the file offered is the drawing's
        /// name, plan-level2.pdf.
        /// </summary>
        private static IEnumerable<CadPrompt> ExportPdf(CadCommandContext c, string request)
        {
            CadSpace space = c.Space;
            CadPlotSettings settings = CadPlotSettings.Read(c.Document, space);
            settings.UseDevice(CadPlotSettings.PdfDevice);
            WithOutputOptions(c, settings);
            bool all = false;

            if (space.IsModel)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter plot area",
                    new[] { "Display", "Extents", "Window" },
                    "Display"
                );
                settings.Area =
                    c.Input.Is("Extents") ? CadPlotArea.Extents
                    : c.Input.Is("Window") ? CadPlotArea.Window
                    : CadPlotArea.Display;

                if (settings.Area == CadPlotArea.Window)
                {
                    yield return CadPrompt.ForPoint("Specify first corner");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify opposite corner", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 second = c.Input.Point;
                    settings.Window = new DisplayBox(
                        Math.Min(first.X, second.X),
                        Math.Min(first.Y, second.Y),
                        Math.Max(first.X, second.X),
                        Math.Max(first.Y, second.Y)
                    );
                }
            }
            else
            {
                yield return CadPrompt.ForKeyword(
                    "Enter plot area",
                    new[] { "Current layout", "All layouts" },
                    "Current Layout"
                );

                all = c.Input.Is("All layouts");
                settings.Area = CadPlotArea.Layout;
            }

            yield return CadPrompt.ForKeyword("Detailed plot configuration?", PlotYesNo, "No");
            bool detailed = c.Input.Is("Yes");

            if (detailed)
            {
                foreach (CadPrompt prompt in PlotPaperSize(c, settings))
                {
                    yield return prompt;
                }

                yield return CadPrompt.ForKeyword(
                    "Enter paper units",
                    new[] { "Inches", "Millimeters" },
                    "Millimeters"
                );
                bool inches = c.Input.Is("Inches");
                yield return CadPrompt.ForKeyword(
                    "Enter drawing orientation",
                    new[] { "Portrait", "Landscape" },
                    settings.Landscape ? "Landscape" : "Portrait"
                );
                settings.Landscape = c.Input.IsNone ? settings.Landscape : c.Input.Is("Landscape");

                foreach (
                    CadPrompt prompt in PlotScale(c, settings, inches ? 25.4 : 1.0, inches ? "Inches" : "Millimeters")
                )
                {
                    yield return prompt;
                }

                foreach (CadPrompt prompt in PlotStyles(c, settings))
                {
                    yield return prompt;
                }
            }

            string offered = PlotFileName(c.Document, space);

            if (all)
            {
                offered = Path.Combine(
                    Path.GetDirectoryName(offered) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(c.Document.Title) + ".pdf"
                );
            }

            var job = new CadExportJob("Save As PDF", new[] { CadExportJob.Pdf }, CadExportJob.Pdf, offered);
            string file;

            if (c.Session.FileDialogs && c.Session.Request(request, c.Document, job))
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                yield return CadPrompt.ForString("Enter file name", offered);
                file =
                    c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0
                        ? c.Input.Text.Trim().Trim('"')
                        : offered;

                if (!Path.IsPathRooted(file))
                {
                    file = Path.Combine(Path.GetDirectoryName(offered) ?? string.Empty, file);
                }
            }

            if (all)
            {
                ExportedPdfOfLayouts(c, detailed ? settings : null, file);
            }
            else
            {
                ExportedPdf(c, space, settings, file);
            }
        }

        /// <summary>
        /// The PDF of every layout handed to the window's writer as one file (<see cref="CadPlotBook"/>), a page each in the
        /// order of the tabs, each plotted by its page setup or by <paramref name="detailed"/> where it is given; a layout whose
        /// sheet is empty is passed over.
        /// </summary>
        private static void ExportedPdfOfLayouts(CadCommandContext c, CadPlotSettings detailed, string file)
        {
            if (!file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                file += ".pdf";
            }

            var pages = new List<CadPlotJob>();

            foreach (CadSpace layout in LayoutSpaces(c))
            {
                CadPlotSettings settings = detailed?.Clone() ?? CadPlotSettings.Read(c.Document, layout);
                settings.UseDevice(CadPlotSettings.PdfDevice);
                settings.Area = CadPlotArea.Layout;
                WithOutputOptions(c, settings);

                if (!CadPlotPage.Compute(c.Document, layout, settings, DisplayBox.Empty).IsEmpty)
                {
                    pages.Add(new CadPlotJob(layout, settings, DisplayBox.Empty, file));
                }
            }

            if (pages.Count == 0)
            {
                c.Write("Nothing to export: the plot area is empty.");
                return;
            }

            if (!c.Session.Request("-PLOT", c.Document, new CadPlotBook(pages, file)))
            {
                c.Write("Exporting a PDF needs the window's PDF writer: nothing was written.");
                return;
            }

            c.Write("Plot written to " + file + ".");
        }

        /// <summary>The PDF of a plot handed to the window's writer, and what became of it said on the command line.</summary>
        private static void ExportedPdf(CadCommandContext c, CadSpace space, CadPlotSettings settings, string file)
        {
            if (!file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                file += ".pdf";
            }

            DisplayBox display = c.Session.View?.VisibleBox ?? DisplayBox.Empty;
            CadPlotPage page = CadPlotPage.Compute(c.Document, space, settings, display);

            if (page.IsEmpty)
            {
                c.Write("Nothing to export: the plot area is empty.");
                return;
            }

            if (!c.Session.Request("-PLOT", c.Document, new CadPlotJob(space, settings, display, file)))
            {
                c.Write("Exporting a PDF needs the window's PDF writer: nothing was written.");
                return;
            }

            c.Write("Plot written to " + file + ".");
        }
    }
}
