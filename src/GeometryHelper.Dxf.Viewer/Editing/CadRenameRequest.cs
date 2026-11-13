using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What RENAME gives its dialog: the kinds of named objects of AutoCAD 2021's Rename dialog, the names of each that can be
    /// renamed (layer 0, the Standard styles, ByLayer, ByBlock and Continuous left out, as AutoCAD lists them), and the
    /// renames the dialog asks for, made in order once it closes with OK, as one step of undo.
    /// </summary>
    public sealed class CadRenameRequest
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _names;

        internal CadRenameRequest(CadDocument document, IReadOnlyDictionary<string, IReadOnlyList<string>> names)
        {
            Document = document;
            _names = names;
            Kinds = names.Keys.ToList();
        }

        /// <summary>Gets the document whose objects are renamed.</summary>
        public CadDocument Document { get; }

        /// <summary>
        /// Gets the kinds of named objects, as the Named Objects list writes them: Blocks, Dimension styles, Layers, Linetypes,
        /// Multileader styles, Table styles, Text styles, UCSs, Viewports and Views.
        /// </summary>
        public IReadOnlyList<string> Kinds { get; }

        /// <summary>Gets the renames asked for, made in this order when the dialog closes with OK.</summary>
        public IList<CadRename> Renames { get; } = new List<CadRename>();

        /// <summary>Gets or sets whether the dialog closed with OK; the renames are made only then.</summary>
        public bool IsAccepted { get; set; }

        /// <summary>Gets the names of a kind that can be renamed, sorted by their letters, either case the same.</summary>
        /// <param name="kind">One of <see cref="Kinds"/>.</param>
        /// <returns>The names; none for a kind not listed.</returns>
        public IReadOnlyList<string> NamesOf(string kind) =>
            kind != null && _names.TryGetValue(kind, out IReadOnlyList<string> names) ? names : Array.Empty<string>();
    }

    /// <summary>A rename the Rename dialog asks for: the kind of object, its name, and the name it takes.</summary>
    public sealed class CadRename
    {
        /// <summary>Makes a rename.</summary>
        /// <param name="kind">The kind, one of <see cref="CadRenameRequest.Kinds"/>.</param>
        /// <param name="oldName">The name it has.</param>
        /// <param name="newName">The name it takes.</param>
        public CadRename(string kind, string oldName, string newName)
        {
            Kind = kind ?? string.Empty;
            OldName = oldName ?? string.Empty;
            NewName = newName ?? string.Empty;
        }

        /// <summary>Gets the kind of object.</summary>
        public string Kind { get; }

        /// <summary>Gets the name it has.</summary>
        public string OldName { get; }

        /// <summary>Gets the name it takes.</summary>
        public string NewName { get; }
    }
}
