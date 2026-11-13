using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The dialogs left by earlier rounds: Multiline Style (MLSTYLE), Multiline Edit Tools (MLEDIT), Plot Stamp (PLOTSTAMP),
    /// Image Adjust (IMAGEADJUST), Attribute Extraction (ATTEXT) and Xbind (XBIND), each shown through a function the
    /// workspace sets, so that a test drives it with none.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows Multiline Style: true when OK was pressed.</summary>
        public Func<MlineStyleViewModel, bool> ShowMlineStyle { get; set; }

        /// <summary>Gets or sets how the workspace shows Multiline Edit Tools, until a tool is picked or it is closed.</summary>
        public Action<MlineEditToolsViewModel> ShowMlineEditTools { get; set; }

        /// <summary>Gets or sets how the workspace shows Plot Stamp: true when OK was pressed with the values taken.</summary>
        public Func<PlotStampViewModel, bool> ShowPlotStamp { get; set; }

        /// <summary>Gets or sets how the workspace shows Image Adjust: true when OK was pressed.</summary>
        public Func<ImageAdjustViewModel, bool> ShowImageAdjust { get; set; }

        /// <summary>Gets or sets how the workspace shows Attribute Extraction, until OK, Cancel or Select Objects.</summary>
        public Action<AttributeExtractionViewModel> ShowAttributeExtraction { get; set; }

        /// <summary>Gets or sets how the workspace shows Xbind: true when OK was pressed.</summary>
        public Func<XbindViewModel, bool> ShowXbind { get; set; }

        /// <summary>The requests of the dialogs left by earlier rounds; one with no function set is left to the command line.</summary>
        private void OnDialogsFiveRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadMlineStyleRequest styles when ShowMlineStyle != null:
                    var dialog = new MlineStyleViewModel(styles);

                    if (ShowMlineStyle(dialog))
                    {
                        dialog.Accept();
                    }

                    e.Handled = true;
                    break;
                case CadMlineEditRequest tools when ShowMlineEditTools != null:
                    ShowMlineEditTools(new MlineEditToolsViewModel(tools));
                    e.Handled = true;
                    break;
                case CadPlotStampRequest stamp when ShowPlotStamp != null:
                    stamp.IsAccepted = ShowPlotStamp(new PlotStampViewModel(stamp.Stamp));
                    e.Handled = true;
                    break;
                case CadImageAdjustRequest adjust when ShowImageAdjust != null:
                    var adjusting = new ImageAdjustViewModel(adjust);

                    if (ShowImageAdjust(adjusting))
                    {
                        adjusting.Accept();
                    }

                    e.Handled = true;
                    break;
                case CadAttextRequest extract when ShowAttributeExtraction != null:
                    ShowAttributeExtraction(new AttributeExtractionViewModel(extract));
                    e.Handled = true;
                    break;
                case CadXbindRequest bind when ShowXbind != null:
                    var binding = new XbindViewModel(bind);

                    if (ShowXbind(binding))
                    {
                        binding.Accept();
                    }

                    e.Handled = true;
                    break;
            }
        }
    }
}
