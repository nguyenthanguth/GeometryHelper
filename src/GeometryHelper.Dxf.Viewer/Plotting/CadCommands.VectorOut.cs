using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The writers of EXPORT that draw vectors, and the commands that stand for them. WMFOUT writes the objects selected as
    /// a Windows Metafile, PSOUT writes an area of the drawing as Encapsulated PostScript, as AutoCAD 2021's prompts and
    /// messages (read from its resource strings) ask it; STLOUT is refused, as the presenter has no solids; SVGOUT writes the
    /// objects as an SVG, a command of the presenter's own. Each is also a type of EXPORT's Export Data dialog (Metafile,
    /// Encapsulated PS, Lithography). The files are written here, with no window: the picture is the display list of the space
    /// worked in (<see cref="CadVectorPicture"/>).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The key of PSOUT's last answer to "What to plot" in <see cref="CadCommandSession.Remembered"/>: 0 Display, 1 Extents, 2 Limits, 3 View, 4 Window.</summary>
        private const string PostScriptAreaKey = "psout.area";

        /// <summary>The key of PSOUT's last size units: 0 inches, 1 millimetres.</summary>
        private const string PostScriptUnitsKey = "psout.units";

        /// <summary>PSOUT's standard values for output size, as AutoCAD 2021's console lists them (probed), in inches: the label, the width and the height; USER, the size Fit makes for an output of no size, last.</summary>
        private static readonly KeyValuePair<string, double[]>[] PostScriptSizes =
        {
            new KeyValuePair<string, double[]>("A", new[] { 8.0, 10.5 }),
            new KeyValuePair<string, double[]>("B", new[] { 10.0, 16.0 }),
            new KeyValuePair<string, double[]>("C", new[] { 16.0, 21.0 }),
            new KeyValuePair<string, double[]>("D", new[] { 21.0, 33.0 }),
            new KeyValuePair<string, double[]>("E", new[] { 33.0, 43.0 }),
            new KeyValuePair<string, double[]>("F", new[] { 28.0, 40.0 }),
            new KeyValuePair<string, double[]>("G", new[] { 11.0, 90.0 }),
            new KeyValuePair<string, double[]>("H", new[] { 28.0, 143.0 }),
            new KeyValuePair<string, double[]>("J", new[] { 34.0, 176.0 }),
            new KeyValuePair<string, double[]>("K", new[] { 40.0, 143.0 }),
            new KeyValuePair<string, double[]>("A4", new[] { 7.8, 11.2 }),
            new KeyValuePair<string, double[]>("A3", new[] { 10.7, 15.6 }),
            new KeyValuePair<string, double[]>("A2", new[] { 15.6, 22.4 }),
            new KeyValuePair<string, double[]>("A1", new[] { 22.4, 32.2 }),
            new KeyValuePair<string, double[]>("A0", new[] { 32.2, 45.9 }),
            new KeyValuePair<string, double[]>("USER", new[] { 7.5, 10.5 }),
        };

        internal static void AddVectorOutTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("WMFOUT", WmfOut));
            registry.Add(new CadCommand("PSOUT", PostScriptOut));
            registry.Add(new CadCommand("STLOUT", StlOut));
            registry.Add(new CadCommand("SVGOUT", SvgOut));
        }

        /// <summary>A file a prompt asked for.</summary>
        private sealed class OutFile
        {
            /// <summary>The file with its folder and its extension; null where the dialog was cancelled or the answer was no file.</summary>
            public string Path { get; set; }
        }

        /// <summary>
        /// The file of a writer: the window's Save dialog of the one type, or with no window or FILEDIA 0 "Enter ... file name
        /// &lt;file&gt;:", the folder of the drawing the home of a name with none, and the type's extension added to a name
        /// that has another.
        /// </summary>
        private static IEnumerable<CadPrompt> AskOutFile(
            CadCommandContext c,
            string request,
            string title,
            string type,
            string question,
            OutFile result
        )
        {
            string extension = CadExportJob.ExtensionsOf(type)[0];
            string offered = ImageFileName(c.Document, extension);
            string file;
            var job = new CadExportJob(title, new[] { type }, type, offered);

            if (c.Session.FileDialogs && c.Session.Request(request, c.Document, job))
            {
                result.Path = job.FileName;

                if (result.Path == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                while (true)
                {
                    yield return CadPrompt.ForString(question, offered);
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
            }

            if (!CadExportJob.ExtensionsOf(type).Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                file += extension;
            }

            result.Path = file;
        }

        /// <summary>Writes bytes to a file, or says "Can't open file." where it cannot be written; true where it was written.</summary>
        private static bool WriteOutFile(CadCommandContext c, string file, byte[] bytes)
        {
            try
            {
                File.WriteAllBytes(file, bytes);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                c.Write("Can't open file.");
                return false;
            }
        }

        // WMFOUT

        /// <summary>
        /// WMFOUT: the window's Create WMF File dialog or, with no window or FILEDIA 0, "Enter file name &lt;file&gt;:", then
        /// "Select objects:" and the objects selected written as a placeable Windows Metafile (<see cref="CadWmfWriter"/>):
        /// the box of the objects fitted to 20 000 units along its longer side, lines of their colours, line weights and
        /// dashes, fills, and the words of text. Nothing selected writes nothing; nothing is printed, and the drawing is not
        /// changed, so there is no undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> WmfOut(CadCommandContext c)
        {
            var file = new OutFile();

            foreach (
                CadPrompt prompt in AskOutFile(
                    c,
                    "WMFOUT",
                    "Create WMF File",
                    CadExportJob.Metafile,
                    "Enter file name",
                    file
                )
            )
            {
                yield return prompt;
            }

            if (file.Path == null)
            {
                yield break;
            }

            foreach (CadPrompt prompt in WriteMetafile(c, file.Path))
            {
                yield return prompt;
            }
        }

        /// <summary>"Select objects:", then the metafile of the objects written to a file already named; nothing selected writes nothing.</summary>
        private static IEnumerable<CadPrompt> WriteMetafile(CadCommandContext c, string file)
        {
            yield return CadPrompt.ForSelection();

            if (c.Input.Handles == null || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            CadVectorPicture picture = CadVectorPicture.Of(c.Document, c.Space, c.Input.Handles, DisplayBox.Empty);

            if (picture.IsEmpty)
            {
                c.Write("Nothing to export: the objects selected have no vector form.");
                yield break;
            }

            WriteOutFile(c, file, CadWmfWriter.Write(picture));
        }

        // SVGOUT

        /// <summary>
        /// SVGOUT, the presenter's own: the window's Create SVG File dialog or "Enter file name &lt;file&gt;:", then "Select
        /// objects or &lt;all objects and viewports&gt;:" and the objects, or all of the space, written as an SVG
        /// (<see cref="CadSvgWriter"/>).
        /// </summary>
        private static IEnumerable<CadPrompt> SvgOut(CadCommandContext c)
        {
            var file = new OutFile();

            foreach (
                CadPrompt prompt in AskOutFile(
                    c,
                    "SVGOUT",
                    "Create SVG File",
                    CadExportJob.Svg,
                    "Enter file name",
                    file
                )
            )
            {
                yield return prompt;
            }

            if (file.Path == null)
            {
                yield break;
            }

            yield return CadPrompt.ForSelection("Select objects or <all objects and viewports>");
            CadVectorPicture picture = CadVectorPicture.Of(c.Document, c.Space, c.Input.Handles, DisplayBox.Empty);

            if (picture.IsEmpty)
            {
                c.Write("Nothing to export: no objects to draw.");
                yield break;
            }

            WriteOutFile(c, file.Path, CadSvgWriter.Write(picture));
        }

        // STLOUT

        /// <summary>
        /// STLOUT, refused, as AutoCAD 2021's console answers a drawing of no solids (probed): "Select solids or watertight meshes:"
        /// asked again and again, Enter on no objects asking it once more, until Esc; what is selected, which can only be 2D
        /// objects, is "1 was filtered out" or "2 were filtered out". No file is written.
        /// </summary>
        private static IEnumerable<CadPrompt> StlOut(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForSelection("Select solids or watertight meshes");
                int removed = c.Input.Handles == null ? 0 : c.Input.Handles.Count;

                if (removed > 0)
                {
                    c.Write(
                        removed == 1
                            ? "1 was filtered out"
                            : removed.ToString(CultureInfo.InvariantCulture) + " were filtered out"
                    );
                }
            }
        }

        // PSOUT

        /// <summary>
        /// PSOUT: the window's Create PostScript File dialog or "Enter PostScript file name &lt;file&gt;:", then the questions
        /// of <see cref="PostScriptQuestions"/>.
        /// </summary>
        private static IEnumerable<CadPrompt> PostScriptOut(CadCommandContext c)
        {
            var file = new OutFile();

            foreach (
                CadPrompt prompt in AskOutFile(
                    c,
                    "PSOUT",
                    "Create PostScript File",
                    CadExportJob.PostScript,
                    "Enter PostScript file name",
                    file
                )
            )
            {
                yield return prompt;
            }

            if (file.Path == null)
            {
                yield break;
            }

            foreach (CadPrompt prompt in PostScriptQuestions(c, file.Path))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// The questions of PSOUT after its file, as AutoCAD 2021 words them: "What to plot -- Display, Extents, Limits, View, or
        /// Window &lt;D&gt;:" (View asks "Enter view name:", Window "Specify window for export" and its two corners), "Enter
        /// screen preview image type [None/EPSI/TIFF] &lt;None&gt;:" (and the preview's size for EPSI or TIFF, which the file
        /// does not hold), "Enter size units [Inches/Millimeters] &lt;Inches&gt;:", "Enter plotted Inches to drawing units (as
        /// P=D) or [Fit/? (help)] &lt;F&gt;:" (a scale as 1=48, or Fit) and "Enter the Size or Width,Height (in Inches)
        /// &lt;USER&gt;:" (a label of <see cref="PostScriptSizes"/>, A to K, A4 to A0 and USER, or two sizes; Fit lists the table
        /// before it), the answers to the area and the units kept for the next PSOUT. The area is written as Encapsulated
        /// PostScript (<see cref="CadEpsWriter"/>) and "Effective plotting area:  7.50 wide by 4.25 high" ends it. Fit scales
        /// the area to the output size, or, with USER, to AutoCAD's USER sheet, 7.5 by 10.5 inches, the output being the size
        /// the area then has; a scale and USER make the output the size of the area; a scale and a size centre the area in the
        /// size, cut by it.
        /// </summary>
        private static IEnumerable<CadPrompt> PostScriptQuestions(CadCommandContext c, string file)
        {
            CadSpace space = c.Space;
            string[] areaNames = { "Display", "Extents", "Limits", "View", "Window" };
            CadPlotSettings settings = CadPlotSettings.Read(c.Document, space);
            int area = (int)Kept(c.Session, PostScriptAreaKey, 0.0);
            area = area >= 0 && area < areaNames.Length ? area : 0;

            while (true)
            {
                yield return CadPrompt.ForString(
                    "What to plot -- Display, Extents, Limits, View, or Window",
                    areaNames[area].Substring(0, 1)
                );
                string typed = Typed(c, areaNames[area].Substring(0, 1)).Trim();
                int named =
                    typed.Length == 0
                        ? -1
                        : Array.FindIndex(areaNames, n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase));

                if (named >= 0)
                {
                    area = named;
                    break;
                }

                c.Write("Requires Display, Extents, Limits, View or Window.");
            }

            c.Remember(PostScriptAreaKey, area);
            settings.Area = new[]
            {
                CadPlotArea.Display,
                CadPlotArea.Extents,
                CadPlotArea.Limits,
                CadPlotArea.View,
                CadPlotArea.Window,
            }[area];

            if (area == 3)
            {
                yield return CadPrompt.ForString("Enter view name");
                string name = Typed(c, string.Empty).Trim();
                bool known = c.Document.Database.ViewTable.Any(v =>
                    v.IsPaperspaceView == !space.IsModel
                    && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)
                );

                if (!known)
                {
                    c.Write("Invalid view - output canceled.");
                    yield break;
                }

                settings.ViewName = name;
            }
            else if (area == 4)
            {
                c.Write("Specify window for export");
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
                var window = new DisplayBox(
                    Math.Min(first.X, second.X),
                    Math.Min(first.Y, second.Y),
                    Math.Max(first.X, second.X),
                    Math.Max(first.Y, second.Y)
                );

                if (!(window.Width > 0.0) || !(window.Height > 0.0))
                {
                    c.Write("Invalid window - output canceled.");
                    yield break;
                }

                settings.Window = window;
            }

            yield return CadPrompt.ForKeyword(
                "Enter screen preview image type",
                new[] { "None", "EPSI", "TIFF" },
                "None"
            );

            if (c.Input.Is("EPSI") || c.Input.Is("TIFF"))
            {
                yield return CadPrompt.ForKeyword(
                    "Enter size of preview image (128x128 is standard)",
                    new[] { "128", "256", "512" },
                    "128"
                );
                c.Write("The file holds no screen preview: the picture alone is written.");
            }

            bool millimetres = Kept(c.Session, PostScriptUnitsKey, 0.0) == 1.0;
            yield return CadPrompt.ForKeyword(
                "Enter size units",
                new[] { "Inches", "Millimeters" },
                millimetres ? "Millimeters" : "Inches"
            );
            millimetres = c.Input.IsNone ? millimetres : c.Input.Is("Millimeters");
            c.Remember(PostScriptUnitsKey, millimetres ? 1.0 : 0.0);
            string units = millimetres ? "Millimeters" : "Inches";
            double perUnit = millimetres ? 25.4 : 1.0;
            DisplayBox display = c.Session.View?.VisibleBox ?? DisplayBox.Empty;
            DisplayBox plotted = CadPlotPage.Compute(c.Document, space, settings, display).Area;

            if (plotted.IsEmpty || !(plotted.Width > 0.0) || !(plotted.Height > 0.0))
            {
                c.Write("Nothing to export: the area to plot is empty.");
                yield break;
            }

            double ratio = 0.0;

            while (true)
            {
                yield return CadPrompt.ForString(
                    "Enter plotted "
                        + (millimetres ? "Millimeters" : "Inches")
                        + " to drawing units (as P=D) or [Fit/? (help)]",
                    "F"
                );
                string typed = Typed(c, "F").Trim();

                if (typed.Length == 0 || "Fit".StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                {
                    // Fit lists the standard sizes before the size is asked, as AutoCAD's console does.
                    WriteSizes(c, perUnit);
                    break;
                }

                if (typed == "?")
                {
                    c.Write(
                        "To specify an explicit scale enter the number of output "
                            + units
                            + " per number of Drawing Units, for example: \"2=1\" or choose Fit to scale"
                    );
                    c.Write("the output to be as large as possible for the selected paper size.");
                    continue;
                }

                if (TryScale(typed, out ratio))
                {
                    break;
                }

                c.Write("Requires two positive numbers as P=D, or Fit.");
            }

            double outWidth = 0.0;
            double outHeight = 0.0;

            while (true)
            {
                yield return CadPrompt.ForString("Enter the Size or Width,Height (in " + units + ")", "USER");
                string typed = Typed(c, "USER").Trim();

                if (typed.Length == 0 || string.Equals(typed, "USER", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (typed == "?")
                {
                    WriteSizes(c, perUnit);
                    continue;
                }

                string label = typed;
                KeyValuePair<string, double[]> sheet = PostScriptSizes.FirstOrDefault(s =>
                    string.Equals(s.Key, label, StringComparison.OrdinalIgnoreCase)
                );

                if (sheet.Key != null)
                {
                    outWidth = sheet.Value[0] * perUnit;
                    outHeight = sheet.Value[1] * perUnit;
                    break;
                }

                string[] parts = typed.Split(',');

                if (
                    parts.Length == 2
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double h)
                    && w > 0.0
                    && h > 0.0
                )
                {
                    outWidth = w;
                    outHeight = h;
                    break;
                }

                c.Write("Invalid size label");
            }

            // The scale in output units to a drawing unit, and the output box in output units.
            bool fit = ratio <= 0.0;
            double scale = ratio;

            if (outWidth <= 0.0)
            {
                if (fit)
                {
                    double[] user = PostScriptSizes[PostScriptSizes.Length - 1].Value;
                    scale = Math.Min(user[0] * perUnit / plotted.Width, user[1] * perUnit / plotted.Height);
                    outWidth = plotted.Width * scale;
                    outHeight = plotted.Height * scale;
                }
                else
                {
                    outWidth = plotted.Width * scale;
                    outHeight = plotted.Height * scale;
                }
            }
            else if (fit)
            {
                scale = Math.Min(outWidth / plotted.Width, outHeight / plotted.Height);
            }

            double points = PostScriptPointsPerUnit(millimetres);
            CadVectorPicture picture = CadVectorPicture.Of(c.Document, c.Space, null, plotted);

            if (picture.IsEmpty)
            {
                c.Write("Nothing to export: the area to plot holds no objects.");
                yield break;
            }

            string title = Path.GetFileNameWithoutExtension(c.Document.Title ?? "Drawing1");

            if (
                !WriteOutFile(
                    c,
                    file,
                    CadEpsWriter.Write(picture, plotted, scale * points, outWidth * points, outHeight * points, title)
                )
            )
            {
                yield break;
            }

            c.Write(
                "Effective plotting area:  "
                    + (plotted.Width * scale).ToString("0.00", CultureInfo.InvariantCulture)
                    + " wide by "
                    + (plotted.Height * scale).ToString("0.00", CultureInfo.InvariantCulture)
                    + " high"
            );
        }

        /// <summary>"Standard values for output size": the table of <see cref="PostScriptSizes"/> in the units chosen, as AutoCAD's console prints it.</summary>
        private static void WriteSizes(CadCommandContext c, double perUnit)
        {
            c.Write("Standard values for output size");
            c.Write("Size      Width      Height");

            foreach (KeyValuePair<string, double[]> size in PostScriptSizes)
            {
                c.Write(
                    size.Key.PadRight(11)
                        + (size.Value[0] * perUnit).ToString("0.00", CultureInfo.InvariantCulture).PadLeft(5)
                        + "      "
                        + (size.Value[1] * perUnit).ToString("0.00", CultureInfo.InvariantCulture).PadLeft(5)
                );
            }
        }

        /// <summary>The points of PostScript to an output unit: 72 to an inch, 72/25.4 to a millimetre.</summary>
        private static double PostScriptPointsPerUnit(bool millimetres) =>
            millimetres ? CadEpsWriter.PointsPerInch / 25.4 : CadEpsWriter.PointsPerInch;

        /// <summary>A scale as P=D, "1=48" or "1:48", "2=1" or one number, which stands for that many output units to a drawing unit; false for anything else.</summary>
        private static bool TryScale(string typed, out double ratio)
        {
            ratio = 0.0;
            string[] parts = typed.Split(new[] { '=', ':' });

            if (
                parts.Length > 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double plotted)
                || !(plotted > 0.0)
            )
            {
                return false;
            }

            double drawn = 1.0;

            if (
                parts.Length == 2
                && (
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out drawn)
                    || !(drawn > 0.0)
                )
            )
            {
                return false;
            }

            ratio = plotted / drawn;
            return true;
        }
    }
}
