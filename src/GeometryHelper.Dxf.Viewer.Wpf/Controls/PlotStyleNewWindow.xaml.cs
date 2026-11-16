using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Add Plot Style Table, as one dialog: the name of the file and what to start from. OK makes the file
    /// (<see cref="PlotStyleNewViewModel.TryCreate"/>) and closes only where that worked.
    /// </summary>
    public partial class PlotStyleNewWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public PlotStyleNewWindow(PlotStyleNewViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public PlotStyleNewViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether a table was made.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK made the file.</returns>
        public static bool Show(Window owner, PlotStyleNewViewModel model) =>
            new PlotStyleNewWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.TryCreate())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
