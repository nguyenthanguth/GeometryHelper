using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Multiline Edit Tools dialog, which MLEDIT shows: a tool clicked closes it, MLEDIT then asking the multilines;
    /// its logic is <see cref="MlineEditToolsViewModel"/>.
    /// </summary>
    public partial class MlineEditToolsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public MlineEditToolsWindow(MlineEditToolsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public MlineEditToolsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until a tool is clicked or it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, MlineEditToolsViewModel model) =>
            new MlineEditToolsWindow(model) { Owner = owner }.ShowDialog();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnTool(object sender, RoutedEventArgs e)
        {
            Model.Pick((string)((Button)sender).Tag);
            DialogResult = true;
        }

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
