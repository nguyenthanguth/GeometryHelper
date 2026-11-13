using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Settings kept in the drawing and changed in place, as the snap and grid of a viewport, as undo steps. A setting is a
    /// handful of values in the drawing, and putting it back by those values is what its step does: the step shares the
    /// drawing kept before it, so that no copy of the drawing is saved for it and an undo or a redo of it reads nothing
    /// again, in a drawing of a million entities too (see <see cref="ApplySetting"/>).
    /// </summary>
    public sealed partial class CadDocument
    {
        /// <summary>
        /// Changes a setting kept in the drawing, one undo step, as AutoCAD 2021 records GRID, SNAP, SETVAR and a snap or grid
        /// variable typed alone: the step puts the setting back by what <paramref name="replay"/> writes, keeps no copy of the
        /// drawing, and undoing or redoing it reads the drawing again no more than a zoom does. Inside a command's group
        /// (<see cref="BeginGroup"/>) the change joins the command's step instead. A change that leaves
        /// <paramref name="read"/> giving the text it gave before makes no step.
        /// </summary>
        /// <remarks>
        /// <para>
        /// AutoCAD's U takes F7, F9 and F8 pressed at the prompt back with the command before them, one U for both under the
        /// name of that command, and as a step of their own where no command came before: pass
        /// <paramref name="joinsPrevious"/> true for such a change. The step then carries the name of the step before it, and
        /// <see cref="Undo"/> takes both back, <see cref="Redo"/> puts both again.
        /// </para>
        /// <para>
        /// The drawing is kept whole before the first step of the list, as for any edit, so <paramref name="change"/> is run
        /// after that. The steps after a step undone are forgotten, as after any edit.
        /// </para>
        /// </remarks>
        /// <param name="name">The name of the step: a command's name, as GRID, or "SETVAR".</param>
        /// <param name="property">The name of the setting as the records show it: GRIDMODE, SNAPUNIT.</param>
        /// <param name="read">Gives the setting as text, before the change and after it; the step records the two.</param>
        /// <param name="change">Writes the new value into the drawing.</param>
        /// <param name="replay">Puts the setting forward, called with true, or back, called with false, in the drawing the document
        /// shows at that time (an undo of another step reads the drawing again, so what it writes into is found afresh).</param>
        /// <param name="joinsPrevious">Whether the step is taken back and put again with the step before it.</param>
        /// <returns>What came of it: done, with no record where the setting did not change.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name, the property, <paramref name="read"/>, <paramref name="change"/> or <paramref name="replay"/> is null.</exception>
        public CadEditResult ApplySetting(
            string name,
            string property,
            Func<string> read,
            Action change,
            Action<CadDocument, bool> replay,
            bool joinsPrevious
        ) => RecordSetting(name, property, read, change, replay, joinsPrevious, false);

        /// <summary>
        /// Changes a setting as <see cref="ApplySetting"/>,
        /// with the step recorded where <paramref name="always"/> is true even though the setting reads the same after the
        /// change: AutoCAD 2021's console undoes MSPACE and PSPACE one U each where they only said "Already in paper space."
        /// </summary>
        internal CadEditResult RecordSetting(
            string name,
            string property,
            Func<string> read,
            Action change,
            Action<CadDocument, bool> replay,
            bool joinsPrevious,
            bool always
        )
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            if (_groupFailed)
            {
                return CadEditResult.Refuse("The command was taken back after an edit of it failed.");
            }

            EnsureStart();
            _shownState = null;
            string before = read();
            change();
            string after = read();

            if (!always && string.Equals(before, after, StringComparison.Ordinal))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            var record = new CadEditRecord(CadEditRecordKind.SettingChanged, null, property, before, after)
            {
                Replay = replay,
            };
            var records = new List<CadEditRecord> { record };

            if (_groupDepth > 0)
            {
                _groupRecords.AddRange(records);
            }
            else
            {
                CommitView(name, records, joinsPrevious);
            }

            Announce(CadChangeKind.Edit, name, records);
            return CadEditResult.Done(records);
        }

        /// <summary>
        /// Whether a step changes the view of spaces and settings kept in the drawing and nothing else, so that it shares the
        /// state before it: every record is the view of a space or has a way to put its setting back.
        /// </summary>
        private static bool SharesState(IReadOnlyList<CadEditRecord> records)
        {
            if (records.Count == 0)
            {
                return false;
            }

            foreach (CadEditRecord record in records)
            {
                if (!IsSpaceView(record) && record.Replay == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether a list of records holds a setting that is put back by its own replay.</summary>
        private static bool HasSettings(IReadOnlyList<CadEditRecord> records)
        {
            foreach (CadEditRecord record in records)
            {
                if (record.Replay != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Puts the settings of steps undone or redone back in the drawing shown: the old values from the last record back, or the new ones from the first on.</summary>
        private void MoveSettings(IReadOnlyList<CadEditRecord> records, bool undo)
        {
            for (int k = 0; k < records.Count; k++)
            {
                CadEditRecord record = records[undo ? records.Count - 1 - k : k];

                if (record.Replay != null)
                {
                    _shownState = null;
                    record.Replay(this, !undo);
                }
            }
        }

        /// <summary>
        /// The index of the first step of the run of steps that share the state kept at the position, the ones since the last
        /// step that kept the drawing whole (or the start): their settings are not in that state.
        /// </summary>
        private int SettingsRunStart()
        {
            int first = _position;

            while (first > 0 && SharesState(_steps[first - 1].Records))
            {
                first--;
            }

            return first;
        }

        /// <summary>Whether a step before the position changed a setting that the state kept at the position lacks.</summary>
        private bool HasSettingsRun()
        {
            for (int i = SettingsRunStart(); i < _position; i++)
            {
                if (HasSettings(_steps[i].Records))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Puts on the drawing just read from the state kept at the position the settings changed since that state, in the
        /// order they were changed: a step of settings alone keeps no drawing of its own, so the drawing read back for an undo
        /// or a redo of another step lacks its settings.
        /// </summary>
        private void ReplaySettings()
        {
            int start = SettingsRunStart();

            for (int i = start; i < _position; i++)
            {
                foreach (CadEditRecord record in _steps[i].Records)
                {
                    if (record.Replay != null)
                    {
                        _shownState = null;
                        record.Replay(this, true);
                    }
                }
            }
        }
    }
}
