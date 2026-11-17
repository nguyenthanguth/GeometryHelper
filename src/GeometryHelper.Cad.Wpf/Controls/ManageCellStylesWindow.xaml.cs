using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's Manage Cell Styles dialog (the Table Cell tab's Manage Cell Styles), dark as the window's other dialogs. Its logic
    /// is <see cref="ManageCellStylesViewModel"/>.
    /// </summary>
    public partial class ManageCellStylesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public ManageCellStylesWindow(ManageCellStylesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.CloseRequested += (s, ok) =>
            {
                if (IsLoaded)
                {
                    DialogResult = ok;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public ManageCellStylesViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the dialog answered the request.</returns>
        public static bool Show(Window owner, ManageCellStylesViewModel model)
        {
            new ManageCellStylesWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }
    }
}
