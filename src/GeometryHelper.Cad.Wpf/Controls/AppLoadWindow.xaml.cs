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
    /// Reload and Unload on top, the chips that filter by kind, and one table of the AutoLISP files and plugins with On, Name,
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
            model.AskFiles = folder => AskFiles(this, folder);
            Closed += (s, e) => Model.Dispose();
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
        private static System.Collections.Generic.IReadOnlyList<string> AskFiles(Window owner, string folder)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Load Applications",
                Filter = AppLoadViewModel.OpenFilter,
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

        private void OnRowsChosen(object sender, SelectionChangedEventArgs e) =>
            Model.ChooseRows(RowList.SelectedItems.Cast<AppLoadRow>());

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
