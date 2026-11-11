using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Dimension Style Manager (DIMSTYLE), as the design lays it out, dark as the window's other dialogs. Its logic is
    /// <see cref="DimStyleManagerViewModel"/>; New, Modify (and a double click on a style) and Override open
    /// <see cref="DimStyleEditWindow"/>, Compare <see cref="DimStyleCompareWindow"/>; Rename and Delete are on the list's menu.
    /// </summary>
    public partial class DimStyleManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <param name="showTextStyles">What shows the Text Style dialog, for the "..." of the Text tab; null for none.</param>
        public DimStyleManagerWindow(DimStyleManagerViewModel model, Action showTextStyles = null)
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
            model.AskName = ask => StyleNameWindow.Ask(this, ask);
            model.ShowEditor = editor =>
            {
                editor.ShowTextStyles = showTextStyles;
                return new DimStyleEditWindow(editor) { Owner = this }.ShowDialog() == true;
            };
            model.ShowCompare = compare => new DimStyleCompareWindow(compare) { Owner = this }.ShowDialog();
        }

        /// <summary>Gets the view model.</summary>
        public DimStyleManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>A double click on a style modifies it, as the foot of the dialog says.</summary>
        private void OnStyleDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (
                e.ChangedButton == MouseButton.Left
                && Model.SelectedStyle != null
                && Model.ModifyCommand.CanExecute(null)
            )
            {
                Model.ModifyCommand.Execute(null);
            }
        }
    }
}
