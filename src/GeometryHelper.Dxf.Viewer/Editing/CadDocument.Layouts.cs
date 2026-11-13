using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The switch from one space to another, model space or a layout, and MSPACE and PSPACE, as undo steps.</summary>
    public sealed partial class CadDocument
    {
        /// <summary>
        /// Shows a space, model space or a layout, as a click on its tab does: one undo step named LAYOUT, which U takes back to
        /// the space shown before and REDO forward, as AutoCAD 2021's console undoes LAYOUT Set (probed: after switching from
        /// Model to Layout1 and to Layout2, two U took it back to Layout1 and to Model, each layout keeping the view it had).
        /// The step keeps no copy of the drawing, so an undo of it reads nothing again. The selection, which belongs to a space,
        /// is cleared by the switch and by its undo. Inside a command's group the switch joins the command's step.
        /// </summary>
        /// <param name="space">The space to show.</param>
        /// <returns>What came of it: done, with no record where the space is already shown or the document has none such;
        /// refused while the Block Editor is open.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space is null.</exception>
        public CadEditResult ShowSpace(CadSpace space) => ShowSpace(space, "LAYOUT");

        /// <summary>
        /// Shows a space as <see cref="ShowSpace(CadSpace)"/>, the step named as the command that made it: AutoCAD 2021's U
        /// says LAYOUT for LAYOUT Set, TILEMODE for TILEMODE and CTAB for CTAB or SETVAR CTAB.
        /// </summary>
        internal CadEditResult ShowSpace(CadSpace space, string name)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (_blockEditor != null)
            {
                return CadEditResult.Refuse("Not allowed in the Block Editor.");
            }

            CadSpace from = CurrentSpace;

            if (space == from || !_spaces.Contains(space))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return ApplySetting(
                name,
                "CTAB",
                () => CurrentSpace?.Name,
                () => SetCurrentSpace(space),
                (document, forward) => document.SetCurrentSpace(forward ? space : from),
                false
            );
        }

        /// <summary>
        /// MSPACE and PSPACE as AutoCAD 2021's console makes them: one undo step each, named as the command, that U takes back
        /// to the viewport worked through before (or the paper) and REDO forward. Probed: after MSPACE, PSPACE, MSPACE and
        /// PSPACE six U in turn undid each one, and a MSPACE that said "Already in model space.", a PSPACE that said "Already
        /// in paper space." and a MSPACE that said "There are no active Model space viewports." each made a step too, an
        /// empty one, so the step is recorded where nothing changes.
        /// </summary>
        /// <param name="handle">The handle of the viewport to work through, null for the paper.</param>
        /// <param name="name">The command's name, MSPACE or PSPACE.</param>
        internal void WorkThrough(string handle, string name)
        {
            CadSpace space = CurrentSpace;
            string from = space?.Through;

            RecordSetting(
                name,
                "CVPORT",
                () => space?.Through ?? string.Empty,
                () => SetThrough(handle),
                (document, forward) => document.SetThrough(forward ? handle : from),
                false,
                true
            );
        }

        /// <summary>Works through a viewport of the layout shown, or on its paper where the handle is null or no longer one that can be worked through.</summary>
        private void SetThrough(string handle)
        {
            if (handle == null || !EnterViewport(handle))
            {
                LeaveViewport();
            }
        }

        /// <summary>Makes a space of the document the one shown, the selection let go where it is another; a space the document no longer has is passed over.</summary>
        private void SetCurrentSpace(CadSpace space)
        {
            if (space == null || space == CurrentSpace || !_spaces.Contains(space))
            {
                return;
            }

            CurrentSpace = space;
            Selection.Clear();
        }
    }
}
