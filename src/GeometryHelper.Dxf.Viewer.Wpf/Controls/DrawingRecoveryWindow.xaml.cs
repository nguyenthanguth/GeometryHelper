using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Drawing Recovery Manager palette, which DRAWINGRECOVERY shows, as a window that stays beside the drawing
    /// until closed; its logic is <see cref="DrawingRecoveryViewModel"/>.
    /// </summary>
    public partial class DrawingRecoveryWindow : Window
    {
        /// <summary>Makes the palette over its view model.</summary>
        /// <param name="model">The view model.</param>
        public DrawingRecoveryWindow(DrawingRecoveryViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public DrawingRecoveryViewModel Model { get; }

        /// <summary>Gets the palette's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the palette over a window, not waiting for it to close.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, DrawingRecoveryViewModel model) =>
            new DrawingRecoveryWindow(model) { Owner = owner }.Show();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOpen(object sender, RoutedEventArgs e) => Model.Open();

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
