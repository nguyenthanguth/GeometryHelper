using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>How objects are picked in the document: groups taken whole (PICKSTYLE), overlapping objects cycled through (SELECTIONCYCLING), and objects selected before a command taken by it (PICKFIRST).</summary>
    public sealed partial class CadDocument
    {
        /// <summary>
        /// Gets or sets PICKSTYLE, as AutoCAD's: 1 (the default) picks a selectable group whole when one of its objects is
        /// picked, 0 picks the object alone; 2 and 3 are AutoCAD's associative hatch selection, which the presenter takes as 0
        /// and 1. Ctrl+Shift+A switches between 0 and 1. Kept by the presenter, not in the drawing, as AutoCAD keeps it in the
        /// registry.
        /// </summary>
        public int PickStyle { get; set; } = 1;

        /// <summary>
        /// Gets or sets SELECTIONCYCLING, as AutoCAD's: 2 (the default) for the list of the objects under a pick where more than
        /// one lies there, 1 for cycling through them with Shift+Space only, 0 off; the window shows the list and cycles. Kept
        /// by the presenter, not in the drawing. Ctrl+W switches it between 0 and 2, as AutoCAD's does; the command
        /// SELECTIONCYCLING sets it.
        /// </summary>
        public int SelectionCycling { get; set; } = 2;

        /// <summary>
        /// Gets or sets PICKFIRST, as AutoCAD's: 1 (the default) lets a command that works on objects (MOVE, ERASE) take those
        /// selected before it started; 0 makes it ask "Select objects:" whatever is selected. Kept by the presenter, not in the
        /// drawing, as AutoCAD keeps it in the registry; the variable PICKFIRST sets it.
        /// </summary>
        public int PickFirst { get; set; } = 1;

        /// <summary>
        /// Gets or sets PICKADD, as AutoCAD's: 1 (the default) and 2 add each object picked, or taken by a window, to those
        /// selected, Shift taking it out; 0 makes what is picked last the selection, Shift adding to it. So with 0, a click on
        /// line A and then on line B leaves B alone selected, and a click on B with Shift held leaves both. AutoCAD's 2 keeps what
        /// its SELECT command took selected after it, which the presenter, with no SELECT, takes as 1. Kept by the presenter,
        /// not in the drawing, as AutoCAD keeps it in the registry; the variable PICKADD sets it.
        /// </summary>
        public int PickAdd { get; set; } = 1;

        /// <summary>
        /// Gets the objects a pick takes with PICKSTYLE on: those given, then the other members of each selectable group any of
        /// them is in, in ordinal order of their handles. A line and a circle of the group G1 and a text of none: picking the
        /// line gives the line and the circle. With PICKSTYLE off, those given.
        /// </summary>
        /// <param name="handles">The handles picked.</param>
        /// <returns>The handles with the members of their groups, each once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public IReadOnlyList<string> WithGroups(IEnumerable<string> handles)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            var given = new List<string>(handles);

            if ((PickStyle & 1) == 0 || given.Count == 0)
            {
                return given;
            }

            var taken = new HashSet<string>(given, StringComparer.OrdinalIgnoreCase);
            var added = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfGroup group in Groups())
            {
                if (!group.Selectable)
                {
                    continue;
                }

                DxfEntity[] members = group.GetAllEntityIds();
                bool hit = false;

                foreach (DxfEntity member in members)
                {
                    if (member.Handle != null && taken.Contains(member.Handle))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    continue;
                }

                foreach (DxfEntity member in members)
                {
                    if (member.Handle != null && !taken.Contains(member.Handle) && FindEntity(member.Handle) != null)
                    {
                        added.Add(member.Handle);
                    }
                }
            }

            given.AddRange(added);
            return given;
        }

        /// <summary>The groups of the drawing in the order of their dictionary; none where it has no ACAD_GROUP.</summary>
        internal List<DxfGroup> Groups()
        {
            var groups = new List<DxfGroup>();
            DxfDBDictionary dictionary = Database.GroupDictionary;

            if (dictionary == null)
            {
                return groups;
            }

            foreach (DxfDBDictionaryEntry entry in dictionary)
            {
                if (entry.Value is DxfGroup group && !group.IsErased)
                {
                    groups.Add(group);
                }
            }

            return groups;
        }
    }
}
