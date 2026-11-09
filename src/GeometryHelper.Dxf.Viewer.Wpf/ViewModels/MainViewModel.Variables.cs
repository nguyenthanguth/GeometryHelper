using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The system variables the window keeps, added to each command line beside those of the drawing: OSMODE and the status
    /// bar's toggles (ORTHOMODE, SNAPMODE, GRIDMODE, DYNMODE, AUTOSNAP's tracking bits), POLARANG, and PICKBOX, APERTURE and
    /// LWDISPLAY of Global settings. Each is set at once, with no step of undo, as AutoCAD keeps them out of the drawing; the
    /// status bar and the canvas follow.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // OSMODE's bit that keeps the object snaps ticked but off, as F3 leaves them.
        private const int SnapsOff = 16384;

        // AUTOSNAP's bits the canvas always has: the marker, its tooltip, the magnet and the tooltips of tracking.
        private const int AutoSnapAlways = 1 | 2 | 4 | 32;

        /// <summary>Adds the variables of the window to a command line's registry, ORTHOMODE and LWDISPLAY of the drawing replaced by those the window draws by.</summary>
        private void AddWindowVariables(CadCommandRegistry registry)
        {
            registry.AddVariable(CadSystemVariable.Integer("OSMODE", 0, 32767, () => (int)SnapModes | (FindToggle("OSNAP").IsOn ? 0 : SnapsOff), SetOsmode));
            registry.AddVariable(ToggleVariable("ORTHOMODE", "ORTHO"));
            registry.AddVariable(ToggleVariable("SNAPMODE", "SNAP"));
            registry.AddVariable(ToggleVariable("GRIDMODE", "GRID"));
            registry.AddVariable(CadSystemVariable.Real("POLARANG", 360.0, () => _settings.PolarAngle, SetPolarAngle));
            registry.AddVariable(CadSystemVariable.Integer("AUTOSNAP", 0, 63, () => AutoSnapAlways | (FindToggle("POLAR").IsOn ? 8 : 0) | (FindToggle("OTRACK").IsOn ? 16 : 0), SetAutosnap));
            registry.AddVariable(CadSystemVariable.Integer("DYNMODE", -3, 3, () => DynamicInput.IsOn ? 3 : -3, v => DynamicInput.IsOn = v > 0));
            registry.AddVariable(CadSystemVariable.Integer("PICKBOX", 1, 50, () => _settings.PickboxPixels, v => ChangeSettings(s => s.PickboxPixels = v)));
            registry.AddVariable(CadSystemVariable.Integer("APERTURE", 2, 50, () => _settings.AperturePixels, v => ChangeSettings(s => s.AperturePixels = v)));
            registry.AddVariable(CadSystemVariable.Integer("LWDISPLAY", 0, 1, () => _settings.ShowLineWeights ? 1 : 0, v => ChangeSettings(s => s.ShowLineWeights = v != 0)));
        }

        /// <summary>A switch of the status bar as a variable of 0 and 1.</summary>
        private CadSystemVariable ToggleVariable(string name, string toggle)
            => CadSystemVariable.Integer(name, 0, 1, () => FindToggle(toggle).IsOn ? 1 : 0, v => FindToggle(toggle).IsOn = v != 0);

        /// <summary>OSMODE set: the snaps of its bits ticked, and OSNAP off where it holds 16384, as AutoCAD reads it.</summary>
        private void SetOsmode(int value)
        {
            SnapModes = (CadSnapModes)(value & (SnapsOff - 1));
            FindToggle("OSNAP").IsOn = (value & SnapsOff) == 0;
        }

        /// <summary>AUTOSNAP set: POLAR follows its bit 8 and OTRACK its bit 16; the canvas keeps its markers, tooltips and magnet.</summary>
        private void SetAutosnap(int value)
        {
            FindToggle("POLAR").IsOn = (value & 8) != 0;
            FindToggle("OTRACK").IsOn = (value & 16) != 0;
        }

        /// <summary>
        /// The polar increment, in degrees, set for the snaps and in the settings in use, as POLARANG and Drafting Settings set
        /// it; the window keeps it in the settings file (<see cref="SettingsChanged"/>).
        /// </summary>
        internal void SetPolarAngle(double degrees)
        {
            Snap.PolarAngle = degrees * Math.PI / 180.0;
            ChangeSettings(s => s.PolarAngle = degrees);
        }

        /// <summary>
        /// Raised when a value of Global settings is changed from the command line or a dialog other than Global settings:
        /// PICKBOX, APERTURE, LWDISPLAY, POLARANG, or the increment of Drafting Settings. The window keeps the settings in use in
        /// the settings file, as it keeps DYN and OSMODE.
        /// </summary>
        public event EventHandler SettingsChanged;

        /// <summary>A value of Global settings changed in the settings in use, given to the canvas as settings applied are, and raised to be kept.</summary>
        private void ChangeSettings(Action<GlobalSettings> change)
        {
            change(_settings);
            Raise(nameof(Settings));
            SettingsApplied?.Invoke(this, EventArgs.Empty);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
