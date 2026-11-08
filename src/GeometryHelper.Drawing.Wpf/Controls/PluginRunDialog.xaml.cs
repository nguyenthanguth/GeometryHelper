using System;
using System.Windows;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The Run plugin dialog: 520 by 520, the plugin and the view as drop-downs, the objects of the view with a tick each, the
    /// plugin's settings with their defaults and ranges, then Cancel and Run. Its logic is <see cref="PluginRunViewModel"/>.
    /// </summary>
    public partial class PluginRunDialog : DrawingDialogWindow
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public PluginRunDialog(PluginRunViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            Attach(model);
        }

        /// <summary>Gets the view model.</summary>
        public PluginRunViewModel Model { get; }

        /// <inheritdoc/>
        public override FrameworkElement Body => Root;
    }
}
