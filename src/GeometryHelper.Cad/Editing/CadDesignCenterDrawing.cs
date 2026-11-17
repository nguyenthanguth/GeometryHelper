using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>The kinds of content DesignCenter lists under a drawing, in the order of its tree.</summary>
    public enum CadDesignCenterKind
    {
        /// <summary>Blocks: the definitions a user names; inserted, or copied in without an INSERT.</summary>
        Blocks,

        /// <summary>Dimstyles: the dimension styles.</summary>
        Dimstyles,

        /// <summary>Layers.</summary>
        Layers,

        /// <summary>Layouts: the paper space tabs, Model left out.</summary>
        Layouts,

        /// <summary>Linetypes: all but ByLayer and ByBlock.</summary>
        Linetypes,

        /// <summary>Multileaderstyles.</summary>
        Multileaderstyles,

        /// <summary>Tablestyles.</summary>
        Tablestyles,

        /// <summary>Textstyles: the styles of letters, those of shapes left out.</summary>
        Textstyles,

        /// <summary>Xrefs: the external references the drawing attaches.</summary>
        Xrefs,
    }

    /// <summary>An item of a drawing's content as DesignCenter lists it: its kind, name and the description it shows.</summary>
    public sealed class CadDesignCenterItem
    {
        internal CadDesignCenterItem(
            CadDesignCenterDrawing drawing,
            CadDesignCenterKind kind,
            string name,
            string description
        )
        {
            Drawing = drawing;
            Kind = kind;
            Name = name;
            Description = description ?? string.Empty;
        }

        /// <summary>Gets the drawing the item is in.</summary>
        public CadDesignCenterDrawing Drawing { get; }

        /// <summary>Gets the kind of content.</summary>
        public CadDesignCenterKind Kind { get; }

        /// <summary>Gets the name, as the drawing spells it.</summary>
        public string Name { get; }

        /// <summary>Gets the description DesignCenter shows under the item: a block's comments, a layer's or linetype's description, the path of an external reference; empty where there is none.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// A drawing DesignCenter browses: a file read for it alone and not changed, or a drawing open in the presenter. It lists
    /// what the drawing holds by kind (<see cref="Items"/>), as the tree of AutoCAD's DesignCenter does under a drawing, and
    /// is what <see cref="CadDesignCenter"/> copies from.
    /// </summary>
    public sealed class CadDesignCenterDrawing
    {
        private readonly bool _owned;
        private DxfDatabase _copy;

        private CadDesignCenterDrawing(string path, string name, DxfDatabase database, bool owned)
        {
            Path = path;
            Name = name;
            Database = database;
            _owned = owned;
        }

        /// <summary>Gets the full path of the file; null for a drawing not saved yet.</summary>
        public string Path { get; }

        /// <summary>Gets the drawing's name as the tree shows it: the file's name with its extension, or the open drawing's title.</summary>
        public string Name { get; }

        /// <summary>The drawing listed.</summary>
        internal DxfDatabase Database { get; }

        /// <summary>
        /// Reads a drawing file, a DWG, DWS, DWT or DXF, to browse it; the file is not changed and not kept open.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="System.IO.IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown when the file is a DWG that cannot be read.</exception>
        /// <exception cref="NotSupportedException">Thrown when the file is a DWG of a version that is not read.</exception>
        public static CadDesignCenterDrawing Open(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return new CadDesignCenterDrawing(
                System.IO.Path.GetFullPath(path),
                System.IO.Path.GetFileName(path),
                CadDrawingFile.Open(path),
                true
            );
        }

        /// <summary>Lists a drawing open in the presenter, as the Open Drawings tab does; it is read and never changed by listing.</summary>
        /// <param name="document">The document.</param>
        /// <param name="title">The name the tab shows; the file's name where null.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadDesignCenterDrawing Of(CadDocument document, string title)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return new CadDesignCenterDrawing(
                document.FileName,
                title ?? (document.FileName == null ? "Drawing" : System.IO.Path.GetFileName(document.FileName)),
                document.Database,
                false
            );
        }

        /// <summary>
        /// Gets what the drawing holds of a kind, in ordinal order of the names, letters of either case alike first. Blocks are
        /// those a user names, with their comments; layers leave out those of external references; linetypes leave out ByLayer
        /// and ByBlock; layouts leave out Model.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<CadDesignCenterItem> Items(CadDesignCenterKind kind)
        {
            IEnumerable<KeyValuePair<string, string>> named;

            switch (kind)
            {
                case CadDesignCenterKind.Blocks:
                    named = CadBlocks.Named(Database).Select(b => Pair(b.Name, b.Comments));
                    break;
                case CadDesignCenterKind.Dimstyles:
                    named = CadDimStyleEditor.Styles(Database).Select(s => Pair(s.Name, null));
                    break;
                case CadDesignCenterKind.Layers:
                    named = Database.LayerTable.Where(l => !l.IsDependent).Select(l => Pair(l.Name, l.Description));
                    break;
                case CadDesignCenterKind.Layouts:
                    named = Database
                        .Layouts.Where(l =>
                            l.BlockTableRecord != null
                            && !string.Equals(l.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)
                        )
                        .Select(l => Pair(l.LayoutName, null));
                    break;
                case CadDesignCenterKind.Linetypes:
                    named = Database
                        .LinetypeTable.Where(l =>
                            !l.IsDependent
                            && !string.Equals(l.Name, "ByLayer", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(l.Name, "ByBlock", StringComparison.OrdinalIgnoreCase)
                        )
                        .Select(l => Pair(l.Name, l.AsciiDescription));
                    break;
                case CadDesignCenterKind.Multileaderstyles:
                    named = CadMLeaderStyleEditor.Styles(Database).Select(n => Pair(n, null));
                    break;
                case CadDesignCenterKind.Tablestyles:
                    named = CadTableStyleEditor.Styles(Database).Select(n => Pair(n, null));
                    break;
                case CadDesignCenterKind.Textstyles:
                    named = CadTextStyleEditor
                        .Styles(Database)
                        .Where(s => !s.IsShapeFile)
                        .Select(s => Pair(s.Name, s.FileName));
                    break;
                default:
                    named = Database
                        .BlockTable.Where(b => b.IsFromExternalReference && !b.IsLayout)
                        .Select(b => Pair(b.Name, b.PathName));
                    break;
            }

            var items = named.ToList();

            // Layouts keep the order of their tabs; the rest are in ordinal order.
            if (kind != CadDesignCenterKind.Layouts)
            {
                items.Sort(
                    (a, b) =>
                    {
                        int order = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                        return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
                    }
                );
            }

            return items.Select(p => new CadDesignCenterItem(this, kind, p.Key, p.Value)).ToList();
        }

        /// <summary>
        /// The drawing to copy blocks out of: the drawing itself where it was read for DesignCenter alone, a copy of an open one
        /// (read once, kept), since carrying a block over puts a temporary INSERT in the drawing it comes from.
        /// </summary>
        internal DxfDatabase WorkingCopy()
        {
            if (_owned)
            {
                return Database;
            }

            return _copy ?? (_copy = DxfDatabase.Parse(Database.ToText()));
        }

        private static KeyValuePair<string, string> Pair(string name, string description) =>
            new KeyValuePair<string, string>(name, description);
    }
}
