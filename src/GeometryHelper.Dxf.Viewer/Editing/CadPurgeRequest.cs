using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A named object of a drawing as the Purge dialog lists it: its kind, its name, and for one in use the reasons it cannot
    /// be purged and the objects of the space shown that use it.
    /// </summary>
    public sealed class CadPurgeItem
    {
        internal CadPurgeItem(
            string kind,
            string keyword,
            string name,
            IReadOnlyList<string> reasons,
            IReadOnlyList<string> objects
        )
        {
            Kind = kind;
            Keyword = keyword;
            Name = name;
            Reasons = reasons ?? Array.Empty<string>();
            Objects = objects ?? Array.Empty<string>();
        }

        /// <summary>Gets the kind as the dialog's tree names it, Layers.</summary>
        public string Kind { get; }

        /// <summary>Gets the name, OLD_GRID.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets why an object in use cannot be purged, a sentence each, as "Layer WALLS is the current layer." and "Used by 214
        /// objects in model space and 3 in layouts."; empty for one that can.
        /// </summary>
        public IReadOnlyList<string> Reasons { get; }

        /// <summary>Gets the handles of the objects of the space shown that use it, in ordinal order, which Select Objects selects.</summary>
        public IReadOnlyList<string> Objects { get; }

        /// <summary>The keyword of its kind, as -PURGE offers it: LAyers.</summary>
        internal string Keyword { get; }
    }

    /// <summary>A kind of named object the Purge dialog lists, Layers, and those of it listed.</summary>
    public sealed class CadPurgeCategory
    {
        internal CadPurgeCategory(string name, IReadOnlyList<CadPurgeItem> items)
        {
            Name = name;
            Items = items;
        }

        /// <summary>Gets the name of the kind, Layers.</summary>
        public string Name { get; }

        /// <summary>Gets the objects listed, in ordinal order of their names, letters of either case alike.</summary>
        public IReadOnlyList<CadPurgeItem> Items { get; }
    }

    /// <summary>
    /// The Purge dialog of PURGE, as data its command and the window share: the named objects nothing uses, by kind, the
    /// lines and arcs of no length and the texts of no content; and, for its Find Non-Purgeable Items mode, the named objects
    /// in use with the reasons. <see cref="Purge"/> purges while the dialog stays open, each purge one undo step, and reads
    /// the drawing again. A drawing with the layers OLD_GRID and TMP unused lists them under Layers (2).
    /// </summary>
    public sealed class CadPurgeRequest
    {
        internal CadPurgeRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the kinds of named object, each with those of it nothing uses, in alphabetical order of the kinds.</summary>
        public IReadOnlyList<CadPurgeCategory> Purgeable { get; internal set; } = Array.Empty<CadPurgeCategory>();

        /// <summary>Gets the kinds of named object, each with those of it in use, the reasons given.</summary>
        public IReadOnlyList<CadPurgeCategory> InUse { get; internal set; } = Array.Empty<CadPurgeCategory>();

        /// <summary>Gets how many lines, arcs, circles and polylines of no length the drawing holds.</summary>
        public int ZeroLength { get; internal set; }

        /// <summary>Gets how many texts and MTEXTs of no content it holds.</summary>
        public int EmptyText { get; internal set; }

        /// <summary>Gets or sets whether each object is asked about before it is purged (Confirm each item to be purged); true until set.</summary>
        public bool ConfirmEach { get; set; } = true;

        /// <summary>
        /// Gets or sets whether what a purge leaves unused is purged too, again until nothing more is (Purge nested items):
        /// a block purged that held the only INSERT of another purges that one as well. True until set.
        /// </summary>
        public bool PurgeNested { get; set; } = true;

        /// <summary>Gets or sets whether a purge erases the geometry of no length too.</summary>
        public bool PurgeZeroLength { get; set; }

        /// <summary>Gets or sets whether a purge erases the texts of no content too.</summary>
        public bool PurgeEmptyText { get; set; }

        /// <summary>Gets or sets the object in use whose objects Select Objects selects.</summary>
        public CadPurgeItem Selected { get; set; }

        /// <summary>Gets or sets what the window's dialog was closed with: Cancel for Close, Select objects; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>What the command does on <see cref="Purge"/>.</summary>
        internal Func<IReadOnlyList<CadPurgeItem>, int> Purging { get; set; }

        /// <summary>
        /// Purges objects of <see cref="Purgeable"/>, with the geometry of no length and the empty texts where the options
        /// say so, as one undo step, each asked about first where <see cref="ConfirmEach"/> says so; the lists read again.
        /// </summary>
        /// <param name="items">The objects, those checked; Purge All gives every one listed.</param>
        /// <returns>How many named objects were purged, nested ones among them.</returns>
        public int Purge(IReadOnlyList<CadPurgeItem> items) =>
            Purging?.Invoke(items ?? Array.Empty<CadPurgeItem>()) ?? 0;
    }
}
