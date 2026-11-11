using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PLOT's model (C15): a plot laid on its sheet, with the numbers AutoCAD gives it; the plot style tables made in code;
    /// the page setup written into a LAYOUT's plot settings and read back after saving, as Apply to Layout does; the list of
    /// scales; and -PLOT's prompts in AutoCAD 2021's words.
    /// </summary>
    public class PlotTests
    {
        /// <summary>A plan of model space: a frame from (120, 60) to (7 620, 5 360), 7 500 by 5 300, and ROOM 1 in it.</summary>
        internal static CadDocument Plan()
        {
            var database = new DxfDatabase("AC1027");
            var corners = new[]
            {
                new GeoPoint3(120, 60, 0),
                new GeoPoint3(7620, 60, 0),
                new GeoPoint3(7620, 5360, 0),
                new GeoPoint3(120, 5360, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "ROOM 1",
                    Position = new GeoPoint3(1500, 2500, 0),
                    Height = 250,
                }
            );
            return CadDocument.From(database, "plan-level2.dxf");
        }

        private static CadPlotSettings Window(double drawingUnits) =>
            new CadPlotSettings
            {
                Area = CadPlotArea.Window,
                Window = new DisplayBox(120, 60, 7620, 5360),
                FitToPaper = false,
                PaperUnits = 1,
                DrawingUnits = drawingUnits,
            };

        [Fact]
        public void AWindowAtOneToTwentyCentredOnA3_Is375By265_Moved17Point5And11FromThePrintableCorner()
        {
            CadDocument document = Plan();

            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, Window(20), DisplayBox.Empty);

            Assert.Equal(420.0, page.SheetWidth, 12);
            Assert.Equal(297.0, page.SheetHeight, 12);
            Assert.Equal(1190.5511811023621, page.SheetWidthPoints, 9);
            Assert.Equal(841.88976377952747, page.SheetHeightPoints, 9);
            Assert.Equal(new DisplayBox(5, 5, 415, 292), page.Printable);
            Assert.Equal(0.05, page.Scale, 15);
            Assert.Equal(17.5, page.OffsetX, 9);
            Assert.Equal(11.0, page.OffsetY, 9);
            Assert.Equal(22.5, page.DrawingToSheet.X(120, 60), 9);
            Assert.Equal(16.0, page.DrawingToSheet.Y(120, 60), 9);
            Assert.Equal(397.5, page.DrawingToSheet.X(7620, 5360), 9);
            Assert.Equal(281.0, page.DrawingToSheet.Y(7620, 5360), 9);

            // On the PDF page, Y downward from the top: (120, 60) is 16 mm above the bottom, 281 mm below the top.
            Assert.Equal(22.5 * 72 / 25.4, page.DrawingToPoints.X(120, 60), 9);
            Assert.Equal(281.0 * 72 / 25.4, page.DrawingToPoints.Y(120, 60), 9);
        }

        [Fact]
        public void FittedTheWindowIsOneMillimetreTo18Point4669_406Point14By287_AndTurnedItStandsUpright()
        {
            CadDocument document = Plan();
            CadPlotSettings settings = Window(20);
            settings.FitToPaper = true;

            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);

            Assert.Equal(287.0 / 5300.0, page.Scale, 15);
            Assert.Equal(18.4669, 1.0 / page.Scale, 4);
            Assert.Equal(406.1321, page.Area.Width * page.Scale, 4);
            Assert.Equal(1.9340, page.OffsetX, 4);
            Assert.Equal(0.0, page.OffsetY, 9);

            // Upright the sheet is 297 by 420 and the window fits its width: 287 / 7 500.
            settings.Landscape = false;
            page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            Assert.Equal(297.0, page.SheetWidth, 12);
            Assert.Equal(420.0, page.SheetHeight, 12);
            Assert.Equal(287.0 / 7500.0, page.Scale, 15);
        }

        [Fact]
        public void UpsideDown_TurnsTheSheetHalfRoundAboutItsMiddle_AndNotCentred_TheOffsetPlacesThePlot()
        {
            CadDocument document = Plan();
            CadPlotSettings settings = Window(20);
            settings.UpsideDown = true;

            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            Assert.Equal(397.5, page.DrawingToSheet.X(120, 60), 9);
            Assert.Equal(281.0, page.DrawingToSheet.Y(120, 60), 9);

            settings.UpsideDown = false;
            settings.Centered = false;
            settings.OffsetX = 3;
            settings.OffsetY = 4;
            page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            Assert.Equal(8.0, page.DrawingToSheet.X(120, 60), 9);
            Assert.Equal(9.0, page.DrawingToSheet.Y(120, 60), 9);
        }

        [Fact]
        public void TheExtentsTheLimitsAndTheDisplay_AreTheAreasPlotted()
        {
            CadDocument document = Plan();
            document.Database.Limmin = new GeoPoint2(0, 0);
            document.Database.Limmax = new GeoPoint2(8400, 5940);
            var settings = new CadPlotSettings { Area = CadPlotArea.Extents };

            Assert.Equal(
                new DisplayBox(120, 60, 7620, 5360),
                CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty).Area
            );

            settings.Area = CadPlotArea.Limits;
            CadPlotPage limits = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            Assert.Equal(new DisplayBox(0, 0, 8400, 5940), limits.Area);

            // A0 to A4 are of one shape: the limits of 8 400 by 5 940 fill the printable area's height, 287 / 5 940.
            Assert.Equal(287.0 / 5940.0, limits.Scale, 15);

            settings.Area = CadPlotArea.Display;
            Assert.Equal(
                new DisplayBox(0, 0, 1000, 500),
                CadPlotPage.Compute(document, document.ModelSpace, settings, new DisplayBox(0, 0, 1000, 500)).Area
            );
        }

        [Fact]
        public void ALayoutPlottedAsTheLayout_PutsThePapersOriginAtThePrintableCorner_AtOneToOne()
        {
            CadDocument document = LayoutTests.TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");

            CadPlotSettings settings = CadPlotSettings.Read(document, plan);
            Assert.Equal(CadPlotArea.Layout, settings.Area);
            Assert.False(settings.FitToPaper);
            Assert.Equal("ISO A3", settings.Paper.Name);
            Assert.Equal(7.5, settings.Paper.MarginLeft, 12);
            Assert.Equal(20.0, settings.Paper.MarginBottom, 12);

            CadPlotPage page = CadPlotPage.Compute(document, plan, settings, DisplayBox.Empty);

            // The sheet the layout tab shows, (-7.5, -20) to (412.5, 277), lies on the whole of the A3 sheet.
            Assert.Equal(new DisplayBox(-7.5, -20, 412.5, 277), page.Area);
            Assert.Equal(1.0, page.Scale, 15);
            Assert.Equal(7.5, page.DrawingToSheet.X(0, 0), 12);
            Assert.Equal(20.0, page.DrawingToSheet.Y(0, 0), 12);
            Assert.Equal(0.0, page.DrawingToSheet.X(-7.5, -20), 12);
            Assert.Equal(420.0, page.DrawingToSheet.X(412.5, 277), 12);
        }

        [Fact]
        public void MonochromePlotsEveryColourBlack_GrayscaleTheGreyOfItsBrightness_AndWhitePlotsBlackWithEach()
        {
            Assert.Equal(0x000000, CadPlotStyles.Map(CadPlotStyleTable.Monochrome, 0xFF0000));
            Assert.Equal(0x000000, CadPlotStyles.Map(CadPlotStyleTable.Monochrome, 0xFFFF00));
            Assert.Equal(0x4C4C4C, CadPlotStyles.Map(CadPlotStyleTable.Grayscale, 0xFF0000));
            Assert.Equal(0x969696, CadPlotStyles.Map(CadPlotStyleTable.Grayscale, 0x00FF00));
            Assert.Equal(0x1D1D1D, CadPlotStyles.Map(CadPlotStyleTable.Grayscale, 0x0000FF));
            Assert.Equal(0x808080, CadPlotStyles.Map(CadPlotStyleTable.Grayscale, 0x808080));
            Assert.Equal(0xFF0000, CadPlotStyles.Map(CadPlotStyleTable.None, 0xFF0000));
            Assert.Equal(0x000000, CadPlotStyles.Map(CadPlotStyleTable.None, 0xFFFFFF));
            Assert.Equal(0x000000, CadPlotStyles.Map(CadPlotStyleTable.Grayscale, 0xFFFFFF));
            Assert.Equal(0x000000, CadPlotStyles.Map(CadPlotStyleTable.Monochrome, 0xFFFFFF));
        }

        [Fact]
        public void APageSetupAppliedToModel_ReadsBackAfterSaving_AndOneUndoTakesItBack()
        {
            CadDocument document = Plan();
            CadPlotSettings before = CadPlotSettings.Read(document, document.ModelSpace);
            CadPlotSettings settings = Window(20);
            settings.Paper = CadPaper.Standard[1];
            settings.Landscape = false;
            settings.UpsideDown = true;
            settings.Centered = false;
            settings.OffsetX = 3;
            settings.OffsetY = 4;
            settings.StyleTable = CadPlotStyleTable.Grayscale;
            settings.PlotWithPlotStyles = false;
            settings.PlotLineweights = false;
            settings.ScaleLineweights = true;

            CadEditResult result = settings.ApplyTo(document, document.ModelSpace);

            Assert.True(result.Succeeded, result.Message);
            DxfLayout model = document.Database.Layouts.Single(l => l.LayoutName == "Model");
            Assert.Equal("ISO_A1_(841.00_x_594.00_MM)", model.CanonicalMediaName);
            Assert.Equal(CadPlotSettings.PdfDevice, model.PlotConfigurationName);
            Assert.Equal(DxfPlotRotation.Degrees270, model.PlotRotation);
            Assert.Equal(DxfPlotType.Window, model.PlotType);
            Assert.Equal(DxfStdScaleType.StdScale1To20, model.StdScaleType);
            Assert.Equal("grayscale.ctb", model.CurrentStyleSheet);

            CadDocument opened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "plan-level2.dxf");
            CadPlotSettings read = CadPlotSettings.Read(opened, opened.ModelSpace);
            Assert.True(read.Paper.IsSameAs(CadPaper.Standard[1]));
            Assert.False(read.Landscape);
            Assert.True(read.UpsideDown);
            Assert.Equal(CadPlotArea.Window, read.Area);
            Assert.Equal(new DisplayBox(120, 60, 7620, 5360), read.Window);
            Assert.False(read.FitToPaper);
            Assert.Equal(1.0, read.PaperUnits);
            Assert.Equal(20.0, read.DrawingUnits);
            Assert.False(read.Centered);
            Assert.Equal(3.0, read.OffsetX);
            Assert.Equal(4.0, read.OffsetY);
            Assert.Equal(CadPlotStyleTable.Grayscale, read.StyleTable);
            Assert.False(read.PlotWithPlotStyles);
            Assert.False(read.PlotLineweights);
            Assert.True(read.ScaleLineweights);

            Assert.True(document.Undo());
            CadPlotSettings undone = CadPlotSettings.Read(document, document.ModelSpace);
            Assert.Equal(before.Area, undone.Area);
            Assert.True(before.Paper.IsSameAs(undone.Paper));
            Assert.Equal(before.Landscape, undone.Landscape);
        }

        [Fact]
        public void APageSetupAppliedToALayout_MovesItsSheetWithIt_AndFittedIsWrittenAsScaledToFit()
        {
            CadDocument document = LayoutTests.TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            Assert.Equal(new DisplayBox(-7.5, -20, 412.5, 277), plan.PaperBounds);
            CadPlotSettings settings = CadPlotSettings.Read(document, plan);
            settings.Paper = CadPaper.Standard[2];
            settings.FitToPaper = true;

            Assert.True(settings.ApplyTo(document, plan).Succeeded);

            // A2 on its side with margins of 5: the paper (-5, -5) to (589, 415), read again from the plot settings.
            plan = document.Spaces.Single(s => s.Name == "Plan");
            Assert.Equal(new DisplayBox(-5, -5, 589, 415), plan.PaperBounds);
            Assert.True(plan.Layout.UseStandardScale);
            Assert.Equal(DxfStdScaleType.ScaleToFit, plan.Layout.StdScaleType);
            Assert.Equal(DxfPlotType.Layout, plan.Layout.PlotType);
            Assert.True(CadPlotSettings.Read(document, plan).FitToPaper);
        }

        [Fact]
        public void TheScaleList_IsOneToOneToOneToAThousand_ThenTheDrawingsOwnNotAlreadyListed()
        {
            var database = new DxfDatabase("AC1027");
            DxfObjectContextCollection scales = database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );
            scales.AddContext(
                new DxfAnnotationScale
                {
                    Name = "1:25",
                    PaperUnits = 1,
                    DrawingUnits = 25,
                }
            );

            var names = CadPlotScale.ListFor(database).Select(s => s.Name).ToArray();

            Assert.Equal(
                new[] { "1:1", "1:2", "1:5", "1:10", "1:20", "1:50", "1:100", "1:200", "1:500", "1:1000" },
                names.Take(10).ToArray()
            );
            Assert.Contains("1:25", names);
            Assert.Single(names, n => n == "1:20");
            Assert.Equal(names.Length, names.Distinct().Count());
        }

        [Fact]
        public void DashPlot_AsksAutoCadsPrompts_SavesThePageSetup_AndHandsTheFileToTheWindow()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadPlotJob job = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "-PLOT")
                {
                    job = e.Plot;
                    e.Handled = true;
                }
            };
            string file = Path.Combine(TestRun.Folder("plot"), "plan-level2-Model.pdf");

            Run(session, "-PLOT");
            Assert.Equal("Detailed plot configuration? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Enter a layout name or [?] <Model>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter an output device name or [?] <GeometryHelper PDF>:", session.PromptText);
            Run(session, "DWG To PDF.pc3");
            Assert.Contains("not found", session.History.Last());
            Run(session, Enter);
            Assert.StartsWith("Enter paper size or [?] <", session.PromptText);
            Run(session, "ISO A3");
            Assert.Equal("Enter paper units [Inches/Millimeters] <Millimeters>:", session.PromptText);
            Run(session, Enter);
            Assert.StartsWith("Enter drawing orientation [Portrait/Landscape] <", session.PromptText);
            Run(session, "L");
            Assert.Equal("Plot upside down? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);
            Assert.StartsWith("Enter plot area [Display/Extents/Limits/Window] <", session.PromptText);
            Run(session, "W");
            Assert.StartsWith("Enter lower left corner of window <", session.PromptText);
            Run(session, "120,60", "7620,5360");
            Assert.StartsWith("Enter plot scale (Plotted Millimeters=Drawing Units) or [Fit] <", session.PromptText);
            Run(session, "1:0");
            Assert.Equal("Requires a scale, as 1=20, or Fit.", session.History.Last());
            Run(session, "1=20");
            Assert.StartsWith("Enter plot offset (x,y) or [Center] <", session.PromptText);
            Run(session, "C", "Y", "monochrome", "Y");
            Assert.Equal(
                "Enter shade plot setting [As displayed/legacy Wireframe/legacy Hidden/Visual styles/Rendered] <As displayed>:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.StartsWith("Enter file name <", session.PromptText);
            Run(session, file);
            Assert.Equal("Save changes to page setup [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Proceed with plot [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.NotNull(job);
            Assert.Equal(file, job.FileName);
            Assert.Same(document.ModelSpace, job.Space);
            Assert.Equal(new DisplayBox(120, 60, 7620, 5360), job.Settings.Window);
            Assert.Equal(20.0, job.Settings.DrawingUnits);
            Assert.True(job.Settings.Centered);
            Assert.Equal(CadPlotStyleTable.Monochrome, job.Settings.StyleTable);
            Assert.Contains("Effective plotting area:  375.00 wide by 265.00 high", session.History);

            CadPlotSettings saved = CadPlotSettings.Read(document, document.ModelSpace);
            Assert.Equal(CadPlotArea.Window, saved.Area);
            Assert.Equal(20.0, saved.DrawingUnits);
            Assert.True(document.Undo());
            Assert.NotEqual(CadPlotArea.Window, CadPlotSettings.Read(document, document.ModelSpace).Area);
        }

        [Fact]
        public void DashPlotToGeometryHelperPng_AsksAnImageSizeInPixels_AndHandsAPngFileToTheWindow_ThePageSetupKeepingThePixels()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadPlotJob job = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "-PLOT")
                {
                    job = e.Plot;
                    e.Handled = true;
                }
            };
            string file = Path.Combine(TestRun.Folder("plot"), "plan-level2-Model");

            Run(session, "-PLOT", "Y", Enter);
            Run(session, "?");
            Assert.Contains("GeometryHelper PNG", session.History);
            Assert.Contains("GeometryHelper JPG", session.History);
            Run(session, "geometryhelper png");
            Assert.Equal("Enter paper size or [?] <1920 x 1080 Pixels (Full HD)>:", session.PromptText);
            Run(session, "9000 x 10");
            Assert.Contains("not found", session.History.Last());
            Run(session, "2480 x 1754");
            Assert.Equal("Enter paper units [Pixels] <Pixels>:", session.PromptText);
            Run(session, Enter, Enter, Enter, "E");
            Assert.StartsWith("Enter plot scale (Plotted Pixels=Drawing Units) or [Fit] <Fit>:", session.PromptText);
            Run(session, Enter, Enter, Enter, Enter, Enter, Enter);
            Assert.StartsWith("Enter file name <", session.PromptText);
            Assert.EndsWith("plan-level2-Model.png>:", session.PromptText);
            Run(session, file, "Y", Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(file + ".png", job.FileName);
            Assert.Equal(CadPlotSettings.PngDevice, job.Settings.DeviceName);
            Assert.Equal("2480 x 1754 Pixels (Custom)", job.Settings.Paper.Label);
            Assert.Equal(CadPlotStyleTable.None, job.Settings.StyleTable);

            // The page setup saved keeps the device and the pixels (group 72 = 2).
            CadPlotSettings saved = CadPlotSettings.Read(document, document.ModelSpace);
            Assert.Equal(CadPlotSettings.PngDevice, saved.DeviceName);
            Assert.True(saved.Paper.IsPixels);
            Assert.Equal(2480.0, saved.Paper.Width);
            Assert.Equal(1754.0, saved.Paper.Height);
        }

        [Fact]
        public void AnImageSizeIs16To8000PixelsASide_AndTheListedOnesAreFound()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CadPaper.Pixels(15, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => CadPaper.Pixels(100, 8001));
            Assert.Same(CadPaper.DefaultPixels, CadPaper.Pixels(1920, 1080));
            Assert.Equal("16 x 8000 Pixels (Custom)", CadPaper.Pixels(16, 8000).Label);
            Assert.Equal(0.0, CadPaper.DefaultPixels.MarginLeft);
            Assert.False(CadPaper.Pixels(800, 600).IsSameAs(new CadPaper("800 x 600 Pixels", 800, 600, 0, 0, 0, 0)));
            Assert.Equal("Custom…", CadPaper.CustomPixelsChoice.Label);
        }

        [Fact]
        public void PlotWithNoWindow_RunsDashPlotsPrompts_AndPreviewAndPageSetupSayTheWindowShowsThem()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "PLOT");
            Assert.Equal("Detailed plot configuration? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter, Enter);
            Assert.Equal("Enter a page setup name:", session.PromptText);
            Run(session, "A3 plan");
            Assert.Equal("Page setup \"A3 plan\" not found.", session.History.Last());
            Run(session, Enter, Enter, Enter, Enter, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(
                "Plotting to a file needs the window's PDF writer: nothing was written.",
                session.History.Last()
            );

            Run(session, "PREVIEW");
            Assert.Equal("PREVIEW is shown by the window.", session.History.Last());
            Run(session, "PAGESETUP");
            Assert.Equal("PAGESETUP is shown by the window.", session.History.Last());
            Assert.Equal("PLOT", CadShortcuts.Default.Keys["Ctrl+P"]);
        }

        [Fact]
        public void PlotWithCmddia0_AsksDashPlotsPrompts_ThoughAWindowWouldShowTheDialog()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            var asked = new List<string>();
            session.UiRequested += (sender, e) =>
            {
                asked.Add(e.Name);
                e.Handled = e.Name == "PLOT";
            };

            Run(session, "PLOT");
            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "PLOT" }, asked);

            Run(session, "CMDDIA", "0", "PLOT");
            Assert.Equal("Detailed plot configuration? [Yes/No] <No>:", session.PromptText);
            Run(session, Esc, "CMDDIA");
            Assert.Equal("Enter new value for CMDDIA <0>:", session.PromptText);
            Assert.Equal(new[] { "PLOT" }, asked);
        }
    }
}
