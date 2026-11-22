using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The drawing NEW starts from: the presenter's own metric template, made in code with the settings of AutoCAD's
    /// acadiso.dwt (not its file). A drawing of R2013 in millimetres, its linear units decimal to four places and its angles
    /// decimal degrees to none, its limits an A3 sheet of 420 by 297, its text 2.5 high, its points dots, and its dimensions
    /// drawn in ISO-25: arrowheads and text 2.5, the text above the line, two decimal places and a decimal comma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every variable the template sets is written into the header, those the library would read the same where the header
    /// gives none among them, so that AutoCAD, which reads a variable the header leaves out as its own imperial drawing has it
    /// (MEASUREMENT 0, DIMSTYLE Standard, DIMASZ 0.18), opens the drawing as metric too.
    /// </para>
    /// <para>
    /// The tables are layer 0, the linetypes ByBlock, ByLayer and Continuous, the text styles Standard (arial.ttf, as
    /// acadiso.dwt of AutoCAD 2021 has it) and Annotative (arial.ttf too), and the dimension styles Standard and
    /// ISO-25. AutoCAD adds Defpoints with the first dimension, and the multileader and table styles Standard when it opens
    /// the drawing; the library reads those styles but does not write them, so MLEADER has none to draw in until a drawing of
    /// AutoCAD's gives it one.
    /// </para>
    /// </remarks>
    public static class CadTemplate
    {
        /// <summary>The name of the dimension style the template makes current.</summary>
        public const string DimensionStyle = "ISO-25";

        /// <summary>
        /// Makes a new drawing of the metric template: R2013 (AC1027), every setting of <see cref="CadTemplate"/> in its header
        /// and tables, nothing drawn.
        /// </summary>
        /// <returns>The drawing.</returns>
        public static DxfDatabase CreateMetric()
        {
            var database = new DxfDatabase("AC1027");

            // The units, limits, text height and point style of acadiso.dwt.
            Pin(database, "$MEASUREMENT", 70, "1");
            Pin(database, "$INSUNITS", 70, "4");
            Pin(database, "$LUNITS", 70, "2");
            Pin(database, "$LUPREC", 70, "4");
            Pin(database, "$AUNITS", 70, "0");
            Pin(database, "$AUPREC", 70, "0");
            Pin(database, "$LTSCALE", 40, 1.0);
            Pin(database, "$PDMODE", 70, "0");
            Pin(database, "$PDSIZE", 40, 0.0);
            Pin(database, "$TEXTSIZE", 40, 2.5);
            database.SetHeader("$LIMMIN", new DxfTypedValue(10, "0.0"), new DxfTypedValue(20, "0.0"));
            database.SetHeader("$LIMMAX", new DxfTypedValue(10, "420.0"), new DxfTypedValue(20, "297.0"));
            Pin(database, "$TEXTSTYLE", 7, "Standard");
            Pin(database, "$CELTYPE", 6, "ByLayer");
            Pin(database, "$CLAYER", 8, "0");

            database.TextStyleTable["Standard"].FileName = "arial.ttf";

            var annotative = new DxfTextStyleTableRecord { Name = "Annotative", FileName = "arial.ttf" };
            database.TextStyleTable.Add(annotative);
            annotative.Annotative = DxfAnnotativeStates.True;

            // ISO-25, current and written into the header where it differs from AutoCAD's own style (acad.dwt), comes with the
            // library's new drawing, as acadiso.dwt has it.
            Pin(database, "$DIMTXSTY", 7, "Standard");
            Pin(database, "$DIMSTYLE", 2, DimensionStyle);
            return database;
        }

        /// <summary>
        /// Makes a new drawing of the presenter's own imperial template, as AutoCAD's "Open with no Template - Imperial" starts
        /// one (the settings of acad.dwt, not its file): R2013 in inches, MEASUREMENT 0, its limits a sheet of 12 by 9, its text
        /// 0.2 high, and its dimensions drawn in Standard, arrowheads and text 0.18, four decimal places and a decimal point.
        /// The layer 0, the linetypes, the text styles Standard and Annotative are those of <see cref="CreateMetric"/>.
        /// </summary>
        /// <returns>The drawing.</returns>
        public static DxfDatabase CreateImperial()
        {
            var database = new DxfDatabase("AC1027");
            Pin(database, "$MEASUREMENT", 70, "0");
            Pin(database, "$INSUNITS", 70, "1");
            Pin(database, "$LUNITS", 70, "2");
            Pin(database, "$LUPREC", 70, "4");
            Pin(database, "$AUNITS", 70, "0");
            Pin(database, "$AUPREC", 70, "0");
            Pin(database, "$LTSCALE", 40, 1.0);
            Pin(database, "$PDMODE", 70, "0");
            Pin(database, "$PDSIZE", 40, 0.0);
            Pin(database, "$TEXTSIZE", 40, 0.2);
            database.SetHeader("$LIMMIN", new DxfTypedValue(10, "0.0"), new DxfTypedValue(20, "0.0"));
            database.SetHeader("$LIMMAX", new DxfTypedValue(10, "12.0"), new DxfTypedValue(20, "9.0"));
            Pin(database, "$TEXTSTYLE", 7, "Standard");
            Pin(database, "$CELTYPE", 6, "ByLayer");
            Pin(database, "$CLAYER", 8, "0");

            database.TextStyleTable["Standard"].FileName = "arial.ttf";

            var annotative = new DxfTextStyleTableRecord { Name = "Annotative", FileName = "arial.ttf" };
            database.TextStyleTable.Add(annotative);
            annotative.Annotative = DxfAnnotativeStates.True;

            // The library's new drawing has ISO-25 current; AutoCAD's own Standard, current here, is written into the header.
            Pin(database, "$DIMTXSTY", 7, "Standard");
            database.SetDimstyleData(database.DimStyleTable["Standard"]);
            database.Dimstyle = "Standard";
            return database;
        }

        /// <summary>The file name of the sample template that ships in the DrawingTemplates folder, written from <see cref="CreateSample"/>.</summary>
        public const string SampleFileName = "GeometryHelper-mm.dwt";

        /// <summary>The description the sample template carries in its template section, the text Select Template shows under its name.</summary>
        public const string SampleDescription =
            "GeometryHelper metric sample: millimetres, layers for a Tekla Structures detail, text styles in ghsimplex and Arial, "
            + "the dimension style GH (text 2.5) and an A3 layout.";

        /// <summary>
        /// Makes the drawing of the sample template <c>GeometryHelper-mm.dwt</c>, for a detail drawn at 1:1 in millimetres and
        /// printed on an A3 sheet: R2013, MEASUREMENT 1, INSUNITS 4 (millimetres), distances to two decimal places, limits of
        /// 42 000 by 29 700 (an A3 sheet at 1:100), LTSCALE and PSLTSCALE 1 (the dashes of the presenter's own linetypes are
        /// millimetres: CENTER has a dash of 12), and nothing drawn.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The layers (colours are AutoCAD's index, lineweights millimetres): 0; Axis red, CENTER, 0.18; Outline white,
        /// Continuous, 0.50; Thin 8, 0.18; Hidden 6, HIDDEN, 0.25; Dimension 3, 0.18; Text 2, 0.25; Hatch 9, 0.13; Rebar 4,
        /// 0.35; Title block white, 0.35; Viewport 8, not plotted; and Defpoints, white and not plotted. The linetypes are those
        /// of <see cref="CadLinetypeFile.BuiltIn"/> (GeometryHelper.lin), nothing of Autodesk's.
        /// </para>
        /// <para>
        /// The text styles all draw in the presenter's own font or in Arial: Standard, Notes and Titles in ghsimplex.shx, height
        /// 0, width factor 0.8; Arial in arial.ttf. The dimension style GH, current, has arrowheads closed and filled 2.5 long,
        /// text 2.5 in Standard above the line and aligned with it, extension lines 1.5 off the object and 1.5 past the line,
        /// no decimal places, millimetres (DIMLUNIT 2) and every colour and lineweight by layer; GH-Annotative is GH made
        /// annotative. The multileader style GH, current, has text and arrowheads 2.5 and Standard beside it. The layouts are
        /// Model and A3: a sheet of 420 by 297 on its side, in millimetres, with no plotter.
        /// </para>
        /// <para>
        /// AutoCAD's own ISO-25 dimension style and its Annotative text style are not kept: the sample holds what it lists.
        /// </para>
        /// </remarks>
        /// <returns>The drawing.</returns>
        public static DxfDatabase CreateSample()
        {
            var database = new DxfDatabase("AC1027");

            Pin(database, "$MEASUREMENT", 70, "1");
            Pin(database, "$INSUNITS", 70, "4");
            Pin(database, "$LUNITS", 70, "2");
            Pin(database, "$LUPREC", 70, "2");
            Pin(database, "$AUNITS", 70, "0");
            Pin(database, "$AUPREC", 70, "0");
            Pin(database, "$LTSCALE", 40, 1.0);
            Pin(database, "$PSLTSCALE", 70, "1");
            Pin(database, "$PDMODE", 70, "0");
            Pin(database, "$PDSIZE", 40, 0.0);
            Pin(database, "$TEXTSIZE", 40, 2.5);
            database.SetHeader("$LIMMIN", new DxfTypedValue(10, "0.0"), new DxfTypedValue(20, "0.0"));
            database.SetHeader("$LIMMAX", new DxfTypedValue(10, "42000.0"), new DxfTypedValue(20, "29700.0"));
            Pin(database, "$TEXTSTYLE", 7, "Standard");
            Pin(database, "$CELTYPE", 6, "ByLayer");
            Pin(database, "$CLAYER", 8, "0");

            AddSampleTextStyles(database);
            AddSampleLayers(database);
            AddSampleDimensionStyles(database);
            AddSampleMultileaderStyles(database);
            SetUpSampleLayout(database);
            return database;
        }

        private static void AddSampleTextStyles(DxfDatabase database)
        {
            DxfTextStyleTable styles = database.TextStyleTable;

            styles["Standard"].FileName = "ghsimplex.shx";
            styles["Standard"].XScale = 0.8;

            foreach (string name in new[] { "Notes", "Titles" })
            {
                styles.Add(
                    new DxfTextStyleTableRecord
                    {
                        Name = name,
                        FileName = "ghsimplex.shx",
                        XScale = 0.8,
                    }
                );
            }

            var arial = new DxfTextStyleTableRecord { Name = "Arial", FileName = "arial.ttf" };
            styles.Add(arial);
            arial.Font = new DxfFontDescriptor("Arial", false, false, 0, 34);
        }

        private static void AddSampleLayers(DxfDatabase database)
        {
            AddSampleLayer(database, "Axis", 1, "CENTER", 0.18, true);
            AddSampleLayer(database, "Outline", 7, null, 0.50, true);
            AddSampleLayer(database, "Thin", 8, null, 0.18, true);
            AddSampleLayer(database, "Hidden", 6, "HIDDEN", 0.25, true);
            AddSampleLayer(database, "Dimension", 3, null, 0.18, true);
            AddSampleLayer(database, "Text", 2, null, 0.25, true);
            AddSampleLayer(database, "Hatch", 9, null, 0.13, true);
            AddSampleLayer(database, "Rebar", 4, null, 0.35, true);
            AddSampleLayer(database, "Title block", 7, null, 0.35, true);
            AddSampleLayer(database, "Viewport", 8, null, 0.25, false);
            AddSampleLayer(database, "Defpoints", 7, null, 0.25, false);
        }

        private static void AddSampleLayer(
            DxfDatabase database,
            string name,
            short colorIndex,
            string linetype,
            double lineWeight,
            bool plot
        )
        {
            var record = new DxfLayerTableRecord
            {
                Name = name,
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, colorIndex),
                LineWeight = database.GetNearestLineWeight((int)Math.Round(lineWeight * 100.0)),
            };

            if (linetype != null)
            {
                record.LinetypeName = LinetypeOf(database, linetype);
            }

            database.LayerTable.Add(record);
            record.IsPlottable = plot;
        }

        // The linetype a layer of the sample names, loaded from the built-in set where the drawing lacks it; null for
        // Continuous, or for a name the built-in set does not have.
        private static string LinetypeOf(DxfDatabase database, string name)
        {
            if (database.LinetypeTable.Has(name))
            {
                return string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : database.LinetypeTable[name].Name;
            }

            CadLinetypeDefinition own = CadLinetypeFile.BuiltIn.FirstOrDefault(l =>
                string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
            );

            if (own == null || own.Fault != null)
            {
                return null;
            }

            try
            {
                return CadLinetypes.Add(database, own).Name;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void AddSampleDimensionStyles(DxfDatabase database)
        {
            DxfDimStyleTable styles = database.DimStyleTable;
            DxfDimStyleTableRecord gh = SampleDimensionStyle(database, "GH");
            DxfDimStyleTableRecord annotative = SampleDimensionStyle(database, "GH-Annotative");

            // Standard stays, as AutoCAD always has one, given the look of GH so that no style in the file is in inches.
            GiveSampleDimensionValues(database, styles["Standard"]);
            styles.Add(gh);
            styles.Add(annotative);
            annotative.Annotative = DxfAnnotativeStates.True;

            // GH is current, in the header too, as AutoCAD keeps the current style's variables there; ISO-25 of the library's new
            // drawing can go once it is not.
            database.SetDimstyleData(gh);
            database.Dimstyle = "GH";
            styles["ISO-25"].Erase();
        }

        private static DxfDimStyleTableRecord SampleDimensionStyle(DxfDatabase database, string name)
        {
            var style = new DxfDimStyleTableRecord { Name = name };
            GiveSampleDimensionValues(database, style);
            return style;
        }

        /// <summary>The look of the sample's dimension styles: AutoCAD's own values but for those the sample sets, which a drawing in millimetres wants.</summary>
        private static void GiveSampleDimensionValues(DxfDatabase database, DxfDimStyleTableRecord style)
        {
            style.Dimtxsty = database.TextStyleTable["Standard"];
            style.Dimtxt = 2.5;
            style.Dimasz = 2.5;
            style.Dimexo = 1.5;
            style.Dimexe = 1.5;
            style.Dimgap = 0.625;
            style.Dimdli = 3.75;
            style.Dimcen = 2.5;
            style.Dimdec = 0;
            style.Dimlunit = 2;
            style.Dimdsep = '.';
            style.Dimscale = 1.0;
            style.Dimtad = 1;
            style.Dimtih = false;
            style.Dimtoh = false;
            style.Dimclrd = DxfColor.ByLayer;
            style.Dimclre = DxfColor.ByLayer;
            style.Dimclrt = DxfColor.ByLayer;
            style.Dimlwd = DxfLineWeight.ByLayer;
            style.Dimlwe = DxfLineWeight.ByLayer;
        }

        private static void AddSampleMultileaderStyles(DxfDatabase database)
        {
            // The library's new drawing has no multileader styles; the dictionary is made, and Standard put in it as AutoCAD keeps it.
            database.NamedObjectsDictionary.SetAt("ACAD_MLEADERSTYLE", new DxfDBDictionary());
            DxfDBDictionary styles = database.MLeaderStyleDictionary;
            styles.SetAt("Standard", new DxfMLeaderStyle());
            styles.SetAt(
                "GH",
                new DxfMLeaderStyle
                {
                    TextHeight = 2.5,
                    ArrowSize = 2.5,
                    LandingGap = 0.625,
                    DoglegLength = 2.5,
                    TextStyle = database.TextStyleTable["Standard"],
                }
            );
            database.MLeaderstyle = "GH";
        }

        private static void SetUpSampleLayout(DxfDatabase database)
        {
            // Layout1 of the library's new drawing becomes A3, on a sheet of 420 by 297 millimetres on its side.
            DxfLayout layout = database.Layouts[1];
            layout.LayoutName = "A3";
            layout.CanonicalMediaName = "ISO_A3_(420.00_x_297.00_MM)";
            layout.PlotPaperUnits = DxfPlotPaperUnit.Millimeters;
            layout.PlotType = DxfPlotType.Layout;
        }

        /// <summary>The file types a template is chosen among, as the Select Template dialog lists them.</summary>
        public static readonly IReadOnlyList<string> Extensions = new[] { ".dwt", ".dwg", ".dxf" };

        /// <summary>
        /// Lists the templates of a folder and its subfolders: the .dwt, .dwg and .dxf files, in the order of the walk
        /// (<see cref="CadSupportWalk"/>): the folder's own files by name (letters of either case the same, then as written) and
        /// then by extension, then the files of its subfolders in the same way, each subfolder after the folder it is in. Of two
        /// files of one name (sheet.dwt in the folder and in a subfolder) the first is listed, so the one at the top wins;
        /// empty where the folder is not there.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns>The full paths of the files.</returns>
        public static IReadOnlyList<string> FindIn(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    return Array.Empty<string>();
                }

                var found = new List<string>();

                foreach (string each in CadSupportWalk.Folders(folder))
                {
                    found.AddRange(
                        CadSupportWalk
                            .FilesOf(each, name =>
                                Extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)
                            )
                            .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                            .ThenBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.Ordinal)
                            .ThenBy(f => Path.GetExtension(f).ToLowerInvariant(), StringComparer.Ordinal)
                    );
                }

                return CadSupportWalk.FirstOfEachName("templates", found);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Reads the description of a template, the text AutoCAD's Template Options writes into a DWG's template section. A
        /// file that is no DWG (a DXF), one with no description, and one that cannot be read have none.
        /// </summary>
        /// <param name="file">The path of the template.</param>
        /// <returns>The description; empty where there is none.</returns>
        public static string DescriptionOf(string file)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file) || !CadDrawingFile.IsDwg(file))
                {
                    return string.Empty;
                }

                return GeometryHelper.Dwg.DwgContainer.Read(File.ReadAllBytes(file)).TemplateDescription;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                // A description shown beside a file in a list: a file that does not read has none, and the list goes on.
                GeometryHelperLog.Debug("CadTemplate: no description read from " + file + ": " + exception.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Finds the file of a template named as typed: as typed, else in the template folder, else, for a name with no folder, the
        /// first of that name in the folder's subfolders; a name with no extension is given .dwt, as AutoCAD does.
        /// </summary>
        /// <param name="typed">The name or path.</param>
        /// <param name="folder">The template folder.</param>
        /// <returns>The full path of the file; null where there is none.</returns>
        public static string Find(string typed, string folder)
        {
            string name = (typed ?? string.Empty).Trim().Trim('"');

            if (name.Length == 0)
            {
                return null;
            }

            if (!Path.HasExtension(name))
            {
                name += ".dwt";
            }

            try
            {
                if (File.Exists(name))
                {
                    return Path.GetFullPath(name);
                }

                if (!Path.IsPathRooted(name) && !string.IsNullOrWhiteSpace(folder))
                {
                    string inFolder = Path.Combine(folder, name);

                    if (File.Exists(inFolder))
                    {
                        return Path.GetFullPath(inFolder);
                    }

                    // A name with no folder is looked for in the subfolders too, the first of the walk winning; one with a folder is not.
                    return name.IndexOfAny(new[] { '\\', '/' }) >= 0
                        ? null
                        : FindIn(folder)
                            .FirstOrDefault(f =>
                                string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase)
                            );
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return null;
            }

            return null;
        }

        /// <summary>Writes a variable into the header, whether or not the library reads the same where it gives none.</summary>
        private static void Pin(DxfDatabase database, string name, int code, string value) =>
            database.SetHeader(name, new DxfTypedValue(code, value));

        private static void Pin(DxfDatabase database, string name, int code, double value) =>
            Pin(database, name, code, value.ToString("R", CultureInfo.InvariantCulture));
    }
}
