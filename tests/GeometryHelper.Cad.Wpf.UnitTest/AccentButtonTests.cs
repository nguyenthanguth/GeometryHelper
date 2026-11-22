using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The blue buttons (the start screen's Open DXF, the OK and Apply of the dialogs): under the mouse they get a lighter blue
    /// and pressed a darker one, as the orange Close does, and never the grey that a flat button turns to.
    /// </summary>
    public sealed class AccentButtonTests
    {
        [Fact]
        public void TheThemesAccentSteps_AreALighterAndADarkerBlueThanTheAccent()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                Color accent = Colour(theme, "GhAccent");
                Color hover = Colour(theme, "GhAccentHover");
                Color pressed = Colour(theme, "GhAccentPressed");
                Assert.Equal(Color.FromRgb(0x3D, 0x9B, 0xFF), accent);
                Assert.True(Luminance(hover) > Luminance(accent), hover.ToString());
                Assert.True(Luminance(pressed) < Luminance(accent), pressed.ToString());
                Assert.NotEqual(Colour(theme, "GhHover"), hover);
            });
        }

        [Fact]
        public void TheSharedAccentButton_IsBlue_LighterUnderTheMouse_DarkerPressed_AndGreyWhenDisabled()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                AssertAccentButton(theme, (Style)theme["GhAccentButton"]);
            });
        }

        [Fact]
        public void TheStartScreensOpenButton_StaysBlueUnderTheMouse()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                var workspace = new CadWorkspace();
                AssertAccentButton(theme, (Style)workspace.FindResource("PrimaryButton"));
            });
        }

        [Fact]
        public void TheOkOfAStyleDialog_StaysBlueUnderTheMouse()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                var dialogs = new ResourceDictionary();
                dialogs.MergedDictionaries.Add(theme);
                dialogs.MergedDictionaries.Add(
                    (ResourceDictionary)
                        Application.LoadComponent(
                            new Uri("/GeometryHelper.Cad.Wpf;component/Themes/StyleDialogs.xaml", UriKind.Relative)
                        )
                );
                AssertAccentButton(theme, (Style)dialogs["StylePrimaryButton"]);
            });
        }

        [Fact]
        public void ThePrimaryOfTheGlobalSettingsAndOfThePluginManager_StaysBlueUnderTheMouse()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                var settings = new GlobalSettingsWindow(new MainViewModel().CreateGlobalSettings("Text and fonts"));
                AssertAccentButton(theme, (Style)settings.FindResource("Primary"));

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
                    var model = new MainViewModel();
                    model.UsePlugins(plugins, loadAtStart: false);
                    var window = new AppLoadWindow(new AppLoadViewModel(model));
                    AssertAccentButton(theme, (Style)window.FindResource("Primary"));
                }
            });
        }

        [Fact]
        public void TheYesOfAQuestion_UsesTheAccentStyle()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                var question = new CadQuestionWindow("Title", "Text", "Detail", "Load", "Do not load");
                FrameworkElement body = question.Body;
                question.Content = null;
                Layout(body);
                Button yes = Descendants(body).OfType<Button>().Single(b => "Load".Equals(b.Content));
                Assert.Same(question.FindResource("GhAccentButton"), yes.Style);
                Assert.Equal(Colour(theme, "GhAccent"), ((SolidColorBrush)yes.Background).Color);
            });
        }

        [Fact]
        public void AFlatButton_StillTurnsGreyUnderTheMouse()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = Theme();
                var button = new Button { Style = (Style)theme["GhFlatButton"], Content = "Flat" };
                Layout(button);
                Assert.Equal(Colour(theme, "GhHover"), ChromeBackgroundWhen(button.Template, "IsMouseOver"));
            });
        }

        // A button with this style: blue, with the lighter and the darker blue set by its template, grey when disabled.
        private static void AssertAccentButton(ResourceDictionary theme, Style style)
        {
            var button = new Button { Style = style, Content = "OK" };
            Layout(button);
            Assert.Equal(Colour(theme, "GhAccent"), ((SolidColorBrush)button.Background).Color);
            Assert.Equal(Colour(theme, "GhAccentInk"), ((SolidColorBrush)button.Foreground).Color);
            Color hover = ChromeBackgroundWhen(button.Template, "IsMouseOver");
            Assert.Equal(Colour(theme, "GhAccentHover"), hover);
            Assert.NotEqual(Colour(theme, "GhHover"), hover);
            Color pressed = ChromeBackgroundWhen(button.Template, "IsPressed");
            Assert.Equal(Colour(theme, "GhAccentPressed"), pressed);
            Assert.NotEqual(Colour(theme, "GhSelected"), pressed);
            Assert.Equal(Colour(theme, "GhBorder"), ChromeBackgroundWhen(button.Template, "IsEnabled"));
        }

        // The colour the template gives its Chrome border when the property is set (IsEnabled: when it is false).
        private static Color ChromeBackgroundWhen(ControlTemplate template, string property)
        {
            var setters = new List<Setter>();
            foreach (TriggerBase trigger in template.Triggers)
            {
                if (trigger is Trigger single && single.Property.Name == property)
                {
                    setters.AddRange(single.Setters.OfType<Setter>());
                }
                else if (
                    property != "IsEnabled"
                    && trigger is MultiTrigger multi
                    && multi.Conditions.Any(c => c.Property.Name == property)
                )
                {
                    setters.AddRange(multi.Setters.OfType<Setter>());
                }
            }

            Setter chrome = setters.Single(s => s.TargetName == "Chrome" && s.Property == Border.BackgroundProperty);
            return ((SolidColorBrush)chrome.Value).Color;
        }

        private static ResourceDictionary Theme()
        {
            // Registers the pack scheme, which the theme is loaded through, with no Application running.
            _ = PackUriHelper.UriSchemePack;
            return (ResourceDictionary)
                Application.LoadComponent(
                    new Uri("/GeometryHelper.Cad.Wpf;component/Themes/Dark.xaml", UriKind.Relative)
                );
        }

        private static Color Colour(ResourceDictionary theme, string key) => ((SolidColorBrush)theme[key]).Color;

        private static double Luminance(Color color)
        {
            double Channel(byte value)
            {
                double c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
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
            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
