using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The panel an extension adds beside the DRAWINGS list (<see cref="CadWorkspaceExtension.SidePanelContent"/>): drawn
    /// offscreen, right of the DRAWINGS list with an edge to drag, and nothing at all different for an app with no such
    /// extension. No window is shown.
    /// </summary>
    public sealed class ExtensionSidePanelTests
    {
        private sealed class PanelExtension : CadWorkspaceExtension
        {
            public override string SidePanelHeader => "Plans";
        }

        private sealed class SecondExtension : CadWorkspaceExtension
        {
            public override string SidePanelHeader => "Second";
        }

        [Fact]
        public void WithNoExtension_NoPanelIsShown_AndTheDrawingsListAndTheCanvasStandWhereTheyAlwaysDid()
        {
            OnWorkspace(false, (model, extension, workspace) =>
            {
                Assert.Null(model.SidePanelExtension);
                Assert.False(model.IsSidePanelShown);

                // The DRAWINGS list is 288 wide: the canvas starts at 288 and the panel takes no width.
                Assert.Equal(288.0, CanvasLeft(workspace), 0.5);
                Assert.Equal(0.0, Dock(workspace).ActualWidth);
            });
        }

        [Fact]
        public void AnExtensionWithAPanel_IsShownRightOfTheDrawingsList_320Wide_WithTheCanvasPushedRightBy320()
        {
            OnWorkspace(true, (model, extension, workspace) =>
            {
                var content = (Border)extension.SidePanelContent;

                Assert.Same(extension, model.SidePanelExtension);
                Assert.True(model.IsSidePanelShown);
                Assert.Equal(320.0, Dock(workspace).ActualWidth, 0.5);
                Assert.Equal(288.0, content.TranslatePoint(new Point(0, 0), workspace).X, 0.5);
                Assert.Equal(319.0, content.ActualWidth, 0.5);

                // The DRAWINGS list of 288, then the panel, its edge drawn over its own border.
                Assert.Equal(320.0 + 288.0, CanvasLeft(workspace), 0.5);
            });
        }

        [Fact]
        public void TheFirstExtensionThatHasAPanelOwnsIt_AndOneWithoutContentOwnsNone()
        {
            // A Border is a FrameworkElement, which needs an STA thread; the thread a test is given is not one after an async test.
            OnSta(() =>
            {
                var model = new MainViewModel();
                var none = new PanelExtension();
                var first = new PanelExtension { SidePanelContent = new Border() };
                var second = new SecondExtension { SidePanelContent = new Border() };

                model.AddExtension(none);
                Assert.Null(model.SidePanelExtension);
                Assert.False(model.IsSidePanelShown);

                model.AddExtension(first);
                model.AddExtension(second);

                Assert.Same(first, model.SidePanelExtension);
            });
        }

        [Fact]
        public void SettingTheContentLate_OrHidingThePanel_ShowsAndHidesItAndTellsTheWindow()
        {
            OnSta(() =>
            {
                var model = new MainViewModel();
                var extension = new PanelExtension();
                model.AddExtension(extension);
                var raised = new List<string>();
                model.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

                extension.SidePanelContent = new Border();
                Assert.True(model.IsSidePanelShown);
                Assert.Contains(nameof(MainViewModel.IsSidePanelShown), raised);

                raised.Clear();
                extension.IsSidePanelVisible = false;
                Assert.False(model.IsSidePanelShown);
                Assert.Contains(nameof(MainViewModel.IsSidePanelShown), raised);
            });
        }

        [Fact]
        public void HidingThePanel_GivesTheRoomBack_AndShowingItAgainKeepsTheWidthTheUserDraggedTo()
        {
            OnWorkspace(true, (model, extension, workspace) =>
            {
                // What letting the edge go does: the width is kept in the settings and laid out.
                model.KeepPanelLayout(288.0, 312.0, 400.0, false, false);
                Layout(workspace);
                Assert.Equal(400.0 + 288.0, CanvasLeft(workspace), 0.5);

                extension.IsSidePanelVisible = false;
                Layout(workspace);
                Assert.Equal(288.0, CanvasLeft(workspace), 0.5);

                extension.IsSidePanelVisible = true;
                Layout(workspace);
                Assert.Equal(400.0 + 288.0, CanvasLeft(workspace), 0.5);
            });
        }

        [Fact]
        public void DrawingsDockedRight_AndPropertiesLeft_StandOnThoseSides_ThePanelStillOnTheCanvasSideOfDrawings()
        {
            OnWorkspace(true, (model, extension, workspace) =>
            {
                model.KeepPanelLayout(300.0, 250.0, 320.0, true, true);
                Layout(workspace);

                // PROPERTIES of 250 at the left, then the canvas, then the panel of 320, then DRAWINGS of 300 at the right edge.
                var content = (Border)extension.SidePanelContent;
                var files = (FrameworkElement)workspace.FindName("FileListPanel");

                Assert.Equal(250.0, CanvasLeft(workspace), 0.5);
                Assert.Equal(1600.0 - 300.0, files.TranslatePoint(new Point(0, 0), workspace).X, 0.5);
                Assert.Equal(1600.0 - 300.0 - 320.0 + 1.0, content.TranslatePoint(new Point(0, 0), workspace).X, 0.5);
            });
        }

        [Fact]
        public void ThePanelTitleIsTheExtensionsHeader()
        {
            OnWorkspace(true, (model, extension, workspace) =>
            {
                var texts = new List<string>();
                Collect(workspace, texts);

                Assert.Contains("Plans", texts);
            });
        }

        [Fact]
        public void TheStartScreen_IsStill760Wide_WhereTheCanvasIsWider_AndFitsTheCanvasWhereThePanelNarrowedIt()
        {
            double plain = 0.0;
            OnWorkspace(false, (model, extension, workspace) =>
            {
                // 1 600 less the list of 288 and Properties of 312 leaves a canvas of 1 000.
                plain = StartScreenOf(workspace).ActualWidth;
            });
            Assert.Equal(760.0, plain, 0.5);

            OnWorkspace(true, (model, extension, workspace) =>
            {
                // With the panel of 320 the canvas is 680 wide, and the start screen is no wider.
                FrameworkElement start = StartScreenOf(workspace);
                double left = start.TranslatePoint(new Point(0, 0), workspace).X;

                Assert.Equal(680.0, 1600.0 - 312.0 - CanvasLeft(workspace), 1.0);
                Assert.True(start.ActualWidth <= 680.5, "The start screen is " + start.ActualWidth + " wide.");
                Assert.True(left >= CanvasLeft(workspace) - 0.5, "The start screen begins at " + left + ".");
            });
        }

        private static FrameworkElement StartScreenOf(CadWorkspace workspace)
        {
            TextBlock name = Descendants(workspace).OfType<TextBlock>().First(t => t.Text == workspace.AppName);
            return (FrameworkElement)VisualTreeHelper.GetParent(name);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static void Collect(DependencyObject root, List<string> texts)
        {
            if (root is TextBlock text)
            {
                texts.Add(text.Text);
            }

            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                Collect(VisualTreeHelper.GetChild(root, i), texts);
            }
        }

        private static FrameworkElement Dock(CadWorkspace workspace) =>
            (FrameworkElement)workspace.FindName("SidePanelDock");

        private static double CanvasLeft(CadWorkspace workspace) =>
            workspace.DrawingCanvas.TranslatePoint(new Point(0, 0), workspace).X;

        private static void Layout(CadWorkspace workspace)
        {
            workspace.Measure(new Size(1600, 960));
            workspace.Arrange(new Rect(0, 0, 1600, 960));
            workspace.UpdateLayout();
        }

        /// <summary>Runs a check on a thread of its own that is STA, whatever thread the test runner gave the test.</summary>
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

        /// <summary>
        /// Builds the model, the extension (its panel a bordered element, if asked for) and the workspace on a thread of their
        /// own, laid out offscreen at 1 600 by 960, and runs the check on it.
        /// </summary>
        private static void OnWorkspace(bool panel, Action<MainViewModel, PanelExtension, CadWorkspace> check)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    var extension = new PanelExtension();

                    if (panel)
                    {
                        extension.SidePanelContent = new Border { Background = Brushes.Red };
                        model.AddExtension(extension);
                    }

                    var workspace = new CadWorkspace { DataContext = model };

                    try
                    {
                        Layout(workspace);
                        check(model, extension, workspace);
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
    }
}
