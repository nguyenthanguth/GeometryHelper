using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    public static partial class CadLayerEditor
    {
        /// <summary>
        /// Turns layers on and off to show some alone, as LAYWALK and the Layer Translator's "Show layer contents when selected"
        /// do, each layer given as the wish for its switches: the layers named are turned on and thawed, every other layer is
        /// turned off and keeps the frozen state it had. One edit; a layer already as wished is left alone.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="what">The name of the step, as LAYWALK.</param>
        /// <param name="wishes">The layers with whether each is to be off and whether it is to be frozen.</param>
        /// <returns>What came of it; done with nothing written where no layer differs.</returns>
        internal static CadEditResult SetShown(
            CadDocument document,
            string what,
            IReadOnlyList<KeyValuePair<string, KeyValuePair<bool, bool>>> wishes
        )
        {
            var changes = new List<Tuple<string, bool, bool>>();
            var planned = new List<CadEditRecord>();

            foreach (KeyValuePair<string, KeyValuePair<bool, bool>> wish in wishes)
            {
                if (!document.Database.LayerTable.Has(wish.Key))
                {
                    continue;
                }

                DxfLayerTableRecord layer = document.Database.LayerTable[wish.Key];
                bool current = string.Equals(layer.Name, document.Database.Clayer, StringComparison.OrdinalIgnoreCase);
                bool off = wish.Value.Key;
                bool frozen = wish.Value.Value && !current;

                if (layer.IsOff == off && layer.IsFrozen == frozen)
                {
                    continue;
                }

                changes.Add(Tuple.Create(layer.Name, off, frozen));
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Show " + layer.Name,
                        (layer.IsOff ? "Off" : "On") + (layer.IsFrozen ? "/Frozen" : string.Empty),
                        (off ? "Off" : "On") + (frozen ? "/Frozen" : string.Empty)
                    )
                    {
                        IsLayerLook = true,
                    }
                );
            }

            if (changes.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return document.ApplyEdit(what, planned, s =>
            {
                foreach (Tuple<string, bool, bool> change in changes)
                {
                    DxfLayerTableRecord layer = s.Database.LayerTable[change.Item1];

                    if (layer.IsOff != change.Item2)
                    {
                        Write(layer, CadXrefLayers.Off, l => l.IsOff = change.Item2);
                    }

                    if (layer.IsFrozen != change.Item3)
                    {
                        Write(layer, CadXrefLayers.Frozen, l => l.IsFrozen = change.Item3);
                    }
                }
            });
        }
    }

    /// <summary>
    /// Shows some layers of a drawing alone, for a while, and puts the others back as they were: what LAYWALK and the Layer
    /// Translator's "Show layer contents when selected" do. The switches of every layer are noted when it is made; each
    /// <see cref="Show"/> turns every layer but those named off, thawing those named, and <see cref="Back"/> gives every
    /// layer the switches it had. Each is an edit of the drawing, so that the command that holds it keeps them in its one
    /// undo step.
    /// </summary>
    internal sealed class CadLayerShowing
    {
        private readonly CadDocument _document;
        private readonly string _what;
        private readonly Dictionary<string, KeyValuePair<bool, bool>> _before = new Dictionary<
            string,
            KeyValuePair<bool, bool>
        >(StringComparer.OrdinalIgnoreCase);

        /// <summary>Notes the switches of every layer of the drawing.</summary>
        /// <param name="document">The document.</param>
        /// <param name="what">The name of the step the edits are, as LAYWALK.</param>
        internal CadLayerShowing(CadDocument document, string what)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _what = what;

            foreach (DxfLayerTableRecord layer in document.Database.LayerTable)
            {
                _before[layer.Name] = new KeyValuePair<bool, bool>(layer.IsOff, layer.IsFrozen);
            }
        }

        /// <summary>Gets the names of the layers shown at the moment: those turned on and thawed by the last <see cref="Show"/>; empty where all are as they were.</summary>
        internal IReadOnlyList<string> Shown { get; private set; } = Array.Empty<string>();

        /// <summary>Turns every layer but those named off, those named on and thawed; none named is <see cref="Back"/>.</summary>
        internal CadEditResult Show(IReadOnlyCollection<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return Back();
            }

            var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var wishes = new List<KeyValuePair<string, KeyValuePair<bool, bool>>>();

            foreach (DxfLayerTableRecord layer in _document.Database.LayerTable)
            {
                bool frozen = _before.TryGetValue(layer.Name, out KeyValuePair<bool, bool> before) && before.Value;
                wishes.Add(
                    new KeyValuePair<string, KeyValuePair<bool, bool>>(
                        layer.Name,
                        wanted.Contains(layer.Name)
                            ? new KeyValuePair<bool, bool>(false, false)
                            : new KeyValuePair<bool, bool>(true, frozen)
                    )
                );
            }

            Shown = _document
                .Database.LayerTable.Select(l => l.Name)
                .Where(n => wanted.Contains(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
            return CadLayerEditor.SetShown(_document, _what, wishes);
        }

        /// <summary>Gives every layer the switches it had when this was made; a layer made since is left as it is, a layer deleted since is forgotten.</summary>
        internal CadEditResult Back()
        {
            Shown = Array.Empty<string>();
            var wishes = new List<KeyValuePair<string, KeyValuePair<bool, bool>>>();

            foreach (
                KeyValuePair<string, KeyValuePair<bool, bool>> before in _before.OrderBy(
                    b => b.Key,
                    StringComparer.OrdinalIgnoreCase
                )
            )
            {
                wishes.Add(new KeyValuePair<string, KeyValuePair<bool, bool>>(before.Key, before.Value));
            }

            return CadLayerEditor.SetShown(_document, _what, wishes);
        }

        /// <summary>Forgets a layer, as one deleted while the layers are shown.</summary>
        internal void Forget(string name) => _before.Remove(name);
    }
}
