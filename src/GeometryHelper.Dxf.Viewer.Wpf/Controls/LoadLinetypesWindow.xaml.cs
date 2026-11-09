using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Load or Reload Linetypes dialog: the .lin file and its linetypes, several chosen at once by their boxes, File...
    /// to read another. Its logic is <see cref="LoadLinetypesViewModel"/>.
    /// </summary>
    public partial class LoadLinetypesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; its <see cref="LoadLinetypesViewModel.Chosen"/> are the linetypes chosen once OK is pressed.</param>
        public LoadLinetypesWindow(LoadLinetypesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public LoadLinetypesViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Pick(Window owner, LoadLinetypesViewModel model) => new LoadLinetypesWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>File...: another .lin file, read at once.</summary>
        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Linetype files (*.lin)|*.lin|All files (*.*)|*.*", Title = "Select Linetype File" };

            if (dialog.ShowDialog(this) == true)
            {
                Model.File = dialog.FileName;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
