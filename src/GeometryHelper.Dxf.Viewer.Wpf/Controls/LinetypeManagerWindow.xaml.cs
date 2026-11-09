using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Linetype Manager: the drawing's linetypes with their appearance and description, Load..., Delete, Current and
    /// Show details, OK and Cancel. Its logic is <see cref="LinetypeManagerViewModel"/>; Load... shows
    /// <see cref="LoadLinetypesWindow"/>.
    /// </summary>
    public partial class LinetypeManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; applied by its owner once OK is pressed.</param>
        public LinetypeManagerWindow(LinetypeManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickLinetypes = dialog => LoadLinetypesWindow.Pick(this, dialog);
        }

        /// <summary>Gets the view model.</summary>
        public LinetypeManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, LinetypeManagerViewModel model) => new LinetypeManagerWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            // A scale being typed gives its text before the dialog closes.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
