using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a command's body works with while it runs: the document, the answer to its last prompt, the objects selected, and
    /// the way to make its edit. One context lives for one run of a command.
    /// </summary>
    public sealed class CadCommandContext
    {
        private readonly IReadOnlyList<string> _preSelected;

        internal CadCommandContext(CadCommandSession session, CadCommand command, IReadOnlyList<string> preSelected)
        {
            Session = session;
            Command = command;
            _preSelected = preSelected;
            Input = CadInput.Nothing;
            Selected = Array.Empty<string>();
        }

        /// <summary>Gets the session the command runs in.</summary>
        public CadCommandSession Session { get; }

        /// <summary>Gets the command.</summary>
        public CadCommand Command { get; }

        /// <summary>Gets the document.</summary>
        public CadDocument Document => Session.Document;

        /// <summary>Gets the drawing as it stands now; a new object after an undo, so it is read again each time rather than kept.</summary>
        public DxfDatabase Database => Session.Document.Database;

        /// <summary>Gets the space the command works in: the document's working space, model space while a viewport of the layout shown is worked through.</summary>
        public CadSpace Space => Session.Document.WorkingSpace;

        /// <summary>
        /// Gets the progress meter of the status bar, for long work: <c>using (var meter = c.Progress.Start("Checking"))</c>,
        /// then <see cref="CadProgressMeter.SetLimit"/> and <see cref="CadProgressMeter.MeterProgress"/>.
        /// </summary>
        public CadProgress Progress => Session.Progress;

        /// <summary>Gets the answer to the last prompt.</summary>
        public CadInput Input { get; internal set; }

        /// <summary>Gets the objects <see cref="SelectObjects"/> gave, in ordinal order, those on locked layers left out.</summary>
        public IReadOnlyList<string> Selected { get; private set; }

        /// <summary>The objects selected before the command started, which BLOCK and WBLOCK put into their dialog's objects.</summary>
        internal IReadOnlyList<string> PreSelected => _preSelected;

        /// <summary>Gets or sets whether the document's selection is cleared when the command ends, as AutoCAD clears it after MOVE.</summary>
        public bool ClearsSelection { get; set; }

        /// <summary>
        /// The text a command run transparently gives the prompt it interrupted as its answer when it ends, as AutoCAD's 'CAL types
        /// its value there; null for none.
        /// </summary>
        internal string TransparentResult { get; set; }

        /// <summary>
        /// Asks "Select objects:" until Enter, unless objects were selected before the command started, which are taken
        /// instead; the result is <see cref="Selected"/>, those on a locked layer left out with "1 was on a locked layer." The
        /// body yields what this yields: <c>foreach (CadPrompt p in c.SelectObjects()) yield return p;</c>.
        /// </summary>
        /// <returns>The prompts to yield.</returns>
        public IEnumerable<CadPrompt> SelectObjects()
        {
            IReadOnlyList<string> handles;

            if (_preSelected.Count > 0)
            {
                handles = _preSelected;
                Session.RememberSelection(handles);
            }
            else
            {
                yield return CadPrompt.ForSelection();
                handles = Input.Handles;
            }

            Selected = Unlocked(handles);
            ClearsSelection = true;
        }

        /// <summary>
        /// Asks for objects of one kind, as <see cref="SelectObjects"/> does with its own words: the objects selected before the
        /// command started that <paramref name="only"/> accepts, else "Select hatch objects:" (the message given) until Enter,
        /// whatever else is picked left out. The result is <see cref="Selected"/>, those on a locked layer left out with "1 was on a
        /// locked layer."
        /// </summary>
        /// <param name="message">The message of the prompt.</param>
        /// <param name="only">Whether an object is of the kind asked for.</param>
        /// <returns>The prompts to yield.</returns>
        internal IEnumerable<CadPrompt> SelectObjectsOf(string message, Func<DxfEntity, bool> only)
        {
            var handles = new List<string>();

            foreach (string handle in _preSelected)
            {
                if (only(Document.FindEntity(handle)))
                {
                    handles.Add(handle);
                }
            }

            if (handles.Count > 0)
            {
                Session.RememberSelection(handles);
            }
            else
            {
                yield return CadPrompt.ForSelection(message);

                foreach (string handle in Input.Handles ?? Array.Empty<string>())
                {
                    if (only(Document.FindEntity(handle)))
                    {
                        handles.Add(handle);
                    }
                }
            }

            Selected = Unlocked(handles);
            ClearsSelection = true;
        }

        /// <summary>Writes a line into the history of the command line.</summary>
        /// <param name="line">The line.</param>
        public void Write(string line) => Session.Write(line);

        /// <summary>
        /// Makes an edit of the command through <see cref="CadDocument.ApplyEdit"/>; all the edits of one run are one undo step.
        /// A refusal or failure is written into the history.
        /// </summary>
        /// <param name="planned">The changes planned to existing entities; null or empty where it only adds.</param>
        /// <param name="apply">Writes the change.</param>
        /// <returns>What came of it.</returns>
        public CadEditResult Apply(IReadOnlyList<CadEditRecord> planned, Action<CadEditScope> apply) =>
            Session.Apply(Command.Name, planned, apply);

        /// <summary>
        /// Opens the command's undo step now, so that edits made through the document's own methods (<see cref="CadLayerEditor"/>,
        /// <see cref="CadGrips"/>, <see cref="CadPropertyEditor"/>) join it rather than being steps of their own.
        /// </summary>
        public void OpenUndoStep() => Session.EnsureGroup(Command.Name);

        /// <summary>
        /// Closes the command's undo step opened so far, so that its edits up to now are one step and those it makes next
        /// another, as TEXTEDIT makes one step of each text it edits.
        /// </summary>
        public void CloseUndoStep() => Session.CloseGroup();

        /// <summary>Gets a number kept from a run of a command before, as the radius CIRCLE offers next.</summary>
        /// <param name="key">The name it is kept under.</param>
        /// <param name="fallback">The value where none is kept.</param>
        /// <returns>The number.</returns>
        public double Remembered(string key, double fallback) =>
            Session.Remembered.TryGetValue(key, out double value) ? value : fallback;

        /// <summary>Keeps a number for the next run of a command.</summary>
        /// <param name="key">The name it is kept under.</param>
        /// <param name="value">The number.</param>
        public void Remember(string key, double value) => Session.Remembered[key] = value;

        /// <summary>Writes a length as the command line writes one: to 4 places.</summary>
        /// <param name="value">The length.</param>
        /// <returns>The text.</returns>
        public static string Length(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);

        /// <summary>The handles of entities not on a locked layer, saying how many were.</summary>
        private IReadOnlyList<string> Unlocked(IReadOnlyList<string> handles)
        {
            var kept = new List<string>();
            int locked = 0;

            // The objects outside the working set of a reference edited in place cannot be selected, as AutoCAD's REFEDIT locks them.
            Func<string, bool> held = Document.RefEditLockTest();

            foreach (string handle in handles)
            {
                DxfEntity entity = Document.FindEntity(handle);

                if (entity == null || (held != null && held(handle)))
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(Database, entity.Layer))
                {
                    locked++;
                    continue;
                }

                kept.Add(handle);
            }

            if (locked > 0)
            {
                Write(
                    locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer."
                );
            }

            kept.Sort(StringComparer.OrdinalIgnoreCase);
            return kept;
        }
    }
}
