using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A group of a drawing as the Object Grouping dialog lists it.</summary>
    public sealed class CadGroupRow
    {
        internal CadGroupRow(string name, string description, bool selectable, bool unnamed, int count)
        {
            Name = name;
            Description = description;
            Selectable = selectable;
            IsUnnamed = unnamed;
            Count = count;
        }

        /// <summary>Gets the name, STAIR_CORE, or *A1 for an unnamed group.</summary>
        public string Name { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }

        /// <summary>Gets whether a pick takes the group whole.</summary>
        public bool Selectable { get; }

        /// <summary>Gets whether the group is unnamed, *A1, which the list shows only with Include Unnamed.</summary>
        public bool IsUnnamed { get; }

        /// <summary>Gets how many objects the group holds.</summary>
        public int Count { get; }
    }

    /// <summary>
    /// The Object Grouping dialog of CLASSICGROUP, as data its command and the window share: the groups of the drawing, the
    /// one chosen, the name and description typed, and the options of Create Group. A button ending in &lt;, as New &lt;,
    /// closes the dialog with <see cref="CadDialogAction.SelectObjects"/> and its words in <see cref="Pick"/>: the command asks
    /// on the command line, then shows the dialog again. Rename, Description, Explode, Selectable and Re-Order change the
    /// drawing while the dialog stays open; OK keeps every change, Cancel takes them all back, as one undo step.
    /// </summary>
    public sealed class CadGroupManagerRequest
    {
        internal CadGroupManagerRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the groups of the drawing, named and unnamed, in ordinal order of their names, letters of either case alike.</summary>
        public IReadOnlyList<CadGroupRow> Groups { get; internal set; } = Array.Empty<CadGroupRow>();

        /// <summary>Gets or sets the name of the group chosen in the list; null for none.</summary>
        public string Selected { get; set; }

        /// <summary>Gets or sets the Group Name typed: the name of a new group, or the new name Rename gives the group chosen.</summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>Gets or sets the Description typed: that of a new group, or the one Description gives the group chosen.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets whether unnamed groups are listed (Include Unnamed).</summary>
        public bool IncludeUnnamed { get; set; }

        /// <summary>Gets or sets whether a group New makes is selectable; true until set.</summary>
        public bool NewSelectable { get; set; } = true;

        /// <summary>Gets or sets whether a group New makes is unnamed.</summary>
        public bool NewUnnamed { get; set; }

        /// <summary>
        /// Gets or sets the button ending in &lt; the dialog was closed with: Find Name, Highlight, New, Remove or Add; for
        /// Highlight from Order Group, the objects from <see cref="HighlightFrom"/> on, <see cref="HighlightCount"/> of them.
        /// </summary>
        public string Pick { get; set; }

        /// <summary>Gets or sets the first position Highlight shows; nought for the whole group.</summary>
        public int HighlightFrom { get; set; }

        /// <summary>Gets or sets how many objects Highlight shows; nought or less for every one from <see cref="HighlightFrom"/> on.</summary>
        public int HighlightCount { get; set; }

        /// <summary>Gets what the last change or pick said where it was refused, as "A group with this name already exists."; null where it was made.</summary>
        public string Message { get; internal set; }

        /// <summary>Gets or sets what the window's dialog was closed with: OK, Cancel or Select objects; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>What the command does on a change made with the dialog open: the change's name, then the numbers of Re-Order.</summary>
        internal Func<string, int[], string> Changing { get; set; }

        /// <summary>Rename: the group chosen given the Group Name typed.</summary>
        /// <returns>Whether it was renamed; <see cref="Message"/> says why not.</returns>
        public bool Rename() => Change("Rename");

        /// <summary>Description: the group chosen given the Description typed.</summary>
        /// <returns>Whether it was changed.</returns>
        public bool ChangeDescription() => Change("Description");

        /// <summary>Explode: the group chosen taken apart, its objects kept.</summary>
        /// <returns>Whether it was exploded.</returns>
        public bool Explode() => Change("Explode");

        /// <summary>Selectable: whether a pick takes the group chosen whole, switched.</summary>
        /// <returns>Whether it was switched.</returns>
        public bool ToggleSelectable() => Change("Selectable");

        /// <summary>
        /// Re-Order: objects of the group chosen moved to another position, as Order Group's Re-Order moves them: the group
        /// A, B, C, D with 1 to 3, 1 object, reads A, C, D, B.
        /// </summary>
        /// <param name="from">The position the objects are taken from, nought the first.</param>
        /// <param name="to">The position they are put at.</param>
        /// <param name="count">How many objects.</param>
        /// <returns>Whether they were moved; <see cref="Message"/> says why not.</returns>
        public bool Reorder(int from, int to, int count) => Change("Order", from, to, count);

        /// <summary>Reverse Order: the objects of the group chosen put in the opposite order.</summary>
        /// <returns>Whether they were.</returns>
        public bool Reverse() => Change("Reverse");

        private bool Change(string what, params int[] numbers)
        {
            if (Changing == null)
            {
                return false;
            }

            Message = Changing(what, numbers);
            return Message == null;
        }
    }
}
