using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Select Color dialog: the Index Color tab with the 255 colours of the index, and ByLayer and ByBlock where
    /// they may be chosen; the True Color tab with red, green and blue, hue, saturation and luminance, and the hexadecimal;
    /// the old colour beside the new one, OK and Cancel. Its logic is <see cref="SelectColorViewModel"/>.
    /// </summary>
    public partial class SelectColorWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; its <see cref="SelectColorViewModel.Color"/> is the colour chosen once OK is pressed.</param>
        public SelectColorWindow(SelectColorViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public SelectColorViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed; the colour is then the model's.</returns>
        public static bool Pick(Window owner, SelectColorViewModel model) => new SelectColorWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
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
            // A box being typed in gives its text before the colour is taken.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
