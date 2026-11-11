using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Table Style dialog (TABLESTYLE), dark as the window's other dialogs. Its logic is
    /// <see cref="TableStyleManagerViewModel"/>; New… asks the name in <see cref="StyleNameWindow"/>, and New… and Modify… open
    /// <see cref="TableStyleModifyWindow"/> over it.
    /// </summary>
    public partial class TableStyleWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public TableStyleWindow(TableStyleManagerViewModel model)
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
            model.AskNewStyle = (offered, names) =>
            {
                string start = model.SelectedStyle?.Name ?? "Standard";
                string name = StyleNameWindow.Ask(
                    this,
                    new StyleNameViewModel("New Table Style", offered, n => Contains(names, n))
                );
                return name == null ? null : Tuple.Create(name, start);
            };
            model.ShowModify = modify => new TableStyleModifyWindow(modify) { Owner = this }.ShowDialog() == true;
        }

        /// <summary>Gets the view model.</summary>
        public TableStyleManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private static bool Contains(System.Collections.Generic.IReadOnlyList<string> names, string name)
        {
            foreach (string each in names)
            {
                if (string.Equals(each, (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
