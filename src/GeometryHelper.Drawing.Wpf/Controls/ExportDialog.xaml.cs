using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The Export dialog, as the design draws it: 680 by 520, the model drawings ticked at the left, the format, the DXF layout,
    /// the version, the folder and the file names at the right, then Cancel and Export. Its logic is <see cref="ExportViewModel"/>.
    /// </summary>
    public partial class ExportDialog : DrawingDialogWindow
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public ExportDialog(ExportViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            Attach(model);
        }

        /// <summary>Gets the view model.</summary>
        public ExportViewModel Model { get; }

        /// <inheritdoc/>
        public override FrameworkElement Body => Root;

        /// <summary>Browse: a Save dialog chooses the folder, as .NET Framework's WPF has no folder dialog.</summary>
        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Choose the folder to export to",
                FileName = "Export here.dxf",
                Filter = "DXF drawings (*.dxf)|*.dxf",
                OverwritePrompt = false,
            };

            if (Directory.Exists(Model.Folder))
            {
                dialog.InitialDirectory = Model.Folder;
            }

            if (dialog.ShowDialog(this) == true)
            {
                Model.Folder = Path.GetDirectoryName(dialog.FileName);
            }
        }
    }
}
