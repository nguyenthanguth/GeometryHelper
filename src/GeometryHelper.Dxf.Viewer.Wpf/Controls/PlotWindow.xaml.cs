using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Plot dialog in its expanded form, which PLOT (Ctrl+P) shows, and its Page Setup form, which PAGESETUP shows.
    /// OK plots (or writes the page setup); Window&lt; closes it for the window's corners to be picked on the drawing, the
    /// command showing it again after; Preview… shows the plot preview over it, whose Plot plots. Its logic is
    /// <see cref="PlotDialogViewModel"/>.
    /// </summary>
    public partial class PlotWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; OK plots nothing itself, the caller plots or applies.</param>
        public PlotWindow(PlotDialogViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;

            // Custom… of a raster device's Paper size list: Custom Image Size over this dialog.
            model.ShowCustomSize = size => new CustomImageSizeWindow(size) { Owner = this }.ShowDialog() == true;

            // Add… of the Page setup box: Add Page Setup over this dialog.
            model.ShowPageSetupName = asked => new PageSetupNameWindow(asked) { Owner = this }.ShowDialog() == true;

            // Import… of Add Page Setup: Select File for the drawing, then Import Page Setups over this dialog.
            model.AskImportFile = () => PageSetupManagerWindow.AskDrawing(this);
            model.ShowImport = chosen => ImportPageSetupsWindow.Show(this, chosen);
        }

        /// <summary>Gets the view model.</summary>
        public PlotDialogViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed, or Plot in its preview.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK or the preview's Plot was pressed.</returns>
        public static bool Show(Window owner, PlotDialogViewModel model) =>
            new PlotWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            // A printer with Plot to file ticked is not supported: the footer says so and the dialog stays.
            if (Model.TryAccept())
            {
                DialogResult = true;
            }
        }

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.HelpUrl);

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnPlotStampSettings(object sender, RoutedEventArgs e) => Model.PlotStampSettings();

        private void OnApply(object sender, RoutedEventArgs e) => Model.ApplyToLayout();

        private void OnAdd(object sender, RoutedEventArgs e) => Model.AskAndAddPageSetup();

        private void OnPickWindow(object sender, RoutedEventArgs e)
        {
            Model.PicksWindow = true;
            DialogResult = false;
        }

        private void OnPreview(object sender, RoutedEventArgs e)
        {
            // The dialog steps aside while the preview covers the window, as AutoCAD's does, and comes back when it closes.
            Visibility = Visibility.Hidden;
            bool plot = PlotPreviewWindow.Show(Owner, Model.CreatePreview());
            Visibility = Visibility.Visible;

            if (plot && Model.TryAccept())
            {
                DialogResult = true;
            }
        }
    }
}
