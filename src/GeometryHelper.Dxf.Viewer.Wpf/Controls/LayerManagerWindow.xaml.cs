using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Layer manager, as the design draws it: the layer table of the drawing with its current mark, on, frozen and locked
    /// flags, colour, linetype, weight and owner; New layer, Delete and Set current; a search. Every change is one undo step, made
    /// through <see cref="LayerManagerViewModel"/>; the dialog follows the drawing while it is open and lets it go when closed.
    /// </summary>
    public partial class LayerManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; disposed when the dialog closes.</param>
        public LayerManagerWindow(LayerManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            Closed += (s, e) => Model.Dispose();
        }

        /// <summary>Gets the view model.</summary>
        public LayerManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
