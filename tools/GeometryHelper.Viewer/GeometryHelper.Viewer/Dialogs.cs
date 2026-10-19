using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A small window that asks for one line of text, made in code.
    /// </summary>
    internal static class NameDialog
    {
        /// <summary>
        /// Asks for a line of text.
        /// </summary>
        /// <param name="owner">The window it opens over.</param>
        /// <param name="title">The title of the window.</param>
        /// <param name="prompt">What is asked.</param>
        /// <param name="initial">The text it starts with.</param>
        /// <returns>The text, or null where the window was cancelled.</returns>
        internal static string Ask(Window owner, string title, string prompt, string initial)
        {
            var box = new TextBox { Text = initial, Margin = new Thickness(0, 4, 0, 10), MinWidth = 280 };
            string answer = null;
            var window = new Window
            {
                Title = title,
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
            };

            var ok = new Button { Content = "OK", IsDefault = true, Padding = new Thickness(18, 3, 18, 3), Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 3, 12, 3) };
            ok.Click += (s, e) =>
            {
                answer = box.Text;
                window.DialogResult = true;
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = prompt });
            panel.Children.Add(box);
            panel.Children.Add(buttons);
            window.Content = panel;
            window.Loaded += (s, e) =>
            {
                box.Focus();
                box.SelectAll();
            };

            return window.ShowDialog() == true ? answer : null;
        }
    }

    /// <summary>
    /// The window that asks how a screenshot is to be taken, made in code: which views, how large, and what behind the scene.
    /// </summary>
    internal static class ScreenshotDialog
    {
        /// <summary>
        /// Asks how a screenshot is to be taken.
        /// </summary>
        /// <param name="owner">The window it opens over.</param>
        /// <param name="current">What was chosen last time, which the window starts with.</param>
        /// <param name="several">Whether more than one view is shown; with one there is no choice of views.</param>
        /// <returns>What was chosen, or null where the window was cancelled.</returns>
        internal static ScreenshotOptions Ask(Window owner, ScreenshotOptions current, bool several)
        {
            ScreenshotOptions chosen = null;
            var window = new Window
            {
                Title = "Screenshot",
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
            };

            var active = new RadioButton { Content = "The active view", GroupName = "views", IsChecked = !current.AllViews || !several, IsEnabled = several, Margin = new Thickness(0, 2, 0, 2) };
            var all = new RadioButton { Content = "All the views, as they stand in the window", GroupName = "views", IsChecked = current.AllViews && several, IsEnabled = several, Margin = new Thickness(0, 2, 0, 2) };
            var scale = new ComboBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2) };
            scale.Items.Add("1x");
            scale.Items.Add("2x");
            scale.Items.Add("4x");
            scale.SelectedIndex = current.Scale == 4 ? 2 : current.Scale == 2 ? 1 : 0;
            var white = new RadioButton { Content = "White", GroupName = "background", IsChecked = current.Background == ScreenshotBackground.White, Margin = new Thickness(0, 2, 0, 2) };
            var transparent = new RadioButton { Content = "Transparent", GroupName = "background", IsChecked = current.Background == ScreenshotBackground.Transparent, Margin = new Thickness(0, 2, 0, 2) };
            var normal = new RadioButton { Content = "As in the window", GroupName = "background", IsChecked = current.Background == ScreenshotBackground.Window, Margin = new Thickness(0, 2, 0, 2) };
            var ok = new Button { Content = "Save as...", IsDefault = true, Padding = new Thickness(18, 3, 18, 3), Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 3, 12, 3) };
            ok.Click += (s, e) =>
            {
                chosen = new ScreenshotOptions
                {
                    AllViews = all.IsChecked == true,
                    Scale = scale.SelectedIndex == 2 ? 4 : scale.SelectedIndex == 1 ? 2 : 1,
                    Background = transparent.IsChecked == true ? ScreenshotBackground.Transparent : normal.IsChecked == true ? ScreenshotBackground.Window : ScreenshotBackground.White,
                };
                window.DialogResult = true;
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var panel = new StackPanel { Margin = new Thickness(14), MinWidth = 300 };
            panel.Children.Add(new TextBlock { Text = "Of", FontWeight = FontWeights.SemiBold });
            panel.Children.Add(active);
            panel.Children.Add(all);
            panel.Children.Add(new TextBlock { Text = "Size, as many times the size of the window", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
            panel.Children.Add(scale);
            panel.Children.Add(new TextBlock { Text = "Behind the scene", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
            panel.Children.Add(white);
            panel.Children.Add(transparent);
            panel.Children.Add(normal);
            panel.Children.Add(buttons);
            window.Content = panel;
            window.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    window.DialogResult = false;
                }
            };

            return window.ShowDialog() == true ? chosen : null;
        }
    }
}
