using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What the right-click menu of the Page Setup Manager does to a page setup (Rename and Delete), each one undo step
    /// named PAGESETUP, a layout set to the page setup following its new name, and the page setups of another drawing read
    /// without a document, as Import Page Setups lists them with the Model or Layout each was made for.
    /// </summary>
    public class PageSetupRenameDeleteTests
    {
        private static CadDocument WithTwoSetups()
        {
            CadDocument document = PlotTests.Plan();
            var onA2 = new CadPlotSettings
            {
                Paper = CadPaper.Standard[2],
                DrawingUnits = 20,
                FitToPaper = false,
            };
            Assert.True(onA2.SaveAsPageSetup(document, "A2 plan").Succeeded);
            Assert.True(
                new CadPlotSettings { Paper = CadPaper.Standard[4] }
                    .SaveAsPageSetup(document, "Small")
                    .Succeeded
            );
            return document;
        }

        [Fact]
        public void Rename_GivesThePageSetupItsNewName_FollowsTheLayoutSetToIt_AndUndoesAsOneStep()
        {
            CadDocument document = WithTwoSetups();
            Assert.True(CadPlotSettings.SetCurrentPageSetup(document, document.ModelSpace, "A2 plan").Succeeded);

            CadEditResult result = CadPlotSettings.RenamePageSetup(document, "a2 PLAN", "Roof plan");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(new[] { "Roof plan", "Small" }, CadPlotSettings.PageSetupNames(document));
            Assert.Equal("Roof plan", CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));
            Assert.Equal(
                "Roof plan",
                ((DxfPlotSettings)document.Database.PlotSettingsDictionary["Roof plan"]).PlotSettingsName
            );
            CadPlotSettings kept = CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "Roof plan");
            Assert.True(kept.Paper.IsSameAs(CadPaper.Standard[2]));
            Assert.Equal(20.0, kept.DrawingUnits);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "A2 plan", "Small" }, CadPlotSettings.PageSetupNames(document));
            Assert.Equal("A2 plan", CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));
        }

        [Fact]
        public void Rename_AcceptsAChangeOfLettersAlone_AndRefusesANameAutoCadRefusesOneTakenOrOneMissing()
        {
            CadDocument document = WithTwoSetups();

            Assert.True(CadPlotSettings.RenamePageSetup(document, "Small", "SMALL").Succeeded);
            Assert.Equal(new[] { "A2 plan", "SMALL" }, CadPlotSettings.PageSetupNames(document));

            CadEditResult taken = CadPlotSettings.RenamePageSetup(document, "SMALL", " a2 plan ");
            CadEditResult bad = CadPlotSettings.RenamePageSetup(document, "SMALL", "A/B");
            CadEditResult missing = CadPlotSettings.RenamePageSetup(document, "Nothing", "Other");

            Assert.False(taken.Succeeded);
            Assert.Contains("A2 plan", taken.Message, StringComparison.Ordinal);
            Assert.False(bad.Succeeded);
            Assert.False(missing.Succeeded);
            Assert.Contains("Nothing", missing.Message, StringComparison.Ordinal);
            Assert.Equal(new[] { "A2 plan", "SMALL" }, CadPlotSettings.PageSetupNames(document));
        }

        [Fact]
        public void Delete_RemovesThePageSetup_LeavesTheLayoutItWasSetToOnItsSettings_AndUndoesAsOneStep()
        {
            CadDocument document = WithTwoSetups();
            Assert.True(CadPlotSettings.SetCurrentPageSetup(document, document.ModelSpace, "A2 plan").Succeeded);

            CadEditResult result = CadPlotSettings.DeletePageSetup(document, "a2 plan");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(new[] { "Small" }, CadPlotSettings.PageSetupNames(document));
            Assert.Equal(string.Empty, CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));
            Assert.True(CadPlotSettings.Read(document, document.ModelSpace).Paper.IsSameAs(CadPaper.Standard[2]));
            Assert.Equal("PAGESETUP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "A2 plan", "Small" }, CadPlotSettings.PageSetupNames(document));
            Assert.Equal("A2 plan", CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));

            Assert.False(CadPlotSettings.DeletePageSetup(document, "Nothing").Succeeded);
            Assert.Throws<ArgumentNullException>(() => CadPlotSettings.DeletePageSetup(document, null));
            Assert.Throws<ArgumentNullException>(() => CadPlotSettings.RenamePageSetup(document, "Small", null));
        }

        [Fact]
        public void PageSetupsOfAnotherDrawing_AreReadWithNoDocument_WithTheSpaceEachWasMadeFor()
        {
            CadDocument source = WithTwoSetups();
            string text = source.Database.ToText();
            DxfDatabase file = DxfDatabase.Parse(text);

            CadPlotSettings read = CadPlotSettings.ReadPageSetup(file, "a2 PLAN", false);

            Assert.True(read.Paper.IsSameAs(CadPaper.Standard[2]));
            Assert.Equal(20.0, read.DrawingUnits);
            Assert.Null(CadPlotSettings.ReadPageSetup(file, "Nothing", false));
            Assert.False(CadPlotSettings.PageSetupIsModel(file, "A2 plan"));
            Assert.False(CadPlotSettings.PageSetupIsModel(file, "Nothing"));

            // AutoCAD marks the page setups made in model space with flag 1024 of group 70.
            string marked = Regex.Replace(
                text,
                @"(AcDbPlotSettings(?:.|\n)*?\n\s*70\r?\n\s*)(\d+)",
                m => m.Groups[1].Value + (int.Parse(m.Groups[2].Value) | 1024),
                RegexOptions.None,
                TimeSpan.FromSeconds(5)
            );
            DxfDatabase made = DxfDatabase.Parse(marked);
            Assert.Contains(CadPlotSettings.PageSetupNames(made), n => CadPlotSettings.PageSetupIsModel(made, n));
            Assert.Throws<ArgumentNullException>(() => CadPlotSettings.PageSetupIsModel(null, "x"));
        }

        [Fact]
        public void ShowPageSetupForNewLayouts_IsZeroUntilSet_AndKeptByTheSession()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("0", session.Registry.FindVariable("SHOWPAGESETUPFORNEWLAYOUTS").GetValue(session));

            Run(session, "SHOWPAGESETUPFORNEWLAYOUTS", "1");

            Assert.Equal("1", session.Registry.FindVariable("SHOWPAGESETUPFORNEWLAYOUTS").GetValue(session));
            Assert.Equal(1.0, session.Remembered["showpagesetupfornewlayouts"]);
            Assert.False(session.Registry.FindVariable("SHOWPAGESETUPFORNEWLAYOUTS").IsInDrawing);
        }
    }
}
