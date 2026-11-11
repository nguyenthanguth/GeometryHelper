using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The system variables the window keeps, added to each command line beside those of the drawing: OSMODE and the status
    /// bar's toggles (ORTHOMODE, SNAPMODE, GRIDMODE, DYNMODE), AUTOSNAP, POLARANG, POLARMODE and POLARADDANG, PICKBOX and
    /// APERTURE of Global settings, and LWDISPLAY. Each is set at once, with no step of undo; the status bar and the canvas
    /// follow. ORTHOMODE, SNAPMODE, GRIDMODE and LWDISPLAY are written into the drawing shown as well, as AutoCAD saves them
    /// there.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // OSMODE's bit that keeps the object snaps ticked but off, as F3 leaves them.
        private const int SnapsOff = 16384;

        // DYNMODE as last set, its sign aside: which of pointer and dimensional input F12 brings back.
        private int _dynamicMode = 3;

        /// <summary>Adds the variables of the window to a command line's registry, ORTHOMODE and LWDISPLAY of the drawing replaced by those the window draws by.</summary>
        private void AddWindowVariables(CadCommandRegistry registry)
        {
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "OSMODE",
                    0,
                    32767,
                    () => (int)SnapModes | (FindToggle("OSNAP").IsOn ? 0 : SnapsOff),
                    SetOsmode
                )
            );
            registry.AddVariable(ToggleVariable("ORTHOMODE", "ORTHO"));
            registry.AddVariable(ToggleVariable("SNAPMODE", "SNAP"));
            registry.AddVariable(ToggleVariable("GRIDMODE", "GRID"));
            registry.AddVariable(CadSystemVariable.Real("POLARANG", 360.0, () => _settings.PolarAngle, SetPolarAngle));
            registry.AddVariable(
                CadSystemVariable.Integer("POLARMODE", 0, 15, () => _settings.PolarMode, SetPolarMode)
            );
            registry.AddVariable(
                CadSystemVariable.Text("POLARADDANG", () => _settings.PolarAddedAngles, SetPolarAddedAngles)
            );
            registry.AddVariable(CadSystemVariable.Integer("AUTOSNAP", 0, 63, Autosnap, SetAutosnap));
            registry.AddVariable(CadSystemVariable.Integer("DYNMODE", -3, 3, DynamicMode, SetDynamicMode));
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "PICKBOX",
                    1,
                    50,
                    () => _settings.PickboxPixels,
                    v => ChangeSettings(s => s.PickboxPixels = v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "APERTURE",
                    2,
                    50,
                    () => _settings.AperturePixels,
                    v => ChangeSettings(s => s.AperturePixels = v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "LWDISPLAY",
                    0,
                    1,
                    () => ShowLineWeights ? 1 : 0,
                    v => SetLineWeightDisplay(v != 0)
                )
            );
        }

        /// <summary>
        /// DYNMODE as AutoCAD reads it: 1 pointer input, 2 dimensional input, 3 both, the value made negative while DYN is off
        /// so that F12 brings the same back, and 0 all off, which F12 turns to 3. The presenter's one box of dynamic input
        /// shows both whichever of 1 to 3 is set.
        /// </summary>
        private int DynamicMode()
        {
            int kept = Math.Abs(_dynamicMode);
            return DynamicInput.IsOn ? (kept == 0 ? 3 : kept) : -kept;
        }

        /// <summary>DYNMODE set: DYN on for 1 to 3, off for 0 and below, the value kept for F12.</summary>
        private void SetDynamicMode(int value)
        {
            _dynamicMode = value;
            DynamicInput.IsOn = value > 0;
        }

        /// <summary>A switch of the status bar as a variable of 0 and 1.</summary>
        private CadSystemVariable ToggleVariable(string name, string toggle) =>
            CadSystemVariable.Integer(
                name,
                0,
                1,
                () => FindToggle(toggle).IsOn ? 1 : 0,
                v => FindToggle(toggle).IsOn = v != 0
            );

        /// <summary>OSMODE set: the snaps of its bits ticked, and OSNAP off where it holds 16384, as AutoCAD reads it.</summary>
        private void SetOsmode(int value)
        {
            SnapModes = (CadSnapModes)(value & (SnapsOff - 1));
            FindToggle("OSNAP").IsOn = (value & SnapsOff) == 0;
        }

        /// <summary>
        /// AUTOSNAP as AutoCAD reads it: 1 the marker, 2 the snap's tooltip, 4 the magnet, 8 POLAR, 16 OTRACK, 32 the tooltips
        /// of polar, tracking and ortho; 63 with everything on.
        /// </summary>
        private int Autosnap() =>
            (Snap.ShowsMarker ? 1 : 0)
            | (Snap.ShowsSnapTip ? 2 : 0)
            | (Snap.Magnet ? 4 : 0)
            | (FindToggle("POLAR").IsOn ? 8 : 0)
            | (FindToggle("OTRACK").IsOn ? 16 : 0)
            | (Snap.ShowsTrackingTip ? 32 : 0);

        /// <summary>AUTOSNAP set: the canvas draws the marker, the tips and the magnet by its bits 1, 2, 4 and 32, POLAR follows 8 and OTRACK 16.</summary>
        private void SetAutosnap(int value)
        {
            Snap.ShowsMarker = (value & 1) != 0;
            Snap.ShowsSnapTip = (value & 2) != 0;
            Snap.Magnet = (value & 4) != 0;
            Snap.ShowsTrackingTip = (value & 32) != 0;
            FindToggle("POLAR").IsOn = (value & 8) != 0;
            FindToggle("OTRACK").IsOn = (value & 16) != 0;
        }

        /// <summary>POLARMODE set: the snaps follow it and the window keeps it in the settings file, as POLARANG.</summary>
        private void SetPolarMode(int value)
        {
            ApplyPolarMode(value);
            ChangeSettings(s => s.PolarMode = value);
        }

        /// <summary>
        /// POLARMODE's bits the snaps follow: 1 polar angles counted from the last segment, 2 tracking on every polar angle, 4
        /// the additional angles of POLARADDANG, 8 points acquired only with Shift held.
        /// </summary>
        private void ApplyPolarMode(int value)
        {
            Snap.PolarFromLastSegment = (value & 1) != 0;
            Snap.TracksPolarAngles = (value & 2) != 0;
            Snap.AdditionalPolarAngles =
                (value & 4) != 0 && CadUnits.TryAngles(_settings.PolarAddedAngles, 0, out double[] added)
                    ? added
                    : Array.Empty<double>();
            Snap.AcquiresWithShift = (value & 8) != 0;
        }

        /// <summary>
        /// POLARADDANG set, as AutoCAD takes it: up to ten angles parted by semicolons, read in the drawing's angle units and
        /// kept from nought up to a whole turn, written as the drawing writes angles (AUNITS, AUPREC), so that "33.5;-10" is
        /// kept as "34;350" at AUPREC 0; the window keeps it in the settings file, and the snaps hold it while POLARMODE has 4.
        /// </summary>
        private bool SetPolarAddedAngles(string typed)
        {
            DxfDatabase database = _current?.Document.Database;
            int aunits = database?.Aunits ?? 0;

            if (!CadUnits.TryAngles(typed, aunits, out double[] angles))
            {
                return false;
            }

            int auprec = database?.Auprec ?? 0;
            string kept = string.Join(";", Array.ConvertAll(angles, a => CadUnits.Angle(a, aunits, auprec)));
            ChangeSettings(s => s.PolarAddedAngles = kept);
            ApplyPolarMode(_settings.PolarMode);
            return true;
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
        /// PICKBOX, APERTURE, LWDISPLAY, POLARANG, the increment of Drafting Settings, the command line's Input Settings; and
        /// when what the settings remember changes: a command used, a misspelling corrected, a file opened or pinned. The window
        /// keeps the settings in use in the settings file, as it keeps DYN and OSMODE.
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
