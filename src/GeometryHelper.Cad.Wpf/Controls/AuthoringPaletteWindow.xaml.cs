using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Block Authoring Palettes of BAUTHORPALETTE, as a small window that floats over the drawing while the Block Editor
    /// works: a Parameters tab and an Actions tab, each a column of tools that start BPARAMETER or BACTION with the kind
    /// answered. Its logic is <see cref="AuthoringPaletteViewModel"/>. It stays open while the commands it starts run.
    /// </summary>
    public partial class AuthoringPaletteWindow : Window
    {
        private static AuthoringPaletteWindow _open;

        /// <summary>Makes the palettes over their view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public AuthoringPaletteWindow(AuthoringPaletteViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public AuthoringPaletteViewModel Model { get; }

        /// <summary>Gets the palettes' content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the palettes beside a window, or closes them, given no model; shown already they are brought forward. They are not
        /// modal: the Block Editor goes on while they stay.
        /// </summary>
        /// <param name="owner">The window they float over; null for none.</param>
        /// <param name="model">The view model; null to close the palettes.</param>
        public static void Toggle(Window owner, AuthoringPaletteViewModel model)
        {
            if (model == null)
            {
                _open?.Close();
                return;
            }

            if (_open != null)
            {
                _open.Activate();
                return;
            }

            var palette = new AuthoringPaletteWindow(model);

            if (owner != null && owner.IsVisible)
            {
                palette.Owner = owner;
                palette.Left = owner.Left + 40.0;
                palette.Top = owner.Top + 200.0;
            }
            else
            {
                palette.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            palette.Closed += (sender, e) => _open = null;
            _open = palette;
            palette.Show();
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (ParametersList == null || ActionsList == null)
            {
                return;
            }

            bool parameters = ParametersTab.IsChecked == true;
            ParametersList.Visibility = parameters ? Visibility.Visible : Visibility.Collapsed;
            ActionsList.Visibility = parameters ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
