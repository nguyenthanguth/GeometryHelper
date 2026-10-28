using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>Another drawing inserted into this one, as AutoCAD's <c>Database.Insert</c> inserts a database.</summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Inserts another drawing into this one's model space, as AutoCAD's <c>Insert(transform, dataBase,
        /// preserveSourceDatabase)</c>: every entity of its model space copied into this model space and moved by the
        /// transformation, as <see cref="WblockCloneObjects"/> copies entities together, its groups among them; and the records
        /// of its tables and its named blocks that this drawing has none of the name of, merged by name, this drawing's own kept
        /// where it has one: its text styles, linetypes, layers, blocks and dimension styles. Its paper space is not inserted.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A drawing of a line from (0, 0, 0) to (10, 0, 0) on the layer steel, inserted by
        /// <c>GeoTransform3.Translation(new GeoVector3(100, 0, 0))</c> into one that has no layer steel, gives this drawing the
        /// line from (100, 0, 0) to (110, 0, 0) and the layer steel as the other drawing has it. A block AutoCAD names itself, a
        /// dimension's <c>*D</c>, comes under a name of its own, as <see cref="DxfBlockTableRecord.AppendEntity"/> says; the
        /// external references of the other drawing and what they brought (a name with a | in it) are not merged, but one an
        /// INSERT of its model space places comes with the INSERT, an external reference still: its BLOCK flagged 4, or 12
        /// overlaid, naming its drawing in its 1, with no entities. A dynamic block comes as a static one, its copy holding
        /// what it draws without what makes it dynamic (the <c>AcDbDynamicBlockGUID</c> of its record, the
        /// <c>AcDbBlockRepBTag</c> of an anonymous block standing for it, their extension dictionaries), which AutoCAD's own
        /// insert keeps.
        /// </para>
        /// <para>
        /// Every entity is checked before anything is written, its copy and its copy moved, so that a drawing this one cannot
        /// hold, a SPLINE into one of R12 or an INSERT the transformation would shear, changes nothing; nor does a drawing whose
        /// model space holds an entity of a type the library has no class for (<see cref="DxfUnknownEntity"/>, as a SHAPE or
        /// an IMAGE), whose geometry is not moved, by any transformation but the identity. The extents of the header grow by
        /// the entities where they are moved to. The other drawing is left as it is.
        /// </para>
        /// </remarks>
        /// <param name="transform">The transformation the entities are moved by.</param>
        /// <param name="dataBase">The drawing to insert, another than this one.</param>
        /// <param name="preserveSourceDatabase">Whether the other drawing is to be kept as it was, as AutoCAD may take objects
        /// out of it otherwise: here it is always kept, so either gives the same.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation or the drawing is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing is this one, or this drawing cannot hold an entity of it, or
        /// an entity of it refuses the transformation, or a block of it inserts itself, at any depth (X holding an INSERT of X,
        /// or Y one of Z and Z one of Y), which AutoCAD's AUDIT takes out; nothing is written then.</exception>
        /// <exception cref="NotSupportedException">Thrown when the transformation is not the identity and model space holds an
        /// entity of a type the library has no class for; nothing is written then.</exception>
        public void Insert(GeoTransform3 transform, DxfDatabase dataBase, bool preserveSourceDatabase)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            CheckSource(dataBase);
            bool moved = !DxfOcsTransform.IsIdentity(transform);
            var entities = new List<DxfEntity>(dataBase.ModelSpace);

            foreach (DxfEntity entity in entities)
            {
                if (moved)
                {
                    entity.GetTransformedCopy(transform);
                }
            }

            IReadOnlyList<DxfTypedValue> least;
            IReadOnlyList<DxfTypedValue> most;
            HashSet<DxfEntity> before;
            File.HoldTableNames();

            try
            {
                CheckCopies(dataBase, entities);
                MergeTables(dataBase);
                least = File.HeaderOf("$EXTMIN");
                most = File.HeaderOf("$EXTMAX");
                before = new HashSet<DxfEntity>(ModelSpace);
                dataBase.WblockCloneObjects(entities, ModelSpace);
            }
            finally
            {
                File.ReleaseTableNames();
            }

            if (moved)
            {
                // The copies as model space holds them: an MTEXT copied into a drawing of R12 is the TEXTs it was written as.
                var copies = new List<DxfEntity>();

                foreach (DxfEntity entity in ModelSpace)
                {
                    if (!before.Contains(entity))
                    {
                        copies.Add(entity);
                    }
                }

                foreach (DxfEntity copy in copies)
                {
                    copy.TransformBy(transform);
                }

                // The extents hold the copies where they were moved to, not where they were first written.
                if (least != null && most != null)
                {
                    File.SetHeader("$EXTMIN", least);
                    File.SetHeader("$EXTMAX", most);
                }

                foreach (DxfEntity copy in copies)
                {
                    File.GrowExtentsBy(copy.Item);
                }
            }

            GeometryHelperLog.Info("DxfDatabase: a drawing of " + entities.Count.ToString(CultureInfo.InvariantCulture) + " entities was inserted into model space.");
        }

        /// <summary>
        /// Makes a block of another drawing's model space, as AutoCAD's <c>Insert(blockName, dataBase,
        /// preserveSourceDatabase)</c>: a new block of the name given, its origin the other drawing's base point
        /// (<c>$INSBASE</c>) and its units the other drawing's (<c>$INSUNITS</c>, from R2007), holding a copy of every entity of
        /// that model space, as <see cref="WblockCloneObjects"/> copies entities together; and the records of the tables and the
        /// named blocks merged by name as <see cref="Insert(GeoTransform3, DxfDatabase, bool)"/> merges them. An INSERT of the
        /// block then places the drawing, as AutoCAD's INSERT of a drawing file does.
        /// </summary>
        /// <remarks>
        /// A drawing of a circle of radius 5 about (20, 0), its base point (20, 0, 0), made the block PLATE, gives this drawing
        /// the block PLATE drawn about (20, 0, 0) with the circle in it; nothing is added to model space. Every entity is checked
        /// before anything is written, as the other <c>Insert</c> checks it, and external references and dynamic blocks come as
        /// that one says.
        /// </remarks>
        /// <param name="blockName">The name of the new block.</param>
        /// <param name="dataBase">The drawing to insert, another than this one.</param>
        /// <param name="preserveSourceDatabase">Whether the other drawing is to be kept as it was: here it always is.</param>
        /// <returns>The new block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the drawing is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is one AutoCAD does not take or that of a block of this
        /// drawing already, or of one of the other drawing (an OUTER of its own would come under the name of the new block), the
        /// drawing is this one, this drawing cannot hold an entity of it, or a block of it inserts itself, at any depth; nothing
        /// is written then.</exception>
        public DxfBlockTableRecord Insert(string blockName, DxfDatabase dataBase, bool preserveSourceDatabase)
        {
            if (blockName == null)
            {
                throw new ArgumentNullException(nameof(blockName));
            }

            DxfSymbolTable.CheckName(blockName, nameof(blockName));
            CheckSource(dataBase);

            if (BlockTable.Has(blockName))
            {
                throw new ArgumentException("The drawing has a block named " + blockName + " already, letters of either case the same.", nameof(blockName));
            }

            // A block of the other drawing of the name would be merged, or brought by an INSERT, under the name of the new one.
            if (dataBase.BlockTable.Has(blockName))
            {
                throw new ArgumentException("The drawing to insert has a block named " + blockName + " of its own, which would come under the same name, letters of either case the same.", nameof(blockName));
            }

            var entities = new List<DxfEntity>(dataBase.ModelSpace);
            DxfBlockTableRecord block;
            File.HoldTableNames();

            try
            {
                CheckCopies(dataBase, entities);
                MergeTables(dataBase);
                block = new DxfBlockTableRecord { Name = blockName, Origin = dataBase.Insbase };

                if (File.Supports(1021))
                {
                    block.Units = dataBase.Insunits;
                }

                BlockTable.Add(block);
                dataBase.WblockCloneObjects(entities, block);
            }
            finally
            {
                File.ReleaseTableNames();
            }

            GeometryHelperLog.Info("DxfDatabase: a drawing of " + entities.Count.ToString(CultureInfo.InvariantCulture) + " entities was made the block " + blockName + ".");
            return block;
        }

        /// <summary>Refuses a drawing to insert that is none, or this one.</summary>
        private void CheckSource(DxfDatabase dataBase)
        {
            if (dataBase == null)
            {
                throw new ArgumentNullException(nameof(dataBase));
            }

            if (ReferenceEquals(dataBase, this))
            {
                throw new ArgumentException("A drawing is inserted into another, not into itself.", nameof(dataBase));
            }
        }

        /// <summary>
        /// Checks that this drawing can hold a copy of each entity of the other's model space and of its named blocks to be
        /// merged, as <see cref="WblockCloneObjects"/> checks its copies, before anything is written.
        /// </summary>
        private void CheckCopies(DxfDatabase source, List<DxfEntity> entities)
        {
            string looped = SelfInserted(source);

            if (looped != null)
            {
                throw new ArgumentException("The block " + looped + " of the drawing to insert inserts itself, by its own INSERTs or theirs, and is not copied: AutoCAD's AUDIT takes such an INSERT out.", "dataBase");
            }

            var all = new List<DxfEntity>(entities);

            foreach (DxfBlockTableRecord block in BlocksToMerge(source))
            {
                all.AddRange(block);
            }

            // A block of the other drawing an INSERT brings is checked once, with the blocks it inserts, however many INSERTs
            // above it bring it again: nothing is written between the checks.
            var checkedBlocks = new HashSet<DxfFileBlock>();

            foreach (DxfEntity entity in all)
            {
                DxfEntity copy = entity.Clone();
                copy.Appending(ModelSpace);

                if (copy is DxfMText mtext && !File.HasMarkers)
                {
                    foreach (DxfEntity text in mtext.R12Texts(File))
                    {
                        File.CheckAdopt(text.Item, checkedBlocks);
                    }
                }
                else
                {
                    File.CheckAdopt(copy.Item, checkedBlocks);
                }
            }
        }

        /// <summary>
        /// Copies the records of the other drawing's tables and its named blocks that this drawing has none of the name of: its
        /// text styles, linetypes and layers, then its blocks, each with copies of its entities, then its dimension styles,
        /// which point at blocks; those of its external references left out.
        /// </summary>
        private void MergeTables(DxfDatabase source)
        {
            File.CopyTableRecords(source.File, "STYLE", Missing(source.TextStyleTable, TextStyleTable));
            File.CopyTableRecords(source.File, "LTYPE", Missing(source.LinetypeTable, LinetypeTable));
            File.CopyTableRecords(source.File, "LAYER", Missing(source.LayerTable, LayerTable));

            // The blocks found to reach none merged, kept for the rest of the merge, so that 3 000 nested blocks are each walked
            // once to find a round, not once for each block merged above them.
            File.Roundless = new HashSet<DxfBlockTableRecord>();

            try
            {
                foreach (DxfBlockTableRecord block in BlocksToMerge(source))
                {
                    // A block the copy of one before it brought, as a block its INSERT places, is here already.
                    if (BlockTable.Has(block.Name))
                    {
                        continue;
                    }

                    var made = new DxfBlockTableRecord { Name = block.Name, Origin = block.Origin, MadeFlags = block.HasAttributeDefinitions ? 2 : 0 };

                    if (File.Supports(1015))
                    {
                        made.Comments = block.Comments;
                    }

                    if (File.Supports(1021))
                    {
                        made.Units = block.Units;
                        made.Explodable = block.Explodable;
                        made.BlockScaling = block.BlockScaling;
                    }

                    BlockTable.Add(made);
                    source.WblockCloneObjects(new List<DxfEntity>(block), made);
                }
            }
            finally
            {
                File.Roundless = null;
            }

            File.CopyTableRecords(source.File, "DIMSTYLE", Missing(source.DimStyleTable, DimStyleTable));
        }

        /// <summary>The named blocks of the other drawing this one has none of the name of: not of a layout, not one AutoCAD names itself, no external reference and none it brought.</summary>
        private List<DxfBlockTableRecord> BlocksToMerge(DxfDatabase source)
        {
            var blocks = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in source.BlockTable)
            {
                string name = block.Name;

                if (!block.IsLayout && !block.IsAnonymous && !name.StartsWith("*", StringComparison.Ordinal) && !block.IsFromExternalReference && !block.IsDependent
                    && !BlockTable.Has(name))
                {
                    blocks.Add(block);
                }
            }

            return blocks;
        }

        /// <summary>
        /// The first block of a drawing, in the order of its table, found on a round of INSERTs that comes back to it, its own
        /// INSERT of itself or one through other blocks; null where none does. Each block is walked once, depth first.
        /// </summary>
        private static string SelfInserted(DxfDatabase source)
        {
            var inserts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (DxfBlockTableRecord block in source.BlockTable)
            {
                if (block.IsLayout || inserts.ContainsKey(block.Name))
                {
                    continue;
                }

                var named = new List<string>();

                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfBlockReference reference)
                    {
                        named.Add(reference.Name);
                    }
                }

                inserts.Add(block.Name, named);
                order.Add(block.Name);
            }

            // 1 for a block on the path walked, 2 for one walked whole.
            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var path = new Stack<KeyValuePair<string, int>>();

            foreach (string start in order)
            {
                if (state.ContainsKey(start))
                {
                    continue;
                }

                state[start] = 1;
                path.Push(new KeyValuePair<string, int>(start, 0));

                while (path.Count > 0)
                {
                    KeyValuePair<string, int> top = path.Pop();
                    List<string> next = inserts[top.Key];

                    if (top.Value == next.Count)
                    {
                        state[top.Key] = 2;
                        continue;
                    }

                    path.Push(new KeyValuePair<string, int>(top.Key, top.Value + 1));
                    string child = next[top.Value];

                    if (!inserts.ContainsKey(child))
                    {
                        continue;
                    }

                    if (state.TryGetValue(child, out int seen))
                    {
                        if (seen == 1)
                        {
                            return child;
                        }

                        continue;
                    }

                    state[child] = 1;
                    path.Push(new KeyValuePair<string, int>(child, 0));
                }
            }

            return null;
        }

        /// <summary>The names of the records of a table of the other drawing this one's has none of: named, and not brought by an external reference.</summary>
        private static List<string> Missing(DxfSymbolTable theirs, DxfSymbolTable ours)
        {
            var names = new List<string>();

            foreach (DxfSymbolTableRecord record in theirs)
            {
                string name = record.Name;

                if (name.Length > 0 && !record.IsDependent && !ours.Has(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }
    }
}
