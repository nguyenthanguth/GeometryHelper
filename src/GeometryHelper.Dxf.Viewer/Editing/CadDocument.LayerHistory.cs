using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The history LAYERP reads (Layer Previous): before each change or set of changes to the settings of layers, the settings
    /// the layers had are kept, so that LAYERP puts them back without touching any other edit. A set is what one command does
    /// (LAYISO turning off nine layers and moving the current one is one), what one Layer manager session does
    /// (<see cref="BeginLayerSet"/>), or one change made on its own, as the layer box makes one. Only the layers the set
    /// changed are kept, with the settings they had.
    /// </summary>
    public sealed partial class CadDocument
    {
        // The most sets kept; the oldest is forgotten past it.
        private const int LayerSetsKept = 100;

        private readonly List<CadLayerChange> _layerChanges = new List<CadLayerChange>();
        private int _layerSetDepth;
        private int _layerSequence;
        private bool _layerRestoring;
        private CadLayerSnapshot _layerBefore;
        private CadLayerChange _layerMade;
        private CadLayerChange _layerUsed;

        /// <summary>
        /// Gets or sets whether changes to layers are kept for LAYERP: on by default, as AutoCAD's LAYERPMODE 1. Turned off,
        /// what is changed is not kept and LAYERP says it is disabled; what was kept stays until LAYERP takes it.
        /// </summary>
        public bool LayerPreviousMode { get; set; } = true;

        /// <summary>
        /// Opens a set of layer changes that LAYERP takes back together, as the Layer manager does for the length of its
        /// dialog: the changes made until <see cref="EndLayerSet"/> are one set, each still its own undo step. Sets nest; the
        /// outermost counts.
        /// </summary>
        public void BeginLayerSet() => _layerSetDepth++;

        /// <summary>Closes the set <see cref="BeginLayerSet"/> opened; the outermost keeps what it changed, if it changed any layer's settings.</summary>
        /// <exception cref="InvalidOperationException">Thrown when no set is open.</exception>
        public void EndLayerSet()
        {
            if (_layerSetDepth == 0)
            {
                throw new InvalidOperationException("No set of layer changes is open.");
            }

            if (--_layerSetDepth == 0 && _groupDepth == 0)
            {
                KeepLayerSet(false);
            }
        }

        /// <summary>
        /// Takes the settings of the layers before an edit that may change them, unless a group or a set already holds them.
        /// Called before the edit is written; the result is given to <see cref="EndLayerCapture"/>.
        /// </summary>
        private CadLayerSnapshot BeginLayerCapture(IReadOnlyList<CadEditRecord> planned)
        {
            if (
                !LayerPreviousMode
                || _layerRestoring
                || !planned.Any(r =>
                    r.Kind == CadEditRecordKind.TableChanged || r.Kind == CadEditRecordKind.SettingChanged
                )
            )
            {
                return null;
            }

            if (_groupDepth > 0 || _layerSetDepth > 0)
            {
                if (_layerBefore == null)
                {
                    _layerBefore = CadLayerSnapshot.Take(Database);
                }

                return null;
            }

            return CadLayerSnapshot.Take(Database);
        }

        /// <summary>Keeps what an edit changed in the layers' settings as a set of its own, where it was not part of a group or a set.</summary>
        private void EndLayerCapture(CadLayerSnapshot before)
        {
            if (before != null)
            {
                KeepLayers(before, true);
            }
        }

        /// <summary>Keeps the set a group or a Layer manager session made, once its last edit is written.</summary>
        private void KeepLayerSet(bool joinsStep)
        {
            CadLayerSnapshot before = _layerBefore;
            _layerBefore = null;

            if (before != null)
            {
                KeepLayers(before, joinsStep);
            }
        }

        private void KeepLayers(CadLayerSnapshot before, bool joinsStep)
        {
            CadLayerChange change = before.ChangeTo(Database, ++_layerSequence);

            if (change == null)
            {
                return;
            }

            _layerChanges.Add(change);

            if (_layerChanges.Count > LayerSetsKept)
            {
                _layerChanges.RemoveAt(0);
            }

            if (joinsStep)
            {
                _layerMade = change;
            }
        }

        /// <summary>
        /// Puts the layers back as the last set kept found them (LAYERP): the layers it changed given their settings again, the
        /// current layer too, one undo step. A layer renamed since is not found by its old name and a layer deleted is gone, so
        /// those are left; a layer added is not a change to settings and stays. Undoing the step brings the set back for the
        /// next LAYERP, as it brings the settings.
        /// </summary>
        /// <param name="notes">Filled with what AutoCAD says besides: the current layer left off, a layer or linetype that no longer exists.</param>
        /// <returns>Done, with no record where the settings are as the set had them; refused, with the words AutoCAD says, when the mode is off or nothing was kept.</returns>
        internal CadEditResult RestorePreviousLayers(List<string> notes)
        {
            if (!LayerPreviousMode)
            {
                return CadEditResult.Refuse("Layer-Previous is disabled. Use LAYERPMODE to turn it on. ");
            }

            CadLayerChange change = _layerChanges.Count == 0 ? null : _layerChanges[_layerChanges.Count - 1];

            if (change == null)
            {
                return CadEditResult.Refuse("*No previous layer status*");
            }

            List<CadEditRecord> planned = change.Plan(Database, notes);
            _layerChanges.Remove(change);

            if (planned.Count == 0)
            {
                return CadEditResult.Done(planned);
            }

            _layerRestoring = true;
            _layerUsed = change;

            try
            {
                CadEditResult result = ApplyEdit("LAYERP", planned, s => change.Apply(s.Database));

                if (!result.Succeeded)
                {
                    _layerChanges.Add(change);
                    _layerChanges.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                }
                else if (
                    Database.Clayer != null
                    && Database.LayerTable.Has(Database.Clayer)
                    && Database.LayerTable[Database.Clayer].IsOff
                )
                {
                    notes.Add("*Warning* The current layer is turned off.");
                }

                return result;
            }
            finally
            {
                _layerRestoring = false;
                _layerUsed = null;
            }
        }

        /// <summary>Hands the step being made the set it kept or took, so that undo and redo of the step move the set with it.</summary>
        private void TieLayerSets(CadUndoStep step)
        {
            step.LayerMade = _layerMade;
            step.LayerUsed = _layerUsed;
            _layerMade = null;
        }

        /// <summary>Moves the sets of the steps undone or redone: an edit that kept a set takes it with it, LAYERP that took one gives it back.</summary>
        private void MoveLayerSets(CadUndoStep step, bool undo)
        {
            if (step.LayerMade != null)
            {
                SetKept(step.LayerMade, !undo);
            }

            if (step.LayerUsed != null)
            {
                SetKept(step.LayerUsed, undo);
            }
        }

        private void SetKept(CadLayerChange change, bool kept)
        {
            _layerChanges.Remove(change);

            if (kept)
            {
                _layerChanges.Add(change);
                _layerChanges.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            }
        }
    }

    /// <summary>The settings of one layer that LAYERP puts back.</summary>
    internal sealed class CadLayerLook
    {
        private CadLayerLook() { }

        public string Name { get; private set; }

        public bool IsOff { get; private set; }

        public bool IsFrozen { get; private set; }

        public bool IsLocked { get; private set; }

        public bool IsPlottable { get; private set; }

        public DxfColor Color { get; private set; }

        public string LinetypeName { get; private set; }

        public DxfLineWeight LineWeight { get; private set; }

        public DxfTransparency Transparency { get; private set; }

        public static CadLayerLook Of(DxfLayerTableRecord layer) =>
            new CadLayerLook
            {
                Name = layer.Name,
                IsOff = layer.IsOff,
                IsFrozen = layer.IsFrozen,
                IsLocked = layer.IsLocked,
                IsPlottable = layer.IsPlottable,
                Color = layer.Color,
                LinetypeName = layer.LinetypeName,
                LineWeight = layer.LineWeight,
                Transparency = layer.Transparency,
            };

        /// <summary>Whether the layer has other settings than these.</summary>
        public bool Differs(DxfDatabase database, DxfLayerTableRecord layer) =>
            layer.IsOff != IsOff
            || layer.IsFrozen != IsFrozen
            || layer.IsLocked != IsLocked
            || layer.IsPlottable != IsPlottable
            || layer.Color.ToString() != Color.ToString()
            || (
                !string.Equals(layer.LinetypeName, LinetypeName, StringComparison.OrdinalIgnoreCase)
                && database.LinetypeTable.Has(LinetypeName ?? string.Empty)
            )
            || layer.LineWeight != LineWeight
            || layer.Transparency.ToString() != Transparency.ToString();
    }

    /// <summary>The settings of every layer and the current layer at one moment.</summary>
    internal sealed class CadLayerSnapshot
    {
        private readonly List<CadLayerLook> _looks = new List<CadLayerLook>();
        private string _current;

        public static CadLayerSnapshot Take(DxfDatabase database)
        {
            var snapshot = new CadLayerSnapshot { _current = database.Clayer };

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                snapshot._looks.Add(CadLayerLook.Of(layer));
            }

            return snapshot;
        }

        /// <summary>What the layers of the drawing have changed since this snapshot: the looks of those that differ, and the current layer if it moved; null where nothing did.</summary>
        public CadLayerChange ChangeTo(DxfDatabase database, int sequence)
        {
            var looks = new List<CadLayerLook>();

            foreach (CadLayerLook look in _looks)
            {
                if (database.LayerTable.Has(look.Name))
                {
                    DxfLayerTableRecord layer = database.LayerTable[look.Name];

                    if (string.Equals(layer.Name, look.Name, StringComparison.Ordinal) && look.Differs(database, layer))
                    {
                        looks.Add(look);
                    }
                }
            }

            bool moved = !string.Equals(_current, database.Clayer, StringComparison.OrdinalIgnoreCase);
            return looks.Count == 0 && !moved ? null : new CadLayerChange(sequence, looks, moved ? _current : null);
        }
    }

    /// <summary>One set LAYERP takes back: the settings the layers it changed had, and the current layer it left, if it moved.</summary>
    internal sealed class CadLayerChange
    {
        private readonly List<CadLayerLook> _looks;
        private readonly string _current;

        public CadLayerChange(int sequence, List<CadLayerLook> looks, string current)
        {
            Sequence = sequence;
            _looks = looks;
            _current = current;
        }

        /// <summary>Gets the order the set was made in.</summary>
        public int Sequence { get; }

        /// <summary>
        /// The records of putting the set back into the drawing as it stands: one for each layer that still differs, the layer
        /// told by its name as the set kept it, and one for the current layer where it can be set. Says what is left out.
        /// </summary>
        public List<CadEditRecord> Plan(DxfDatabase database, List<string> notes)
        {
            var records = new List<CadEditRecord>();

            if (_current != null)
            {
                if (!database.LayerTable.Has(_current))
                {
                    notes.Add("*Note* The previously current layer was not set because it no longer exists.");
                }
                else if (!string.Equals(database.Clayer, database.LayerTable[_current].Name, StringComparison.Ordinal))
                {
                    records.Add(
                        new CadEditRecord(
                            CadEditRecordKind.SettingChanged,
                            null,
                            "Current layer",
                            database.Clayer,
                            database.LayerTable[_current].Name
                        )
                    );
                }
            }

            bool linetype = false;

            foreach (CadLayerLook look in _looks)
            {
                if (!database.LayerTable.Has(look.Name))
                {
                    continue;
                }

                DxfLayerTableRecord layer = database.LayerTable[look.Name];

                if (
                    !string.Equals(layer.LinetypeName, look.LinetypeName, StringComparison.OrdinalIgnoreCase)
                    && !database.LinetypeTable.Has(look.LinetypeName ?? string.Empty)
                )
                {
                    linetype = true;
                }

                if (look.Differs(database, layer))
                {
                    records.Add(
                        new CadEditRecord(
                            CadEditRecordKind.TableChanged,
                            null,
                            "Layer " + look.Name,
                            "changed",
                            "restored"
                        )
                        {
                            IsLayerLook = !(
                                layer.IsPlottable != look.IsPlottable
                                || layer.Transparency.ToString() != look.Transparency.ToString()
                            ),
                        }
                    );
                }
            }

            if (linetype)
            {
                notes.Add(
                    "*Note* The linetype of one or more layers could not be restored because the linetype is no longer loaded."
                );
            }

            return records;
        }

        /// <summary>Writes the set into the drawing: the current layer first, so that a layer the set freezes is not the current one by then.</summary>
        public void Apply(DxfDatabase database)
        {
            if (_current != null && database.LayerTable.Has(_current))
            {
                database.Clayer = database.LayerTable[_current].Name;
            }

            foreach (CadLayerLook look in _looks)
            {
                if (database.LayerTable.Has(look.Name))
                {
                    CadLayerEditor.Restore(database, look);
                }
            }
        }
    }
}
