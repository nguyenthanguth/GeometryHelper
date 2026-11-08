using System;
using System.Windows;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// A message of the model drawing, as the Messages board of the design draws them: 420 wide, the icon of its kind, its
    /// title, its text and a detail in monospace, then its action and OK. Its logic is <see cref="MessageViewModel"/>.
    /// </summary>
    public partial class MessageDialog : DrawingDialogWindow
    {
        /// <summary>Makes the message over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public MessageDialog(MessageViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            Attach(model);
        }

        /// <summary>Gets the view model.</summary>
        public MessageViewModel Model { get; }

        /// <inheritdoc/>
        public override FrameworkElement Body => Root;
    }
}
