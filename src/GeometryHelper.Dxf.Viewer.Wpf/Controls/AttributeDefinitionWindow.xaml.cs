using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Attribute Definition dialog, which ATTDEF (ATT) shows: the modes, the attribute, the insertion point and the
    /// text settings; the height and rotation picked on the canvas close it for the command line to ask, and ATTDEF shows it
    /// again. Its logic is <see cref="AttributeDefinitionViewModel"/>.
    /// </summary>
    public partial class AttributeDefinitionWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public AttributeDefinitionWindow(AttributeDefinitionViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public AttributeDefinitionViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, AttributeDefinitionViewModel model)
        {
            new AttributeDefinitionWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Ok);

        private void OnPickHeight(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.PickHeight);

        private void OnPickRotation(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.PickRotation);

        private void OnCancel(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Cancel);

        private void CloseWith(CadDialogAction action)
        {
            Model.Close(action);
            DialogResult = action != CadDialogAction.Cancel;
        }
    }
}
