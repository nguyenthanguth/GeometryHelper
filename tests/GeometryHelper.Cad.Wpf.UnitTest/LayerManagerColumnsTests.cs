using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The columns of the Layer manager's table in a drawing of 367 layers whose names differ in length: the Name column
    /// keeps one width while the list is scrolled, the header stays over the columns of every row, and a name too long for
    /// its column is cut with an ellipsis and carries a tooltip. The windows are laid out on an STA thread and never shown.
    /// </summary>
    public class LayerManagerColumnsTests
    {
        private const int LayerCount = 367;

        private static readonly double[] Offsets = { 0, 1, 7, 40, 120, 250, 1e9 };

        // The first layers have short names and the later ones long names, so a row realized only after scrolling is wider.
        private static string LayerName(int i) =>
            "L" + i.ToString("000") + (i < 30 ? "" : "_" + new string('x', 20 + (i % 9) * 12));

        private static MainViewModel Open(TestFolder folder, bool layout)
        {
            DxfDatabase db = layout ? TestFiles.RoomWithViewport(folder, out _, out _) : TestFiles.Room(out _, out _);

            for (int i = 0; i < LayerCount; i++)
            {
                db.LayerTable.Add(new DxfLayerTableRecord { Name = LayerName(i), ColorIndex = 1 + i % 200 });
            }

            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("many.dxf", db)));

            if (layout)
            {
                model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
            }

            return model;
        }

        private static void OnWindow(bool layout, Action<LayerManagerWindow, FrameworkElement, double> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Open(folder, layout);

                        using (LayerManagerViewModel manager = model.CreateLayerManager())
                        {
                            var window = new LayerManagerWindow(manager);
                            Assert.Equal(layout, manager.HasViewportColumns);
                            FrameworkElement body = window.Body;
                            object context = window.DataContext;
                            window.Content = null;
                            body.DataContext = context;
                            double width = manager.WindowWidth;
                            Relayout(body, width);
                            test(window, body, width);
                            window.Close();
                        }
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

        private static void Relayout(FrameworkElement body, double width)
        {
            body.Measure(new Size(width, 600));
            body.Arrange(new Rect(0, 0, width, 600));
            body.UpdateLayout();
        }

        private static ListBox ListBoxOf(FrameworkElement body) =>
            Descendants(body).OfType<ListBox>().Single(l => l.Name == "Table");

        // The header grid: the one of 13 columns that is not inside the list.
        private static Grid HeadGrid(FrameworkElement body)
        {
            List<DependencyObject> inList = Descendants(ListBoxOf(body)).ToList();
            return Descendants(body).OfType<Grid>().Single(g => g.ColumnDefinitions.Count == 13 && !inList.Contains(g));
        }

        private static ScrollViewer ListScroller(FrameworkElement body) =>
            Descendants(ListBoxOf(body)).OfType<ScrollViewer>().First();

        // Where each column of a grid starts, in the body's coordinates.
        private static double[] StartsOf(Grid g, FrameworkElement body)
        {
            double x0 = g.TransformToAncestor(body).Transform(new Point(0, 0)).X;
            var starts = new double[g.ColumnDefinitions.Count];

            for (int c = 0; c < starts.Length; c++)
            {
                starts[c] = x0;
                x0 += g.ColumnDefinitions[c].ActualWidth;
            }

            return starts;
        }

        /// <summary>Where each column starts, in the body's coordinates, for the header and for every realized row.</summary>
        private static (double[] Header, List<double[]> Rows) ColumnStarts(FrameworkElement body)
        {
            var rows = new List<double[]>();

            foreach (ListBoxItem item in Descendants(ListBoxOf(body)).OfType<ListBoxItem>())
            {
                rows.Add(StartsOf(Descendants(item).OfType<Grid>().First(g => g.ColumnDefinitions.Count == 13), body));
            }

            return (StartsOf(HeadGrid(body), body), rows);
        }

        // Lays the body out at each vertical offset of the list (the last one is its end), and hands the offset to check.
        private static void Scroll(FrameworkElement body, double width, Action<double> check)
        {
            ScrollViewer scroller = ListScroller(body);
            // The list scrolls by rows: its offsets are row numbers.
            Assert.True(
                scroller.ScrollableHeight > 300,
                $"the list scrolls {scroller.ScrollableHeight} (viewport {scroller.ViewportHeight}, extent {scroller.ExtentHeight})"
            );

            foreach (double offset in Offsets)
            {
                scroller.ScrollToVerticalOffset(Math.Min(offset, scroller.ScrollableHeight));
                Relayout(body, width);
                check(scroller.VerticalOffset);
            }
        }

        private static string Describe(double[] starts) => string.Join(" ", starts.Select(h => h.ToString("0")));

        private static void AssertNameColumnsAre(FrameworkElement body, double expected, double offset)
        {
            (double[] head, List<double[]> rows) = ColumnStarts(body);
            Assert.NotEmpty(rows);
            Assert.True(
                Math.Abs(head[2] - head[1] - expected) < 0.5,
                $"at {offset}: the header's Name column is {head[2] - head[1]:0.0} wide, expected {expected:0.0}"
            );

            foreach (double[] row in rows)
            {
                Assert.True(
                    Math.Abs(row[2] - row[1] - expected) < 0.5,
                    $"at {offset}: a row's Name column is {row[2] - row[1]:0.0} wide, expected {expected:0.0}"
                );
            }
        }

        private static void AssertHeaderIsOverTheRows(FrameworkElement body, double offset)
        {
            (double[] head, List<double[]> rows) = ColumnStarts(body);
            Assert.NotEmpty(rows);

            foreach (double[] row in rows)
            {
                for (int c = 0; c < head.Length; c++)
                {
                    Assert.True(
                        Math.Abs(head[c] - row[c]) < 0.5,
                        $"at {offset}: column {c} starts at {head[c]:0.0} in the header but {row[c]:0.0} in a row "
                            + $"(header {Describe(head)}; row {Describe(row)})"
                    );
                }
            }
        }

        [Fact]
        public void TheNameColumn_KeepsOneWidthWhileTheListIsScrolled()
        {
            OnWindow(false, (window, body, width) =>
            {
                double expected = ((LayerManagerViewModel)window.DataContext).NameWidth;

                // The columns fit the dialog's 1 380: no sideways scrolling to begin with, and none after the list is scrolled.
                DependencyObject up = ListBoxOf(body);

                while (!(up is ScrollViewer))
                {
                    up = VisualTreeHelper.GetParent(up);
                }

                var sideways = (ScrollViewer)up;
                Assert.Equal(ScrollBarVisibility.Auto, sideways.HorizontalScrollBarVisibility);
                Assert.Equal(0, sideways.ScrollableWidth);
                Scroll(body, width, offset =>
                {
                    AssertNameColumnsAre(body, expected, offset);
                    Assert.Equal(0, sideways.ScrollableWidth);
                });
            });
        }

        [Fact]
        public void TheHeader_StaysOverTheColumnsOfEveryRow_WhileTheListIsScrolled()
        {
            OnWindow(false, (window, body, width) =>
                Scroll(body, width, offset => AssertHeaderIsOverTheRows(body, offset))
            );
        }

        [Fact]
        public void InALayout_TheNameColumnAndTheVpColumns_StayPutAndUnderTheirHeadsWhileTheListIsScrolled()
        {
            OnWindow(true, (window, body, width) =>
            {
                Assert.Equal(1700, width);
                double expected = ((LayerManagerViewModel)window.DataContext).NameWidth;
                Scroll(body, width, offset =>
                {
                    AssertNameColumnsAre(body, expected, offset);
                    AssertHeaderIsOverTheRows(body, offset);

                    // The seven VP heads, then the seven VP cells of a row, start at the same place.
                    StackPanel vpHead = HeadGrid(body).Children.OfType<StackPanel>().Single();
                    StackPanel vpRow = Descendants(ListBoxOf(body))
                        .OfType<StackPanel>()
                        .First(s => s.Children.OfType<Border>().Count() == 7);
                    Assert.Equal(7, vpHead.Children.Count);
                    double headX = vpHead.TransformToAncestor(body).Transform(new Point(0, 0)).X;
                    double rowX = vpRow.TransformToAncestor(body).Transform(new Point(0, 0)).X;
                    Assert.True(
                        Math.Abs(headX - rowX) < 0.5,
                        $"at {offset}: the VP heads start at {headX:0.0}, the cells at {rowX:0.0}"
                    );
                });
            });
        }

        [Fact]
        public void ANameTooLongForItsColumn_IsCutWithAnEllipsis_AndCarriesItsFullNameAsATooltip()
        {
            OnWindow(false, (window, body, width) =>
            {
                var manager = (LayerManagerViewModel)window.DataContext;
                ScrollViewer scroller = ListScroller(body);
                scroller.ScrollToVerticalOffset(scroller.ScrollableHeight);
                Relayout(body, width);
                int checkedRows = 0;

                foreach (ListBoxItem item in Descendants(ListBoxOf(body)).OfType<ListBoxItem>())
                {
                    var row = (LayerRowViewModel)item.DataContext;
                    TextBlock name = Descendants(item).OfType<TextBlock>().First(t => t.Text == row.Name);
                    Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
                    Assert.Equal(row.Name, name.ToolTip);
                    Assert.True(name.ActualWidth <= manager.NameWidth, $"{row.Name} is {name.ActualWidth} wide");
                    checkedRows++;
                }

                Assert.True(checkedRows > 5);
            });
        }

        [Fact]
        public void TheNameColumn_IsResizedByItsHeader_InTheHeaderAndEveryRow_AndStaysBetweenItsLimits()
        {
            OnWindow(false, (window, body, width) =>
            {
                var manager = (LayerManagerViewModel)window.DataContext;
                Assert.Equal(140, manager.NameWidth);
                Assert.Single(Descendants(HeadGrid(body)).OfType<Thumb>());

                manager.NameWidth = 260;
                Relayout(body, width);
                AssertNameColumnsAre(body, 260, 0);
                AssertHeaderIsOverTheRows(body, 0);

                manager.NameWidth = 5;
                Assert.Equal(LayerManagerViewModel.MinNameWidth, manager.NameWidth);
                manager.NameWidth = 5000;
                Assert.Equal(LayerManagerViewModel.MaxNameWidth, manager.NameWidth);
            });
        }

        [Fact]
        public void TheLayerFilterPropertiesPreview_KeepsItsHeaderOverItsRows_WhileTheListIsScrolled()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Open(folder, false);
                        var dialog = new LayerFilterEditViewModel(model.Document, null, null, null);
                        dialog.Rows[0].Name = "*";
                        Assert.True(dialog.Preview.Count > 300);

                        var window = new LayerFilterPropertiesWindow(dialog);
                        FrameworkElement body = window.Body;
                        object context = window.DataContext;
                        window.Content = null;
                        body.DataContext = context;
                        Relayout(body, window.Width);

                        ListBox preview = Descendants(body)
                            .OfType<ListBox>()
                            .Single(l =>
                                System.Windows.Automation.AutomationProperties.GetName(l).StartsWith("Layers the")
                            );
                        List<DependencyObject> inList = Descendants(preview).ToList();
                        Grid head = Descendants(body)
                            .OfType<Grid>()
                            .Single(g =>
                                g.ColumnDefinitions.Count == 8 && !inList.Contains(g) && g.Parent is DockPanel
                            );
                        ScrollViewer scroller = Descendants(preview).OfType<ScrollViewer>().First();
                        Assert.True(scroller.ScrollableHeight > 300);

                        foreach (double offset in new[] { 0, 1, 40, 250, 1e9 })
                        {
                            scroller.ScrollToVerticalOffset(Math.Min(offset, scroller.ScrollableHeight));
                            Relayout(body, window.Width);

                            foreach (
                                Grid row in Descendants(preview)
                                    .OfType<ListBoxItem>()
                                    .Select(i =>
                                        Descendants(i).OfType<Grid>().First(g => g.ColumnDefinitions.Count == 8)
                                    )
                            )
                            {
                                double[] heads = StartsOf(head, body);
                                double[] cells = StartsOf(row, body);

                                for (int c = 0; c < 8; c++)
                                {
                                    Assert.True(
                                        Math.Abs(heads[c] - cells[c]) < 1.5,
                                        $"at {offset}: column {c} starts at {heads[c]:0.0} in the header but {cells[c]:0.0} in a row "
                                            + $"(header {Describe(heads)}; row {Describe(cells)})"
                                    );
                                }
                            }
                        }

                        window.Close();
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
