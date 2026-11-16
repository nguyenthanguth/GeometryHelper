using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The windows of plot style tables laid out offscreen: the Plot Style Table Editor with its three tabs, Add Plot Style
    /// Table, the Plot Styles folder of STYLESMANAGER, and the Plot dialog's Plot style table box with its New… and Edit….
    /// A window that names a resource that does not exist, or binds a control to nothing, fails here and not in front of the user.
    /// </summary>
    public sealed class PlotStyleWindowTests
    {
        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject deeper in Descendants(child))
                {
                    yield return deeper;
                }
            }
        }

        /// <summary>The window's content laid out at the size of the window, as a screenshot draws it.</summary>
        private static FrameworkElement Laid(Window window, FrameworkElement body, double height)
        {
            object context = window.DataContext;
            window.Content = null;
            body.DataContext = context;
            body.Measure(new Size(window.Width, height));
            body.Arrange(
                new Rect(0, 0, window.Width, double.IsNaN(window.Height) ? body.DesiredSize.Height : window.Height)
            );
            body.UpdateLayout();
            return body;
        }

        [Fact]
        public void TheEditor_HasTheThreeTabs_ThePropertiesOfAStyleInTheFormView_AndARowOfEveryStyleInTheTableView()
        {
            OnSta(() =>
            {
                var editor = new PlotStyleEditorViewModel(
                    CadPlotStyleFolder.BuiltIn("monochrome.ctb"),
                    @"D:\Plot Styles"
                );
                var window = new PlotStyleEditorWindow(editor);
                Assert.Equal(1200.0, window.Width);
                FrameworkElement body = Laid(window, window.Body, 680);

                Assert.Equal(
                    new[] { "General", "Table View", "Form View" },
                    Descendants(body).OfType<RadioButton>().Select(r => (string)r.Content).ToArray()
                );
                Assert.Contains(Descendants(body).OfType<Button>(), b => (b.Content as string) == "Save & Close");
                Assert.Contains(Descendants(body).OfType<Button>(), b => (b.Content as string) == "Save As…");
                Assert.Contains(Descendants(body).OfType<Button>(), b => (b.Content as string) == "Cancel");
                Assert.Contains(Descendants(body).OfType<TextBlock>(), t =>
                    t.Text == "Plot Style Table Editor - monochrome.ctb"
                );
                Assert.Contains(Descendants(body).OfType<TextBlock>(), t => t.Text == "monochrome.ctb" && t.IsShown());
                Assert.Contains(Descendants(body).OfType<CheckBox>(), c =>
                    (c.Content as string) == "Apply global scale factor to non-ISO linetypes"
                );

                // Table View: a row for each of the 255 styles (the list holds them, a few of them laid out).
                editor.Tab = PlotStyleEditorViewModel.TableViewTab;
                body.UpdateLayout();
                ListBox rows = Descendants(body).OfType<ListBox>().First(l => l.IsShown());
                Assert.Equal(255, rows.Items.Count);
                Assert.Contains(Descendants(body).OfType<TextBlock>(), t => t.Text == "Line join" && t.IsShown());

                // Form View: the properties of the style chosen, in AutoCAD's order.
                editor.Tab = PlotStyleEditorViewModel.FormViewTab;
                body.UpdateLayout();
                string[] labels =
                {
                    "Name",
                    "Description",
                    "Color",
                    "Enable dithering",
                    "Convert to grayscale",
                    "Use assigned pen number",
                    "Virtual pen number",
                    "Screening",
                    "Linetype",
                    "Adaptive adjustment",
                    "Lineweight",
                    "Line End Style",
                    "Line Join Style",
                    "Fill Style",
                };
                List<string> shown = Descendants(body)
                    .OfType<TextBlock>()
                    .Where(t => t.IsShown())
                    .Select(t => t.Text)
                    .ToList();
                Assert.Equal(labels, shown.Where(t => labels.Contains(t)).Distinct().ToArray());
                Assert.DoesNotContain(Descendants(body).OfType<Button>(), b =>
                    (b.Content as string) == "Add Style" && b.IsShown()
                );
            });
        }

        [Fact]
        public void TheEditorOfANamedTable_OffersAddStyleAndDeleteStyleInTheFormView()
        {
            OnSta(() =>
            {
                var editor = new PlotStyleEditorViewModel(CadPlotStyleFolder.BuiltIn("acad.stb"), null);
                editor.Tab = PlotStyleEditorViewModel.FormViewTab;
                var window = new PlotStyleEditorWindow(editor);
                FrameworkElement body = Laid(window, window.Body, 680);

                Button add = Descendants(body).OfType<Button>().First(b => (b.Content as string) == "Add Style");
                Button delete = Descendants(body).OfType<Button>().First(b => (b.Content as string) == "Delete Style");
                Assert.True(add.IsShown());
                Assert.True(add.IsEnabled);
                Assert.False(delete.IsEnabled);
                editor.AddStyle();
                body.UpdateLayout();
                Assert.True(delete.IsEnabled);
            });
        }

        [Fact]
        public void AddPlotStyleTable_ShowsTheNameTheStartAndTheKind()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                    var window = new PlotStyleNewWindow(new PlotStyleNewViewModel(folder.FullPath, false));
                    Assert.Equal(460.0, window.Width);
                    FrameworkElement body = Laid(window, window.Body, 400);

                    Assert.Equal(
                        new[] { "Add Plot Style Table", "File name", "Start from", "Kind" },
                        Descendants(body)
                            .OfType<TextBlock>()
                            .Select(t => t.Text)
                            .Where(t =>
                                t == "Add Plot Style Table" || t == "File name" || t == "Start from" || t == "Kind"
                            )
                            .ToArray()
                    );
                    ComboBox start = Descendants(body).OfType<ComboBox>().Single();
                    Assert.Equal(1 + 6, start.Items.Count);
                    Assert.Equal("Start from scratch", start.Items[0]);
                    Assert.Contains(Descendants(body).OfType<TextBlock>(), t =>
                        t.Text == "Colour-dependent plot style table (.ctb)"
                    );
                }
            });
        }

        [Fact]
        public void ThePlotStylesFolder_ListsTheFilesWithTheirKindAndDescription_AndHasFourButtons()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                    var window = new PlotStylesWindow(new PlotStylesManagerViewModel(folder.FullPath, false));
                    Assert.Equal("Plot Styles", window.Title);
                    FrameworkElement body = Laid(window, window.Body, 480);

                    Assert.Equal(9, Descendants(body).OfType<ListBox>().Single().Items.Count);
                    Assert.Equal(
                        new[] { "Close", "Edit…", "New…", "Delete", "Open folder" },
                        Descendants(body)
                            .OfType<Button>()
                            .Select(b => b.Content as string)
                            .Where(c => c != null)
                            .ToArray()
                    );
                    Assert.Contains(Descendants(body).OfType<TextBlock>(), t =>
                        t.Text == "9 plot style tables in " + folder.FullPath
                    );
                    Assert.Contains(Descendants(body).OfType<TextBlock>(), t => t.Text == "Colour-dependent");
                }
            });
        }

        [Fact]
        public void ThePlotDialog_ShowsThePlotStyleTableListWithNewAndEdit()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    CadPlotStyleFolder.EnsureBuiltIns(folder.FullPath);
                    CadDocument document = PlotPdfTests.Plan();
                    document.PlotStyleFolder = folder.FullPath;
                    var job = new CadPlotJob(document.ModelSpace, new CadPlotSettings(), DisplayBox.Empty, null);
                    var window = new PlotWindow(new PlotDialogViewModel(document, job));
                    FrameworkElement body = Laid(window, window.Body, 760);

                    ComboBox table = Descendants(body)
                        .OfType<ComboBox>()
                        .Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Plot style table");
                    Assert.Equal(7, table.Items.Count);
                    Assert.Equal("monochrome.ctb", table.SelectedItem);
                    Button edit = Descendants(body)
                        .OfType<Button>()
                        .Single(b =>
                            System.Windows.Automation.AutomationProperties.GetName(b) == "Edit plot style table"
                        );
                    Button make = Descendants(body)
                        .OfType<Button>()
                        .Single(b =>
                            System.Windows.Automation.AutomationProperties.GetName(b) == "New plot style table"
                        );
                    Assert.Equal("Edit…", edit.Content);
                    Assert.Equal("New…", make.Content);
                    Assert.True(edit.IsEnabled);
                    Assert.True(make.IsEnabled);
                    Assert.True(File.Exists(Path.Combine(folder.FullPath, "monochrome.ctb")));
                }
            });
        }
    }
}

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>Whether an element is shown by its own visibility and its parents', as a laid-out body that is in no window has it.</summary>
    internal static class PlotStyleVisibility
    {
        public static bool IsShown(this System.Windows.DependencyObject element)
        {
            for (
                System.Windows.DependencyObject d = element;
                d != null;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d)
            )
            {
                if (d is System.Windows.UIElement ui && ui.Visibility != System.Windows.Visibility.Visible)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
