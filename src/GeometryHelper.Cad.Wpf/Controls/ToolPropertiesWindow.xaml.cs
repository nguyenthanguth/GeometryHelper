using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Tool Properties of the tool palettes: a tool's name and description; for a block tool the block, its drawing, scale and
    /// rotation; for a hatch tool the type, pattern, angle and scale; for a command tool its command line; and the layer,
    /// colour, linetype and lineweight the objects it makes are drawn with. Its logic is <see cref="ToolPropertiesViewModel"/>.
    /// </summary>
    public partial class ToolPropertiesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ToolPropertiesWindow(ToolPropertiesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Title = model.Title;
        }

        /// <summary>Gets the view model.</summary>
        public ToolPropertiesViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; OK writes it into the tool.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed and the tool was changed.</returns>
        public static bool Show(Window owner, ToolPropertiesViewModel model) =>
            new ToolPropertiesWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter =
                    "Drawings (*.dwg;*.dxf)|*.dwg;*.dxf|Drawing (*.dwg)|*.dwg|DXF (*.dxf)|*.dxf|All files (*.*)|*.*",
                Title = "Select source file",
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.SourceFile = dialog.FileName;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Apply())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
