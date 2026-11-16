using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// What was greyed with a working command behind it, now wired: Select Similar, Add Selected, QuickCalc and Quick
    /// Properties in the canvas menus, Add Current Scale and the rest of Annotative Object Scale, Save As's Other Formats; the
    /// Select Similar Settings dialog of SELECTSIMILAR's SEttings; the text window of TEXTSCR and F2.
    /// </summary>
    public sealed class SelectSimilarWiringAndTextWindowTests
    {
        /// <summary>A drawing of two lines on layer A, one on B and a circle on A, in millimetres.</summary>
        private static MainViewModel Opened(out string[] handles)
        {
            var model = new MainViewModel();
            DxfDatabase db = CadTemplate.CreateMetric();
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "A" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "B" });
            var a1 = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "A" };
            var a2 = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)) { Layer = "A" };
            var b1 = new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)) { Layer = "B" };
            var c1 = new DxfCircle(new GeoPoint3(300, 0, 0), new GeoVector3(0, 0, 1), 20) { Layer = "A" };
            db.ModelSpace.AppendEntity(a1);
            db.ModelSpace.AppendEntity(a2);
            db.ModelSpace.AppendEntity(b1);
            db.ModelSpace.AppendEntity(c1);
            handles = new[] { a1.Handle, a2.Handle, b1.Handle, c1.Handle };
            model.OpenDocument(CadDocument.From(db, "similar.dxf"));
            return model;
        }

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.Header).ToArray();

        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> items, string header) =>
            items.Single(i => i.Header == header);

        [Fact]
        public void SelectSimilarInTheEditMenu_SelectsTheLinesOfThatLayer()
        {
            MainViewModel model = Opened(out string[] h);
            model.Document.Selection.Set(new[] { h[0] });

            CadMenuItemViewModel item = Item(model.CanvasMenu(), "Select Similar");
            Assert.True(item.IsEnabled);
            Assert.Null(item.ToolTip);
            item.Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.Equal(new[] { h[0], h[1] }.OrderBy(x => x), model.Document.Selection.Handles.OrderBy(x => x));
        }

        [Fact]
        public void AddSelectedInTheEditMenu_StartsTheCommandThatDrawsTheKindSelected_OnItsLayer()
        {
            MainViewModel model = Opened(out string[] h);
            model.Document.Selection.Set(new[] { h[3] });

            CadMenuItemViewModel item = Item(model.CanvasMenu(), "Add Selected");
            Assert.True(item.IsEnabled);
            item.Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.Equal("Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:", model.Prompt);
            model.Execute("500,0");
            model.Execute("10");
            Assert.Equal("A", model.Document.ModelSpace.Block.OfType<DxfCircle>().Last().Layer);
        }

        [Fact]
        public void QuickCalcAndQuickProperties_AreEnabledInTheMenus_AndOpenTheirWindows()
        {
            MainViewModel model = Opened(out string[] h);
            QuickCalcViewModel palette = null;
            model.ShowQuickCalc = p => palette = p;

            CadMenuItemViewModel calc = Item(model.CanvasMenu(), "QuickCalc");
            Assert.True(calc.IsEnabled);
            calc.Command.Execute(null);
            Assert.NotNull(palette);

            // While a command waits for a point, the Command menu opens the palette beside it and leaves the prompt as it was.
            palette = null;
            model.Execute("LINE");
            model.Execute("0,0");
            string prompt = model.Prompt;
            CadMenuItemViewModel inCommand = Item(model.CanvasMenu(), "QuickCalc");
            Assert.True(inCommand.IsEnabled);
            inCommand.Command.Execute(null);
            Assert.NotNull(palette);
            Assert.Equal(prompt, model.Prompt);
            model.Escape();

            model.Document.Selection.Set(new[] { h[0] });
            CadMenuItemViewModel quick = Item(model.CanvasMenu(), "Quick Properties");
            Assert.True(quick.IsEnabled);
            quick.Command.Execute(null);
            Assert.True(model.IsQuickPropertiesShown);
        }

        [Fact]
        public void AnnotativeObjectScale_RunsItsThreeCommandsOnTheSelection_AndSynchronizeStaysGrey()
        {
            var model = new MainViewModel();
            DxfDatabase db = CadTemplate.CreateMetric();
            var text = new DxfDBText
            {
                Position = new GeoPoint3(0, 0, 0),
                Height = 5,
                TextString = "NOTE",
            };
            db.ModelSpace.AppendEntity(text);
            model.OpenDocument(CadDocument.From(db, "scale.dxf"));
            model.Document.Selection.Set(new[] { text.Handle });

            CadMenuItemViewModel scales = Item(model.CanvasMenu(), "Annotative Object Scale");
            Assert.True(scales.IsEnabled);
            Assert.False(Item(scales.Items, "Synchronize Multiple-scale Positions").IsEnabled);

            foreach (string name in new[] { "Add Current Scale", "Delete Current Scale" })
            {
                int before = model.History.Count;
                CadMenuItemViewModel item = Item(scales.Items, name);
                Assert.True(item.IsEnabled);
                item.Command.Execute(null);
                string command = name.StartsWith("Add", StringComparison.Ordinal)
                    ? "AIOBJECTSCALEADD"
                    : "AIOBJECTSCALEREMOVE";
                Assert.Contains("Command: " + command, model.History.Skip(before));
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void SaveAsOtherFormats_IsEnabledWithADrawing_AndRunsExport()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(CadTemplate.CreateMetric(), "export.dxf"));
            model.AskExportFile = job => job.FileName = null;
            AppMenuViewModel menu = model.CreateAppMenu();

            AppMenuItemViewModel other = menu
                .Items.Single(i => i.Key == "saveas")
                .Items.Single(i => i.Name == "Other Formats");
            Assert.True(other.IsEnabled);
            menu.Choose(other);

            Assert.Contains("Command: EXPORT", model.History);
        }

        [Fact]
        public void TheSettingsDialog_ListsTheEightPropertiesInAutoCadsOrder_TickedAsTheModeHasThem()
        {
            var dialog = new SelectSimilarSettingsViewModel(130);

            Assert.Equal(
                new[]
                {
                    "Color",
                    "Layer",
                    "Linetype",
                    "Linetype scale",
                    "Lineweight",
                    "Plot style",
                    "Object style",
                    "Name",
                },
                dialog.Options.Select(o => o.Name)
            );
            Assert.Equal(
                new[] { false, true, false, false, false, false, false, true },
                dialog.Options.Select(o => o.IsChecked)
            );
            Assert.Equal(130, dialog.Mode);

            dialog.Options[0].IsChecked = true;
            dialog.Options[1].IsChecked = false;
            Assert.Equal(1 | 128, dialog.Mode);
            foreach (SelectSimilarOptionViewModel option in dialog.Options)
            {
                option.IsChecked = false;
            }

            Assert.Equal(0, dialog.Mode);
        }

        [Fact]
        public void SettingsTyped_ShowTheDialogOverTheMode_AndOkSetsSelectSimilarMode()
        {
            MainViewModel model = Opened(out string[] h);
            SelectSimilarSettingsViewModel shown = null;
            model.ShowSelectSimilarSettings = dialog =>
            {
                shown = dialog;
                dialog.Options.Single(o => o.Name == "Layer").IsChecked = false;
                dialog.Options.Single(o => o.Name == "Name").IsChecked = false;
                return true;
            };

            model.Execute("SELECTSIMILAR");
            Assert.Equal("Select objects or [SEttings]:", model.Prompt);
            model.Execute("SE");

            Assert.NotNull(shown);
            model.Document.Selection.Clear();
            model.Session.FeedSelection(new[] { h[0] });
            model.Session.Enter();

            // With the layer no longer compared, the line on B is like the lines on A.
            Assert.Equal(new[] { h[0], h[1], h[2] }.OrderBy(x => x), model.Document.Selection.Handles.OrderBy(x => x));
            model.Execute("SELECTSIMILARMODE");
            Assert.Equal("Enter new value for SELECTSIMILARMODE <0>:", model.Prompt);
            model.Escape();
        }

        [Fact]
        public void TextscrShowsTheTextWindowOverTheHistory_AndF2RunsIt()
        {
            MainViewModel model = Opened(out _);
            TextWindowViewModel shown = null;
            model.ShowTextWindow = window => shown = window;

            model.Execute("TEXTSCR");
            Assert.NotNull(shown);
            Assert.Same(model.History, shown.Lines);
            Assert.Equal("Command: TEXTSCR", shown.Lines.Last());

            shown = null;
            model.Execute("LINE");
            Assert.True(model.RunKey("F2"));
            Assert.NotNull(shown);

            // F2 shows the window beside the command and does not end it.
            Assert.True(model.IsCommandRunning);
            Assert.Equal("Specify first point:", model.Prompt);
        }

        [Fact]
        public void WithNoWindow_TextscrSaysSo()
        {
            MainViewModel model = Opened(out _);

            model.Execute("TEXTSCR");

            Assert.Equal("The text window is shown by the window.", model.History.Last());
        }

        [Fact]
        public void TheTextWindowCopiesTheLinesChosen_OrEveryLineWhereNoneIs()
        {
            MainViewModel model = Opened(out _);
            TextWindowViewModel shown = null;
            model.ShowTextWindow = window => shown = window;
            model.Execute("TEXTSCR");

            string all = shown.TextToCopy(null);
            Assert.Equal(string.Join("\r\n", model.History), all);
            Assert.Equal("a\r\nb", shown.TextToCopy(new[] { "a", "b" }));
            Assert.Equal(all, shown.TextToCopy(new string[0]));
        }

        [Fact]
        public void TheTwoWindows_AreLaidOutAndFollowTheHistoryOffscreen()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                MainViewModel model = Opened(out _);
                model.ShowTextWindow = _ => { };
                model.Execute("TEXTSCR");
                var text = new TextWindow(new TextWindowViewModel(model.History));
                var settings = new SelectSimilarSettingsWindow(new SelectSimilarSettingsViewModel(130));

                Assert.Equal(720, text.Width);
                Assert.Equal(360, settings.Width);

                foreach (
                    (Window window, FrameworkElement body) in new (Window, FrameworkElement)[]
                    {
                        (text, text.Body),
                        (settings, settings.Body),
                    }
                )
                {
                    object context = window.DataContext;
                    window.Content = null;
                    body.DataContext = context;
                    Layout(body, window.Width, window.Height);
                    window.Close();
                }

                Assert.Equal(model.History.Count, text.Model.Lines.Count);
                Assert.Equal(8, Descendants(settings.Body).OfType<CheckBox>().Count());
                Assert.Contains(Descendants(text.Body).OfType<Button>(), b => "Copy".Equals(b.Content));
            });
        }

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
