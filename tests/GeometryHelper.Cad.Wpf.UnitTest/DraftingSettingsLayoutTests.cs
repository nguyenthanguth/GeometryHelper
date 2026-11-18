using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The layout of Drafting Settings, measured offscreen on an STA thread: the Object Snap tab's mode check boxes begin
    /// right under their group's title with Select All and Clear All beside them, no dead space is left above the footer,
    /// and the three tabs share one window size.
    /// </summary>
    public sealed class DraftingSettingsLayoutTests
    {
        private static readonly string[] Tabs =
        {
            DraftingSettingsViewModel.SnapAndGridTab,
            DraftingSettingsViewModel.PolarTrackingTab,
            DraftingSettingsViewModel.ObjectSnapTab,
        };

        private static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);

                if (child is T match)
                {
                    yield return match;
                }

                foreach (T below in Descendants<T>(child))
                {
                    yield return below;
                }
            }
        }

        private static Rect Box(FrameworkElement element, FrameworkElement root) =>
            element
                .TransformToAncestor(root)
                .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

        /// <summary>The window's body, laid out at the window's size and shown on the tab, as the tests of the dialog do.</summary>
        private static FrameworkElement Laid(string tab, out Window window)
        {
            var model = new MainViewModel();
            window = new DraftingSettingsWindow(model.CreateDraftingSettings(tab));
            var body = (FrameworkElement)window.Content;
            window.Content = null;
            body.DataContext = window.DataContext;
            body.Measure(new Size(window.Width, window.Height));
            body.Arrange(new Rect(0, 0, window.Width, window.Height));
            body.UpdateLayout();
            return body;
        }

        private static void Png(FrameworkElement body, string name)
        {
            string folder = Environment.GetEnvironmentVariable("GH_DRAFTING_PNG_DIR");

            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(body.ActualWidth),
                (int)Math.Ceiling(body.ActualHeight),
                96,
                96,
                PixelFormats.Pbgra32
            );
            bitmap.Render(body);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream stream = File.Create(Path.Combine(folder, name + ".png")))
            {
                encoder.Save(stream);
            }
        }

        private static void OnSta(Action work)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    work();
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

        [Fact]
        public void TheObjectSnapModes_BeginRightUnderTheGroupsTitle_WithSelectAllBesideThem_AndNoDeadSpaceAboveTheFooter()
        {
            OnSta(() =>
            {
                FrameworkElement body = Laid(DraftingSettingsViewModel.ObjectSnapTab, out Window window);
                Png(body, "object-snap");

                TextBlock title = Descendants<TextBlock>(body).First(t => t.Text == "Object Snap modes");
                CheckBox first = Descendants<CheckBox>(body)
                    .First(c => c.ActualWidth > 0 && c.DataContext is SnapModeRowViewModel);
                Button selectAll = Descendants<Button>(body).First(b => Equals(b.Content, "Select All"));
                Button clearAll = Descendants<Button>(body).First(b => Equals(b.Content, "Clear All"));
                List<CheckBox> boxes = Descendants<CheckBox>(body)
                    .Where(c => c.ActualWidth > 0 && c.DataContext is SnapModeRowViewModel)
                    .ToList();

                Rect titleBox = Box(title, body);
                Rect firstBox = Box(first, body);
                Rect selectBox = Box(selectAll, body);
                Rect clearBox = Box(clearAll, body);

                Assert.Equal(14, boxes.Count);

                // No gap: the first check box starts within one row's spacing of the group title's bottom (it was 85 pixels).
                Assert.InRange(firstBox.Top - titleBox.Bottom, 0, 20);

                // Select All is at the top of the group, beside the columns and not above them.
                Assert.True(selectBox.Top <= firstBox.Top + 0.5, "Select All starts at " + selectBox.Top + ".");
                Assert.True(selectBox.Left > Box(boxes.Last(), body).Right, "Select All is right of the columns.");
                Assert.True(clearBox.Top >= selectBox.Bottom, "Clear All is under Select All.");

                // Two even columns, nine rows and five, the rows evenly spaced.
                List<double> lefts = boxes
                    .Select(c => Math.Round(Box(c, body).Left))
                    .Distinct()
                    .OrderBy(l => l)
                    .ToList();
                Assert.Equal(2, lefts.Count);
                List<double> tops = boxes
                    .Where(c => Math.Round(Box(c, body).Left) == lefts[0])
                    .Select(c => Box(c, body).Top)
                    .OrderBy(t => t)
                    .ToList();
                Assert.Equal(9, tops.Count);
                double step = tops[1] - tops[0];
                for (int i = 2; i < tops.Count; i++)
                {
                    Assert.Equal(step, tops[i] - tops[i - 1], 1);
                }

                // The content ends within 24 pixels of the footer.
                double dead = DeadSpace(body);
                Assert.True(dead <= 24, "The content ends " + dead + " above the footer.");

                window.Close();
            });
        }

        [Fact]
        public void TheThreeTabs_ShareOneWindowSize_AndEachIsDrawnToPngWhenAFolderIsGiven()
        {
            OnSta(() =>
            {
                foreach (string tab in Tabs)
                {
                    FrameworkElement body = Laid(tab, out Window window);
                    Png(body, tab.Replace(' ', '-').ToLowerInvariant());
                    Assert.Equal(window.Width, body.ActualWidth);
                    Assert.Equal(window.Height, body.ActualHeight);

                    // The tallest tab, Snap and Grid, and the Object Snap tab fill the window; none runs into the footer.
                    double dead = DeadSpace(body);
                    Assert.True(dead >= 0, tab + " runs into the footer by " + -dead + ".");
                    if (tab != DraftingSettingsViewModel.PolarTrackingTab)
                    {
                        Assert.True(dead <= 24, tab + " leaves " + dead + " above the footer.");
                    }

                    window.Close();
                }
            });
        }

        /// <summary>The pixels between the lowest thing drawn in the tab and the footer's top.</summary>
        private static double DeadSpace(FrameworkElement body)
        {
            Button ok = Descendants<Button>(body).First(b => Equals(b.Content, "OK"));
            double footerTop = Box(Footer(ok), body).Top;
            double bottom = Descendants<FrameworkElement>(body)
                .Where(e => e is TextBlock || e is CheckBox || e is Image || e is Button || IsGroupEdge(e))
                .Where(e => e.ActualWidth > 0 && e.ActualHeight > 0 && Box(e, body).Bottom <= footerTop + 0.5)
                .Max(e => Box(e, body).Bottom);
            return footerTop - bottom;
        }

        /// <summary>The bordered edge of a group: a border with a thickness of 1 and rounded corners.</summary>
        private static bool IsGroupEdge(FrameworkElement element) =>
            element is Border b && b.BorderThickness.Left == 1 && b.CornerRadius.TopLeft >= 4;

        /// <summary>The footer border holding OK: the nearest ancestor whose own Border carries the header colour.</summary>
        private static FrameworkElement Footer(DependencyObject ok)
        {
            DependencyObject at = ok;

            while (
                at != null && !(at is Border b && b.Padding.Left == 16 && b.Padding.Top == 10 && b.Child is DockPanel)
            )
            {
                at = VisualTreeHelper.GetParent(at);
            }

            return (FrameworkElement)at;
        }
    }
}
