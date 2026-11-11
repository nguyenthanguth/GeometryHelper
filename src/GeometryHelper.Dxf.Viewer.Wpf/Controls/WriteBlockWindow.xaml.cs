using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Write Block dialog, which WBLOCK (W) shows: the source, base point, objects and the file written. Pick point
    /// and Select objects close it for the command line to ask, and WBLOCK shows it again. Its logic is
    /// <see cref="WriteBlockViewModel"/>.
    /// </summary>
    public partial class WriteBlockWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public WriteBlockWindow(WriteBlockViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public WriteBlockViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, WriteBlockViewModel model)
        {
            new WriteBlockWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            // FILEDIA 0: the dialog closes for WBLOCK to ask the file on the command line, and opens again with it.
            if (Model.BrowsesOnCommandLine)
            {
                CloseWith(CadDialogAction.Browse);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = CadExportJob.Dwg + "|*.dwg|DXF (*.dxf)|*.dxf",
                FilterIndex = (Model.FileName ?? string.Empty).EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 2,
                Title = "Browse for Drawing File",
                FileName = Path.GetFileName(Model.FileName ?? string.Empty),
                InitialDirectory = Path.GetDirectoryName(Model.FileName ?? string.Empty),
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.FileName = dialog.FileName;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (
                File.Exists(Model.FileName)
                && MessageBox.Show(
                    this,
                    Model.FileName + " already exists. Do you want to replace it?",
                    "Write Block",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                ) != MessageBoxResult.Yes
            )
            {
                return;
            }

            CloseWith(CadDialogAction.Ok);
        }

        private void OnPickPoint(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.PickPoint);

        private void OnSelectObjects(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.SelectObjects);

        private void OnCancel(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Cancel);

        private void CloseWith(CadDialogAction action)
        {
            Model.Close(action);
            DialogResult = action != CadDialogAction.Cancel;
        }
    }
}
