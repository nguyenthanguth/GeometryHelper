using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Load/Unload Applications, from APPLOAD and from the Plugin manager button, as the design draws it: 900 by 700, Load…,
    /// Reload, Unload and Remove on top, the chips that filter by kind, and one table of the AutoLISP files, Python files and plugins with On, Name,
    /// Kind, Commands, File and At start. Its logic is <see cref="AppLoadViewModel"/>, disposed when the dialog closes; the
    /// Open dialog Load… asks for is shown here. Delete unloads the rows chosen, and files dragged from Explorer onto the
    /// table are loaded.
    /// </summary>
    public partial class AppLoadWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public AppLoadWindow(AppLoadViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PropertyChanged += OnModelChanged;
            model.AskFiles = folder => AskFiles(this, folder);
            Closed += (s, e) =>
            {
                Model.PropertyChanged -= OnModelChanged;
                Model.Dispose();
            };
        }

        /// <summary>Gets the view model.</summary>
        public AppLoadViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window, which waits until it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when the dialog was closed with its Close button.</returns>
        public static bool Show(Window owner, AppLoadViewModel model) =>
            new AppLoadWindow(model) { Owner = owner }.ShowDialog() == true;

        /// <summary>Shows the Open dialog for a plugin's .dll and loads the one chosen, as NETLOAD's window does.</summary>
        /// <param name="owner">The window the dialog belongs to.</param>
        /// <param name="load">What is done with the path chosen.</param>
        public static void PickPlugin(Window owner, Action<string> load)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Load a plugin",
                Filter = "Plugins (*.dll)|*.dll|All files (*.*)|*.*",
            };

            if (dialog.ShowDialog(owner) == true)
            {
                load(dialog.FileName);
            }
        }

        /// <summary>Load…: an Open dialog for .lsp and .dll files, several allowed, opened on the folder of the last Load.</summary>
        private static System.Collections.Generic.IReadOnlyList<string> AskFiles(Window owner, string folder) =>
            AskFiles(owner, "Load Applications", AppLoadViewModel.OpenFilter, folder);

        /// <summary>LISPLOAD: an Open dialog for AutoLISP files, several allowed, opened on the folder of the last load.</summary>
        /// <param name="owner">The window the dialog belongs to.</param>
        /// <param name="filter">The filter, in the form the Windows dialogs take.</param>
        /// <param name="folder">The folder to start in; empty for the dialog's own.</param>
        /// <returns>The paths chosen; null when the user cancelled.</returns>
        public static System.Collections.Generic.IReadOnlyList<string> PickLisp(
            Window owner,
            string filter,
            string folder
        ) => AskFiles(owner, "Load AutoLISP files", filter, folder);

        /// <summary>PYLOAD: an Open dialog for Python files, several allowed, opened on the folder of the last load.</summary>
        /// <param name="owner">The window the dialog belongs to.</param>
        /// <param name="filter">The filter, in the form the Windows dialogs take.</param>
        /// <param name="folder">The folder to start in; empty for the dialog's own.</param>
        /// <returns>The paths chosen; null when the user cancelled.</returns>
        public static System.Collections.Generic.IReadOnlyList<string> PickPython(
            Window owner,
            string filter,
            string folder
        ) => AskFiles(owner, "Load Python files", filter, folder);

        private static System.Collections.Generic.IReadOnlyList<string> AskFiles(
            Window owner,
            string title,
            string filter,
            string folder
        )
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                FilterIndex = 1,
                Multiselect = true,
            };

            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
            {
                dialog.InitialDirectory = folder;
            }

            return dialog.ShowDialog(owner) == true ? dialog.FileNames : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = true;

        private bool _showingChoice;

        private void OnRowsChosen(object sender, SelectionChangedEventArgs e)
        {
            if (_showingChoice)
            {
                return;
            }

            _showingChoice = true;

            try
            {
                Model.ChooseRows(RowList.SelectedItems.Cast<AppLoadRow>());
            }
            finally
            {
                _showingChoice = false;
            }
        }

        /// <summary>The table is made anew when a box is ticked or a file loaded; the rows the model keeps chosen are selected again.</summary>
        private void OnModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AppLoadViewModel.Chosen) || _showingChoice)
            {
                return;
            }

            _showingChoice = true;

            try
            {
                RowList.SelectedItems.Clear();

                foreach (AppLoadRow row in Model.Chosen)
                {
                    RowList.SelectedItems.Add(row);
                }
            }
            finally
            {
                _showingChoice = false;
            }
        }

        /// <summary>Delete unloads the rows chosen, as Unload does.</summary>
        private void OnRowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && Model.UnloadCommand.CanExecute(null))
            {
                Model.UnloadCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnRowsDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// Files dropped on the table are loaded in the order of the drop, but after the drop has ended, so that Explorer is not
        /// held while a question about an untrusted file is asked.
        /// </summary>
        private void OnRowsDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                Dispatcher.BeginInvoke(
                    new Action(() => Model.LoadDropped(files)),
                    System.Windows.Threading.DispatcherPriority.Background
                );
            }

            e.Handled = true;
        }
    }
}
