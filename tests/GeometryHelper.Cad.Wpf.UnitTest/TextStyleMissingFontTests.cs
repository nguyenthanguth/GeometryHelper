using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Text Style dialog's font boxes where a style names a font this machine lacks (a TrueType family nobody installed, a
    /// localized family name, an SHX file not in the font folders): the box shows the style's own font, marked as not here, and
    /// showing a style never rewrites it, so that Apply after a look at every style changes nothing.
    /// </summary>
    public sealed class TextStyleMissingFontTests
    {
        private const string Missing = "GhNoSuchFontFamily";
        private const string Localized = "맑은 고딕";

        private static MainViewModel Opened(out CadDocument document)
        {
            var database = new DxfDatabase("AC1027");
            AddStyle(
                database,
                "Absent",
                "ghnosuchfontfamily.ttf",
                string.Empty,
                new DxfFontDescriptor(Missing, false, false, 129, 34)
            );
            AddStyle(
                database,
                "AbsentBold",
                "ghnosuchfontfamily.ttf",
                string.Empty,
                new DxfFontDescriptor(Missing, true, true, 129, 34)
            );
            AddStyle(
                database,
                "Korean",
                "malgun.ttf",
                string.Empty,
                new DxfFontDescriptor(Localized, false, false, 129, 34)
            );
            AddStyle(database, "NoFile", "ghnosuchfont.shx", "ghnosuchbig.shx", default);
            AddStyle(database, "FileOnly", "ghnosuchfontfile.ttf", string.Empty, default);
            AddStyle(database, "ArialFile", "arial.ttf", string.Empty, default);
            AddStyle(
                database,
                "TtWithBig",
                "arial.ttf",
                "ghnosuchbig.shx",
                new DxfFontDescriptor("Arial", false, false, 0, 34)
            );
            AddStyle(database, "Fine", "ghnosuchfont.shx", string.Empty, default);
            database.TextStyleTable["Fine"].TextSize = 2.123456;
            database.TextStyleTable["Fine"].XScale = 0.123456789;
            database.TextStyleTable["Fine"].ObliquingAngle = 12.34567 * Math.PI / 180.0;
            var model = new MainViewModel();
            document = CadDocument.From(database, "missing.dxf");
            model.OpenDocument(document);
            return model;
        }

        private static void AddStyle(DxfDatabase database, string name, string file, string big, DxfFontDescriptor font)
        {
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = name });
            DxfTextStyleTableRecord style = database.TextStyleTable[name];
            style.FileName = file;
            style.BigFontFileName = big;
            style.Font = font;
            style.XScale = 0.8;
        }

        [Fact]
        public void EachStyleShowsItsOwnFontInTheBox_EvenWhereTheMachineHasNone()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                ComboBox font = Box(body, "Font name");

                foreach (
                    (string style, string name, bool installed) in new[]
                    {
                        ("Absent", Missing, false),
                        ("AbsentBold", Missing, false),
                        ("NoFile", "ghnosuchfont.shx", false),
                        ("FileOnly", "ghnosuchfontfile.ttf", false),
                    }
                )
                {
                    Select(dialog, body, style);
                    var shown = Assert.IsType<FontItemViewModel>(font.SelectedItem);
                    Assert.Equal(name, shown.Name);
                    Assert.Equal(installed, shown.IsInstalled);
                    Assert.False(dialog.IsDirty, style);
                }

                window.Close();
            });
        }

        [Fact]
        public void ALocalizedFamilyNameThatIsInstalledUnderAnotherName_SelectsTheInstalledEntry_AndKeepsTheStoredName()
        {
            OnSta(() =>
            {
                if (!SkiaCanvasRenderer.HasFamily(Localized))
                {
                    return;
                }

                MainViewModel model = Opened(out CadDocument document);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Korean");

                var shown = Assert.IsType<FontItemViewModel>(Box(body, "Font name").SelectedItem);
                Assert.True(shown.IsInstalled);
                Assert.Equal("Malgun Gothic", shown.Name);
                Assert.True(dialog.IsTrueType);
                Assert.False(dialog.IsDirty);
                Assert.Equal(Localized, document.Database.TextStyleTable["Korean"].Font.TypeFace);
                window.Close();
            });
        }

        [Fact]
        public void TheBigFontBoxShowsTheStylesBigFont_WhereNoFolderHasIt()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "NoFile");

                Assert.NotNull(Box(body, "Big font").SelectedItem);
                var shown = Assert.IsType<FontItemViewModel>(Box(body, "Big font").SelectedItem);
                Assert.Equal("ghnosuchbig.shx", shown.Name);
                Assert.False(shown.IsInstalled);
                Assert.True(dialog.UseBigFont);
                Assert.False(dialog.IsDirty);
                window.Close();
            });
        }

        [Fact]
        public void ShowingEveryStyleAndPressingApply_LeavesEveryStyleAsItWas()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out CadDocument document);
                string before = document.Database.ToText();
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Button apply = Descendants(body).OfType<Button>().Single(b => "Apply".Equals(b.Content));

                foreach (StyleRowViewModel row in dialog.Styles.ToList())
                {
                    Select(dialog, body, row.Name);
                    Assert.False(dialog.IsDirty, row.Name);
                    Assert.False(dialog.HasNotice, row.Name);
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }

                Assert.Equal(before, document.Database.ToText());

                DxfTextStyleTableRecord noFile = document.Database.TextStyleTable["NoFile"];
                Assert.Equal("ghnosuchfont.shx", noFile.FileName);
                Assert.Equal("ghnosuchbig.shx", noFile.BigFontFileName);
                Assert.Equal(Missing, document.Database.TextStyleTable["AbsentBold"].Font.TypeFace);
                Assert.True(document.Database.TextStyleTable["AbsentBold"].Font.Bold);
                Assert.Equal(129, document.Database.TextStyleTable["Absent"].Font.CharacterSet);
                window.Close();
            });
        }

        [Fact]
        public void ARealEditOfAStyleWithAnAbsentFont_StillMarksItDirty_AndKeepsTheFont()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out CadDocument document);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Absent");
                Assert.False(dialog.IsDirty);

                dialog.WidthFactor = "0.5";
                Assert.True(dialog.IsDirty);
                Assert.True(dialog.Apply());

                DxfTextStyleTableRecord style = document.Database.TextStyleTable["Absent"];
                Assert.Equal(0.5, style.XScale, 12);
                Assert.Equal(Missing, style.Font.TypeFace);
                Assert.Equal("ghnosuchfontfamily.ttf", style.FileName);
                Assert.Equal(129, style.Font.CharacterSet);

                dialog.FontStyle = "Bold";
                Assert.True(dialog.IsDirty);
                Assert.True(dialog.Apply());
                Assert.True(document.Database.TextStyleTable["Absent"].Font.Bold);
                Assert.Equal(Missing, document.Database.TextStyleTable["Absent"].Font.TypeFace);
                window.Close();
            });
        }

        [Fact]
        public void PickingAnotherFont_IsARealEdit_AndPickingTheAbsentOneBackClearsIt()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out CadDocument document);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Absent");
                ComboBox font = Box(body, "Font name");
                FontItemViewModel own = (FontItemViewModel)font.SelectedItem;
                FontItemViewModel other = dialog.FontNames.First(f => f.IsTrueType && f.IsInstalled);

                font.SelectedItem = other;
                Assert.True(dialog.IsDirty);
                Assert.Equal(other.Name, dialog.FontName);

                font.SelectedItem = own;
                Assert.False(dialog.IsDirty);
                Assert.Equal(Missing, dialog.FontName);
                window.Close();
            });
        }

        [Fact]
        public void TheDialogOfTheMissingFonts_IsDrawnOffscreen()
        {
            string folder = Environment.GetEnvironmentVariable("GH_PNG_FOLDER");

            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            OnSta(() =>
            {
                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);

                foreach (string style in new[] { "Standard", "Absent", "NoFile", "Korean" })
                {
                    Select(dialog, body, style);
                    body.Measure(new Size(780, 560));
                    body.Arrange(new Rect(0, 0, 780, 560));
                    body.UpdateLayout();
                    var target = new RenderTargetBitmap(780, 560, 96, 96, PixelFormats.Pbgra32);
                    target.Render(body);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(target));

                    using (FileStream stream = File.Create(Path.Combine(folder, "text-style-" + style + ".png")))
                    {
                        encoder.Save(stream);
                    }
                }

                window.Close();
            });
        }

        private static TextStyleWindow Show(TextStyleViewModel dialog, out FrameworkElement body)
        {
            var window = new TextStyleWindow(dialog);
            body = window.Body;
            window.Content = null;
            body.DataContext = dialog;
            Layout(body);
            return window;
        }

        private static void Select(TextStyleViewModel dialog, FrameworkElement body, string name)
        {
            dialog.SelectedStyle = dialog.Styles.Single(s => s.Name == name);
            Layout(body);
            body.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            Layout(body);
        }

        private static ComboBox Box(FrameworkElement body, string name) =>
            Descendants(body).OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == name);

        private static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(780, 560));
            element.Arrange(new Rect(0, 0, 780, 560));
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
