using System;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Add Scale and Edit Scale of Edit Drawing Scales: the name appearing in the scale list and the paper and
    /// drawing units. Its logic is <see cref="ScaleEditViewModel"/>.
    /// </summary>
    public partial class ScaleEditWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ScaleEditWindow(ScaleEditViewModel model)
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
        public ScaleEditViewModel Model { get; }

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Ask(Window owner, ScaleEditViewModel model) =>
            new ScaleEditWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Ok())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
