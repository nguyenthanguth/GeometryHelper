using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The windows of data links and data extraction in the workspace: the Data Link Manager, the Select a Data Link picker, the Excel data link dialog of Edit, and the Data Extraction wizard.</summary>
    public partial class CadWorkspace
    {
        /// <summary>Gives the view model the windows of DATALINK, Link Cell and DATAEXTRACTION.</summary>
        private void AttachDataLinks()
        {
            MainViewModel model = _model;
            model.ShowDataLinkManager = dialog => DataLinkManagerWindow.Show(Window.GetWindow(this), dialog);
            model.ShowDataExtraction = dialog => DataExtractionWindow.Show(Window.GetWindow(this), dialog);
            model.PickDataLink = () =>
            {
                CadDocument document = model.Document;

                if (document == null)
                {
                    return null;
                }

                var picker = new DataLinkManagerViewModel(document, true);
                return DataLinkManagerWindow.Show(Window.GetWindow(this), picker) ? picker.ChosenName : null;
            };
            model.EditDataLink = name =>
            {
                CadDocument document = model.Document;
                return document != null
                    && ExcelDataLinkWindow.Show(Window.GetWindow(this), new ExcelDataLinkViewModel(document, name));
            };
        }
    }
}
