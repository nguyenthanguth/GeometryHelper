using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The character map of the MTEXT editor's Symbol menu (Other…), as AutoCAD 2021 hands it to Windows: a font, its characters by
    /// character set, Select (or a double click) to gather the one picked in Characters to copy, and Copy to put them on the
    /// clipboard. Its logic is <see cref="CharacterMapViewModel"/>.
    /// </summary>
    public partial class CharacterMapWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public CharacterMapWindow(CharacterMapViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public CharacterMapViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until Close.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, CharacterMapViewModel model) =>
            new CharacterMapWindow(model) { Owner = owner }.ShowDialog();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnCellDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Model.Selected != null && e.OriginalSource is FrameworkElement)
            {
                Model.Select();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
