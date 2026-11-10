using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's View Manager, which VIEW shows: the tree of views, the properties of the one chosen and its buttons; New…
    /// opens <see cref="NewViewWindow"/>. Edit Boundaries… and New View's Define window close it for the command line. Its
    /// logic is <see cref="ViewManagerViewModel"/>.
    /// </summary>
    public partial class ViewManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ViewManagerWindow(ViewManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.ShowNewView = view => new NewViewWindow(view) { Owner = this }.ShowDialog() == true && view.Request.Action == CadDialogAction.SelectObjects;
            Loaded += (sender, e) =>
            {
                // Back from New View's Define window: New View again, with the window picked.
                if (Model.Request.IsNewViewOpen && Model.New())
                {
                    DialogResult = true;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public ViewManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, ViewManagerViewModel model)
        {
            new ViewManagerWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSetCurrent(object sender, RoutedEventArgs e) => Model.SetCurrent();

        private void OnNew(object sender, RoutedEventArgs e)
        {
            if (Model.New())
            {
                DialogResult = true;
            }
        }

        private void OnUpdateLayers(object sender, RoutedEventArgs e) => Model.UpdateLayers();

        private void OnEditBoundaries(object sender, RoutedEventArgs e)
        {
            if (Model.EditBoundaries())
            {
                DialogResult = true;
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnApply(object sender, RoutedEventArgs e) => Model.Apply();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Ok())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Cancel();
            DialogResult = false;
        }
    }
}
