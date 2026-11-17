using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What a command's body works with while it runs: the document, the answer to its last prompt, the objects selected, and
    /// the way to make its edit. One context lives for one run of a command.
    /// </summary>
    public sealed class CadCommandContext
    {
        private readonly IReadOnlyList<string> _preSelected;
        private bool _preSelectionTaken;

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

        /// <summary>The handles <see cref="SelectObjects"/> was given, before those of no entity and those on a locked layer were left out: the Block Editor's parameters and actions are among them.</summary>
        internal IReadOnlyList<string> RawSelected { get; private set; } = Array.Empty<string>();

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
        public IEnumerable<CadPrompt> SelectObjects() => SelectObjectsWithEcho(true);

        /// <summary>
        /// <see cref="SelectObjects"/> that says "N found" for the objects taken from before the command, as AutoCAD's commands
        /// do, or does not, as ALIGN, DRAWORDER and MIRROR3D take them without a word.
        /// </summary>
        /// <param name="echo">Whether "N found" is written for the objects selected before the command.</param>
        /// <returns>The prompts to yield.</returns>
        internal IEnumerable<CadPrompt> SelectObjectsWithEcho(bool echo)
        {
            IReadOnlyList<string> handles;

            if (_preSelected.Count > 0)
            {
                handles = _preSelected;
                Session.RememberSelection(handles);

                if (echo)
                {
                    Write(Found(handles.Count));
                }
            }
            else
            {
                yield return CadPrompt.ForSelection();
                handles = Input.Handles;
            }

            RawSelected = handles;
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
                Write(Found(handles.Count));
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

        /// <summary>
        /// Asks a command's first selection, unless objects were selected before the command started, which are taken instead as
        /// AutoCAD takes them for the commands that start with "Select objects": the prompt is not asked, "N found" is written
        /// (N the objects <paramref name="only"/> accepts), and <see cref="Input"/> is the selection as if it had been picked there,
        /// so that the body reads <c>c.Input.Handles</c> after it as it does after the prompt. Where none of the objects selected
        /// before is accepted, "0 found" is written and the prompt is asked. The objects taken are used once: a second call asks.
        /// The body yields what this yields: <c>foreach (CadPrompt p in c.SelectFirst(prompt)) yield return p;</c>.
        /// </summary>
        /// <param name="prompt">The prompt asked where nothing was selected before.</param>
        /// <param name="only">Whether an object is of the kind the command works on; null accepts every object.</param>
        /// <param name="echo">Whether "N found" is written for the objects taken.</param>
        /// <returns>The prompts to yield.</returns>
        internal IEnumerable<CadPrompt> SelectFirst(
            CadPrompt prompt,
            Func<DxfEntity, bool> only = null,
            bool echo = true
        )
        {
            IReadOnlyList<string> handles = TakePreSelection(only, echo);

            if (handles.Count > 0)
            {
                Input = new CadInput(CadInputKind.Selection, default, 0.0, null, handles);
                yield break;
            }

            yield return prompt;
        }

        /// <summary>
        /// Takes the objects selected before the command started that <paramref name="only"/> accepts, once, for a body that
        /// reads its objects its own way: they are remembered as the previous selection, the document's selection is cleared when
        /// the command ends (grips gone, as AutoCAD's), and "N found" is written.
        /// </summary>
        /// <param name="only">Whether an object is of the kind the command works on; null accepts every object.</param>
        /// <param name="echo">Whether "N found" is written.</param>
        /// <returns>The handles taken, in ordinal order; empty where nothing was selected before or none was accepted.</returns>
        internal IReadOnlyList<string> TakePreSelection(Func<DxfEntity, bool> only = null, bool echo = true)
        {
            if (_preSelected.Count == 0 || _preSelectionTaken)
            {
                return Array.Empty<string>();
            }

            _preSelectionTaken = true;
            var handles = new List<string>();

            foreach (string handle in _preSelected)
            {
                if (only == null || (Document.FindEntity(handle) is DxfEntity entity && only(entity)))
                {
                    handles.Add(handle);
                }
            }

            if (echo)
            {
                Write(Found(handles.Count));
            }

            if (handles.Count > 0)
            {
                Session.RememberSelection(handles);
            }

            ClearsSelection = true;
            return handles;
        }

        /// <summary>
        /// Asks a command's one object (PEDIT, SPLINEDIT, HATCHEDIT, TEXTEDIT), unless exactly one object was selected before the
        /// command started, which is taken instead without a word, as AutoCAD takes it: the prompt is not asked and
        /// <see cref="Input"/> is that object picked there. Where several were selected, or the one is not accepted, the prompt is
        /// asked and the selection is left as it is. The object is taken once: a second call asks.
        /// </summary>
        /// <param name="prompt">The prompt asked where no single object was selected before.</param>
        /// <param name="only">Whether an object is of the kind the command works on; null accepts every object.</param>
        /// <param name="filtering">
        /// Whether the objects <paramref name="only"/> accepts are counted (SPLINEDIT takes the one spline among a mixed selection)
        /// rather than every object selected (PEDIT takes one object, a line too, which it offers to turn into a polyline).
        /// </param>
        /// <returns>The prompts to yield.</returns>
        internal IEnumerable<CadPrompt> SelectOneFirst(
            CadPrompt prompt,
            Func<DxfEntity, bool> only = null,
            bool filtering = false
        )
        {
            var candidates = new List<string>();

            if (!_preSelectionTaken)
            {
                foreach (string handle in _preSelected)
                {
                    if (!filtering || only == null || (Document.FindEntity(handle) is DxfEntity entity && only(entity)))
                    {
                        candidates.Add(handle);
                    }
                }
            }

            if (
                candidates.Count == 1
                && Document.FindEntity(candidates[0]) is DxfEntity one
                && (filtering || only == null || only(one))
            )
            {
                _preSelectionTaken = true;
                Session.RememberSelection(candidates);
                ClearsSelection = true;
                Input = new CadInput(CadInputKind.Selection, default, 0.0, null, candidates);
                yield break;
            }

            yield return prompt;
        }

        /// <summary>"3 found", as AutoCAD counts the objects a selection took.</summary>
        private static string Found(int count) => count.ToString(CultureInfo.InvariantCulture) + " found";

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
