using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Object Selection Filters dialog, which FILTER shows. Apply closes it with true; Add Selected Object closes it
    /// with <see cref="SelectionFiltersViewModel.IsAddingSelected"/> set, and FILTER shows it again once the object is
    /// picked. Its logic is <see cref="SelectionFiltersViewModel"/>.
    /// </summary>
    public partial class SelectionFiltersWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public SelectionFiltersWindow(SelectionFiltersViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public SelectionFiltersViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether Apply was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when Apply was pressed.</returns>
        public static bool Show(Window owner, SelectionFiltersViewModel model) =>
            new SelectionFiltersWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAddToList(object sender, RoutedEventArgs e) => Model.AddToList();

        private void OnSubstitute(object sender, RoutedEventArgs e) => Model.Substitute();

        private void OnEditItem(object sender, RoutedEventArgs e) => Model.EditItem();

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnClearList(object sender, RoutedEventArgs e) => Model.ClearList();

        private void OnSaveAs(object sender, RoutedEventArgs e) => Model.SaveAs();

        private void OnDeleteFilter(object sender, RoutedEventArgs e) => Model.DeleteCurrentFilter();

        private void OnAddSelected(object sender, RoutedEventArgs e)
        {
            Model.AddSelectedObject();
            DialogResult = false;
        }

        private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
