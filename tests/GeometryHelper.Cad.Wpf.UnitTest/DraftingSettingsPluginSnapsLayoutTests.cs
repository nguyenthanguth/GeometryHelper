using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The layout of the "Plugin snaps" group of Drafting Settings' Object Snap tab, measured offscreen on an STA thread as
    /// <see cref="DraftingSettingsLayoutTests"/> measures the tab: the group sits under the fourteen rows and clear of Select All,
    /// Clear All and the footer, holds one row per snap with its box, and scrolls past five snaps without making the window
    /// taller; with no plugin snap loaded it is not drawn.
    /// </summary>
    public sealed class DraftingSettingsPluginSnapsLayoutTests : IDisposable
    {
        private readonly PluginWindowKit _kit = new PluginWindowKit();

        public void Dispose() => _kit.Dispose();

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

        /// <summary>The Object Snap tab's body, laid out at the window's size as the dialog's own layout tests lay it out.</summary>
        private static FrameworkElement Laid(MainViewModel model, out Window window)
        {
            window = new DraftingSettingsWindow(model.CreateDraftingSettings(DraftingSettingsViewModel.ObjectSnapTab));
            var body = (FrameworkElement)window.Content;
            window.Content = null;
            body.DataContext = window.DataContext;
            body.Measure(new Size(window.Width, window.Height));
            body.Arrange(new Rect(0, 0, window.Width, window.Height));
            body.UpdateLayout();
            return body;
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

        private static Rect GroupBox(FrameworkElement body)
        {
            TextBlock title = Descendants<TextBlock>(body).First(t => t.Text == "Plugin snaps" && t.ActualWidth > 0);
            DependencyObject at = title;

            // The group is the HeaderedContentControl holding its title.
            while (at != null && !(at is HeaderedContentControl))
            {
                at = VisualTreeHelper.GetParent(at);
            }

            return Box((FrameworkElement)at, body);
        }

        private static double FooterTop(FrameworkElement body)
        {
            Button ok = Descendants<Button>(body).First(b => Equals(b.Content, "OK"));
            DependencyObject at = ok;

            while (
                at != null && !(at is Border b && b.Padding.Left == 16 && b.Padding.Top == 10 && b.Child is DockPanel)
            )
            {
                at = VisualTreeHelper.GetParent(at);
            }

            return Box((FrameworkElement)at, body).Top;
        }

        [Fact]
        public void ThePluginSnapsGroup_SitsUnderTheFourteenRowsClearOfSelectAllAndTheFooter_WithOneBoxInIt()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            OnSta(() =>
            {
                FrameworkElement body = Laid(model, out Window window);

                Rect group = GroupBox(body);
                List<CheckBox> modes = Descendants<CheckBox>(body)
                    .Where(c => c.ActualWidth > 0 && c.DataContext is SnapModeRowViewModel)
                    .ToList();
                CheckBox mine = Descendants<CheckBox>(body)
                    .Single(c => c.ActualWidth > 0 && c.DataContext is PluginSnapRowViewModel);
                Button selectAll = Descendants<Button>(body).First(b => Equals(b.Content, "Select All"));
                Button clearAll = Descendants<Button>(body).First(b => Equals(b.Content, "Clear All"));

                Assert.Equal(14, modes.Count);
                Assert.True(
                    group.Top >= modes.Max(c => Box(c, body).Bottom),
                    "The group starts under the fourteen rows."
                );
                Assert.False(group.IntersectsWith(Box(selectAll, body)));
                Assert.False(group.IntersectsWith(Box(clearAll, body)));
                Assert.True(
                    group.Bottom <= FooterTop(body) + 0.5,
                    "The group ends " + group.Bottom + ", the footer starts " + FooterTop(body) + "."
                );
                Assert.True(group.Contains(Box(mine, body)), "The snap's box is inside the group.");
                Assert.True(group.Left >= 0 && group.Right <= body.ActualWidth);
                window.Close();
            });
        }

        [Fact]
        public void WithNoPluginSnap_TheGroupIsNotDrawn_AndTheWindowIsAsHighAsAfterThePluginIsUnloaded()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            OnSta(() =>
            {
                FrameworkElement plain = Laid(model, out Window plainWindow);
                Assert.DoesNotContain(Descendants<TextBlock>(plain), t =>
                    t.Text == "Plugin snaps" && t.ActualWidth > 0
                );
                double plainHeight = plainWindow.Height;
                plainWindow.Close();

                _kit.LoadSample(model);
                Laid(model, out Window withWindow);
                Assert.True(withWindow.Height > plainHeight, withWindow.Height + " is not above " + plainHeight + ".");
                withWindow.Close();

                Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);
                Laid(model, out Window afterWindow);
                Assert.Equal(plainHeight, afterWindow.Height);
                afterWindow.Close();
            });
        }

        [Fact]
        public void EightPluginSnaps_ScrollInsideTheGroup_AndTheWindowIsNoTallerThanForFive()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            // The same plugin loaded with five snaps, measured, unloaded and loaded again with eight.
            LoadWidgets(model, 5);

            OnSta(() =>
            {
                FrameworkElement fiveBody = Laid(model, out Window fiveWindow);
                double fiveHeight = fiveWindow.Height;
                Assert.Equal(5, Descendants<CheckBox>(fiveBody).Count(c => c.DataContext is PluginSnapRowViewModel));
                Assert.True(
                    Descendants<ScrollViewer>(fiveBody).All(s => s.ScrollableHeight <= 0 || s.ActualHeight <= 0),
                    "Five rows fit."
                );
                fiveWindow.Close();

                Assert.True(model.UnloadPlugin(PluginWindowKit.HookName).Succeeded);
                LoadWidgets(model, 8);
                FrameworkElement eightBody = Laid(model, out Window eightWindow);

                Assert.Equal(fiveHeight, eightWindow.Height);
                Assert.Equal(8, Descendants<CheckBox>(eightBody).Count(c => c.DataContext is PluginSnapRowViewModel));
                Assert.True(
                    Descendants<ScrollViewer>(eightBody).Any(s => s.ScrollableHeight > 0 && s.ActualHeight > 0),
                    "Eight rows do not fit the group, so it scrolls."
                );
                Assert.True(
                    GroupBox(eightBody).Bottom <= FooterTop(eightBody) + 0.5,
                    "The group runs into the footer."
                );
                eightWindow.Close();
            });
        }

        /// <summary>Loads a plugin of <paramref name="count"/> snaps WIDGETA, WIDGETB and so on, off, that give no point.</summary>
        private void LoadWidgets(MainViewModel model, int count)
        {
            _kit.LoadHook(model, host =>
            {
                for (int i = 0; i < count; i++)
                {
                    string letter = ((char)('A' + i)).ToString();
                    host.AddObjectSnap(new CadCustomSnap("Widget " + letter, "WIDGET" + letter, r => new GeoPoint2[0]));
                }
            });
        }
    }
}
