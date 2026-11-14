using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What EXPORTPDF's Save As PDF dialog sets beside the file, as AutoCAD 2021's Output Controls: whether the PDF opens in
    /// the viewer when written and carries the plot stamp, what is exported, the page setup used or overridden, and the
    /// options of Export to PDF Options. EXPORTPDF hangs it on its file request (<see cref="CadExportJob.PdfOptions"/>); a window
    /// that answers only the file leaves it as made, which exports as EXPORTPDF did before it: the current layout, or the
    /// display of model space, by the page setup in use. A layout drawing offers Current layout and All layouts, model space
    /// Display, Extents and Window.
    /// </summary>
    public sealed class CadExportPdfOptions
    {
        /// <summary>Export the current layout.</summary>
        public const string CurrentLayout = "Current layout";

        /// <summary>Export every layout, a page each, in the order of the tabs.</summary>
        public const string AllLayouts = "All layouts";

        /// <summary>Export what model space shows.</summary>
        public const string Display = "Display";

        /// <summary>Export the extents of model space.</summary>
        public const string Extents = "Extents";

        /// <summary>Export a window of model space, its two corners picked after the dialog where none is given.</summary>
        public const string Window = "Window";

        private string _export;

        internal CadExportPdfOptions(bool isModel, CadPlotSettings pageSetup, CadPlotStamp plotStamp)
        {
            IsModel = isModel;
            Exports = Array.AsReadOnly(
                isModel ? new[] { Display, Extents, Window } : new[] { CurrentLayout, AllLayouts }
            );
            _export = Exports[0];
            PageSetupOverride = pageSetup ?? throw new ArgumentNullException(nameof(pageSetup));
            PlotStamp = plotStamp ?? throw new ArgumentNullException(nameof(plotStamp));
            IncludePlotStamp = plotStamp.On;
        }

        /// <summary>Gets whether model space is exported, its Export list Display, Extents and Window.</summary>
        public bool IsModel { get; }

        /// <summary>Gets what the Export list offers: Current layout and All layouts in a layout, else Display, Extents and Window.</summary>
        public IReadOnlyList<string> Exports { get; }

        /// <summary>Gets or sets what is exported, one of <see cref="Exports"/>; the first to start with.</summary>
        /// <exception cref="ArgumentException">Thrown when the value set is not one of <see cref="Exports"/>.</exception>
        public string Export
        {
            get => _export;
            set
            {
                foreach (string each in Exports)
                {
                    if (string.Equals(each, value, StringComparison.Ordinal))
                    {
                        _export = each;
                        return;
                    }
                }

                throw new ArgumentException("The export is not one of those listed.", nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the window exported where <see cref="Export"/> is <see cref="Window"/>, in drawing units; empty, as it
        /// starts, the two corners are asked on the command line once the dialog is closed.
        /// </summary>
        public DisplayBox WindowBox { get; set; } = DisplayBox.Empty;

        /// <summary>Gets or sets whether the page setup is overridden by <see cref="PageSetupOverride"/> (Page Setup: Override); off, Current, each space is exported by its own.</summary>
        public bool OverridePageSetup { get; set; }

        /// <summary>
        /// Gets the settings Page Setup Override changes: the paper size, the plot style table, the orientation, the scale
        /// and Scale lineweights; a copy of the page setup in use to start with.
        /// </summary>
        public CadPlotSettings PageSetupOverride { get; }

        /// <summary>Gets or sets whether the PDF carries the plot stamp (Include plot stamp); the session's stamp's On to start with.</summary>
        public bool IncludePlotStamp { get; set; }

        /// <summary>Gets the plot stamp the dialog's Plot Stamp Settings change, a copy of the session's; it becomes the session's when the PDF is exported.</summary>
        public CadPlotStamp PlotStamp { get; }

        /// <summary>Gets or sets whether the PDF is opened in the viewer once written (Open in viewer when done); off as made.</summary>
        public bool OpenInViewer { get; set; }

        /// <summary>Gets or sets whether each hyperlink of an object plotted becomes a link of the PDF over its box (Include hyperlinks); on as made.</summary>
        public bool IncludeHyperlinks { get; set; } = true;
    }
}
