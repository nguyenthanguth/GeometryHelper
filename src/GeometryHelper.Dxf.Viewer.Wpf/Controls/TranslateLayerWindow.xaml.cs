using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's New Layer and Edit Layer of the Layer Translator: the name, the colour and linetype (Select Color and Select
    /// Linetype), the lineweight, the transparency and the plot style. Its logic is <see cref="TranslateLayerViewModel"/>.
    /// </summary>
    public partial class TranslateLayerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public TranslateLayerWindow(TranslateLayerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            model.PickLinetype = dialog => SelectLinetypeWindow.Pick(this, dialog);
        }

        /// <summary>Gets the view model.</summary>
        public TranslateLayerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK, once the name and the transparency passed their checks.</returns>
        public static bool Show(Window owner, TranslateLayerViewModel model) =>
            new TranslateLayerWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnColor(object sender, RoutedEventArgs e) => Model.ChooseColor();

        private void OnLinetype(object sender, RoutedEventArgs e) => Model.ChooseLinetype();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            // OK is refused, the reason shown, while the name or the transparency is wrong; the layer is read again by the caller.
            if (Model.Commit() != null)
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
