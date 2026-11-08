using System;
using System.Windows;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The Drawing settings dialog, as the design draws it: 840 by 600, the tabs Views, Scales, Dimensions, Marks and Sheet,
    /// the rows of the tab shown at the left and its preview at the right, then Cancel, Apply and OK. Its logic is
    /// <see cref="DrawingSettingsViewModel"/>.
    /// </summary>
    public partial class DrawingSettingsDialog : DrawingDialogWindow
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public DrawingSettingsDialog(DrawingSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            Attach(model);
        }

        /// <summary>Gets the view model.</summary>
        public DrawingSettingsViewModel Model { get; }

        /// <inheritdoc/>
        public override FrameworkElement Body => Root;
    }
}
