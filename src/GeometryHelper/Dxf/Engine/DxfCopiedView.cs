using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The view context an annotative HATCH carries with its copy until the copy is added to a drawing: the name of its entry and
    /// the groups of its record from AcDbObjectContextData on, as they stood when it was taken. Either the original's view
    /// context as it was (<see cref="IsView"/>), or the original's default context before the copy was moved, from which the view
    /// context is made.
    /// </summary>
    internal sealed class DxfCopiedView
    {
        internal DxfCopiedView(string name, IReadOnlyList<DxfTypedValue> groups, bool isView)
        {
            Name = name;
            Groups = groups;
            IsView = isView;
        }

        /// <summary>The name of its entry, that of the default context.</summary>
        internal string Name { get; }

        /// <summary>The groups of its record from AcDbObjectContextData on.</summary>
        internal IReadOnlyList<DxfTypedValue> Groups { get; }

        /// <summary>Whether the groups are those of a view context (ACDB_HATCHVIEWCONTEXTDATA_CLASS), else of a default context.</summary>
        internal bool IsView { get; }
    }
}
