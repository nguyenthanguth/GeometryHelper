using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The LayerWalk dialog, the Layer Translator with its Settings and Edit Layer dialogs, and the DesignCenter palette, made
    /// on a thread of their own and laid out, to see the design's parts: the orange-red close cross, the groups, the lists and
    /// the buttons AutoCAD 2021 has.
    /// </summary>
    public class LayerToolsWindowTests
    {
        private static MainViewModel Plan(TestFolder folder)
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            foreach (string name in new[] { "WALL", "DOOR", "TRIM" })
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = name, ColorIndex = 2 });
            }

            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALL" }
            );
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan.dxf", database)));
            return model;
        }

        private static List<string> TextsOf(FrameworkElement body) =>
            Descendants(body).OfType<TextBlock>().Select(t => t.Text).ToList();

        private static void Check(Window window, FrameworkElement body, params string[] texts)
        {
            Style title = (Style)window.FindResource("GhTitleCloseButton");
            object context = window.DataContext;
            window.Content = null;
            body.DataContext = context;
            Layout(body);
            // Nothing is cut: what the parts ask for fits the window the design gives.
            body.Measure(new Size(window.Width, double.PositiveInfinity));
            Assert.True(
                body.DesiredSize.Height <= window.Height + 0.5,
                window.Title + " needs " + body.DesiredSize.Height + " of " + window.Height
            );
            Button cross = Assert.Single(Descendants(body).OfType<Button>(), b => b.Style == title);
            Assert.Equal(
                Color.FromRgb(0xF2, 0x6B, 0x3A),
                ((SolidColorBrush)Descendants(cross).OfType<IconView>().Single().Stroke).Color
            );
            List<string> found = TextsOf(body);

            foreach (string text in texts)
            {
                Assert.Contains(text, found);
            }
        }

        [Fact]
        public void LayerWalk_IsDrawnAsTheDesign_TheFilterBox_TheLayerList_TheTwoButtonsAndRestoreOnExit()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var request = new CadLayerWalkRequest(model.Document);
                    var window = new LayerWalkWindow(new LayerWalkViewModel(request));
                    FrameworkElement body = window.Body;
                    Check(window, body, "LayerWalk", "Filter:", "Layers");

                    List<string> buttons = Descendants(body)
                        .OfType<Button>()
                        .Select(b => b.Content as string)
                        .Where(c => c != null)
                        .ToList();
                    Assert.Contains("Select Objects", buttons);
                    Assert.Contains("Purge Layers", buttons);
                    Assert.Contains("Close", buttons);
                    CheckBox restore = Descendants(body).OfType<CheckBox>().Single();
                    Assert.Equal("Restore on exit", restore.Content);
                    Assert.True(restore.IsChecked);
                    ListBox layers = Descendants(body).OfType<ListBox>().Single();
                    Assert.Equal(4, layers.Items.Count);
                    Assert.Equal(SelectionMode.Extended, layers.SelectionMode);
                    Assert.Equal(
                        new[] { "Select All", "Clear All", "Purge Layers" },
                        layers.ContextMenu.Items.OfType<MenuItem>().Select(i => (string)i.Header).ToArray()
                    );
                    window.Close();
                }
            });
        }

        [Fact]
        public void LayerWalk_SelectingRowsWalksThem_AndTheFilterKeepsTheSelectionOfTheRowsThatStay()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var window = new LayerWalkWindow(new LayerWalkViewModel(new CadLayerWalkRequest(model.Document)));
                    FrameworkElement body = window.Body;
                    body.DataContext = window.Model;
                    Layout(body);
                    ListBox layers = Descendants(body).OfType<ListBox>().Single();

                    layers.SelectedItems.Add(window.Model.Rows.First(r => r.Name == "DOOR"));
                    layers.SelectedItems.Add(window.Model.Rows.First(r => r.Name == "WALL"));

                    Assert.Equal(new[] { "DOOR", "WALL" }, window.Model.Selected);
                    Assert.Equal(
                        new[] { "0", "TRIM" },
                        model.Document.Database.LayerTable.Where(l => l.IsOff).Select(l => l.Name).OrderBy(n => n)
                    );

                    window.Model.Filter = "D*";
                    Assert.Equal(new[] { "DOOR" }, window.Model.Rows.Select(r => r.Name));
                    Assert.Equal(new[] { "DOOR", "WALL" }, window.Model.Selected);
                    Assert.Equal(
                        new[] { "DOOR" },
                        layers.SelectedItems.Cast<LayerWalkRowViewModel>().Select(r => r.Name)
                    );
                    window.Close();
                }
            });
        }

        [Fact]
        public void TheLayerTranslator_IsDrawnAsTheDesign_TwoListsTheMappingsAndTheButtonsOfAutoCad()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var window = new LayerTranslatorWindow(
                        new LayerTranslatorViewModel(new CadLayerTranslatorRequest(model.Document))
                    );
                    FrameworkElement body = window.Body;
                    Check(
                        window,
                        body,
                        "Layer Translator",
                        "Translate From",
                        "Translate To",
                        "Selection Filter:",
                        "Layer Translation Mappings",
                        "Old",
                        "New",
                        "Linetype",
                        "Lineweight",
                        "Transparency",
                        "Plot Style"
                    );

                    List<string> buttons = Descendants(body)
                        .OfType<Button>()
                        .Select(b => b.Content as string)
                        .Where(c => c != null)
                        .ToList();
                    foreach (
                        string name in new[]
                        {
                            "Map",
                            "Map same",
                            "Load…",
                            "New…",
                            "Edit…",
                            "Remove",
                            "Save…",
                            "Settings…",
                            "Translate",
                            "Cancel",
                            "Select",
                        }
                    )
                    {
                        Assert.Contains(name, buttons);
                    }

                    Assert.False(
                        Descendants(body).OfType<Button>().Single(b => (b.Content as string) == "Translate").IsEnabled
                    );
                    Assert.Equal(4, Descendants(body).OfType<ListBox>().Single(l => l.Name == "FromList").Items.Count);
                    Assert.Equal(4, Descendants(body).OfType<ListBox>().Single(l => l.Name == "ToList").Items.Count);

                    window.Model.MapSame();
                    Layout(body);
                    Assert.Equal(
                        4,
                        Descendants(body).OfType<ListBox>().Single(l => l.Name == "MappingList").Items.Count
                    );
                    Assert.True(
                        Descendants(body).OfType<Button>().Single(b => (b.Content as string) == "Translate").IsEnabled
                    );
                    window.Close();
                }
            });
        }

        [Fact]
        public void TheSettingsAndEditLayerDialogs_AreDrawnWithTheirSixSwitchesAndTheirSixFields()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var settings = new TranslateSettingsWindow(
                        new TranslateSettingsViewModel(new CadLayerTranslatorSettings())
                    );
                    Check(settings, settings.Body, "Layer Translator Settings");
                    Assert.Equal(
                        new[]
                        {
                            "Force object color to ByLayer",
                            "Force object linetype to ByLayer",
                            "Force object transparency to ByLayer",
                            "Translate objects in blocks",
                            "Write transaction log",
                            "Show layer contents when selected",
                        },
                        Descendants(settings.Body).OfType<CheckBox>().Select(c => (string)c.Content).ToArray()
                    );
                    Assert.All(Descendants(settings.Body).OfType<CheckBox>(), c => Assert.True(c.IsChecked));
                    settings.Close();

                    var made = new TranslateLayerWindow(
                        new TranslateLayerViewModel(model.Document, new CadTranslateLayer("A-ANNO"), true, null)
                    );
                    Check(made, made.Body, "New Layer", "Name", "Color", "Linetype", "Lineweight");
                    Assert.False(Descendants(made.Body).OfType<TextBox>().First().IsReadOnly);
                    Assert.Equal(25, made.Model.LineWeights.Count);
                    made.Close();

                    var edit = new TranslateLayerWindow(
                        new TranslateLayerViewModel(model.Document, new CadTranslateLayer("A-ANNO"), false, null)
                    );
                    Check(edit, edit.Body, "Edit Layer", "Transparency (0 to 90)", "Plot style");
                    Assert.True(Descendants(edit.Body).OfType<TextBox>().First().IsReadOnly);
                    edit.Close();
                }
            });
        }

        [Fact]
        public void DesignCenter_IsDrawnAsTheDesign_ThreeTabsTheToolRowTheTreeTheListAndThePreview()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });
                    var window = new DesignCenterWindow(palette);
                    FrameworkElement body = window.Body;
                    Check(window, body, "DESIGNCENTER");

                    Assert.Equal(
                        new[] { "Folders", "Open Drawings", "History" },
                        Descendants(body).OfType<RadioButton>().Select(r => (string)r.Content).ToArray()
                    );
                    List<string> tools = Descendants(body)
                        .OfType<ButtonBase>()
                        .Select(b => b.Content as string)
                        .Where(c => c != null)
                        .ToList();
                    foreach (
                        string name in new[]
                        {
                            "Load…",
                            "◀ Back",
                            "Forward ▶",
                            "▲ Up",
                            "Search",
                            "Favorites",
                            "Tree",
                            "Preview",
                            "Description",
                        }
                    )
                    {
                        Assert.Contains(name, tools);
                    }

                    Assert.False(
                        Descendants(body).OfType<Button>().Single(b => (b.Content as string) == "Search").IsEnabled
                    );
                    Assert.False(
                        Descendants(body).OfType<Button>().Single(b => (b.Content as string) == "Favorites").IsEnabled
                    );
                    TreeView tree = Descendants(body).OfType<TreeView>().Single();
                    Assert.Single(tree.Items);

                    palette.Tab = "Open Drawings";
                    Layout(body);
                    Assert.Single(tree.Items);
                    ((DesignCenterNode)tree.Items[0]).IsSelected = true;
                    Layout(body);
                    ListBox list = Descendants(body).OfType<ListBox>().Single();
                    Assert.Equal(9, list.Items.Count);
                    Assert.Equal(SelectionMode.Extended, list.SelectionMode);
                    window.Close();
                }
            });
        }

        private static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(1000, 800));
            element.Arrange(new Rect(0, 0, 1000, 800));
            element.UpdateLayout();
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
