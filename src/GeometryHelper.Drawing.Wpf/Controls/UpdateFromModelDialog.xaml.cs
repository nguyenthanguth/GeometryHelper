using System;
using System.Windows;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The Update from model dialog, as the design draws it: 760 by 560, the source to update from, a line of what changed,
    /// the model changes and the hand edits as tables, then Cancel and Update. Its logic is <see cref="UpdateFromModelViewModel"/>;
    /// the changes are found when it opens.
    /// </summary>
    public partial class UpdateFromModelDialog : DrawingDialogWindow
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public UpdateFromModelDialog(UpdateFromModelViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            Attach(model);
        }

        /// <summary>Gets the view model.</summary>
        public UpdateFromModelViewModel Model { get; }

        /// <inheritdoc/>
        public override FrameworkElement Body => Root;
    }
}
