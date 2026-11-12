using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What an edit does to one entity, or to the drawing's tables and settings.</summary>
    public enum CadEditRecordKind
    {
        /// <summary>An entity is made and added to a space.</summary>
        Added,

        /// <summary>An entity is erased.</summary>
        Erased,

        /// <summary>An entity is moved, turned, scaled or mirrored as a whole.</summary>
        Transformed,

        /// <summary>One property of an entity is set: its layer, colour, a point, a radius, its text.</summary>
        PropertyChanged,

        /// <summary>An entity's shape is changed some other way, as dragging one grip of it changes it.</summary>
        Reshaped,

        /// <summary>A table of the drawing is changed: a layer added, switched off, frozen, locked or given a colour.</summary>
        TableChanged,

        /// <summary>A setting of the drawing's header is changed, as the current layer; nothing drawn changes.</summary>
        SettingChanged,
    }

    /// <summary>
    /// One small record of an edit: which entity, what was done to it and, for a property, its value before and after, as
    /// text. An undo step is a list of these. They say what changed, so that the canvas draws again only the spaces touched
    /// and an owner of an entity can recognise an edit of its own; what the drawing was before is kept by the document whole
    /// (see <see cref="CadDocument.Undo"/>), so the records never need to be enough to undo an edit by themselves.
    /// </summary>
    public sealed class CadEditRecord
    {
        /// <summary>Makes a record.</summary>
        /// <param name="kind">What the edit does.</param>
        /// <param name="handle">The handle of the entity, or null for an edit of a table or a setting.</param>
        /// <param name="property">The name of the property set, as the Properties panel labels it ("Layer", "Radius"); null
        /// where the edit is not of one property.</param>
        /// <param name="oldValue">The value before, as text; null where there is none.</param>
        /// <param name="newValue">The value after, as text; null where there is none.</param>
        public CadEditRecord(
            CadEditRecordKind kind,
            string handle,
            string property = null,
            string oldValue = null,
            string newValue = null
        )
        {
            Kind = kind;
            Handle = handle;
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>Gets what the edit does.</summary>
        public CadEditRecordKind Kind { get; }

        /// <summary>Gets the handle of the entity, or null for an edit of a table or a setting.</summary>
        public string Handle { get; }

        /// <summary>Gets the name of the property set, or null.</summary>
        public string Property { get; }

        /// <summary>Gets the value before, as text, or null.</summary>
        public string OldValue { get; }

        /// <summary>Gets the value after, as text, or null.</summary>
        public string NewValue { get; }

        /// <summary>
        /// Gets the name of the space the entity is in ("Model", or a layout's name), filled in by the document when the edit is
        /// applied; null for an edit of a table or a setting, and for an entity inside a block.
        /// </summary>
        public string Space { get; internal set; }

        /// <summary>
        /// Gets or sets whether the record is of how a layer is drawn and nothing else: switched on or off, frozen or thawed,
        /// locked, or given a colour, linetype or lineweight, so that only the entities that read that layer are drawn again.
        /// </summary>
        internal bool IsLayerLook { get; set; }

        /// <summary>
        /// Gets or sets how a setting kept in the drawing is put back or forward without reading the drawing again (see
        /// <see cref="CadDocument.ApplySetting"/>): called with true to make the setting as it is after the edit, with false as
        /// it was before; null for every other record.
        /// </summary>
        internal Action<CadDocument, bool> Replay { get; set; }

        /// <summary>
        /// Gets or sets what the step of a view did to a ZOOM Previous list, so that U and REDO move the list with the view; null
        /// for every other record.
        /// </summary>
        internal CadViewListChange ViewList { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            Kind
            + (Handle != null ? " " + Handle : string.Empty)
            + (Property != null ? " " + Property + ": " + OldValue + " -> " + NewValue : string.Empty);
    }

    /// <summary>One step of the undo list: the edits of one command, of one Properties change or of one grip dragged.</summary>
    public sealed class CadUndoStep
    {
        internal CadUndoStep(string name, IReadOnlyList<CadEditRecord> records, bool joinsPrevious = false)
        {
            Name = name;
            Records = records;
            JoinsPrevious = joinsPrevious;
        }

        /// <summary>Gets the name of what made the step: a command's name, as MOVE, or "Properties" or "Grip".</summary>
        public string Name { get; }

        /// <summary>Gets the edits of the step in the order they were made.</summary>
        public IReadOnlyList<CadEditRecord> Records { get; }

        /// <summary>
        /// Gets whether the step is taken back and put again together with the step before it, as AutoCAD's U takes F7 pressed
        /// after a LINE back with the line: one U for both, under the name of the line.
        /// </summary>
        internal bool JoinsPrevious { get; }

        /// <inheritdoc/>
        public override string ToString() => Name + " (" + Records.Count + ")";
    }

    /// <summary>What came of an edit: whether it was made and, where it was not, why.</summary>
    public sealed class CadEditResult
    {
        private CadEditResult(bool succeeded, bool refused, string message, IReadOnlyList<CadEditRecord> records)
        {
            Succeeded = succeeded;
            Refused = refused;
            Message = message;
            Records = records;
        }

        /// <summary>Gets whether the edit was made.</summary>
        public bool Succeeded { get; }

        /// <summary>Gets whether the edit was refused before anything was written, by the edit gate or by a check of the edit's own.</summary>
        public bool Refused { get; }

        /// <summary>Gets why the edit was not made, as the command line shows it; null when it was made.</summary>
        public string Message { get; }

        /// <summary>Gets the records of the edit made; empty when it was not.</summary>
        public IReadOnlyList<CadEditRecord> Records { get; }

        internal static CadEditResult Done(IReadOnlyList<CadEditRecord> records) =>
            new CadEditResult(true, false, null, records);

        /// <summary>Makes the result of an edit refused before anything was written, as a check of the window refuses one, so that it is reported as the document's own refusals are.</summary>
        /// <param name="message">Why, as the command line shows it.</param>
        /// <returns>The result: not made, refused, with the message.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the message is null.</exception>
        public static CadEditResult Refuse(string message) =>
            new CadEditResult(
                false,
                true,
                message ?? throw new ArgumentNullException(nameof(message)),
                Array.Empty<CadEditRecord>()
            );

        internal static CadEditResult Fail(string message) =>
            new CadEditResult(false, false, message, Array.Empty<CadEditRecord>());

        /// <inheritdoc/>
        public override string ToString() =>
            Succeeded ? "Done (" + Records.Count + ")" : (Refused ? "Refused: " : "Failed: ") + Message;
    }

    /// <summary>
    /// The one place an edit is asked about before it is written. <see cref="CadDocument.ApplyEdit"/> asks the gate of the
    /// document once for every existing entity an edit plans to change or erase, before anything is written; an answer of
    /// false refuses the whole edit and the drawing stays as it was. The owner contract (an entity whose owner says it may only
    /// be moved, or not edited at all) is plugged in here: the default lets every edit through.
    /// </summary>
    public interface ICadEditGate
    {
        /// <summary>Asks whether one planned change of an entity may be made.</summary>
        /// <param name="document">The document.</param>
        /// <param name="entity">The entity, as it stands before the edit.</param>
        /// <param name="record">What the edit plans to do to it.</param>
        /// <param name="reason">Why it may not, as the command line shows it ("This line belongs to the model"); null when it may.</param>
        /// <returns>True when the change may be made.</returns>
        bool Allows(CadDocument document, DxfEntity entity, CadEditRecord record, out string reason);
    }

    /// <summary>The edit gates the presenter brings.</summary>
    public static class CadEditGate
    {
        /// <summary>Gets the gate that lets every edit through, which a document has until another is given.</summary>
        public static ICadEditGate AllowAll { get; } = new OpenGate();

        private sealed class OpenGate : ICadEditGate
        {
            public bool Allows(CadDocument document, DxfEntity entity, CadEditRecord record, out string reason)
            {
                reason = null;
                return true;
            }
        }
    }

    /// <summary>What made a document change.</summary>
    public enum CadChangeKind
    {
        /// <summary>An edit was applied.</summary>
        Edit,

        /// <summary>A step was undone.</summary>
        Undo,

        /// <summary>A step was redone.</summary>
        Redo,

        /// <summary>A failed edit was taken back, the drawing as it was before it.</summary>
        Rollback,

        /// <summary>Every display list was made again, as REGEN does.</summary>
        Regen,

        /// <summary>The drawing was saved; nothing drawn changed.</summary>
        Saved,
    }

    /// <summary>What changed in a document: the entities touched, the spaces whose display lists were dropped, and why.</summary>
    public sealed class CadDocumentChangedEventArgs : EventArgs
    {
        internal CadDocumentChangedEventArgs(
            CadChangeKind kind,
            string stepName,
            IReadOnlyList<string> handles,
            IReadOnlyList<CadSpace> spaces,
            bool tablesChanged
        )
        {
            Kind = kind;
            StepName = stepName;
            Handles = handles;
            Spaces = spaces;
            TablesChanged = tablesChanged;
        }

        /// <summary>Gets what made the change.</summary>
        public CadChangeKind Kind { get; }

        /// <summary>Gets the name of the step applied, undone or redone; null for a regen or a save.</summary>
        public string StepName { get; }

        /// <summary>Gets the handles of the entities touched, in ordinal order, each once.</summary>
        public IReadOnlyList<string> Handles { get; }

        /// <summary>Gets the spaces whose display list and pick index were dropped, in the order of their tabs; the canvas draws them again.</summary>
        public IReadOnlyList<CadSpace> Spaces { get; }

        /// <summary>Gets whether a table of the drawing changed, so that the layer box and the Layer manager read it again.</summary>
        public bool TablesChanged { get; }
    }
}
