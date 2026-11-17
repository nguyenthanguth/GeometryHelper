using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's Publish dialog, which PUBLISH shows: the sheet list and where it is published to. Its logic is
    /// <see cref="PublishViewModel"/>; the window gives it the file and folder dialogs of Windows unless a host gave its own.
    /// </summary>
    public partial class PublishWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public PublishWindow(PublishViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;

            if (model.AskSheetFiles == null)
            {
                model.AskSheetFiles = () =>
                    AskFiles(this, "Select Drawings", "Drawings (*.dwg;*.dxf)|*.dwg;*.dxf|All files (*.*)|*.*", true);
            }

            if (model.AskLoadList == null)
            {
                model.AskLoadList = () =>
                {
                    var files = AskFiles(
                        this,
                        "Load List of Sheets",
                        "Drawing Set Descriptions (*.dsd)|*.dsd|All files (*.*)|*.*",
                        false
                    );
                    return files == null || files.Count == 0 ? null : files[0];
                };
            }

            if (model.AskSaveList == null)
            {
                model.AskSaveList = offered => AskSave(this, offered);
            }

            if (model.AskAppend == null)
            {
                model.AskAppend = () => AskAppend(this);
            }

            if (model.AskOutputFolder == null)
            {
                model.AskOutputFolder = folder => AskFolder(this, folder);
            }

            if (model.AskPageSetupFile == null)
            {
                model.AskPageSetupFile = () => PageSetupManagerWindow.AskDrawing(this);
            }

            if (model.ShowImportChoice == null)
            {
                model.ShowImportChoice = choice => PublishImportWindow.Show(this, choice);
            }
        }

        /// <summary>Gets the view model.</summary>
        public PublishViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether Publish was pressed with a sheet to publish.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when Publish was pressed.</returns>
        public static bool Show(Window owner, PublishViewModel model) =>
            new PublishWindow(model) { Owner = owner }.ShowDialog() == true;

        private static System.Collections.Generic.IReadOnlyList<string> AskFiles(
            Window owner,
            string title,
            string filter,
            bool many
        )
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = many,
                CheckFileExists = true,
            };
            return dialog.ShowDialog(owner) == true ? dialog.FileNames : null;
        }

        private static string AskSave(Window owner, string offered)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save List As",
                Filter = "Drawing Set Descriptions (*.dsd)|*.dsd",
                FileName = System.IO.Path.GetFileName(offered),
            };
            string folder = System.IO.Path.GetDirectoryName(offered);

            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
            {
                dialog.InitialDirectory = folder;
            }

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        private static bool? AskAppend(Window owner)
        {
            MessageBoxResult answer = MessageBox.Show(
                owner,
                "Replace the sheets in the list with those of the file?\n\nYes replaces them, No adds the file's sheets after them.",
                "Load Sheet List",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );
            return answer == MessageBoxResult.Cancel ? (bool?)null : answer == MessageBoxResult.No;
        }

        private static string AskFolder(Window owner, string folder)
        {
            // The folder is the one the file dialog is left in: neither WPF of .NET Framework nor this project has a folder dialog.
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a Folder for Generated Files",
                FileName = "Select this folder",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
            };

            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
            {
                dialog.InitialDirectory = folder;
            }

            return dialog.ShowDialog(owner) == true ? System.IO.Path.GetDirectoryName(dialog.FileName) : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>A box of a row taking the focus selects the row, as clicking beside it does.</summary>
        private void OnRowFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PublishSheetRow row)
            {
                Model.Selected = row;
            }
        }

        private void OnAddSheets(object sender, RoutedEventArgs e) => Model.AddSheets();

        private void OnRemove(object sender, RoutedEventArgs e) => Model.Remove();

        private void OnRemoveAll(object sender, RoutedEventArgs e) => Model.RemoveAll();

        private void OnMoveUp(object sender, RoutedEventArgs e) => Model.MoveUp();

        private void OnMoveDown(object sender, RoutedEventArgs e) => Model.MoveDown();

        private void OnLoadList(object sender, RoutedEventArgs e) => Model.LoadList();

        private void OnSaveList(object sender, RoutedEventArgs e) => Model.SaveList();

        private void OnBrowseFolder(object sender, RoutedEventArgs e) => Model.BrowseOutputFolder();

        private void OnPublish(object sender, RoutedEventArgs e)
        {
            if (Model.Accept())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.HelpUrl);
    }
}
