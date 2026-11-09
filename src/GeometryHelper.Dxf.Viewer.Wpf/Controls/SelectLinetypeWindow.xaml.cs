using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Select Linetype dialog, as the Layer manager's linetype cell shows it: the linetypes loaded with their
    /// appearance, Load... for more, OK and Cancel. Its logic is <see cref="SelectLinetypeViewModel"/>.
    /// </summary>
    public partial class SelectLinetypeWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; its <see cref="SelectLinetypeViewModel.Linetype"/> is the linetype chosen once OK is pressed.</param>
        public SelectLinetypeWindow(SelectLinetypeViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickLinetypes = dialog => LoadLinetypesWindow.Pick(this, dialog);
        }

        /// <summary>Gets the view model.</summary>
        public SelectLinetypeViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed with a linetype chosen.</returns>
        public static bool Pick(Window owner, SelectLinetypeViewModel model) => new SelectLinetypeWindow(model) { Owner = owner }.ShowDialog() == true && model.Linetype != null;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnDoubleClick(object sender, MouseButtonEventArgs e) => DialogResult = Model.Linetype != null;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
