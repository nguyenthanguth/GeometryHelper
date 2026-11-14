using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// EXPORTPDF's Save As PDF with Page Setup Override and Export to PDF Options, each shown through a function the workspace
    /// sets, and the PDF opened in the viewer when done. What the dialogs leave is kept for the next EXPORTPDF until the app
    /// closes, as AutoCAD keeps it: Open in viewer when done, the override and the options.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private bool _pdfOpenInViewer = true;
        private bool _pdfHyperlinks = true;
        private CadPlotSettings _pdfOverride;

        /// <summary>Gets or sets how the workspace shows Save As PDF: true when Save was pressed. With none, EXPORTPDF shows the Save dialog alone.</summary>
        public Func<SaveAsPdfViewModel, bool> ShowSaveAsPdf { get; set; }

        /// <summary>Gets or sets how the workspace shows Page Setup Override: true when OK was pressed.</summary>
        public Func<PageSetupOverrideViewModel, bool> ShowPageSetupOverride { get; set; }

        /// <summary>Gets or sets how the workspace shows Export to PDF Options: true when OK was pressed.</summary>
        public Func<ExportPdfOptionsViewModel, bool> ShowExportPdfOptions { get; set; }

        /// <summary>Gets or sets how the workspace opens a PDF written in the viewer of its type.</summary>
        public Action<string> OpenInViewer { get; set; }

        /// <summary>EXPORTPDF's file: Save As PDF where it is set, its Browse… the Save dialog; else the Save dialog alone.</summary>
        private void OnExportPdfRequested(CadUiRequestEventArgs e)
        {
            CadExportPdfOptions options = e.Export?.PdfOptions;

            if (options == null || ShowSaveAsPdf == null)
            {
                OnExportFileRequested(e);
                return;
            }

            options.OpenInViewer = _pdfOpenInViewer;
            options.IncludeHyperlinks = _pdfHyperlinks;

            if (_pdfOverride != null)
            {
                PageSetupOverrideViewModel.CopyOverride(_pdfOverride, options.PageSetupOverride);
                options.OverridePageSetup = true;
            }

            var dialog = new SaveAsPdfViewModel(e.Export)
            {
                BrowseFile = AskExportFile,
                ShowPageSetupOverride = ShowPageSetupOverride,
                ShowOptions = ShowExportPdfOptions,
                EditPlotStamp = ShowPlotStamp,
            };

            if (ShowSaveAsPdf(dialog) && dialog.Accept())
            {
                _pdfOpenInViewer = options.OpenInViewer;
                _pdfHyperlinks = options.IncludeHyperlinks;
                _pdfOverride = options.OverridePageSetup ? options.PageSetupOverride.Clone() : null;
            }
            else
            {
                dialog.Cancel();
            }

            e.Handled = true;
        }

        /// <summary>EXPORTPDF's Open in viewer when done: the PDF written opened by the workspace.</summary>
        private void OnOpenInViewerRequested(CadUiRequestEventArgs e)
        {
            if (OpenInViewer != null && e.Payload is string file)
            {
                OpenInViewer(file);
                e.Handled = true;
            }
        }
    }
}
