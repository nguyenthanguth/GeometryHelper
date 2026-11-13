using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The dialogs left by earlier rounds in the workspace: Multiline Style, Multiline Edit Tools, Plot Stamp, Image Adjust,
    /// Attribute Extraction and Xbind.
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
        }
    }
}
