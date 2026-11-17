using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Data Link Manager of DATALINK and the Select a Data Link dialog of Link Cell, dark as the window's other dialogs. Its
    /// logic is <see cref="DataLinkManagerViewModel"/>; New and Edit open <see cref="ExcelDataLinkWindow"/>, Rename asks the
    /// new name, Delete asks to be sure.
    /// </summary>
    public partial class DataLinkManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model, giving it the windows of its buttons.</summary>
        /// <param name="model">The view model.</param>
        public DataLinkManagerWindow(DataLinkManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.ShowExcelLink = dialog => ExcelDataLinkWindow.Show(this, dialog);
            model.Confirm = question =>
                MessageBox.Show(this, question, "Data Link Manager", MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes;
            model.AskName = present => DataLinkNameWindow.Ask(this, present);
            model.CloseRequested += (s, ok) =>
            {
                if (IsLoaded)
                {
                    DialogResult = ok;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public DataLinkManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True where OK was pressed in a manager that picks.</returns>
        public static bool Show(Window owner, DataLinkManagerViewModel model) =>
            new DataLinkManagerWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Model.IsPicking)
            {
                Model.Choose();
            }
            else
            {
                Model.Edit();
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Model.CancelCommand.Execute(null);
    }
}
