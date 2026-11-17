using System;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The dialogs of data links and data extraction: the Data Link Manager (DATALINK), the Excel data link dialog, the Data
    /// Extraction wizard (DATAEXTRACTION), and the Select a Data Link picker that Link Cell opens. Each is shown through a
    /// function the workspace sets, so that a test drives it with none.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Data Link Manager, until it is closed.</summary>
        public Action<DataLinkManagerViewModel> ShowDataLinkManager { get; set; }

        /// <summary>Gets or sets how the workspace shows the Data Extraction wizard, until Finish, Cancel or Select objects.</summary>
        public Action<DataExtractionViewModel> ShowDataExtraction { get; set; }

        /// <summary>Gets or sets how the workspace picks a data link: the Select a Data Link dialog, giving the name chosen, or null.</summary>
        public Func<string> PickDataLink { get; set; }

        /// <summary>Gets or sets how the workspace edits a data link in the Modify Excel Link dialog, given its name; true where OK was pressed.</summary>
        public Func<string, bool> EditDataLink { get; set; }

        /// <summary>The requests of DATALINK and DATAEXTRACTION; one with no function set is left to the command line.</summary>
        private void OnDataLinksRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadDataLinkManagerRequest manager when ShowDataLinkManager != null:
                    ShowDataLinkManager(new DataLinkManagerViewModel(manager.Document));
                    e.Handled = true;
                    break;
                case CadDataExtractionRequest extraction when ShowDataExtraction != null:
                    ShowDataExtraction(new DataExtractionViewModel(extraction));
                    e.Handled = true;
                    break;
            }
        }
    }
}
