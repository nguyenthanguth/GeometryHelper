using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The settings a plugin declares, as a dialog: a number box with its range, a check, a text box or a drop-down per
    /// setting, each value taken as it is set and a refusal said under its row. Its logic is <see cref="PluginSettingsViewModel"/>.
    /// </summary>
    public partial class PluginSettingsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public PluginSettingsWindow(PluginSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;

            // The rows are the plugin's settings, fixed for the dialog's life; given here, as a binding to them was found not to attach.
            RowsList.ItemsSource = model.Rows;
        }

        /// <summary>Gets the view model.</summary>
        public PluginSettingsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
