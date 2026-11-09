using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Multileader Style Manager (MLEADERSTYLE), as the design lays it out, dark as the window's other dialogs. Its
    /// logic is <see cref="MLeaderStyleManagerViewModel"/>; New asks the name in <see cref="StyleNameWindow"/>, and New, Modify
    /// (and a double click on a style) open <see cref="MLeaderStyleEditWindow"/>.
    /// </summary>
    public partial class MLeaderStyleManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <param name="showTextStyles">What shows the Text Style dialog, for the "…" beside Text style; null for none.</param>
        public MLeaderStyleManagerWindow(MLeaderStyleManagerViewModel model, Action showTextStyles = null)
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
                return new MLeaderStyleEditWindow(editor) { Owner = this }.ShowDialog() == true;
            };
        }

        /// <summary>Gets the view model.</summary>
        public MLeaderStyleManagerViewModel Model { get; }

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
            if (e.ChangedButton == MouseButton.Left && Model.SelectedStyle != null && Model.ModifyCommand.CanExecute(null))
            {
                Model.ModifyCommand.Execute(null);
            }
        }
    }
}
