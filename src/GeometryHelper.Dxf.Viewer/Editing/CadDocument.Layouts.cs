using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The switch from one space to another, model space or a layout, as an undo step.</summary>
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
        public CadEditResult ShowSpace(CadSpace space)
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
                "LAYOUT",
                "CTAB",
                () => CurrentSpace?.Name,
                () => SetCurrentSpace(space),
                (document, forward) => document.SetCurrentSpace(forward ? space : from),
                false
            );
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
