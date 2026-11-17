using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>What UNDO's Control option lets the undo list keep, as AutoCAD 2021's UNDO Control does.</summary>
    internal enum CadUndoControl
    {
        /// <summary>Every step is kept, the default.</summary>
        All,

        /// <summary>U and UNDO are turned off, and the steps made while it is on cannot be undone once it is turned back to All.</summary>
        None,

        /// <summary>Only the last step is kept: UNDO takes back one command, and the one before it is gone.</summary>
        One,
    }

    /// <summary>
    /// UNDO's own settings and its groups: Control (All, None, One), Auto, Combine and Layer, and BEgin and End, which make the
    /// commands between them one step of U and REDO under the name GROUP.
    /// </summary>
    public sealed partial class CadDocument
    {
        private CadUndoControl _undoControl;

        // The steps up to this position cannot be undone: where UNDO Control None or One was set, and, in One, the step before the last.
        private int _undoLimit;
        private int _undoGroup;
        private int _undoGroupCount;

        /// <summary>Gets what UNDO Control keeps of the undo list.</summary>
        internal CadUndoControl UndoControl => _undoControl;

        /// <summary>Gets the number of steps UNDO Control None or One dropped from the undo list: those no undo reaches; nought while All has been on throughout.</summary>
        internal int UndoLimit => _undoLimit;

        /// <summary>
        /// Gets or sets UNDO Auto: whether a command made of several actions is one step. AutoCAD 2021 sets it with the menu macros
        /// that run several commands, which the presenter has none of, so it is kept and shown by UNDO and nothing more; true
        /// until set.
        /// </summary>
        internal bool UndoAuto { get; set; } = true;

        /// <summary>Gets or sets UNDO Control Combine: whether consecutive real-time zooms and pans are one step; true until set.</summary>
        internal bool UndoCombine { get; set; } = true;

        /// <summary>Gets or sets UNDO Control Layer, the layer dialog's operations combined; kept and shown, true until set.</summary>
        internal bool UndoLayer { get; set; } = true;

        /// <summary>Gets whether UNDO BEgin has opened a group that UNDO End has not closed.</summary>
        internal bool IsUndoGroupOpen => _undoGroup != 0;

        /// <summary>
        /// Sets what UNDO Control keeps. None and One drop the steps made so far (they cannot be undone, nor can those made while
        /// None is on once All is back) and turn Auto off; All turns it on, as AutoCAD 2021 shows it.
        /// </summary>
        /// <param name="control">The control.</param>
        internal void SetUndoControl(CadUndoControl control)
        {
            if (control != CadUndoControl.All || _undoControl == CadUndoControl.None)
            {
                _undoLimit = _position;
            }

            _undoControl = control;
            UndoAuto = control == CadUndoControl.All;

            if (control != CadUndoControl.All)
            {
                _undoGroup = 0;
            }
        }

        /// <summary>Under One only the last step is kept: the one before it cannot be undone.</summary>
        private void LimitUndo()
        {
            if (_undoControl == CadUndoControl.One)
            {
                _undoLimit = Math.Max(_undoLimit, _position - 1);
            }
        }

        /// <summary>
        /// UNDO BEgin: the steps made from now to UNDO End are a group, taken back and put again as one. Where a group is open
        /// already it is closed first, as AutoCAD 2021 does, and the new one begins.
        /// </summary>
        internal void BeginUndoGroup() => _undoGroup = ++_undoGroupCount;

        /// <summary>UNDO End: closes the group; nothing is said where none is open.</summary>
        internal void EndUndoGroup() => _undoGroup = 0;

        /// <summary>
        /// The steps an undo takes, the last first, and the position it leaves: the last step; the steps joined to it (F7 pressed
        /// after a LINE is taken back with it, as AutoCAD's U takes both); and, where the last step is one of a closed group, the
        /// whole of the group. While a group is open U takes one step of it at a time.
        /// </summary>
        private List<CadUndoStep> StepsToUndo(out int position)
        {
            position = _position;
            var undone = new List<CadUndoStep> { _steps[position - 1] };
            position--;

            while (position > UndoFloor)
            {
                CadUndoStep last = undone[undone.Count - 1];
                CadUndoStep before = _steps[position - 1];

                if (
                    !last.JoinsPrevious
                    && !(last.GroupId != 0 && last.GroupId != _undoGroup && before.GroupId == last.GroupId)
                )
                {
                    break;
                }

                undone.Add(before);
                position--;
            }

            return undone;
        }

        /// <summary>The steps a redo puts again, the first first, and the position it leaves: the next step, those joined to it, and the rest of its closed group.</summary>
        private List<CadUndoStep> StepsToRedo(out int position)
        {
            position = _position;
            var redone = new List<CadUndoStep> { _steps[position] };
            position++;

            while (position < _steps.Count)
            {
                CadUndoStep first = redone[0];
                CadUndoStep next = _steps[position];

                if (
                    !next.JoinsPrevious
                    && !(first.GroupId != 0 && first.GroupId != _undoGroup && next.GroupId == first.GroupId)
                )
                {
                    break;
                }

                redone.Add(next);
                position++;
            }

            return redone;
        }

        /// <summary>
        /// What UNDO writes for the next undo, as AutoCAD 2021 words it: the name of the step ("LINE"), or "GROUP" and the names
        /// of the steps of a group in the order they were made ("GROUP LINE CIRCLE LINE"); null where there is nothing to undo.
        /// </summary>
        internal string NextUndoLabel
        {
            get
            {
                if (!CanUndo)
                {
                    return null;
                }

                List<CadUndoStep> steps = StepsToUndo(out _);
                steps.Reverse();
                return LabelOf(steps);
            }
        }

        /// <summary>What UNDO and REDO write for the next redo, worded as <see cref="NextUndoLabel"/> words an undo; null where there is nothing to redo.</summary>
        internal string NextRedoLabel => CanRedo ? LabelOf(StepsToRedo(out _)) : null;

        /// <summary>The label of steps in the order they were made.</summary>
        private string LabelOf(List<CadUndoStep> steps)
        {
            // A step that is not of a group keeps the name it always had: a joined step carries the name of the one it joins.
            if (steps[0].GroupId == 0 || steps[0].GroupId == _undoGroup)
            {
                return steps[0].Name;
            }

            var names = new List<string> { "GROUP" };

            foreach (CadUndoStep step in steps)
            {
                names.Add(step.Name);
            }

            return string.Join(" ", names);
        }
    }
}
