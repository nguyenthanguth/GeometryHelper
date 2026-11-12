using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// PLOT (Ctrl+P), PAGESETUP and PREVIEW shown through functions the workspace sets, so that a test drives them with none,
    /// -PLOT's file written by the PDF writer, and a plot printed on a printer of Windows through the queue it is given. With
    /// no dialog to show, PLOT asks -PLOT's prompts on the command line.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The settings of the last plot or print made, which New Page Setup can start with as &lt;Previous plot&gt;.</summary>
        private CadPlotSettings _previousPlot;

        /// <summary>Gets or sets how the workspace shows the Plot dialog: true when OK (or the preview's Plot) was pressed.</summary>
        public Func<PlotDialogViewModel, bool> ShowPlot { get; set; }

        /// <summary>Gets or sets how the workspace shows the plot preview of PREVIEW: true when its Plot was pressed.</summary>
        public Func<PlotPreviewViewModel, bool> ShowPlotPreview { get; set; }

        /// <summary>
        /// Gets or sets how the workspace asks where a plot is written, as AutoCAD's Browse for Plot File: given the folder
        /// offered (null for none) and the file's name, the path chosen, or null where it was cancelled.
        /// </summary>
        public Func<string, string, string> AskPlotFile { get; set; }

        /// <summary>Gets or sets the printers of Windows the Plot dialog lists after the presenter's own plotters, and prints to; null for none.</summary>
        public Func<IEnumerable<IPlotPrinter>> WindowsPrinters { get; set; }

        /// <summary>Gets or sets how the workspace shows the Page Setup Manager, which PAGESETUP opens; null shows the Page Setup dialog on the layout's own page setup instead.</summary>
        public Action<PageSetupManagerViewModel> ShowPageSetupManager { get; set; }

        /// <summary>
        /// PLOT's and PAGESETUP's request: the dialog shown, then the plot written to the file asked for, or the page setup
        /// written to the layout as one undo step. A dialog closed by Window&lt; hands its settings back to the command, which
        /// asks the window's corners and shows it again.
        /// </summary>
        private void OnPlotRequested(object sender, CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            bool isPageSetup = e.Name == "PAGESETUP";

            if (document != null && e.Plot != null && isPageSetup && ShowPageSetupManager != null)
            {
                OpenPageSetupManager(document, e.Plot.Space);
                e.Handled = true;
                return;
            }

            if (document == null || e.Plot == null || ShowPlot == null)
            {
                return;
            }

            var dialog = new PlotDialogViewModel(document, e.Plot, isPageSetup, Printers(), _settings.CustomPixelSizes);

            // A size of an image made with Custom… is kept in the settings file, as AutoCAD keeps it in the device's PC3.
            dialog.CustomSizeMade += (s, size) =>
            {
                _settings.NoteCustomPixelSize(size);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
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

            _previousPlot = dialog.Settings.Clone();

            if (dialog.IsPrinter)
            {
                Printed(sender, dialog);
                return;
            }

            if (!((CadCommandSession)sender).FileDialogs)
            {
                // FILEDIA 0: the command asks the file on the command line, then writes the plot as -PLOT does.
                if (dialog.SaveChangesToLayout)
                {
                    SayRefused(dialog.ApplyToLayout());
                }

                dialog.HandBack(true);
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
            var dialog = new PlotDialogViewModel(document, job, false, Printers());
            var preview = new PlotPreviewViewModel(document, job.Space, dialog.Settings, job.Display);
            e.Handled = true;

            if (preview.Page.IsEmpty)
            {
                ((CadCommandSession)sender).Write("Nothing to plot: the plot area is empty.");
                return;
            }

            if (ShowPlotPreview(preview))
            {
                _previousPlot = dialog.Settings.Clone();
                if (dialog.IsPrinter)
                {
                    Printed(sender, dialog);
                    return;
                }

                string path = AskPlotFile?.Invoke(dialog.SuggestedFolder, dialog.SuggestedFileName);

                if (path != null)
                {
                    Plotted(
                        sender,
                        () => PlotDialogViewModel.Write(path, document, job.Space, job.Settings, job.Display),
                        path
                    );
                }
            }
        }

        /// <summary>-PLOT's request: the plot written to the file it named, by the PDF writer or, for GeometryHelper PNG and JPG, the image writer.</summary>
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
                PlotDialogViewModel.Write(job.FileName, document, job.Space, job.Settings, job.Display);
                e.Handled = true;
            }
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is System.IO.IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
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
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is System.IO.IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
            {
                GeometryHelperLog.Warn("MainViewModel: the plot to " + path + " was not written.", exception);
                session.Write("The plot was not written: " + exception.Message);
            }
        }

        /// <summary>
        /// A plot printed on the printer the dialog has chosen, and where it went on the command line, or why it was not. A
        /// printer that fails, or a page with nothing on it, is said and not thrown.
        /// </summary>
        private void Printed(object sender, PlotDialogViewModel dialog)
        {
            var session = (CadCommandSession)sender;
            string printer = dialog.Settings.DeviceName;

            try
            {
                dialog.Print();
                session.Write("Plot sent to " + printer + ".");
            }
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is System.IO.IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
            {
                GeometryHelperLog.Warn("MainViewModel: the plot to " + printer + " was not printed.", exception);
                session.Write("The plot was not printed: " + exception.Message);
            }
        }

        /// <summary>PAGESETUP's manager, given the printers; a size of an image it made is kept in the settings file. The window gives it its dialogs.</summary>
        private void OpenPageSetupManager(CadDocument document, CadSpace space)
        {
            var manager = new PageSetupManagerViewModel(document, space, Printers(), _settings.CustomPixelSizes);
            manager.CustomSizeMade += (s, size) =>
            {
                _settings.NoteCustomPixelSize(size);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
            manager.Session = _current?.Session;
            manager.PreviousPlot = () => _previousPlot;
            ShowPageSetupManager(manager);
        }

        private IEnumerable<IPlotPrinter> Printers()
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
