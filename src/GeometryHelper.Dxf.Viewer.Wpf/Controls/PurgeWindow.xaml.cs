using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Purge dialog, which PURGE shows: the modes Purgeable Items and Find Non-Purgeable Items, the tree, its
    /// options and preview, or the possible reasons and Select Objects; Purge Checked Items, Purge All and Close. Its logic is
    /// <see cref="PurgeViewModel"/>.
    /// </summary>
    public partial class PurgeWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public PurgeWindow(PurgeViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public PurgeViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; Select Objects, where pressed, is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, PurgeViewModel model)
        {
            new PurgeWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnMode(object sender, RoutedEventArgs e)
        {
            if (Model != null)
            {
                Model.Mode = (string)((RadioButton)sender).Content;
            }
        }

        private void OnTwisty(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PurgeNodeViewModel node)
            {
                node.IsExpanded = !node.IsExpanded;
                e.Handled = true;
            }
        }

        private void OnPurgeChecked(object sender, RoutedEventArgs e) => Model.PurgeChecked();

        private void OnPurgeAll(object sender, RoutedEventArgs e) => Model.PurgeAll();

        private void OnSelectObjects(object sender, RoutedEventArgs e)
        {
            Model.SelectObjects();
            DialogResult = true;
        }

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
