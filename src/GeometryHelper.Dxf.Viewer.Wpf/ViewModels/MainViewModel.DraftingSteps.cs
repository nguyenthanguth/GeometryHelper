using System;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The snap, grid and ORTHO changes of the window as undo steps, as AutoCAD 2021's U takes them back, probed in its
    /// console. GRID, SNAP, SETVAR and a variable typed alone (GRIDMODE, SNAPUNIT) are a step each, named by the command.
    /// F7, F9, F8 and F5 pressed at the prompt, and the status bar's buttons, join the step of the command before them, so that
    /// one U takes both back, and are a step of their own where no command came before. Such a step keeps no copy of the
    /// drawing: it holds the snap and grid of the viewport as they were before and after, and an undo or a redo of it writes
    /// those values back (<see cref="CadDocument.ApplySetting"/>), reading nothing again.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Runs a change of the snap, the grid or ORTHO that writes into the drawing shown and records it as an undo step; a
        /// change that touches nothing the drawing keeps (the sheet of a layout with no viewport, a window with no drawing) is
        /// only run.
        /// </summary>
        /// <param name="property">The variable that changes: SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL, SNAPISOPAIR, GRIDDISPLAY or ORTHOMODE.</param>
        /// <param name="joinsPrevious">Whether the change is made at the prompt by a key or a button, so that its step joins the step before it.</param>
        /// <param name="change">Makes the change, in the window and in the drawing.</param>
        private void RecordDrafting(string property, bool joinsPrevious, Action change)
        {
            CadDocument document = _current?.Document;

            if (document == null)
            {
                change();
                return;
            }

            bool ortho = string.Equals(property, "ORTHOMODE", StringComparison.Ordinal);
            DxfViewport own = ortho ? null : DraftingViewport();
            bool kept = ortho || own != null || (PaperShown() == null && ActiveViewport(document) != null);

            if (!kept)
            {
                change();
                return;
            }

            // A command running (GRID asking its spacing, SETVAR) is one step with what it changes here.
            if (_current.Session.IsRunning)
            {
                _current.Session.OpenUndoStep();
            }

            string handle = own?.Handle;
            DraftingState before = null;
            DraftingState after = null;

            string Read()
            {
                DraftingState state = ortho
                    ? new DraftingState { Ortho = document.Database.Orthomode }
                    : ReadSnapGrid(document, handle);

                if (before == null)
                {
                    before = state;
                }
                else
                {
                    after = state;
                }

                return FieldText(state, property);
            }

            document.ApplySetting(
                _current.Session.IsRunning ? _current.Session.Current.Name : property,
                property,
                Read,
                change,
                (doc, forward) =>
                {
                    DraftingState from = forward ? before : after;
                    DraftingState to = forward ? after : before;

                    if (ortho)
                    {
                        WriteToDrawing(property, () =>
                            WriteHeaderFlag(doc.Database, "$ORTHOMODE", to.Ortho, v => doc.Database.Orthomode = v)
                        );
                    }
                    else
                    {
                        WriteSnapGrid(doc, handle, from, to);
                    }
                },
                joinsPrevious
            );
        }

        /// <summary>Runs the changes of a dialog as the one step of a command, as AutoCAD's DSETTINGS is one; with no drawing, or inside a command, they are only run.</summary>
        /// <param name="name">The name of the step.</param>
        /// <param name="changes">Makes the changes.</param>
        internal void RunAsOneStep(string name, Action changes)
        {
            CadDocument document = _current?.Document;

            if (document == null)
            {
                changes();
                return;
            }

            document.BeginGroup(name);

            try
            {
                changes();
            }
            finally
            {
                document.EndGroup();
            }
        }

        /// <summary>The snap and grid a space keeps in the drawing: the viewport's, or the <c>*Active</c> viewport's where the handle is null.</summary>
        private static DraftingState ReadSnapGrid(CadDocument document, string handle)
        {
            if (handle != null)
            {
                return document.FindEntity(handle) is DxfViewport viewport
                    ? ViewportDrafting(viewport)
                    : new DraftingState();
            }

            DxfViewportTableRecord active = ActiveViewport(document);
            return active != null ? ActiveSnapGrid(active) : new DraftingState();
        }

        /// <summary>The snap and grid of the <c>*Active</c> viewport record, which model space keeps.</summary>
        private static DraftingState ActiveSnapGrid(DxfViewportTableRecord active) =>
            new DraftingState
            {
                Snap = active.SnapEnabled,
                Grid = active.GridEnabled,
                SnapX = active.SnapIncrements.X,
                SnapY = active.SnapIncrements.Y,
                GridX = active.GridIncrements.X,
                GridY = active.GridIncrements.Y,
                SnapBase = active.SnapBase,
                SnapAngle = active.SnapAngle,
                GridMajor = active.GridMajor,
                Isometric = active.IsometricSnapEnabled,
                Isoplane = active.SnapPair,
                GridDisplay = active.GridDisplay,
            };

        /// <summary>
        /// Writes into a drawing the values of the snap and grid that differ between two states, the <c>*Active</c> viewport's
        /// or a viewport's by its handle: an undo puts the values of before back, a redo those of after, and a value that
        /// stayed is not written, so that the file keeps the codes it had.
        /// </summary>
        private static void WriteSnapGrid(CadDocument document, string handle, DraftingState from, DraftingState to)
        {
            if (handle != null)
            {
                if (document.FindEntity(handle) is DxfViewport v)
                {
                    WriteToDrawing("SNAPMODE", () => WriteViewport(v, from, to));
                }

                return;
            }

            DxfViewportTableRecord a = ActiveViewport(document);

            if (a != null)
            {
                WriteToDrawing("SNAPMODE", () => WriteActive(a, from, to));
            }
        }

        private static void WriteActive(DxfViewportTableRecord a, DraftingState from, DraftingState to)
        {
            if (from.Snap != to.Snap)
            {
                a.SnapEnabled = to.Snap;
            }

            if (from.Grid != to.Grid)
            {
                a.GridEnabled = to.Grid;
            }

            if (Differs(from.SnapX, to.SnapX) || Differs(from.SnapY, to.SnapY))
            {
                a.SnapIncrements = new GeoVector2(to.SnapX, to.SnapY);
            }

            if (Differs(from.GridX, to.GridX) || Differs(from.GridY, to.GridY))
            {
                a.GridIncrements = new GeoVector2(to.GridX, to.GridY);
            }

            if (Differs(from.SnapBase.X, to.SnapBase.X) || Differs(from.SnapBase.Y, to.SnapBase.Y))
            {
                a.SnapBase = to.SnapBase;
            }

            if (Differs(from.SnapAngle, to.SnapAngle))
            {
                a.SnapAngle = to.SnapAngle;
            }

            if (from.GridMajor != to.GridMajor)
            {
                a.GridMajor = to.GridMajor;
            }

            if (from.Isometric != to.Isometric)
            {
                a.IsometricSnapEnabled = to.Isometric;
            }

            if (from.Isoplane != to.Isoplane)
            {
                a.SnapPair = to.Isoplane;
            }

            if (from.GridDisplay != to.GridDisplay)
            {
                a.GridDisplay = to.GridDisplay;
            }
        }

        private static void WriteViewport(DxfViewport v, DraftingState from, DraftingState to)
        {
            if (from.Snap != to.Snap)
            {
                v.SnapOn = to.Snap;
            }

            if (from.Grid != to.Grid)
            {
                v.GridOn = to.Grid;
            }

            if (Differs(from.SnapX, to.SnapX) || Differs(from.SnapY, to.SnapY))
            {
                v.SnapIncrement = new GeoVector2(to.SnapX, to.SnapY);
            }

            if (Differs(from.GridX, to.GridX) || Differs(from.GridY, to.GridY))
            {
                v.GridIncrement = new GeoVector2(to.GridX, to.GridY);
            }

            if (Differs(from.SnapBase.X, to.SnapBase.X) || Differs(from.SnapBase.Y, to.SnapBase.Y))
            {
                v.SnapBasePoint = to.SnapBase;
            }

            if (Differs(from.SnapAngle, to.SnapAngle))
            {
                v.SnapAngle = to.SnapAngle;
            }

            if (from.GridMajor != to.GridMajor)
            {
                v.GridMajor = (short)to.GridMajor;
            }

            if (from.Isometric != to.Isometric)
            {
                v.SnapIsometric = to.Isometric;
            }

            if (from.Isoplane != to.Isoplane)
            {
                v.SnapIsoPair = to.Isoplane;
            }

            if (from.GridDisplay != to.GridDisplay)
            {
                WriteGridDisplay(v, to.GridDisplay);
            }
        }

        /// <summary>Whether two values differ at all: a value put back is written exactly as it was read, so no tolerance applies.</summary>
        private static bool Differs(double a, double b) => !a.Equals(b);

        /// <summary>The value of a snap or grid variable in a state, as the step records it.</summary>
        private static string FieldText(DraftingState state, string property)
        {
            string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

            switch (property)
            {
                case "SNAPMODE":
                    return state.Snap ? "1" : "0";
                case "GRIDMODE":
                    return state.Grid ? "1" : "0";
                case "ORTHOMODE":
                    return state.Ortho ? "1" : "0";
                case "SNAPUNIT":
                    return Number(state.SnapX) + "," + Number(state.SnapY);
                case "GRIDUNIT":
                    return Number(state.GridX) + "," + Number(state.GridY);
                case "SNAPBASE":
                    return Number(state.SnapBase.X) + "," + Number(state.SnapBase.Y);
                case "SNAPANG":
                    return Number(state.SnapAngle);
                case "GRIDMAJOR":
                    return state.GridMajor.ToString(CultureInfo.InvariantCulture);
                case "SNAPSTYL":
                    return state.Isometric ? "1" : "0";
                case "SNAPISOPAIR":
                    return state.Isoplane.ToString(CultureInfo.InvariantCulture);
                default:
                    return state.GridDisplay.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
