using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Hatch Creation tab of AutoCAD as a dialog, for HATCH's seTtings and HATCHEDIT: the pattern file (built-in, or a .pat
    /// file chosen by Browse…), the type, the pattern and its preview,
    /// the angle, scale or spacing, double, ISO pen width, origin, island detection, association, and a gradient's colours. Its
    /// logic is <see cref="HatchSettingsViewModel"/>.
    /// </summary>
    public partial class HatchSettingsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; its <see cref="HatchSettingsViewModel.Settings"/> are the settings once OK is pressed.</param>
        public HatchSettingsWindow(HatchSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            model.BrowsePatternFile = PickPatternFile;
        }

        /// <summary>Gets the view model.</summary>
        public HatchSettingsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, HatchSettingsViewModel model) =>
            new HatchSettingsWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>Browse…: a .pat file; null where none was chosen.</summary>
        private string PickPatternFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Hatch pattern files (*.pat)|*.pat|All files (*.*)|*.*",
                Title = "Select Pattern File",
            };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        /// <summary>A box of the dialog: Enter takes what is typed, as leaving the box does, and does not press OK.</summary>
        private void OnBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ((TextBox)sender).GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        /// <summary>Click to set new origin: what is typed taken, the dialog closed for HATCH to ask the point.</summary>
        private void OnPickOrigin(object sender, RoutedEventArgs e)
        {
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Model.ClickToSetOrigin();
            DialogResult = false;
        }
    }
}
