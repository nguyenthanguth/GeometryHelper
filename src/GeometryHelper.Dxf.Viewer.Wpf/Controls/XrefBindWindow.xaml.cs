using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Bind Xrefs/DGN Underlays dialog, which the External References palette's Bind… shows: Bind Type, Bind or
    /// Insert, and the drawings to bind; OK binds them, Cancel, Esc and the close button change nothing. Its logic is
    /// <see cref="XrefBindViewModel"/>.
    /// </summary>
    public partial class XrefBindWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public XrefBindWindow(XrefBindViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public XrefBindViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the view model.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, XrefBindViewModel model)
        {
            var dialog = new XrefBindWindow(model);

            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
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
