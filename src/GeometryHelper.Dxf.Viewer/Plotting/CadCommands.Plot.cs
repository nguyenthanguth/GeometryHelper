using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// PLOT, PREVIEW and PAGESETUP, which the window shows as the Plot dialog and the preview, and -PLOT, AutoCAD's plot on
    /// the command line: its detailed configuration asks the layout, the device, the sheet and its units, the turn, the area,
    /// the scale, the offset, the plot styles and the lineweights in AutoCAD 2021's words, then the file, whether to save the
    /// page setup, and whether to go on. The file is written by the window, which holds the PDF writer.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] YesNo = { "Yes", "No" };

        internal static void AddPlotTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("PLOT", c => PlotDialog(c, "PLOT")));
            registry.Add(new CadCommand("-PLOT", PlotTyped));
            registry.Add(new CadCommand("PREVIEW", c => PlotDialog(c, "PREVIEW")));
            registry.Add(new CadCommand("PAGESETUP", c => PlotDialog(c, "PAGESETUP")));
        }

        /// <summary>
        /// PLOT, PREVIEW and PAGESETUP: the window's dialog or preview, handed the space's page setup. A dialog closed for its
        /// Window&lt; button has the window picked here, "Specify first corner:" and "Specify opposite corner:", and is shown
        /// again with it. With no window to show them, PLOT asks -PLOT's prompts, as AutoCAD's PLOT with FILEDIA 0.
        /// </summary>
        private static IEnumerable<CadPrompt> PlotDialog(CadCommandContext c, string name)
        {
            CadSpace space = c.Space;
            var job = new CadPlotJob(space, CadPlotSettings.Read(c.Document, space), c.Session.View?.VisibleBox ?? DisplayBox.Empty, null);

            while (true)
            {
                if (!c.Session.Request(name, c.Document, job))
                {
                    if (name != "PLOT")
                    {
                        c.Write(name + " is shown by the window.");
                        yield break;
                    }

                    foreach (CadPrompt prompt in PlotTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (!job.PicksWindow)
                {
                    yield break;
                }

                job.PicksWindow = false;
                c.Write("Specify window for printing");
                yield return CadPrompt.ForPoint("Specify first corner");

                if (c.Input.IsPoint)
                {
                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify opposite corner", first);

                    if (c.Input.IsPoint)
                    {
                        GeoPoint2 second = c.Input.Point;
                        job.Settings.Area = CadPlotArea.Window;
                        job.Settings.Window = new DisplayBox(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));
                    }
                }
            }
        }

        /// <summary>-PLOT: AutoCAD's prompts, the detailed configuration or a page setup, then the file the window writes.</summary>
        private static IEnumerable<CadPrompt> PlotTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Detailed plot configuration?", YesNo, "No");
            bool detailed = c.Input.Is("Yes");
            CadSpace space = c.Space;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter a layout name or", new[] { "?" }, space.Name);

                if (c.Input.Is("?"))
                {
                    foreach (CadSpace each in c.Document.Spaces)
                    {
                        c.Write(each.Name);
                    }

                    continue;
                }

                if (c.Input.Kind == CadInputKind.Text && !string.Equals(c.Input.Text.Trim(), space.Name, StringComparison.OrdinalIgnoreCase))
                {
                    CadSpace named = SpaceNamed(c.Document, c.Input.Text.Trim());

                    if (named == null)
                    {
                        c.Write("Layout \"" + c.Input.Text.Trim() + "\" not found.");
                        continue;
                    }

                    space = named;
                }

                break;
            }

            CadPlotSettings settings = CadPlotSettings.Read(c.Document, space);

            if (!detailed)
            {
                while (true)
                {
                    yield return CadPrompt.ForString("Enter a page setup name");

                    if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                    {
                        break;
                    }

                    CadPlotSettings named = CadPlotSettings.ReadPageSetup(c.Document, space, c.Input.Text.Trim());

                    if (named != null)
                    {
                        settings = named;
                        break;
                    }

                    c.Write("Page setup \"" + c.Input.Text.Trim() + "\" not found.");
                }
            }

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter an output device name or", new[] { "?" }, CadPlotSettings.PdfDevice);

                if (c.Input.Is("?"))
                {
                    c.Write(CadPlotSettings.PdfDevice);
                    continue;
                }

                if (c.Input.Kind == CadInputKind.Text && !string.Equals(c.Input.Text.Trim(), CadPlotSettings.PdfDevice, StringComparison.OrdinalIgnoreCase))
                {
                    c.Write("Printer/plotter \"" + c.Input.Text.Trim() + "\" not found: the presenter plots with " + CadPlotSettings.PdfDevice + ".");
                    continue;
                }

                break;
            }

            settings.DeviceName = CadPlotSettings.PdfDevice;

            if (detailed)
            {
                foreach (CadPrompt prompt in PlotDetails(c, space, settings))
                {
                    yield return prompt;
                }
            }

            string offered = PlotFileName(c.Document, space);
            yield return CadPrompt.ForString("Enter file name", offered);
            string file = c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim().Trim('"') : offered;

            if (!file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                file += ".pdf";
            }

            yield return CadPrompt.ForKeyword("Save changes to page setup", YesNo, "No");
            bool save = c.Input.Is("Yes");
            yield return CadPrompt.ForKeyword("Proceed with plot", YesNo, "Yes");
            bool proceed = !c.Input.Is("No");
            DisplayBox display = c.Session.View?.VisibleBox ?? DisplayBox.Empty;

            if (save)
            {
                c.Apply(CadPlotSettings.Planned(space), settings.EditOf(space));
            }

            if (!proceed)
            {
                yield break;
            }

            CadPlotPage page = CadPlotPage.Compute(c.Document, space, settings, display);

            if (page.IsEmpty)
            {
                c.Write("Nothing to plot: the plot area is empty.");
                yield break;
            }

            if (!c.Session.Request("-PLOT", c.Document, new CadPlotJob(space, settings, display, file)))
            {
                c.Write("Plotting to a file needs the window's PDF writer: nothing was written.");
                yield break;
            }

            c.Write("Effective plotting area:  " + Mm(page.Area.Width * page.Scale) + " wide by " + Mm(page.Area.Height * page.Scale) + " high");
            c.Write("Plot written to " + file + ".");
        }

        /// <summary>The prompts of -PLOT's detailed configuration after the device, each setting what it asks.</summary>
        private static IEnumerable<CadPrompt> PlotDetails(CadCommandContext c, CadSpace space, CadPlotSettings settings)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter paper size or", new[] { "?" }, settings.Paper.Label);

                if (c.Input.Is("?"))
                {
                    foreach (CadPaper each in CadPaper.Standard)
                    {
                        c.Write(each.Label);
                    }

                    continue;
                }

                if (c.Input.Kind == CadInputKind.Text)
                {
                    CadPaper paper = PaperNamed(c.Input.Text.Trim());

                    if (paper == null)
                    {
                        c.Write("Paper size \"" + c.Input.Text.Trim() + "\" not found.");
                        continue;
                    }

                    settings.Paper = paper;
                }

                break;
            }

            yield return CadPrompt.ForKeyword("Enter paper units", new[] { "Inches", "Millimeters" }, "Millimeters");
            bool inches = c.Input.Is("Inches");
            yield return CadPrompt.ForKeyword("Enter drawing orientation", new[] { "Portrait", "Landscape" }, settings.Landscape ? "Landscape" : "Portrait");
            settings.Landscape = c.Input.IsNone ? settings.Landscape : c.Input.Is("Landscape");
            yield return CadPrompt.ForKeyword("Plot upside down?", YesNo, settings.UpsideDown ? "Yes" : "No");
            settings.UpsideDown = c.Input.IsNone ? settings.UpsideDown : c.Input.Is("Yes");

            string[] areas = space.IsModel ? new[] { "Display", "Extents", "Limits", "Window" } : new[] { "Display", "Extents", "Layout", "Window" };
            CadPlotArea current = space.IsModel && settings.Area == CadPlotArea.Layout ? CadPlotArea.Extents : settings.Area;
            yield return CadPrompt.ForKeyword("Enter plot area", areas, current.ToString());

            if (!c.Input.IsNone)
            {
                settings.Area = (CadPlotArea)Enum.Parse(typeof(CadPlotArea), c.Input.Text);
            }

            if (settings.Area == CadPlotArea.Window)
            {
                DisplayBox window = settings.Window;
                GeoPoint2 first = window.IsEmpty ? default(GeoPoint2) : new GeoPoint2(window.MinX, window.MinY);
                GeoPoint2 second = window.IsEmpty ? default(GeoPoint2) : new GeoPoint2(window.MaxX, window.MaxY);
                yield return CadPrompt.ForPoint("Enter lower left corner of window", null, null, Corner(first));
                first = c.Input.IsPoint ? c.Input.Point : first;
                yield return CadPrompt.ForPoint("Enter upper right corner of window", first, null, Corner(second));
                second = c.Input.IsPoint ? c.Input.Point : second;
                settings.Window = new DisplayBox(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));
            }

            double unit = inches ? 25.4 : 1.0;

            while (true)
            {
                string scale = settings.FitToPaper ? "Fit" : Number(settings.PaperUnits / unit) + "=" + Number(settings.DrawingUnits);
                yield return CadPrompt.ForKeywordOrText("Enter plot scale (Plotted " + (inches ? "Inches" : "Millimeters") + "=Drawing Units) or", new[] { "Fit" }, scale);

                if (c.Input.Is("Fit"))
                {
                    settings.FitToPaper = true;
                }
                else if (c.Input.Kind == CadInputKind.Text)
                {
                    if (!TryRatio(c.Input.Text, out double paper, out double drawing))
                    {
                        c.Write("Requires a scale, as 1=20, or Fit.");
                        continue;
                    }

                    settings.FitToPaper = false;
                    settings.PaperUnits = paper * unit;
                    settings.DrawingUnits = drawing;
                }

                break;
            }

            while (true)
            {
                string offset = settings.Centered ? "Center" : Mm(settings.OffsetX / unit) + "," + Mm(settings.OffsetY / unit);
                yield return CadPrompt.ForKeywordOrText("Enter plot offset (x,y) or", new[] { "Center" }, offset);

                if (c.Input.Is("Center"))
                {
                    settings.Centered = true;
                }
                else if (c.Input.Kind == CadInputKind.Text)
                {
                    if (!TryPair(c.Input.Text, out double x, out double y))
                    {
                        c.Write("Requires an offset as x,y, or Center.");
                        continue;
                    }

                    settings.Centered = false;
                    settings.OffsetX = x * unit;
                    settings.OffsetY = y * unit;
                }

                break;
            }

            yield return CadPrompt.ForKeyword("Plot with plot styles?", YesNo, settings.PlotWithPlotStyles ? "Yes" : "No");
            settings.PlotWithPlotStyles = c.Input.IsNone ? settings.PlotWithPlotStyles : c.Input.Is("Yes");

            while (true)
            {
                string table = settings.StyleTable == CadPlotStyleTable.Monochrome ? "monochrome.ctb" : settings.StyleTable == CadPlotStyleTable.Grayscale ? "grayscale.ctb" : ".";
                yield return CadPrompt.ForKeywordOrText("Enter plot style table name (enter . for none) or", new[] { "?" }, table);

                if (c.Input.Is("?"))
                {
                    c.Write("monochrome.ctb");
                    c.Write("grayscale.ctb");
                    continue;
                }

                if (c.Input.Kind == CadInputKind.Text)
                {
                    string typed = c.Input.Text.Trim();
                    string bare = typed.EndsWith(".ctb", StringComparison.OrdinalIgnoreCase) ? typed.Substring(0, typed.Length - 4) : typed;

                    if (typed == ".")
                    {
                        settings.StyleTable = CadPlotStyleTable.None;
                    }
                    else if (string.Equals(bare, "monochrome", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.StyleTable = CadPlotStyleTable.Monochrome;
                    }
                    else if (string.Equals(bare, "grayscale", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.StyleTable = CadPlotStyleTable.Grayscale;
                    }
                    else
                    {
                        c.Write("Plot style table \"" + typed + "\" not found.");
                        continue;
                    }
                }

                break;
            }

            yield return CadPrompt.ForKeyword("Plot with lineweights?", YesNo, settings.PlotLineweights ? "Yes" : "No");
            settings.PlotLineweights = c.Input.IsNone ? settings.PlotLineweights : c.Input.Is("Yes");

            if (space.IsModel)
            {
                // A 2D plot is as displayed whatever is answered, as AutoCAD plots 2D Wireframe.
                yield return CadPrompt.ForKeyword("Enter shade plot setting", new[] { "As displayed", "legacy Wireframe", "legacy Hidden", "Visual styles", "Rendered" }, "As displayed");
            }
            else
            {
                yield return CadPrompt.ForKeyword("Scale lineweights with plot scale?", YesNo, settings.ScaleLineweights ? "Yes" : "No");
                settings.ScaleLineweights = c.Input.IsNone ? settings.ScaleLineweights : c.Input.Is("Yes");

                // Paper space plots after its viewports and shows its objects, whatever is answered.
                yield return CadPrompt.ForKeyword("Plot paper space first?", YesNo, "No");
                yield return CadPrompt.ForKeyword("Hide paperspace objects?", YesNo, "No");
            }
        }

        private static CadSpace SpaceNamed(CadDocument document, string name)
        {
            foreach (CadSpace each in document.Spaces)
            {
                if (string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return each;
                }
            }

            return null;
        }

        /// <summary>A sheet of the list by its label, its name, or the name a DXF keeps, letters of either case the same.</summary>
        private static CadPaper PaperNamed(string typed)
        {
            foreach (CadPaper paper in CadPaper.Standard)
            {
                if (string.Equals(typed, paper.Label, StringComparison.OrdinalIgnoreCase) || string.Equals(typed, paper.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typed, paper.CanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    return paper;
                }
            }

            return null;
        }

        /// <summary>The file a plot is offered as: the drawing's name and the space's beside the drawing, in Documents for a drawing of no file.</summary>
        private static string PlotFileName(CadDocument document, CadSpace space)
        {
            string folder = document.HasFile ? Path.GetDirectoryName(document.FileName) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string name = Path.GetFileNameWithoutExtension(document.Title) + "-" + space.Name;

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            return Path.Combine(folder ?? string.Empty, name + ".pdf");
        }

        /// <summary>A scale typed as 1=20 or 1:20, both numbers finite and above nought.</summary>
        private static bool TryRatio(string typed, out double paper, out double drawing)
        {
            paper = 0.0;
            drawing = 0.0;
            string[] parts = typed.Split('=', ':');
            return parts.Length == 2
                && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out paper)
                && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out drawing)
                && paper > 0.0 && drawing > 0.0 && !double.IsInfinity(paper) && !double.IsInfinity(drawing);
        }

        /// <summary>An offset typed as x,y, both finite.</summary>
        private static bool TryPair(string typed, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            string[] parts = typed.Split(',');
            return parts.Length == 2
                && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && !double.IsInfinity(x) && !double.IsInfinity(y) && !double.IsNaN(x) && !double.IsNaN(y);
        }

        private static string Corner(GeoPoint2 point) => point.X.ToString("0.000000", CultureInfo.InvariantCulture) + "," + point.Y.ToString("0.000000", CultureInfo.InvariantCulture);

        private static string Mm(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
