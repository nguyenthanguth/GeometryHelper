using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Close in orange-red (design v70): the theme's footer Close and title-bar cross with the design's colours, the cross of a
    /// drawing row readable on a selected row, and the dialogs with a footer Close drawn offscreen, their Close and their
    /// cross on the new styles and every other button left as it was.
    /// </summary>
    public sealed class CloseButtonTests
    {
        private const string SampleName = "GeometryHelper.Dxf.Viewer.SamplePlugin";

        [Fact]
        public void TheThemesCloseStyles_CarryTheDesignsColours()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                Assert.Equal(Rgb(0xC2, 0x41, 0x0C), ((SolidColorBrush)theme["GhClose"]).Color);
                Assert.Equal(Rgb(0xDC, 0x5A, 0x1E), ((SolidColorBrush)theme["GhCloseHover"]).Color);
                Assert.Equal(Rgb(0xF2, 0x6B, 0x3A), ((SolidColorBrush)theme["GhCloseCross"]).Color);
                Assert.Equal(Rgb(0xFF, 0x9A, 0x6E), ((SolidColorBrush)theme["GhCloseCrossOnSelected"]).Color);
                Color pressed = ((SolidColorBrush)theme["GhClosePressed"]).Color;
                Assert.True(Luminance(pressed) < Luminance(Rgb(0xC2, 0x41, 0x0C)), pressed.ToString());

                var close = new Button { Style = (Style)theme["GhCloseButton"], Content = "Close" };
                Layout(close);
                Assert.Equal(Rgb(0xC2, 0x41, 0x0C), ((SolidColorBrush)close.Background).Color);
                Assert.Equal(Colors.White, ((SolidColorBrush)close.Foreground).Color);
                Assert.Equal(FontWeights.SemiBold, close.FontWeight);
                Assert.Equal(12.0, close.FontSize);
                Assert.Equal(new Thickness(0), close.BorderThickness);
                Border chrome = Descendants(close).OfType<Border>().First();
                Assert.Equal(new CornerRadius(4), chrome.CornerRadius);
                Assert.Equal(Rgb(0xDC, 0x5A, 0x1E), BackgroundSetBy(close.Template, "IsMouseOver"));

                var cross = new Button { Style = (Style)theme["GhTitleCloseButton"] };
                Layout(cross);
                IconView icon = Descendants(cross).OfType<IconView>().Single();
                Assert.Equal(Rgb(0xF2, 0x6B, 0x3A), ((SolidColorBrush)icon.Stroke).Color);
                Assert.Equal(
                    Colors.Transparent,
                    ((SolidColorBrush)Descendants(cross).OfType<Border>().First().Background).Color
                );
                Assert.Equal(Rgb(0xC2, 0x41, 0x0C), BackgroundSetBy(cross.Template, "IsMouseOver"));
                Setter white = SettersOf(cross.Template, "IsMouseOver")
                    .Single(s => s.TargetName == "Cross" && s.Property == IconView.StrokeProperty);
                Assert.Equal(Colors.White, ((SolidColorBrush)white.Value).Color);
            });
        }

        [Fact]
        public void TheCrossOfADrawingRow_StaysAtThreeToOne_OnTheSelectedRowWhereTheDesignsOrangeDropsBelow()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                Color selected = (Color)theme["GhSelectedColor"];
                Color cross = ((SolidColorBrush)theme["GhCloseCross"]).Color;
                Color onSelected = ((SolidColorBrush)theme["GhCloseCrossOnSelected"]).Color;

                // #F26B3A on #1F4E79 is 2.86 to 1, under the 3 to 1 of a graphic; #FF9A6E is 4.17 to 1.
                Assert.InRange(Contrast(cross, selected), 2.8, 2.9);
                Assert.True(Contrast(onSelected, selected) >= 3.0, Contrast(onSelected, selected).ToString("0.00"));
                Assert.True(Contrast(cross, (Color)theme["GhPanelColor"]) >= 3.0);
                Assert.True(Contrast(cross, ((SolidColorBrush)theme["GhHover"]).Color) >= 3.0);
            });
        }

        [Fact]
        public void TheCrossOfAnOpenDrawingRow_IsOrange_AndLighterOnTheCurrentRow()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("first.dxf", TestFiles.Room(out _, out _))));
                    Assert.True(model.Open(folder.Write("second.dxf", TestFiles.Room(out _, out _))));
                    var workspace = new CadWorkspace { DataContext = model };
                    workspace.Measure(new Size(1600, 960));
                    workspace.Arrange(new Rect(0, 0, 1600, 960));
                    workspace.UpdateLayout();

                    List<IconView> crosses = Descendants(workspace)
                        .OfType<IconView>()
                        .Where(i => i.Name == "CloseCross" && i.DataContext is FileRowViewModel row && row.IsOpen)
                        .ToList();
                    Assert.Equal(2, crosses.Count);
                    IconView current = Assert.Single(crosses, i => ((FileRowViewModel)i.DataContext).IsCurrent);
                    Assert.Equal(Rgb(0xFF, 0x9A, 0x6E), ((SolidColorBrush)current.Stroke).Color);
                    Assert.Equal(
                        Rgb(0xF2, 0x6B, 0x3A),
                        ((SolidColorBrush)crosses.Single(i => i != current).Stroke).Color
                    );
                }
            });
        }

        [Fact]
        public void EveryDialogWithAFooterClose_DrawsItOrangeRed_AndItsTitleCross_LeavingTheOtherButtonsAlone()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                using (
                    var plugins = new CadPluginManager(
                        new CadPluginAssemblyLoader(),
                        Path.Combine(folder.FullPath, "plugins"),
                        Path.Combine(folder.FullPath, "plugins.json"),
                        Path.Combine(folder.FullPath, "copies")
                    )
                )
                {
                    string dev = Path.Combine(folder.FullPath, "dev");
                    Directory.CreateDirectory(dev);
                    string sample = Path.Combine(dev, SampleName + ".dll");
                    File.Copy(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll"),
                        sample,
                        true
                    );
                    plugins.AddTrustedFolder(dev);
                    Assert.True(plugins.Load(sample).Succeeded);

                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                    DimStyleManagerViewModel dimensions = model.CreateDimStyleManager();

                    using (LayerManagerViewModel layers = model.CreateLayerManager())
                    using (var pluginManager = new PluginManagerViewModel(plugins))
                    {
                        var windows = new List<(Window Window, FrameworkElement Body, bool HasCross)>();
                        var layerWindow = new LayerManagerWindow(layers);
                        windows.Add((layerWindow, layerWindow.Body, true));
                        var pluginWindow = new PluginManagerWindow(pluginManager);
                        windows.Add((pluginWindow, pluginWindow.Body, true));
                        var settingsWindow = new PluginSettingsWindow(pluginManager.CreateSettings());
                        windows.Add((settingsWindow, settingsWindow.Body, true));
                        var textWindow = new TextStyleWindow(model.CreateTextStyle());
                        windows.Add((textWindow, textWindow.Body, true));
                        var dimWindow = new DimStyleManagerWindow(dimensions);
                        windows.Add((dimWindow, dimWindow.Body, true));
                        var compareWindow = new DimStyleCompareWindow(dimensions.CreateCompare());
                        windows.Add((compareWindow, compareWindow.Body, false));

                        foreach ((Window window, FrameworkElement body, bool hasCross) in windows)
                        {
                            Style footer = (Style)window.FindResource("GhCloseButton");
                            Style title = (Style)window.FindResource("GhTitleCloseButton");
                            object context = window.DataContext;
                            window.Content = null;
                            body.DataContext = context;
                            Layout(body);
                            List<Button> buttons = Descendants(body).OfType<Button>().ToList();

                            Button close = Assert.Single(buttons, b => "Close".Equals(b.Content));
                            Assert.Same(footer, close.Style);
                            Assert.Equal(Rgb(0xC2, 0x41, 0x0C), ((SolidColorBrush)close.Background).Color);
                            Assert.DoesNotContain(buttons, b => b != close && b.Style == footer);

                            List<Button> crosses = buttons.Where(b => b.Style == title).ToList();
                            Assert.Equal(hasCross ? 1 : 0, crosses.Count);
                            Assert.All(crosses, b => Assert.Equal("Close", b.ToolTip));
                            Assert.All(crosses, b =>
                                Assert.Equal(
                                    Rgb(0xF2, 0x6B, 0x3A),
                                    ((SolidColorBrush)Descendants(b).OfType<IconView>().Single().Stroke).Color
                                )
                            );
                            window.Close();
                        }
                    }
                }
            });
        }

        private static ResourceDictionary Theme()
        {
            // Registers the pack scheme, which the theme is loaded through, with no Application running.
            _ = PackUriHelper.UriSchemePack;
            return (ResourceDictionary)
                Application.LoadComponent(
                    new Uri("/GeometryHelper.Dxf.Viewer.Wpf;component/Themes/Dark.xaml", UriKind.Relative)
                );
        }

        private static Color BackgroundSetBy(ControlTemplate template, string property)
        {
            Setter setter = SettersOf(template, property)
                .Single(s => s.TargetName == "Chrome" && s.Property == Border.BackgroundProperty);
            return ((SolidColorBrush)setter.Value).Color;
        }

        private static IEnumerable<Setter> SettersOf(ControlTemplate template, string property) =>
            template
                .Triggers.OfType<MultiTrigger>()
                .Where(t => t.Conditions.Any(c => c.Property.Name == property))
                .SelectMany(t => t.Setters.OfType<Setter>());

        private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

        // The relative luminance and contrast ratio of WCAG 2.
        private static double Luminance(Color color)
        {
            double Channel(byte value)
            {
                double c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
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
