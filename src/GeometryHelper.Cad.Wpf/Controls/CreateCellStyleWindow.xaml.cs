using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's Create New Cell Style dialog (the Table Cell tab's Create New Cell Style and Manage Cell Styles' New), dark as the
    /// window's other dialogs. Its logic is <see cref="CreateCellStyleViewModel"/>.
    /// </summary>
    public partial class CreateCellStyleWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public CreateCellStyleWindow(CreateCellStyleViewModel model)
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
        public CreateCellStyleViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the dialog answered the request, OK or Cancel.</returns>
        public static bool Show(Window owner, CreateCellStyleViewModel model)
        {
            new CreateCellStyleWindow(model) { Owner = owner }.ShowDialog();
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
