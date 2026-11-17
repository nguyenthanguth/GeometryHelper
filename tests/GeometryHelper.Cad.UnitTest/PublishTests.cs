using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// PUBLISH's model: the sheet list of the drawing open (Model and layouts, named as AutoCAD names them), sheets added from
    /// other files, removed and moved, page setups per sheet, the sheet list file (.dsd) that round-trips, the plan of what a
    /// publish does with each sheet, and the commands PUBLISH and -PUBLISH with the window's dialog stubbed.
    /// </summary>
    public class PublishTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "publish-" + Guid.NewGuid().ToString("N")
        );

        public PublishTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        /// <summary>A drawing saved as <paramref name="name"/> with a line in model space, Layout1 on ISO A3 and Sheet 2 on ISO A1.</summary>
        private CadDocument Site(string name)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 500, 0)));
            var sheet = new DxfLayout { LayoutName = "Sheet 2" };
            sheet.AddToLayoutDictionary(database, null);
            sheet.Initialize();
            CadDocument document = CadDocument.From(database, Path.Combine(_folder, name));
            OnPaper(document, "Layout1", CadPaper.Standard[3]);
            OnPaper(document, "Sheet 2", CadPaper.Standard[1]);
            document.SaveAs(Path.Combine(_folder, name));
            return CadDocument.Open(Path.Combine(_folder, name));
        }

        private static void OnPaper(CadDocument document, string layout, CadPaper paper)
        {
            CadSpace space = document.Spaces.Single(s => s.Name == layout);
            CadPlotSettings settings = CadPlotSettings.Read(document, space);
            settings.Paper = paper;
            settings.Area = CadPlotArea.Layout;
            Assert.True(settings.ApplyTo(document, space).Succeeded);
        }

        private static string[] Names(CadPublishList list) => list.Sheets.Select(s => s.Name).ToArray();

        [Fact]
        public void TheDrawingOpen_ListsItsModelAndItsLayoutsInTheOrderOfTheTabs_NamedByTheDrawingAndTheSpace()
        {
            CadDocument site = Site("site.dxf");
            var list = new CadPublishList(site);

            IReadOnlyList<CadPublishSheet> added = list.AddCurrentDrawing();

            Assert.Equal(new[] { "site-Model", "site-Layout1", "site-Sheet 2" }, Names(list));
            Assert.Equal(3, added.Count);
            Assert.All(list.Sheets, s => Assert.Equal(CadPublishSheet.Loaded, s.Status));
            Assert.All(list.Sheets, s => Assert.Same(site, s.Document));
            Assert.All(list.Sheets, s => Assert.Equal(site.FileName, s.DrawingFile));
            Assert.True(list.Sheets[0].IsModel);
            Assert.Same(site.ModelSpace, list.Sheets[0].Space);
            Assert.Equal(string.Empty, list.Sheets[1].PageSetup);

            // Asked again, nothing is listed twice.
            Assert.Empty(list.AddCurrentDrawing());
            Assert.Equal(3, list.Sheets.Count);
        }

        [Fact]
        public void IncludeModelAndIncludeLayouts_DecideWhichSpacesAreListed()
        {
            CadDocument site = Site("site.dxf");
            var onlyLayouts = new CadPublishList(site);
            onlyLayouts.Options.IncludeModel = false;
            onlyLayouts.AddCurrentDrawing();
            var onlyModel = new CadPublishList(site);
            onlyModel.Options.IncludeLayouts = false;
            onlyModel.AddCurrentDrawing();

            Assert.Equal(new[] { "site-Layout1", "site-Sheet 2" }, Names(onlyLayouts));
            Assert.Equal(new[] { "site-Model" }, Names(onlyModel));
        }

        [Fact]
        public void AddSheets_ReadsAnotherFileWithTheReaders_ListsItsSpaces_AndSaysWhyAMissingOrBrokenOneWasNotAdded()
        {
            CadDocument site = Site("site.dxf");
            Site("annex.dxf");
            string broken = Path.Combine(_folder, "broken.dxf");
            File.WriteAllText(broken, "this is not a drawing");
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();

            IReadOnlyList<CadPublishSheet> added = list.AddDrawing(
                Path.Combine(_folder, "annex.dxf"),
                out string problem
            );

            Assert.Null(problem);
            Assert.Equal(new[] { "annex-Model", "annex-Layout1", "annex-Sheet 2" }, added.Select(s => s.Name));
            Assert.All(added, s => Assert.NotSame(site, s.Document));
            Assert.All(added, s => Assert.Equal(CadPublishSheet.Loaded, s.Status));
            Assert.Equal(6, list.Sheets.Count);

            Assert.Empty(list.AddDrawing(Path.Combine(_folder, "nothing.dxf"), out problem));
            Assert.Equal("Drawing not found", problem);
            Assert.Empty(list.AddDrawing(broken, out problem));
            Assert.StartsWith("Drawing failed to load", problem, StringComparison.Ordinal);
            Assert.Equal(6, list.Sheets.Count);

            // The drawing that is open is not read again: its sheets are the open ones.
            Assert.Empty(list.AddDrawing(site.FileName, out problem));
        }

        [Fact]
        public void ASheetIsRemovedAndMoved_AndTheEndsOfTheListStay()
        {
            var list = new CadPublishList(Site("site.dxf"));
            list.AddCurrentDrawing();
            CadPublishSheet model = list.Sheets[0];
            CadPublishSheet second = list.Sheets[2];

            Assert.False(list.MoveUp(model));
            Assert.False(list.MoveDown(second));
            Assert.True(list.MoveUp(second));
            Assert.Equal(new[] { "site-Model", "site-Sheet 2", "site-Layout1" }, Names(list));
            Assert.True(list.MoveDown(model));
            Assert.Equal(new[] { "site-Sheet 2", "site-Model", "site-Layout1" }, Names(list));
            Assert.True(list.Remove(model));
            Assert.False(list.Remove(model));
            Assert.Equal(new[] { "site-Sheet 2", "site-Layout1" }, Names(list));
            list.RemoveAll();
            Assert.Empty(list.Sheets);
        }

        [Fact]
        public void ASheetIsRenamed_ButNotToAnotherSheetsNameOrWithLettersAutoCadRefuses()
        {
            var list = new CadPublishList(Site("site.dxf"));
            list.AddCurrentDrawing();
            CadPublishSheet sheet = list.Sheets[1];

            Assert.Null(list.Rename(sheet, "  Plan A3 "));
            Assert.Equal("Plan A3", sheet.Name);
            Assert.Equal("SITE-MODEL: This sheet name already exists.", list.Rename(sheet, "SITE-MODEL"));
            Assert.StartsWith("Invalid characters in sheet name", list.Rename(sheet, "a:b"), StringComparison.Ordinal);
            Assert.Equal("Enter a name for the sheet.", list.Rename(sheet, "  "));
            Assert.Equal("Plan A3", sheet.Name);
            Assert.Null(list.Rename(sheet, "Plan A3"));
        }

        [Fact]
        public void APageSetupOfTheDrawingIsChosenForASheet_ItsSettingsAreTheNamedOnes_AndAMissingNameIsRefused()
        {
            CadDocument site = Site("site.dxf");
            CadSpace layout = site.Spaces.Single(s => s.Name == "Layout1");
            CadPlotSettings named = CadPlotSettings.Read(site, layout);
            named.Paper = CadPaper.Standard[4];
            Assert.True(named.SaveAsPageSetup(site, "A4 sheet").Succeeded);
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            CadPublishSheet sheet = list.Sheets.Single(s => s.LayoutName == "Layout1");

            Assert.Equal(new[] { "A4 sheet" }, list.PageSetupNames(sheet));
            Assert.Equal("ISO A3", list.SettingsOf(sheet).Paper.Name);
            Assert.Null(list.SetPageSetup(sheet, "a4 SHEET"));
            Assert.Equal("A4 sheet", sheet.PageSetup);
            Assert.Equal("ISO A4", list.SettingsOf(sheet).Paper.Name);
            Assert.Equal("Page setup not found", list.SetPageSetup(sheet, "no such"));
            Assert.Equal("A4 sheet", sheet.PageSetup);
            Assert.Null(list.SetPageSetup(sheet, string.Empty));
            Assert.Equal("ISO A3", list.SettingsOf(sheet).Paper.Name);
        }

        [Fact]
        public void APageSetupImportedFromAnotherFile_PlotsTheSheet_WhereTheFileHoldsIt()
        {
            CadDocument other = Site("other.dxf");
            CadSpace layout = other.Spaces.Single(s => s.Name == "Layout1");
            CadPlotSettings named = CadPlotSettings.Read(other, layout);
            named.Paper = CadPaper.Standard[2];
            Assert.True(named.SaveAsPageSetup(other, "Big").Succeeded);
            other.SaveAs(other.FileName);
            CadDocument site = Site("site.dxf");
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            CadPublishSheet sheet = list.Sheets.Single(s => s.LayoutName == "Layout1");

            Assert.Equal(new[] { "Big" }, list.ImportablePageSetups(other.FileName, out string problem));
            Assert.Null(problem);
            Assert.Equal("Page setup not found", list.SetPageSetup(sheet, "Small", other.FileName));
            Assert.Null(list.SetPageSetup(sheet, "big", other.FileName));

            Assert.Equal("Big", sheet.PageSetup);
            Assert.Equal(other.FileName, sheet.PageSetupFile);
            Assert.Equal("ISO A2", list.SettingsOf(sheet).Paper.Name);
            Assert.Empty(list.ImportablePageSetups(Path.Combine(_folder, "gone.dxf"), out problem));
            Assert.Equal("Drawing not found", problem);
        }

        [Fact]
        public void APdfPublish_PlansOneFileOfAPageForEverySheet_AndAPdfEachWhereTheFileIsNotMultiSheet()
        {
            CadDocument site = Site("site.dxf");
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            list.Options.OutputFolder = Path.Combine(_folder, "out");

            IReadOnlyList<CadPublishStep> together = list.Plan(null);

            Assert.Equal(3, together.Count);
            Assert.All(together, s => Assert.Null(s.Skipped));
            Assert.All(together, s => Assert.Null(s.Printer));
            Assert.All(together, s => Assert.Equal(Path.Combine(_folder, "out", "site.pdf"), s.File));
            Assert.All(together, s => Assert.Equal(CadPlotSettings.PdfDevice, s.Settings.DeviceName));
            Assert.Equal(new[] { 420.0, 841.0 }, together.Skip(1).Select(s => s.Settings.Paper.Width));

            list.Options.MultiSheet = false;
            IReadOnlyList<CadPublishStep> apart = list.Plan(null);

            Assert.Equal(
                new[] { "site-Model.pdf", "site-Layout1.pdf", "site-Sheet 2.pdf" },
                apart.Select(s => Path.GetFileName(s.File))
            );

            list.Options.FileName = "all-sheets";
            list.Options.MultiSheet = true;
            Assert.Equal(Path.Combine(_folder, "out", "all-sheets.pdf"), list.MultiSheetFile());
            list.Options.FileName = Path.Combine(_folder, "elsewhere", "set.PDF");
            Assert.Equal(Path.Combine(_folder, "elsewhere", "set.PDF"), list.MultiSheetFile());
        }

        [Fact]
        public void WithNoOutputFolderTheFirstDrawingsFolderIsUsed_AndThePlotStampAndShxGoOnEverySheet()
        {
            var list = new CadPublishList(Site("site.dxf"));
            list.AddCurrentDrawing();
            list.PdfShx = true;
            list.PlotStamp = new CadPlotStamp { On = true };

            Assert.Equal(_folder, list.OutputFolder());
            IReadOnlyList<CadPublishStep> steps = list.Plan(null);

            Assert.All(steps, s => Assert.True(s.Settings.PdfShx));
            Assert.All(steps, s => Assert.NotNull(s.Settings.PlotStamp));
            Assert.NotSame(steps[0].Settings.PlotStamp, steps[1].Settings.PlotStamp);
            Assert.NotSame(list.PlotStamp, steps[0].Settings.PlotStamp);
        }

        [Fact]
        public void ThePlotterNamedInThePageSetup_IsUsedWhereThisComputerHasThePrinter_ElseTheSheetIsAPdfOfItsOwn()
        {
            CadDocument site = Site("site.dxf");
            site.Spaces.Single(s => s.Name == "Layout1").Layout.PlotConfigurationName = "Office printer";
            site.Spaces.Single(s => s.Name == "Sheet 2").Layout.PlotConfigurationName = "DWG To PDF.pc3";
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            list.Options.PublishTo = CadPublishTo.PlotterNamedInPageSetup;

            IReadOnlyList<CadPublishStep> steps = list.Plan(new[] { "office PRINTER", "Another" });

            Assert.Equal(new[] { null, "office PRINTER", null }, steps.Select(s => s.Printer));
            Assert.Equal(
                new[] { "site-Model.pdf", null, "site-Sheet 2.pdf" },
                steps.Select(s => s.File == null ? null : Path.GetFileName(s.File))
            );
            Assert.True(steps[1].Settings.IsPrinter);
            Assert.Equal("office PRINTER", steps[1].Settings.DeviceName);
            Assert.Equal(CadPlotSettings.PdfDevice, steps[2].Settings.DeviceName);
        }

        [Fact]
        public void ASheetWithNothingToPlot_IsPassedOver_AndSoIsOneWhoseDrawingWasNotRead()
        {
            DxfDatabase empty = CadTemplate.CreateMetric();
            CadDocument bare = CadDocument.From(empty, Path.Combine(_folder, "bare.dxf"));
            var list = new CadPublishList(bare);
            list.AddCurrentDrawing();
            string dsd = Path.Combine(_folder, "gone.dsd");
            File.WriteAllText(
                dsd,
                "[DWF6Version]\nVer=1\n[DWF6Sheet:gone-Model]\nDWG="
                    + Path.Combine(_folder, "gone.dxf")
                    + "\nLayout=Model\nSetup=\n"
            );
            list.Load(dsd, true);

            IReadOnlyList<CadPublishStep> steps = list.Plan(null);

            Assert.Equal("Nothing to plot: the plot area is empty.", steps[0].Skipped);
            Assert.Equal("Drawing not found", steps.Last().Skipped);
            Assert.All(steps, s => Assert.Null(s.File));
            Assert.All(steps, s => Assert.Null(s.Settings));
        }

        [Fact]
        public void ASheetListSavedAndLoaded_BringsBackTheSheetsTheirPageSetupsAndTheOptions()
        {
            CadDocument site = Site("site.dxf");
            CadDocument annex = Site("annex.dxf");
            CadSpace layout = site.Spaces.Single(s => s.Name == "Layout1");
            CadPlotSettings named = CadPlotSettings.Read(site, layout);
            named.Paper = CadPaper.Standard[4];
            Assert.True(named.SaveAsPageSetup(site, "A4 sheet").Succeeded);
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            list.AddDrawing(annex.FileName, out _);
            Assert.Null(list.SetPageSetup(list.Sheets[1], "A4 sheet"));
            list.Rename(list.Sheets[4], "Annex plan");
            list.MoveUp(list.Sheets[5]);
            list.Options.MultiSheet = false;
            list.Options.Copies = 3;
            list.Options.OutputFolder = Path.Combine(_folder, "out");
            list.Options.OpenInViewer = true;
            string file = Path.Combine(_folder, "set.dsd");

            list.Save(file);

            var again = new CadPublishList(site);
            IReadOnlyList<string> notes = again.Load(file, false);

            Assert.Empty(notes);
            Assert.Equal(Names(list), Names(again));
            Assert.Equal(list.Sheets.Select(s => s.LayoutName), again.Sheets.Select(s => s.LayoutName));
            Assert.Equal(list.Sheets.Select(s => s.DrawingFile), again.Sheets.Select(s => s.DrawingFile));
            Assert.Equal(list.Sheets.Select(s => s.PageSetup), again.Sheets.Select(s => s.PageSetup));
            Assert.All(again.Sheets, s => Assert.Equal(CadPublishSheet.Loaded, s.Status));
            Assert.Same(site, again.Sheets[0].Document);
            Assert.Equal(CadPublishTo.Pdf, again.Options.PublishTo);
            Assert.False(again.Options.MultiSheet);
            Assert.Equal(3, again.Options.Copies);
            Assert.True(again.Options.OpenInViewer);
            Assert.Equal(Path.Combine(_folder, "out"), again.Options.OutputFolder.TrimEnd('\\', '/'));

            // And once more, the text is the same.
            string second = Path.Combine(_folder, "set2.dsd");
            again.Save(second);
            Assert.Equal(File.ReadAllText(file), File.ReadAllText(second));
        }

        [Fact]
        public void TheFileIsTextOfAutoCadsSectionsAndKeys_WithTypeSixForAMultiSheetPdfAndTwoForThePlotter()
        {
            CadDocument site = Site("site.dxf");
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            string file = Path.Combine(_folder, "set.dsd");

            list.Save(file);
            string text = File.ReadAllText(file);

            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            Assert.Equal(
                new[] { "[DWF6Version]", "Ver=1", "[DWF6MinorVersion]", "MinorVer=1", "[DWF6Sheet:site-Model]" },
                lines.Take(5)
            );
            Assert.Contains("DWG=" + site.FileName, lines);
            Assert.Contains("Layout=Layout1", lines);
            Assert.Contains("Setup=", lines);
            Assert.Contains("[Target]", lines);
            Assert.Contains("Type=6", lines);
            Assert.Contains("DWF=" + Path.Combine(_folder, "site.pdf"), lines);
            Assert.Contains("OUT=" + _folder + "\\", lines);
            Assert.Contains("[SheetSet Properties]", lines);
            Assert.Contains("NoOfCopies=1", lines);
            Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));

            list.Options.MultiSheet = false;
            list.Save(file);
            Assert.Contains("Type=5", File.ReadAllText(file));
            list.Options.PublishTo = CadPublishTo.PlotterNamedInPageSetup;
            list.Save(file);
            Assert.Contains("Type=2", File.ReadAllText(file));
        }

        [Fact]
        public void AListAutoCadWrote_IsRead_ItsOtherSectionsKept_AndDwfBecomesPdfWithANote()
        {
            CadDocument site = Site("site.dxf");
            string annex = Site("annex.dxf").FileName;
            string text =
                "[DWF6Version]\r\nVer=1\r\n[DWF6MinorVersion]\r\nMinorVer=1\r\n"
                + "[DWF6Sheet:annex-Layout1]\r\nDWG=\""
                + annex
                + "\"\r\nLayout=Layout1\r\nSetup=\r\nOriginalSheetPath="
                + annex
                + "\r\nHas Plot Port=0\r\nHas3DDWF=0\r\n"
                + "[DWF6Sheet:annex-Sheet 2]\r\nDWG="
                + annex
                + "\r\nLayout=Sheet 2\r\nSetup=\r\nOriginalSheetPath="
                + annex
                + "\r\nHas Plot Port=0\r\nHas3DDWF=0\r\n"
                + "[DWF6Sheet:annex-Plan]\r\nDWG="
                + annex
                + "\r\nLayout=Plan\r\nSetup=\r\n"
                + "[Target]\r\nType=1\r\nDWF="
                + Path.Combine(_folder, "multi.dwf")
                + "\r\nOUT="
                + _folder
                + "\\\r\nPWD=\r\n"
                + "[PdfOptions]\r\nIncludeHyperlinks=TRUE\r\nVectorResolution=1200\r\n"
                + "[SheetSet Properties]\r\nIsSheetSet=FALSE\r\nNoOfCopies=2\r\nViewFile=TRUE\r\n";
            string file = Path.Combine(_folder, "acad.dsd");
            File.WriteAllText(file, text);
            var list = new CadPublishList(site);

            IReadOnlyList<string> notes = list.Load(file, false);

            Assert.Equal(new[] { "annex-Layout1", "annex-Sheet 2", "annex-Plan" }, Names(list));
            Assert.Equal(
                new[]
                {
                    "annex-Plan: Layout not found",
                    "DWF and DWFx files have no writer in the presenter: the sheets are published as PDF.",
                },
                notes
            );
            Assert.Equal(CadPublishTo.Pdf, list.Options.PublishTo);
            Assert.True(list.Options.MultiSheet);
            Assert.Equal(Path.Combine(_folder, "multi.pdf"), list.Options.FileName);
            Assert.Equal(2, list.Options.Copies);
            Assert.True(list.Options.OpenInViewer);

            string saved = Path.Combine(_folder, "saved.dsd");
            list.Save(saved);
            string again = File.ReadAllText(saved);
            Assert.Contains("[PdfOptions]\r\nIncludeHyperlinks=TRUE\r\nVectorResolution=1200\r\n", again);
            Assert.Contains("IsSheetSet=FALSE", again);
            Assert.Contains("Type=6", again);
        }

        [Fact]
        public void ARelativePathInAListIsTakenFromTheListsFolder_AndAnImportedSetupIsWrittenAsNameBarFile()
        {
            CadDocument other = Site("other.dxf");
            CadSpace layout = other.Spaces.Single(s => s.Name == "Layout1");
            CadPlotSettings named = CadPlotSettings.Read(other, layout);
            Assert.True(named.SaveAsPageSetup(other, "Big").Succeeded);
            other.SaveAs(other.FileName);
            string file = Path.Combine(_folder, "rel.dsd");
            File.WriteAllText(
                file,
                "[DWF6Version]\nVer=1\n[DWF6Sheet:x-1]\nDWG=other.dxf\nLayout=Layout1\nSetup=Big|"
                    + other.FileName
                    + "\n"
            );
            var list = new CadPublishList(null);

            Assert.Empty(list.Load(file, false));

            CadPublishSheet sheet = Assert.Single(list.Sheets);
            Assert.Equal(other.FileName, sheet.DrawingFile);
            Assert.Equal("Big", sheet.PageSetup);
            Assert.Equal(other.FileName, sheet.PageSetupFile);
            list.Save(file);
            Assert.Contains("Setup=Big|" + other.FileName, File.ReadAllText(file));
        }

        [Fact]
        public void ALoadCanAppend_AndANameTheListHasIsGivenANumber()
        {
            var list = new CadPublishList(Site("site.dxf"));
            list.AddCurrentDrawing();
            string file = Path.Combine(_folder, "set.dsd");
            list.Save(file);

            list.Load(file, true);

            Assert.Equal(6, list.Sheets.Count);
            Assert.Equal(
                new[]
                {
                    "site-Model",
                    "site-Layout1",
                    "site-Sheet 2",
                    "site-Model (2)",
                    "site-Layout1 (2)",
                    "site-Sheet 2 (2)",
                },
                Names(list)
            );
            list.Load(file, false);
            Assert.Equal(3, list.Sheets.Count);
        }

        [Fact]
        public void AFileThatIsNoSheetListIsRefused_AndNonAsciiLettersSurviveTheTrip()
        {
            var list = new CadPublishList(Site("site.dxf"));
            string file = Path.Combine(_folder, "text.dsd");
            File.WriteAllText(file, "just some words\n");
            Assert.Throws<FormatException>(() => list.Load(file, false));

            list.AddCurrentDrawing();
            Assert.Null(list.Rename(list.Sheets[0], "Mặt bằng tầng 2"));
            list.Save(file);
            Assert.Equal(0xFF, File.ReadAllBytes(file)[0]);
            var again = new CadPublishList(null);
            again.Load(file, false);

            Assert.Equal("Mặt bằng tầng 2", again.Sheets[0].Name);
        }

        [Fact]
        public void TheNumberOfCopiesIsOneToNinetyNine()
        {
            var options = new CadPublishOptions();

            options.Copies = 99;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Copies = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Copies = 100);
            Assert.Equal(99, options.Copies);
            Assert.Equal(CadPublishTo.Pdf, options.PublishTo);
            Assert.True(options.MultiSheet);
        }

        [Fact]
        public void Publish_HandsTheWindowTheListOfTheDrawingOpen_AndWithTheDialogShownTheCommandEnds()
        {
            CadDocument site = Site("site.dxf");
            CadCommandSession session = Session(site);
            CadPublishList got = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PUBLISH")
                {
                    got = (CadPublishList)e.Payload;
                    e.Handled = true;
                }
            };

            Run(session, "PUBLISH");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "site-Model", "site-Layout1", "site-Sheet 2" }, Names(got));
            Assert.Same(site, got.Current);
            Assert.Null(got.PlotStamp);
        }

        [Fact]
        public void Publish_WithNoWindowAsksDashPublishsQuestion_ReadsTheListAndHandsItOver()
        {
            CadDocument site = Site("site.dxf");
            var list = new CadPublishList(site);
            list.AddCurrentDrawing();
            list.Options.MultiSheet = false;
            list.Save(Path.Combine(_folder, "site.dsd"));
            CadCommandSession session = Session(site);
            CadPublishList got = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "-PUBLISH")
                {
                    got = (CadPublishList)e.Payload;
                    e.Handled = true;
                }
            };

            Run(session, "PUBLISH");
            Assert.Equal("Enter name of sheet list <" + Path.Combine(_folder, "site.dsd") + ">:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(3, got.Sheets.Count);
            Assert.False(got.Options.MultiSheet);
        }

        [Fact]
        public void DashPublish_SaysWhenTheListIsMissingUnreadableOrEmpty_AndWhenThereIsNoWriter()
        {
            CadDocument site = Site("site.dxf");
            CadCommandSession session = Session(site);

            Run(session, "-PUBLISH", "nothing.dsd");
            Assert.Equal(
                new[] { Path.Combine(_folder, "nothing.dsd"), "Could not locate sheet list file." },
                session.History.Skip(session.History.Count - 2)
            );

            File.WriteAllText(Path.Combine(_folder, "words.dsd"), "words");
            Run(session, "-PUBLISH", "words.dsd");
            Assert.Equal("Error reading list file.", session.History.Last());

            File.WriteAllText(
                Path.Combine(_folder, "ghost.dsd"),
                "[DWF6Version]\nVer=1\n[DWF6Sheet:ghost-Model]\nDWG="
                    + Path.Combine(_folder, "ghost.dxf")
                    + "\nLayout=Model\nSetup=\n"
            );
            Run(session, "-PUBLISH", "ghost.dsd");
            Assert.Equal("There were no plottable sheets in the current operation.", session.History.Last());
            Assert.Contains("ghost-Model: Drawing not found", session.History);

            new CadPublishList(site).Save(Path.Combine(_folder, "site.dsd"));
            var withSheets = new CadPublishList(site);
            withSheets.AddCurrentDrawing();
            withSheets.Save(Path.Combine(_folder, "site.dsd"));
            Run(session, "-PUBLISH", Enter);
            Assert.Equal("Publishing needs the window's writers: nothing was written.", session.History.Last());
        }

        [Fact]
        public void TheSessionsPlotStampAndPdfShxRideOnTheList()
        {
            CadDocument site = Site("site.dxf");
            CadCommandSession session = Session(site);
            session.PlotStamp.On = true;
            CadPublishList got = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PUBLISH")
                {
                    got = (CadPublishList)e.Payload;
                    e.Handled = true;
                }
            };
            Run(session, "PDFSHX", "1");

            Run(session, "PUBLISH");

            Assert.NotNull(got.PlotStamp);
            Assert.True(got.PdfShx);
        }
    }
}
