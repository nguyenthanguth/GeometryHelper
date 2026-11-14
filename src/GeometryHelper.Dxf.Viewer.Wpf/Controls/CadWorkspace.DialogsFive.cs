using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The dialogs left by earlier rounds in the workspace: Multiline Style, Multiline Edit Tools, Plot Stamp, Image Adjust,
    /// Attribute Extraction, Xbind, Insert Object, the Drawing Recovery Manager, Annotation Object Scale, Insert Layout(s),
    /// Template Options, and EXPORTPDF's Save As PDF, Page Setup Override and Export to PDF Options.
    /// </summary>
    public partial class CadWorkspace
    {
        /// <summary>Gives the view model the windows of the dialogs left by earlier rounds.</summary>
        private void AttachDialogsFive()
        {
            MainViewModel model = _model;
            model.ShowMlineStyle = dialog => MlineStyleWindow.Show(Window.GetWindow(this), dialog);
            model.ShowMlineEditTools = dialog => MlineEditToolsWindow.Show(Window.GetWindow(this), dialog);
            model.ShowPlotStamp = dialog => PlotStampWindow.Show(Window.GetWindow(this), dialog);
            model.ShowImageAdjust = dialog => ImageAdjustWindow.Show(Window.GetWindow(this), dialog);
            model.ShowAttributeExtraction = dialog => AttributeExtractionWindow.Show(Window.GetWindow(this), dialog);
            model.ShowXbind = dialog => XbindWindow.Show(Window.GetWindow(this), dialog);
            model.ShowInsertObject = dialog => InsertObjectWindow.Show(Window.GetWindow(this), dialog);
            model.ShowDrawingRecovery = palette => DrawingRecoveryWindow.Show(Window.GetWindow(this), palette);
            model.ShowObjectScale = dialog => ObjectScaleWindow.Show(Window.GetWindow(this), dialog);
            model.ShowInsertLayout = dialog => InsertLayoutWindow.Show(Window.GetWindow(this), dialog);
            model.ShowTemplateOptions = dialog => TemplateOptionsWindow.Show(Window.GetWindow(this), dialog);
            model.ShowSaveAsPdf = dialog => SaveAsPdfWindow.Show(Window.GetWindow(this), dialog);
            model.ShowPageSetupOverride = dialog => PageSetupOverrideWindow.Show(Window.GetWindow(this), dialog);
            model.ShowExportPdfOptions = dialog => ExportPdfOptionsWindow.Show(Window.GetWindow(this), dialog);
            model.OpenInViewer = OpenInViewer;
        }

        /// <summary>EXPORTPDF's Open in viewer when done: the PDF opened by the program Windows gives its type; a failure is logged.</summary>
        private static void OpenInViewer(string file)
        {
            try
            {
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            }
            catch (Exception exception)
                when (exception is Win32Exception
                    || exception is InvalidOperationException
                    || exception is FileNotFoundException
                )
            {
                GeometryHelperLog.Warn("CadWorkspace: " + file + " was not opened in a viewer.", exception);
            }
        }
    }
}
