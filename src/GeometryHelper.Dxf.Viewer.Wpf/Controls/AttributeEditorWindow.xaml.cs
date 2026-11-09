using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Enhanced Attribute Editor, which EATTEDIT shows and a double click on an INSERT with attributes opens: the
    /// block and tag, the tabs Attribute, Text Options and Properties; Apply, which keeps what was typed and the editor open,
    /// Cancel, OK, and Select block, which closes it for the command line to ask another. Its logic is
    /// <see cref="AttributeEditorViewModel"/>.
    /// </summary>
    public partial class AttributeEditorWindow : Window
    {
        /// <summary>Makes the editor over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public AttributeEditorWindow(AttributeEditorViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public AttributeEditorViewModel Model { get; }

        /// <summary>Gets the editor's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the editor on a tab, as a screenshot draws it: Attribute, Text Options or Properties.</summary>
        /// <param name="tab">The tab.</param>
        public void ShowTab(string tab)
        {
            Model.Tab = tab;
            TextTab.IsChecked = tab == "Text Options";
            PropertiesTab.IsChecked = tab == "Properties";
        }

        /// <summary>Shows the editor over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, AttributeEditorViewModel model)
        {
            new AttributeEditorWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (Model != null)
            {
                Model.Tab = (string)((RadioButton)sender).Content;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Ok);

        private void OnApply(object sender, RoutedEventArgs e) => Model.Apply();

        private void OnSelectBlock(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.SelectBlock);

        private void OnCancel(object sender, RoutedEventArgs e) => CloseWith(CadDialogAction.Cancel);

        private void CloseWith(CadDialogAction action)
        {
            Model.Close(action);
            DialogResult = action != CadDialogAction.Cancel;
        }
    }
}
