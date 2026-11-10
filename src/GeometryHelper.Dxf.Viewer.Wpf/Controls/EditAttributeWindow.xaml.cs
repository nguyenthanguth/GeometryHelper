using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Edit Attribute dialog of the Block Attribute Manager: Active Block, the tabs Attribute, Text Options and
    /// Properties, Cancel and OK, which keeps the dialog open where what was typed does not read. Its logic is
    /// <see cref="EditAttributeViewModel"/>.
    /// </summary>
    public partial class EditAttributeWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public EditAttributeWindow(EditAttributeViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public EditAttributeViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog on a tab, as a screenshot draws it: Attribute, Text Options or Properties.</summary>
        /// <param name="tab">The tab.</param>
        public void ShowTab(string tab)
        {
            Model.Tab = tab;
            TextTab.IsChecked = Model.Tab == "Text Options";
            PropertiesTab.IsChecked = Model.Tab == "Properties";
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

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Ok())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
