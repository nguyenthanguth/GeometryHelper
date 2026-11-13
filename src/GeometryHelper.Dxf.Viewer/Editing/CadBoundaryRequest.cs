using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What BOUNDARY gives its Boundary Creation dialog: the settings it shares with -BOUNDARY, island detection and the
    /// object type, the boundary set, and whether the dialog closed with Pick Points, BOUNDARY then asking "Pick internal
    /// point:" until Enter, or with New, BOUNDARY then asking "Select objects:" and showing the dialog again.
    /// </summary>
    public sealed class CadBoundaryRequest
    {
        /// <summary>Gets or sets whether the islands inside an area are bounded too: on as AutoCAD starts.</summary>
        public bool IslandDetection { get; set; } = true;

        /// <summary>Gets or sets whether the boundaries are made regions rather than polylines.</summary>
        public bool IsRegion { get; set; }

        /// <summary>Gets or sets whether the dialog closed with Pick Points; false where it was cancelled, nothing then kept.</summary>
        public bool IsPicking { get; set; }

        /// <summary>
        /// Gets or sets whether the dialog closed with the New of Boundary set, BOUNDARY then asking "Select objects:" and
        /// showing the dialog again with the objects picked as the set.
        /// </summary>
        public bool IsSelectingSet { get; set; }

        /// <summary>
        /// Gets or sets the objects of the boundary set New picked last, the Existing set of the list; null where none was
        /// picked.
        /// </summary>
        public IReadOnlyList<string> ExistingSet { get; set; }

        /// <summary>
        /// Gets or sets whether the areas are looked for among the <see cref="ExistingSet"/> alone (Existing set) rather than
        /// every object shown (Current viewport).
        /// </summary>
        public bool UsesExistingSet { get; set; }
    }
}
