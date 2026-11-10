using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Custom Image Size (design PlotRasterCustom): the width and the height of an image in pixels, each 16 to 8 000, from
    /// Custom… of the Paper size list of GeometryHelper PNG or JPG. Its logic is <see cref="CustomImageSizeViewModel"/>.
    /// </summary>
    public partial class CustomImageSizeWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public CustomImageSizeWindow(CustomImageSizeViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) =>
            {
                WidthBox.Focus();
                WidthBox.SelectAll();
            };
        }

        /// <summary>Gets the view model.</summary>
        public CustomImageSizeViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.CanOk)
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
