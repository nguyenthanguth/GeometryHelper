using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.RibbonWave4Harness;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The small follow-ups of ribbon waves 3 and 4: the title of a collapsed panel broken only between words, the Table Style box
    /// showing Standard in a drawing with no table style, the named view box going back to Unsaved View once the view moved, and
    /// Show All and Hide All of Dimensional.
    /// </summary>
    public sealed class RibbonPolishTests
    {
        #region A collapsed panel's title

        // The narrowest the strip can be told: every step of every tab is taken at it.
        private const double Narrowest = 200;

        [Fact]
        public void NoCollapsedPanelTitleOfAnyTabIsBrokenInsideAWord_AtTheNarrowestWidth()
        {
            var seen = new List<string>();
            var broken = new List<string>();

            OnEveryTab(
                (tab, strip) =>
                {
                    foreach (RibbonPanel panel in Panels(strip).Where(p => p.Size == RibbonPanelSize.Collapsed))
                    {
                        var host = (Border)panel.Template.FindName("CollapsedHost", panel);
                        TextBlock label = Descendants(host)
                            .OfType<TextBlock>()
                            .Single(t => string.Equals(t.Text, panel.Title, StringComparison.Ordinal));
                        seen.Add(tab + "/" + panel.Title);

                        // A word is broken where it stands when it is wider than the width the label is given: the widest line the
                        // label draws is then narrower than the word by a letter or more (layout rounds a width to a pixel, so a pixel is let go).
                        foreach (string word in label.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (WidthOf(label, word) > label.ActualWidth + 1.0)
                            {
                                broken.Add(
                                    tab
                                        + "/"
                                        + panel.Title
                                        + ": "
                                        + word
                                        + " is "
                                        + WidthOf(label, word).ToString("0.0")
                                        + " wide in a label "
                                        + label.ActualWidth.ToString("0.0")
                                        + " wide"
                                );
                            }
                        }
                    }
                }
            );

            // Every tab collapses some panel at that width, Annotate's Dimensions among them.
            Assert.Contains("Annotate/Dimensions", seen);
            Assert.True(seen.Count >= 20, "Only " + seen.Count + " collapsed panels were found.");
            Assert.True(broken.Count == 0, string.Join("; ", broken));
        }

        /// <summary>Home, then each of the built-in tabs, laid out in one window drawn offscreen at the narrowest strip.</summary>
        private static void OnEveryTab(Action<string, RibbonStrip> check)
        {
            Exception failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), "plan.dxf"), "plan.dxf");
                    var workspace = new CadWorkspace { DataContext = model };

                    try
                    {
                        var tabs = new (string Name, CadWorkspaceExtension Tab)[]
                        {
                            ("Home", null),
                            ("Insert", model.InsertTab),
                            ("Annotate", model.AnnotateTab),
                            ("Parametric", model.ParametricTab),
                            ("View", model.ViewTab),
                            ("Manage", model.ManageTab),
                            ("Output", model.OutputTab),
                        };

                        foreach ((string name, CadWorkspaceExtension tab) in tabs)
                        {
                            model.SelectedTab = tab;
                            object content = tab == null ? model : tab.TabContent;
                            Layout(workspace, 3200);
                            RibbonStrip strip = Descendants(workspace)
                                .OfType<RibbonStrip>()
                                .Single(s => ReferenceEquals(s.DataContext, content));
                            double around = 3200 - strip.ActualWidth;
                            Layout(workspace, Narrowest + around);
                            Pump(workspace);
                            check(name, strip);
                        }
                    }
                    finally
                    {
                        workspace.DrawingCanvas.Dispose();
                    }
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

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        #endregion

        #region The table style box

        [Fact]
        public void TheTableStyleBoxOfADrawingWithNoTableStyle_ListsStandardTicked_AsAutoCadsDoes()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), "plan.dxf"), "plan.dxf");

            Assert.Empty(CadTableStyleEditor.Styles(model.Document.Database));
            Assert.Equal("Standard", model.CurrentTableStyle);
            Assert.Equal(
                new[] { "Standard", MainViewModel.ManageTableStyles },
                model.TableStyleItems.Select(i => i.Name)
            );
            StyleBoxItemViewModel standard = model.TableStyleItems.First();
            Assert.True(standard.IsCurrent);
            Assert.False(standard.IsDialog);
            Assert.True(model.TableStyleItems.Last().IsDialog);
        }

        [Fact]
        public void TheTableStyleBoxOfADrawingWithTableStyles_ListsThemAsItDid()
        {
            var database = new DxfDatabase("AC1032");
            database.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            database.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            database.TableStyleDictionary.SetAt("Schedule", new DxfTableStyle());
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plan.dxf"), "plan.dxf");

            Assert.Equal(
                new[] { "Schedule", "Standard", MainViewModel.ManageTableStyles },
                model.TableStyleItems.Select(i => i.Name)
            );
            Assert.Equal("Standard", Assert.Single(model.TableStyleItems, i => i.IsCurrent).Name);
        }

        #endregion

        #region The named view box

        private static MainViewModel TwoViewsSaved()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), "plan.dxf"), "plan.dxf");
            model.SetCanvasSize(400, 300);

            foreach (
                string line in new[]
                {
                    "-VIEW",
                    "W",
                    "Plan A",
                    "0,0",
                    "100,80",
                    "-VIEW",
                    "W",
                    "Plan B",
                    "200,200",
                    "400,360",
                }
            )
            {
                model.Execute(line);
            }

            Assert.False(model.IsCommandRunning);
            return model;
        }

        [Fact]
        public void APanOrAZoomAfterAPick_PutsTheBoxBackOnUnsavedView_SoTheSameViewCanBePickedAgain()
        {
            MainViewModel model = TwoViewsSaved();

            model.SelectedNamedView = "Plan A";

            Assert.Equal("Plan A", model.SelectedNamedView);
            Assert.Equal(50, model.Session.View.CenterX, 3);

            // The view as the pick left it: the box keeps its pick.
            model.NotifyViewChangedByUser();
            Assert.Equal("Plan A", model.SelectedNamedView);

            // The wheel and the middle button tell the model once they have moved the view.
            model.Session.View.Pan(120, 0);
            model.NotifyViewChangedByUser();

            Assert.Equal("Unsaved View", model.SelectedNamedView);
            Assert.NotEqual(50, model.Session.View.CenterX, 3);

            model.SelectedNamedView = "Plan A";

            Assert.Equal("Plan A", model.SelectedNamedView);
            Assert.Equal(50, model.Session.View.CenterX, 3);
            Assert.Equal(40, model.Session.View.CenterY, 3);

            // A zoom of the same centre is a move too.
            model.Session.View.ZoomAt(200, 150, 2.0);
            model.NotifyViewChangedByUser();

            Assert.Equal("Unsaved View", model.SelectedNamedView);
        }

        [Fact]
        public void AZoomTypedAfterAPick_PutsTheBoxBackOnUnsavedView()
        {
            MainViewModel model = TwoViewsSaved();
            model.SelectedNamedView = "Plan B";
            Assert.Equal("Plan B", model.SelectedNamedView);

            model.Execute("ZOOM");
            model.Execute("0.5X");

            Assert.False(model.IsCommandRunning);
            Assert.Equal("Unsaved View", model.SelectedNamedView);

            model.SelectedNamedView = "Plan B";

            Assert.Equal("Plan B", model.SelectedNamedView);
            Assert.Equal(300, model.Session.View.CenterX, 3);
            Assert.Equal(280, model.Session.View.CenterY, 3);
        }

        #endregion

        #region Show All and Hide All of Dimensional

        [Fact]
        public void ShowAllAndHideAllOfDimensional_ShowAndHideTheDimensionOfADynamicConstraint_AndKeepTheSelection()
        {
            var database = new DxfDatabase("AC1027");
            var line = new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(10, 1, 0) };
            database.ModelSpace.AppendEntity(line);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plan.dxf"), "plan.dxf");
            model.StartCommand("DCHORIZONTAL");
            model.Session.Feed(new GeoPoint2(1, 0.1));
            model.Session.Feed(new GeoPoint2(9, 0.9));
            model.Session.Feed(new GeoPoint2(5, 4));
            model.Enter();
            Assert.False(model.IsCommandRunning);

            DxfEntity dimension = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfDimension>());
            Assert.True(dimension.Visible);
            var ribbon = (ParametricRibbonViewModel)model.ParametricTab.TabContent;
            model.Document.Selection.Set(new[] { line.Handle });

            ribbon.DisplayAllCommand.Execute("DCDISPLAY Hideall");

            Assert.False(model.IsCommandRunning);
            Assert.False(dimension.Visible);
            Assert.Equal(new[] { line.Handle }, model.Document.Selection.Handles.ToArray());

            ribbon.DisplayAllCommand.Execute("DCDISPLAY Showall");

            Assert.False(model.IsCommandRunning);
            Assert.True(dimension.Visible);
            Assert.Equal(new[] { line.Handle }, model.Document.Selection.Handles.ToArray());
        }

        #endregion
    }
}
