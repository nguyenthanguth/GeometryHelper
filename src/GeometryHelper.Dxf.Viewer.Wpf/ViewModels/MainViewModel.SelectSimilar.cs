using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Select Similar Settings dialog of SELECTSIMILAR's SEttings, shown through a function the workspace sets, so that a
    /// test drives it with none; with none, SELECTSIMILAR says the dialog is shown by the window.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Select Similar Settings dialog: true when OK was pressed, its boxes then taken.</summary>
        public Func<SelectSimilarSettingsViewModel, bool> ShowSelectSimilarSettings { get; set; }

        /// <summary>SELECTSIMILAR's SEttings: the dialog over the mode, OK giving the command the bits ticked.</summary>
        private void OnSelectSimilarRequested(CadUiRequestEventArgs e)
        {
            if (ShowSelectSimilarSettings == null || !(e.Payload is CadSelectSimilarRequest request))
            {
                return;
            }

            var dialog = new SelectSimilarSettingsViewModel(request.Mode);

            if (ShowSelectSimilarSettings(dialog))
            {
                request.Mode = dialog.Mode;
            }

            e.Handled = true;
        }
    }
}
