using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Block Definition dialog, which BLOCK (B) shows: the name, base point, objects, behaviour, unit and
    /// description. Pick point and Select objects close it for the command line to ask, and BLOCK shows it again. Its logic is
    /// <see cref="BlockDefinitionViewModel"/>.
    /// </summary>
    public partial class BlockDefinitionWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public BlockDefinitionWindow(BlockDefinitionViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public BlockDefinitionViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, BlockDefinitionViewModel model)
        {
            new BlockDefinitionWindow(model) { Owner = owner }.ShowDialog();
            return true;
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
            bool redefine = false;

            if (Model.Exists)
            {
                if (!CadQuestionWindow.AskRedefineBlock(this, Model.Name.Trim()))
                {
                    return;
                }

                redefine = true;
            }

            Model.Close(CadDialogAction.Ok, redefine);
            DialogResult = true;
        }

        private void OnNameChosen(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (((System.Windows.Controls.ComboBox)sender).SelectedItem is string name)
            {
                Model.Name = name;
            }
        }

        private void OnPickPoint(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.PickPoint);

        private void OnSelectObjects(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.SelectObjects);

        private void OnCancel(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Cancel);

        private void CloseWith(CadDialogAction action)
        {
            Model.Close(action);
            DialogResult = action != CadDialogAction.Cancel;
        }
    }
}
