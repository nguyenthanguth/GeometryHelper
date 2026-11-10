using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The drafting toggles AutoCAD keeps in the drawing, so that each open drawing shows its own as AutoCAD 2021 does: SNAP
    /// and GRID with their spacing from the <c>*Active</c> viewport (SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT), ORTHO from
    /// <c>$ORTHOMODE</c> and the lineweights from <c>$LWDISPLAY</c>. A drawing that has no such value takes the window's:
    /// SNAP and ORTHO off, GRID and the lineweights of Global settings, a spacing of 10. Switched, each is written into the
    /// drawing, as AutoCAD saves it. POLAR, OTRACK, OSNAP and DYN stay the window's, as AutoCAD keeps them in the registry.
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

        /// <summary>Gets the spacing of the grid across: the drawing's GRIDUNIT, its snap spacing where that is nought, 10 with no drawing.</summary>
        public double GridSpacingX => _current == null ? 10.0 : _current.Drafting.GridX > 0.0 ? _current.Drafting.GridX : _current.Drafting.SnapX;

        /// <summary>Gets the spacing of the grid up: the drawing's GRIDUNIT, its snap spacing where that is nought, 10 with no drawing.</summary>
        public double GridSpacingY => _current == null ? 10.0 : _current.Drafting.GridY > 0.0 ? _current.Drafting.GridY : _current.Drafting.SnapY;

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

        /// <summary>Shows the toggles and spacings of the drawing now shown, writing nothing back into it.</summary>
        private void LoadDrafting()
        {
            DraftingState state = _current.Drafting;
            _loadingDrafting = true;

            try
            {
                FindToggle("SNAP").IsOn = state.Snap;
                FindToggle("GRID").IsOn = state.Grid;
                FindToggle("ORTHO").IsOn = state.Ortho;
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
            Raise(nameof(GridSpacingX));
            Raise(nameof(GridSpacingY));
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
                    WriteToDrawing("ORTHOMODE", () => WriteHeaderFlag(database, "$ORTHOMODE", toggle.IsOn, v => database.Orthomode = v));
                    break;
            }
        }

        /// <summary>
        /// The snap spacing set, by SNAP or Drafting Settings: the snaps step by it, and the drawing shown keeps it as its
        /// SNAPUNIT, the grid following where its GRIDUNIT is nought.
        /// </summary>
        internal void SetSnapSpacing(double x, double y)
        {
            Snap.GridX = x;
            Snap.GridY = y;

            if (_current?.Document == null)
            {
                return;
            }

            _current.Drafting.SnapX = x;
            _current.Drafting.SnapY = y;
            WriteToDrawing("SNAPUNIT", () =>
            {
                DxfViewportTableRecord active = ActiveViewport(_current.Document);

                if (active != null)
                {
                    active.SnapIncrements = new GeoVector2(x, y);
                }
            });
            RaiseDrafting();
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
            WriteToDrawing("LWDISPLAY", () => WriteHeaderFlag(database, "$LWDISPLAY", on, v => database.LineWeightDisplay = v));
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
        }
    }
}
