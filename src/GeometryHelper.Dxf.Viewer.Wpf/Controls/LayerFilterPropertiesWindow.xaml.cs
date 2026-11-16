using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Layer Filter Properties: the name of a property filter, its definition as rows of cells and a preview of the
    /// layers it lets through. Its logic is <see cref="LayerFilterEditViewModel"/>; the Layer manager shows it for New Property
    /// Filter and Properties.
    /// </summary>
    public partial class LayerFilterPropertiesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public LayerFilterPropertiesWindow(LayerFilterEditViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        /// <summary>Gets the view model.</summary>
        public LayerFilterEditViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Ask(Window owner, LayerFilterEditViewModel model) =>
            new LayerFilterPropertiesWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = Model.CanOk;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
