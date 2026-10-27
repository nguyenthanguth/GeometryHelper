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
    /// it; read, not changed, and only from the legacy dictionary ACAD_LAYERFILTERS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The root, All, is made by AutoCAD, not kept in the drawing: it is not deleted or renamed. Nested in it are the named
    /// filters the drawing keeps in the dictionary ACAD_LAYERFILTERS of the layer table's extension dictionary, one for each
    /// entry, named by it, in the order of the record: a drawing with the filters Steel and Notes gives the two.
    /// </para>
    /// <para>
    /// ACAD_LAYERFILTERS holds the filters of AutoCAD 2004 and earlier, and only the names of its entries are read. The
    /// property and group filters of AutoCAD 2005 and later, which it keeps in a dictionary of its own, AcLyDictionary, are
    /// not read: a drawing saved by AutoCAD 2005 or later most likely gives no filter but All.
    /// </para>
    /// <para>
    /// What a filter lets through (AutoCAD's <c>FilterExpression</c>), the groups of layers (<c>LayerGroup</c>), the filters
    /// nested deeper and the filter that is current are kept in records whose groups are in neither the DXF reference nor
    /// ezdxf, and no drawing at hand shows them: they are not read, and a filter is not added, changed or deleted here.
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

        /// <summary>Gets whether AutoCAD makes the filter rather than the drawing keeping it, as AutoCAD's <c>DynamicallyGenerated</c>: true for the root, All.</summary>
        public bool DynamicallyGenerated { get; }

        /// <summary>Gets whether the filter may be deleted, as AutoCAD's <c>AllowDelete</c>: false for the root, true for a filter the drawing keeps.</summary>
        public bool AllowDelete => !DynamicallyGenerated;

        /// <summary>Gets whether the filter may be renamed, as AutoCAD's <c>AllowRename</c>: false for the root, true for a filter the drawing keeps.</summary>
        public bool AllowRename => !DynamicallyGenerated;

        /// <summary>Nests a filter the drawing keeps in this one, last.</summary>
        internal void Nest(string name) => _nested.Add(new DxfLayerFilter(name, this, false));

        /// <summary>Gives the filter as a line for debugging: its name.</summary>
        /// <returns>The line, such as <c>Steel</c>.</returns>
        public override string ToString() => Name;
    }
}
