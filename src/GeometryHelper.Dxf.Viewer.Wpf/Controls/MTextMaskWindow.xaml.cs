using System;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The MTEXT editor's Background Mask dialog, as AutoCAD 2021's and the design draw it: Use background mask, the border
    /// offset factor and the fill colour. Its logic is <see cref="MTextMaskViewModel"/>.
    /// </summary>
    public partial class MTextMaskWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public MTextMaskWindow(MTextMaskViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PickColor = dialog => SelectColorWindow.Pick(this, dialog);
            DialogFault.Follow(this, model, () => model.FaultBox);
        }

        /// <summary>Gets the view model.</summary>
        public MTextMaskViewModel Model { get; }

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Show(Window owner, MTextMaskViewModel model) =>
            new MTextMaskWindow(model) { Owner = owner }.ShowDialog() == true;

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
