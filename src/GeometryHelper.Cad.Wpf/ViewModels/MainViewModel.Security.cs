using System;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The gate in front of the code the window loads: one <see cref="CadLoadSecurity"/> for the whole app, built from Global
    /// settings and given to the session of every drawing and to the Python host, so that SETVAR of SECURELOAD in one drawing
    /// holds in the others, and a file answered Load Once in one is not asked about in another.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly CadLoadSecurity _loadSecurity = new CadLoadSecurity();
        private bool _loadSecurityFollowsSettings;

        /// <summary>
        /// Gets the gate of every drawing and of Python in this window. Its <see cref="CadLoadSecurity.Ask"/> is what shows the
        /// warning (the window sets it); until then a file outside the trusted folders is refused under SECURELOAD 1.
        /// </summary>
        internal CadLoadSecurity LoadSecurity => _loadSecurity;

        // Called once by the constructor: the settings in, and a change to SECURELOAD, TRUSTEDPATHS, LEGACYCODESEARCH or the
        // Always Load answers (a SETVAR, or a file answered Always Load) written back to them and saved.
        private void StartLoadSecurity()
        {
            UseLoadSecuritySettings();
            _loadSecurity.Changed += OnLoadSecurityChanged;
        }

        /// <summary>Gives the gate the values of the settings in use, without writing them back.</summary>
        private void UseLoadSecuritySettings()
        {
            _loadSecurityFollowsSettings = true;

            try
            {
                _loadSecurity.SecureLoad = _settings.SecureLoad;
                _loadSecurity.TrustedPaths = _settings.TrustedPaths;
                _loadSecurity.LegacyCodeSearch = _settings.LegacyCodeSearch;
                _loadSecurity.AlwaysLoaded = _settings.AlwaysLoadedFiles;
            }
            finally
            {
                _loadSecurityFollowsSettings = false;
            }
        }

        private void OnLoadSecurityChanged(object sender, EventArgs e)
        {
            if (_loadSecurityFollowsSettings)
            {
                return;
            }

            ChangeSettings(
                s =>
                {
                    s.SecureLoad = _loadSecurity.SecureLoad;
                    s.TrustedPaths = _loadSecurity.TrustedPaths;
                    s.LegacyCodeSearch = _loadSecurity.LegacyCodeSearch;
                    s.AlwaysLoadedFiles = _loadSecurity.AlwaysLoaded;
                },
                "a change to SECURELOAD, TRUSTEDPATHS, LEGACYCODESEARCH or the files always loaded"
            );
        }
    }
}
