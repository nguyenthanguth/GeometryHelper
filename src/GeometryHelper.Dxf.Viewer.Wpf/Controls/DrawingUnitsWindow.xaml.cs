using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Drawing Units dialog, which UNITS shows: Length, Angle, Insertion scale, Sample Output and Lighting (greyed);
    /// Direction… opens <see cref="DirectionControlWindow"/>, whose pick closes both. Its logic is <see cref="DrawingUnitsViewModel"/>.
    /// </summary>
    public partial class DrawingUnitsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public DrawingUnitsWindow(DrawingUnitsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.ShowDirection = direction => new DirectionControlWindow(direction) { Owner = this }.ShowDialog() == true && direction.Request.Action == CadDialogAction.SelectObjects;
            Loaded += (sender, e) =>
            {
                // Back from Direction Control's pick: Direction Control again, with the angle picked.
                if (Model.Request.IsDirectionOpen && Model.Direction())
                {
                    DialogResult = true;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public DrawingUnitsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, DrawingUnitsViewModel model)
        {
            new DrawingUnitsWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnDirection(object sender, RoutedEventArgs e)
        {
            if (Model.Direction())
            {
                DialogResult = true;
            }
        }

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
