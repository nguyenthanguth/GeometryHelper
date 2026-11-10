using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Layer States Manager, which LAYERSTATE shows: the states, the restore options, the buttons that change them
    /// and Layer properties to restore. New… and Rename ask a name with <see cref="StyleNameWindow"/>, Import… and Export…
    /// a .las file. Its logic is <see cref="LayerStatesViewModel"/>.
    /// </summary>
    public partial class LayerStatesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public LayerStatesWindow(LayerStatesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskName = (title, offered) => StyleNameWindow.Ask(this, new StyleNameViewModel(title, offered, n => false));
            model.AskImportFile = () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Layer states (*.las)|*.las|All files (*.*)|*.*", Title = "Import layer state" };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
            model.AskExportFile = offered =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Layer states (*.las)|*.las", Title = "Export layer state", FileName = offered + ".las" };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
        }

        /// <summary>Gets the view model.</summary>
        public LayerStatesViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, LayerStatesViewModel model)
        {
            new LayerStatesWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnNew(object sender, RoutedEventArgs e) => Model.New();

        private void OnUpdate(object sender, RoutedEventArgs e) => Model.Update();

        private void OnEdit(object sender, RoutedEventArgs e) => Model.Edit();

        private void OnRename(object sender, RoutedEventArgs e) => Model.Rename();

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnImport(object sender, RoutedEventArgs e) => Model.Import();

        private void OnExport(object sender, RoutedEventArgs e) => Model.Export();

        private void OnSelectAll(object sender, RoutedEventArgs e) => Model.SetAll(true);

        private void OnClearAll(object sender, RoutedEventArgs e) => Model.SetAll(false);

        private void OnFold(object sender, RoutedEventArgs e) => Model.ShowProperties = !Model.ShowProperties;

        private void OnRestore(object sender, RoutedEventArgs e)
        {
            Model.Restore();
            DialogResult = true;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Model.Close();
            DialogResult = false;
        }
    }
}
