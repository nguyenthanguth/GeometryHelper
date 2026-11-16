using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Plot style tables in the presenter's dialogs (plot item 15): a plot with a table drawn in the colours, lineweight,
    /// linetype and line ends the table says, the Plot and Page Setup dialog listing, choosing, editing and making tables, the
    /// Plot Style Table Editor over a table, and the Plot Styles folder of STYLESMANAGER, all with no window.
    /// </summary>
    public class PlotStyleEditorTests
    {
        /// <summary>A table whose style of colour 1 plots blue, 0.35 mm, dashed, round ended and bevelled.</summary>
        private static CadPlotStyleSheet RedToBlue()
        {
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewColorDependent();
            CadPlotStyle red = sheet.StyleForColorIndex(1);
            red.Rgb = 0x0000FF;
            red.LineWeightIndex = sheet.LineWeightIndexOf(0.35);
            red.LineEnd = CadPlotLineEnd.Round;
            red.LineJoin = CadPlotLineJoin.Bevel;
            sheet.Description = "Red to blue";
            return sheet;
        }

        private static PlotDialogViewModel Dialog(CadDocument document, CadPlotSettings settings = null)
        {
            var job = new CadPlotJob(
                document.ModelSpace,
                settings ?? CadPlotSettings.Read(document, document.ModelSpace),
                DisplayBox.Empty,
                null
            );
            return new PlotDialogViewModel(document, job);
        }

        private static string Content(CadDocument document, CadPlotSettings settings, out string text)
        {
            using (var memory = new MemoryStream())
            {
                CadPdfWriter.Write(memory, document, document.ModelSpace, settings, DisplayBox.Empty);
                byte[] pdf = memory.ToArray();
                text = System.Text.Encoding.GetEncoding(28591).GetString(pdf);
                return string.Join("\n", Pdf.Streams(pdf));
            }
        }

        [Fact]
        public void APdfPlottedWithATable_DrawsTheRedFrameInTheTablesColourLineweightAndEnds()
        {
            using (var folder = new TestFolder())
            {
                RedToBlue().Save(Path.Combine(folder.FullPath, "site.ctb"));
                CadDocument document = PlotPdfTests.Plan();
                document.PlotStyleFolder = folder.FullPath;
                CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
                settings.StyleSheet = "site.ctb";

                string content = Content(document, settings, out string text);

                // The frame was red 0.5 mm; the table makes it blue 0.35 mm (0.992 points), round ended, bevelled.
                Assert.Contains("0 0 1 RG", content);
                Assert.DoesNotContain("1 0 0 RG", content);
                Assert.Matches(@"/LW \.992", text);
                Assert.DoesNotMatch(@"/LW 1\.417", text);
                Assert.Matches(@"/LC 1\b", text);
                Assert.Matches(@"/LJ 2\b", text);
            }
        }

        [Fact]
        public void ADashedStyle_DrawsTheFrameAsDashesInsteadOfOneLine()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleSheet solid = RedToBlue();
                CadPlotStyleSheet dashed = RedToBlue();
                dashed.StyleForColorIndex(1).Linetype = 1;
                solid.Save(Path.Combine(folder.FullPath, "solid.ctb"));
                dashed.Save(Path.Combine(folder.FullPath, "dashed.ctb"));
                CadDocument document = PlotPdfTests.Plan();
                document.PlotStyleFolder = folder.FullPath;
                CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();

                settings.StyleSheet = "solid.ctb";
                string whole = Content(document, settings, out string wholeText);
                settings.StyleSheet = "dashed.ctb";
                string cut = Content(document, settings, out string cutText);

                // The PDF writer turns a dashed stroke into the outline of its dashes: filled, and many more marks than four sides.
                Assert.Matches(@"/LW \.992", wholeText);
                Assert.DoesNotMatch(@"/LW \.992", cutText);
                Assert.Contains("0 0 1 rg", cut);
                Assert.True(
                    cut.Length > whole.Length + 100,
                    "The dashes should make a longer page: " + cut.Length + " against " + whole.Length
                );
            }
        }

        [Fact]
        public void TheSameDrawingPlottedWithAcad_KeepsTheRedFrameAndItsLineweight_AndWithTheSettingsToNone_AsDisplayed()
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            settings.StyleSheet = "acad.ctb";

            string content = Content(document, settings, out string text);

            Assert.Contains("1 0 0 RG", content);
            Assert.Matches(@"/LW 1\.417", text);

            settings.StyleSheet = "monochrome.ctb";
            content = Content(document, settings, out text);
            Assert.Contains("0 0 0 RG", content);
            Assert.DoesNotContain("1 0 0 RG", content);

            settings.PlotWithPlotStyles = false;
            content = Content(document, settings, out text);
            Assert.Contains("1 0 0 RG", content);
        }

        [Fact]
        public void ScreeningFiftyPlotsTheRedFrameHalfWayToWhite()
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            settings.StyleSheet = "Screening 50%.ctb";

            string content = Content(document, settings, out string text);

            // Red halfway to white, and the black of the text screened to a grey.
            Assert.Contains("1 .502 .502 RG", content);
            Assert.Contains(".502 .502 .502 RG", content);
            Assert.DoesNotContain("1 0 0 RG", content);
        }

        [Fact]
        public void APngPlottedWithATable_HasTheFrameInTheTablesColour()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleSheet table = CadPlotStyleSheet.NewColorDependent();
                table.StyleForColorIndex(1).Rgb = 0x0000FF;
                table.Save(Path.Combine(folder.FullPath, "blue.ctb"));
                CadDocument document = PlotPdfTests.Plan();
                document.PlotStyleFolder = folder.FullPath;
                PlotDialogViewModel dialog = Dialog(document);
                dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);
                dialog.PlotWithPlotStyles = true;
                dialog.StyleSheet = "blue.ctb";
                string path = Path.Combine(folder.FullPath, "plan.png");

                CadPlotPage page = dialog.Plot(path);

                using (SKBitmap image = SKBitmap.Decode(path))
                {
                    int left = (int)Math.Round((1920 - 7500 * page.Scale) / 2.0);
                    SKColor[] across = Enumerable.Range(left - 3, 8).Select(x => image.GetPixel(x, 300)).ToArray();
                    Assert.Contains(across, p => p.Blue > 200 && p.Red < p.Blue - 40 && p.Green < p.Blue - 40);
                    Assert.DoesNotContain(across, p => p.Red > 200 && p.Green < p.Red - 40 && p.Blue < p.Red - 40);
                }
            }
        }

        [Fact]
        public void ThePlotDialogListsNoneThenTheTablesOfTheFolderOfTheDrawingsKind_AndTheChoiceIsWhatItPlotsWith()
        {
            using (var folder = new TestFolder())
            {
                RedToBlue().Save(Path.Combine(folder.FullPath, "site.ctb"));
                CadPlotStyleSheet.NewNamed().Save(Path.Combine(folder.FullPath, "named.stb"));
                CadDocument document = PlotPdfTests.Plan();
                document.PlotStyleFolder = folder.FullPath;
                PlotDialogViewModel dialog = Dialog(document, new CadPlotSettings());

                // monochrome.ctb is the default and the folder lacks it: listed after the files, as a page setup of another drawing is.
                Assert.Equal(new[] { "None", "site.ctb", "monochrome.ctb" }, dialog.StyleSheets);
                Assert.Equal("monochrome.ctb", dialog.StyleSheet);
                Assert.Equal(CadPlotStyleTable.Monochrome, dialog.StyleTable);

                dialog.StyleSheet = "site.ctb";
                Assert.Equal("site.ctb", dialog.Settings.StyleSheet);
                Assert.Equal(CadPlotStyleTable.None, dialog.StyleTable);
                Assert.Equal("Red to blue", dialog.StyleNote);
                Assert.True(dialog.CanEditStyleSheet);
                Assert.True(dialog.CanNewStyleSheet);

                dialog.StyleSheet = "named.stb";
                Assert.Equal("site.ctb", dialog.StyleSheet);

                dialog.StyleSheet = "None";
                Assert.Equal(string.Empty, dialog.Settings.StyleSheet);
                Assert.False(dialog.CanEditStyleSheet);
                Assert.StartsWith("No table", dialog.StyleNote, StringComparison.Ordinal);

                dialog.StyleTable = CadPlotStyleTable.Grayscale;
                Assert.Equal("grayscale.ctb", dialog.StyleSheet);
            }
        }

        [Fact]
        public void WithNoFolder_ThePlotDialogListsTheBuiltInTables_AndNewAndEditAreGrey()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan());

            Assert.Equal(
                new[]
                {
                    "None",
                    "acad.ctb",
                    "grayscale.ctb",
                    "monochrome.ctb",
                    "Screening 25%.ctb",
                    "Screening 50%.ctb",
                    "Screening 75%.ctb",
                },
                dialog.StyleSheets
            );
            Assert.False(dialog.CanNewStyleSheet);
            Assert.False(dialog.CanEditStyleSheet);
            Assert.False(dialog.EditStyleSheet());
            Assert.False(dialog.NewStyleSheet());
        }

        [Fact]
        public void InADrawingOfNamedPlotStyles_ThePlotDialogListsTheStbTables_AndNewMakesOne()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                var database = new DxfDatabase("AC1027");
                database.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
                CadDocument document = CadDocument.From(database, "named.dxf");
                document.PlotStyleFolder = folder.FullPath;
                PlotDialogViewModel dialog = Dialog(document, new CadPlotSettings { StyleSheet = "acad.stb" });

                Assert.True(dialog.PlotsByName);
                Assert.Equal(new[] { "None", "acad.stb", "grayscale.stb", "monochrome.stb" }, dialog.StyleSheets);
                Assert.Equal("acad.stb", dialog.StyleSheet);
                Assert.True(dialog.CanEditStyleSheet);

                dialog.ShowNewStyleSheet = made =>
                {
                    Assert.True(made.IsNamed);
                    made.Name = "walls";
                    return made.TryCreate();
                };
                Assert.True(dialog.NewStyleSheet());
                Assert.Equal("walls.stb", dialog.StyleSheet);
                Assert.True(CadPlotStyleSheet.Load(Path.Combine(folder.FullPath, "walls.stb")).IsNamed);
            }
        }

        [Fact]
        public void EditFromThePlotDialog_ShowsTheEditorOverTheTableRead_AndWhatItSavesIsWhatTheNextPlotUses()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                CadDocument document = PlotPdfTests.Plan();
                document.PlotStyleFolder = folder.FullPath;
                CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
                settings.StyleSheet = "monochrome.ctb";
                PlotDialogViewModel dialog = Dialog(document, settings);
                PlotStyleEditorViewModel seen = null;
                dialog.ShowStyleEditor = editor =>
                {
                    seen = editor;
                    Assert.Equal("Plot Style Table Editor - monochrome.ctb", editor.Title);
                    Assert.Equal(folder.FullPath, editor.Location);
                    editor.Rows[0].Color = "#0000FF";
                    Assert.True(editor.Save());
                    return true;
                };

                Assert.True(dialog.EditStyleSheet());

                Assert.NotNull(seen);
                string content = Content(document, dialog.Settings, out string text);
                Assert.Contains("0 0 1 RG", content);
                Assert.Contains("0 0 0 RG", content);
            }
        }

        [Fact]
        public void NewFromThePlotDialog_MakesTheFile_ChoosesIt_AndOpensTheEditorOnIt()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                CadDocument document = PlotPdfTests.Plan();
                document.PlotStyleFolder = folder.FullPath;
                PlotDialogViewModel dialog = Dialog(document);
                PlotStyleEditorViewModel edited = null;
                dialog.ShowNewStyleSheet = made =>
                {
                    Assert.False(made.IsNamed);
                    Assert.Contains("monochrome.ctb", made.Sources);
                    made.Name = "mine";
                    made.Source = "monochrome.ctb";
                    return made.TryCreate();
                };
                dialog.ShowStyleEditor = editor =>
                {
                    edited = editor;
                    return true;
                };

                Assert.True(dialog.NewStyleSheet());

                Assert.Equal("mine.ctb", dialog.StyleSheet);
                Assert.Contains("mine.ctb", dialog.StyleSheets);
                Assert.True(File.Exists(Path.Combine(folder.FullPath, "mine.ctb")));
                Assert.Equal("mine.ctb", edited.FileName);
                Assert.Equal(0x000000, edited.Sheet.StyleForColorIndex(1).Rgb);
                Assert.False(edited.Sheet.StyleForColorIndex(1).UsesObjectColor);
            }
        }

        [Fact]
        public void TheAddPlotStyleTableDialog_RefusesABadOrTakenName_AndMakesTheKindOfTheDrawing()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                var colour = new PlotStyleNewViewModel(folder.FullPath, false);
                var named = new PlotStyleNewViewModel(folder.FullPath, true);

                Assert.Equal(PlotStyleNewViewModel.FromScratch, colour.Source);
                Assert.Equal("Colour-dependent plot style table (.ctb)", colour.Kind);
                Assert.DoesNotContain("monochrome.ctb", named.Sources);
                Assert.Contains("monochrome.stb", named.Sources);

                Assert.False(colour.TryCreate());
                Assert.Equal("A plot style table needs a name.", colour.Message);
                colour.Name = "a:b";
                Assert.False(colour.TryCreate());
                Assert.StartsWith("A plot style table's name may not hold", colour.Message, StringComparison.Ordinal);
                colour.Name = "MONOCHROME";
                Assert.False(colour.TryCreate());
                Assert.Equal("A plot style table named MONOCHROME.ctb is there already.", colour.Message);
                colour.Source = "no such table";
                Assert.Equal(PlotStyleNewViewModel.FromScratch, colour.Source);

                named.Name = "walls";
                Assert.True(named.TryCreate());
                Assert.Equal("walls.stb", named.CreatedFileName);
                CadPlotStyleSheet made = CadPlotStyleSheet.Load(Path.Combine(folder.FullPath, "walls.stb"));
                Assert.True(made.IsNamed);
                Assert.Equal(new[] { "Normal" }, made.Styles.Select(s => s.Name));
            }
        }

        [Fact]
        public void TheEditor_ShowsTheGeneralTab_TakesEveryPropertyOfAStyle_AndDropsAValueThatIsNotOne()
        {
            CadPlotStyleSheet sheet = CadPlotStyleSheet.NewColorDependent();
            sheet.FileName = "site.ctb";
            var editor = new PlotStyleEditorViewModel(sheet, null);

            Assert.True(editor.IsGeneral);
            Assert.Equal("Plot Style Table Editor - site.ctb", editor.Title);
            Assert.Equal("255 plot styles", editor.StyleCountText);
            Assert.Equal("Colour-dependent plot style table (.ctb)", editor.Kind);
            Assert.False(editor.CanAddStyle);
            Assert.False(editor.CanDeleteStyle);

            editor.Tab = "Table View";
            Assert.True(editor.IsTableView);
            editor.Tab = "Nothing";
            Assert.True(editor.IsTableView);
            editor.Tab = "Form View";
            Assert.True(editor.IsFormView);

            PlotStyleRowViewModel row = editor.Rows[0];
            Assert.Equal("Color_1", row.Name);
            Assert.Equal("Use object color", row.Color);
            Assert.False(row.HasColor);
            Assert.Equal("Automatic", row.PhysicalPen);
            Assert.Equal("100", row.Screening);
            Assert.Equal("Use object linetype", row.Linetype);
            Assert.Equal("Use object lineweight", row.LineWeight);
            Assert.Equal("Use object end style", row.LineEnd);
            Assert.Equal("Use object join style", row.LineJoin);
            Assert.Equal("Use object fill style", row.Fill);
            Assert.False(editor.IsModified);

            row.Color = "255, 128, 0";
            Assert.Equal("#FF8000", row.Color);
            Assert.True(row.HasColor);
            Assert.Equal(0xFF8000, row.SwatchRgb);
            row.Color = "zzz";
            Assert.Equal("#FF8000", row.Color);
            row.PhysicalPen = "7";
            row.VirtualPen = "200";
            row.Screening = "40";
            row.Linetype = "ISO Dot";
            row.LineWeight = "0.250 mm";
            row.LineEnd = "Round";
            row.LineJoin = "Bevel";
            row.Fill = "Crosshatch";
            row.Dither = false;
            row.Grayscale = true;
            row.Adaptive = false;
            row.Description = "Orange";

            Assert.True(editor.IsModified);
            CadPlotStyle style = editor.Sheet.StyleForColorIndex(1);
            Assert.Equal(0xFF8000, style.Rgb);
            Assert.Equal(7, style.PhysicalPen);
            Assert.Equal(200, style.VirtualPen);
            Assert.Equal(40, style.Screening);
            Assert.Equal(22, style.Linetype);
            Assert.Equal(0.25, editor.Sheet.LineWeightOf(style), 6);
            Assert.Equal(CadPlotLineEnd.Round, style.LineEnd);
            Assert.Equal(CadPlotLineJoin.Bevel, style.LineJoin);
            Assert.Equal(CadPlotFill.Crosshatch, style.Fill);
            Assert.False(style.Dither);
            Assert.True(style.Grayscale);
            Assert.False(style.Adaptive);
            Assert.Equal("Orange", style.Description);

            row.PhysicalPen = "99";
            row.Screening = "101";
            row.Screening = "abc";
            row.Linetype = "Wavy";
            row.LineJoin = "Fancy";
            Assert.Equal("7", row.PhysicalPen);
            Assert.Equal("40", row.Screening);
            Assert.Equal("ISO Dot", row.Linetype);
            Assert.Equal("Bevel", row.LineJoin);
            row.PhysicalPen = "automatic";
            row.LineJoin = "Use object join style";
            row.Color = "Use object color";
            Assert.Equal("Automatic", row.PhysicalPen);
            Assert.Equal(CadPlotLineJoin.Object, style.LineJoin);
            Assert.True(style.UsesObjectColor);

            editor.ScaleFactor = "2.5";
            editor.ScaleFactor = "-1";
            editor.ApplyGlobalScaleFactor = true;
            editor.Description = "A table";
            Assert.Equal("2.5", editor.ScaleFactor);
            Assert.Equal(2.5, editor.Sheet.ScaleFactor);
            Assert.True(editor.Sheet.ApplyGlobalScaleFactor);
            Assert.Equal("A table", editor.Sheet.Description);
            Assert.Equal(27, editor.LineWeightOptions.Count);
            Assert.Equal("Use object lineweight", editor.LineWeightOptions[0]);
            Assert.Equal("0.050 mm", editor.LineWeightOptions[1]);
        }

        [Fact]
        public void TheEditor_NeverChangesTheTableItWasGiven_AndSaveWritesItsCopyBackToTheFile()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleSheet original = CadPlotStyleFolder.BuiltIn("acad.ctb");
                original.Save(Path.Combine(folder.FullPath, "acad.ctb"));
                var editor = new PlotStyleEditorViewModel(original, folder.FullPath);

                Assert.True(editor.Save());
                editor.Rows[0].Color = "#00FF00";

                Assert.True(original.StyleForColorIndex(1).UsesObjectColor);
                Assert.True(
                    CadPlotStyleSheet
                        .Load(Path.Combine(folder.FullPath, "acad.ctb"))
                        .StyleForColorIndex(1)
                        .UsesObjectColor
                );
                Assert.True(editor.Save());
                Assert.False(editor.IsModified);
                Assert.Equal(
                    0x00FF00,
                    CadPlotStyleSheet.Load(Path.Combine(folder.FullPath, "acad.ctb")).StyleForColorIndex(1).Rgb
                );
            }
        }

        [Fact]
        public void SaveAs_WritesAnotherFile_AddsTheExtension_AndRefusesABadName()
        {
            using (var folder = new TestFolder())
            {
                var editor = new PlotStyleEditorViewModel(
                    CadPlotStyleFolder.BuiltIn("monochrome.ctb"),
                    folder.FullPath
                );
                editor.AskSaveAs = suggested =>
                {
                    Assert.Equal(Path.Combine(folder.FullPath, "monochrome.ctb"), suggested);
                    return Path.Combine(folder.FullPath, "copy");
                };

                Assert.True(editor.SaveAs());

                Assert.True(File.Exists(Path.Combine(folder.FullPath, "copy.ctb")));
                Assert.Equal("copy.ctb", editor.FileName);
                Assert.Equal("Plot Style Table Editor - copy.ctb", editor.Title);
                Assert.False(editor.SaveAs(Path.Combine(folder.FullPath, "a?b.ctb")));
                Assert.StartsWith("A plot style table's name may not hold", editor.Message, StringComparison.Ordinal);
                editor.AskSaveAs = suggested => null;
                Assert.False(editor.SaveAs());

                var unsaved = new PlotStyleEditorViewModel(CadPlotStyleSheet.NewColorDependent(), null);
                unsaved.Rows[0].Color = "#FF0000";
                Assert.False(unsaved.Save());
                Assert.StartsWith("This table has no folder", unsaved.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheEditorOfANamedTable_AddsDeletesAndRenamesStyles_ButNeverNormal()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleSheet named = CadPlotStyleFolder.BuiltIn("acad.stb");
                named.Save(Path.Combine(folder.FullPath, "acad.stb"));
                var editor = new PlotStyleEditorViewModel(named, folder.FullPath);

                Assert.True(editor.CanAddStyle);
                Assert.False(editor.CanDeleteStyle);
                Assert.False(editor.Rows[0].CanRename);
                Assert.True(editor.Rows[0].IsNameLocked);

                Assert.True(editor.AddStyle());
                Assert.True(editor.AddStyle());
                Assert.Equal(new[] { "Normal", "Style 1", "Style 2" }, editor.Rows.Select(r => r.Name));
                Assert.Equal("Style 2", editor.SelectedRow.Name);
                Assert.True(editor.CanDeleteStyle);
                Assert.Equal("3 plot styles", editor.StyleCountText);

                editor.SelectedRow.Name = "Heavy";
                editor.Rows[1].Name = "heavy";
                Assert.Equal("Style 1", editor.Rows[1].Name);
                Assert.StartsWith("The table has a style named", editor.Message, StringComparison.Ordinal);
                editor.Rows[1].Name = "Thin";
                Assert.Equal("Thin", editor.Rows[1].Name);
                editor.Rows[0].Name = "Other";
                Assert.Equal("Normal", editor.Rows[0].Name);

                Assert.True(editor.DeleteStyle());
                Assert.Equal(new[] { "Normal", "Thin" }, editor.Rows.Select(r => r.Name));
                Assert.Equal("Thin", editor.SelectedRow.Name);
                editor.SelectedRow = editor.Rows[0];
                Assert.False(editor.DeleteStyle());

                Assert.True(editor.Save());
                Assert.Equal(
                    new[] { "Normal", "Thin" },
                    CadPlotStyleSheet.Load(Path.Combine(folder.FullPath, "acad.stb")).Styles.Select(s => s.Name)
                );

                var colour = new PlotStyleEditorViewModel(CadPlotStyleSheet.NewColorDependent(), null);
                Assert.False(colour.AddStyle());
                Assert.False(colour.DeleteStyle());
                colour.Rows[0].Name = "Renamed";
                Assert.Equal("Color_1", colour.Rows[0].Name);
            }
        }

        [Fact]
        public void ThePlotStylesFolder_ListsBothKindsInOrder_EditsAFileByName_MakesAndDeletesTables()
        {
            using (var folder = new TestFolder())
            {
                CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                File.WriteAllBytes(Path.Combine(folder.FullPath, "broken.ctb"), new byte[10]);
                var manager = new PlotStylesManagerViewModel(folder.FullPath, false);

                Assert.Equal(10, manager.Items.Count);
                Assert.Equal("acad.ctb", manager.Items[0].Name);
                Assert.Equal("acad.stb", manager.Items[1].Name);
                Assert.Equal("(does not read)", manager.Items.Single(r => r.Name == "broken.ctb").Description);
                Assert.Equal("Named", manager.Items.Single(r => r.Name == "grayscale.stb").Kind);
                Assert.Equal("Colour-dependent", manager.Items.Single(r => r.Name == "grayscale.ctb").Kind);
                Assert.Equal(
                    "Every colour plots in black.",
                    manager.Items.Single(r => r.Name == "monochrome.ctb").Description
                );
                Assert.Equal("10 plot style tables in " + folder.FullPath, manager.Summary);
                Assert.False(manager.CanEdit);
                Assert.False(manager.Edit());

                PlotStyleEditorViewModel opened = null;
                manager.ShowEditor = editor =>
                {
                    opened = editor;
                    editor.Description = "Changed in the manager";
                    return editor.Save();
                };
                manager.Selected = manager.Items.Single(r => r.Name == "monochrome.ctb");
                Assert.True(manager.CanEdit);
                Assert.True(manager.Edit());
                Assert.Equal("monochrome.ctb", opened.FileName);
                Assert.Equal(
                    "Changed in the manager",
                    manager.Items.Single(r => r.Name == "monochrome.ctb").Description
                );
                Assert.Equal("monochrome.ctb", manager.Selected.Name);

                Assert.False(manager.Edit("broken.ctb"));
                Assert.NotEmpty(manager.Message);

                manager.ShowNew = made =>
                {
                    made.Name = "walls";
                    return made.TryCreate();
                };
                Assert.True(manager.New());
                Assert.Equal("walls.ctb", opened.FileName);
                Assert.Equal("walls.ctb", manager.Selected.Name);

                Assert.True(manager.Delete());
                Assert.False(File.Exists(Path.Combine(folder.FullPath, "walls.ctb")));
                Assert.DoesNotContain(manager.Items, r => r.Name == "walls.ctb");
                Assert.Null(manager.Selected);
                Assert.False(manager.Delete());
            }
        }

        [Fact]
        public void TheViewModel_MakesTheFolderOfTheSettings_WithTheTablesThePresenterMakes_AndGivesItToTheDocuments()
        {
            string beside = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(MainViewModel.DefaultSettingsPath)),
                "PlotStyles"
            );
            var model = new MainViewModel();

            Assert.Equal(beside, model.PlotStyleFolder);
            Assert.True(File.Exists(Path.Combine(beside, "monochrome.ctb")));
            Assert.True(File.Exists(Path.Combine(beside, "acad.stb")));
        }
    }
}
