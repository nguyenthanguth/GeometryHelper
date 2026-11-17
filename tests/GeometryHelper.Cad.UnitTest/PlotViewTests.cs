using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What to plot: View. A named view of the drawing plotted as its window, listed by the space's kind, asked by -PLOT in
    /// AutoCAD 2021's words (probed in its console: the list of areas always names View, a name not found is asked again),
    /// and kept in the page setup as plot type 3 with the view's name (group 6).
    /// </summary>
    public class PlotViewTests
    {
        /// <summary>The plan of PlotTests with the model views Stair core and entrance, and the paper view Sheet.</summary>
        private static CadDocument PlanWithViews()
        {
            CadDocument document = PlotTests.Plan();
            DxfDatabase database = document.Database;
            database.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "Stair core",
                    CenterPoint = new GeoPoint2(3000, 2000),
                    Height = 1000,
                    Width = 2000,
                }
            );
            database.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "entrance",
                    CenterPoint = new GeoPoint2(500, 400),
                    Height = 800,
                    Width = 600,
                }
            );
            database.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "Sheet",
                    CenterPoint = new GeoPoint2(200, 150),
                    Height = 100,
                    Width = 140,
                    IsPaperspaceView = true,
                }
            );
            return CadDocument.From(DxfDatabase.Parse(database.ToText()), "plan-level2.dxf");
        }

        [Fact]
        public void TheViewsOfModelSpace_AreListedByNameLettersOfEitherCaseAlike_AndThoseOfPaperSpaceApart()
        {
            CadDocument document = PlanWithViews();

            Assert.Equal(new[] { "entrance", "Stair core" }, CadPlotSettings.ViewNames(document, document.ModelSpace));
            Assert.Empty(CadPlotSettings.ViewNames(PlotTests.Plan(), document.ModelSpace));
        }

        [Fact]
        public void AViewPlottedOnA3Fitted_IsItsWindow_2000By1000_AtAScaleOfPoint205()
        {
            CadDocument document = PlanWithViews();
            var settings = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "STAIR CORE" };

            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);

            // The window (2 000, 1 500) to (4 000, 2 500); the printable area of A3 on its side is 410 by 287: 0.205 by width.
            Assert.Equal(new DisplayBox(2000, 1500, 4000, 2500), page.Area);
            Assert.Equal(0.205, page.Scale, 12);
            Assert.Equal(5.0, page.DrawingToSheet.X(2000, 1500) - page.OffsetX, 9);
        }

        [Fact]
        public void AViewTheDrawingLacks_IsPlottedAsTheExtents()
        {
            CadDocument document = PlanWithViews();
            var settings = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "gone" };

            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);

            Assert.Equal(document.GetDisplay(document.ModelSpace).Bounds, page.Area);
        }

        [Fact]
        public void AViewAppliedToTheModelPageSetup_IsPlotType3WithItsName_AndReadsBackAfterSaving()
        {
            CadDocument document = PlanWithViews();
            var settings = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "entrance" };

            Assert.True(settings.ApplyTo(document, document.ModelSpace).Succeeded);

            DxfLayout model = document.Database.Layouts.Single(l => l.LayoutName == "Model");
            Assert.Equal(DxfPlotType.View, model.PlotType);
            Assert.Equal("entrance", model.PlotViewName);

            CadDocument opened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "plan-level2.dxf");
            CadPlotSettings read = CadPlotSettings.Read(opened, opened.ModelSpace);
            Assert.Equal(CadPlotArea.View, read.Area);
            Assert.Equal("entrance", read.ViewName);

            Assert.True(document.Undo());
            Assert.NotEqual(CadPlotArea.View, CadPlotSettings.Read(document, document.ModelSpace).Area);
            Assert.Equal(string.Empty, CadPlotSettings.Read(document, document.ModelSpace).ViewName);
        }

        [Fact]
        public void ANamedPageSetupThatPlotsAView_KeepsItsName()
        {
            CadDocument document = PlanWithViews();
            var settings = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "Stair core" };

            Assert.True(settings.SaveAsPageSetup(document, "Stair").Succeeded);

            CadPlotSettings named = CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "Stair");
            Assert.Equal(CadPlotArea.View, named.Area);
            Assert.Equal("Stair core", named.ViewName);
        }

        [Fact]
        public void DashPlot_NamesViewAmongTheAreas_AsksItsNameAgainUntilTheDrawingHasIt_AndHandsItToTheWindow()
        {
            CadDocument document = PlanWithViews();
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

            Run(session, "-PLOT", "Y", Enter, Enter, "ISO A3", Enter, "L", Enter);
            Assert.StartsWith("Enter plot area [Display/Extents/Limits/View/Window] <", session.PromptText);
            Run(session, "V");
            Assert.Equal("Enter view name:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("No view specified.  You must enter a view name.", session.History.Last());
            Run(session, "x");
            Assert.Equal("<x> not found.", session.History.Last());
            Run(session, "Sheet");
            Assert.Equal("<Sheet> not found.", session.History.Last());
            Run(session, "STAIR CORE");
            Assert.StartsWith("Enter plot scale (Plotted Millimeters=Drawing Units) or [Fit] <", session.PromptText);
            Run(session, "F", "C", "Y", "monochrome", "Y", Enter, file, "N", Enter);

            Assert.False(session.IsRunning);
            Assert.NotNull(job);
            Assert.Equal(CadPlotArea.View, job.Settings.Area);
            Assert.Equal("Stair core", job.Settings.ViewName);
        }

        [Fact]
        public void DashPlot_OfADrawingWithNoViews_StillNamesView_AndAsksAgainForAnyName()
        {
            CadDocument document = PlotTests.Plan();
            CadCommandSession session = Session(document);

            Run(session, "-PLOT", "Y", Enter, Enter, "ISO A3", Enter, "L", Enter);
            Assert.StartsWith("Enter plot area [Display/Extents/Limits/View/Window] <", session.PromptText);
            Run(session, "V", "Any");
            Assert.Equal("<Any> not found.", session.History.Last());
        }

        [Fact]
        public void APageSetupWithAViewTypeButNoViewInTheDrawing_PlotsTheExtentsAndIsNotAnError()
        {
            CadDocument document = PlotTests.Plan();
            var settings = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "Stair core" };

            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);

            Assert.False(page.Area.IsEmpty);
            Assert.True(page.Scale > 0.0);
        }
    }
}
