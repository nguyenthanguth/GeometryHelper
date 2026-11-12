using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Named page setups (PAGESETUP's manager) and the printers of Windows as plotters, with no window: a page setup added,
    /// replaced, set current and imported as one undo step each, the plotter a layout names kept to be chosen again, and the
    /// names AutoCAD refuses.
    /// </summary>
    public class PageSetupTests
    {
        private static CadPlotSettings OnA2()
        {
            var settings = new CadPlotSettings
            {
                Area = CadPlotArea.Window,
                Window = new DisplayBox(120, 60, 7620, 5360),
                FitToPaper = false,
                PaperUnits = 1,
                DrawingUnits = 20,
                Paper = CadPaper.Standard[2],
                StyleTable = CadPlotStyleTable.Grayscale,
            };

            return settings;
        }

        [Fact]
        public void SettingsSavedAsAPageSetup_AreListedByName_ReadBackAfterSaving_AndLeaveTheLayoutAlone()
        {
            CadDocument document = PlotTests.Plan();
            CadPlotSettings before = CadPlotSettings.Read(document, document.ModelSpace);

            CadEditResult result = OnA2().SaveAsPageSetup(document, "A2 plan");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(new[] { "A2 plan" }, CadPlotSettings.PageSetupNames(document));
            CadDocument opened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "plan-level2.dxf");
            CadPlotSettings read = CadPlotSettings.ReadPageSetup(opened, opened.ModelSpace, "a2 PLAN");
            Assert.True(read.Paper.IsSameAs(CadPaper.Standard[2]));
            Assert.Equal(CadPlotArea.Window, read.Area);
            Assert.Equal(new DisplayBox(120, 60, 7620, 5360), read.Window);
            Assert.False(read.FitToPaper);
            Assert.Equal(20.0, read.DrawingUnits);
            Assert.Equal(CadPlotStyleTable.Grayscale, read.StyleTable);
            Assert.Equal(
                CadPlotSettings.PdfDevice,
                ((DxfPlotSettings)document.Database.PlotSettingsDictionary["A2 plan"]).PlotConfigurationName
            );

            // The layout Model is as it was.
            CadPlotSettings model = CadPlotSettings.Read(document, document.ModelSpace);
            Assert.True(before.Paper.IsSameAs(model.Paper));
            Assert.Equal(before.Area, model.Area);
            Assert.Equal(string.Empty, CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));

            Assert.True(document.Undo());
            Assert.Empty(CadPlotSettings.PageSetupNames(document));
        }

        [Fact]
        public void SavingUnderANameTaken_ReplacesThePageSetup_LettersOfEitherCaseTheSame()
        {
            CadDocument document = PlotTests.Plan();
            OnA2().SaveAsPageSetup(document, "Sheet");
            var again = new CadPlotSettings { Paper = CadPaper.Standard[4], Landscape = false };

            CadEditResult result = again.SaveAsPageSetup(document, "  sheet ");

            Assert.True(result.Succeeded, result.Message);
            Assert.Single(CadPlotSettings.PageSetupNames(document));
            CadPlotSettings read = CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "Sheet");
            Assert.True(read.Paper.IsSameAs(CadPaper.Standard[4]));
            Assert.False(read.Landscape);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("A3/plan")]
        [InlineData("<None>")]
        [InlineData("a:b")]
        [InlineData("what?")]
        public void ANameAutoCadRefuses_IsRefused_AndNothingIsAdded(string name)
        {
            CadDocument document = PlotTests.Plan();

            CadEditResult result = OnA2().SaveAsPageSetup(document, name);

            Assert.False(result.Succeeded);
            Assert.NotNull(CadPlotSettings.CheckPageSetupName(name));
            Assert.Empty(CadPlotSettings.PageSetupNames(document));
            Assert.Null(CadPlotSettings.CheckPageSetupName("A3 plan (2)"));
            Assert.NotNull(CadPlotSettings.CheckPageSetupName(new string('x', 256)));
        }

        [Fact]
        public void SetCurrent_CopiesThePageSetupAndItsNameIntoTheLayout_AsOneUndoStep()
        {
            CadDocument document = PlotTests.Plan();
            OnA2().SaveAsPageSetup(document, "A2 plan");
            CadPlotSettings before = CadPlotSettings.Read(document, document.ModelSpace);

            CadEditResult result = CadPlotSettings.SetCurrentPageSetup(document, document.ModelSpace, "a2 plan");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("A2 plan", CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));
            CadPlotSettings now = CadPlotSettings.Read(document, document.ModelSpace);
            Assert.True(now.Paper.IsSameAs(CadPaper.Standard[2]));
            Assert.Equal(CadPlotArea.Window, now.Area);
            Assert.Equal(20.0, now.DrawingUnits);
            Assert.Equal(CadPlotStyleTable.Grayscale, now.StyleTable);
            Assert.True(document.Database.Layouts.Single(l => l.ModelType).ModelType);

            CadDocument opened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "plan-level2.dxf");
            Assert.Equal("A2 plan", CadPlotSettings.CurrentPageSetupName(opened, opened.ModelSpace));

            Assert.True(document.Undo());
            Assert.Equal(string.Empty, CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));
            Assert.True(before.Paper.IsSameAs(CadPlotSettings.Read(document, document.ModelSpace).Paper));
        }

        [Fact]
        public void SetCurrentToAPageSetupTheDrawingHasNot_IsRefused()
        {
            CadDocument document = PlotTests.Plan();

            CadEditResult result = CadPlotSettings.SetCurrentPageSetup(document, document.ModelSpace, "Nothing");

            Assert.False(result.Succeeded);
            Assert.Contains("Nothing", result.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Import_CopiesPageSetupsOfAnotherDrawing_AddingANumberWhereANameIsTaken()
        {
            CadDocument source = PlotTests.Plan();
            OnA2().SaveAsPageSetup(source, "A2 plan");
            new CadPlotSettings { Paper = CadPaper.Standard[4] }.SaveAsPageSetup(source, "Small");
            DxfDatabase file = DxfDatabase.Parse(source.Database.ToText());
            CadDocument target = PlotTests.Plan();

            Assert.Equal(new[] { "A2 plan", "Small" }, CadPlotSettings.PageSetupNames(file));
            CadEditResult first = CadPlotSettings.ImportPageSetups(target, file, new[] { "a2 plan" });
            CadEditResult second = CadPlotSettings.ImportPageSetups(
                target,
                file,
                new[] { "A2 plan", "Small", "Missing" }
            );

            Assert.True(first.Succeeded, first.Message);
            Assert.True(second.Succeeded, second.Message);
            Assert.Equal(new[] { "A2 plan", "A2 plan (2)", "Small" }, CadPlotSettings.PageSetupNames(target));
            CadPlotSettings copy = CadPlotSettings.ReadPageSetup(target, target.ModelSpace, "A2 plan (2)");
            Assert.True(copy.Paper.IsSameAs(CadPaper.Standard[2]));
            Assert.Equal(20.0, copy.DrawingUnits);
            Assert.Equal(
                "A2 plan (2)",
                ((DxfPlotSettings)target.Database.PlotSettingsDictionary["A2 plan (2)"]).PlotSettingsName
            );

            Assert.True(target.Undo());
            Assert.Equal(new[] { "A2 plan" }, CadPlotSettings.PageSetupNames(target));
            Assert.False(CadPlotSettings.ImportPageSetups(target, file, new[] { "Missing" }).Succeeded);
        }

        [Fact]
        public void APlotterTheLayoutNamed_IsKeptToBeChosenAgain_AndAPrinterIsWrittenAsTheLayoutsPlotter()
        {
            CadDocument document = PlotTests.Plan();
            document.Database.Layouts.Single(l => l.ModelType).PlotConfigurationName = "Microsoft Print to PDF";

            CadPlotSettings read = CadPlotSettings.Read(document, document.ModelSpace);
            Assert.Equal("Microsoft Print to PDF", read.SavedPlotter);
            Assert.Equal(CadPlotSettings.PdfDevice, read.DeviceName);
            Assert.False(read.IsPrinter);

            read.UsePrinter(" Microsoft Print to PDF ");
            Assert.True(read.IsPrinter);
            Assert.False(read.IsRaster);
            Assert.Equal("Microsoft Print to PDF", read.DeviceName);
            Assert.True(read.ApplyTo(document, document.ModelSpace).Succeeded);
            Assert.Equal(
                "Microsoft Print to PDF",
                document.Database.Layouts.Single(l => l.ModelType).PlotConfigurationName
            );

            read.UseDevice(CadPlotSettings.PdfDevice);
            Assert.False(read.IsPrinter);
            Assert.Equal(string.Empty, new CadPlotSettings().SavedPlotter);
        }

        [Fact]
        public void APrinterChosenAfterAnImageDevice_GivesBackISOA3AndTheMonochromeTable()
        {
            var settings = new CadPlotSettings();
            settings.UseDevice(CadPlotSettings.PngDevice);
            Assert.True(settings.Paper.IsPixels);

            settings.UsePrinter("HP LaserJet");

            Assert.False(settings.Paper.IsPixels);
            Assert.True(settings.Paper.IsSameAs(CadPaper.Default));
            Assert.Equal(CadPlotStyleTable.Monochrome, settings.StyleTable);
            Assert.Throws<ArgumentException>(() => settings.UsePrinter("  "));
            Assert.Throws<ArgumentNullException>(() => settings.UsePrinter(null));
        }
    }
}
