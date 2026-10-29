using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The tree of the layer filters of a drawing, as AutoCAD's <c>LayerFilterTree</c>: the filter All at its root, the
    /// filters the drawing keeps nested in it. Given by <see cref="DxfDatabase.LayerFilters"/>, as the drawing has them when it
    /// is asked for.
    /// </summary>
    public sealed class DxfLayerFilterTree
    {
        internal DxfLayerFilterTree(DxfLayerFilter root)
        {
            Root = root;
        }

        /// <summary>Gets the filter at the root of the tree, as AutoCAD's <c>Root</c>: All, which every layer passes.</summary>
        public DxfLayerFilter Root { get; }

        /// <summary>Gives the tree as a line for debugging: how many filters are nested in its root.</summary>
        /// <returns>The line, such as <c>All, 2 filters</c>.</returns>
        public override string ToString() => Root.Name + ", " + Root.NestedFilters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " filters";
    }

    /// <summary>
    /// A layer filter of a drawing, as AutoCAD's <c>LayerFilter</c>: its name, the filter it is nested in and those nested in
    /// it; read, not changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The root, All, is made by AutoCAD, not kept in the drawing: it is not deleted or renamed. Nested in it are the named
    /// filters the drawing keeps, as AutoCAD 2021 lists them: first those of the dictionary ACLYDICTIONARY of the layer table's
    /// extension dictionary, where AutoCAD 2005 and later keep property filters and groups, each an XRECORD named by its group
    /// 300, in the order of the dictionary; then those of ACAD_LAYERFILTERS, the dictionary of AutoCAD 2004 and earlier, named
    /// by their entries, where ACLYDICTIONARY has none of the name (AutoCAD keeps a property filter in both); last the three
    /// AutoCAD makes, All Used Layers, Unreconciled New Layers and Viewport Overrides. A drawing with the property filter
    /// GH_STEEL and the group GH_NOTES gives those two, then the three.
    /// </para>
    /// <para>
    /// What a filter lets through (AutoCAD's <c>FilterExpression</c>, group 301 of a property filter's XRECORD), the layers of
    /// a group (its 330s), the filters nested deeper and the filter that is current are not read, and a filter is not added,
    /// changed or deleted here.
    /// </para>
    /// </remarks>
    public sealed class DxfLayerFilter
    {
        private readonly List<DxfLayerFilter> _nested = new List<DxfLayerFilter>();

        internal DxfLayerFilter(string name, DxfLayerFilter parent, bool dynamicallyGenerated)
        {
            Name = name;
            Parent = parent;
            DynamicallyGenerated = dynamicallyGenerated;
        }

        /// <summary>Gets the name of the filter, as AutoCAD's <c>Name</c>: All for the root, else the name of its entry.</summary>
        public string Name { get; }

        /// <summary>Gets the filter this one is nested in, as AutoCAD's <c>Parent</c>; null for the root.</summary>
        public DxfLayerFilter Parent { get; }

        /// <summary>Gets the filters nested in this one, as AutoCAD's <c>NestedFilters</c>, in the order of the record; empty for one with none.</summary>
        public IReadOnlyList<DxfLayerFilter> NestedFilters => _nested.AsReadOnly();

        /// <summary>Gets whether AutoCAD makes the filter rather than the drawing keeping it, as AutoCAD's <c>DynamicallyGenerated</c>: true for the root, All, and the three nested in it that AutoCAD makes.</summary>
        public bool DynamicallyGenerated { get; }

        /// <summary>Gets whether the filter may be deleted, as AutoCAD's <c>AllowDelete</c>: false for a filter AutoCAD makes, true for one the drawing keeps.</summary>
        public bool AllowDelete => !DynamicallyGenerated;

        /// <summary>Gets whether the filter may be renamed, as AutoCAD's <c>AllowRename</c>: false for a filter AutoCAD makes, true for one the drawing keeps.</summary>
        public bool AllowRename => !DynamicallyGenerated;

        /// <summary>Nests a filter in this one, last: one the drawing keeps, or one AutoCAD makes.</summary>
        internal void Nest(string name, bool dynamicallyGenerated = false) => _nested.Add(new DxfLayerFilter(name, this, dynamicallyGenerated));

        /// <summary>Whether a filter of a name, letters of either case the same, is nested in this one.</summary>
        internal bool Nests(string name) => _nested.Exists(f => string.Equals(f.Name, name, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>Gives the filter as a line for debugging: its name.</summary>
        /// <returns>The line, such as <c>Steel</c>.</returns>
        public override string ToString() => Name;
    }
}
