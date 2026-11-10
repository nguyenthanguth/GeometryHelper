using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Object Grouping dialog, which CLASSICGROUP shows: the list of groups, Group Identification, Create Group and
    /// Change Group; Re-Order… opens <see cref="OrderGroupWindow"/>. A button ending in &lt; closes it for the command line.
    /// Its logic is <see cref="GroupManagerViewModel"/>.
    /// </summary>
    public partial class GroupManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public GroupManagerWindow(GroupManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.ShowOrder = order => new OrderGroupWindow(order) { Owner = this }.ShowDialog() == true;
        }

        /// <summary>Gets the view model.</summary>
        public GroupManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, GroupManagerViewModel model)
        {
            new GroupManagerWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnPick(object sender, RoutedEventArgs e)
        {
            Model.Pick((string)((FrameworkElement)sender).Tag);
            DialogResult = true;
        }

        private void OnRename(object sender, RoutedEventArgs e) => Model.Rename();

        private void OnDescription(object sender, RoutedEventArgs e) => Model.ChangeDescription();

        private void OnExplode(object sender, RoutedEventArgs e) => Model.Explode();

        private void OnSelectable(object sender, RoutedEventArgs e) => Model.ToggleSelectable();

        private void OnReorder(object sender, RoutedEventArgs e)
        {
            if (Model.Reorder())
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
