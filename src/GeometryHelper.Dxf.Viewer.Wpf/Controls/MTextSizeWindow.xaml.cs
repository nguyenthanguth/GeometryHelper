using System;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The ruler's Set Mtext Width and Set Mtext Height, as AutoCAD 2021's. Its logic is <see cref="MTextSizeViewModel"/>.</summary>
    public partial class MTextSizeWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public MTextSizeWindow(MTextSizeViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) =>
            {
                ValueBox.Focus();
                ValueBox.SelectAll();
            };
        }

        /// <summary>Gets the view model.</summary>
        public MTextSizeViewModel Model { get; }

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Ask(Window owner, MTextSizeViewModel model) =>
            new MTextSizeWindow(model) { Owner = owner }.ShowDialog() == true;

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
