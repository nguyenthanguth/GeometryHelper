using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Property Settings dialog of MATCHPROP's Settings, shown through a function the workspace sets, so that a test drives
    /// it with none; with none, MATCHPROP says the dialog is shown by the window and goes on with the settings it has.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Property Settings dialog: true when OK was pressed, its boxes then taken.</summary>
        public Func<MatchPropSettingsViewModel, bool> ShowMatchPropSettings { get; set; }

        /// <summary>MATCHPROP's Settings: the dialog over the session's settings, OK giving the session those checked.</summary>
        private void OnMatchPropRequested(object sender, CadUiRequestEventArgs e)
        {
            if (!(sender is CadCommandSession session) || ShowMatchPropSettings == null)
            {
                return;
            }

            var dialog = new MatchPropSettingsViewModel(session.MatchProperties);

            if (ShowMatchPropSettings(dialog))
            {
                session.MatchProperties = dialog.Result;
            }

            e.Handled = true;
        }
    }
}
