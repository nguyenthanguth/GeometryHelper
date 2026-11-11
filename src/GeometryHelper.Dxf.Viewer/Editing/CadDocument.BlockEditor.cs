using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Block Editor, as AutoCAD 2021's BEDIT: a block of the drawing (or model space itself, as &lt;Current Drawing&gt;)
    /// shown alone in a space of its own, where every drawing and modify command works on the block's entities.
    /// </summary>
    /// <remarks>
    /// The edits are written into the block at once and each is a step of undo, as in AutoCAD; undo stops at the state the
    /// block was opened or last saved in. Save Block (BSAVE) makes them final: the steps of the session so far become one,
    /// "BEDIT", and every INSERT of the block is drawn anew; closing without saving reads the drawing back as it stood when the
    /// block was opened or last saved. After the editor closes, one undo takes back the whole session. A door of 4 objects
    /// opened, given a fifth line and saved, is one step "BEDIT" that every INSERT of the door shows with its five.
    /// </remarks>
    public sealed partial class CadDocument
    {
        private CadBlockEditor _blockEditor;

        /// <summary>Raised when the Block Editor opens, closes, or turns to another block (Save Block As).</summary>
        public event EventHandler BlockEditorChanged;

        /// <summary>Gets the Block Editor open on this drawing; null where none is.</summary>
        public CadBlockEditor BlockEditor => _blockEditor;

        /// <summary>The step undo stops at: where the Block Editor opened or last saved, else the first.</summary>
        private int UndoFloor => _blockEditor?.Floor ?? 0;

        /// <summary>The number of steps done, which undo takes back one by one.</summary>
        internal int UndoPosition => _position;

        /// <summary>The spaces of the tabs and, while it is open, the Block Editor's, which edits find by name as the others.</summary>
        private List<CadSpace> AllSpaces
        {
            get
            {
                var all = new List<CadSpace>(_spaces);

                if (_blockEditor != null && !all.Contains(_blockEditor.Space))
                {
                    all.Add(_blockEditor.Space);
                }

                return all;
            }
        }

        /// <summary>The space of a name, the Block Editor's among them; null for none or a null name.</summary>
        private CadSpace SpaceNamed(string name) =>
            name == null ? null : AllSpaces.Find(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Opens the Block Editor on a block, as BEDIT: the block shown alone in a space of its own, made the current space;
        /// a name the drawing has no block of makes a new, empty block, its origin at (0, 0); &lt;Current Drawing&gt; opens
        /// model space itself. The selection is let go.
        /// </summary>
        /// <param name="name">The name of the block, or <see cref="CadBlockEditor.CurrentDrawing"/>.</param>
        /// <returns>What came of it: refused where the editor is open already ("Not allowed in the Block Editor."), a command's
        /// group of edits is open, the name is not one AutoCAD takes, or it names a layout, an external reference or an
        /// anonymous block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadEditResult OpenBlockEditor(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.Trim();

            if (_blockEditor != null)
            {
                return CadEditResult.Refuse(CadBlockEditor.NotAllowed);
            }

            if (_groupDepth > 0)
            {
                return CadEditResult.Refuse("A command is running: end it first.");
            }

            bool whole = string.Equals(name, CadBlockEditor.CurrentDrawing, StringComparison.OrdinalIgnoreCase);

            if (!whole && !CadCommands.IsBlockName(name))
            {
                return CadEditResult.Refuse("Invalid block name.");
            }

            bool exists = !whole && Database.BlockTable.Has(name);

            if (exists)
            {
                DxfBlockTableRecord found = Database.BlockTable[name];

                if (
                    found.IsLayout
                    || found.IsAnonymous
                    || found.IsFromExternalReference
                    || found.Name.StartsWith("*", StringComparison.Ordinal)
                )
                {
                    return CadEditResult.Refuse("The block " + found.Name + " cannot be edited in the Block Editor.");
                }

                name = found.Name;
            }

            EnsureStart();
            int start = _position;

            if (!whole && !exists)
            {
                string made = name;
                CadEditResult result = ApplyEdit(
                    "BEDIT",
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New block", null, made) },
                    s =>
                    {
                        var record = new DxfBlockTableRecord { Name = made, Origin = new GeoPoint3(0.0, 0.0, 0.0) };
                        s.Database.BlockTable.Add(record);
                    }
                );

                if (!result.Succeeded)
                {
                    return result;
                }
            }

            CadSpace space = whole
                ? ModelSpace
                : new CadSpace(this, CadBlockEditor.SpacePrefix + name, true, Database.BlockTable[name], null);
            Show(
                new CadBlockEditor(
                    this,
                    whole ? CadBlockEditor.CurrentDrawing : name,
                    space,
                    !whole && !exists,
                    start,
                    _position,
                    CurrentSpace
                )
            );
            GeometryHelperLog.Info("CadDocument: the Block Editor opened " + _blockEditor.Name + ".");
            return CadEditResult.Done(Array.Empty<CadEditRecord>());
        }

        /// <summary>
        /// Shows another visibility state of the block open in the Block Editor, as AutoCAD's Visibility State box: the
        /// entities the state hides are no longer drawn or picked, those it shows and those no state lists are. A view of the
        /// block, not an edit of it: nothing is written and no undo step made.
        /// </summary>
        /// <param name="state">The name of the state, one of <see cref="CadBlockEditor.VisibilityStates"/>.</param>
        /// <returns>What came of it: refused where the editor is not open or the block has no state of the name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadEditResult SetBlockEditorVisibilityState(string state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            CadBlockEditor editor = _blockEditor;

            if (editor == null)
            {
                return CadEditResult.Refuse("The Block Editor is not open.");
            }

            string found = null;

            foreach (string name in editor.VisibilityStates)
            {
                if (string.Equals(name, state.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    found = name;
                }
            }

            if (found == null)
            {
                return CadEditResult.Refuse("The block " + editor.Name + " has no visibility state " + state + ".");
            }

            editor.ShowState(found);
            _displays.Remove(editor.Space);
            _indexes.Remove(editor.Space);
            _stale.Remove(editor.Space);
            Selection.Clear();
            Changed?.Invoke(
                this,
                new CadDocumentChangedEventArgs(
                    CadChangeKind.Regen,
                    null,
                    Array.Empty<string>(),
                    new[] { editor.Space },
                    false
                )
            );
            return CadEditResult.Done(Array.Empty<CadEditRecord>());
        }

        /// <summary>
        /// The handles of the entities of a block a space draws that the Block Editor's visibility state shows and hides, as
        /// <see cref="CadBlockEditor.StateSets"/> gives them; false where the block is not the one open or has no state.
        /// </summary>
        internal bool StateSetsOf(DxfBlockTableRecord block, out HashSet<string> shown, out HashSet<string> hidden)
        {
            shown = null;
            hidden = null;
            return _blockEditor != null
                && ReferenceEquals(_blockEditor.Space.Block, block)
                && _blockEditor.StateSets(out shown, out hidden);
        }

        /// <summary>
        /// Saves the block open in the Block Editor, as BSAVE: its edits made final, every INSERT of it drawn anew (their
        /// attributes are not synchronised, as AutoCAD's ATTSYNC is not run), and the steps of the session so far one undo
        /// step, "BEDIT". Undo inside the editor then stops here. For &lt;Current Drawing&gt; model space keeps its edits.
        /// </summary>
        /// <returns>What came of it: refused where the editor is not open or a command's group of edits is; done with no
        /// record where nothing was changed since the block was opened or last saved.</returns>
        public CadEditResult SaveBlock()
        {
            string refused = BlockEditorRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;

            if (!editor.HasUnsavedChanges)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            var redrawn = new CadEditRecord(CadEditRecordKind.TableChanged, null, "Redefine block", null, editor.Name);
            Merge(editor.Start, "BEDIT", redrawn);
            editor.Saved();
            Announce(CadChangeKind.Edit, "BSAVE", new[] { redrawn });
            GeometryHelperLog.Info("CadDocument: the Block Editor saved " + editor.Name + ".");
            return CadEditResult.Done(new[] { redrawn });
        }

        /// <summary>
        /// Saves the content of the Block Editor as a new block, as BSAVEAS: the block opened is read back as it was opened or
        /// last saved, the new one holds the content as it is now with the base point, unit, description and flags of the block
        /// opened, and the editor goes on with the new block. The session stays one undo step.
        /// </summary>
        /// <param name="name">The name of the new block.</param>
        /// <returns>What came of it: refused where the editor is not open, a command's group of edits is open, the name is not
        /// one AutoCAD takes or a block of it exists already.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadEditResult SaveBlockAs(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string refused = BlockEditorRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            name = name.Trim();

            if (!CadCommands.IsBlockName(name))
            {
                return CadEditResult.Refuse("Invalid block name.");
            }

            if (Database.BlockTable.Has(name))
            {
                return CadEditResult.Refuse("Block \"" + name + "\" already exists.");
            }

            CadBlockEditor editor = _blockEditor;

            // The content as it stands, kept in the drawing as it is now, which the one read back no longer holds.
            DxfDatabase edited = Database;
            List<DxfEntity> content = editor.Space.Block.Where(e => !e.IsErased).ToList();
            GeoPoint3 origin = editor.IsCurrentDrawing ? Database.Insbase : editor.Space.Block.Origin;
            DxfBlockTableRecord from = editor.IsCurrentDrawing ? null : editor.Space.Block;
            string description = from?.Comments ?? string.Empty;
            DxfUnits units = from?.Units ?? Database.Insunits;
            bool explodable = from?.Explodable ?? true;
            DxfBlockScaling scaling = from?.BlockScaling ?? DxfBlockScaling.Any;

            Discard(editor.DiscardTarget);
            editor.Space.IsBlockEditor = false;
            CadEditResult result = ApplyEdit(
                "BSAVEAS",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New block", null, name) },
                s =>
                {
                    var record = new DxfBlockTableRecord { Name = name, Origin = origin };
                    s.Database.BlockTable.Add(record);
                    KeepValue(() => record.Comments = description, description.Length > 0);
                    KeepValue(() => record.Units = units, record.Units != units);
                    KeepValue(() => record.Explodable = explodable, record.Explodable != explodable);
                    KeepValue(() => record.BlockScaling = scaling, record.BlockScaling != scaling);

                    if (content.Count > 0)
                    {
                        edited.WblockCloneObjects(content, record);
                    }
                }
            );

            if (!result.Succeeded)
            {
                // The block opened is shown again as it was read back; a new one never saved is gone, and the editor with it.
                if (editor.IsCurrentDrawing || Database.BlockTable.Has(editor.Name))
                {
                    Show(
                        new CadBlockEditor(
                            this,
                            editor.Name,
                            editor.IsCurrentDrawing ? ModelSpace : SpaceOf(editor.Name),
                            false,
                            editor.Start,
                            _position,
                            editor.Previous
                        )
                    );
                }
                else
                {
                    Leave(editor, false);
                }

                return result;
            }

            var space = new CadSpace(this, CadBlockEditor.SpacePrefix + name, true, Database.BlockTable[name], null);
            var saved = new CadBlockEditor(this, name, space, false, editor.Start, _position, editor.Previous);
            var redrawn = new CadEditRecord(CadEditRecordKind.TableChanged, null, "New block", null, name);
            Merge(editor.Start, "BEDIT", redrawn);
            saved.Saved();
            Show(saved);
            Announce(CadChangeKind.Edit, "BSAVEAS", new[] { redrawn });
            GeometryHelperLog.Info("CadDocument: the Block Editor saved " + editor.Name + " as " + name + ".");
            return CadEditResult.Done(new[] { redrawn });
        }

        /// <summary>
        /// Closes the Block Editor, as BCLOSE: the block saved first (<see cref="SaveBlock"/>) where asked, else read back as it
        /// was opened or last saved (a new block never saved not made at all); the space shown before BEDIT shown again.
        /// </summary>
        /// <param name="save">Whether the changes are saved; false discards them.</param>
        /// <returns>What came of it: refused where the editor is not open or a command's group of edits is.</returns>
        public CadEditResult CloseBlockEditor(bool save)
        {
            string refused = BlockEditorRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;
            CadEditResult result = CadEditResult.Done(Array.Empty<CadEditRecord>());

            if (save)
            {
                result = SaveBlock();
            }
            else
            {
                Discard(editor.DiscardTarget);
            }

            Leave(editor, save);
            return result;
        }

        /// <summary>Takes the Block Editor down: the space shown before BEDIT shown again, the selection let go.</summary>
        private void Leave(CadBlockEditor editor, bool saved)
        {
            editor.Space.IsBlockEditor = false;
            _blockEditor = null;
            _displays.Remove(editor.Space);
            _indexes.Remove(editor.Space);
            _stale.Remove(editor.Space);
            _byHandle = null;
            Selection.Clear();
            CurrentSpace = _spaces.Contains(editor.Previous) ? editor.Previous : ModelSpace;

            // Undo reaches past where the editor opened again: the drawing it goes to is read ahead, as after a step.
            ReadAround();
            GeometryHelperLog.Info(
                "CadDocument: the Block Editor closed " + editor.Name + (saved ? ", saved." : ", not saved.")
            );
            BlockEditorChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(
                this,
                new CadDocumentChangedEventArgs(
                    CadChangeKind.Regen,
                    null,
                    Array.Empty<string>(),
                    AllSpaces.ToArray(),
                    false
                )
            );
        }

        /// <summary>Why the Block Editor cannot save or close now; null where it can.</summary>
        private string BlockEditorRefused()
        {
            if (_blockEditor == null)
            {
                return "The Block Editor is not open.";
            }

            return _groupDepth > 0 ? "A command is running: end it first." : null;
        }

        /// <summary>Makes an editor the one shown: its space current, the selection let go, the index of handles made again.</summary>
        private void Show(CadBlockEditor editor)
        {
            _blockEditor = editor;
            editor.Space.IsBlockEditor = true;
            _displays.Remove(editor.Space);
            _indexes.Remove(editor.Space);
            _stale.Remove(editor.Space);
            _byHandle = null;
            Selection.Clear();
            CurrentSpace = editor.Space;
            BlockEditorChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>A new space of the Block Editor over the block of a name in the drawing as it stands.</summary>
        private CadSpace SpaceOf(string name) =>
            new CadSpace(this, CadBlockEditor.SpacePrefix + name, true, Database.BlockTable[name], null);

        /// <summary>Points the Block Editor's space at its block in a drawing read again, as undo reads it; left as it is where the drawing has no such block.</summary>
        private void RebindBlockEditor(DxfDatabase database)
        {
            CadBlockEditor editor = _blockEditor;

            if (editor != null && !editor.IsCurrentDrawing && database.BlockTable.Has(editor.Name))
            {
                editor.Space.Rebind(database.BlockTable[editor.Name], null);
            }
        }

        /// <summary>
        /// Makes the steps from one on, up to the one done last, one step of a name, the record given added to theirs: the
        /// drawing before the first and after the last kept, those between forgotten, the steps undone before forgotten too.
        /// </summary>
        private void Merge(int from, string name, CadEditRecord added)
        {
            DropRedo();
            var records = new List<CadEditRecord>();

            for (int i = from; i < _position; i++)
            {
                records.AddRange(_steps[i].Records);
            }

            records.Add(added);
            int count = _position - from;

            if (count == 0)
            {
                Commit(name, records);
                return;
            }

            _steps.RemoveRange(from, count);
            _steps.Add(new CadUndoStep(name, records.ToArray()));
            _states.RemoveRange(from + 1, count - 1);

            if (_savedPosition == _position)
            {
                _savedPosition = from + 1;
            }
            else if (_savedPosition > from)
            {
                _savedPosition = -1;
            }

            _position = from + 1;

            // The drawing the next undo goes to is now the one before the first step merged: read it ahead, as after a step.
            ReadAround();
        }

        /// <summary>Reads the drawing back as it stood after a step, the steps after it forgotten, as the Block Editor's changes discarded.</summary>
        private void Discard(int target)
        {
            DropRedo();

            if (target == _position)
            {
                return;
            }

            KeptState state = _states[target];
            _steps.RemoveRange(target, _steps.Count - target);
            _states.RemoveRange(target + 1, _states.Count - target - 1);

            if (_savedPosition > target)
            {
                _savedPosition = -1;
            }

            _position = target;

            // Read as an undo reads it: the drawing read ahead taken where it is the one, and those of the new place read ahead.
            Reload(state, null);
            Changed?.Invoke(
                this,
                new CadDocumentChangedEventArgs(
                    CadChangeKind.Rollback,
                    "BCLOSE",
                    Array.Empty<string>(),
                    AllSpaces.ToArray(),
                    true
                )
            );
        }

        /// <summary>Forgets the steps undone, which no redo reaches once the Block Editor has saved or discarded.</summary>
        private void DropRedo()
        {
            if (_steps.Count > _position)
            {
                _steps.RemoveRange(_position, _steps.Count - _position);
                _states.RemoveRange(_position + 1, _states.Count - _position - 1);

                if (_savedPosition > _position)
                {
                    _savedPosition = -1;
                }
            }
        }

        /// <summary>Sets a value of a block, leaving it out where the drawing's version holds none of it.</summary>
        private static void KeepValue(Action set, bool differs)
        {
            if (!differs)
            {
                return;
            }

            try
            {
                set();
            }
            catch (InvalidOperationException exception)
            {
                GeometryHelperLog.Debug("CadDocument: a value of a block was left out: " + exception.Message);
            }
        }
    }

    /// <summary>
    /// The Block Editor open on a drawing (<see cref="CadDocument.BlockEditor"/>): the block it shows, the space that shows it
    /// and whether it has changes not saved.
    /// </summary>
    public sealed class CadBlockEditor
    {
        /// <summary>The name the Edit Block Definition dialog lists first, which opens model space itself.</summary>
        public const string CurrentDrawing = "<Current Drawing>";

        /// <summary>What a command that leaves the block says while the editor is open.</summary>
        public const string NotAllowed = "Not allowed in the Block Editor.";

        /// <summary>The start of the name of the Block Editor's space, which no layout can have, as a colon is not allowed in one.</summary>
        internal const string SpacePrefix = "Block Editor: ";

        private readonly CadDocument _document;

        // The visibility state chosen in the Visibility State box; null for the block's first.
        private string _state;

        internal CadBlockEditor(
            CadDocument document,
            string name,
            CadSpace space,
            bool isNew,
            int start,
            int floor,
            CadSpace previous
        )
        {
            _document = document;
            Name = name;
            Space = space;
            IsNew = isNew;
            Start = start;
            Floor = floor;
            Previous = previous;
        }

        /// <summary>Gets the name of the block, or <see cref="CurrentDrawing"/> where model space is open.</summary>
        public string Name { get; }

        /// <summary>Gets whether model space itself is open (&lt;Current Drawing&gt;).</summary>
        public bool IsCurrentDrawing => ReferenceEquals(Space, _document.ModelSpace);

        /// <summary>Gets the space that shows the block, the document's current space while the editor is open.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets whether the block was made by BEDIT and has not been saved yet: closing without saving makes no block.</summary>
        public bool IsNew { get; private set; }

        /// <summary>
        /// Gets the base point of the block, where the UCS icon stands: the origin of its definition, or the drawing's
        /// $INSBASE for &lt;Current Drawing&gt;.
        /// </summary>
        public GeoPoint3 BasePoint => IsCurrentDrawing ? _document.Database.Insbase : Space.Block.Origin;

        /// <summary>
        /// Gets whether the block has changes not saved: an edit since it was opened or last saved, or a new block never saved.
        /// Undoing back to where it was opened leaves none.
        /// </summary>
        public bool HasUnsavedChanges => IsNew || _document.UndoPosition > Floor || _document.IsInGroup;

        /// <summary>The undo position before BEDIT, which the session's steps are merged from.</summary>
        internal int Start { get; }

        /// <summary>The undo position the block was opened or last saved at, which undo stops at.</summary>
        internal int Floor { get; private set; }

        /// <summary>The space shown before BEDIT, shown again when the editor closes.</summary>
        internal CadSpace Previous { get; }

        /// <summary>Where closing without saving reads the drawing back to: before BEDIT for a new block never saved, else the floor.</summary>
        internal int DiscardTarget => IsNew ? Start : Floor;

        /// <summary>Marks the block saved where the undo position now stands.</summary>
        internal void Saved()
        {
            IsNew = false;
            Floor = _document.UndoPosition;
        }

        /// <summary>
        /// Gets the visibility states of the block open, as AutoCAD's Visibility State box lists them, read anew from the block
        /// on each call (<see cref="DxfBlockTableRecord.GetVisibilityStates"/>); empty for a block of none, or for &lt;Current
        /// Drawing&gt;.
        /// </summary>
        public IReadOnlyList<string> VisibilityStates
        {
            get
            {
                var names = new List<string>();

                foreach (DxfBlockVisibilityState state in States())
                {
                    names.Add(state.Name);
                }

                return names;
            }
        }

        /// <summary>
        /// Gets the visibility state the editor shows, as AutoCAD's Visibility State box: the first of the block's when it
        /// opens; null for a block of none. The entities another state lists and this one does not are hidden; an entity no
        /// state lists, as one drawn in the editor, is shown in every state.
        /// </summary>
        public string VisibilityState
        {
            get
            {
                IReadOnlyList<DxfBlockVisibilityState> states = States();

                if (states.Count == 0)
                {
                    return null;
                }

                foreach (DxfBlockVisibilityState state in states)
                {
                    if (string.Equals(state.Name, _state, StringComparison.Ordinal))
                    {
                        return state.Name;
                    }
                }

                return states[0].Name;
            }
        }

        /// <summary>Shows a state of the block, the one of the name given.</summary>
        internal void ShowState(string name) => _state = name;

        /// <summary>
        /// The handles of the entities the state shown shows, drawn though the block keeps them invisible as its first state
        /// hides them, and of those it hides: those another state lists and it does not. False for a block of no state.
        /// </summary>
        internal bool StateSets(out HashSet<string> shown, out HashSet<string> hidden)
        {
            IReadOnlyList<DxfBlockVisibilityState> states = States();
            string current = VisibilityState;
            shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (current == null)
            {
                return false;
            }

            foreach (DxfBlockVisibilityState state in states)
            {
                foreach (DxfEntity entity in state.Entities)
                {
                    (state.Name == current ? shown : hidden).Add(entity.Handle);
                }
            }

            hidden.ExceptWith(shown);
            return true;
        }

        /// <summary>The block's visibility states; none for &lt;Current Drawing&gt;, or a block whose states cannot be read.</summary>
        private IReadOnlyList<DxfBlockVisibilityState> States()
        {
            if (IsCurrentDrawing || Space.Block == null)
            {
                return Array.Empty<DxfBlockVisibilityState>();
            }

            try
            {
                return Space.Block.GetVisibilityStates();
            }
            catch (Exception exception)
                when (exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is ArgumentException
                )
            {
                return Array.Empty<DxfBlockVisibilityState>();
            }
        }
    }

    /// <summary>The units a block can be given, as the Units row of the Block group names them: Unitless, Inches, Millimeters and the rest.</summary>
    internal static class CadBlockUnits
    {
        internal static readonly DxfUnits[] Values = (DxfUnits[])Enum.GetValues(typeof(DxfUnits));

        internal static readonly string[] Names = Array.ConvertAll(Values, CadCommands.UnitName);

        /// <summary>The place of a unit's name in <see cref="Names"/>, letters of either case alike; -1 for none.</summary>
        internal static int IndexOf(string name) =>
            Array.FindIndex(Names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }
}
