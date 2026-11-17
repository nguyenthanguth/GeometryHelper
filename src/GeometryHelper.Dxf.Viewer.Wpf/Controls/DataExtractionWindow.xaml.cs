using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>Shows a page of the Data Extraction wizard while the wizard is at it: the parameter is the page's number from nought.</summary>
    public sealed class PageShownConverter : IValueConverter
    {
        /// <summary>Whether the page is the parameter.</summary>
        /// <param name="value">The wizard's page.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">The page's number.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Visible where the wizard is at that page, Collapsed where it is not.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is DataExtractionPage page
            && string.Equals(
                ((int)page).ToString(CultureInfo.InvariantCulture),
                parameter?.ToString(),
                StringComparison.Ordinal
            )
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>Not supported.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Never.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// The Data Extraction wizard of DATAEXTRACTION, dark as the window's other dialogs: eight pages in one window, Back, Next,
    /// Finish and Cancel. Its logic is <see cref="DataExtractionViewModel"/>; Select objects closes it for the picking and the
    /// command shows it again at the same page.
    /// </summary>
    public partial class DataExtractionWindow : Window
    {
        /// <summary>Makes the wizard over its view model, giving it the file dialogs of its buttons.</summary>
        /// <param name="model">The view model.</param>
        public DataExtractionWindow(DataExtractionViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskSaveAs = offered =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Data Extraction As",
                    Filter = "Data extraction (*.dxe)|*.dxe",
                    DefaultExt = ".dxe",
                    FileName = Path.GetFileName(offered ?? string.Empty),
                    InitialDirectory = SafeFolder(offered),
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
            model.AskOpenDxe = present =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Open Data Extraction",
                    Filter = "Data extraction (*.dxe)|*.dxe|All files (*.*)|*.*",
                    FileName = Path.GetFileName(present ?? string.Empty),
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
            model.AskDrawings = () =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Add Drawings",
                    Filter = "Drawings (*.dwg;*.dxf)|*.dwg;*.dxf|All files (*.*)|*.*",
                    Multiselect = true,
                };
                return dialog.ShowDialog(this) == true ? dialog.FileNames : Enumerable.Empty<string>();
            };
            model.AskFolder = () =>
            {
                // A folder is chosen as the open file dialog chooses it: its name typed, no file needed.
                var dialog = new OpenFileDialog
                {
                    Title = "Add Folder",
                    ValidateNames = false,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "Select this folder",
                };
                return dialog.ShowDialog(this) == true ? Path.GetDirectoryName(dialog.FileName) : null;
            };
            model.AskOutput = present =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Output File",
                    Filter = "Excel workbook (*.xlsx)|*.xlsx|Comma delimited (*.csv)|*.csv|Text (*.txt)|*.txt",
                    FileName = Path.GetFileName(present ?? string.Empty),
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
        public DataExtractionViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the wizard over a window until Finish, Cancel or Select objects.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, DataExtractionViewModel model) =>
            new DataExtractionWindow(model) { Owner = owner }.ShowDialog();

        private static string SafeFolder(string file)
        {
            try
            {
                string folder = Path.GetDirectoryName(file ?? string.Empty);
                return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? folder : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowseExisting(object sender, RoutedEventArgs e) => Model.BrowseExisting();

        private void OnAddDrawings(object sender, RoutedEventArgs e) => Model.AddDrawings();

        private void OnAddFolder(object sender, RoutedEventArgs e) => Model.AddFolder();

        private void OnRemoveSource(object sender, RoutedEventArgs e) => Model.RemoveSource();

        private void OnSelectObjects(object sender, RoutedEventArgs e)
        {
            Model.SelectObjects();
            DialogResult = false;
        }

        private void OnCheckAllTypes(object sender, RoutedEventArgs e) => Model.CheckAllTypes(true);

        private void OnUncheckAllTypes(object sender, RoutedEventArgs e) => Model.CheckAllTypes(false);

        private void OnCheckAllProperties(object sender, RoutedEventArgs e) => Model.CheckAllProperties(true);

        private void OnUncheckAllProperties(object sender, RoutedEventArgs e) => Model.CheckAllProperties(false);

        /// <summary>A click in the category list: the boxes have taken it by the time the mouse is up, and the properties are listed anew.</summary>
        private void OnCategoryClicked(object sender, MouseButtonEventArgs e) =>
            Dispatcher.BeginInvoke(
                new Action(Model.CategoriesChanged),
                System.Windows.Threading.DispatcherPriority.Background
            );

        private void OnColumnUp(object sender, RoutedEventArgs e) => Model.MoveColumn(false);

        private void OnColumnDown(object sender, RoutedEventArgs e) => Model.MoveColumn(true);

        private void OnHeaderEdited(object sender, RoutedEventArgs e) => Model.CommitColumns();

        private void OnBrowseOutput(object sender, RoutedEventArgs e) => Model.BrowseOutput();
    }
}
