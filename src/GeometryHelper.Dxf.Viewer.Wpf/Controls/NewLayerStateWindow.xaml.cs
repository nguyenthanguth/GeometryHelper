using System;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's New Layer State to Save: the name and an optional description. Its logic is <see cref="NewLayerStateViewModel"/>.</summary>
    public partial class NewLayerStateWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public NewLayerStateWindow(NewLayerStateViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) => NameBox.Focus();
        }

        /// <summary>Gets the view model.</summary>
        public NewLayerStateViewModel Model { get; }

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Ask(Window owner, NewLayerStateViewModel model) =>
            new NewLayerStateWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = Model.CanOk;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
