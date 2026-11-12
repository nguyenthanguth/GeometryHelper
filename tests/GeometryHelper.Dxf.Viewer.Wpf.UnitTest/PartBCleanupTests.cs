using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The presenter alone (GeometryHelper DXF) lists nothing of the Tekla drawing (Part B): not in its ribbon, its menus, its
    /// commands, its Global settings, its start screen or its style and layer dialogs. What Part B needs comes in through
    /// <see cref="CadWorkspaceExtension"/>, and a plain <see cref="MainViewModel"/> has none.
    /// </summary>
    public sealed class PartBCleanupTests
    {
        private static MainViewModel Opened()
        {
            var model = new MainViewModel();
            model.NewDrawing();
            return model;
        }

        [Fact]
        public void TheRibbonTabsOfThePresenter_AreItsOwn_AndNoExtensionOffersAFileKindOrANewDocument()
        {
            MainViewModel model = Opened();
            var tabs = new[] { model.InsertTab, model.AnnotateTab, model.ViewTab, model.SettingsTab, model.PluginsTab };

            Assert.Equal(new[] { "Insert", "Annotate", "View", "Settings", "Plugins" }, tabs.Select(t => t.TabHeader));
            Assert.All(model.Extensions, e => Assert.DoesNotContain("Drawing", e.TabHeader ?? string.Empty));
            Assert.All(model.Extensions, e => Assert.Null(e.FileKindName));
            Assert.All(model.Extensions, e => Assert.Null(e.NewDocumentOffer));
            Assert.False(model.HasOwnFiles);
            Assert.Null(model.NewDocumentOffer);
        }

        [Fact]
        public void TheCommandsAliasesAndKeysOfThePresenter_NameNoPartBCommand()
        {
            MainViewModel model = Opened();
            string[] partB =
            {
                "DRAWINGNEW",
                "DRAWINGUPDATE",
                "DRAWINGSETTINGS",
                "EXPORTDXF",
                "AUTODIM",
                "AUTOMARK",
                "VIEWSECTION",
                "VIEWDETAIL",
                "PLUGIN",
            };
            CadShortcuts shortcuts = model.CurrentShortcuts();

            foreach (string name in partB)
            {
                Assert.Null(model.Session.Registry.Find(name));
                Assert.DoesNotContain(name, shortcuts.Aliases.Values);
                Assert.DoesNotContain(name, shortcuts.Keys.Values);
            }
        }

        [Fact]
        public void TheApplicationMenuAndTheStartScreenOfThePresenter_SayNothingOfModels()
        {
            var model = new MainViewModel();

            var names = new List<string>();
            void Collect(IEnumerable<AppMenuItemViewModel> items)
            {
                foreach (AppMenuItemViewModel item in items)
                {
                    names.Add(item.Name + " | " + item.Description);
                    Collect(item.Items);
                }
            }

            Collect(model.CreateAppMenu().Items);

            Assert.NotEmpty(names);
            Assert.All(names, n => Assert.DoesNotContain("from a model", n, StringComparison.OrdinalIgnoreCase));
            Assert.All(names, n => Assert.DoesNotContain("DRAWINGNEW", n, StringComparison.Ordinal));
            Assert.Equal("Open and edit any 2D drawing. No file is open.", model.StartScreenText);
            Assert.Equal(new[] { MainViewModel.NewDrawingWelcomeText }, model.RecentHistory);
        }

        [Fact]
        public void GlobalSettingsOfThePresenter_ShowNoRowOrSectionOfPartB()
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings();
            string[] sections = { "DXF export", "Drawing defaults", "Data sources", "Drawing text" };
            string[] rows =
            {
                "Hidden lines",
                "Dimension decimals",
                "Layout",
                "Open the folder after export",
                "Preset",
                "Sheet",
                "Sheet size",
                "Orientation",
                "Projection",
                "Check for model changes",
                "Check every",
            };
            var titles = new List<string>();
            var labels = new List<string>();
            var intros = new List<string>();

            foreach (GlobalSettingCategory category in dialog.Categories)
            {
                dialog.SelectedCategory = category;
                intros.Add(category.Intro);
                titles.AddRange(dialog.Sections.Select(s => s.Title));
                labels.AddRange(dialog.Sections.SelectMany(s => s.Rows).Select(r => r.Label));
            }

            Assert.NotEmpty(labels);
            Assert.Empty(titles.Intersect(sections));
            Assert.Empty(labels.Intersect(rows));
            Assert.All(intros, i => Assert.DoesNotContain("model", i, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TheModelDataOfGlobalSettings_IsKeptForGeometryHelperDrawing_AndNothingOfThePresenterAppliesIt()
        {
            var settings = new GlobalSettings
            {
                DefaultPreset = "Part",
                DefaultSheetSize = "A3",
                DefaultOrientation = "Portrait",
                DefaultProjection = "First angle",
                HiddenLines = 0x00FF00,
                ExportLayout = "SheetInModelSpace",
            };
            string path;
            GlobalSettings read;

            using (var folder = new TestFolder())
            {
                path = System.IO.Path.Combine(folder.FullPath, "settings.json");
                settings.Save(path);
                read = GlobalSettings.Load(path);
            }

            Assert.Equal("Part", read.DefaultPreset);
            Assert.Equal("A3", read.DefaultSheetSize);
            Assert.Equal("Portrait", read.DefaultOrientation);
            Assert.Equal("First angle", read.DefaultProjection);
            Assert.Equal(0x00FF00, read.HiddenLines);
            Assert.Equal("SheetInModelSpace", read.ExportLayout);
            Assert.Null(typeof(CanvasOptions).GetProperty("HiddenLineColor"));
            Assert.Null(typeof(CanvasOptions).GetProperty("HiddenLayerName"));
        }
    }
}
