using System;
using System.Windows;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The New drawing dialog, as the design draws it: 720 by 560, three steps (Source, Objects, Settings) under its header,
    /// Cancel, Back and Next or Create at its foot, and a band with Stop while the drawing is made. Its logic is
    /// <see cref="NewDrawingViewModel"/>.
    /// </summary>
    public partial class NewDrawingDialog : DrawingDialogWindow
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public NewDrawingDialog(NewDrawingViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            Attach(model);
        }

        /// <summary>Gets the view model.</summary>
        public NewDrawingViewModel Model { get; }

        /// <inheritdoc/>
        public override FrameworkElement Body => Root;
    }
}
