using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Wave 2 of the presenter's second half of AutoCAD parity: the Plot dialog's View, the Manage ribbon tab with CUI,
    /// ALIASEDIT and APPLOAD, and the application menu's search by description, with AUTOCOMPLETEDELAY as AutoCAD 2021's alias
    /// of INPUTSEARCHDELAY. Driven with no window.
    /// </summary>
    public sealed class ManageSearchAndPlotViewTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private static CadDocument PlanWithViews()
        {
            CadDocument plan = PlotPdfTests.Plan();
            DxfDatabase database = plan.Database;
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
            return CadDocument.From(DxfDatabase.Parse(database.ToText()), "plan-level2.dxf");
        }

        private MainViewModel Opened()
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            Assert.True(model.Open(room));
            return model;
        }

        // ---- Plot area: View

        [Fact]
        public void TheWhatToPlotListOffersViewBetweenLimitsAndWindow_AndChoosingItTakesTheFirstViewByName()
        {
            CadDocument document = PlanWithViews();
            var job = new CadPlotJob(document.ModelSpace, new CadPlotSettings(), DisplayBox.Empty, null);
            var dialog = new PlotDialogViewModel(document, job);

            Assert.Equal(
                new[] { "Display", "Extents", "Limits", "View", "Window" },
                dialog.Areas.Select(a => a.Name).ToArray()
            );
            PlotAreaRow view = dialog.Areas.Single(a => a.Area == CadPlotArea.View);
            Assert.True(view.IsEnabled);
            Assert.Equal(new[] { "entrance", "Stair core" }, dialog.ViewNames.ToArray());
            Assert.False(dialog.IsView);

            dialog.Area = view;

            Assert.True(dialog.IsView);
            Assert.Equal("entrance", dialog.ViewName);
            Assert.Equal(CadPlotArea.View, dialog.Settings.Area);
            Assert.Equal(new DisplayBox(200, 0, 800, 800), dialog.Page.Area);

            dialog.ViewName = "STAIR CORE";

            Assert.Equal("Stair core", dialog.Settings.ViewName);
            Assert.Equal(new DisplayBox(2000, 1500, 4000, 2500), dialog.Page.Area);
        }

        [Fact]
        public void ViewIsGreyedWhereTheDrawingHasNoView_AndTheAreaCannotBeSetToIt()
        {
            CadDocument document = PlotPdfTests.Plan();
            var job = new CadPlotJob(document.ModelSpace, new CadPlotSettings(), DisplayBox.Empty, null);
            var dialog = new PlotDialogViewModel(document, job);

            PlotAreaRow view = dialog.Areas.Single(a => a.Area == CadPlotArea.View);

            Assert.False(view.IsEnabled);
            Assert.Empty(dialog.ViewNames);
            dialog.Area = view;
            Assert.Equal(CadPlotArea.Display, dialog.Settings.Area);
            Assert.False(dialog.IsView);
        }

        [Fact]
        public void APageSetupThatPlotsAViewTheDrawingLacks_OpensOnTheDisplay_AndOneThatHasItKeepsItsView()
        {
            CadDocument document = PlanWithViews();
            var lacking = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "Roof" };
            var keeping = new CadPlotSettings { Area = CadPlotArea.View, ViewName = "ENTRANCE" };

            var first = new PlotDialogViewModel(
                document,
                new CadPlotJob(document.ModelSpace, lacking, DisplayBox.Empty, null)
            );
            var second = new PlotDialogViewModel(
                document,
                new CadPlotJob(document.ModelSpace, keeping, DisplayBox.Empty, null)
            );

            // A view of another name is replaced by the first of the list; the name of the right one is spelt as the drawing spells it.
            Assert.Equal("entrance", first.ViewName);
            Assert.Equal("entrance", second.ViewName);
            Assert.True(second.IsView);

            var bare = PlotPdfTests.Plan();
            var none = new PlotDialogViewModel(
                bare,
                new CadPlotJob(bare.ModelSpace, lacking.Clone(), DisplayBox.Empty, null)
            );
            Assert.Equal(CadPlotArea.Display, none.Settings.Area);
        }

        // ---- Manage tab

        [Fact]
        public void TheManageTab_IsBuiltIn_AndItsCommandsAreInTheRegistryOfADrawing()
        {
            MainViewModel model = Opened();

            Assert.Equal("Manage", model.ManageTab.TabHeader);
            Assert.IsType<ManageRibbonViewModel>(model.ManageTab.TabContent);
            Assert.True(model.ManageTab.IsTabVisible);
            CadCommandRegistry registry = model.Session.Registry;

            foreach (
                string name in new[]
                {
                    "CUI",
                    "ALIASEDIT",
                    "APPLOAD",
                    "SCRIPT",
                    "PURGE",
                    "OVERKILL",
                    "AUDIT",
                    "RECOVER",
                }
            )
            {
                Assert.NotNull(registry.Find(name));
            }
        }

        [Fact]
        public void UserInterfaceAndEditAliases_OpenGlobalSettingsOnShortcuts()
        {
            MainViewModel model = Opened();
            var asked = new System.Collections.Generic.List<string>();
            model.SettingsRequested += (s, category) => asked.Add(category);

            model.StartCommand("CUI");
            model.StartCommand("ALIASEDIT");

            Assert.Equal(new[] { "Shortcuts", "Shortcuts" }, asked);
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void LoadApplication_IsGreyedWithNoPluginManager_AndShowsThePluginManagerWithOne()
        {
            MainViewModel model = Opened();
            var ribbon = (ManageRibbonViewModel)model.ManageTab.TabContent;
            Assert.False(ribbon.CanLoadApplication);
            model.StartCommand("APPLOAD");
            Assert.Contains("No plugin manager is in use: applications cannot be loaded.", model.History);

            using (
                var manager = new CadPluginManager(
                    new CadPluginAssemblyLoader(),
                    Path.Combine(_folder.FullPath, "plugins"),
                    Path.Combine(_folder.FullPath, "plugins.json"),
                    Path.Combine(_folder.FullPath, "copies")
                )
            )
            {
                int raised = 0;
                model.PluginManagerRequested += (s, e) => raised++;
                model.UsePlugins(manager, loadAtStart: false);

                Assert.True(ribbon.CanLoadApplication);
                model.StartCommand("APPLOAD");
                Assert.Equal(1, raised);
            }
        }

        // ---- Command search

        [Fact]
        public void TheAppMenusSearch_FindsACommandByItsDescription_BelowTheCommandsOfThatName()
        {
            MainViewModel model = Opened();
            AppMenuViewModel menu = model.CreateAppMenu();

            menu.SearchText = "damaged";

            CommandSuggestionViewModel recover = Assert.Single(menu.SearchResults, r => r.Name == "RECOVER");
            Assert.Equal("Open a damaged drawing and repair it", recover.Note);
            Assert.False(recover.IsVariable);

            // A name that holds the letters comes first; the commands found by their words follow below a rule.
            menu.SearchText = "plot";
            var rows = menu.SearchResults.ToList();
            int lastByName = rows.FindLastIndex(r => r.Name.Contains("PLOT"));
            List<CommandSuggestionViewModel> byWords = rows.Where(r => !r.Name.Contains("PLOT") && !r.IsVariable)
                .ToList();
            Assert.NotEmpty(byWords);
            Assert.All(byWords, r => Assert.True(rows.IndexOf(r) > lastByName, r.Name));
            Assert.True(byWords[0].StartsMidString);
        }

        [Fact]
        public void TheAppMenusSearch_TakesEveryWordOfTheText_AndRunsTheCommandFoundWithEnter()
        {
            MainViewModel model = Opened();
            AppMenuViewModel menu = model.CreateAppMenu();

            menu.SearchText = "bevel corner";

            CommandSuggestionViewModel chamfer = Assert.Single(menu.SearchResults);
            Assert.Equal("CHAMFER", chamfer.Name);
            Assert.Equal("Bevel the corner of two lines", chamfer.Note);

            int closed = 0;
            menu.Chosen += (s, e) => closed++;
            menu.RunSearchResult(menu.SearchResults[0]);

            Assert.Equal(1, closed);
            Assert.Contains("Command: CHAMFER", model.History);
        }

        [Fact]
        public void TheAppMenusSearch_ListsNothingForWordsNoNameOrDescriptionHolds()
        {
            MainViewModel model = Opened();
            AppMenuViewModel menu = model.CreateAppMenu();

            menu.SearchText = "zebra giraffe";

            Assert.True(menu.IsSearching);
            Assert.Empty(menu.SearchResults);
        }

        [Fact]
        public void EveryCommandOfTheRegistryThatHasADescription_IsFoundByAWordOfIt_AtMostOnce()
        {
            MainViewModel model = Opened();
            AppMenuViewModel menu = model.CreateAppMenu();

            menu.SearchText = "layout";

            Assert.Equal(menu.SearchResults.Select(r => r.Name).Distinct().Count(), menu.SearchResults.Count);
            Assert.True(menu.SearchResults.Count <= 100);
        }

        // ---- AutoComplete variables

        [Fact]
        public void AutocompletedelayIsAnAliasOfInputsearchdelay_AsAutoCad2021Has_AndAsksItsOwnValue()
        {
            MainViewModel model = Opened();

            Assert.Equal("INPUTSEARCHDELAY", model.Session.Registry.FindVariable("AUTOCOMPLETEDELAY").Name);
            model.StartCommand("AUTOCOMPLETEDELAY");

            Assert.Equal("Enter new value for INPUTSEARCHDELAY <300>:", model.Prompt);
            model.Execute("50");
            Assert.Contains("Requires an integer between 100 and 10000.", model.History);
        }

        [Fact]
        public void AutocompletemodeStillRunsDashInputsearchoptions()
        {
            MainViewModel model = Opened();

            model.StartCommand("AUTOCOMPLETEMODE");

            Assert.StartsWith("Enter an input search Option", model.Prompt);
        }
    }
}
