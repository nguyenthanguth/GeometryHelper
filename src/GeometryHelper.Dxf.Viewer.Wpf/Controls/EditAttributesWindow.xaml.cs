using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Edit Attributes dialog, which INSERT shows with ATTDIA 1: the block's name, a box for each attribute's value,
    /// eight a page, Previous and Next, Cancel and OK. Its logic is <see cref="EditAttributesViewModel"/>.
    /// </summary>
    public partial class EditAttributesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public EditAttributesWindow(EditAttributesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public EditAttributesViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, EditAttributesViewModel model)
        {
            new EditAttributesWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnPrevious(object sender, RoutedEventArgs e) => Model.Page--;

        private void OnNext(object sender, RoutedEventArgs e) => Model.Page++;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Model.Close(CadDialogAction.Ok);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Close(CadDialogAction.Cancel);
            DialogResult = false;
        }
    }
}
