using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What MLSTYLE gives its Multiline Style dialog: the styles of the drawing, the current one, and the buttons of AutoCAD
    /// 2021's dialog as calls that act at once, as AutoCAD's do, each one edit of MLSTYLE's one undo step: New (<see cref="Add"/>),
    /// Modify, Rename, Delete, Load and Save. The style made current by Set Current (<see cref="Current"/>) is set when the
    /// dialog closes with OK. Each call gives the reason it was refused, in the words of the typed MLSTYLE, or null.
    /// </summary>
    public sealed class CadMlineStyleRequest
    {
        private readonly CadCommandContext _context;

        internal CadMlineStyleRequest(CadCommandContext context)
        {
            _context = context;
            Current = CadCommands.MlineStyleCurrentName(context.Database);
        }

        /// <summary>Gets the document whose styles are listed.</summary>
        public CadDocument Document => _context.Document;

        /// <summary>Gets the names of the styles of the drawing, in capitals, sorted by their letters as the typed "?" lists them.</summary>
        public IReadOnlyList<string> Names => CadCommands.MlineStyleNames(_context.Database);

        /// <summary>Gets or sets the style Set Current chose, made current when the dialog closes with OK.</summary>
        public string Current { get; set; }

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }

        /// <summary>Gets a copy of what a style is made of; null for a name the drawing has not.</summary>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>The settings, a copy.</returns>
        public DxfMlineStyleSettings SettingsOf(string name) =>
            CadCommands.MlineStyleFound(_context.Database, name)?.GetSettings();

        /// <summary>Gets whether only the description of a style may be modified: STANDARD, or a style a multiline is drawn by.</summary>
        /// <param name="name">The name.</param>
        /// <returns>True where the elements, caps and fill are kept.</returns>
        public bool IsLocked(string name) => CadCommands.MlineStyleLocked(_context.Database, name);

        /// <summary>Gets why a style may not be deleted (STANDARD, the current style, one in use); null where it may.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The reason, or null.</returns>
        public string WhyNotDeleted(string name) => CadCommands.MlineStyleDeleteRefusal(_context.Database, name);

        /// <summary>Gets why a name cannot be given to a new style; null for a name AutoCAD takes that the drawing has not.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The reason, or null.</returns>
        public string WhyNotNamed(string name) => CadCommands.MlineStyleNameIsRefused(_context.Database, name);

        /// <summary>New: adds a style of a name.</summary>
        /// <param name="name">The name.</param>
        /// <param name="settings">What it is made of.</param>
        /// <returns>The reason it was refused, or null.</returns>
        public string Add(string name, DxfMlineStyleSettings settings) =>
            CadCommands.MlineStyleAdd(_context, name, settings);

        /// <summary>Modify: gives a style new settings; of STANDARD or a style in use, only the description is taken.</summary>
        /// <param name="name">The name.</param>
        /// <param name="settings">What it is made of.</param>
        /// <returns>The reason it was refused, or null.</returns>
        public string Modify(string name, DxfMlineStyleSettings settings) =>
            CadCommands.MlineStyleModify(_context, name, settings);

        /// <summary>Rename: a style other than STANDARD takes a new name, the current style kept current.</summary>
        /// <param name="oldName">The name it has.</param>
        /// <param name="newName">The name it takes.</param>
        /// <returns>The reason it was refused, or null.</returns>
        public string Rename(string oldName, string newName)
        {
            string refusal = CadCommands.MlineStyleRename(_context, oldName, newName);

            if (refusal == null && string.Equals(Current, oldName, StringComparison.OrdinalIgnoreCase))
            {
                Current = newName.Trim().ToUpperInvariant();
            }

            return refusal;
        }

        /// <summary>Delete: removes a style that is not STANDARD, current or in use.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The reason it was refused, or null.</returns>
        public string Delete(string name) => CadCommands.MlineStyleDelete(_context, name);

        /// <summary>The names of the styles a file of them holds, in its order (Load's list).</summary>
        /// <param name="path">The file, a .mln.</param>
        /// <param name="refusal">Why it was not read; null where it was.</param>
        /// <returns>The names; none where it was not read.</returns>
        public IReadOnlyList<string> NamesInFile(string path, out string refusal) =>
            CadCommands.MlineStyleNamesInFile(_context, path, out refusal);

        /// <summary>Load: adds a style of a file of them.</summary>
        /// <param name="path">The file.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The reason it was refused, or null.</returns>
        public string Load(string path, string name) => CadCommands.MlineStyleLoad(_context, path, name);

        /// <summary>Save: writes a style to a file of them, its other styles kept.</summary>
        /// <param name="name">The style's name.</param>
        /// <param name="path">The file.</param>
        /// <returns>The reason it was refused, or null.</returns>
        public string Save(string name, string path) => CadCommands.MlineStyleSave(_context, name, path);
    }

    /// <summary>
    /// What MLEDIT gives its Multiline Edit Tools dialog: the twelve tools of AutoCAD 2021's dialog, in its rows of four, and
    /// the tool picked, which MLEDIT then asks the multilines of, as the typed option of the same letters.
    /// </summary>
    public sealed class CadMlineEditRequest
    {
        private static readonly string[][] AllTools =
        {
            new[] { "CC", "Closed Cross" },
            new[] { "CT", "Closed Tee" },
            new[] { "CJ", "Corner Joint" },
            new[] { "CS", "Cut Single" },
            new[] { "OC", "Open Cross" },
            new[] { "OT", "Open Tee" },
            new[] { "AV", "Add Vertex" },
            new[] { "CA", "Cut All" },
            new[] { "MC", "Merged Cross" },
            new[] { "MT", "Merged Tee" },
            new[] { "DV", "Delete Vertex" },
            new[] { "WA", "Weld All" },
        };

        /// <summary>
        /// Gets the tools as the dialog lays them out, row by row, four to a row: the option letters MLEDIT takes and the name
        /// under the picture, Closed Cross (CC) first and Weld All (WA) last.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Tools { get; } =
            Array.ConvertAll(AllTools, t => new KeyValuePair<string, string>(t[0], t[1]));

        /// <summary>Gets or sets the letters of the tool picked; null where the dialog was closed with none.</summary>
        public string Tool { get; set; }
    }
}
