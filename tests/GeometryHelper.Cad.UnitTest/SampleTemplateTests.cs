using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The sample template that ships in the DrawingTemplates folder, GeometryHelper-mm.dwt: it is there, Select Template lists
    /// it with its description, it holds the layers, text styles, dimension styles, multileader style, layouts and variables
    /// <see cref="CadTemplate.CreateSample"/> makes in code, and it is a DWG that reads back the same. The file is written
    /// from the code: set the environment variable GEOMETRYHELPER_WRITE_SAMPLE_TEMPLATE to the full path of the file to write
    /// and run <see cref="TheSampleTemplateIsWrittenWhereTheVariableSays"/>.
    /// </summary>
    public class SampleTemplateTests
    {
        private const string WriteVariable = "GEOMETRYHELPER_WRITE_SAMPLE_TEMPLATE";

        private static string Shipped =>
            Path.Combine(
                CadSupportFolders.Default.FolderOf(CadSupportKind.DrawingTemplates),
                CadTemplate.SampleFileName
            );

        private static DxfDatabase Open() => CadDrawingFile.Open(Shipped);

        private static string Name(DxfSymbolTableRecord record) => record == null ? "(none)" : record.Name;

        private static string Value(object value)
        {
            switch (value)
            {
                case null:
                    return "(none)";
                case DxfSymbolTableRecord record:
                    return record.Name;
                case DxfColor color:
                    return color.ToString();
                case double number:
                    return Math.Round(number, 6).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        /// <summary>The meaningful content of a drawing, one line each: the variables, layers, styles and layouts. Handles, times and GUIDs are not in it.</summary>
        private static IReadOnlyList<string> Summary(DxfDatabase database)
        {
            var lines = new List<string>
            {
                "MEASUREMENT " + database.Measurement,
                "INSUNITS " + database.Insunits,
                "LUNITS " + database.Lunits,
                "LUPREC " + database.Luprec,
                "LTSCALE " + Value(database.Ltscale),
                "PSLTSCALE " + database.Psltscale,
                "LIMMIN " + database.Limmin,
                "LIMMAX " + database.Limmax,
                "TEXTSTYLE " + database.Textstyle,
                "CELTYPE " + database.Celtype,
                "CLAYER " + database.Clayer,
                "DIMSTYLE " + database.Dimstyle,
                "CMLEADERSTYLE " + database.MLeaderstyle,
            };

            lines.AddRange(
                database.LayerTable.Select(l =>
                    string.Join(
                        " ",
                        "LAYER",
                        l.Name,
                        l.Color,
                        l.LinetypeName,
                        l.LineWeight,
                        l.IsPlottable,
                        l.IsOff,
                        l.IsFrozen,
                        l.IsLocked
                    )
                )
            );
            lines.AddRange(database.LinetypeTable.Select(l => "LINETYPE " + l.Name));
            lines.AddRange(
                database.TextStyleTable.Select(t =>
                    string.Join(
                        " ",
                        "TEXTSTYLE",
                        t.Name,
                        t.FileName,
                        t.BigFontFileName,
                        Value(t.TextSize),
                        Value(t.XScale),
                        Value(t.ObliquingAngle),
                        t.Font.TypeFace,
                        t.Annotative
                    )
                )
            );

            foreach (DxfDimStyleTableRecord style in database.DimStyleTable)
            {
                IEnumerable<string> variables = typeof(DxfDimStyleTableRecord)
                    .GetProperties()
                    .Where(p =>
                        p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.GetIndexParameters().Length == 0
                    )
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => p.Name + "=" + Value(p.GetValue(style)));
                lines.Add("DIMSTYLE " + style.Name + " " + style.Annotative + " " + string.Join(" ", variables));
            }

            foreach (DxfDBDictionaryEntry entry in database.MLeaderStyleDictionary.Cast<DxfDBDictionaryEntry>())
            {
                var style = (DxfMLeaderStyle)entry.Value;
                lines.Add(
                    string.Join(
                        " ",
                        "MLEADERSTYLE",
                        entry.Key,
                        Value(style.TextHeight),
                        Value(style.ArrowSize),
                        Value(style.LandingGap),
                        Value(style.DoglegLength),
                        Name(style.TextStyle),
                        style.Annotative
                    )
                );
            }

            lines.AddRange(
                database.Layouts.Select(l =>
                    string.Join(
                        " ",
                        "LAYOUT",
                        l.LayoutName,
                        l.TabOrder,
                        l.CanonicalMediaName,
                        l.PlotPaperSize,
                        l.PlotPaperUnits,
                        l.PlotRotation,
                        l.PlotType
                    )
                )
            );
            return lines;
        }

        [Fact]
        public void TheSampleTemplateIsWrittenWhereTheVariableSays()
        {
            string target = Environment.GetEnvironmentVariable(WriteVariable);

            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            Assert.True(Path.IsPathRooted(target), WriteVariable + " is the full path of the file to write.");
            CadDocument
                .From(CadTemplate.CreateSample(), CadTemplate.SampleFileName)
                .SaveAsTemplate(target, CadTemplate.SampleDescription, true, null);
            Assert.True(File.Exists(target));
        }

        [Fact]
        public void TheSampleTemplate_IsInTheDrawingTemplatesFolder_AndSelectTemplateListsIt()
        {
            string folder = CadSupportFolders.Default.FolderOf(CadSupportKind.DrawingTemplates);

            Assert.True(File.Exists(Shipped), Shipped + " is not there.");
            Assert.Contains(Shipped, CadTemplate.FindIn(folder));
            Assert.Equal(Shipped, CadTemplate.Find("GeometryHelper-mm", folder));
        }

        [Fact]
        public void TheSampleTemplate_IsADwgOfAutoCad2018_WithItsDescription()
        {
            Assert.True(CadDrawingFile.IsDwg(Shipped));
            Assert.Equal("AC1032", Encoding.ASCII.GetString(File.ReadAllBytes(Shipped), 0, 6));
            Assert.Equal(CadTemplate.SampleDescription, CadTemplate.DescriptionOf(Shipped));
            Assert.Contains("millimetres", CadTemplate.SampleDescription);
        }

        [Fact]
        public void TheSampleTemplate_ReadsBackAsTheDrawingTheCodeMakes()
        {
            Assert.Equal(Summary(CadTemplate.CreateSample()), Summary(Open()));
        }

        [Fact]
        public void TheSampleTemplate_IsMetric_InMillimetres_WithItsVariables()
        {
            DxfDatabase database = Open();

            Assert.Equal(DxfMeasurementValue.Metric, database.Measurement);
            Assert.Equal(DxfUnits.Millimeters, database.Insunits);
            Assert.Equal(2, database.Lunits);
            Assert.Equal(2, database.Luprec);
            Assert.Equal(1.0, database.Ltscale, 6);
            Assert.True(database.Psltscale);
            Assert.Equal(42000.0, database.Limmax.X, 6);
            Assert.Equal(29700.0, database.Limmax.Y, 6);
            Assert.Equal("0", database.Clayer);
            Assert.Equal("ByLayer", database.Celtype);
            Assert.Equal("Standard", database.Textstyle);
            Assert.Equal("GH", database.Dimstyle);
            Assert.Equal("GH", database.MLeaderstyle);
        }

        [Fact]
        public void TheSampleTemplate_HoldsTheseLayers_ColourLinetypeLineweightAndPlot()
        {
            DxfDatabase database = Open();

            Assert.Equal(
                new[]
                {
                    "0|7|Continuous|ByLineWeightDefault|True",
                    "Axis|1|CENTER|LineWeight018|True",
                    "Outline|7|Continuous|LineWeight050|True",
                    "Thin|8|Continuous|LineWeight018|True",
                    "Hidden|6|HIDDEN|LineWeight025|True",
                    "Dimension|3|Continuous|LineWeight018|True",
                    "Text|2|Continuous|LineWeight025|True",
                    "Hatch|9|Continuous|LineWeight013|True",
                    "Rebar|4|Continuous|LineWeight035|True",
                    "Title block|7|Continuous|LineWeight035|True",
                    "Viewport|8|Continuous|LineWeight025|False",
                    "Defpoints|7|Continuous|LineWeight025|False",
                },
                database.LayerTable.Select(l =>
                    string.Join("|", l.Name, l.ColorIndex, l.LinetypeName, l.LineWeight, l.IsPlottable)
                )
            );
            Assert.All(database.LayerTable, l => Assert.False(l.IsOff || l.IsFrozen || l.IsLocked));
            Assert.Equal(
                new[] { "ByBlock", "ByLayer", "Continuous", "CENTER", "HIDDEN" },
                database.LinetypeTable.Select(l => l.Name)
            );
            Assert.Empty(database.ModelSpace);
        }

        [Fact]
        public void TheSampleTemplate_HoldsTextStylesOnlyInOurOwnFontAndArial_NeverAnAutodeskFont()
        {
            DxfDatabase database = Open();

            Assert.Equal(
                new[] { "Standard|ghsimplex.shx|0|0.8", "Notes|ghsimplex.shx|0|0.8", "Titles|ghsimplex.shx|0|0.8" },
                database
                    .TextStyleTable.Where(t => t.Name != "Arial")
                    .Select(t => string.Join("|", t.Name, t.FileName, t.TextSize, t.XScale))
            );
            DxfTextStyleTableRecord arial = database.TextStyleTable["Arial"];
            Assert.Equal("arial.ttf", arial.FileName);
            Assert.Equal("Arial", arial.Font.TypeFace);
            Assert.Equal(4, database.TextStyleTable.Count());
            Assert.All(database.TextStyleTable, t =>
                Assert.Contains(t.FileName, new[] { "ghsimplex.shx", "arial.ttf" }, StringComparer.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public void TheSampleTemplate_HoldsTheDimensionStylesGhAndGhAnnotative_ForMillimetres()
        {
            DxfDatabase database = Open();

            Assert.Equal(
                new[] { "GH", "GH-Annotative", "Standard" },
                database.DimStyleTable.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal)
            );

            foreach (string name in new[] { "GH", "GH-Annotative" })
            {
                DxfDimStyleTableRecord style = database.DimStyleTable[name];
                Assert.Equal("Standard", style.Dimtxsty.Name);
                Assert.Equal(2.5, style.Dimtxt, 6);
                Assert.Equal(2.5, style.Dimasz, 6);
                Assert.Null(style.Dimblk);
                Assert.Equal(0.0, style.Dimtsz, 6);
                Assert.Equal(1.5, style.Dimexo, 6);
                Assert.Equal(1.5, style.Dimexe, 6);
                Assert.Equal(0, style.Dimdec);
                Assert.Equal(2, style.Dimlunit);
                Assert.Equal(1, style.Dimtad);
                Assert.Equal(1.0, style.Dimscale, 6);
                Assert.Equal(Math.PI / 4.0, style.Dimjogang, 6);
                Assert.Equal(DxfColor.ByLayer, style.Dimclrd);
                Assert.Equal(DxfColor.ByLayer, style.Dimclre);
                Assert.Equal(DxfColor.ByLayer, style.Dimclrt);
            }

            Assert.Equal(DxfAnnotativeStates.False, database.DimStyleTable["GH"].Annotative);
            Assert.Equal(DxfAnnotativeStates.True, database.DimStyleTable["GH-Annotative"].Annotative);

            // The variables of the header are those of GH, the style that is current.
            DxfDimStyleTableRecord header = database.GetDimstyleData();
            Assert.Equal(2.5, header.Dimtxt, 6);
            Assert.Equal(0, header.Dimdec);
        }

        [Fact]
        public void TheSampleTemplate_HoldsTheMultileaderStyleGh_MatchingTheDimensionStyle()
        {
            DxfDatabase database = Open();

            var names = database.MLeaderStyleDictionary.Cast<DxfDBDictionaryEntry>().Select(e => e.Key).ToList();
            Assert.Equal(new[] { "GH", "Standard" }, names.OrderBy(n => n, StringComparer.Ordinal));
            var gh = (DxfMLeaderStyle)database.MLeaderStyleDictionary.GetAt("GH");
            Assert.Equal(2.5, gh.TextHeight, 6);
            Assert.Equal(2.5, gh.ArrowSize, 6);
            Assert.Equal("Standard", gh.TextStyle.Name);
        }

        [Fact]
        public void TheSampleTemplate_HasTheLayoutsModelAndA3_TheSheetOf420By297OnItsSide()
        {
            DxfDatabase database = Open();

            Assert.Equal(new[] { "Model", "A3" }, database.Layouts.Select(l => l.LayoutName));
            DxfLayout a3 = database.Layouts[1];
            Assert.Equal("ISO_A3_(420.00_x_297.00_MM)", a3.CanonicalMediaName);
            Assert.Equal(420.0, a3.PlotPaperSize.X, 6);
            Assert.Equal(297.0, a3.PlotPaperSize.Y, 6);
            Assert.Equal(DxfPlotPaperUnit.Millimeters, a3.PlotPaperUnits);
            Assert.Equal(DxfPlotRotation.Degrees000, a3.PlotRotation);
            Assert.Equal(DxfPlotType.Layout, a3.PlotType);
        }

        [Fact]
        public void NoDimensionStyleOfTheSample_IsInInches_AndNoNameInItIsAutodesks()
        {
            DxfDatabase database = Open();

            Assert.DoesNotContain(database.DimStyleTable, s => s.Name == "ISO-25");
            Assert.DoesNotContain(database.TextStyleTable, s => s.Name == "Annotative");
            Assert.All(database.DimStyleTable, s => Assert.Equal(2.5, s.Dimtxt, 6));
        }
    }
}
