using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Plot Style Table Editor, over a .ctb or .stb file: the General, Table View and Form View tabs. Save &amp; Close
    /// writes the table; Save As… writes it to another file. Its logic is <see cref="PlotStyleEditorViewModel"/>.
    /// </summary>
    public partial class PlotStyleEditorWindow : Window
    {
        /// <summary>Makes the editor over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public PlotStyleEditorWindow(PlotStyleEditorViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.AskSaveAs = suggested =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save plot style table as",
                    InitialDirectory = Path.GetDirectoryName(suggested),
                    FileName = Path.GetFileName(suggested),
                    Filter = model.IsNamed
                        ? "Named plot style tables (*.stb)|*.stb"
                        : "Plot style tables (*.ctb)|*.ctb",
                    OverwritePrompt = true,
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
        }

        /// <summary>Gets the view model.</summary>
        public PlotStyleEditorViewModel Model { get; }

        /// <summary>Gets the editor's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the editor over a window and says whether the table was saved.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when Save &amp; Close or Save As… wrote the table.</returns>
        public static bool Show(Window owner, PlotStyleEditorViewModel model) =>
            new PlotStyleEditorWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnTab(object sender, RoutedEventArgs e) => Model.Tab = (string)((RadioButton)sender).Content;

        private void OnAddStyle(object sender, RoutedEventArgs e) => Model.AddStyle();

        private void OnDeleteStyle(object sender, RoutedEventArgs e) => Model.DeleteStyle();

        private void OnSaveAndClose(object sender, RoutedEventArgs e)
        {
            // A value typed and not yet left is taken first.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (Model.Save())
            {
                DialogResult = true;
            }
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (Model.SaveAs())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
