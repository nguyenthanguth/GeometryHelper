using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The font boxes' second names: a TrueType family shows its English name first and the names its name table holds in
    /// other languages after it (DotumChe, 돋움체), a name typed in the box finds it by any of them, and the name a style
    /// stores stays the one the user picked. The tests of the real fonts end quietly where the font is not installed.
    /// </summary>
    public sealed class FontLocalNamesTests
    {
        private const string Korean = "맑은 고딕";

        private static bool Has(string family) =>
            CadTrueTypeFonts.Families.Contains(family, StringComparer.OrdinalIgnoreCase);

        private static MainViewModel Opened(out CadDocument document)
        {
            var database = new DxfDatabase("AC1027");
            AddStyle(database, "Korean", "malgun.ttf", new DxfFontDescriptor(Korean, false, false, 129, 34));
            AddStyle(database, "Malgun", "malgun.ttf", new DxfFontDescriptor("Malgun Gothic", false, false, 129, 34));
            AddStyle(database, "Plain", "arial.ttf", new DxfFontDescriptor("Arial", false, false, 0, 34));
            AddStyle(database, "Absent", "dotumche.ttc", new DxfFontDescriptor("DotumChe", false, false, 129, 34));
            var model = new MainViewModel();
            document = CadDocument.From(database, "names.dxf");
            model.OpenDocument(document);
            return model;
        }

        private static void AddStyle(DxfDatabase database, string name, string file, DxfFontDescriptor font)
        {
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = name });
            DxfTextStyleTableRecord style = database.TextStyleTable[name];
            style.FileName = file;
            style.Font = font;
            style.XScale = 0.8;
        }

        // ---- The labels

        [Theory]
        [InlineData("DotumChe", "돋움체", "DotumChe  돋움체")]
        [InlineData("MS Gothic", "ＭＳ ゴシック", "MS Gothic  ＭＳ ゴシック")]
        [InlineData("Arial", "", "Arial")]
        public void ALabelIsTheEnglishNameThenTheOthers_AndTheEnglishNameAloneWhereThereAreNone(
            string english,
            string other,
            string label
        )
        {
            Assert.Equal(label, FontNameLabels.Label(english, other.Length == 0 ? new string[0] : new[] { other }));
        }

        [Fact]
        public void TheOtherNames_AreDistinct_NeverTheEnglishNameOneOfTheLetters_AndKeepTheirOrder()
        {
            var others = FontNameLabels.Others(
                "Arial",
                new[] { "아리알", "ARIAL", " ", null, "아리알", "Ａｒｉａｌ", "arial" }
            );

            Assert.Equal(new[] { "아리알", "Ａｒｉａｌ" }, others);
            Assert.Equal("Arial  아리알  Ａｒｉａｌ", FontNameLabels.Label("Arial", others));
        }

        [Fact]
        public void AnEntryWithOtherNames_ShowsThemMutedAfterTheName_AndAllOfThemInTheTooltip()
        {
            var item = new FontItemViewModel("DotumChe", true, true, new[] { "돋움체", "dotumche", "돋움체" });

            Assert.Equal("DotumChe", item.Name);
            Assert.Equal(new[] { "돋움체" }, item.LocalNames);
            Assert.Equal("돋움체", item.LocalText);
            Assert.Equal("DotumChe  돋움체", item.AllNames);
        }

        [Fact]
        public void AnEntryOfOneName_HasNoOtherNameAndNoTooltip()
        {
            var item = new FontItemViewModel("GhNoSuchFontFamily", true, false);

            Assert.Empty(item.LocalNames);
            Assert.Equal(string.Empty, item.LocalText);
            Assert.Null(item.AllNames);
            Assert.Equal("(not installed)", item.Hint);
            Assert.Empty(new FontItemViewModel("romans.shx", false).LocalNames);
        }

        [Fact]
        public void ARealFamilyShowsTheOtherNamesOfItsNameTable()
        {
            if (!Has("Malgun Gothic"))
            {
                return;
            }

            var item = new FontItemViewModel("Malgun Gothic", true);

            Assert.Contains(Korean, item.LocalNames);
            Assert.StartsWith("Malgun Gothic  " + Korean, item.AllNames);
        }

        // ---- The search

        [Theory]
        [InlineData("d", true)]
        [InlineData("dot", true)]
        [InlineData("DOTUM", true)]
        [InlineData("돋", true)]
        [InlineData("돋움", true)]
        [InlineData("돋움체", true)]
        [InlineData("움", false)]
        [InlineData("x", false)]
        [InlineData("", false)]
        public void WhatIsTypedMatchesTheStartOfAnyName(string typed, bool found)
        {
            var item = new FontItemViewModel("DotumChe", true, true, new[] { "돋움체" });

            Assert.Equal(found, item.Matches(typed));
        }

        [Fact]
        public void TypingALocalNameOrAnEnglishOne_SelectsTheFontInTheBox_AndLettersKeepGrowingTheSearch()
        {
            OnSta(() =>
            {
                var dotum = new FontItemViewModel("DotumChe", true, true, new[] { "돋움체" });
                var dubai = new FontItemViewModel("Dubai", true, true, new string[0]);
                var gothic = new FontItemViewModel("MS Gothic", true, true, new[] { "ＭＳ ゴシック" });
                var box = new ComboBox { ItemsSource = new[] { dotum, dubai, gothic } };
                FontSearch.SetIsEnabled(box, true);

                Type(box, "돋");
                Assert.Same(dotum, box.SelectedItem);

                box.SelectedItem = null;
                Thread.Sleep(1100);
                Type(box, "d");
                Assert.Same(dotum, box.SelectedItem);
                Type(box, "u");
                Assert.Same(dubai, box.SelectedItem);

                Thread.Sleep(1100);
                Type(box, "Ｍ");
                Assert.Same(gothic, box.SelectedItem);

                Thread.Sleep(1100);
                Type(box, "m");
                Assert.Same(gothic, box.SelectedItem);
                Type(box, "q");
                Assert.Same(gothic, box.SelectedItem);
            });
        }

        // ---- The dialog

        [Fact]
        public void TheFontListIsSortedByTheEnglishName_AndEachEntryCarriesItsOtherNames()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();
                List<string> names = dialog
                    .FontNames.Where(f => f.Name != TextStyleViewModel.BuiltInFont)
                    .Select(f => f.Name)
                    .ToList();

                for (int i = 1; i < names.Count; i++)
                {
                    Assert.True(
                        string.Compare(names[i - 1], names[i], StringComparison.OrdinalIgnoreCase) <= 0,
                        names[i - 1] + " before " + names[i]
                    );
                }

                if (Has("Malgun Gothic"))
                {
                    Assert.Contains(Korean, dialog.FontNames.Single(f => f.Name == "Malgun Gothic").LocalNames);
                }

                Assert.DoesNotContain(dialog.FontNames, f => f.IsTrueType && f.Name == Korean);
            });
        }

        [Fact]
        public void TypingALocalNameInTheTextStyleBox_PicksTheFamily_AndApplyStoresItsEnglishName()
        {
            OnSta(() =>
            {
                if (!Has("Malgun Gothic"))
                {
                    return;
                }

                MainViewModel model = Opened(out CadDocument document);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Plain");
                ComboBox font = Box(body, "Font name");

                Type(font, "맑");

                var picked = Assert.IsType<FontItemViewModel>(font.SelectedItem);
                Assert.Equal("Malgun Gothic", picked.Name);
                Assert.Equal("Malgun Gothic", dialog.FontName);
                Assert.True(dialog.IsDirty);
                Assert.True(dialog.Apply());

                DxfTextStyleTableRecord style = document.Database.TextStyleTable["Plain"];
                Assert.Equal("Malgun Gothic", style.Font.TypeFace);
                Assert.Equal("malgun.ttf", style.FileName, ignoreCase: true);
                window.Close();
            });
        }

        [Fact]
        public void PickingAFamilyWithOtherNamesFromTheList_StoresItsEnglishName()
        {
            OnSta(() =>
            {
                if (!Has("MS Gothic"))
                {
                    return;
                }

                MainViewModel model = Opened(out CadDocument document);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Plain");

                Box(body, "Font name").SelectedItem = dialog.FontNames.Single(f => f.Name == "MS Gothic");

                Assert.True(dialog.Apply());
                Assert.Equal("MS Gothic", document.Database.TextStyleTable["Plain"].Font.TypeFace);
                window.Close();
            });
        }

        [Fact]
        public void AStyleStoringALocalizedName_ShowsTheInstalledFamilyWithBothNames_AndRoundTripsByteForByte()
        {
            OnSta(() =>
            {
                if (!Has("Malgun Gothic"))
                {
                    return;
                }

                MainViewModel model = Opened(out CadDocument document);
                string before = document.Database.ToText();
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Korean");

                ComboBox font = Box(body, "Font name");
                var shown = Assert.IsType<FontItemViewModel>(font.SelectedItem);
                Assert.Equal("Malgun Gothic", shown.Name);
                Assert.Equal(Korean, shown.LocalText);
                Assert.Equal(Korean, dialog.FontName);
                Assert.False(dialog.IsDirty);

                Button apply = Descendants(body).OfType<Button>().Single(b => "Apply".Equals(b.Content));

                foreach (StyleRowViewModel row in dialog.Styles.ToList())
                {
                    Select(dialog, body, row.Name);
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }

                Assert.Equal(before, document.Database.ToText());
                Assert.Equal(Korean, document.Database.TextStyleTable["Korean"].Font.TypeFace);
                window.Close();
            });
        }

        [Fact]
        public void TheClosedBoxShowsBothNames_TheSecondTrimmedWithAnEllipsis_AndTheTooltipHasThemAll()
        {
            OnSta(() =>
            {
                if (!Has("Malgun Gothic"))
                {
                    return;
                }

                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();
                TextStyleWindow window = Show(dialog, out FrameworkElement body);
                Select(dialog, body, "Malgun");
                ComboBox font = Box(body, "Font name");

                TextBlock local = Descendants(font).OfType<TextBlock>().Single(t => t.Text == Korean);
                Assert.Equal(TextTrimming.CharacterEllipsis, local.TextTrimming);
                Assert.Contains(Descendants(font).OfType<TextBlock>(), t => t.Text == "Malgun Gothic");
                Assert.Equal(
                    "Malgun Gothic  " + Korean,
                    Descendants(font).OfType<Grid>().Select(g => g.ToolTip).OfType<string>().Single()
                );
                window.Close();
            });
        }

        [Fact]
        public void TheStyleListShowsTheOtherNamesAfterTheFont_ButOnlyWhatIsStoredForALocalizedOrAbsentOne()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();
                StyleRowViewModel Row(string name) => dialog.Styles.Single(s => s.Name == name);

                Assert.Equal(string.Empty, Row("Plain").DetailLocal);
                Assert.Equal("Arial", Row("Plain").DetailAll);
                Assert.Equal(string.Empty, Row("Absent").DetailLocal);
                Assert.Equal(string.Empty, Row("Korean").DetailLocal);
                Assert.Equal(Korean, Row("Korean").Detail);

                if (Has("Malgun Gothic"))
                {
                    Assert.Equal(Korean, Row("Malgun").DetailLocal);
                    Assert.Equal("Malgun Gothic  " + Korean, Row("Malgun").DetailAll);
                }
            });
        }

        // ---- The MTEXT editor and the Missing Fonts dialog

        [Fact]
        public void TheConverterOfTheMTextFontBox_GivesTheOtherNamesAndTheTooltip()
        {
            var converter = new FontLocalNamesConverter();

            Assert.Equal(string.Empty, converter.Convert("Arial", typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Null(converter.Convert("Arial", typeof(string), "all", CultureInfo.InvariantCulture));
            Assert.Equal(string.Empty, converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));

            if (Has("Malgun Gothic"))
            {
                Assert.Equal(
                    Korean,
                    converter.Convert("Malgun Gothic", typeof(string), null, CultureInfo.InvariantCulture)
                );
                Assert.Equal(
                    "Malgun Gothic  " + Korean,
                    converter.Convert("Malgun Gothic", typeof(string), "all", CultureInfo.InvariantCulture)
                );
            }
        }

        [Fact]
        public void TheDialogsFontListHasOneEntryPerFamily_ByItsEnglishName_AndNoneByALocalName()
        {
            OnSta(() =>
            {
                MainViewModel model = Opened(out _);
                TextStyleViewModel dialog = model.CreateTextStyle();

                Assert.All(dialog.FontNames.Where(f => f.IsTrueType), f => Assert.DoesNotContain(Korean, f.Name));
                Assert.Equal(CadTrueTypeFonts.Families.Count, dialog.FontNames.Count(f => f.IsTrueType));
            });
        }

        [Fact]
        public void AMissingTrueTypeRow_ShowsJustWhatTheDrawingStores_WhereTheMachineLacksTheFamily()
        {
            Assert.Equal(string.Empty, new MissingTrueTypeRowViewModel("DotumChe", null).LocalText);
            Assert.Equal(string.Empty, new MissingTrueTypeRowViewModel("돋움체", null).LocalText);
            Assert.Equal(string.Empty, new MissingTrueTypeRowViewModel("GhNoSuchFontFamily", null).LocalText);

            if (Has("Malgun Gothic"))
            {
                Assert.Equal(Korean, new MissingTrueTypeRowViewModel("Malgun Gothic", null).LocalText);
            }
        }

        // ---- The picture

        [Fact]
        public void TheFontBoxAndItsListWithOtherNames_AreDrawnOffscreen()
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

                foreach (string style in new[] { "Malgun", "Korean", "Absent" })
                {
                    Select(dialog, body, style);
                    Save(body, 780, 560, Path.Combine(folder, "font-names-dialog-" + style + ".png"));
                }

                var items = new[]
                {
                    new FontItemViewModel(TextStyleViewModel.BuiltInFont, false),
                    new FontItemViewModel("romans.shx", false),
                    new FontItemViewModel("Arial", true, true, new string[0]),
                    new FontItemViewModel("DotumChe", true, true, new[] { "돋움체" }),
                    new FontItemViewModel("Malgun Gothic", true, true, new[] { Korean }),
                    new FontItemViewModel("Microsoft JhengHei", true, true, new[] { "微軟正黑體" }),
                    new FontItemViewModel("MS Gothic", true, true, new[] { "ＭＳ ゴシック" }),
                    new FontItemViewModel("SimSun", true, true, new[] { "宋体", "宋體", "宋体" }),
                    new FontItemViewModel("GhNoSuchFontFamily", true, false),
                };

                foreach (int width in new[] { 340, 200 })
                {
                    var list = new ListBox
                    {
                        ItemsSource = items,
                        ItemTemplate = (DataTemplate)window.FindResource("FontItemTemplate"),
                        Width = width,
                        Background = (Brush)window.FindResource("GhInput"),
                        Foreground = (Brush)window.FindResource("GhText"),
                        BorderThickness = new Thickness(0),
                    };
                    list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
                    list.SetValue(
                        System.Windows.Documents.TextElement.FontFamilyProperty,
                        window.FindResource("GhFont")
                    );
                    list.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, 12.0);
                    var frame = new Border { Child = list, Background = (Brush)window.FindResource("GhInput") };
                    frame.Measure(new Size(width, 320));
                    frame.Arrange(new Rect(0, 0, width, 320));
                    frame.UpdateLayout();
                    frame.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                    Assert.True(list.ActualHeight > 100, "list " + list.ActualWidth + " x " + list.ActualHeight);
                    Save(frame, width, 320, Path.Combine(folder, "font-names-list-" + width + ".png"));
                }

                window.Close();
            });
        }

        private static void Save(FrameworkElement element, int width, int height, string path)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));

            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static void Type(UIElement box, string text) =>
            box.RaiseEvent(
                new TextCompositionEventArgs(
                    Keyboard.PrimaryDevice,
                    new TextComposition(InputManager.Current, box, text)
                )
                {
                    RoutedEvent = UIElement.PreviewTextInputEvent,
                }
            );

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
