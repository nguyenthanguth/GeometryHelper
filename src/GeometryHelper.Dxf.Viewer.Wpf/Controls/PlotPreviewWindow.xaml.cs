using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's plot preview, which PREVIEW and the Plot dialog's Preview… show over the whole window: the toolbar of Plot,
    /// Pan, Zoom realtime, Zoom window, Zoom original and Close Preview, the sheet, and the shortcut menu of the right button.
    /// ESC or ENTER leaves it. Its logic is <see cref="PlotPreviewViewModel"/>.
    /// </summary>
    public partial class PlotPreviewWindow : Window
    {
        /// <summary>Makes the preview over its view model.</summary>
        /// <param name="model">The view model.</param>
        public PlotPreviewWindow(PlotPreviewViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Sheet.Model = model;
            model.PropertyChanged += (sender, e) => Sheet.InvalidateVisual();
        }

        /// <summary>Gets the view model.</summary>
        public PlotPreviewViewModel Model { get; }

        /// <summary>Gets the preview's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the preview over a window, covering it, and says whether Plot was chosen.</summary>
        /// <param name="owner">The window it covers; null to be shown at its own size.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when Plot was chosen.</returns>
        public static bool Show(Window owner, PlotPreviewViewModel model)
        {
            var window = new PlotPreviewWindow(model) { Owner = owner };

            if (owner != null && owner.WindowState != WindowState.Minimized)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                Rect bounds = owner.WindowState == WindowState.Maximized ? SystemParameters.WorkArea : new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);
                window.Left = bounds.Left;
                window.Top = bounds.Top;
                window.Width = bounds.Width;
                window.Height = bounds.Height;
            }

            window.ShowDialog();
            return model.PlotChosen;
        }

        /// <inheritdoc/>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                e.Handled = true;
                Close();
            }
        }

        private void OnPlot(object sender, RoutedEventArgs e)
        {
            Model.PlotChosen = true;
            Close();
        }

        private void OnExit(object sender, RoutedEventArgs e) => Close();

        private void OnZoomOriginal(object sender, RoutedEventArgs e) => Sheet.ZoomOriginal();
    }
}
