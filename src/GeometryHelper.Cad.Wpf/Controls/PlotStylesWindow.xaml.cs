using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's Plot Styles folder as STYLESMANAGER shows it, a list of the .ctb and .stb files with Edit…, New…, Delete and
    /// Open folder; a double-click edits a table. Its logic is <see cref="PlotStylesManagerViewModel"/>.
    /// </summary>
    public partial class PlotStylesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public PlotStylesWindow(PlotStylesManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public PlotStylesManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when the dialog was closed with its Close button.</returns>
        public static bool Show(Window owner, PlotStylesManagerViewModel model) =>
            new PlotStylesWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnRowDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Model.Edit();
            }
        }

        private void OnEdit(object sender, RoutedEventArgs e) => Model.Edit();

        private void OnNew(object sender, RoutedEventArgs e) => Model.New();

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo("explorer.exe", "\"" + Model.Folder + "\"") { UseShellExecute = true }
                );
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception)
            {
                GeometryHelperLog.Warn(
                    "PlotStylesWindow: the folder " + Model.Folder + " could not be opened.",
                    exception
                );
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
