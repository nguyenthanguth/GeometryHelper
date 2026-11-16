using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Field dialog of AutoCAD 2021, which FIELD and the text editor's Insert Field show: the category and the names in it
    /// on the left, the preview, the options of the field chosen and the field expression on the right. Select object closes
    /// it to ask for an object and shows it again. Its logic is <see cref="FieldDialogViewModel"/>.
    /// </summary>
    internal partial class FieldWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        internal FieldWindow(FieldDialogViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        internal FieldDialogViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        internal FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the field and the action it closed with are in its request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        internal static bool Show(Window owner, FieldDialogViewModel model)
        {
            new FieldWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnNameDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Model.CanOk)
            {
                OnOk(sender, e);
            }
        }

        private void OnSelectObject(object sender, RoutedEventArgs e)
        {
            Model.Close(CadFieldAction.SelectObject);
            DialogResult = true;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Model.Close(CadFieldAction.Ok);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Close(CadFieldAction.Cancel);
            DialogResult = false;
        }
    }
}
