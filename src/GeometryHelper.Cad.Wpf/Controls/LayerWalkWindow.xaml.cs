using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's LayerWalk, which LAYWALK shows: the layers listed under a filter box, the ones selected the only ones shown in
    /// the drawing; Select Objects picks layers on the canvas, Purge Layers (also in the list's menu) deletes the unused ones
    /// selected, and Restore on exit puts every layer back when it closes. Its logic is <see cref="LayerWalkViewModel"/>.
    /// </summary>
    public partial class LayerWalkWindow : Window
    {
        private bool _filling;

        /// <summary>Makes the dialog over its view model, the layers walked selected again.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public LayerWalkWindow(LayerWalkViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.Refilled += (s, e) => SelectWalked();
            SelectWalked();
        }

        /// <summary>Gets the view model.</summary>
        public LayerWalkViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; what it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, LayerWalkViewModel model)
        {
            new LayerWalkWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        /// <summary>The layers already walked (the dialog shown again after Select Objects) selected in the list.</summary>
        private void SelectWalked()
        {
            _filling = true;

            try
            {
                foreach (
                    LayerWalkRowViewModel row in Model.Rows.Where(r =>
                        Model.Selected.Contains(r.Name, StringComparer.OrdinalIgnoreCase)
                    )
                )
                {
                    Layers.SelectedItems.Add(row);
                }
            }
            finally
            {
                _filling = false;
            }
        }

        private string[] Names() => Layers.SelectedItems.Cast<LayerWalkRowViewModel>().Select(r => r.Name).ToArray();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_filling && !Model.IsRefilling)
            {
                Model.Select(Names());
            }
        }

        private void OnSelectAll(object sender, RoutedEventArgs e) => Layers.SelectAll();

        private void OnClearAll(object sender, RoutedEventArgs e) => Layers.UnselectAll();

        private void OnPurge(object sender, RoutedEventArgs e)
        {
            Model.Purge(Names());
        }

        private void OnSelectObjects(object sender, RoutedEventArgs e)
        {
            Model.SelectObjects();
            DialogResult = true;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Model.Close();
            DialogResult = false;
        }
    }
}
