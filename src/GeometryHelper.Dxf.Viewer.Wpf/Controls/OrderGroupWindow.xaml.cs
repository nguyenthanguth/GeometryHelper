using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Order Group dialog of Object Grouping: the group, Remove from position, Enter new position and Number of
    /// objects; Re-Order, Highlight and Reverse Order. Highlight closes it, and Object Grouping with it, for the command line
    /// to show the objects. Its logic is <see cref="OrderGroupViewModel"/>.
    /// </summary>
    public partial class OrderGroupWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public OrderGroupWindow(OrderGroupViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public OrderGroupViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnReorder(object sender, RoutedEventArgs e) => Model.Reorder();

        private void OnReverse(object sender, RoutedEventArgs e) => Model.Reverse();

        private void OnHighlight(object sender, RoutedEventArgs e)
        {
            if (Model.Highlight())
            {
                DialogResult = true;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
