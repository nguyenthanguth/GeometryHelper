using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Plot style tables as files (plot item 15): the .ctb and .stb reader and writer, the tables the presenter makes itself,
    /// the folder they are listed from, and the plot style a plot finds for an object by its colour or by its name.
    /// </summary>
    public class PlotStyleTableTests
    {
        private static readonly string Folder = TestRun.Folder("plotstyles1");

        private static string Fresh(string name)
        {
            string folder = Path.Combine(Folder, name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>A colour-dependent table edited in every way the editor edits one.</summary>
        private static CadPlotStyleSheet Edited()
        {
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewColorDependent();
            sheet.Description = "Site plots, 2026";
            sheet.ScaleFactor = 1.5;
            sheet.ApplyGlobalScaleFactor = true;
            CadPlotStyle red = sheet.StyleForColorIndex(1);
            red.Rgb = 0x0000FF;
            red.Description = "Red to blue";
            red.Screening = 50;
            red.Linetype = 1;
            red.LineWeightIndex = sheet.LineWeightIndexOf(0.35);
            red.LineEnd = CadPlotLineEnd.Round;
            red.LineJoin = CadPlotLineJoin.Bevel;
            red.PhysicalPen = 3;
            red.VirtualPen = 17;
            red.Dither = false;
            red.Adaptive = false;
            red.Fill = CadPlotFill.Crosshatch;
            CadPlotStyle green = sheet.StyleForColorIndex(3);
            green.Grayscale = true;
            sheet.SetLineWeight(sheet.LineWeightIndexOf(0.25), 0.28);
            sheet.LineWeightIndexOf(0.77);
            return sheet;
        }

        [Fact]
        public void EveryTableThePresenterMakes_WrittenReadAndWrittenAgain_IsTheSameTextAndTheSameBytes()
        {
            foreach (string name in CadPlotStyleFolder.BuiltInNames)
            {
                CadPlotStyleSheet made = CadPlotStyleFolder.BuiltIn(name);
                byte[] first = made.ToBytes();
                CadPlotStyleSheet read = CadPlotStyleSheet.Read(first);

                Assert.Equal(made.ToText(), read.ToText());
                Assert.Equal(first, read.ToBytes());
                Assert.Equal(name.EndsWith(".stb", StringComparison.Ordinal), read.IsNamed);
            }
        }

        [Fact]
        public void ATableEditedInEveryWay_WrittenToAFileAndLoaded_KeepsEveryProperty_AndTheFileIsTheSameBytesWrittenAgain()
        {
            string folder = Fresh("roundtrip");
            string path = Path.Combine(folder, "site.ctb");
            CadPlotStyleSheet sheet = Edited();
            sheet.Save(path);

            CadPlotStyleSheet read = CadPlotStyleSheet.Load(path);

            Assert.Equal("site.ctb", sheet.FileName);
            Assert.Equal("site.ctb", read.FileName);
            Assert.Equal(sheet.ToText(), read.ToText());
            Assert.Equal(File.ReadAllBytes(path), read.ToBytes());
            Assert.Equal("Site plots, 2026", read.Description);
            Assert.Equal(1.5, read.ScaleFactor);
            Assert.True(read.ApplyGlobalScaleFactor);
            Assert.Equal(255, read.Styles.Count);

            CadPlotStyle red = read.StyleForColorIndex(1);
            Assert.False(red.UsesObjectColor);
            Assert.Equal(0x0000FF, red.Rgb);
            Assert.Equal("Red to blue", red.Description);
            Assert.Equal(50, red.Screening);
            Assert.Equal(1, red.Linetype);
            Assert.Equal(0.35, read.LineWeightOf(red), 6);
            Assert.Equal(CadPlotLineEnd.Round, red.LineEnd);
            Assert.Equal(CadPlotLineJoin.Bevel, red.LineJoin);
            Assert.Equal(3, red.PhysicalPen);
            Assert.Equal(17, red.VirtualPen);
            Assert.False(red.Dither);
            Assert.False(red.Adaptive);
            Assert.Equal(CadPlotFill.Crosshatch, red.Fill);
            Assert.True(read.StyleForColorIndex(3).Grayscale);
            Assert.True(read.StyleForColorIndex(2).UsesObjectColor);
            Assert.Equal(0.28, read.LineWeights[8], 6);
            Assert.Equal(0.77, read.LineWeights[read.LineWeights.Count - 1], 6);
        }

        [Fact]
        public void TheFileStartsWithTheHeadOfAutoCADsFiles_AndItsThreeNumbersGiveTheLengthsOfTheTextAndTheStream()
        {
            byte[] colour = CadPlotStyleSheet.NewColorDependent().ToBytes();
            byte[] named = CadPlotStyleSheet.NewNamed().ToBytes();
            const string ctb = "PIAFILEVERSION_2.0,CTBVER1,compress\r\npmzlibcodec";

            Assert.Equal(ctb, Encoding.ASCII.GetString(colour, 0, ctb.Length));
            Assert.StartsWith(
                "PIAFILEVERSION_2.0,STBVER1,compress\r\npmzlibcodec",
                Encoding.ASCII.GetString(named, 0, 60),
                StringComparison.Ordinal
            );
            Assert.Equal(60, ctb.Length + 12);

            int textLength = BitConverter.ToInt32(colour, ctb.Length + 4);
            int streamLength = BitConverter.ToInt32(colour, ctb.Length + 8);
            Assert.Equal(Encoding.UTF8.GetByteCount(CadPlotStyleSheet.NewColorDependent().ToText()), textLength);
            Assert.Equal(colour.Length - 60, streamLength);
            Assert.Equal(0x78, colour[60]);
        }

        [Fact]
        public void ATableOfAutoCADsSpelling_IsRead_WithItsNotesAndItsOtherObjectColour_AndMissingStylesKeepTheirDefaults()
        {
            // Six decimals, a note after a number, 255 as the screening, and the other value of "use object color".
            string text =
                "description=\"From a plotter's folder\n"
                + "aci_table_available=TRUE\n"
                + "scale_factor=0.0076200000000 (+7.Z+\"8V?S_LC )\n"
                + "apply_factor=FALSE\n"
                + "custom_lineweight_display_units=1\n"
                + "plot_style{\n"
                + " 0{\n  name=\"Color_1\n  localized_name=\"Color_1\n  description=\"\n  color=-1006632961\n  color_policy=3\n"
                + "  physical_pen_number=0\n  virtual_pen_number=0\n  screen=255\n  linepattern_size=0.500000\n  linetype=31\n"
                + "  adaptive_linetype=TRUE\n  lineweight=0\n  fill_style=73\n  end_style=4\n  join_style=5\n }\n"
                + " 1{\n  name=\"Color_2\n  localized_name=\"Color_2\n  description=\"\n  color=-1040187392\n  mode_color=-1040187392\n  color_policy=1\n"
                + "  screen=100\n  linetype=0\n  adaptive_linetype=FALSE\n  lineweight=13\n  fill_style=64\n  end_style=2\n  join_style=2\n }\n"
                + "}\n"
                + "custom_lineweight_table{\n 0=0.000000\n 13=0.500000\n}\n";

            CadPlotStyleSheet sheet = CadPlotStyleSheet.FromText(text, false);

            Assert.Equal("From a plotter's folder", sheet.Description);
            Assert.Equal(0.00762, sheet.ScaleFactor, 9);
            Assert.True(sheet.LineWeightsInInches);
            Assert.Equal(255, sheet.Styles.Count);

            CadPlotStyle first = sheet.StyleForColorIndex(1);
            Assert.True(first.UsesObjectColor);
            Assert.True(first.Grayscale);
            Assert.Equal(255, first.Screening);
            Assert.Equal(100, first.EffectiveScreening);

            CadPlotStyle second = sheet.StyleForColorIndex(2);
            Assert.False(second.UsesObjectColor);
            Assert.Equal(0x000000, second.Rgb);
            Assert.Equal(0, second.Linetype);
            Assert.Equal(0.5, sheet.LineWeightOf(second), 6);
            Assert.Equal(CadPlotLineEnd.Round, second.LineEnd);

            Assert.True(sheet.StyleForColorIndex(3).UsesObjectColor);
            Assert.Equal("Color_255", sheet.StyleForColorIndex(255).Name);
            Assert.Equal(14, sheet.LineWeights.Count);
        }

        [Fact]
        public void BytesThatAreNotATable_AreRefused_WithTheReason()
        {
            byte[] good = CadPlotStyleSheet.NewColorDependent().ToBytes();
            byte[] damaged = good.Take(70).ToArray();
            var notATable = new byte[100];

            Assert.Throws<InvalidDataException>(() => CadPlotStyleSheet.Read(notATable));
            Assert.Throws<InvalidDataException>(() => CadPlotStyleSheet.Read(good.Take(40).ToArray()));
            Assert.Throws<InvalidDataException>(() => CadPlotStyleSheet.Read(damaged));
            Assert.Throws<ArgumentNullException>(() => CadPlotStyleSheet.Read(null));

            string folder = Fresh("refused");
            string path = Path.Combine(folder, "broken.ctb");
            File.WriteAllBytes(path, notATable);
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => CadPlotStyleSheet.Load(path));
            Assert.Contains("broken.ctb", exception.Message);
        }

        [Fact]
        public void ANamedTable_StartsWithNormal_TakesStylesByName_AndKeepsNormalWhole()
        {
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewNamed();
            CadPlotStyle heavy = sheet.AddStyle("Heavy");
            heavy.Rgb = 0x112233;
            sheet.AddStyle("Thin");

            Assert.Equal(new[] { "Normal", "Heavy", "Thin" }, sheet.Styles.Select(s => s.Name));
            Assert.Same(heavy, sheet.StyleNamed("HEAVY"));
            Assert.Null(sheet.StyleNamed("Missing"));
            Assert.Throws<ArgumentException>(() => sheet.AddStyle("heavy"));
            Assert.Throws<ArgumentException>(() => sheet.AddStyle("a/b"));
            Assert.Throws<ArgumentException>(() => sheet.AddStyle("  "));
            Assert.Throws<InvalidOperationException>(() => sheet.RemoveStyle("Normal"));
            Assert.Throws<InvalidOperationException>(() => sheet.RenameStyle("Normal", "Plain"));
            Assert.Throws<InvalidOperationException>(() => sheet.StyleForColorIndex(1));

            sheet.RenameStyle("Thin", "Hairline");
            Assert.True(sheet.RemoveStyle("Heavy"));
            Assert.False(sheet.RemoveStyle("Heavy"));

            CadPlotStyleSheet read = CadPlotStyleSheet.Read(sheet.ToBytes());
            Assert.Equal(new[] { "Normal", "Hairline" }, read.Styles.Select(s => s.Name));
            Assert.True(read.IsNamed);
            Assert.Equal(sheet.ToText(), read.ToText());

            Assert.Throws<InvalidOperationException>(() => CadPlotStyleSheet.NewColorDependent().AddStyle("Heavy"));
        }

        [Fact]
        public void ATableSavedUnderTheOtherKindsExtension_IsRefused()
        {
            string folder = Fresh("extension");

            Assert.Throws<ArgumentException>(() => CadPlotStyleSheet.NewNamed().Save(Path.Combine(folder, "a.ctb")));
            Assert.Throws<ArgumentException>(() =>
                CadPlotStyleSheet.NewColorDependent().Save(Path.Combine(folder, "a.stb"))
            );
            Assert.Empty(Directory.GetFiles(folder));
        }

        [Fact]
        public void TheFolder_ListsItsOwnKindInOrder_AndTheEmptyFolderListsTheTablesThePresenterMakes()
        {
            string folder = Fresh("names");
            CadPlotStyleSheet.NewColorDependent().Save(Path.Combine(folder, "b.ctb"));
            CadPlotStyleSheet.NewColorDependent().Save(Path.Combine(folder, "A.ctb"));
            File.WriteAllBytes(Path.Combine(folder, "a.ctb.bak"), CadPlotStyleSheet.NewColorDependent().ToBytes());
            CadPlotStyleSheet.NewNamed().Save(Path.Combine(folder, "n.stb"));

            Assert.Equal(new[] { "A.ctb", "b.ctb" }, CadPlotStyleFolder.Names(folder, false));
            Assert.Equal(new[] { "n.stb" }, CadPlotStyleFolder.Names(folder, true));
            Assert.Equal(
                new[]
                {
                    "acad.ctb",
                    "grayscale.ctb",
                    "monochrome.ctb",
                    "Screening 25%.ctb",
                    "Screening 50%.ctb",
                    "Screening 75%.ctb",
                },
                CadPlotStyleFolder.Names(string.Empty, false)
            );
            Assert.Equal(new[] { "acad.stb", "grayscale.stb", "monochrome.stb" }, CadPlotStyleFolder.Names(null, true));
            Assert.Empty(CadPlotStyleFolder.Names(Path.Combine(folder, "missing"), false));
        }

        [Fact]
        public void EnsureBuiltIns_WritesWhatTheFolderLacks_AndNeverReplacesATheUserEdited()
        {
            string folder = Path.Combine(Fresh("ensure"), "PlotStyles");

            Assert.Equal(CadPlotStyleFolder.BuiltInNames, CadPlotStyleFolder.EnsureBuiltIns(folder));
            Assert.Empty(CadPlotStyleFolder.EnsureBuiltIns(folder));

            CadPlotStyleSheet edited = CadPlotStyleFolder.Load(folder, "monochrome.ctb");
            edited.StyleForColorIndex(1).Rgb = 0xFF0000;
            edited.Save(Path.Combine(folder, "monochrome.ctb"));
            File.Delete(Path.Combine(folder, "Screening 25%.ctb"));

            Assert.Equal(new[] { "Screening 25%.ctb" }, CadPlotStyleFolder.EnsureBuiltIns(folder));
            Assert.Equal(0xFF0000, CadPlotStyleFolder.Load(folder, "MONOCHROME.CTB").StyleForColorIndex(1).Rgb);
        }

        [Fact]
        public void ATableThatIsNotInTheFolder_IsTheBuiltInOneOfItsName_AndNoOtherNameIsFound()
        {
            string folder = Fresh("fallback");
            File.WriteAllBytes(Path.Combine(folder, "grayscale.ctb"), new byte[50]);

            Assert.NotNull(CadPlotStyleFolder.Load(folder, "monochrome.ctb"));
            Assert.NotNull(CadPlotStyleFolder.Load(folder, "grayscale.ctb"));
            Assert.Null(CadPlotStyleFolder.Load(folder, "mine.ctb"));
            Assert.Null(CadPlotStyleFolder.Load(folder, string.Empty));
            Assert.Null(CadPlotStyleFolder.Load(folder, "..\\monochrome.ctb"));
            Assert.Equal("monochrome.ctb", CadPlotStyleFolder.Load(null, "monochrome.ctb").FileName);
        }

        [Fact]
        public void TheFolderBesideTheSettingsFile_IsPlotStyles_AndAnyFolderTheUserNamesIsTaken()
        {
            string settings = Path.Combine(Folder, "own", "settings.json");

            Assert.Equal(Path.Combine(Folder, "own", "PlotStyles"), CadPlotStyleFolder.Resolve(string.Empty, settings));
            Assert.Equal(
                "D:\\Standards\\Plot Styles",
                CadPlotStyleFolder.Resolve(" D:\\Standards\\Plot Styles ", settings)
            );
            Assert.Equal(CadPlotStyleFolder.DefaultPath, CadPlotStyleFolder.Resolve(null));
            Assert.EndsWith(Path.Combine("GeometryHelper", "Drawing", "PlotStyles"), CadPlotStyleFolder.DefaultPath);
        }

        [Fact]
        public void TheMonochromeAndGrayscaleFiles_PlotEveryColourAsTheTablesMadeInCodeDid_AndAcadAsDisplayed()
        {
            CadDocument document = PlotTests.Plan();
            CadPlotStyleResolver monochrome = CadPlotStyleResolver.Create(document, "monochrome.ctb");
            CadPlotStyleResolver grayscale = CadPlotStyleResolver.Create(document, "grayscale.ctb");
            CadPlotStyleResolver acad = CadPlotStyleResolver.Create(document, "acad.ctb");
            var colours = new List<int>
            {
                0xFF0000,
                0x00FF00,
                0x0000FF,
                0x808080,
                0xFFFFFF,
                0xFAFAFA,
                0x123456,
                0xFEFEFE,
            };

            for (int aci = 1; aci <= 255; aci++)
            {
                colours.Add(DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)aci).ColorValue);
            }

            foreach (int rgb in colours)
            {
                Assert.Equal(CadPlotStyles.Map(CadPlotStyleTable.Monochrome, rgb), monochrome.PlottedColor(rgb));
                Assert.Equal(CadPlotStyles.Map(CadPlotStyleTable.Grayscale, rgb), grayscale.PlottedColor(rgb));
                Assert.Equal(CadPlotStyles.Map(CadPlotStyleTable.None, rgb), acad.PlottedColor(rgb));
            }

            Assert.NotNull(monochrome.Sheet);
            Assert.False(monochrome.IsNamed);
        }

        [Fact]
        public void ATableNotFound_PlotsAsTheLegacyTableOfItsNameDid()
        {
            CadDocument document = PlotTests.Plan();

            Assert.Equal(0x4C4C4C, CadPlotStyleResolver.Create(document, "my grayscale.ctb").PlottedColor(0xFF0000));
            Assert.Equal(0x000000, CadPlotStyleResolver.Create(document, "my monochrome.ctb").PlottedColor(0xFF0000));
            Assert.Equal(0xFF0000, CadPlotStyleResolver.Create(document, "other.ctb").PlottedColor(0xFF0000));
            Assert.Equal(0xFF0000, CadPlotStyleResolver.Create(document, string.Empty).PlottedColor(0xFF0000));
            Assert.Null(CadPlotStyleResolver.Create(document, "other.ctb").Sheet);
        }

        [Fact]
        public void AStyleOfRed_SetsTheColourTheLineweightTheLinetypeAndTheShapeOfTheEnds_AndAnotherColourIsLeftAlone()
        {
            string folder = Fresh("pen");
            Edited().Save(Path.Combine(folder, "site.ctb"));
            CadDocument document = PlotTests.Plan();
            document.PlotStyleFolder = folder;
            CadPlotStyleResolver resolver = CadPlotStyleResolver.Create(document, "site.ctb");

            // Colour 1 is red, #FF0000: its style turns it blue at half screening, 0.35 mm, dashed, round ends, bevelled corners.
            CadPlotPen red = resolver.PenFor(0xFF0000, "0", null);
            Assert.Equal(0x8080FF, red.Rgb);
            Assert.True(red.HasLineWeight);
            Assert.Equal(0.35, red.LineWeight, 6);
            Assert.True(red.HasLinetype);
            Assert.Equal(new[] { 6.35, 3.175 }, red.Dashes);
            Assert.Equal(CadPlotLineEnd.Round, red.LineEnd);
            Assert.Equal(CadPlotLineJoin.Bevel, red.LineJoin);

            // Colour 3 (green) as the grey of its brightness; colour 2 (yellow) is as it is.
            Assert.Equal(0x969696, resolver.PenFor(0x00FF00, "0", null).Rgb);
            CadPlotPen yellow = resolver.PenFor(0xFFFF00, "0", null);
            Assert.Equal(0xFFFF00, yellow.Rgb);
            Assert.False(yellow.HasLineWeight);
            Assert.False(yellow.HasLinetype);
            Assert.Equal(CadPlotLineEnd.Object, yellow.LineEnd);

            // A true colour takes the style of the index nearest it: #FE0001 is nearest colour 1, and keeps its own colour where
            // the style sets one, which this one does.
            Assert.Equal(0x8080FF, resolver.PenFor(0xFE0001, "0", null).Rgb);
        }

        [Fact]
        public void AnObjectsWhite_PlotsBlackWhereTheStylesColourIsTheObjects_AndAColourTheStyleSetsPlotsAsSet()
        {
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewColorDependent();
            sheet.StyleForColorIndex(2).Rgb = 0xFFFFFF;
            string folder = Fresh("white");
            sheet.Save(Path.Combine(folder, "white.ctb"));
            CadDocument document = PlotTests.Plan();
            document.PlotStyleFolder = folder;
            CadPlotStyleResolver resolver = CadPlotStyleResolver.Create(document, "white.ctb");

            Assert.Equal(0x000000, resolver.PenFor(0xFFFFFF, "0", null).Rgb);
            Assert.Equal(0xFFFFFF, resolver.PenFor(0xFFFF00, "0", null).Rgb);
        }

        [Theory]
        [InlineData(25, 0xFFBFBF)]
        [InlineData(50, 0xFF8080)]
        [InlineData(75, 0xFF4040)]
        [InlineData(100, 0xFF0000)]
        [InlineData(0, 0xFFFFFF)]
        public void ScreeningOfRed_PlotsItThatManyPerCentOfTheWayFromWhite(int percent, int expected)
        {
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewColorDependent();
            sheet.StyleForColorIndex(1).Screening = percent;
            string folder = Fresh("screen");
            sheet.Save(Path.Combine(folder, "s.ctb"));
            CadDocument document = PlotTests.Plan();
            document.PlotStyleFolder = folder;

            Assert.Equal(expected, CadPlotStyleResolver.Create(document, "s.ctb").PlottedColor(0xFF0000));
        }

        [Fact]
        public void TheBuiltInScreeningTables_PlotEveryColourScreened()
        {
            CadDocument document = PlotTests.Plan();

            Assert.Equal(0xFF8080, CadPlotStyleResolver.Create(document, "Screening 50%.ctb").PlottedColor(0xFF0000));
            Assert.Equal(0xFFBFBF, CadPlotStyleResolver.Create(document, "Screening 25%.ctb").PlottedColor(0xFF0000));
            Assert.Equal(0xFF4040, CadPlotStyleResolver.Create(document, "screening 75%.ctb").PlottedColor(0xFF0000));
        }

        [Fact]
        public void TheSettingsKeepTheTablesFileName_AndTheThreeLegacyTablesStayAWayInToIt()
        {
            var settings = new CadPlotSettings();
            Assert.Equal("monochrome.ctb", settings.StyleSheet);
            Assert.Equal(CadPlotStyleTable.Monochrome, settings.StyleTable);

            settings.StyleSheet = "  acad.ctb ";
            Assert.Equal("acad.ctb", settings.StyleSheet);
            Assert.Equal(CadPlotStyleTable.None, settings.StyleTable);
            Assert.Equal("acad.ctb", settings.EffectiveStyleSheet);

            settings.StyleTable = CadPlotStyleTable.Grayscale;
            Assert.Equal("grayscale.ctb", settings.StyleSheet);

            settings.StyleTable = CadPlotStyleTable.None;
            Assert.Equal(string.Empty, settings.StyleSheet);

            settings.StyleSheet = "Screening 50%.ctb";
            settings.PlotWithPlotStyles = false;
            Assert.Equal(string.Empty, settings.EffectiveStyleSheet);
            Assert.Equal("Screening 50%.ctb", settings.StyleSheet);
        }

        [Fact]
        public void ATablesNameWrittenToALayout_IsReadBackAfterSaving_WhateverTheName()
        {
            CadDocument document = PlotTests.Plan();
            var settings = new CadPlotSettings { StyleSheet = "Site plots.ctb" };

            Assert.True(settings.ApplyTo(document, document.ModelSpace).Succeeded);

            Assert.Equal("Site plots.ctb", CadPlotSettings.LayoutOf(document, document.ModelSpace).CurrentStyleSheet);
            Assert.Equal("Site plots.ctb", CadPlotSettings.Read(document, document.ModelSpace).StyleSheet);
            Assert.Equal(CadPlotStyleTable.None, CadPlotSettings.Read(document, document.ModelSpace).StyleTable);

            settings.StyleSheet = string.Empty;
            Assert.True(settings.ApplyTo(document, document.ModelSpace).Succeeded);
            Assert.Equal(string.Empty, CadPlotSettings.Read(document, document.ModelSpace).StyleSheet);
        }

        [Fact]
        public void GlobalSettings_KeepThePlotStyleFolder_ThroughSavingAndLoading_EmptyByDefault()
        {
            string path = Path.Combine(Fresh("settings"), "settings.json");
            var settings = new GlobalSettings();
            Assert.Equal(string.Empty, settings.PlotStyleFolder);

            settings.PlotStyleFolder = "  D:\\Standards\\Plot Styles ";
            settings.Save(path);

            Assert.Equal("D:\\Standards\\Plot Styles", GlobalSettings.Load(path).PlotStyleFolder);
            Assert.Equal("D:\\Standards\\Plot Styles", settings.Clone().PlotStyleFolder);
        }

        /// <summary>
        /// A drawing of named plot styles: the layers Walls (style Heavy, by its group 390) and Plain (none), and three lines,
        /// the first on Walls, the second on Plain, the third on Plain with the style Thin of its own (380 of 3, 390).
        /// </summary>
        private static CadDocument Named(out string wallsLine, out string plainLine, out string thinLine)
        {
            var db = new DxfDatabase("AC1027");
            db.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Walls" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Plain" });
            var walls = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0))
            {
                Layer = "Walls",
                ColorIndex = 1,
            };
            var plain = new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(100, 10, 0))
            {
                Layer = "Plain",
                ColorIndex = 1,
            };
            var thin = new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(100, 20, 0))
            {
                Layer = "Plain",
                ColorIndex = 1,
            };
            db.ModelSpace.AppendEntity(walls);
            db.ModelSpace.AppendEntity(plain);
            db.ModelSpace.AppendEntity(thin);
            wallsLine = walls.Handle;
            plainLine = plain.Handle;
            thinLine = thin.Handle;
            string dictionary = db.PlotStyleNameDictionary.Handle;
            string normal = db.PlotStyleNameDictionary.First().Value.Handle;
            string text = db.ToText();

            // Two more entries in the dictionary of plot style names, and the placeholders they point at.
            text = Once(
                text,
                "  3\nNormal\n350\n" + normal + "\n",
                "  3\nNormal\n350\n" + normal + "\n  3\nHeavy\n350\n7F01\n  3\nThin\n350\n7F02\n"
            );
            text = Once(
                text,
                "  0\nACDBPLACEHOLDER\n",
                "  0\nACDBPLACEHOLDER\n  5\n7F01\n330\n"
                    + dictionary
                    + "\n  0\nACDBPLACEHOLDER\n  5\n7F02\n330\n"
                    + dictionary
                    + "\n  0\nACDBPLACEHOLDER\n"
            );

            // The layer Walls given Heavy, in place of the Normal its group 390 holds.
            int layer = text.IndexOf("  2\nWalls\n", StringComparison.Ordinal);
            int pointer = text.IndexOf("390\n" + normal + "\n", layer, StringComparison.Ordinal);
            text =
                text.Substring(0, pointer) + "390\n7F01\n" + text.Substring(pointer + ("390\n" + normal + "\n").Length);

            // The third line given Thin of its own.
            int start = text.IndexOf("\n  5\n" + thinLine + "\n", StringComparison.Ordinal);
            int at = text.IndexOf("100\nAcDbLine\n", start, StringComparison.Ordinal);
            text = text.Insert(at, "380\n3\n390\n7F02\n");
            return CadDocument.From(DxfDatabase.Parse(text), "named.dxf");
        }

        private static string Once(string text, string old, string replacement)
        {
            int at = text.IndexOf(old, StringComparison.Ordinal);
            Assert.True(at >= 0, "The text has no " + old.Replace("\n", "|"));
            return text.Substring(0, at) + replacement + text.Substring(at + old.Length);
        }

        [Fact]
        public void InADrawingOfNamedPlotStyles_AnObjectTakesItsOwnStyleThenItsLayersThenNormal()
        {
            CadDocument document = Named(out string wallsLine, out string plainLine, out string thinLine);
            string folder = Fresh("named");
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewNamed();
            sheet.StyleNamed("Normal").Rgb = 0x00FF00;
            sheet.AddStyle("Heavy").Rgb = 0x0000FF;
            CadPlotStyle thin = sheet.AddStyle("Thin");
            thin.Rgb = 0xFF00FF;
            thin.LineWeightIndex = sheet.LineWeightIndexOf(0.13);
            sheet.Save(Path.Combine(folder, "site.stb"));
            document.PlotStyleFolder = folder;
            CadPlotStyleResolver resolver = CadPlotStyleResolver.Create(document, "site.stb");

            Assert.True(resolver.IsNamed);
            Assert.Equal("Heavy", resolver.StyleNameOf("Walls", wallsLine));
            Assert.Equal("Normal", resolver.StyleNameOf("Plain", plainLine));
            Assert.Equal("Thin", resolver.StyleNameOf("Plain", thinLine));
            Assert.Equal("Heavy", resolver.StyleNameOf("Walls", null));
            Assert.Equal("Normal", resolver.StyleNameOf("Missing", "FFFF"));

            Assert.Equal(0x0000FF, resolver.PenFor(0xFF0000, "Walls", wallsLine).Rgb);
            Assert.Equal(0x00FF00, resolver.PenFor(0xFF0000, "Plain", plainLine).Rgb);
            CadPlotPen own = resolver.PenFor(0xFF0000, "Plain", thinLine);
            Assert.Equal(0xFF00FF, own.Rgb);
            Assert.Equal(0.13, own.LineWeight, 6);
        }

        [Fact]
        public void APlotOfANamedDrawing_ColoursEachItemByItsStyle()
        {
            CadDocument document = Named(out string wallsLine, out string plainLine, out string thinLine);
            string folder = Fresh("named-items");
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewNamed();
            sheet.AddStyle("Heavy").Rgb = 0x0000FF;
            sheet.AddStyle("Thin").Rgb = 0xFF00FF;
            sheet.Save(Path.Combine(folder, "site.stb"));
            document.PlotStyleFolder = folder;
            CadPlotStyleResolver resolver = CadPlotStyleResolver.Create(document, "site.stb");

            var plotted = document
                .GetDisplay(document.ModelSpace)
                .Items.ToDictionary(item => item.Handle, item => resolver.PenFor(item).Rgb);

            Assert.Equal(0x0000FF, plotted[wallsLine]);
            Assert.Equal(0xFF0000, plotted[plainLine]);
            Assert.Equal(0xFF00FF, plotted[thinLine]);
        }

        [Fact]
        public void StylesManager_WithNoWindow_NamesTheTablesOfTheFolderOnTheCommandLine()
        {
            CadDocument document = PlotTests.Plan();
            CadCommandSession session = EditingDrawings.Session(document);

            EditingDrawings.Run(session, "STYLESMANAGER");

            Assert.False(session.IsRunning);
            Assert.Contains("Plot style tables (built in):", session.History);
            Assert.Contains("acad.ctb", session.History);
            Assert.Contains("monochrome.stb", session.History);

            string folder = Fresh("manager");
            CadPlotStyleSheet.NewColorDependent().Save(Path.Combine(folder, "site.ctb"));
            document.PlotStyleFolder = folder;
            EditingDrawings.Run(session, "STYLESMANAGER");
            Assert.Contains("Plot style tables in " + folder + ":", session.History);
            Assert.Contains("site.ctb", session.History);
        }

        [Fact]
        public void StylesManager_WithAWindow_HandsItTheFolderAndTheKindNewMakes()
        {
            CadDocument document = PlotTests.Plan();
            string folder = Fresh("manager-window");
            document.PlotStyleFolder = folder;
            CadCommandSession session = EditingDrawings.Session(document);
            CadStylesManagerRequest request = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "STYLESMANAGER")
                {
                    request = (CadStylesManagerRequest)e.Payload;
                    e.Handled = true;
                }
            };

            EditingDrawings.Run(session, "STYLESMANAGER");

            Assert.NotNull(request);
            Assert.Equal(folder, request.Folder);
            Assert.False(request.NewIsNamed);
            Assert.False(session.IsRunning);
            Assert.DoesNotContain("acad.ctb", session.History);
        }

        [Fact]
        public void DashPlot_ListsTheTablesOfTheFolder_AndTakesOneTypedWithOrWithoutItsExtension()
        {
            string folder = Fresh("dashplot");
            CadPlotStyleSheet.NewColorDependent().Save(Path.Combine(folder, "site.ctb"));
            CadDocument document = PlotTests.Plan();
            document.PlotStyleFolder = folder;
            CadCommandSession session = EditingDrawings.Session(document);
            CadPlotJob job = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "-PLOT")
                {
                    job = e.Plot;
                    e.Handled = true;
                }
            };

            EditingDrawings.Run(session, "-PLOT", "Y");

            for (
                int i = 0;
                i < 30 && !session.PromptText.StartsWith("Enter plot style table name", StringComparison.Ordinal);
                i++
            )
            {
                EditingDrawings.Run(session, EditingDrawings.Enter);
            }

            Assert.Equal("Enter plot style table name (enter . for none) or [?] <.>:", session.PromptText);
            EditingDrawings.Run(session, "?");
            Assert.Contains("site.ctb", session.History);
            Assert.DoesNotContain("grayscale.ctb", session.History);
            EditingDrawings.Run(session, "nothing");
            Assert.Equal("Plot style table \"nothing\" not found.", session.History.Last());
            EditingDrawings.Run(session, "SITE");

            for (int i = 0; i < 30 && session.IsRunning; i++)
            {
                EditingDrawings.Run(session, EditingDrawings.Enter);
            }

            Assert.NotNull(job);
            Assert.Equal("site.ctb", job.Settings.StyleSheet);
        }
    }
}
