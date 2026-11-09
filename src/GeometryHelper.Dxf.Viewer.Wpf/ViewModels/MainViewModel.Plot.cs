using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// PLOT (Ctrl+P), PAGESETUP and PREVIEW shown through functions the workspace sets, so that a test drives them with none,
    /// and -PLOT's file written by the PDF writer. With no dialog to show, PLOT asks -PLOT's prompts on the command line.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Plot dialog: true when OK (or the preview's Plot) was pressed.</summary>
        public Func<PlotDialogViewModel, bool> ShowPlot { get; set; }

        /// <summary>Gets or sets how the workspace shows the plot preview of PREVIEW: true when its Plot was pressed.</summary>
        public Func<PlotPreviewViewModel, bool> ShowPlotPreview { get; set; }

        /// <summary>
        /// Gets or sets how the workspace asks where a plot is written, as AutoCAD's Browse for Plot File: given the folder
        /// offered (null for none) and the file's name, the path chosen, or null where it was cancelled.
        /// </summary>
        public Func<string, string, string> AskPlotFile { get; set; }

        /// <summary>Gets or sets the printers of Windows the Plot dialog lists, greyed: each its name and whether it is on the network; null for none.</summary>
        public Func<IEnumerable<KeyValuePair<string, bool>>> WindowsPrinters { get; set; }

        /// <summary>
        /// PLOT's and PAGESETUP's request: the dialog shown, then the plot written to the file asked for, or the page setup
        /// written to the layout as one undo step. A dialog closed by Window&lt; hands its settings back to the command, which
        /// asks the window's corners and shows it again.
        /// </summary>
        private void OnPlotRequested(object sender, CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            if (document == null || e.Plot == null || ShowPlot == null)
            {
                return;
            }

            bool isPageSetup = e.Name == "PAGESETUP";
            var dialog = new PlotDialogViewModel(document, e.Plot, isPageSetup, Printers());
            bool ok = ShowPlot(dialog);
            e.Handled = true;

            if (dialog.PicksWindow)
            {
                dialog.HandBack();
                return;
            }

            if (!ok)
            {
                return;
            }

            if (isPageSetup)
            {
                SayRefused(dialog.ApplyToLayout());
                return;
            }

            string path = AskPlotFile?.Invoke(dialog.SuggestedFolder, dialog.SuggestedFileName);

            if (path != null)
            {
                Plotted(sender, () => dialog.Plot(path), path);
            }
        }

        /// <summary>PREVIEW's request: the preview of the space's page setup, whose Plot writes it to the file asked for.</summary>
        private void OnPreviewRequested(object sender, CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            if (document == null || e.Plot == null || ShowPlotPreview == null)
            {
                return;
            }

            CadPlotJob job = e.Plot;
            var preview = new PlotPreviewViewModel(document, job.Space, job.Settings, job.Display);
            e.Handled = true;

            if (preview.Page.IsEmpty)
            {
                ((CadCommandSession)sender).Write("Nothing to plot: the plot area is empty.");
                return;
            }

            if (ShowPlotPreview(preview))
            {
                var dialog = new PlotDialogViewModel(document, job);
                string path = AskPlotFile?.Invoke(dialog.SuggestedFolder, dialog.SuggestedFileName);

                if (path != null)
                {
                    Plotted(sender, () => CadPdfWriter.Write(path, document, job.Space, job.Settings, job.Display), path);
                }
            }
        }

        /// <summary>-PLOT's request: the plot written to the file it named, by the PDF writer.</summary>
        private void OnPlotFileRequested(object sender, CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;
            CadPlotJob job = e.Plot;

            if (document == null || job?.FileName == null)
            {
                return;
            }

            try
            {
                CadPdfWriter.Write(job.FileName, document, job.Space, job.Settings, job.Display);
                e.Handled = true;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is System.IO.IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                GeometryHelperLog.Warn("MainViewModel: the plot to " + job.FileName + " was not written.", exception);
                ((CadCommandSession)sender).Write("The plot was not written: " + exception.Message);
            }
        }

        /// <summary>A plot written, its file named on the command line, or why it was not.</summary>
        private void Plotted(object sender, Func<CadPlotPage> plot, string path)
        {
            var session = (CadCommandSession)sender;

            try
            {
                plot();
                session.Write("Plot written to " + path + ".");
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is System.IO.IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                GeometryHelperLog.Warn("MainViewModel: the plot to " + path + " was not written.", exception);
                session.Write("The plot was not written: " + exception.Message);
            }
        }

        private IEnumerable<KeyValuePair<string, bool>> Printers()
        {
            try
            {
                return WindowsPrinters?.Invoke();
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("MainViewModel: the printers of Windows could not be listed.", exception);
                return null;
            }
        }
    }
}
