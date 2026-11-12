using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The system variables the window keeps, added to each command line beside those of the drawing: OSMODE and the status
    /// bar's toggles (ORTHOMODE, SNAPMODE, GRIDMODE, DYNMODE), the snap's SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL and
    /// SNAPISOPAIR, SNAPUNIT, GRIDUNIT and GRIDDISPLAY, written into the drawing's <c>*Active</c> viewport (in a layout's
    /// paper space into its viewport 1, through a viewport into that viewport, as AutoCAD 2021 keeps them), SNAPTYPE,
    /// POLARDIST, GRIDSTYLE and SNAPGRIDLEGACY of the settings file, AUTOSNAP, POLARANG, POLARMODE and POLARADDANG,
    /// PICKBOX, GRIPSIZE, GRIPMULTIFUNCTIONAL, APERTURE and GRIPDYNCOLOR of Global settings, INPUTSEARCHDELAY and INPUTSEARCHOPTIONFLAGS of the command line's
    /// Input Search Options, and LWDISPLAY. Each is set at once, with no step of undo; the status bar and the canvas
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
            registry.AddVariable(CadSystemVariable.Integer("SNAPMODE", 0, 2, SnapMode, SetSnapMode));
            registry.AddVariable(ToggleVariable("GRIDMODE", "GRID"));
            registry.AddVariable(CadSystemVariable.Point("SNAPUNIT", SnapUnit, TakeSnapUnit));
            registry.AddVariable(
                CadSystemVariable.Point("GRIDUNIT", () => new GeoPoint2(Layout.GridX, Layout.GridY), TakeGridUnit)
            );
            registry.AddVariable(
                CadSystemVariable.Distance("POLARDIST", () => _settings.PolarDistance, SetPolarDistance)
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "SNAPTYPE",
                    0,
                    1,
                    () => _settings.PolarSnap ? 1 : 0,
                    v => SetPolarSnap(v != 0)
                )
            );
            registry.AddVariable(CadSystemVariable.Integer("GRIDSTYLE", 0, 7, () => _settings.GridStyle, SetGridStyle));
            registry.AddVariable(CadSystemVariable.Integer("GRIDDISPLAY", 0, 15, () => GridDisplay, SetGridDisplay));
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "SNAPGRIDLEGACY",
                    0,
                    1,
                    () => SnapGridLegacy ? 1 : 0,
                    v => SetSnapGridLegacy(v != 0)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Point(
                    "SNAPBASE",
                    () => SnapBase,
                    v => ChangeDrafting("SNAPBASE", s => s.SnapBase = v, a => a.SnapBase = v, p => p.SnapBasePoint = v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Angle(
                    "SNAPANG",
                    () => SnapAngle,
                    v => ChangeDrafting("SNAPANG", s => s.SnapAngle = v, a => a.SnapAngle = v, p => p.SnapAngle = v)
                )
            );
            registry.AddVariable(CadSystemVariable.Integer("GRIDMAJOR", 1, 100, () => GridMajor, v => SetGridMajor(v)));
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "SNAPSTYL",
                    0,
                    1,
                    () => Layout.Isometric ? 1 : 0,
                    v => SetIsometricSnap(v != 0)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "SNAPISOPAIR",
                    0,
                    2,
                    () => Layout.Isoplane,
                    v => ChangeDrafting("SNAPISOPAIR", s => s.Isoplane = v, a => a.SnapPair = v, p => p.SnapIsoPair = v)
                )
            );
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
                    "GRIPSIZE",
                    1,
                    255,
                    () => _settings.GripSize,
                    v => ChangeSettings(s => s.GripSize = v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "GRIPMULTIFUNCTIONAL",
                    0,
                    3,
                    () => _settings.GripMultifunctional,
                    v => ChangeSettings(s => s.GripMultifunctional = v)
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
                    "INPUTSEARCHDELAY",
                    100,
                    10000,
                    () => _settings.AutoCompleteDelay,
                    v => ChangeSettings(s => s.AutoCompleteDelay = v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "INPUTSEARCHOPTIONFLAGS",
                    0,
                    31,
                    () => _settings.InputSearchFlags,
                    v => ChangeSettings(s => s.InputSearchFlags = v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "GRIPDYNCOLOR",
                    1,
                    255,
                    () => _settings.GripDynamicColor,
                    v => ChangeSettings(s => s.GripDynamicColor = v)
                )
            );
            registry.AddVariable(CadSystemVariable.Switch("LWDISPLAY", () => ShowLineWeights, SetLineWeightDisplay));
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "TABLEINDICATOR",
                    0,
                    1,
                    () => _settings.TableIndicator ? 1 : 0,
                    v => ChangeSettings(s => s.TableIndicator = v != 0)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Integer(
                    "SHORTCUTMENU",
                    0,
                    31,
                    () => _settings.ShortcutMenu,
                    v => ChangeSettings(s => s.ShortcutMenu = v)
                )
            );
        }

        /// <summary>
        /// SNAPMODE as AutoCAD 2021 reads it: 0 off, 1 on, and 2, which AutoCAD takes and reads back though its help lists only
        /// 0 and 1, kept while SNAP stays on as it was set, by each space apart as AutoCAD keeps it by viewport: model space,
        /// a layout's paper and each viewport worked through.
        /// </summary>
        private int SnapMode() =>
            !FindToggle("SNAP").IsOn ? 0
            : SnapModeTwo.Contains(SnapModeSpace()) ? 2
            : 1;

        /// <summary>SNAPMODE set: SNAP on for 1 and 2, off for 0, and 2 kept for the space shown alone.</summary>
        private void SetSnapMode(int value)
        {
            FindToggle("SNAP").IsOn = value != 0;

            if (value == 2)
            {
                SnapModeTwo.Add(SnapModeSpace());
            }
            else
            {
                SnapModeTwo.Remove(SnapModeSpace());
            }
        }

        /// <summary>The spaces of the drawing shown, or of the window where none is open, whose SNAPMODE was set to 2.</summary>
        private HashSet<string> SnapModeTwo => (_current?.Drafting ?? _windowDrafting).SnapModeTwo;

        /// <summary>
        /// The space whose SNAPMODE is read and set, as AutoCAD keeps it by viewport: the handle of the viewport whose snap the
        /// space shown has (the one worked through, or a layout's viewport 1), the layout's name for one with no viewport 1,
        /// empty for model space and the Block Editor.
        /// </summary>
        private string SnapModeSpace()
        {
            DxfViewport own = DraftingViewport();

            if (own != null)
            {
                return own.Handle;
            }

            CadSpace paper = PaperShown();
            return paper != null ? "Layout " + paper.Name : string.Empty;
        }

        /// <summary>
        /// SNAPUNIT as AutoCAD reads it: the drawing's snap spacing, the Y doubled while the snap is isometric, as AutoCAD keeps
        /// half the vertical spacing; SNAP Style Isometric 5 reads 8.6603,5.0000.
        /// </summary>
        private GeoPoint2 SnapUnit() => new GeoPoint2(Snap.GridX, Layout.Isometric ? 2.0 * Snap.GridY : Snap.GridY);

        /// <summary>
        /// SNAPUNIT set: both spacings above nought, kept as typed, so that 4,6 set while the snap is isometric reads 4,12 as
        /// AutoCAD's does; 0,5 or -1,1 refused.
        /// </summary>
        private bool TakeSnapUnit(GeoPoint2 value)
        {
            if (!(value.X > 0.0) || !(value.Y > 0.0))
            {
                return false;
            }

            SetSnapSpacing(value.X, value.Y);
            return true;
        }

        /// <summary>GRIDUNIT set: each spacing kept without its sign, as AutoCAD keeps -2,3 as 2,3; nought for the snap's.</summary>
        private bool TakeGridUnit(GeoPoint2 value)
        {
            SetGridSpacing(Math.Abs(value.X), Math.Abs(value.Y));
            return true;
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
        internal void SetPolarMode(int value)
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
        internal bool SetPolarAddedAngles(string typed)
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
        /// PICKBOX, GRIPSIZE, GRIPMULTIFUNCTIONAL, APERTURE, GRIPDYNCOLOR, LWDISPLAY, POLARANG, the increment of Drafting Settings, the command line's Input
        /// Settings; and when what the settings remember changes: a command used, a misspelling corrected, a file opened or
        /// pinned. The window keeps the settings in use in the settings file, as it keeps DYN and OSMODE.
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
