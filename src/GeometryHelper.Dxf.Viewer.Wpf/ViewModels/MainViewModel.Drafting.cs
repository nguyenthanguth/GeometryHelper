using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The drafting toggles AutoCAD keeps in the drawing, so that each open drawing shows its own as AutoCAD 2021 does: SNAP
    /// and GRID with their spacing, base point, angle, major lines and isometric plane from the <c>*Active</c> viewport
    /// (SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL, SNAPISOPAIR, GRIDDISPLAY), ORTHO
    /// from <c>$ORTHOMODE</c> and the lineweights from <c>$LWDISPLAY</c>. A drawing that has no such value takes the
    /// window's: SNAP and ORTHO off, GRID and the lineweights of Global settings, a spacing of 10. Switched, each is written
    /// into the drawing, as AutoCAD saves it. A layout's paper space has a snap and a grid of their own, kept in the viewport
    /// 1 of its sheet as AutoCAD keeps them, and a viewport worked through (MSPACE) has its own, kept in it: GRID switched on
    /// or GRIDUNIT 3,3 set in Layout1 leaves model space's as it was. POLAR, OTRACK, OSNAP,
    /// DYN, SNAPTYPE, POLARDIST, GRIDSTYLE and SNAPGRIDLEGACY stay the window's, as AutoCAD keeps them in the registry.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // Set while the toggles are given a drawing's own, so that showing them writes nothing back.
        private bool _loadingDrafting;

        /// <summary>
        /// Gets whether lineweights are drawn: the <c>$LWDISPLAY</c> of the drawing shown, as LWDISPLAY sets it; where the
        /// drawing has none, or none is open, <see cref="GlobalSettings.ShowLineWeights"/>.
        /// </summary>
        public bool ShowLineWeights => _current?.Drafting.LineWeights ?? _settings.ShowLineWeights;

        /// <summary>
        /// Gets the spacing of the grid across: the GRIDUNIT of the space shown (model space's, a layout's viewport 1's or the
        /// viewport worked through), its snap spacing where that is nought, 10 with no drawing.
        /// </summary>
        public double GridSpacingX
        {
            get
            {
                if (_current == null)
                {
                    return 10.0;
                }

                DraftingState layout = Layout;
                return layout.GridX > 0.0 ? layout.GridX : layout.SnapX;
            }
        }

        /// <summary>
        /// Gets the spacing of the grid up: the GRIDUNIT of the space shown (model space's, a layout's viewport 1's or the
        /// viewport worked through), its snap spacing where that is nought, 10 with no drawing.
        /// </summary>
        public double GridSpacingY
        {
            get
            {
                if (_current == null)
                {
                    return 10.0;
                }

                DraftingState layout = Layout;
                return layout.GridY > 0.0 ? layout.GridY : layout.SnapY;
            }
        }

        /// <summary>
        /// Gets the point the snap and the grid are laid from, in the coordinates of the UCS: the drawing's SNAPBASE, the
        /// origin where it has none or none is open.
        /// </summary>
        public GeoPoint2 SnapBase => Layout.SnapBase;

        /// <summary>
        /// Gets the angle the snap, the grid and the crosshair are turned by, in radians from the X axis of the UCS: the
        /// drawing's SNAPANG, nought where it has none or none is open.
        /// </summary>
        public double SnapAngle => Layout.SnapAngle;

        /// <summary>
        /// Gets the plane of the drawing's isometric snap, its SNAPISOPAIR while SNAPSTYL is 1, which ortho, the crosshair and
        /// the grid follow; null for the rectangular snap of SNAPSTYL 0.
        /// </summary>
        public CadIsoplane? Isoplane => Layout.Isometric ? (CadIsoplane)Layout.Isoplane : (CadIsoplane?)null;

        /// <summary>Gets how many lines of the grid make one step between its major lines: the drawing's GRIDMAJOR, 5 where it has none.</summary>
        public int GridMajor => Layout.GridMajor;

        // The snap and grid of the window while no drawing is open, which the variables set then.
        private readonly DraftingState _windowDrafting = new DraftingState();

        /// <summary>
        /// The snap and grid of the space shown: through a viewport that viewport's, in a layout's paper space its viewport
        /// 1's (or the window's for the layout where it has none), elsewhere the drawing's; the window's where none is open.
        /// </summary>
        private DraftingState Layout
        {
            get
            {
                DxfViewport own = DraftingViewport();

                if (own != null)
                {
                    return ViewportDrafting(own);
                }

                CadSpace paper = PaperShown();
                return paper != null ? _current.Drafting.Paper(paper.Name) : _current?.Drafting ?? _windowDrafting;
            }
        }

        /// <summary>
        /// The viewport whose snap and grid the space shown has, as AutoCAD 2021 keeps them: the one worked through (MSPACE),
        /// or in a layout's paper space its viewport 1; null in model space, the Block Editor, or a layout with no viewport 1.
        /// </summary>
        private DxfViewport DraftingViewport()
        {
            CadDocument document = _current?.Document;
            CadSpace space = document?.CurrentSpace;

            if (space == null || space.IsModel || space.IsBlockEditor)
            {
                return null;
            }

            return document.ActiveViewport ?? SheetViewport(space);
        }

        /// <summary>The snap and grid a viewport keeps, read as the drawing's are from its <c>*Active</c> viewport.</summary>
        private static DraftingState ViewportDrafting(DxfViewport viewport)
        {
            GeoVector2 snap = viewport.SnapIncrement;
            GeoVector2 grid = viewport.GridIncrement;
            return new DraftingState
            {
                Snap = viewport.SnapOn,
                Grid = viewport.GridOn,
                SnapX = snap.X,
                SnapY = snap.Y,
                GridX = grid.X,
                GridY = grid.Y,
                SnapBase = viewport.SnapBasePoint,
                SnapAngle = viewport.SnapAngle,
                GridMajor = viewport.GridMajor,
                Isometric = viewport.SnapIsometric,
                Isoplane = viewport.SnapIsoPair,
                GridDisplay =
                    (viewport.GridBoundToLimits ? 0 : 1)
                    | (viewport.GridAdaptive ? 2 : 0)
                    | (viewport.GridSubdivisionRestricted ? 0 : 4)
                    | (viewport.GridFollow ? 8 : 0),
            };
        }

        /// <summary>GRIDDISPLAY written into a viewport, as AutoCAD keeps it in its flags.</summary>
        private static void WriteGridDisplay(DxfViewport viewport, int bits)
        {
            viewport.GridBoundToLimits = (bits & 1) == 0;
            viewport.GridAdaptive = (bits & 2) != 0;
            viewport.GridSubdivisionRestricted = (bits & 4) == 0;
            viewport.GridFollow = (bits & 8) != 0;
        }

        /// <summary>
        /// A value of the snap or the grid set where the space shown keeps it: in model space kept for the drawing shown and
        /// written into its <c>*Active</c> viewport, in a layout written into its viewport 1 or the viewport worked through, as
        /// AutoCAD saves it; the snaps, the grid and the crosshair follow at once.
        /// </summary>
        private void ChangeDrafting(
            string name,
            Action<DraftingState> change,
            Action<DxfViewportTableRecord> write,
            Action<DxfViewport> writeViewport
        )
        {
            DxfViewport own = DraftingViewport();

            if (own != null)
            {
                WriteToDrawing(name, () => writeViewport(own));
                RaiseDrafting();
                return;
            }

            change(Layout);

            if (_current?.Document != null && PaperShown() == null)
            {
                WriteToDrawing(name, () =>
                {
                    DxfViewportTableRecord active = ActiveViewport(_current.Document);

                    if (active != null)
                    {
                        write(active);
                    }
                });
            }

            RaiseDrafting();
        }

        /// <summary>
        /// F5 or Ctrl+E: the next isometric plane, Left, Top, Right and round again, written "&lt;Isoplane Top&gt;" as AutoCAD
        /// writes it; a command running goes on.
        /// </summary>
        private bool CycleIsoplane()
        {
            int next = (Layout.Isoplane + 1) % 3;
            ChangeDrafting("SNAPISOPAIR", s => s.Isoplane = next, v => v.SnapPair = next, v => v.SnapIsoPair = next);
            if (_current != null)
            {
                _current.Session.Write("<Isoplane " + (CadIsoplane)next + ">");
                SyncHistory();
            }

            return true;
        }

        /// <summary>The <c>*Active</c> viewport of a drawing, which holds its snap and grid; null where it has none.</summary>
        private static DxfViewportTableRecord ActiveViewport(CadDocument document)
        {
            DxfViewportTable table = document.Database.ViewportTable;
            return table.Has("*Active") ? table["*Active"] : null;
        }

        /// <summary>The toggles and spacings a drawing opened has: its own where it has them, the window's where it has none.</summary>
        private DraftingState ReadDrafting(CadDocument document)
        {
            var state = new DraftingState { Grid = _settings.Grid, LineWeights = _settings.ShowLineWeights };
            DxfViewportTableRecord active = ActiveViewport(document);

            if (active != null)
            {
                state.Snap = active.SnapEnabled;
                state.Grid = active.GridEnabled;
                state.SnapX = active.SnapIncrements.X;
                state.SnapY = active.SnapIncrements.Y;
                state.GridX = active.GridIncrements.X;
                state.GridY = active.GridIncrements.Y;
                state.SnapBase = active.SnapBase;
                state.SnapAngle = active.SnapAngle;
                state.GridMajor = active.GridMajor;
                state.Isometric = active.IsometricSnapEnabled;
                state.Isoplane = active.SnapPair;
                state.GridDisplay = active.GridDisplay;
            }

            if (document.Database.GetHeader("$ORTHOMODE") != null)
            {
                state.Ortho = document.Database.Orthomode;
            }

            if (document.Database.GetHeader("$LWDISPLAY") != null)
            {
                state.LineWeights = document.Database.LineWeightDisplay;
            }

            return state;
        }

        /// <summary>Settings applied: a drawing with no GRIDMODE or LWDISPLAY of its own takes theirs, as it took them when opened.</summary>
        private void FollowSettings(KnownFile file)
        {
            if (ActiveViewport(file.Document) == null)
            {
                file.Drafting.Grid = _settings.Grid;
            }

            if (file.Document.Database.GetHeader("$LWDISPLAY") == null)
            {
                file.Drafting.LineWeights = _settings.ShowLineWeights;
            }
        }

        /// <summary>
        /// Shows the toggles and spacings of the drawing now shown, writing nothing back into it: SNAP, GRID and the snap's
        /// spacing those of the space shown, ORTHO the drawing's.
        /// </summary>
        private void LoadDrafting()
        {
            DraftingState state = Layout;
            _loadingDrafting = true;

            try
            {
                FindToggle("SNAP").IsOn = state.Snap;
                FindToggle("GRID").IsOn = state.Grid;
                FindToggle("ORTHO").IsOn = _current.Drafting.Ortho;
                Snap.GridX = state.SnapX;
                Snap.GridY = state.SnapY;
            }
            finally
            {
                _loadingDrafting = false;
            }

            RaiseDrafting();
        }

        private void RaiseDrafting()
        {
            DraftingState layout = Layout;
            Snap.GridOrigin = layout.SnapBase;
            Snap.SnapAngle = layout.SnapAngle;
            Snap.Isoplane = Isoplane;
            Raise(nameof(SnapBase));
            Raise(nameof(SnapAngle));
            Raise(nameof(Isoplane));
            Raise(nameof(GridMajor));
            Raise(nameof(GridSpacingX));
            Raise(nameof(GridSpacingY));
            Raise(nameof(GridDisplay));
            Raise(nameof(ShowLineWeights));
        }

        /// <summary>SNAP, GRID or ORTHO switched: kept for the drawing shown and written into it, as AutoCAD saves it there.</summary>
        private void WriteDraftingToggle(StatusToggleViewModel toggle)
        {
            if (_loadingDrafting || _current?.Document == null)
            {
                return;
            }

            DraftingState state = _current.Drafting;
            DxfDatabase database = _current.Document.Database;

            switch (toggle.Name)
            {
                case "SNAP":
                    _snapModeTwo = false;

                    if (WriteSpaceToggle("SNAPMODE", v => v.SnapOn = toggle.IsOn, s => s.Snap = toggle.IsOn))
                    {
                        break;
                    }

                    state.Snap = toggle.IsOn;
                    WriteToDrawing("SNAPMODE", () =>
                    {
                        DxfViewportTableRecord active = ActiveViewport(_current.Document);

                        if (active != null)
                        {
                            active.SnapEnabled = toggle.IsOn;
                        }
                    });
                    break;
                case "GRID":
                    if (WriteSpaceToggle("GRIDMODE", v => v.GridOn = toggle.IsOn, s => s.Grid = toggle.IsOn))
                    {
                        break;
                    }

                    state.Grid = toggle.IsOn;
                    WriteToDrawing("GRIDMODE", () =>
                    {
                        DxfViewportTableRecord active = ActiveViewport(_current.Document);

                        if (active != null)
                        {
                            active.GridEnabled = toggle.IsOn;
                        }
                    });
                    break;
                case "ORTHO":
                    state.Ortho = toggle.IsOn;
                    WriteToDrawing("ORTHOMODE", () =>
                        WriteHeaderFlag(database, "$ORTHOMODE", toggle.IsOn, v => database.Orthomode = v)
                    );
                    break;
            }
        }

        /// <summary>
        /// SNAP or GRID switched where the space shown keeps its own, as AutoCAD 2021 does: written into the viewport worked
        /// through or the layout's viewport 1, or kept for the window where the layout has none; false in model space and the
        /// Block Editor, which keep the drawing's.
        /// </summary>
        private bool WriteSpaceToggle(string name, Action<DxfViewport> write, Action<DraftingState> keep)
        {
            DxfViewport own = DraftingViewport();

            if (own != null)
            {
                WriteToDrawing(name, () => write(own));
                return true;
            }

            CadSpace paper = PaperShown();

            if (paper == null)
            {
                return false;
            }

            keep(_current.Drafting.Paper(paper.Name));
            return true;
        }

        /// <summary>
        /// The layout whose paper space is shown, not through one of its viewports: its sheet has a snap and a grid of their
        /// own, as AutoCAD 2021 keeps them in a layout apart from model space's; null in model space, the Block Editor or
        /// through a viewport.
        /// </summary>
        private CadSpace PaperShown()
        {
            CadDocument document = _current?.Document;
            CadSpace space = document?.CurrentSpace;
            return space != null && !space.IsModel && !space.IsBlockEditor && document.ActiveViewport == null
                ? space
                : null;
        }

        /// <summary>The viewport 1 of a layout, which shows its sheet and holds its snap and grid; null where it has none.</summary>
        private static DxfViewport SheetViewport(CadSpace space) =>
            space.Layout?.GetViewports().FirstOrDefault(v => v.Number == 1 && !v.IsErased);

        /// <summary>
        /// The snap spacing set, by SNAP or Drafting Settings: the snaps step by it, and the space shown keeps it as its
        /// SNAPUNIT (model space's, a layout's viewport 1's or the viewport worked through), the grid following where its
        /// GRIDUNIT is nought.
        /// </summary>
        internal void SetSnapSpacing(double x, double y)
        {
            Snap.GridX = x;
            Snap.GridY = y;

            if (_current?.Document == null)
            {
                return;
            }

            ChangeDrafting(
                "SNAPUNIT",
                s =>
                {
                    s.SnapX = x;
                    s.SnapY = y;
                },
                a => a.SnapIncrements = new GeoVector2(x, y),
                v => v.SnapIncrement = new GeoVector2(x, y)
            );
        }

        /// <summary>The grid spacing as the drawing shown holds it (GRIDUNIT), nought along an axis that takes the snap's; 10 by 10 with none.</summary>
        internal GeoVector2 GridUnit => new GeoVector2(Layout.GridX, Layout.GridY);

        /// <summary>
        /// Gets how the drawing shown draws its grid, its GRIDDISPLAY: 1 beyond the limits, 2 adaptive, 4 subdivided as it
        /// zooms in, 8 following the dynamic UCS; 3 where it has none, or none is open.
        /// </summary>
        public int GridDisplay => Layout.GridDisplay;

        /// <summary>
        /// Gets where the grid is drawn as dots, AutoCAD's GRIDSTYLE of the settings in use: 1 model space, 2 the Block Editor,
        /// 4 the layouts; 0, lines everywhere, by default.
        /// </summary>
        public int GridStyle => _settings.GridStyle;

        /// <summary>
        /// The grid spacing set by Drafting Settings: kept for the drawing shown as its GRIDUNIT, nought along an axis taking
        /// the snap's spacing there, and the grid drawn by it.
        /// </summary>
        internal void SetGridSpacing(double x, double y) =>
            ChangeDrafting(
                "GRIDUNIT",
                s =>
                {
                    s.GridX = x;
                    s.GridY = y;
                },
                a => a.GridIncrements = new GeoVector2(x, y),
                v => v.GridIncrement = new GeoVector2(x, y)
            );

        /// <summary>SNAPSTYL set by Drafting Settings: the isometric snap on or off, its spacing kept as it is, as SNAPSTYL typed keeps it.</summary>
        internal void SetIsometricSnap(bool on) =>
            ChangeDrafting(
                "SNAPSTYL",
                s => s.Isometric = on,
                a => a.IsometricSnapEnabled = on,
                v => v.SnapIsometric = on
            );

        /// <summary>GRIDMAJOR set by Drafting Settings: a major line every so many lines of the grid.</summary>
        internal void SetGridMajor(int every) =>
            ChangeDrafting(
                "GRIDMAJOR",
                s => s.GridMajor = every,
                a => a.GridMajor = every,
                v => v.GridMajor = (short)every
            );

        /// <summary>GRIDDISPLAY set by Drafting Settings: kept for the space shown, as AutoCAD saves it there.</summary>
        internal void SetGridDisplay(int bits) =>
            ChangeDrafting(
                "GRIDDISPLAY",
                s => s.GridDisplay = bits,
                a => a.GridDisplay = bits,
                v => WriteGridDisplay(v, bits)
            );

        /// <summary>SNAPTYPE set by Drafting Settings: PolarSnap or the grid; the window keeps it in the settings file, as AutoCAD keeps it in the registry.</summary>
        internal void SetPolarSnap(bool on)
        {
            Snap.PolarSnap = on;
            ChangeSettings(s => s.PolarSnap = on);
        }

        /// <summary>POLARDIST set by Drafting Settings: the step of PolarSnap, nought for the snap X spacing; kept in the settings file.</summary>
        internal void SetPolarDistance(double distance)
        {
            Snap.PolarDistance = distance;
            ChangeSettings(s => s.PolarDistance = distance);
        }

        /// <summary>GRIDSTYLE set by Drafting Settings: kept in the settings file, as AutoCAD keeps it in the registry.</summary>
        internal void SetGridStyle(int bits)
        {
            ChangeSettings(s => s.GridStyle = bits);
            Raise(nameof(GridStyle));
        }

        /// <summary>
        /// LWDISPLAY set: the drawing shown draws its lineweights or not and keeps it in <c>$LWDISPLAY</c>, as AutoCAD saves it;
        /// with no drawing open, Global settings keep it, as before a drawing gives its own.
        /// </summary>
        private void SetLineWeightDisplay(bool on)
        {
            if (_current?.Document == null)
            {
                ChangeSettings(s => s.ShowLineWeights = on);
                Raise(nameof(ShowLineWeights));
                return;
            }

            _current.Drafting.LineWeights = on;
            DxfDatabase database = _current.Document.Database;
            WriteToDrawing("LWDISPLAY", () =>
                WriteHeaderFlag(database, "$LWDISPLAY", on, v => database.LineWeightDisplay = v)
            );
            Raise(nameof(ShowLineWeights));
        }

        /// <summary>
        /// A flag of the header set through the library, which checks the drawing's version. The library writes a flag only when
        /// its value changes, and reads one the header leaves out as off, so off is written to such a header by way of on, and
        /// the drawing holds it as AutoCAD's does.
        /// </summary>
        private static void WriteHeaderFlag(DxfDatabase database, string name, bool on, Action<bool> set)
        {
            if (!on && database.GetHeader(name) == null)
            {
                set(true);
            }

            set(on);
        }

        /// <summary>A value written into the drawing; one the drawing cannot hold, as LWDISPLAY in a file of R12, is kept for it in the window alone.</summary>
        private static void WriteToDrawing(string name, Action write)
        {
            try
            {
                write();
            }
            catch (InvalidOperationException exception)
            {
                GeometryHelperLog.Warn("MainViewModel: " + name + " could not be written to the drawing.", exception);
            }
        }

        /// <summary>The drafting toggles and spacings of one open drawing.</summary>
        private sealed class DraftingState
        {
            public bool Snap { get; set; }

            public bool Grid { get; set; }

            public bool Ortho { get; set; }

            public bool LineWeights { get; set; }

            public double SnapX { get; set; } = 10.0;

            public double SnapY { get; set; } = 10.0;

            public double GridX { get; set; } = 10.0;

            public double GridY { get; set; } = 10.0;

            public GeoPoint2 SnapBase { get; set; } = GeoPoint2.Origin;

            public double SnapAngle { get; set; }

            public int GridMajor { get; set; } = 5;

            public bool Isometric { get; set; }

            public int Isoplane { get; set; }

            public int GridDisplay { get; set; } = 3;

            // The paper-space snap and grid of the layouts, by name, that have no viewport 1 to keep them.
            private readonly Dictionary<string, DraftingState> _papers = new Dictionary<string, DraftingState>(
                StringComparer.OrdinalIgnoreCase
            );

            /// <summary>The snap and grid the window keeps for a layout's paper space where the layout has no viewport 1: grid off at first.</summary>
            public DraftingState Paper(string layout)
            {
                if (!_papers.TryGetValue(layout, out DraftingState paper))
                {
                    paper = new DraftingState();
                    _papers.Add(layout, paper);
                }

                return paper;
            }
        }
    }
}
