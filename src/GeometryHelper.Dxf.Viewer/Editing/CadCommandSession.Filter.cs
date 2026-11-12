using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The object selection filter of FILTER, kept for the session as AutoCAD keeps its named filters.</summary>
    public sealed partial class CadCommandSession
    {
        /// <summary>Gets the filter FILTER edits and applies: its current lines and the lists saved under a name.</summary>
        public CadSelectionFilter SelectionFilter { get; } = new CadSelectionFilter();

        /// <summary>Makes the objects a filter passed the Previous set, in place of those the selection gathered; none passed leaves the Previous set empty.</summary>
        internal void RememberFiltered(IReadOnlyList<string> handles) => _previous = new List<string>(handles);
    }
}
