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
    /// The states are read and listed, not saved, restored, renamed or taken apart. AutoCAD 2021 writes a state's XRECORD as
    /// its mask (91), its description (301), 290 and 302, then each layer: its handle (330), or its name (8) for a state of
    /// an xref, then its flags (90), colour (62), lineweight (370), linetype (331) and plot style (1), and 440; the mask's flag
    /// 0x8000 marks a state hidden. Only the names and those two marks are read here: AutoCAD's <c>SaveLayerState</c>,
    /// <c>RestoreLayerState</c>, <c>RenameLayerState</c>, <c>DeleteLayerState</c>, <c>GetLayerStateLayers</c>,
    /// <c>GetLayerStateMask</c> and <c>GetLayerStateDescription</c> are not here.
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
        /// Gets the names of the layer states, as AutoCAD's <c>GetLayerStateNames(true, true)</c>: the entries of the dictionary
        /// of the layer states, in the order of the record, every one, hidden or of an xref.
        /// </summary>
        /// <returns>The names, a new list; empty where the drawing has no layer state.</returns>
        public IReadOnlyList<string> GetLayerStateNames() => GetLayerStateNames(true, true);

        /// <summary>
        /// Gets the names of the layer states, as AutoCAD's <c>GetLayerStateNames</c>: the entries of the dictionary of the
        /// layer states, in the order of the record, a hidden state only where <paramref name="includeHidden"/> says and the
        /// state of an xref only where <paramref name="includeXref"/> says. A state is hidden where flag 0x8000 of the mask of
        /// its XRECORD (group 91) is set, as AutoCAD 2021 saves one of the mask 0x8001; it is of an xref where its XRECORD names
        /// its layers by name (group 8) and none by handle (330), as AutoCAD keeps the state of an attached xref, whose name it
        /// gives as the xref's and the state's joined by <c>__</c>. A drawing AutoCAD saved with the states GH_PLAN and
        /// GH_NOTES_OFF and the hidden GH_HIDDEN gives the two for <c>(false, false)</c> and all three for <c>(true, false)</c>.
        /// </summary>
        /// <param name="includeHidden">Whether the hidden states are given.</param>
        /// <param name="includeXref">Whether the states of xrefs are given.</param>
        /// <returns>The names, a new list; empty where the drawing has no layer state.</returns>
        public IReadOnlyList<string> GetLayerStateNames(bool includeHidden, bool includeXref)
        {
            var names = new List<string>();
            DxfDBDictionary states = LayerStatesDictionary;

            if (states != null)
            {
                foreach (DxfDBDictionaryEntry entry in states)
                {
                    IReadOnlyList<DxfTypedValue> data = (entry.Value as DxfXrecord)?.Data ?? new DxfTypedValue[0];
                    bool hidden = false;
                    bool byName = false;
                    bool byHandle = false;

                    foreach (DxfTypedValue group in data)
                    {
                        if (group.TypeCode == 91 && int.TryParse(group.Value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int mask))
                        {
                            hidden |= (mask & 0x8000) != 0;
                        }

                        byName |= group.TypeCode == 8;
                        byHandle |= group.TypeCode == 330;
                    }

                    if ((includeHidden || !hidden) && (includeXref || !(byName && !byHandle)))
                    {
                        names.Add(entry.Key);
                    }
                }
            }

            return names.AsReadOnly();
        }

        /// <summary>Gives the manager as a line for debugging: how many layer states the drawing has.</summary>
        /// <returns>The line, such as <c>2 layer states</c>.</returns>
        public override string ToString() => GetLayerStateNames().Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " layer states";
    }
}
