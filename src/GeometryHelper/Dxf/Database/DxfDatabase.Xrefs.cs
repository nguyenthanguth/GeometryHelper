using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>The external references of the drawing, as AutoCAD's <c>Database</c> attaches, overlays and detaches them.</summary>
    public sealed partial class DxfDatabase
    {
        // The tables whose records an external reference brings, named after it with a | between.
        private static readonly string[] DependentTables = { "LAYER", "LTYPE", "STYLE", "DIMSTYLE" };

        /// <summary>
        /// Attaches another drawing as an external reference, as AutoCAD's <c>AttachXref</c>: a block of the name given whose
        /// BLOCK is flagged an external reference (70 of 4) and names the drawing's path (its 1), with its BLOCK_RECORD from R13
        /// on, and no entities: AutoCAD reads the drawing into it when it opens this one. An INSERT of the block places it, as
        /// <c>new DxfBlockReference(position, xref)</c> appended to model space; until then the block is
        /// <see cref="DxfXrefStatus.Unreferenced"/>.
        /// </summary>
        /// <remarks>
        /// The drawing is not read: the block's origin is the origin, and its <see cref="DxfBlockTableRecord.XrefStatus"/> says
        /// whether the path names a file. <c>db.AttachXref("column.dwg", "COLUMN")</c> writes the BLOCK <c>2 COLUMN, 70 4,
        /// 3 COLUMN, 1 column.dwg</c>.
        /// </remarks>
        /// <param name="fileName">The path of the drawing, as it is to be written: absolute, or from the folder of this one.</param>
        /// <param name="blockName">The name of the block.</param>
        /// <returns>The block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path is empty or has a line break in it, or the name is one AutoCAD
        /// does not take or that of a block of the drawing already, letters of either case the same.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no BLOCKS section.</exception>
        public DxfBlockTableRecord AttachXref(string fileName, string blockName) => Xref(fileName, blockName, 4);

        /// <summary>
        /// Overlays another drawing as an external reference, as AutoCAD's <c>OverlayXref</c>: as <see cref="AttachXref"/>
        /// attaches one, its BLOCK flagged an overlay as well (70 of 12), so that a drawing that references this one does not
        /// see it.
        /// </summary>
        /// <param name="fileName">The path of the drawing, as it is to be written.</param>
        /// <param name="blockName">The name of the block.</param>
        /// <returns>The block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path is empty or has a line break in it, or the name is one AutoCAD
        /// does not take or that of a block of the drawing already.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no BLOCKS section.</exception>
        public DxfBlockTableRecord OverlayXref(string fileName, string blockName) => Xref(fileName, blockName, 12);

        /// <summary>
        /// Detaches an external reference, as AutoCAD's <c>DetachXref</c>: every INSERT of it erased, in model space, paper
        /// space and the blocks, then the records it brought, those named after it with a | (the layer <c>COLUMN|steel</c> and
        /// the like, of the layer, linetype, text style, dimension style and block tables), and last its block.
        /// </summary>
        /// <param name="xrefBlock">The block of the external reference, of this drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the block is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block is of another drawing, or is no external reference.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is erased.</exception>
        public void DetachXref(DxfBlockTableRecord xrefBlock)
        {
            if (xrefBlock == null)
            {
                throw new ArgumentNullException(nameof(xrefBlock));
            }

            if (xrefBlock.File != File)
            {
                throw new ArgumentException(
                    "The block " + xrefBlock.Name + " is not of this drawing.",
                    nameof(xrefBlock)
                );
            }

            if (xrefBlock.Gone)
            {
                throw new InvalidOperationException("The block " + xrefBlock.Name + " is erased already.");
            }

            if (!xrefBlock.IsFromExternalReference)
            {
                throw new ArgumentException(
                    "The block " + xrefBlock.Name + " is no external reference to detach.",
                    nameof(xrefBlock)
                );
            }

            string name = xrefBlock.Name;
            string prefix = name + "|";
            IReadOnlyList<DxfBlockReference> inserts = xrefBlock.GetBlockReferences(true);

            foreach (DxfBlockReference insert in inserts)
            {
                insert.Erase();
            }

            int dependents = 0;

            foreach (string table in DependentTables)
            {
                foreach (DxfItem item in File.TableItems(table).ToArray())
                {
                    if (
                        DxfReader
                            .NameOf(File.ValueOf(item, 2) ?? string.Empty)
                            .StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        string handle = item.Handle;
                        File.RemoveTableRecord(item, table);
                        dependents++;

                        // The head of the DIMSTYLE table lists its records' handles from R2000 on.
                        if (table == "DIMSTYLE")
                        {
                            DxfDimStyleTable.ListHandle(File, handle, false);
                        }
                    }
                }
            }

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                if (block.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !block.IsLayout)
                {
                    block.Remove();
                    dependents++;
                }
            }

            xrefBlock.Remove();
            GeometryHelperLog.Info(
                "DxfDatabase: the external reference "
                    + name
                    + " was detached, with "
                    + inserts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " INSERTs and "
                    + dependents.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " records it brought."
            );
        }

        /// <summary>
        /// Binds an external reference, as AutoCAD's <c>BindXrefs</c> binds one: its block made an ordinary block, its origin
        /// the base point of its drawing (<c>$INSBASE</c>) and its path kept, holding a copy of every entity of that drawing's
        /// model space, its INSERTs placing it as they placed the reference; and the records that drawing's entities draw by
        /// brought with them. Bound (<paramref name="insertBind"/> false), each layer, linetype, text style, dimension style and
        /// named block of the drawing comes under the reference's name, <c>$0$</c> and its own, the 0 raised past a name the
        /// drawing has (<c>GRID$1$WALL</c> where <c>GRID$0$WALL</c> is taken), as AutoCAD 2021's -XREF Bind names them; layer 0,
        /// Defpoints, ByBlock, ByLayer and Continuous are this drawing's. Inserted (true), each keeps its own name and is merged
        /// by name, this drawing's own kept, as <see cref="Insert(string, DxfDatabase, bool)"/> merges them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The records the reference brought into this drawing (the layer <c>GRID|WALL</c> and the like, of the layer, linetype,
        /// text style and dimension style tables) take the new names in their places, with what this drawing set on them, as
        /// AutoCAD keeps a layer's colour changed in the drawing: <c>GRID|WALL</c> made red here becomes the red layer
        /// <c>GRID$0$WALL</c>, no longer of a reference, its <c>AcXRefLayerPropertyOverride</c> data taken out. Inserted,
        /// <c>GRID|WALL</c> becomes <c>WALL</c>, or goes where the drawing has a WALL of its own, what drew on it drawing on
        /// that. Blocks named after the reference are taken out, and come again from its drawing.
        /// </para>
        /// <para>
        /// A drawing of a line on WALL, its base point (100, 50, 0), bound as GRID gives the block GRID drawn about (100, 50, 0)
        /// with the line on <c>GRID$0$WALL</c>. An external reference of the drawing an INSERT of its model space places comes
        /// with the INSERT, an external reference still, as <see cref="Insert(string, DxfDatabase, bool)"/> brings one, to be
        /// bound in its turn; one overlaid is left out, as a drawing that references this one does not see it. The drawing
        /// given is left as it is: it is copied, and the copy renamed. Every entity is checked before anything is written, as
        /// <c>Insert</c> checks it.
        /// </para>
        /// </remarks>
        /// <param name="xrefBlock">The block of the external reference, of this drawing.</param>
        /// <param name="xrefDatabase">The drawing it references, as read from its file; another than this one.</param>
        /// <param name="insertBind">Whether the records keep their own names (Insert) rather than coming under the reference's
        /// (Bind).</param>
        /// <exception cref="ArgumentNullException">Thrown when the block or the drawing is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block is of another drawing or is no external reference; when the
        /// drawing is this one, or, inserted, has a block of the reference's name; or when this drawing cannot hold an entity of
        /// it, or a block of it inserts itself, as <c>Insert</c> refuses them. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is erased.</exception>
        public void BindXref(DxfBlockTableRecord xrefBlock, DxfDatabase xrefDatabase, bool insertBind)
        {
            if (xrefBlock == null)
            {
                throw new ArgumentNullException(nameof(xrefBlock));
            }

            if (xrefBlock.File != File)
            {
                throw new ArgumentException(
                    "The block " + xrefBlock.Name + " is not of this drawing.",
                    nameof(xrefBlock)
                );
            }

            if (xrefBlock.Gone)
            {
                throw new InvalidOperationException("The block " + xrefBlock.Name + " is erased already.");
            }

            if (!xrefBlock.IsFromExternalReference)
            {
                throw new ArgumentException(
                    "The block " + xrefBlock.Name + " is no external reference to bind.",
                    nameof(xrefBlock)
                );
            }

            CheckSource(xrefDatabase);
            string name = xrefBlock.Name;
            DxfDatabase source = Parse(xrefDatabase.ToText());

            if (insertBind && source.BlockTable.Has(name))
            {
                throw new ArgumentException(
                    "The drawing referenced has a block named "
                        + name
                        + " of its own, which would come under the name of the block bound.",
                    nameof(xrefDatabase)
                );
            }

            // The new names of the records of each table, by their own: the reference's name, $0$ and their own, the number
            // raised past the names this drawing and the copy have; their own where inserted.
            var renamed = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            foreach (string table in DependentTables)
            {
                renamed[table] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!insertBind)
            {
                RenameBound(source, name, renamed);
            }

            var entities = new List<DxfEntity>();

            foreach (DxfEntity entity in source.ModelSpace)
            {
                // An overlay of the drawing is not seen through it, and does not come.
                if (
                    entity is DxfBlockReference reference
                    && source.BlockTable.Has(reference.Name)
                    && source.BlockTable[reference.Name].IsFromOverlayReference
                )
                {
                    continue;
                }

                entities.Add(entity);
            }

            CopyIntoBlock(
                source,
                entities,
                name,
                () => TakeDependents(name, insertBind, renamed),
                () =>
                {
                    xrefBlock.MakeBound();
                    xrefBlock.Origin = source.Insbase;
                    return xrefBlock;
                }
            );
            GeometryHelperLog.Info(
                "DxfDatabase: the external reference "
                    + name
                    + " was "
                    + (insertBind ? "inserted" : "bound")
                    + ", with "
                    + entities.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " entities."
            );
        }

        /// <summary>
        /// Renames the records of a copy of a drawing referenced as binding names them: <c>GRID$0$WALL</c> for its layer WALL,
        /// the number raised past a name this drawing or the copy has; layer 0, Defpoints, ByBlock, ByLayer, Continuous, the
        /// records of its own references and its blocks AutoCAD names itself keep theirs. Each new name is kept by table.
        /// </summary>
        private void RenameBound(
            DxfDatabase source,
            string name,
            Dictionary<string, Dictionary<string, string>> renamed
        )
        {
            var layers = new List<DxfLayerTableRecord>();

            foreach (DxfLayerTableRecord layer in source.LayerTable)
            {
                if (
                    !layer.IsDependent
                    && layer.Name.Length > 0
                    && layer.Name != "0"
                    && !string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase)
                )
                {
                    layers.Add(layer);
                }
            }

            var linetypes = new List<DxfLinetypeTableRecord>();

            foreach (DxfLinetypeTableRecord linetype in source.LinetypeTable)
            {
                string own = linetype.Name;

                if (
                    !linetype.IsDependent
                    && own.Length > 0
                    && !string.Equals(own, "ByBlock", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(own, "ByLayer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(own, "Continuous", StringComparison.OrdinalIgnoreCase)
                )
                {
                    linetypes.Add(linetype);
                }
            }

            var styles = new List<DxfTextStyleTableRecord>();

            foreach (DxfTextStyleTableRecord style in source.TextStyleTable)
            {
                if (!style.IsDependent && !style.IsShapeFile && style.Name.Length > 0)
                {
                    styles.Add(style);
                }
            }

            var dimensionStyles = new List<DxfDimStyleTableRecord>();

            foreach (DxfDimStyleTableRecord style in source.DimStyleTable)
            {
                if (!style.IsDependent && style.Name.Length > 0)
                {
                    dimensionStyles.Add(style);
                }
            }

            var blocks = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in source.BlockTable)
            {
                if (
                    !block.IsLayout
                    && !block.IsAnonymous
                    && !block.Name.StartsWith("*", StringComparison.Ordinal)
                    && !block.IsFromExternalReference
                    && !block.IsDependent
                )
                {
                    blocks.Add(block);
                }
            }

            GiveBoundNames(renamed["LAYER"], layers.ConvertAll(r => r.Name), LayerTable, source.LayerTable, name);
            GiveBoundNames(
                renamed["LTYPE"],
                linetypes.ConvertAll(r => r.Name),
                LinetypeTable,
                source.LinetypeTable,
                name
            );
            GiveBoundNames(
                renamed["STYLE"],
                styles.ConvertAll(r => r.Name),
                TextStyleTable,
                source.TextStyleTable,
                name
            );
            GiveBoundNames(
                renamed["DIMSTYLE"],
                dimensionStyles.ConvertAll(r => r.Name),
                DimStyleTable,
                source.DimStyleTable,
                name
            );
            var blockNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            GiveBoundNames(blockNames, blocks.ConvertAll(r => r.Name), BlockTable, source.BlockTable, name);

            // Layers, linetypes and text styles renamed each in one walk of the copy, their records first; a dimension style
            // with the styles AutoCAD keeps for its kinds of dimension (D1$0 with D1), and a block with every INSERT of it.
            DxfFile file = source.File;
            RenameRecords(file, "LAYER", renamed["LAYER"]);
            file.Rename(
                new[] { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" },
                (type, code) => code == 8 && type != "XRECORD",
                1003,
                renamed["LAYER"]
            );
            RenameRecords(file, "LTYPE", renamed["LTYPE"]);
            file.Rename(new[] { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" }, NamesLinetype, -1, renamed["LTYPE"]);
            RenameRecords(file, "STYLE", renamed["STYLE"]);
            file.Rename(
                new[] { "ENTITIES", "BLOCKS", "OBJECTS" },
                DxfTextStyleTableRecord.NamesStyle,
                -1,
                renamed["STYLE"]
            );

            foreach (DxfDimStyleTableRecord style in dimensionStyles)
            {
                // A style AutoCAD keeps for a kind of dimension of another was renamed with it.
                if (renamed["DIMSTYLE"].TryGetValue(style.Name, out string to))
                {
                    style.Name = to;
                }
            }

            foreach (DxfBlockTableRecord block in blocks)
            {
                block.Name = blockNames[block.Name];
            }
        }

        /// <summary>
        /// Gives each name of a table of the copy its bound name, <c>GRID$0$WALL</c>, the number raised past a name either
        /// drawing's table has or one given already.
        /// </summary>
        private static void GiveBoundNames(
            Dictionary<string, string> renamed,
            List<string> names,
            DxfSymbolTable ours,
            DxfSymbolTable theirs,
            string prefix
        )
        {
            var given = new HashSet<string>(renamed.Values, StringComparer.OrdinalIgnoreCase);

            foreach (string own in names)
            {
                renamed[own] = BoundName(prefix, own, ours, theirs, given);
            }
        }

        /// <summary>The first of <c>GRID$0$WALL</c>, <c>GRID$1$WALL</c> and on that neither table has nor was given already.</summary>
        private static string BoundName(
            string prefix,
            string own,
            DxfSymbolTable ours,
            DxfSymbolTable theirs,
            HashSet<string> given
        )
        {
            for (int i = 0; ; i++)
            {
                string name = prefix + "$" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "$" + own;

                if (!ours.Has(name) && (theirs == null || !theirs.Has(name)) && given.Add(name))
                {
                    return name;
                }
            }
        }

        /// <summary>Whether a group names a linetype by its name: the 6 of an entity, a layer or a style of multilines, not a dimension style's first arrow.</summary>
        private static bool NamesLinetype(string type, int code) =>
            code == 6 && type != "XRECORD" && type != "LTYPE" && type != "DIMSTYLE";

        /// <summary>Writes the new name of each record of a table the map renames, its flags of an external reference cleared.</summary>
        private static void RenameRecords(DxfFile file, string table, Dictionary<string, string> renamed)
        {
            if (renamed.Count == 0)
            {
                return;
            }

            foreach (DxfItem item in file.TableItems(table).ToArray())
            {
                if (renamed.TryGetValue(DxfReader.NameOf(file.ValueOf(item, 2) ?? string.Empty), out string to))
                {
                    int flags = int.TryParse(
                        file.ValueOf(item, 70)?.Trim(),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out int read
                    )
                        ? read
                        : 0;
                    file.SetValues(
                        item,
                        new[]
                        {
                            new KeyValuePair<int, string>(2, to),
                            new KeyValuePair<int, string>(
                                70,
                                (flags & ~48).ToString(System.Globalization.CultureInfo.InvariantCulture)
                            ),
                        },
                        RecordGroups,
                        false
                    );
                }
            }
        }

        // The groups a record of a table starts with, in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> RecordGroups = Array.AsReadOnly(new[] { 100, 100, 2, 70 });

        /// <summary>
        /// The records an external reference brought into this drawing, named after it with a |, taken as binding takes them:
        /// renamed in their places to the names the map gives their own, or, inserted, to their own, those this drawing has a
        /// record of already taken out and what drew by them drawing by that record; a layer's
        /// <c>AcXRefLayerPropertyOverride</c> data taken out; and the blocks named after it taken out, to come again.
        /// </summary>
        private void TakeDependents(
            string name,
            bool insertBind,
            Dictionary<string, Dictionary<string, string>> renamed
        )
        {
            string prefix = name + "|";

            foreach (string table in DependentTables)
            {
                Dictionary<string, string> theirs = renamed[table];
                var given = new HashSet<string>(theirs.Values, StringComparer.OrdinalIgnoreCase);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var dropped = new List<DxfItem>();
                var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                DxfSymbolTable ours = Table(table);

                foreach (DxfItem item in File.TableItems(table))
                {
                    string old = DxfReader.NameOf(File.ValueOf(item, 2) ?? string.Empty);

                    if (!old.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || map.ContainsKey(old))
                    {
                        continue;
                    }

                    string own = old.Substring(prefix.Length);
                    string to =
                        insertBind ? own
                        : theirs.TryGetValue(own, out string bound) ? bound
                        : BoundName(name, own, ours, null, given);
                    map[old] = to;

                    if (insertBind && ours.Has(to))
                    {
                        dropped.Add(item);
                    }
                    else
                    {
                        kept[old] = to;
                    }
                }

                if (map.Count == 0)
                {
                    continue;
                }

                foreach (DxfItem item in dropped)
                {
                    string handle = item.Handle;
                    File.RemoveTableRecord(item, table);

                    if (table == "DIMSTYLE")
                    {
                        DxfDimStyleTable.ListHandle(File, handle, false);
                    }
                }

                RenameRecords(File, table, kept);

                if (table == "LAYER")
                {
                    File.Rename(
                        new[] { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" },
                        (type, code) => code == 8 && type != "XRECORD",
                        1003,
                        map
                    );

                    foreach (string layer in kept.Values)
                    {
                        if (LayerTable.Has(layer) && LayerTable[layer].GetXDataForApplication(XrefOverrides) != null)
                        {
                            LayerTable[layer].XData = new[] { new DxfTypedValue(1001, XrefOverrides) };
                        }
                    }
                }
                else if (table == "LTYPE")
                {
                    File.Rename(new[] { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" }, NamesLinetype, -1, map);
                }
            }

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                if (block.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !block.IsLayout)
                {
                    block.Remove();
                }
            }
        }

        /// <summary>
        /// Binds named symbols an external reference brought, as AutoCAD's XBIND binds them: each record of the table renamed in its
        /// place with every | of its name made <c>$0$</c>, the number raised past a name the table has (the layer
        /// <c>GRID|WALL</c> becomes <c>GRID$0$WALL</c>, or <c>GRID$1$WALL</c> where that is taken), no longer of a reference, a
        /// layer's <c>AcXRefLayerPropertyOverride</c> data taken out, and what drew by a layer or a linetype drawing by its new
        /// name. A name the table has no record of, or whose record is no reference's, is left out.
        /// </summary>
        /// <param name="table">The table: LAYER, LTYPE, STYLE or DIMSTYLE.</param>
        /// <param name="names">The names of the records, with their |.</param>
        /// <returns>How many records were bound.</returns>
        internal int BindDependentSymbols(string table, IReadOnlyList<string> names)
        {
            DxfSymbolTable ours = Table(table);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var given = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string old in names)
            {
                if (!ours.Has(old) || old.IndexOf('|') < 0 || map.ContainsKey(old))
                {
                    continue;
                }

                for (int i = 0; ; i++)
                {
                    string name = old.Replace(
                        "|",
                        "$" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "$"
                    );

                    if (!ours.Has(name) && given.Add(name))
                    {
                        map[old] = name;
                        break;
                    }
                }
            }

            if (map.Count == 0)
            {
                return 0;
            }

            RenameRecords(File, table, map);

            if (table == "LAYER")
            {
                File.Rename(
                    new[] { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" },
                    (type, code) => code == 8 && type != "XRECORD",
                    1003,
                    map
                );

                foreach (string layer in map.Values)
                {
                    if (LayerTable.Has(layer) && LayerTable[layer].GetXDataForApplication(XrefOverrides) != null)
                    {
                        LayerTable[layer].XData = new[] { new DxfTypedValue(1001, XrefOverrides) };
                    }
                }
            }
            else if (table == "LTYPE")
            {
                File.Rename(new[] { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" }, NamesLinetype, -1, map);
            }

            return map.Count;
        }

        // The application whose extended data says which properties of a layer of an external reference the drawing changed.
        private const string XrefOverrides = "AcXRefLayerPropertyOverride";

        /// <summary>The table of this drawing a table of names is.</summary>
        private DxfSymbolTable Table(string table) =>
            table == "LAYER" ? LayerTable
            : table == "LTYPE" ? LinetypeTable
            : table == "STYLE" ? (DxfSymbolTable)TextStyleTable
            : DimStyleTable;

        /// <summary>Adds the block of an external reference, its BLOCK's flags those given, as <see cref="AttachXref"/> says.</summary>
        private DxfBlockTableRecord Xref(string fileName, string blockName, int flags)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (blockName == null)
            {
                throw new ArgumentNullException(nameof(blockName));
            }

            if (fileName.Trim().Length == 0 || fileName.IndexOf('\n') >= 0 || fileName.IndexOf('\r') >= 0)
            {
                throw new ArgumentException(
                    "The path of an external reference is given, on one line: '" + fileName + "' is not one.",
                    nameof(fileName)
                );
            }

            DxfSymbolTable.CheckName(blockName, nameof(blockName));

            if (BlockTable.Has(blockName))
            {
                throw new ArgumentException(
                    "The drawing has a block named " + blockName + " already, letters of either case the same.",
                    nameof(blockName)
                );
            }

            var block = new DxfBlockTableRecord
            {
                Name = blockName,
                MadeFlags = flags,
                MadePath = fileName,
            };
            BlockTable.Add(block);
            GeometryHelperLog.Info(
                "DxfDatabase: the drawing "
                    + fileName
                    + " was "
                    + (flags == 4 ? "attached" : "overlaid")
                    + " as the external reference "
                    + blockName
                    + "."
            );
            return block;
        }
    }
}
