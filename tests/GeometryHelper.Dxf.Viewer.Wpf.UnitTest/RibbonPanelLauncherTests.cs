using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The launcher of a ribbon panel, the small arrow at the right of its title, as AutoCAD 2021's Paragraph panel has it:
    /// beside the title of the panel drawn full, and on the title bar of its drop-down when the panel is collapsed, where it
    /// opens the same dialog. A panel with no launcher draws none.
    /// </summary>
    public class RibbonPanelLauncherTests
    {
        [Fact]
        public void TheParagraphPanelCollapsed_DropsDownWithTheLauncherOnItsTitleBar_WhichOpensTheParagraphDialog()
        {
            OnWorkspace(2300, (model, workspace) =>
            {
                model.Execute("MTEXT");
                model.Execute("0,100");
                model.Execute("200,40");
                Layout(workspace, 2300);
                int shown = 0;
                model.ShowParagraph = dialog =>
                {
                    shown++;
                    return false;
                };

                RibbonStrip strip = Descendants(workspace)
                    .OfType<RibbonStrip>()
                    .Single(s => s.DataContext is TextEditorViewModel);
                RibbonPanel paragraph = strip.Children.OfType<RibbonPanel>().Single(p => p.Title == "Paragraph");

                // Full: one launcher, beside the title.
                Button inline = Launchers(paragraph).Single();
                Assert.Equal("Paragraph settings", AutomationProperties.GetName(inline));
                inline.Command.Execute(null);
                Assert.Equal(1, shown);

                // Collapsed: the drop-down's title bar, the pin at its left, the title and the launcher at its right. The window
                // is drawn offscreen, where the popup does not show; its title bar is read where the template builds it.
                paragraph.Size = RibbonPanelSize.Collapsed;
                paragraph.IsDropDownOpen = true;
                var popup = (Popup)paragraph.Template.FindName("PART_Popup", paragraph);
                Assert.Empty(Launchers(paragraph));

                Button dropped = Launchers(popup.Child).Single();
                Assert.Equal("Paragraph settings", AutomationProperties.GetName(dropped));
                Assert.Equal(
                    "Paragraph: tab stops, indents, alignment, paragraph spacing and line spacing.",
                    dropped.ToolTip
                );
                dropped.Command.Execute(null);
                Assert.Equal(2, shown);
                paragraph.IsDropDownOpen = false;

                // A panel with no launcher, Style, draws none, full or dropped.
                RibbonPanel style = strip.Children.OfType<RibbonPanel>().Single(p => p.Title == "Style");
                Assert.Empty(Launchers(style));
                style.Size = RibbonPanelSize.Collapsed;
                style.IsDropDownOpen = true;
                Assert.Empty(Launchers(((Popup)style.Template.FindName("PART_Popup", style)).Child));
                style.IsDropDownOpen = false;
            });
        }

        /// <summary>The launchers shown under an element: buttons named "... settings", neither they nor what holds them hidden.</summary>
        private static List<Button> Launchers(DependencyObject root) =>
            Descendants(root)
                .OfType<Button>()
                .Where(b =>
                    Shown(b, root)
                    && (AutomationProperties.GetName(b) ?? string.Empty).EndsWith(" settings", StringComparison.Ordinal)
                )
                .ToList();

        private static bool Shown(DependencyObject element, DependencyObject root)
        {
            for (DependencyObject at = element; at != null; at = VisualTreeHelper.GetParent(at))
            {
                if (at is UIElement ui && ui.Visibility != Visibility.Visible)
                {
                    return false;
                }

                if (ReferenceEquals(at, root))
                {
                    break;
                }
            }

            return true;
        }

        /// <summary>Runs a check on the window drawn offscreen at a width on a thread of its own, a room drawing open.</summary>
        private static void OnWorkspace(double width, Action<MainViewModel, CadWorkspace> check)
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(model.Open(room));
                        var workspace = new CadWorkspace { DataContext = model };

                        try
                        {
                            Layout(workspace, width);
                            check(model, workspace);
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

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
