using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Edit Drawing Scales, which SCALELISTEDIT shows: the drawing's scale list, Add…, Edit…, Move Up, Move Down,
    /// Delete and Reset, and the scale chosen. Add… and Edit… show <see cref="ScaleEditWindow"/>, Reset asks first. Its logic
    /// is <see cref="ScaleListEditViewModel"/>.
    /// </summary>
    public partial class ScaleListEditWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ScaleListEditWindow(ScaleListEditViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskScale = dialog => ScaleEditWindow.Ask(this, dialog);
            model.ConfirmReset = () => CadQuestionWindow.AskResetScaleList(this);
            model.ConfirmDeleteInUse = () => CadQuestionWindow.AskDeleteScalesInUse(this);
            model.ShowMessage = message => CadQuestionWindow.ShowMessage(this, message);
        }

        /// <summary>Gets the view model.</summary>
        public ScaleListEditViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, ScaleListEditViewModel model)
        {
            new ScaleListEditWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
            Model.Choose(List.SelectedItems.OfType<ScaleRowViewModel>());

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e) => Model.Edit();

        private void OnAdd(object sender, RoutedEventArgs e) => Model.Add();

        private void OnEdit(object sender, RoutedEventArgs e) => Model.Edit();

        private void OnMoveUp(object sender, RoutedEventArgs e) => Model.MoveUp();

        private void OnMoveDown(object sender, RoutedEventArgs e) => Model.MoveDown();

        private void OnDelete(object sender, RoutedEventArgs e) => Model.Delete();

        private void OnReset(object sender, RoutedEventArgs e) => Model.Reset();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Model.Ok();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Cancel();
            DialogResult = false;
        }
    }
}
