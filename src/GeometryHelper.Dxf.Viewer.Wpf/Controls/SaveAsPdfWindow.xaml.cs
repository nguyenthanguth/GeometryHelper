using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's Save As PDF, from EXPORTPDF; its logic is <see cref="SaveAsPdfViewModel"/>.</summary>
    public partial class SaveAsPdfWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public SaveAsPdfWindow(SaveAsPdfViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public SaveAsPdfViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether Save was pressed with a file named.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when Save was pressed.</returns>
        public static bool Show(Window owner, SaveAsPdfViewModel model) =>
            new SaveAsPdfWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e) => Model.Browse();

        private void OnPlotStamp(object sender, RoutedEventArgs e) => Model.PlotStampSettings();

        private void OnOverride(object sender, RoutedEventArgs e) => Model.OverridePageSetup();

        private void OnOptions(object sender, RoutedEventArgs e) => Model.EditOptions();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Accept())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
