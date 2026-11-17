using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The New Excel Data Link and Modify Excel Link dialog of the Data Link Manager, dark as the window's other dialogs. Its
    /// logic is <see cref="ExcelDataLinkViewModel"/>; Browse asks for an .xlsx or .csv file.
    /// </summary>
    public partial class ExcelDataLinkWindow : Window
    {
        /// <summary>Makes the dialog over its view model, giving it the file dialog of Browse.</summary>
        /// <param name="model">The view model.</param>
        public ExcelDataLinkWindow(ExcelDataLinkViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.BrowseFile = file =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Open Excel File",
                    Filter = "Excel and comma delimited files (*.xlsx;*.csv)|*.xlsx;*.csv|All files (*.*)|*.*",
                    FileName = Path.GetFileName(file ?? string.Empty),
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
            model.CloseRequested += (s, ok) =>
            {
                if (IsLoaded)
                {
                    DialogResult = ok;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public ExcelDataLinkViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True where OK was pressed and the link made.</returns>
        public static bool Show(Window owner, ExcelDataLinkViewModel model) =>
            new ExcelDataLinkWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e) => Model.Browse();

        private void OnCancel(object sender, RoutedEventArgs e) => Model.CancelCommand.Execute(null);
    }
}
