using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Parameters Manager palette (PARAMETERS), a window that stays open beside the drawing and follows the document
    /// shown. Its logic is <see cref="ParametersPaletteViewModel"/>.
    /// </summary>
    public partial class ParametersPaletteWindow : Window
    {
        private static ParametersPaletteWindow _open;

        /// <summary>Makes the window over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ParametersPaletteWindow(ParametersPaletteViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public ParametersPaletteViewModel Model { get; }

        /// <summary>Gets the window's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Gets whether the palette is open.</summary>
        public static bool IsOpen => _open != null;

        /// <summary>Shows the palette over a window, or brings the one open to the front.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void ShowPalette(Window owner, ParametersPaletteViewModel model)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }

            var window = new ParametersPaletteWindow(model) { Owner = owner };

            if (owner != null)
            {
                window.Left = owner.Left + owner.ActualWidth - window.Width - 24.0;
                window.Top = owner.Top + 120.0;
            }

            _open = window;
            window.Closed += (s, e) => _open = null;
            window.Show();
        }

        /// <summary>Closes the palette where it is open.</summary>
        public static void ClosePalette() => _open?.Close();

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
