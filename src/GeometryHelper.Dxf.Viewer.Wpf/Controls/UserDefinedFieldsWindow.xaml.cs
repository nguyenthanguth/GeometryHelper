using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's User Defined Fields, from Add / Edit… of Plot Stamp; its logic is <see cref="UserDefinedFieldsViewModel"/>.</summary>
    public partial class UserDefinedFieldsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public UserDefinedFieldsWindow(UserDefinedFieldsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public UserDefinedFieldsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, UserDefinedFieldsViewModel model) =>
            new UserDefinedFieldsWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        // Add and Edit put the words of the field picked in the Field text box, all selected, to be typed over.
        private void OnAdd(object sender, RoutedEventArgs e)
        {
            Model.Add();
            OnEdit(sender, e);
        }

        private void OnEdit(object sender, RoutedEventArgs e)
        {
            FieldText.Focus();
            FieldText.SelectAll();
        }

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
