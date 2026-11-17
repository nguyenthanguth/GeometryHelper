using System;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The Delete Duplicate Objects dialog of OVERKILL, shown through a function the workspace sets, so that a test drives it with none; with none, OVERKILL asks its options on the command line.</summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Delete Duplicate Objects dialog: true when OK was pressed, the settings then taken.</summary>
        public Func<OverkillViewModel, bool> ShowOverkill { get; set; }

        /// <summary>OVERKILL's dialog: over the session's settings, OK giving the command those the boxes hold.</summary>
        private void OnOverkillRequested(CadUiRequestEventArgs e)
        {
            if (ShowOverkill == null || !(e.Payload is CadOverkillRequest request))
            {
                return;
            }

            var dialog = new OverkillViewModel(request);
            request.Action = CadDialogAction.Cancel;

            if (ShowOverkill(dialog) && dialog.IsValid)
            {
                dialog.Commit();
            }

            e.Handled = true;
        }
    }
}
