using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Page Setup Manager, which PAGESETUP opens: the named page setups of the drawing and the one the space is set
    /// to, with Set Current, New…, Modify… and Import…. Its logic is <see cref="PageSetupManagerViewModel"/>.
    /// </summary>
    public partial class PageSetupManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public PageSetupManagerWindow(PageSetupManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;

            // Its dialogs open over this one, as the Custom Image Size of the Plot dialog does, unless a host gave its own.
            if (model.ShowName == null)
            {
                model.ShowName = asked => PageSetupNameWindow.Show(this, asked);
            }

            if (model.ShowPageSetup == null)
            {
                model.ShowPageSetup = dialog => PlotWindow.Show(this, dialog);
            }

            if (model.ShowImport == null)
            {
                model.ShowImport = chosen => ImportPageSetupsWindow.Show(this, chosen);
            }

            if (model.AskImportFile == null)
            {
                model.AskImportFile = AskDrawing;
            }
        }

        /// <summary>Select File, where Import… asks the drawing the page setups are copied from; null where it is cancelled.</summary>
        private string AskDrawing()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File",
                Filter = "Drawings (*.dwg;*.dxf;*.dwt)|*.dwg;*.dxf;*.dwt|All files (*.*)|*.*",
            };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        /// <summary>Gets the view model.</summary>
        public PageSetupManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the manager over a window, which is waited on until it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, PageSetupManagerViewModel model) =>
            new PageSetupManagerWindow(model) { Owner = owner }.ShowDialog();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnSetCurrent(object sender, RoutedEventArgs e) => Model.SetCurrent();

        private void OnNew(object sender, RoutedEventArgs e) => Model.New();

        private void OnModify(object sender, RoutedEventArgs e) => Model.Modify();

        private void OnImport(object sender, RoutedEventArgs e) => Model.Import();
    }
}
