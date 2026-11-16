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
    /// OVERKILL's Delete Duplicate Objects dialog: its view model (the boxes, the tolerance, what OK hands back), the command
    /// driven through the main view model with the dialog stubbed, and the window laid out offscreen.
    /// </summary>
    public sealed class OverkillDialogTests
    {
        private static CadOverkillRequest Request(int selected = 3, CadOverkillSettings settings = null) =>
            new CadOverkillRequest(settings ?? new CadOverkillSettings(), selected);

        private static MainViewModel TwoLines(out DxfLine first, out DxfLine second)
        {
            var database = new DxfDatabase("AC1027");
            first = new DxfLine
            {
                StartPoint = new GeoPoint3(0, 0, 0),
                EndPoint = new GeoPoint3(100, 0, 0),
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
            };
            second = new DxfLine
            {
                StartPoint = new GeoPoint3(0, 0, 0),
                EndPoint = new GeoPoint3(100, 0, 0),
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5),
            };
            database.ModelSpace.AppendEntity(first);
            database.ModelSpace.AppendEntity(second);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plan.dxf"), "plan.dxf");
            return model;
        }

        [Fact]
        public void TheDialogOpensOnTheSettingsTheCommandHas_NineBoxesInTheDesignsOrder()
        {
            var dialog = new OverkillViewModel(Request());

            Assert.Equal("0.000001", dialog.ToleranceText);
            Assert.Equal(
                new[]
                {
                    "Color",
                    "Layer",
                    "Linetype",
                    "Linetype scale",
                    "Lineweight",
                    "Material",
                    "Plot style",
                    "Thickness",
                    "Transparency",
                },
                dialog.Ignored.Select(i => i.Name)
            );
            Assert.All(dialog.Ignored, i => Assert.False(i.IsChecked));
            Assert.True(dialog.OptimizePolylines);
            Assert.False(dialog.IgnorePolylineWidths);
            Assert.False(dialog.DoNotBreakPolylines);
            Assert.True(dialog.CombinePartialOverlaps);
            Assert.True(dialog.CombineEndToEnd);
            Assert.True(dialog.MaintainAssociativity);
            Assert.Equal("3 objects selected", dialog.SelectedText);
            Assert.Equal("1 object selected", new OverkillViewModel(Request(1)).SelectedText);
        }

        [Fact]
        public void TheBoxesOfSettingsKept_AreChecked_AndOkHandsBackWhatTheyHold()
        {
            var settings = new CadOverkillSettings
            {
                Tolerance = 0.25,
                Ignore = CadOverkillIgnore.Layer | CadOverkillIgnore.Transparency,
                CombineEndToEnd = false,
            };
            CadOverkillRequest request = Request(5, settings);
            var dialog = new OverkillViewModel(request);

            Assert.Equal("0.25", dialog.ToleranceText);
            Assert.Equal(new[] { "Layer", "Transparency" }, dialog.Ignored.Where(i => i.IsChecked).Select(i => i.Name));
            Assert.False(dialog.CombineEndToEnd);

            dialog.Ignored.Single(i => i.Name == "Color").IsChecked = true;
            dialog.Ignored.Single(i => i.Name == "Layer").IsChecked = false;
            dialog.ToleranceText = "0,5";
            dialog.DoNotBreakPolylines = true;
            dialog.IgnorePolylineWidths = true;
            dialog.CombinePartialOverlaps = false;
            dialog.CombineEndToEnd = true;
            dialog.MaintainAssociativity = false;
            dialog.Commit();

            Assert.Equal(CadDialogAction.Ok, request.Action);
            Assert.Equal(0.5, settings.Tolerance);
            Assert.Equal(CadOverkillIgnore.Color | CadOverkillIgnore.Transparency, settings.Ignore);
            Assert.True(settings.DoNotBreakPolylines);
            Assert.True(settings.IgnorePolylineWidths);
            Assert.False(settings.CombinePartialOverlaps);
            Assert.True(settings.CombineEndToEnd);
            Assert.False(settings.MaintainAssociativity);
            Assert.True(settings.OptimizePolylines);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("-0.1")]
        [InlineData("NaN")]
        public void ATolerance_ThatIsNoNumberOfNoughtOrMore_IsToldAtTheFoot_AndOkWillNotTakeIt(string text)
        {
            CadOverkillRequest request = Request();
            var dialog = new OverkillViewModel(request) { ToleranceText = text };

            Assert.False(dialog.IsValid);
            Assert.Equal("The tolerance must be a number, nought or more.", dialog.Message);
            Assert.Throws<InvalidOperationException>(() => dialog.Commit());

            dialog.ToleranceText = "0";
            Assert.True(dialog.IsValid);
            Assert.Equal(string.Empty, dialog.Message);
        }

        [Fact]
        public void TheTwoOptionsOfOptimizingPolylines_CanBeSetOnlyWhileItIsOn()
        {
            var dialog = new OverkillViewModel(Request());
            var raised = new List<string>();
            dialog.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

            Assert.True(dialog.CanEditPolylineOptions);
            dialog.OptimizePolylines = false;

            Assert.False(dialog.CanEditPolylineOptions);
            Assert.Contains(nameof(OverkillViewModel.CanEditPolylineOptions), raised);
        }

        [Fact]
        public void TheOverkillCommand_ShowsTheDialog_AndOkRunsItOverTheSelection_KeepingTheSettings()
        {
            MainViewModel model = TwoLines(out DxfLine first, out DxfLine second);
            OverkillViewModel shown = null;
            model.ShowOverkill = dialog =>
            {
                shown = dialog;
                dialog.Ignored.Single(i => i.Name == "Color").IsChecked = true;
                return true;
            };

            model.StartCommand("OVERKILL");
            model.Session.FeedSelection(new[] { first.Handle, second.Handle });
            model.Enter();

            Assert.NotNull(shown);
            Assert.Equal("2 objects selected", shown.SelectedText);
            Assert.True(first.IsErased);
            Assert.False(second.IsErased);
            Assert.Contains("1 duplicate(s) deleted", model.Session.History);
            Assert.Equal(CadOverkillIgnore.Color, model.Session.Overkill.Ignore);
        }

        [Fact]
        public void CancelOrAToleranceNotANumber_ChangesNothing()
        {
            MainViewModel model = TwoLines(out DxfLine first, out DxfLine second);
            model.ShowOverkill = dialog =>
            {
                dialog.Ignored.Single(i => i.Name == "Color").IsChecked = true;
                return false;
            };

            model.StartCommand("OVERKILL");
            model.Session.FeedSelection(new[] { first.Handle, second.Handle });
            model.Enter();

            Assert.False(first.IsErased);
            Assert.False(second.IsErased);
            Assert.Equal(CadOverkillIgnore.None, model.Session.Overkill.Ignore);

            model.ShowOverkill = dialog =>
            {
                dialog.ToleranceText = "x";
                return true;
            };
            model.StartCommand("OVERKILL");
            model.Session.FeedSelection(new[] { first.Handle, second.Handle });
            model.Enter();

            Assert.False(first.IsErased);
            Assert.Equal(1E-6, model.Session.Overkill.Tolerance);
        }

        [Fact]
        public void WithNoDialogShown_OverkillAsksItsOptionsOnTheCommandLine()
        {
            MainViewModel model = TwoLines(out DxfLine first, out DxfLine second);

            model.StartCommand("OVERKILL");
            model.Session.FeedSelection(new[] { first.Handle, second.Handle });
            model.Enter();

            Assert.StartsWith("Enter an option to change", model.Session.PromptText);
        }

        [Fact]
        public void TheWindow_IsLaidOutAsTheDesignDrawsIt()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                var window = new OverkillWindow(new OverkillViewModel(Request(12)));
                Assert.Equal(520.0, window.Width);
                Assert.Equal("Delete Duplicate Objects", window.Title);
                FrameworkElement body = window.Body;
                object context = window.DataContext;
                window.Content = null;
                body.DataContext = context;
                body.Measure(new Size(520, 800));
                body.Arrange(new Rect(0, 0, 520, body.DesiredSize.Height));
                body.UpdateLayout();

                List<string> boxes = Descendants(body).OfType<CheckBox>().Select(c => c.Content as string).ToList();
                Assert.Equal(
                    new[]
                    {
                        "Color",
                        "Layer",
                        "Linetype",
                        "Linetype scale",
                        "Lineweight",
                        "Material",
                        "Plot style",
                        "Thickness",
                        "Transparency",
                        "Optimize segments within polylines",
                        "Ignore polyline segment widths",
                        "Do not break polylines",
                        "Combine co-linear objects that partially overlap",
                        "Combine co-linear objects when aligned end to end",
                        "Maintain associative objects",
                    },
                    boxes
                );
                Assert.Contains(Descendants(body).OfType<TextBlock>(), t => t.Text == "12 objects selected");
                Assert.Contains(Descendants(body).OfType<TextBlock>(), t => t.Text == "Tolerance:");
                Assert.Equal(
                    new[] { "Cancel", "OK" },
                    Descendants(body)
                        .OfType<Button>()
                        .Select(b => b.Content as string)
                        .Where(c => c != null)
                        .OrderBy(c => c)
                );
                window.Close();
            });
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
