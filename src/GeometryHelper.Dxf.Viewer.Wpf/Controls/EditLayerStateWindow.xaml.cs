using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Edit Layer State of the Layer States Manager: the layers of the state chosen with their properties, Add layers
    /// and Remove layers, and the properties the state restores. Its logic is <see cref="EditLayerStateViewModel"/>.
    /// </summary>
    public partial class EditLayerStateWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public EditLayerStateWindow(EditLayerStateViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            model.PickLinetype = dialog => SelectLinetypeWindow.Pick(this, dialog);
            model.PickLayers = offered => SelectLayersWindow.Pick(this, offered);
        }

        /// <summary>Gets the view model.</summary>
        public EditLayerStateViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Show(Window owner, EditLayerStateViewModel model) =>
            new EditLayerStateWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAddLayers(object sender, RoutedEventArgs e) => Model.AddLayers();

        private void OnRemoveLayers(object sender, RoutedEventArgs e) => Model.RemoveLayers();

        private void OnSelectAll(object sender, RoutedEventArgs e) => Model.SetAll(true);

        private void OnClearAll(object sender, RoutedEventArgs e) => Model.SetAll(false);

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }

    /// <summary>
    /// AutoCAD's Select Layers to Add to Layer State: the drawing's layers the state does not name, several chosen at once
    /// with Ctrl and Shift.
    /// </summary>
    public sealed class SelectLayersWindow : Window
    {
        private readonly System.Windows.Controls.ListBox _list;

        private SelectLayersWindow(IReadOnlyList<string> offered)
        {
            Title = "Select Layers to Add to Layer State";
            Width = 360;
            Height = 420;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _list = new System.Windows.Controls.ListBox
            {
                ItemsSource = offered,
                SelectionMode = System.Windows.Controls.SelectionMode.Extended,
                Margin = new Thickness(8),
            };
            var ok = new System.Windows.Controls.Button
            {
                Content = "OK",
                IsDefault = true,
                Padding = new Thickness(14, 4, 14, 4),
                Margin = new Thickness(0, 0, 8, 0),
            };
            ok.Click += (s, e) => DialogResult = true;
            var cancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                IsCancel = true,
                Padding = new Thickness(14, 4, 14, 4),
            };
            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8),
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var panel = new System.Windows.Controls.DockPanel();
            System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
            panel.Children.Add(buttons);
            panel.Children.Add(_list);
            Content = panel;
        }

        /// <summary>Shows the dialog and gives the layers chosen; empty for Cancel.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="offered">The layers offered.</param>
        /// <returns>The layers chosen.</returns>
        public static IReadOnlyList<string> Pick(Window owner, IReadOnlyList<string> offered)
        {
            var dialog = new SelectLayersWindow(offered) { Owner = owner };
            var chosen = new List<string>();

            if (dialog.ShowDialog() == true)
            {
                foreach (object item in dialog._list.SelectedItems)
                {
                    chosen.Add((string)item);
                }
            }

            return chosen;
        }
    }
}
