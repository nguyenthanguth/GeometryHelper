using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The layer states of a drawing, as AutoCAD's <c>LayerStateManager</c> lists them: the XRECORDs of the dictionary
    /// ACAD_LAYERSTATES in the extension dictionary of the layer table, one for each state, named by its entry. Reached by
    /// <see cref="DxfDatabase.LayerStateManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drawing AutoCAD saved with the states Plan and Sections gives <c>GetLayerStateNames()</c> as Plan and Sections, and
    /// <c>HasLayerState("plan")</c> true, letters of either case the same as AutoCAD finds a state; each state's record is
    /// <c>LayerStatesDictionary.GetAt("Plan")</c>, a <see cref="DxfXrecord"/> whose <see cref="DxfXrecord.Data"/> are its
    /// groups as the file holds them.
    /// </para>
    /// <para>
    /// The states are read and listed, not saved, restored, renamed or taken apart: what the groups of a state's XRECORD hold
    /// (its layers, its mask, its description, whether it is hidden or of an xref) is in neither the DXF reference nor ezdxf,
    /// and no drawing at hand shows one, so AutoCAD's <c>SaveLayerState</c>, <c>RestoreLayerState</c>,
    /// <c>RenameLayerState</c>, <c>DeleteLayerState</c>, <c>GetLayerStateLayers</c>, <c>GetLayerStateMask</c> and
    /// <c>GetLayerStateDescription</c> are not here.
    /// </para>
    /// </remarks>
    public sealed class DxfLayerStateManager
    {
        private readonly DxfDatabase _database;

        internal DxfLayerStateManager(DxfDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Gets the dictionary of the layer states, as AutoCAD's <c>LayerStatesDictionaryId</c> names it: the DICTIONARY the
        /// layer table's extension dictionary names ACAD_LAYERSTATES; null where the drawing has none, as a drawing no state was
        /// saved in.
        /// </summary>
        public DxfDBDictionary LayerStatesDictionary
        {
            get
            {
                DxfDBDictionary extension = _database.LayerTable.ExtensionDictionary;
                return extension != null && extension.Contains("ACAD_LAYERSTATES") ? extension.GetAt("ACAD_LAYERSTATES") as DxfDBDictionary : null;
            }
        }

        /// <summary>Gets whether the drawing has a layer state of a name, as AutoCAD's <c>HasLayerState</c>, letters of either case the same.</summary>
        /// <param name="name">The name of the state.</param>
        /// <returns>Whether the dictionary of the layer states has an entry of the name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public bool HasLayerState(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            DxfDBDictionary states = LayerStatesDictionary;
            return states != null && states.Contains(name);
        }

        /// <summary>
        /// Gets the names of the layer states, as AutoCAD's <c>GetLayerStateNames</c>: the entries of the dictionary of the
        /// layer states, in the order of the record; every one, since which are hidden or of an xref is kept in groups of their
        /// XRECORDs that are not confirmed.
        /// </summary>
        /// <returns>The names, a new list; empty where the drawing has no layer state.</returns>
        public IReadOnlyList<string> GetLayerStateNames()
        {
            var names = new List<string>();
            DxfDBDictionary states = LayerStatesDictionary;

            if (states != null)
            {
                foreach (DxfDBDictionaryEntry entry in states)
                {
                    names.Add(entry.Key);
                }
            }

            return names.AsReadOnly();
        }

        /// <summary>Gives the manager as a line for debugging: how many layer states the drawing has.</summary>
        /// <returns>The line, such as <c>2 layer states</c>.</returns>
        public override string ToString() => GetLayerStateNames().Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " layer states";
    }
}
