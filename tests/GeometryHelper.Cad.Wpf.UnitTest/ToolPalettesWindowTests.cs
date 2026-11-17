using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
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
    /// The tool palettes drawn: the window with its tabs along the edge and its tools as icons, icons with names or rows, the
    /// menus it builds, the Tool Properties, View Options and name dialogs, the Tool Palettes button of the Manage tab, and the
    /// canvas giving objects up to a drag onto a palette.
    /// </summary>
    public class ToolPalettesWindowTests
    {
        /// <summary>Registers the pack scheme and loads one window's XAML, so that the icon set can be read by its pack address whichever test runs first.</summary>
        private static void Prepare()
        {
            _ = PackUriHelper.UriSchemePack;
            new ToolPaletteNameWindow(new ToolPaletteNameViewModel("t", "l", "n")).Close();
        }

        private static MainViewModel Plan(TestFolder folder)
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };
            Assert.True(model.Open(folder.Write("plan.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        private static List<string> TextsOf(FrameworkElement body) =>
            Descendants(body).OfType<TextBlock>().Select(t => t.Text).ToList();

        /// <summary>Checks a window's content has the close cross the design gives and fits the window.</summary>
        private static void Check(Window window, FrameworkElement body, params string[] texts)
        {
            Style title = (Style)window.FindResource("GhTitleCloseButton");
            object context = window.DataContext;
            window.Content = null;
            body.DataContext = context;
            Layout(body, window.Width, double.IsNaN(window.Height) ? 900 : window.Height);
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
        public void TheToolPalettes_AreDrawnAsTheDesign_TheTabsAlongTheEdgeAndTheToolsAsIcons()
        {
            OnSta(() =>
            {
                Prepare();

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var palettes = new ToolPalettesViewModel(model);
                    var window = new ToolPalettesWindow(palettes);
                    FrameworkElement body = window.Body;
                    Check(window, body, "TOOL PALETTES", "Draw");

                    ListBox tabs = Descendants(body).OfType<ListBox>().Single(l => l.Name == "TabList");
                    ListBox tools = Descendants(body).OfType<ListBox>().Single(l => l.Name == "ItemList");
                    Assert.Equal(6, tabs.Items.Count);
                    Assert.Equal(8, tools.Items.Count);
                    Assert.Equal(SelectionMode.Extended, tools.SelectionMode);
                    Assert.Equal(8, Descendants(tools).OfType<IconView>().Count());
                    Assert.DoesNotContain("Circle", ShownTexts(tools));
                    Assert.Equal(
                        new[] { "Draw", "Modify", "Annotation", "Hatches", "Architectural Hatches", "ISO Hatches" },
                        Descendants(tabs).OfType<TextBlock>().Select(t => t.Text).Where(t => t.Length > 0).ToArray()
                    );
                    Assert.Equal(1, Descendants(tabs).OfType<ListBoxItem>().Count(i => i.IsSelected));
                    Assert.True(
                        Assert.Single(Descendants(body).OfType<Button>(), b => b.Name == "MenuButton").IsEnabled
                    );
                    window.Close();
                }
            });
        }

        [Fact]
        public void TheViewOptions_DrawTheToolsAsIconsWithNamesOrAsRows_AndTheHatchesWithTheirPictures()
        {
            OnSta(() =>
            {
                Prepare();

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var palettes = new ToolPalettesViewModel(model);
                    var window = new ToolPalettesWindow(palettes);
                    FrameworkElement body = window.Body;
                    Check(window, body);
                    ListBox tools = Descendants(body).OfType<ListBox>().Single(l => l.Name == "ItemList");

                    palettes.SetView(CadToolViewStyle.IconWithText, 32, false);
                    Layout(body, 340, 640);
                    Assert.Contains("Circle", ShownTexts(tools));

                    palettes.SetView(CadToolViewStyle.List, 24, false);
                    Layout(body, 340, 640);
                    Assert.Contains("Rectangle", ShownTexts(tools));
                    Assert.Equal(8, Descendants(tools).OfType<IconView>().Count());

                    palettes.Choose(palettes.Tabs[3]);
                    palettes.SetView(CadToolViewStyle.IconWithText, 48, true);
                    Layout(body, 340, 640);
                    List<string> names = ShownTexts(tools);
                    Assert.Contains("ANSI", names);
                    Assert.Contains("ANSI31", names);
                    Assert.Contains("Fills", names);
                    Assert.Contains(Descendants(tools).OfType<Image>(), i => i.Source != null);
                    window.Close();
                }
            });
        }

        [Fact]
        public void AnEmptyPaletteSaysHowToFillIt_AndSoDoesNoPalette()
        {
            OnSta(() =>
            {
                Prepare();

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var palettes = new ToolPalettesViewModel(model);
                    var window = new ToolPalettesWindow(palettes);
                    FrameworkElement body = window.Body;
                    Check(window, body);

                    palettes.NewPalette("Mine");
                    Layout(body, 340, 640);
                    Assert.Contains(palettes.EmptyText, TextsOf(body));

                    while (palettes.Tabs.Count > 0)
                    {
                        palettes.DeletePalette(palettes.Tabs[0]);
                    }

                    Layout(body, 340, 640);
                    Assert.Contains("There is no palette: right-click and choose New Palette.", TextsOf(body));
                    Assert.Contains("No palette", TextsOf(body));
                    window.Close();
                }
            });
        }

        [Fact]
        public void TheMenus_OfAToolAndOfThePalette_OfferWhatTheGuideSays()
        {
            OnSta(() =>
            {
                Prepare();

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var palettes = new ToolPalettesViewModel(model);
                    var window = new ToolPalettesWindow(palettes);
                    Check(window, window.Body);

                    ContextMenu menu = (ContextMenu)
                        typeof(ToolPalettesWindow)
                            .GetMethod("PaletteMenu", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(window, new object[] { null });

                    Assert.Equal(
                        new[]
                        {
                            "New Palette…",
                            "Rename Palette…",
                            "Delete Palette",
                            "Add Text…",
                            "Add Separator",
                            "Paste",
                            "View Options…",
                            "Import Palette…",
                            "Export Palette…",
                            "Export All Palettes…",
                        },
                        menu.Items.OfType<MenuItem>().Select(i => (string)i.Header)
                    );
                    Assert.Equal(3, menu.Items.OfType<Separator>().Count());
                    Assert.False(menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Paste").IsEnabled);

                    palettes.Copy(new[] { palettes.Items[0] });
                    menu = (ContextMenu)
                        typeof(ToolPalettesWindow)
                            .GetMethod("PaletteMenu", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(window, new object[] { null });
                    Assert.True(menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Paste").IsEnabled);
                    window.Close();
                }
            });
        }

        [Fact]
        public void ToolProperties_ShowTheGroupsOfTheKindOfTool_ABlockAHatchACommandAndAText()
        {
            OnSta(() =>
            {
                Prepare();

                CadTool[] tools =
                {
                    CadTool.ForBlock("Tree", null, "TREE"),
                    CadTool.ForHatch("Steel", "ANSI32"),
                    CadTool.ForCommand("Line", "LINE", "Line"),
                    CadTool.ForText("Notes"),
                };
                string[][] groups =
                {
                    new[] { "Insert", "Overrides" },
                    new[] { "Hatch", "Overrides" },
                    new[] { "Command", "Overrides" },
                    new string[0],
                };

                for (int i = 0; i < tools.Length; i++)
                {
                    var model = new ToolPropertiesViewModel(tools[i], new[] { "0", "WALL" }, new[] { "Continuous" });
                    var window = new ToolPropertiesWindow(model);
                    Check(window, window.Body, model.Title);
                    var shown = Descendants(window.Body)
                        .OfType<HeaderedContentControl>()
                        .Where(g => g.Visibility == Visibility.Visible)
                        .Select(g => (string)g.Header)
                        .ToArray();
                    Assert.Equal(groups[i], shown);
                    Assert.Equal(
                        tools[i].Kind != CadToolKind.Text,
                        Descendants(window.Body).OfType<TextBlock>().Any(t => t.Text == "Description" && Shown(t))
                    );
                    window.Close();
                }
            });
        }

        [Fact]
        public void ToolProperties_OfAHatchTool_ShowPatternAngleAndScaleForAPattern_AndOnlyAGradientsNameForAGradient()
        {
            OnSta(() =>
            {
                Prepare();

                var model = new ToolPropertiesViewModel(CadTool.ForHatch("Steel", "ANSI32"), null, null);
                var window = new ToolPropertiesWindow(model);
                Check(window, window.Body, "Tool Properties", "Pattern", "Angle", "Scale", "Associative");
                Assert.DoesNotContain("Spacing", ShownTexts(window.Body));
                Assert.DoesNotContain("Gradient", ShownTexts(window.Body));

                model.HatchType = "Gradient";
                Layout(window.Body, 500, 900);
                Assert.Contains("Gradient", ShownTexts(window.Body));
                Assert.DoesNotContain("Pattern", ShownTexts(window.Body));
                window.Close();
            });
        }

        [Fact]
        public void TheNameAndViewOptionsDialogs_AreDrawn_WithTheirFieldsAndButtons()
        {
            OnSta(() =>
            {
                Prepare();

                var name = new ToolPaletteNameWindow(
                    new ToolPaletteNameViewModel("New Palette", "Palette name:", "New Palette")
                );
                Check(name, name.Body, "New Palette", "Palette name:");
                Assert.Equal(
                    new[] { "OK", "Cancel" },
                    Descendants(name.Body)
                        .OfType<Button>()
                        .Select(b => b.Content as string)
                        .Where(c => c != null)
                        .ToArray()
                );
                Assert.Equal("New Palette", Descendants(name.Body).OfType<TextBox>().Single().Text);
                name.Close();

                var options = new ToolPaletteViewOptionsWindow(
                    new ToolPaletteViewOptionsViewModel(CadToolViewStyle.IconWithText, 40)
                );
                Check(options, options.Body, "View Options");
                Assert.Equal(
                    new[] { "Icon only", "Icon with text", "List view", "Current palette", "All palettes" },
                    Descendants(options.Body).OfType<RadioButton>().Select(r => (string)r.Content).ToArray()
                );
                Assert.Equal(40.0, Descendants(options.Body).OfType<Slider>().Single().Value);
                options.Close();
            });
        }

        [Fact]
        public void TheIconConverter_FindsAnIconOfTheSet_AndGivesThePlainCommandIconForAnyOther()
        {
            OnSta(() =>
            {
                Prepare();
                var converter = new ToolIconConverter();

                object line = converter.Convert("GhIconLine", typeof(System.Windows.Media.Geometry), null, null);
                object other = converter.Convert(
                    "GhIconNoSuchThing",
                    typeof(System.Windows.Media.Geometry),
                    null,
                    null
                );
                object plain = converter.Convert(
                    "GhIconPluginCommand",
                    typeof(System.Windows.Media.Geometry),
                    null,
                    null
                );

                Assert.IsAssignableFrom<System.Windows.Media.Geometry>(line);
                Assert.NotNull(other);
                Assert.Equal(plain.ToString(), other.ToString());
                Assert.NotEqual(line.ToString(), other.ToString());
                Assert.Throws<NotSupportedException>(() => converter.ConvertBack(null, null, null, null));
                var width = new ToolRowWidthConverter();
                Assert.Equal(270.0, width.Convert(300.0, typeof(double), null, null));
                Assert.Equal(40.0, width.Convert(20.0, typeof(double), null, null));
                Assert.Equal(40.0, width.Convert(null, typeof(double), null, null));
                Assert.Throws<NotSupportedException>(() => width.ConvertBack(null, null, null, null));
            });
        }

        [Fact]
        public void EveryCommandToolOfTheStarterSet_HasAnIconInTheSetOfTheWindow()
        {
            OnSta(() =>
            {
                Prepare();
                var icons = new IconKeyConverter();

                foreach (
                    CadTool tool in CadToolPalettes
                        .CreateStarter()
                        .Palettes.SelectMany(p => p.Tools)
                        .Where(t => t.Kind == CadToolKind.Command)
                )
                {
                    Assert.NotNull(
                        icons.Convert("GhIcon" + tool.Icon, typeof(System.Windows.Media.Geometry), null, null)
                    );
                }

                Assert.NotNull(icons.Convert("GhIconInsertBlock", typeof(System.Windows.Media.Geometry), null, null));
                Assert.NotNull(icons.Convert("GhIconHatch", typeof(System.Windows.Media.Geometry), null, null));
            });
        }

        [Fact]
        public void TheManageTabsToolPalettesButton_IsOnWithADrawing_AndRunsToolpalettes()
        {
            OnSta(() =>
            {
                Prepare();

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder);
                    var workspace = new CadWorkspace { DataContext = model };
                    model.SelectedTab = model.ManageTab;
                    Layout(workspace, 1600, 960);

                    Assert.NotNull(model.ShowToolPalettes);
                    Assert.NotNull(model.CloseToolPalettes);
                    Button button = Descendants(workspace)
                        .OfType<Button>()
                        .Single(b => b.Content as string == "Tool Palettes");
                    Assert.True(button.IsEnabled);
                    Assert.Equal("TOOLPALETTES", button.CommandParameter);
                    Assert.Same(model.StartCommandCommand, button.Command);
                }
            });
        }

        [Fact]
        public void AnObjectSelectedAndDraggedOutOfTheCanvas_IsGivenUpToADrag_OnlyWhileSomethingTakesObjects()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };
                    model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out _)));
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
                    canvas.RenderNow();
                    var dragged = new List<IReadOnlyList<string>>();
                    canvas.ObjectsDragged += (s, e) => dragged.Add(e.Handles);
                    Point on = CanvasMouseTests.At(canvas, 2000, 0);
                    Point away = CanvasMouseTests.At(canvas, 2000, 800);

                    CanvasMouseTests.Click(canvas, on);
                    Assert.Contains(bottom, model.Document.Selection.Handles);

                    // Nothing takes objects: the drag does what it always did, and gives nothing up.
                    CanvasMouseTests.Drag(canvas, on, away);
                    Assert.Empty(dragged);

                    CanvasMouseTests.Click(canvas, on);
                    bool wanted = true;
                    canvas.ObjectDragWanted = () => wanted;
                    CanvasMouseTests.Drag(canvas, on, away);

                    Assert.Equal(bottom, Assert.Single(Assert.Single(dragged)));

                    // An object not selected, or a command running, is not dragged out.
                    dragged.Clear();
                    model.Document.Selection.Clear();
                    CanvasMouseTests.Drag(canvas, on, away);
                    Assert.Empty(dragged);
                    CanvasMouseTests.Click(canvas, on);
                    model.Execute("LINE");
                    CanvasMouseTests.Drag(canvas, on, away);
                    Assert.Empty(dragged);
                    canvas.Dispose();
                }
            });
        }

        /// <summary>Whether an element and every element above it is shown, not collapsed or hidden: a window not on screen has no IsVisible.</summary>
        private static bool Shown(UIElement element)
        {
            for (DependencyObject at = element; at != null; at = VisualTreeHelper.GetParent(at))
            {
                if (at is UIElement e && e.Visibility != Visibility.Visible)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> ShownTexts(FrameworkElement body) =>
            Descendants(body).OfType<TextBlock>().Where(Shown).Select(t => t.Text).ToList();

        private static void Layout(FrameworkElement element, double width, double height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
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
