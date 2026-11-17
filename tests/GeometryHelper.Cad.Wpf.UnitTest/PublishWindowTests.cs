using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// PUBLISH in the window: the sheets of a drawing and of an added file written as one PDF of a page each, the size of each
    /// sheet; a PDF each; sheets for a printer; the Publish dialog's view model (Add Sheets, Remove, Move, Load and Save List,
    /// page setups, the checks Publish makes); the commands through the main view model with the dialog stubbed; and the
    /// dialog laid out offscreen.
    /// </summary>
    public sealed class PublishWindowTests
    {
        /// <summary>A drawing saved in the folder: a line in model space, Layout1 on <paramref name="first"/> and, with <paramref name="second"/>, a layout Sheet 2 on that sheet, each on its side.</summary>
        private static string Site(TestFolder folder, string name, CadPaper first, CadPaper second = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 500, 0)));

            if (second != null)
            {
                var sheet = new DxfLayout { LayoutName = "Sheet 2" };
                sheet.AddToLayoutDictionary(database, null);
                sheet.Initialize();
            }

            string path = Path.Combine(folder.FullPath, name);
            CadDocument document = CadDocument.From(database, path);
            OnPaper(document, "Layout1", first);

            if (second != null)
            {
                OnPaper(document, "Sheet 2", second);
            }

            document.SaveAs(path);
            return path;
        }

        private static void OnPaper(CadDocument document, string layout, CadPaper paper)
        {
            CadSpace space = document.Spaces.Single(s => s.Name == layout);
            CadPlotSettings settings = CadPlotSettings.Read(document, space);
            settings.Paper = paper;
            settings.Landscape = true;
            settings.Area = CadPlotArea.Layout;
            Assert.True(settings.ApplyTo(document, space).Succeeded);
        }

        private static string[] Boxes(string pdfFile)
        {
            string text = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(pdfFile));
            return Regex
                .Matches(text, @"/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value + " " + m.Groups[2].Value)
                .ToArray();
        }

        private static int Pages(string pdfFile) =>
            Regex.Matches(Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(pdfFile)), @"/Type /Page\b").Count;

        private static CadPublishList TwoLayoutsAndAFile(TestFolder folder, out CadDocument site)
        {
            string sitePath = Site(folder, "site.dxf", CadPaper.Standard[3], CadPaper.Standard[1]);
            string annexPath = Site(folder, "annex.dxf", CadPaper.Standard[4]);
            site = CadDocument.Open(sitePath);
            var list = new CadPublishList(site);
            list.Options.IncludeModel = false;
            list.AddCurrentDrawing();
            list.AddDrawing(annexPath, out string problem);
            Assert.Null(problem);
            return list;
        }

        [Fact]
        public void ATwoLayoutDrawingAndAnAddedFile_AreOnePdfOfAPageForEachSheet_EachTheSizeOfItsOwnSheet()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                string pdf = Path.Combine(folder.FullPath, "set.pdf");
                list.Options.FileName = pdf;

                CadPublishResult result = CadPublisher.Publish(list, null);

                Assert.Equal(new[] { pdf }, result.Files);
                Assert.Equal(3, result.Written);
                Assert.Equal(0, result.Failed);
                Assert.Equal(3, Pages(pdf));

                // ISO A3 and ISO A1 of the drawing open, then ISO A4 of the file added, each on its side.
                Assert.Equal(new[] { "1190.5512 841.8898", "2383.937 1683.7795", "841.8898 595.2756" }, Boxes(pdf));
                Pdf.AssertCrossReferencesPointAtTheirObjects(File.ReadAllBytes(pdf));
                Assert.Equal("Published 3 sheets to " + pdf + ".", result.Messages[0]);
                Assert.Equal("Publish complete: 3 sheets published.", result.Messages.Last());
            }
        }

        [Fact]
        public void WithSingleSheetFiles_EachSheetIsAPdfOfOnePage_NamedByTheSheetInTheOutputFolder()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                list.Options.MultiSheet = false;
                string output = Path.Combine(folder.FullPath, "out");
                list.Options.OutputFolder = output;

                CadPublishResult result = CadPublisher.Publish(list, null);

                string[] files = Directory.GetFiles(output).Select(Path.GetFileName).OrderBy(f => f).ToArray();
                Assert.Equal(new[] { "annex-Layout1.pdf", "site-Layout1.pdf", "site-Sheet 2.pdf" }, files);
                Assert.Equal(3, result.Files.Count);
                Assert.All(files, f => Assert.Equal(1, Pages(Path.Combine(output, f))));
                Assert.Equal(new[] { "841.8898 595.2756" }, Boxes(Path.Combine(output, "annex-Layout1.pdf")));
                Assert.Equal(new[] { "2383.937 1683.7795" }, Boxes(Path.Combine(output, "site-Sheet 2.pdf")));
            }
        }

        [Fact]
        public void ASheetWhosePageSetupNamesAPrinterThisComputerHas_IsPrintedThereWithTheCopies_TheOtherIsAPdf()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out CadDocument site);
                site.Spaces.Single(s => s.Name == "Layout1").Layout.PlotConfigurationName = "Office printer";
                list.Options.PublishTo = CadPublishTo.PlotterNamedInPageSetup;
                list.Options.Copies = 3;
                list.Options.OutputFolder = Path.Combine(folder.FullPath, "out");
                var printer = new FakePlotPrinter("Office printer");

                CadPublishResult result = null;
                Exception failure = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        result = CadPublisher.Publish(list, new[] { printer });
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Null(failure);
                PlotPrintJob job = Assert.Single(printer.Jobs);
                Assert.Equal("site-Layout1", job.Title);
                Assert.Equal(3, job.Copies);
                Assert.Equal("A3", job.Paper.Name);
                Assert.Equal(1, result.Printed);
                Assert.Equal(2, result.Written);
                Assert.Contains("Printed site-Layout1 on Office printer, 3 copies.", result.Messages);
                Assert.Equal(
                    new[] { "annex-Layout1.pdf", "site-Sheet 2.pdf" },
                    Directory.GetFiles(Path.Combine(folder.FullPath, "out")).Select(Path.GetFileName).OrderBy(f => f)
                );
            }
        }

        [Fact]
        public void ASheetThatCannotBePublishedIsToldAndTheOthersAreStillWritten_AFailingPrinterToo()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out CadDocument site);
                string dsd = folder.WriteText(
                    "ghost.dsd",
                    "[DWF6Version]\nVer=1\n[DWF6Sheet:ghost-Model]\nDWG="
                        + Path.Combine(folder.FullPath, "ghost.dxf")
                        + "\nLayout=Model\nSetup=\n"
                );
                list.Load(dsd, true);
                list.Options.FileName = Path.Combine(folder.FullPath, "set.pdf");

                CadPublishResult result = CadPublisher.Publish(list, null);

                Assert.Equal(3, result.Written);
                Assert.Equal(1, result.Failed);
                Assert.Contains("ghost-Model: Drawing not found. Not published.", result.Messages);
                Assert.Equal("Publish complete: 3 sheets published, 1 sheet not published.", result.Messages.Last());

                // A printer that fails is told, the sheet after it still goes.
                site.Spaces.Single(s => s.Name == "Layout1").Layout.PlotConfigurationName = "Broken";
                list.Options.PublishTo = CadPublishTo.PlotterNamedInPageSetup;
                list.Options.OutputFolder = Path.Combine(folder.FullPath, "out");
                var printer = new FakePlotPrinter("Broken")
                {
                    FailWith = new InvalidOperationException("The printer is off."),
                };
                CadPublishResult printed = null;
                var thread = new Thread(() => printed = CadPublisher.Publish(list, new[] { printer }));
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Contains("site-Layout1 was not printed: The printer is off.", printed.Messages);
                Assert.Equal(2, printed.Written);
                Assert.Equal(2, printed.Failed);
            }
        }

        [Fact]
        public void TheViewModel_AddsRemovesAndMovesSheets_AndTellsWhatAddSheetsCouldNotRead()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out CadDocument site);
                var dialog = new PublishViewModel(list);
                string annex = Path.Combine(folder.FullPath, "annex.dxf");

                Assert.Equal(
                    new[] { "site-Layout1", "site-Sheet 2", "annex-Layout1" },
                    dialog.Rows.Select(r => r.Name)
                );
                Assert.Equal("3 sheets listed", dialog.Message);
                Assert.False(dialog.CanRemove);

                dialog.Selected = dialog.Rows[2];
                Assert.True(dialog.CanMoveUp);
                Assert.False(dialog.CanMoveDown);
                dialog.MoveUp();
                Assert.Equal(
                    new[] { "site-Layout1", "annex-Layout1", "site-Sheet 2" },
                    dialog.Rows.Select(r => r.Name)
                );
                Assert.Equal("annex-Layout1", dialog.Selected.Name);
                dialog.Remove();
                Assert.Equal(new[] { "site-Layout1", "site-Sheet 2" }, dialog.Rows.Select(r => r.Name));
                Assert.Equal("site-Sheet 2", dialog.Selected.Name);

                dialog.IncludeModel = true;
                dialog.AskSheetFiles = () => new[] { annex, Path.Combine(folder.FullPath, "nowhere.dxf") };
                Assert.Equal(2, dialog.AddSheets());
                Assert.Equal(
                    new[] { "site-Layout1", "site-Sheet 2", "annex-Model", "annex-Layout1" },
                    dialog.Rows.Select(r => r.Name)
                );
                Assert.True(dialog.HasProblem);
                Assert.Equal("nowhere.dxf: Drawing not found", dialog.Message);

                dialog.RemoveAll();
                Assert.Empty(dialog.Rows);
                Assert.False(dialog.HasSheets);
                Assert.Same(site, list.Current);
            }
        }

        [Fact]
        public void ASheetsNameIsEditedInPlace_AndANameAutoCadRefusesIsToldAndNotTaken()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                var dialog = new PublishViewModel(list);

                dialog.Rows[0].Name = "Plan A3";
                Assert.Equal("Plan A3", list.Sheets[0].Name);
                Assert.False(dialog.HasProblem);

                dialog.Rows[0].Name = "SITE-SHEET 2";
                Assert.Equal("Plan A3", dialog.Rows[0].Name);
                Assert.Equal("SITE-SHEET 2: This sheet name already exists.", dialog.Message);
                Assert.True(dialog.HasProblem);

                dialog.Rows[0].Name = "a|b";
                Assert.StartsWith("Invalid characters in sheet name", dialog.Message, StringComparison.Ordinal);
                dialog.Rows[0].Name = "Plan A3 again";
                Assert.False(dialog.HasProblem);
            }
        }

        [Fact]
        public void APageSetupIsChosenInTheRowsList_AnImportedOneToo_AndTheListShowsDefaultNoneFirst()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out CadDocument site);
                CadSpace layout = site.Spaces.Single(s => s.Name == "Layout1");
                CadPlotSettings named = CadPlotSettings.Read(site, layout);
                named.Paper = CadPaper.Standard[2];
                Assert.True(named.SaveAsPageSetup(site, "A2 sheet").Succeeded);
                string otherPath = Site(folder, "other.dxf", CadPaper.Standard[0]);
                CadDocument other = CadDocument.Open(otherPath);
                CadPlotSettings big = CadPlotSettings.Read(other, other.Spaces.Single(s => s.Name == "Layout1"));
                Assert.True(big.SaveAsPageSetup(other, "Big").Succeeded);
                other.SaveAs(otherPath);
                var dialog = new PublishViewModel(list);
                PublishSheetRow row = dialog.Rows[0];

                Assert.Equal("<Default: None>", row.PageSetupChoice);
                Assert.Equal(new[] { "<Default: None>", "A2 sheet", "Import…" }, row.PageSetupChoices);

                row.PageSetupChoice = "A2 sheet";
                Assert.Equal("A2 sheet", list.Sheets[0].PageSetup);
                Assert.Equal("ISO A2", list.SettingsOf(list.Sheets[0]).Paper.Name);

                dialog.AskPageSetupFile = () => otherPath;
                PublishImportChoiceViewModel shown = null;
                dialog.ShowImportChoice = choice =>
                {
                    shown = choice;
                    return true;
                };
                row.PageSetupChoice = PublishSheetRow.ImportChoice;

                // One page setup in the file is taken without asking; the sheet then plots by it.
                Assert.Null(shown);
                Assert.Equal("Big", list.Sheets[0].PageSetup);
                Assert.Equal(otherPath, list.Sheets[0].PageSetupFile);
                Assert.Equal("Imported: Big", row.PageSetupChoice);
                Assert.Equal(new[] { "<Default: None>", "A2 sheet", "Imported: Big", "Import…" }, row.PageSetupChoices);
                Assert.Equal("ISO A0", list.SettingsOf(list.Sheets[0]).Paper.Name);

                row.PageSetupChoice = "<Default: None>";
                Assert.Equal(string.Empty, list.Sheets[0].PageSetup);
            }
        }

        [Fact]
        public void ImportOfAFileWithSeveralPageSetupsAsksWhichOne_AFileWithNoneAndAMissingOneAreTold()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                string otherPath = Site(folder, "other.dxf", CadPaper.Standard[0]);
                CadDocument other = CadDocument.Open(otherPath);
                CadSpace layout = other.Spaces.Single(s => s.Name == "Layout1");
                CadPlotSettings plain = CadPlotSettings.Read(other, layout);
                Assert.True(plain.SaveAsPageSetup(other, "One").Succeeded);
                plain.Paper = CadPaper.Standard[1];
                Assert.True(plain.SaveAsPageSetup(other, "Two").Succeeded);
                other.SaveAs(otherPath);
                var dialog = new PublishViewModel(list);
                dialog.AskPageSetupFile = () => otherPath;
                PublishImportChoiceViewModel shown = null;
                dialog.ShowImportChoice = choice =>
                {
                    shown = choice;
                    choice.Selected = "Two";
                    return true;
                };

                dialog.Rows[1].PageSetupChoice = PublishSheetRow.ImportChoice;

                Assert.Equal(new[] { "One", "Two" }, shown.Names);
                Assert.Equal(otherPath, shown.FileName);
                Assert.Equal("Two", list.Sheets[1].PageSetup);
                Assert.Equal("ISO A1", list.SettingsOf(list.Sheets[1]).Paper.Name);

                dialog.ShowImportChoice = choice => false;
                dialog.Rows[0].PageSetupChoice = PublishSheetRow.ImportChoice;
                Assert.Equal(string.Empty, list.Sheets[0].PageSetup);

                dialog.AskPageSetupFile = () => Path.Combine(folder.FullPath, "annex.dxf");
                dialog.Rows[0].PageSetupChoice = PublishSheetRow.ImportChoice;
                Assert.Equal("The drawing annex.dxf has no page setups.", dialog.Message);

                dialog.AskPageSetupFile = () => Path.Combine(folder.FullPath, "gone.dxf");
                dialog.Rows[0].PageSetupChoice = PublishSheetRow.ImportChoice;
                Assert.Equal("Drawing not found", dialog.Message);
            }
        }

        [Fact]
        public void LoadAndSaveList_AskTheirFiles_ReplaceOrAppend_AndBringTheOptionsBack()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                var dialog = new PublishViewModel(list)
                {
                    PublishTo = PublishViewModel.PdfEntry,
                    CopiesText = "4",
                    IsSingleSheet = true,
                    OpenInViewer = true,
                };
                string file = Path.Combine(folder.FullPath, "set.dsd");
                dialog.AskSaveList = offered =>
                {
                    Assert.Equal(Path.Combine(folder.FullPath, "site.dsd"), offered);
                    return file;
                };

                Assert.True(dialog.SaveList());
                Assert.True(File.Exists(file));

                dialog.RemoveAll();
                dialog.PublishTo = PublishViewModel.PlotterEntry;
                dialog.CopiesText = "1";
                dialog.IsMultiSheet = true;
                dialog.OpenInViewer = false;
                dialog.AskLoadList = () => file;
                Assert.True(dialog.LoadList());

                Assert.Equal(
                    new[] { "site-Layout1", "site-Sheet 2", "annex-Layout1" },
                    dialog.Rows.Select(r => r.Name)
                );
                Assert.Equal(PublishViewModel.PdfEntry, dialog.PublishTo);
                Assert.Equal("4", dialog.CopiesText);
                Assert.False(dialog.IsCopiesEnabled);
                Assert.True(dialog.IsSingleSheet);
                Assert.True(dialog.OpenInViewer);

                // With sheets listed the load asks; Cancel leaves the list as it was, Yes... appends.
                dialog.AskAppend = () => null;
                Assert.False(dialog.LoadList());
                Assert.Equal(3, dialog.Rows.Count);
                dialog.AskAppend = () => true;
                Assert.True(dialog.LoadList());
                Assert.Equal(6, dialog.Rows.Count);
                dialog.AskAppend = () => false;
                Assert.True(dialog.LoadList());
                Assert.Equal(3, dialog.Rows.Count);

                dialog.AskLoadList = () => folder.WriteText("words.dsd", "words");
                Assert.False(dialog.LoadList());
                Assert.Equal("Error reading list file.", dialog.Message);
                dialog.AskSaveList = offered => null;
                Assert.False(dialog.SaveList());
            }
        }

        [Fact]
        public void Publish_RefusesACopiesNumberOutsideOneToNinetyNine_AndAListWithNothingToPublish()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                var dialog = new PublishViewModel(list);

                dialog.CopiesText = "100";
                Assert.False(dialog.Accept());
                Assert.Equal("The number of copies must be between 1 and 99.", dialog.Message);
                dialog.CopiesText = "abc";
                Assert.False(dialog.Accept());
                dialog.CopiesText = "99";
                Assert.True(dialog.Accept());
                Assert.Equal(99, list.Options.Copies);
                Assert.False(dialog.HasProblem);

                dialog.RemoveAll();
                Assert.False(dialog.Accept());
                Assert.Equal("There were no plottable sheets in the current operation.", dialog.Message);
                Assert.True(dialog.IsPdf);
                Assert.False(dialog.IsCopiesEnabled);
                dialog.PublishTo = PublishViewModel.PlotterEntry;
                Assert.False(dialog.IsMultiSheetEnabled);
                Assert.False(dialog.IsFileNameEnabled);
                Assert.True(dialog.IsCopiesEnabled);
            }
        }

        [Fact]
        public void Publish_InTheWindow_ShowsTheDialogOverTheDrawingsSheets_WritesThePdf_AndRemembersTheOptions()
        {
            using (var folder = new TestFolder())
            {
                string sitePath = Site(folder, "site.dxf", CadPaper.Standard[3], CadPaper.Standard[1]);
                string annexPath = Site(folder, "annex.dxf", CadPaper.Standard[4]);
                var model = new MainViewModel();
                Assert.True(model.Open(sitePath));
                PublishViewModel shown = null;
                string pdf = Path.Combine(folder.FullPath, "set.pdf");
                model.ShowPublish = dialog =>
                {
                    shown = dialog;
                    Assert.Equal(
                        new[] { "site-Model", "site-Layout1", "site-Sheet 2" },
                        dialog.Rows.Select(r => r.Name)
                    );
                    dialog.Remove();
                    dialog.Selected = dialog.Rows[0];
                    dialog.Remove();
                    dialog.IncludeModel = false;
                    dialog.AskSheetFiles = () => new[] { annexPath };
                    dialog.AddSheets();
                    dialog.FileName = pdf;
                    return dialog.Accept();
                };

                model.Execute("PUBLISH");

                Assert.False(model.IsCommandRunning);
                Assert.Equal(3, shown.Rows.Count);
                Assert.Contains("Published 3 sheets to " + pdf + ".", model.History);
                Assert.Contains("Publish complete: 3 sheets published.", model.History);
                Assert.Equal(new[] { "1190.5512 841.8898", "2383.937 1683.7795", "841.8898 595.2756" }, Boxes(pdf));

                // The next dialog opens on the options left, the list made from the drawing again.
                PublishViewModel second = null;
                model.ShowPublish = dialog =>
                {
                    second = dialog;
                    return false;
                };
                model.Execute("PUBLISH");

                Assert.Equal(pdf, second.FileName);
                Assert.False(second.IncludeModel);
                Assert.Equal(new[] { "site-Layout1", "site-Sheet 2" }, second.Rows.Select(r => r.Name));
                Assert.Equal("The publishing operation was canceled.", model.History.Last());
            }
        }

        [Fact]
        public void DashPublish_ReadsTheSheetListAndPublishesAtOnce_WithNoDialog()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out CadDocument site);
                list.Options.FileName = Path.Combine(folder.FullPath, "from-list.pdf");
                string dsd = Path.Combine(folder.FullPath, "set.dsd");
                list.Save(dsd);
                var model = new MainViewModel();
                Assert.True(model.Open(site.FileName));
                bool dialogShown = false;
                model.ShowPublish = dialog =>
                {
                    dialogShown = true;
                    return true;
                };

                model.Execute("-PUBLISH");
                Assert.Equal(
                    "Enter name of sheet list <" + Path.Combine(folder.FullPath, "site.dsd") + ">:",
                    model.Session.PromptText
                );
                model.Execute(dsd);

                Assert.False(dialogShown);
                Assert.False(model.IsCommandRunning);
                Assert.Equal(3, Pages(Path.Combine(folder.FullPath, "from-list.pdf")));
                Assert.Contains("Publish complete: 3 sheets published.", model.History);
            }
        }

        [Fact]
        public void Publish_WithNoDialogSet_AsksDashPublishsQuestionOnTheCommandLine_AndOpenInViewerOpensTheFile()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out CadDocument site);
                list.Options.FileName = Path.Combine(folder.FullPath, "viewed.pdf");
                list.Options.OpenInViewer = true;
                list.Save(Path.Combine(folder.FullPath, "site.dsd"));
                var model = new MainViewModel();
                Assert.True(model.Open(site.FileName));
                var opened = new List<string>();
                model.OpenInViewer = opened.Add;

                model.Execute("PUBLISH");
                Assert.StartsWith("Enter name of sheet list", model.Session.PromptText, StringComparison.Ordinal);
                model.Execute(string.Empty);

                Assert.Equal(new[] { Path.Combine(folder.FullPath, "viewed.pdf") }, opened);
            }
        }

        [Fact]
        public void TheOutputTabAndTheAppMenu_StartPublish_AsBatchPlot()
        {
            var model = new MainViewModel();
            AppMenuItemViewModel batch = model
                .CreateAppMenu()
                .Items.Single(i => i.Key == "print")
                .Items.Single(i => i.Name == "Batch Plot");

            Assert.Equal(
                "Plot several drawings and layouts at once to a PDF or a printer (PUBLISH)",
                batch.Description
            );
        }

        [Fact]
        public void TheWindow_IsLaidOutAsTheDesignDrawsIt_SheetListSideButtonsAndThePublishToGroup()
        {
            using (var folder = new TestFolder())
            {
                CadPublishList list = TwoLayoutsAndAFile(folder, out _);
                OnSta(() =>
                {
                    _ = PackUriHelper.UriSchemePack;
                    var window = new PublishWindow(new PublishViewModel(list));
                    Assert.Equal(800.0, window.Width);
                    Assert.Equal("Publish", window.Title);
                    FrameworkElement body = window.Body;
                    object context = window.DataContext;
                    window.Content = null;
                    body.DataContext = context;
                    body.Measure(new Size(800, 640));
                    body.Arrange(new Rect(0, 0, 800, 640));
                    body.UpdateLayout();

                    List<TextBlock> words = Descendants(body).OfType<TextBlock>().ToList();
                    Assert.Contains(words, t => t.Text == "Sheet Name");
                    Assert.Contains(words, t => t.Text == "Page Setup");
                    Assert.Contains(words, t => t.Text == "Status");
                    Assert.Contains(words, t => t.Text == "Include when adding sheets:");
                    Assert.Equal(
                        new[]
                        {
                            "Add Sheets…",
                            "Cancel",
                            "Help",
                            "Load List…",
                            "Move Down",
                            "Move Up",
                            "Publish",
                            "Remove",
                            "Remove All",
                            "Save List…",
                        },
                        Descendants(body)
                            .OfType<Button>()
                            .Select(b => b.Content as string)
                            .Where(c => c != null && c != "Browse…")
                            .OrderBy(c => c, StringComparer.Ordinal)
                    );
                    Assert.Equal(
                        new[] { "Model", "Layouts", "Publish in background", "Open in viewer when done" },
                        Descendants(body).OfType<CheckBox>().Select(c => c.Content as string).Where(c => c != null)
                    );
                    Assert.Equal(
                        new[] { "Multi-sheet file", "Single-sheet files" },
                        Descendants(body).OfType<RadioButton>().Select(r => r.Content as string)
                    );
                    ListBox sheets = Descendants(body).OfType<ListBox>().Single(l => l.Name == "Sheets");
                    Assert.Equal(3, sheets.Items.Count);
                    window.Close();
                });
            }
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
