using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The editing half of the document: every change goes through <see cref="ApplyEdit"/>, one undo step each (or one for a
    /// group of them, as a command makes), with undo, redo, saving and the <see cref="Changed"/> event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// How undo works. The library writes a change into the drawing at once and has no undo of its own; an erased entity's
    /// handle is never given again, and a record written anew is written in the library's style, not in the bytes the file had.
    /// So an edit is not undone by setting its old values back: the document keeps the drawing as it stood before the first
    /// edit and after every step, each saved to bytes at once and compressed on a worker thread (a drawing of 10 MB takes about
    /// a tenth of that), and undoing a step reads the drawing it stood at before. Undoing every step gives the drawing back byte
    /// for byte, its handles and its <c>$HANDSEED</c> among it, and redo is as exact. The records of a step say what it changed,
    /// so that only the entities it touched are drawn again (see <see cref="GetDisplay"/>) and an owner can recognise its own
    /// edits.
    /// </para>
    /// <para>
    /// After an undo or a redo <see cref="Database"/> is a new <see cref="DxfDatabase"/> read from those bytes, so an entity is
    /// held by its handle, not by the object, across them; the spaces are the same <see cref="CadSpace"/> objects, pointed at
    /// the new drawing.
    /// </para>
    /// </remarks>
    public sealed partial class CadDocument
    {
        private readonly List<KeptState> _states = new List<KeptState>();
        private readonly List<CadUndoStep> _steps = new List<CadUndoStep>();
        private int _position;
        private int _savedPosition;
        private bool _hasPath;
        private int? _entityCount;
        private int _ownerDepth;
        private int _groupDepth;
        private string _groupName;
        private List<CadEditRecord> _groupRecords;
        private bool _groupFailed;
        private readonly CadOwnerRegistry _owners = new CadOwnerRegistry();
        private ICadEditGate _gate;
        private ICadEditGate _ownerGate;

        /// <summary>Gets the owners registered with this document: the code that owns entities it wrote (a model drawing, for one) and says how each may be edited.</summary>
        public CadOwnerRegistry Owners => _owners;

        /// <summary>Raised after the drawing changed: an edit applied, a step undone or redone, a regen, a save.</summary>
        public event EventHandler<CadDocumentChangedEventArgs> Changed;

        /// <summary>
        /// Gets or sets the gate every planned change of an existing entity is asked through before it is written (see
        /// <see cref="ICadEditGate"/>); the gate of the document's <see cref="Owners"/> (<see cref="CadOwnerEditGate"/>) until another
        /// is given, and again when set to null. With no owner registered it lets every edit through.
        /// </summary>
        public ICadEditGate EditGate
        {
            get => _gate ?? (_ownerGate ?? (_ownerGate = new CadOwnerEditGate(_owners)));
            set => _gate = value;
        }

        /// <summary>Gets whether the drawing has a file of its own to <see cref="Save"/> to: it was opened from one or saved as one.</summary>
        public bool HasFile => _hasPath;

        /// <summary>
        /// Gets the number of entities in model space, as the file list shows it ("DXF · 412 entities"): counted when first asked
        /// and again after the drawing next changes (an edit, an undo, a redo, a regen).
        /// </summary>
        public int EntityCount
        {
            get
            {
                if (_entityCount == null)
                {
                    int count = 0;

                    foreach (DxfEntity entity in Database.ModelSpace)
                    {
                        count++;
                    }

                    _entityCount = count;
                }

                return _entityCount.Value;
            }
        }

        /// <summary>Gets whether the drawing differs from the one opened or last saved, as the title's * shows; a user coordinate system changed counts, as in AutoCAD.</summary>
        public bool IsModified => _position != _savedPosition || (_groupRecords != null && _groupRecords.Count > 0) || (_ucs != null && _ucs.IsModified);

        /// <summary>Gets whether there is a step to undo; false while a command's group of edits is open.</summary>
        public bool CanUndo => _position > UndoFloor && _groupDepth == 0;

        /// <summary>Gets whether there is a step to redo; false while a command's group of edits is open.</summary>
        public bool CanRedo => _position < _steps.Count && _groupDepth == 0;

        /// <summary>Gets the step <see cref="Undo"/> would undo, or null.</summary>
        public CadUndoStep NextUndo => CanUndo ? _steps[_position - 1] : null;

        /// <summary>Gets the step <see cref="Redo"/> would redo, or null.</summary>
        public CadUndoStep NextRedo => CanRedo ? _steps[_position] : null;

        /// <summary>Gets whether a group of edits is open, as while a command runs.</summary>
        public bool IsInGroup => _groupDepth > 0;

        /// <summary>
        /// Applies one edit: the one way the presenter changes the drawing. The gate (<see cref="EditGate"/>) is asked about
        /// every planned change of an existing entity first, and one refusal refuses the whole edit with nothing written. Then
        /// <paramref name="apply"/> writes the change; if it throws, the drawing is read back as it stood before and the edit
        /// fails with the exception's message. A made edit is one undo step, or part of the open group's
        /// (<see cref="BeginGroup"/>); the display lists and pick indexes of the spaces it touched are dropped, and
        /// <see cref="Changed"/> is raised.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A planned change of an entity whose owner takes its own edits (<see cref="CadEditRule.MoveOnly"/> or
        /// <see cref="CadEditRule.Edit"/> in <see cref="Owners"/>) is handed to the owner first, as a <see cref="CadEdit"/>
        /// through <see cref="CadOwnerRegistry.TryApply"/>, after the gate let it through: the owner changes the entity and
        /// records its hand edit in edits of its own, which join this one's undo step, so that one undo takes both back. The
        /// action then leaves that entity alone (<see cref="CadEditScope.IsHandled"/>), and is not run at all where the owners
        /// made every planned change. An owner that does not take its edit refuses the whole edit, "The model owner did not
        /// take this edit.", the drawing read back as it was. A mark of handle 2F moved 40 to the right by MOVE reaches its
        /// owner as <c>CadEdit("2F", Move, newValue: "40,0")</c>.
        /// </para>
        /// <para>
        /// The value an owner is given is the planned record's <see cref="CadEditRecord.NewValue"/> (and old value), written as
        /// <see cref="CadEditKind"/> says: the presenter's commands, grips and Properties plan them so. A move planned with no
        /// value is measured: the action is run once, the entity's box seen to move, and the drawing read back before the
        /// owner is asked; so such an action may run twice and is to find its entities through the scope each time. The edits
        /// an owner makes while it applies (inside <see cref="CadOwnerRegistry.TryApply"/>) are its own and are not handed back.
        /// </para>
        /// </remarks>
        /// <param name="name">The name of the step: a command's name, as MOVE, or "Properties", "Grip".</param>
        /// <param name="planned">The changes planned to existing entities, tables and settings, each asked through the gate
        /// where it names an entity; may be null or empty for an edit that only adds entities.</param>
        /// <param name="apply">Writes the change; an entity it makes is added through <see cref="CadEditScope.Append{T}"/>, so
        /// that it is recorded.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the action is null.</exception>
        public CadEditResult ApplyEdit(string name, IReadOnlyList<CadEditRecord> planned, Action<CadEditScope> apply)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            if (_groupFailed)
            {
                return CadEditResult.Refuse("The command was taken back after an edit of it failed.");
            }

            planned = planned ?? Array.Empty<CadEditRecord>();
            EnsureStart();

            foreach (CadEditRecord record in planned)
            {
                if (record == null)
                {
                    throw new ArgumentException("A planned change is null.", nameof(planned));
                }

                if (record.Handle == null || record.Kind == CadEditRecordKind.Added)
                {
                    continue;
                }

                DxfEntity entity = FindEntity(record.Handle);

                if (entity == null)
                {
                    return CadEditResult.Refuse("The drawing has no entity " + record.Handle + ".");
                }

                record.Space = FindSpace(record.Handle)?.Name;

                if (!EditGate.Allows(this, entity, record, out string reason))
                {
                    return CadEditResult.Refuse(reason ?? "The " + KindName(entity) + " " + record.Handle + " may not be changed.");
                }
            }

            // The edits of entities whose owner records its own (a mark moved, a dimension's text overridden) go to the owner
            // first, joining this edit's undo step; the owner's own writes, made while it applies, are not handed over again.
            List<CadEditRecord> owned = _ownerDepth > 0 ? null : Owned(planned);

            if (owned == null || owned.Count == 0)
            {
                return Write(name, planned, apply, null);
            }

            BeginGroup(name);

            try
            {
                return HandOver(name, planned, apply, owned);
            }
            finally
            {
                if (_groupDepth > 0)
                {
                    EndGroup();
                }
            }
        }

        /// <summary>Marks the start of an owner applying an edit through <see cref="CadOwnerRegistry.TryApply"/>: the edits it makes are its own.</summary>
        internal void EnterOwner() => _ownerDepth++;

        /// <summary>Marks the end of an owner applying an edit.</summary>
        internal void LeaveOwner() => _ownerDepth--;

        /// <summary>Writes an edit: the action run, the records kept as a step or in the open group, the change announced.</summary>
        private CadEditResult Write(string name, IReadOnlyList<CadEditRecord> planned, Action<CadEditScope> apply, HashSet<string> handled)
        {
            var scope = new CadEditScope(this, handled);
            List<CadEditRecord> followed;

            try
            {
                apply(scope);

                // The associative dimensions tied to what the edit changed follow it, in the same step.
                followed = CadDimAssociation.Follow(this, planned, scope.Records);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadDocument: the edit " + name + " failed and was taken back.", exception);
                TakeBack();
                return CadEditResult.Fail(exception.Message);
            }

            var records = new List<CadEditRecord>(planned.Count + scope.Records.Count + followed.Count);
            records.AddRange(planned);
            records.AddRange(scope.Records);
            records.AddRange(followed);
            KeepHandles(records, scope);
            _entityCount = null;

            if (records.Exists(r => r.Kind == CadEditRecordKind.TableChanged))
            {
                AddNewSpaces();
            }

            if (_groupDepth > 0)
            {
                _groupRecords.AddRange(records);
            }
            else
            {
                Commit(name, records);
            }

            Announce(CadChangeKind.Edit, name, records);
            return CadEditResult.Done(records);
        }

        /// <summary>
        /// Brings the index of handles up to date after an edit without walking every space again where it can: an entity the
        /// edit added through its scope is put in, one it erased stays and is found erased, and one it moved or changed is the
        /// same object; any other edit (a table, an entity added by a record alone) drops the index, to be made again when next
        /// asked. A drawing of a million entities is then not walked after every edit.
        /// </summary>
        private void KeepHandles(IReadOnlyList<CadEditRecord> records, CadEditScope scope)
        {
            if (_byHandle == null)
            {
                return;
            }

            foreach (CadEditRecord record in records)
            {
                switch (record.Kind)
                {
                    case CadEditRecordKind.Transformed:
                    case CadEditRecordKind.PropertyChanged:
                    case CadEditRecordKind.Reshaped:
                    case CadEditRecordKind.Erased:
                    case CadEditRecordKind.SettingChanged:
                        continue;
                    case CadEditRecordKind.TableChanged when record.IsLayerLook:
                        continue;
                    case CadEditRecordKind.Added:
                        DxfEntity entity = scope.AddedEntityOf(record);
                        CadSpace space = SpaceNamed(record.Space);

                        if (entity != null && space != null && !string.IsNullOrEmpty(record.Handle) && string.Equals(entity.Handle, record.Handle, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!_byHandle.ContainsKey(record.Handle))
                            {
                                _byHandle[record.Handle] = entity;
                                _spaceByHandle[record.Handle] = space;
                            }

                            continue;
                        }

                        break;
                }

                _byHandle = null;
                return;
            }
        }

        /// <summary>The planned changes of entities whose owner takes its edits itself: those of the rules MoveOnly and Edit.</summary>
        private List<CadEditRecord> Owned(IReadOnlyList<CadEditRecord> planned)
        {
            List<CadEditRecord> owned = null;

            foreach (CadEditRecord record in planned)
            {
                if (record.Handle == null || record.Kind == CadEditRecordKind.Added || record.Kind == CadEditRecordKind.TableChanged || record.Kind == CadEditRecordKind.SettingChanged)
                {
                    continue;
                }

                DxfEntity entity = FindEntity(record.Handle);
                CadEditRule rule = entity == null ? CadEditRule.Free : _owners.GetEditRule(entity);

                if (rule == CadEditRule.MoveOnly || rule == CadEditRule.Edit)
                {
                    (owned ?? (owned = new List<CadEditRecord>())).Add(record);
                }
            }

            return owned;
        }

        /// <summary>
        /// Hands the edits of owned entities to their owners, then writes the rest, all in the group just opened. A move
        /// planned with no displacement is measured first: the action is run, the entity's box seen to move, and the drawing
        /// read back as it was. An owner that does not take its edit refuses the whole edit, the drawing as it was.
        /// </summary>
        private CadEditResult HandOver(string name, IReadOnlyList<CadEditRecord> planned, Action<CadEditScope> apply, List<CadEditRecord> owned)
        {
            byte[] savepoint = Snapshot();
            int kept = _groupRecords.Count;
            Dictionary<string, string> measured = null;

            if (owned.Exists(r => r.NewValue == null && CadOwnerEditGate.KindOf(r) == CadEditKind.Move))
            {
                var before = new Dictionary<string, GeoPoint3>(StringComparer.OrdinalIgnoreCase);

                foreach (CadEditRecord record in owned)
                {
                    if (record.NewValue == null && CadOwnerEditGate.KindOf(record) == CadEditKind.Move)
                    {
                        if (!TryCorner(FindEntity(record.Handle), out GeoPoint3 corner))
                        {
                            return CadEditResult.Refuse("The move of the " + KindName(FindEntity(record.Handle)) + " " + record.Handle + " cannot be measured for its owner.");
                        }

                        before[record.Handle] = corner;
                    }
                }

                try
                {
                    apply(new CadEditScope(this, null));
                }
                catch (Exception exception)
                {
                    GeometryHelperLog.Warn("CadDocument: the edit " + name + " failed while its move was measured.", exception);
                    Restore(savepoint, kept);
                    return CadEditResult.Fail(exception.Message);
                }

                measured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, GeoPoint3> pair in before)
                {
                    if (TryCorner(FindEntity(pair.Key), out GeoPoint3 after))
                    {
                        measured[pair.Key] = Value(after.X - pair.Value.X) + "," + Value(after.Y - pair.Value.Y);
                    }
                }

                Restore(savepoint, kept);
            }

            var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CadEditRecord record in owned)
            {
                CadEdit edit = ToOwnerEdit(record, measured);
                bool took = false;

                if (edit != null)
                {
                    took = _owners.TryApply(this, edit);
                }

                if (!took)
                {
                    if (!_groupFailed)
                    {
                        Restore(savepoint, kept);
                    }

                    return CadEditResult.Refuse("The model owner did not take this edit.");
                }

                handled.Add(record.Handle);
            }

            // Where the owners made every planned change, the action is not run: it would write what they wrote.
            if (planned.All(r => r.Handle != null && handled.Contains(r.Handle)))
            {
                return CadEditResult.Done(planned.ToArray());
            }

            return Write(name, planned, apply, handled);
        }

        /// <summary>Reads the drawing back from a savepoint taken in the open group, the group's records made since dropped.</summary>
        private void Restore(byte[] savepoint, int kept)
        {
            Reload(savepoint, null);

            if (_groupRecords != null && _groupRecords.Count > kept)
            {
                _groupRecords.RemoveRange(kept, _groupRecords.Count - kept);
            }

            Changed?.Invoke(this, new CadDocumentChangedEventArgs(CadChangeKind.Rollback, _groupName, Array.Empty<string>(), AllSpaces.ToArray(), true));
        }

        /// <summary>
        /// The edit of the owner contract a planned change is (<see cref="CadEditKind"/> says the formats): a transformation
        /// the move, turn, scale or mirror its property names with its value, a grip that only translates a move, a reshaping
        /// grip the grip's index with where it was and where it went, a property its label and texts, an erase.
        /// </summary>
        private static CadEdit ToOwnerEdit(CadEditRecord record, Dictionary<string, string> measured)
        {
            switch (record.Kind)
            {
                case CadEditRecordKind.Erased:
                    return new CadEdit(record.Handle, CadEditKind.Erase);
                case CadEditRecordKind.PropertyChanged:
                    return new CadEdit(record.Handle, CadEditKind.Property, record.Property, record.OldValue, record.NewValue);
                case CadEditRecordKind.Reshaped:
                    string grip = record.Property != null && record.Property.StartsWith("Grip ", StringComparison.Ordinal) ? record.Property.Substring(5) : record.Property;
                    return new CadEdit(record.Handle, CadEditKind.Grip, grip, record.OldValue, record.NewValue);
                case CadEditRecordKind.Transformed:
                    string value = record.NewValue;

                    if (value == null && measured != null)
                    {
                        measured.TryGetValue(record.Handle, out value);
                    }

                    return new CadEdit(record.Handle, CadOwnerEditGate.KindOf(record), null, null, value);
                default:
                    return null;
            }
        }

        /// <summary>The least corner of an entity's box, which a move carries by its displacement; false for one with no box.</summary>
        private static bool TryCorner(DxfEntity entity, out GeoPoint3 corner)
        {
            corner = default;

            if (entity == null)
            {
                return false;
            }

            try
            {
                GeoAabb3 box = entity.GeometricExtents;

                if (box.IsEmpty)
                {
                    return false;
                }

                corner = box.Min;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A measured coordinate of a move, to 15 figures, so that the last bit a subtraction leaves is not written: 40, not 40.000000000000007.</summary>
        private static string Value(double value) => value.ToString("G15", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Opens a group of edits that becomes one undo step when it is closed, as a command that draws line after line is one
        /// step. Groups nest; the outermost names the step. An edit of the group that fails takes the whole group back.
        /// </summary>
        /// <param name="name">The name of the step.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public void BeginGroup(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (_groupDepth++ == 0)
            {
                EnsureStart();
                _groupName = name;
                _groupRecords = new List<CadEditRecord>();
                _groupFailed = false;
            }
        }

        /// <summary>Closes the group <see cref="BeginGroup"/> opened; the outermost makes its edits one undo step, if it made any.</summary>
        /// <exception cref="InvalidOperationException">Thrown when no group is open.</exception>
        public void EndGroup()
        {
            if (_groupDepth == 0)
            {
                throw new InvalidOperationException("No group of edits is open.");
            }

            if (--_groupDepth > 0)
            {
                return;
            }

            List<CadEditRecord> records = _groupRecords;
            string name = _groupName;
            bool failed = _groupFailed;
            _groupRecords = null;
            _groupName = null;
            _groupFailed = false;

            if (!failed && records.Count > 0)
            {
                Commit(name, records);
            }
        }

        /// <summary>
        /// Closes every open group and takes its edits back, the drawing read back as it stood before the group began, as a
        /// command that fails part way changes nothing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no group is open.</exception>
        public void CancelGroup()
        {
            if (_groupDepth == 0)
            {
                throw new InvalidOperationException("No group of edits is open.");
            }

            bool made = !_groupFailed && _groupRecords.Count > 0;
            string name = _groupName;
            _groupDepth = 0;
            _groupRecords = null;
            _groupName = null;
            _groupFailed = false;

            if (made)
            {
                Reload(_states[_position].Bytes, null);
                Changed?.Invoke(this, new CadDocumentChangedEventArgs(CadChangeKind.Rollback, name, Array.Empty<string>(), AllSpaces.ToArray(), true));
            }
        }

        /// <summary>
        /// Takes a savepoint inside the open group, which <see cref="BackTo"/> reads the drawing back to: the Undo option inside
        /// TRIM, EXTEND, LENGTHEN, OFFSET, FILLET and CHAMFER takes back the last pick so, the rest of the command's edits kept
        /// in its one step. Where the group has made nothing yet the savepoint is the drawing before the group, so the first
        /// pick of a command costs no saving.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no group is open.</exception>
        internal CadGroupMark MarkGroup()
        {
            if (_groupDepth == 0)
            {
                throw new InvalidOperationException("No group of edits is open.");
            }

            EnsureStart();
            int kept = _groupRecords.Count;
            return new CadGroupMark(kept == 0 ? _states[_position].Bytes : Snapshot(), kept);
        }

        /// <summary>Reads the drawing back to a savepoint <see cref="MarkGroup"/> took in the group still open, the records made since dropped.</summary>
        internal void BackTo(CadGroupMark mark)
        {
            if (_groupDepth == 0 || _groupFailed)
            {
                return;
            }

            Restore(mark.State, mark.Kept);
        }

        /// <summary>Drags a grip of an entity to a point, one undo step, as <see cref="CadGrips.Drag"/> says.</summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        /// <param name="newPoint">The point it is dragged to, in drawing units.</param>
        /// <returns>What came of it.</returns>
        public CadEditResult GripDrag(string handle, int gripIndex, GeoPoint2 newPoint) => CadGrips.Drag(this, handle, gripIndex, newPoint);

        /// <summary>Undoes the last step: the drawing is read back as it stood before it (see the remarks of the class).</summary>
        /// <returns>False when there was nothing to undo or a group is open.</returns>
        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            CadUndoStep step = _steps[_position - 1];
            _position--;
            Reload(_states[_position].Bytes, step.Records);
            Announce(CadChangeKind.Undo, step.Name, step.Records);
            return true;
        }

        /// <summary>Redoes the step undone last.</summary>
        /// <returns>False when there was nothing to redo or a group is open.</returns>
        public bool Redo()
        {
            if (!CanRedo)
            {
                return false;
            }

            CadUndoStep step = _steps[_position];
            _position++;
            Reload(_states[_position].Bytes, step.Records);
            Announce(CadChangeKind.Redo, step.Name, step.Records);
            return true;
        }

        /// <summary>Saves the drawing to the file it was opened from or last saved as.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no file yet (it was opened from a stream or
        /// given in memory) or a group of edits is open; use <see cref="SaveAs"/>.</exception>
        public void Save()
        {
            if (!_hasPath)
            {
                throw new InvalidOperationException("The drawing has no file yet: save it with SaveAs.");
            }

            SaveAs(FileName);
        }

        /// <summary>Saves the drawing to a file, which it is then known by.</summary>
        /// <param name="path">The path of the file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a group of edits is open.</exception>
        public void SaveAs(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (_groupDepth > 0)
            {
                throw new InvalidOperationException("A command is running: end it before saving.");
            }

            string full = Path.GetFullPath(path);
            _ucs?.WriteTo(Database);
            Database.Save(full);
            _ucs?.Saved();
            FileName = full;
            _hasPath = true;
            _savedPosition = _position;
            GeometryHelperLog.Info("CadDocument: saved " + full + ".");
            Changed?.Invoke(this, new CadDocumentChangedEventArgs(CadChangeKind.Saved, null, Array.Empty<string>(), Array.Empty<CadSpace>(), false));
        }

        /// <summary>Drops every display list and pick index, so that each space is drawn again from the drawing, as REGEN does.</summary>
        public void Regen()
        {
            _builder = new DisplayBuilder(Database, this);
            _displays.Clear();
            _indexes.Clear();
            _stale.Clear();
            _byHandle = null;
            _entityCount = null;
            Changed?.Invoke(this, new CadDocumentChangedEventArgs(CadChangeKind.Regen, null, Array.Empty<string>(), AllSpaces.ToArray(), true));
        }

        /// <summary>The name of an entity's kind for a message: LINE, CIRCLE.</summary>
        internal static string KindName(DxfEntity entity)
        {
            string name = entity.GetType().Name;
            return name.StartsWith("Dxf", StringComparison.Ordinal) ? name.Substring(3).ToLowerInvariant() : name;
        }

        /// <summary>Keeps the drawing as it stands before the first edit, once.</summary>
        private void EnsureStart()
        {
            if (_states.Count == 0)
            {
                _states.Add(Keep());
                _savedPosition = 0;
            }
        }

        /// <summary>Makes the edits one step: the drawing after them kept, the steps undone before forgotten.</summary>
        private void Commit(string name, List<CadEditRecord> records)
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

            _steps.Add(new CadUndoStep(name, records.ToArray()));
            _states.Add(Keep());
            _position++;
        }

        /// <summary>Reads the drawing back as it stood after the last step, an edit or a group that failed taken back.</summary>
        private void TakeBack()
        {
            Reload(_states[_position].Bytes, null);

            if (_groupDepth > 0)
            {
                _groupFailed = true;
                _groupRecords.Clear();
            }

            Changed?.Invoke(this, new CadDocumentChangedEventArgs(CadChangeKind.Rollback, _groupName, Array.Empty<string>(), AllSpaces.ToArray(), true));
        }

        private byte[] Snapshot()
        {
            var raw = new MemoryStream();
            Database.Save(raw);
            return Pack(raw.GetBuffer(), (int)raw.Length);
        }

        /// <summary>The drawing as it stands, saved now and compressed on a worker thread, for a step of the undo list.</summary>
        private KeptState Keep()
        {
            var raw = new MemoryStream();
            Database.Save(raw);
            return new KeptState(raw.GetBuffer(), (int)raw.Length);
        }

        private static byte[] Pack(byte[] raw, int length)
        {
            var packed = new MemoryStream();

            using (var deflate = new DeflateStream(packed, CompressionLevel.Fastest, true))
            {
                deflate.Write(raw, 0, length);
            }

            return packed.ToArray();
        }

        /// <summary>
        /// A drawing kept for undo: saved at once, as the step stands, and compressed on a worker thread, so that an edit of a
        /// drawing of a million entities (140 MB as text) does not wait for its packing; an undo that needs it first waits.
        /// </summary>
        private sealed class KeptState
        {
            private readonly Task<byte[]> _packing;

            public KeptState(byte[] raw, int length)
            {
                _packing = Task.Run(() => Pack(raw, length));
            }

            public byte[] Bytes => _packing.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reads the drawing from kept bytes and points the spaces at it. The display lists of the spaces the records touched
        /// are dropped, those of the others kept, as the drawing there is the same; null records drop them all.
        /// </summary>
        private void Reload(byte[] state, IReadOnlyList<CadEditRecord> records)
        {
            var bytes = new MemoryStream();

            using (var inflate = new DeflateStream(new MemoryStream(state), CompressionMode.Decompress))
            {
                inflate.CopyTo(bytes);
            }

            bytes.Position = 0;
            Rebind(DxfDatabase.Open(bytes), records);
        }

        /// <summary>
        /// Gives a drawing of R13 or later written without its OBJECTS section (as a program that writes only the tables, blocks
        /// and entities saves one) the named object dictionary AutoCAD makes on opening it, so that a group or a style has a
        /// dictionary to go in: an empty DICTIONARY, owned by none, in an OBJECTS section put before the end of the file, its
        /// handle the next above every handle of the file. The drawing is read back with it, before the edit that needs it, so
        /// that undoing that edit leaves the drawing as it was read; a drawing that has one, one of R12, or a binary one, is left.
        /// </summary>
        internal void EnsureNamedObjectsDictionary()
        {
            if (Database.NamedObjectsDictionary != null || Database.Version == null || string.CompareOrdinal(Database.Version, "AC1012") < 0)
            {
                return;
            }

            EnsureStart();
            var raw = new MemoryStream();
            Database.Save(raw);
            Encoding latin = Encoding.GetEncoding(28591);
            string text = latin.GetString(raw.GetBuffer(), 0, (int)raw.Length);
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = text.Split('\n');
            ulong top = 0;
            int end = -1;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                string code = lines[i].Trim();
                string value = lines[i + 1].Trim();

                if ((code == "5" || code == "105") && ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong handle) && handle > top)
                {
                    top = handle;
                }
                else if (code == "0" && value == "EOF")
                {
                    end = i;
                }
            }

            if (end < 0)
            {
                return;
            }

            string root = (top + 1).ToString("X", CultureInfo.InvariantCulture);
            string section = string.Join(newline, "  0", "SECTION", "  2", "OBJECTS", "  0", "DICTIONARY", "  5", root, "330", "0", "100", "AcDbDictionary", "281", "     1", "  0", "ENDSEC") + newline;
            int at = 0;

            for (int i = 0; i < end; i++)
            {
                at += lines[i].Length + 1;
            }

            byte[] made = latin.GetBytes(text.Insert(at, section));
            Rebind(DxfDatabase.Open(new MemoryStream(made)), null);
        }

        /// <summary>Points the document and its spaces at a drawing read anew; see <see cref="Reload"/>.</summary>
        private void Rebind(DxfDatabase database, IReadOnlyList<CadEditRecord> records)
        {
            var drop = new HashSet<CadSpace>(records == null ? AllSpaces : new List<CadSpace>());
            DisplayBuilder older = _builder;
            Database = database;
            _builder = new DisplayBuilder(database, this);
            _byHandle = null;
            _entityCount = null;
            ModelSpace.Rebind(database.ModelSpace, null);
            var spaces = new List<CadSpace> { ModelSpace };

            foreach (DxfLayout layout in LayoutsOf(database))
            {
                CadSpace kept = _spaces.Find(s => !s.IsModel && string.Equals(s.Name, layout.LayoutName, StringComparison.OrdinalIgnoreCase));

                if (kept == null)
                {
                    kept = new CadSpace(this, layout.LayoutName, false, layout.BlockTableRecord, layout);
                }
                else
                {
                    kept.Rebind(layout.BlockTableRecord, layout);
                }

                spaces.Add(kept);
            }

            foreach (CadSpace gone in _spaces)
            {
                if (!spaces.Contains(gone))
                {
                    drop.Add(gone);
                }
            }

            _spaces.Clear();
            _spaces.AddRange(spaces);
            RebindBlockEditor(database);

            if (!_spaces.Contains(CurrentSpace) && !(CurrentSpace != null && CurrentSpace.IsBlockEditor))
            {
                CurrentSpace = ModelSpace;
            }

            foreach (CadSpace space in drop)
            {
                _displays.Remove(space);
                _indexes.Remove(space);
                _stale.Remove(space);
            }

            if (records != null)
            {
                // The drawing read back differs from the one shown by the records' entities alone.
                Invalidate(records, older);
            }

            var alive = new List<string>();

            foreach (string handle in Selection.Handles)
            {
                if (FindEntity(handle) != null)
                {
                    alive.Add(handle);
                }
            }

            Selection.Set(alive);
        }

        /// <summary>
        /// Gives each layout an edit added (LAYOUT New) a space of its own, after the others, as its tab is the last; a layout an
        /// edit renamed (LAYOUT Rename) a space of its new name in the place of its old one.
        /// </summary>
        private void AddNewSpaces()
        {
            for (int i = 0; i < _spaces.Count; i++)
            {
                CadSpace space = _spaces[i];

                if (!space.IsModel && space.Layout != null && !string.Equals(space.Name, space.Layout.LayoutName, StringComparison.Ordinal))
                {
                    _spaces[i] = new CadSpace(this, space.Layout.LayoutName, false, space.Block, space.Layout);
                    _displays.Remove(space);
                    _indexes.Remove(space);
                    _stale.Remove(space);

                    if (CurrentSpace == space)
                    {
                        CurrentSpace = _spaces[i];
                    }
                }
            }

            foreach (DxfLayout layout in LayoutsOf(Database))
            {
                if (!_spaces.Exists(s => !s.IsModel && string.Equals(s.Name, layout.LayoutName, StringComparison.OrdinalIgnoreCase)))
                {
                    _spaces.Add(new CadSpace(this, layout.LayoutName, false, layout.BlockTableRecord, layout));
                }
            }
        }

        /// <summary>The spaces a list of edits touched: their own, every layout too where model space is among them (a viewport draws it), all where a table changed.</summary>
        private HashSet<CadSpace> Touched(IReadOnlyList<CadEditRecord> records, out bool tables)
        {
            var touched = new HashSet<CadSpace>();
            tables = false;
            bool all = false;

            foreach (CadEditRecord record in records)
            {
                // The drawing's linetype scale changes every dash drawn, as AutoCAD regenerates after LTSCALE, and its point
                // style every point: a new builder reads them.
                if (record.Kind == CadEditRecordKind.TableChanged || (record.Kind == CadEditRecordKind.SettingChanged && Redraws(record.Property)))
                {
                    tables = true;
                    all = true;
                    continue;
                }

                if (record.Kind == CadEditRecordKind.SettingChanged)
                {
                    continue;
                }

                CadSpace space = SpaceNamed(record.Space);

                if (space == null)
                {
                    all = true;
                }
                else
                {
                    touched.Add(space);
                }
            }

            if (all || touched.Contains(ModelSpace))
            {
                touched.UnionWith(all ? AllSpaces : _spaces.FindAll(s => !s.IsModel));
            }

            return touched;
        }

        /// <summary>
        /// Drops or marks stale the display lists of the spaces edits touched. Where every record names an entity of a space or
        /// a layer's look alone (<see cref="CadEditRecord.IsLayerLook"/>), a space's list is kept with those handles and the
        /// layers drawn differently since <paramref name="older"/>, and the next <see cref="GetDisplay"/> draws only their
        /// entities again; a layout goes whole where model space changed, as its viewports draw model space's list; any other
        /// edit drops the lists of the spaces it touched, as before.
        /// </summary>
        private void Invalidate(IReadOnlyList<CadEditRecord> records, DisplayBuilder older)
        {
            HashSet<CadSpace> touched = Touched(records, out _);
            var handles = new Dictionary<CadSpace, List<string>>();
            bool whole = false;
            bool layers = false;

            foreach (CadEditRecord record in records)
            {
                if (record.Kind == CadEditRecordKind.TableChanged)
                {
                    layers |= record.IsLayerLook;
                    whole |= !record.IsLayerLook;
                }
                else if (record.Kind == CadEditRecordKind.SettingChanged)
                {
                    whole |= Redraws(record.Property);
                }
                else
                {
                    CadSpace space = record.Handle == null ? null : SpaceNamed(record.Space);

                    if (space == null)
                    {
                        whole = true;
                        continue;
                    }

                    if (!handles.TryGetValue(space, out List<string> named))
                    {
                        named = new List<string>();
                        handles[space] = named;
                    }

                    named.Add(record.Handle);
                }
            }

            List<string> changedLayers = !whole && layers ? _builder.LayersChangedSince(older) : new List<string>();
            bool modelChanged = touched.Contains(ModelSpace);

            foreach (CadSpace space in touched)
            {
                _indexes.Remove(space);

                if (whole || (!space.IsModel && (layers || modelChanged)))
                {
                    _displays.Remove(space);
                    _stale.Remove(space);
                    continue;
                }

                if (_displays.TryGetValue(space, out DisplayList2 list))
                {
                    _displays.Remove(space);
                    _stale[space] = new StaleDisplay(list);
                }

                if (_stale.TryGetValue(space, out StaleDisplay stale))
                {
                    stale.Handles.UnionWith(handles.TryGetValue(space, out List<string> named) ? named : new List<string>());
                    stale.Layers.UnionWith(changedLayers);
                }
            }
        }

        /// <summary>A display list an edit changed, with the handles of the entities and the layers it changed since the list was made.</summary>
        private sealed class StaleDisplay
        {
            public StaleDisplay(DisplayList2 list)
            {
                List = list;
            }

            public DisplayList2 List { get; }

            public HashSet<string> Handles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> Layers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Whether a setting of the header changes how every space is drawn: $LTSCALE, $PDMODE and $PDSIZE.</summary>
        private static bool Redraws(string property)
            => string.Equals(property, "$LTSCALE", StringComparison.Ordinal) || string.Equals(property, "$PDMODE", StringComparison.Ordinal) || string.Equals(property, "$PDSIZE", StringComparison.Ordinal);

        /// <summary>Drops or marks stale the display lists of the spaces edits touched and raises <see cref="Changed"/>.</summary>
        private void Announce(CadChangeKind kind, string name, IReadOnlyList<CadEditRecord> records)
        {
            HashSet<CadSpace> touched = Touched(records, out bool tables);
            DisplayBuilder older = _builder;

            if (tables && kind == CadChangeKind.Edit)
            {
                // The builder reads the layers once; a layer switched off or given a colour needs a new one.
                _builder = new DisplayBuilder(Database, this);
            }

            Invalidate(records, older);

            var handles = new SortedSet<string>(StringComparer.Ordinal);

            foreach (CadEditRecord record in records)
            {
                if (record.Handle != null)
                {
                    handles.Add(record.Handle);
                }
            }

            var spaces = AllSpaces.FindAll(touched.Contains);
            Changed?.Invoke(this, new CadDocumentChangedEventArgs(kind, name, new List<string>(handles), spaces, tables));
        }
    }

    /// <summary>A savepoint inside a group of edits: the drawing as it stood, and how many of the group's records were made by then.</summary>
    internal sealed class CadGroupMark
    {
        internal CadGroupMark(byte[] state, int kept)
        {
            State = state;
            Kept = kept;
        }

        internal byte[] State { get; }

        internal int Kept { get; }
    }

    /// <summary>
    /// What an edit's action is given to write with: the document and the drawing, and the way to add an entity so that it is
    /// recorded. An entity of the drawing is changed through its own members (<c>TransformBy</c>, its setters, <c>Erase</c>);
    /// the edit's planned records say which.
    /// </summary>
    public sealed class CadEditScope
    {
        private readonly List<CadEditRecord> _records = new List<CadEditRecord>();
        private readonly HashSet<string> _handled;

        // The entity of each record Added made, by the record itself; looked up only, never enumerated.
        private readonly Dictionary<CadEditRecord, DxfEntity> _added = new Dictionary<CadEditRecord, DxfEntity>();

        internal CadEditScope(CadDocument document, HashSet<string> handled)
        {
            Document = document;
            _handled = handled;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the drawing.</summary>
        public DxfDatabase Database => Document.Database;

        internal IReadOnlyList<CadEditRecord> Records => _records;

        /// <summary>
        /// Gets whether the owner of an entity took its planned edit and made the change itself (a mark moved by the drawing
        /// that owns it), so that the action leaves that entity alone; false for an entity the presenter edits itself.
        /// </summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <returns>True when the owner made the change.</returns>
        public bool IsHandled(string handle) => handle != null && _handled != null && _handled.Contains(handle);

        /// <summary>Adds an entity made in code to a space and records it as added.</summary>
        /// <typeparam name="T">The kind of entity.</typeparam>
        /// <param name="space">The space.</param>
        /// <param name="entity">The entity, in no drawing yet.</param>
        /// <returns>The entity, now in the space with a handle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space or the entity is null.</exception>
        public T Append<T>(CadSpace space, T entity)
            where T : DxfEntity
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            space.Block.AppendEntity(entity);
            Added(space, entity);
            return entity;
        }

        /// <summary>Records an entity a call of the library added to a space by itself, as <c>WblockCloneObjects</c> adds its copies.</summary>
        /// <param name="space">The space.</param>
        /// <param name="entity">The entity added.</param>
        /// <exception cref="ArgumentNullException">Thrown when the space or the entity is null.</exception>
        public void Added(CadSpace space, DxfEntity entity)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var record = new CadEditRecord(CadEditRecordKind.Added, entity.Handle) { Space = space.Name };
            _records.Add(record);
            _added[record] = entity;
        }

        /// <summary>The entity <see cref="Added"/> recorded with a record of this scope; null for a record made otherwise.</summary>
        internal DxfEntity AddedEntityOf(CadEditRecord record) => _added.TryGetValue(record, out DxfEntity entity) ? entity : null;

        /// <summary>Records a change the planned records did not name, found while writing.</summary>
        /// <param name="record">The record.</param>
        /// <exception cref="ArgumentNullException">Thrown when the record is null.</exception>
        public void Record(CadEditRecord record) => _records.Add(record ?? throw new ArgumentNullException(nameof(record)));
    }
}
