using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Block Attribute Manager, which BATTMAN shows: Select block and the Block list, the grid of the block's
    /// attribute definitions, Sync, Move Up, Move Down, Edit and Remove; Settings, Apply, Cancel and OK. Edit opens
    /// <see cref="EditAttributeWindow"/>, Settings <see cref="AttributeManagerSettingsWindow"/>. Its logic is
    /// <see cref="BlockAttributeManagerViewModel"/>.
    /// </summary>
    public partial class BlockAttributeManagerWindow : Window
    {
        /// <summary>Makes the manager over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public BlockAttributeManagerWindow(BlockAttributeManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.ShowEditor = editor => new EditAttributeWindow(editor) { Owner = this }.ShowDialog() == true;
            model.ShowSettings = settings => new AttributeManagerSettingsWindow(settings) { Owner = this }.ShowDialog() == true;
        }

        /// <summary>Gets the view model.</summary>
        public BlockAttributeManagerViewModel Model { get; }

        /// <summary>Gets the manager's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the manager over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, BlockAttributeManagerViewModel model)
        {
            new BlockAttributeManagerWindow(model) { Owner = owner }.ShowDialog();
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
            if (e.ChangedButton == MouseButton.Left && Model.CanEdit)
            {
                Model.Edit();
            }
        }

        private void OnSync(object sender, RoutedEventArgs e) => Model.Sync();

        private void OnMoveUp(object sender, RoutedEventArgs e) => Model.MoveUp();

        private void OnMoveDown(object sender, RoutedEventArgs e) => Model.MoveDown();

        private void OnEdit(object sender, RoutedEventArgs e) => Model.Edit();

        private void OnRemove(object sender, RoutedEventArgs e) => Model.Remove();

        private void OnSettings(object sender, RoutedEventArgs e) => Model.OpenSettings();

        private void OnApply(object sender, RoutedEventArgs e) => Model.Apply();

        private void OnOk(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Ok);

        private void OnSelectBlock(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.SelectBlock);

        private void OnCancel(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Cancel);

        private void CloseWith(CadDialogAction action)
        {
            Model.Close(action);
            DialogResult = action != CadDialogAction.Cancel;
        }
    }
}
