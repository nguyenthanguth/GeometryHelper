using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Edit Block Definition dialog, which BEDIT (BE) shows, and the same window titled Save Block As for BSAVEAS:
    /// the name typed or chosen from &lt;Current Drawing&gt; and the drawing's blocks, the preview and description of the block
    /// chosen. A double click on a row is OK. Its logic is <see cref="BlockEditViewModel"/>.
    /// </summary>
    public partial class BlockEditWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public BlockEditWindow(BlockEditViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (sender, e) => NameBox.Focus();
        }

        /// <summary>Gets the view model.</summary>
        public BlockEditViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the name and the action it closed with are in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, BlockEditViewModel model)
        {
            new BlockEditWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Model.Selected != null && Model.CanOk)
            {
                OnOk(sender, e);
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
