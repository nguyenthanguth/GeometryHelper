using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The block table of a DXF file, as AutoCAD's <c>BlockTable</c>: model space, paper space and every block, each a
    /// <see cref="DxfBlockTableRecord"/>, in the order of the file, found by name and added to.
    /// </summary>
    /// <remarks>
    /// <c>db.BlockTable.Add(new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(1, 2, 0) })</c> adds the block BOLT
    /// drawn about (1, 2, 0), empty until entities are appended to it. In a file of R13 or later the table is its BLOCK_RECORD
    /// table, each record with its BLOCK; in one of R12 it is the blocks of the BLOCKS section, model space and paper space
    /// among them where the file has them as blocks and first where it does not.
    /// </remarks>
    public sealed class DxfBlockTable : DxfSymbolTable, IEnumerable<DxfBlockTableRecord>
    {
        private readonly DxfDatabase _database;
        private List<DxfBlockTableRecord> _records;

        internal DxfBlockTable(DxfDatabase database, DxfItem item)
            : base(item)
        {
            _database = database;
        }

        /// <summary>
        /// Gets the block of a name, letters of either case the same: <c>*Model_Space</c> is <see cref="DxfDatabase.ModelSpace"/>.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>The block.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no block of the name.</exception>
        public new DxfBlockTableRecord this[string key] => (DxfBlockTableRecord)base[key];

        /// <summary>Gets an enumerator over the blocks, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfBlockTableRecord> GetEnumerator() => Read().ToArray().AsEnumerable();

        IEnumerator<DxfBlockTableRecord> IEnumerable<DxfBlockTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records() => Read().ToArray();

        /// <summary>The block of a name; model and paper space by AutoCAD's names whatever the file calls them.</summary>
        private protected override DxfSymbolTableRecord Find(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            Read();

            if (DxfBlockTableRecord.IsModelName(key.Trim()))
            {
                return _database.ModelSpace;
            }

            return DxfBlockTableRecord.IsPaperName(key.Trim()) ? _database.PaperSpace : base.Find(key);
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfBlockTableRecord block))
            {
                throw new ArgumentException("The block table takes a DxfBlockTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            List<DxfBlockTableRecord> records = Read();
            block.AddTo(_database.File);
            records.Add(block);
        }

        /// <summary>Reads the table as the drawing is opened, so that model space and paper space stand for their records from the first.</summary>
        internal void Load() => Read();

        /// <summary>A block the file added itself, as one copied with an entity of another drawing, of the table from then on.</summary>
        internal void Adopted(DxfFileBlock block, DxfItem record)
        {
            if (_records == null)
            {
                Read();
                return;
            }

            _records.Add(new DxfBlockTableRecord(_database.File, record, block));
        }

        /// <summary>A block erased, no longer of the table.</summary>
        internal void Forget(DxfBlockTableRecord block) => Read().Remove(block);

        /// <summary>
        /// The blocks, read once: the BLOCK_RECORDs in their order, each with its block of the BLOCKS section, then the blocks
        /// no BLOCK_RECORD names; model space and paper space first where neither names them.
        /// </summary>
        private List<DxfBlockTableRecord> Read()
        {
            if (_records != null)
            {
                return _records;
            }

            DxfFile file = _database.File;
            var blocks = new List<DxfFileBlock>(file.Blocks.Values);
            blocks.Sort((a, b) => a.Head.First.CompareTo(b.Head.First));
            var taken = new HashSet<DxfFileBlock>();
            var records = new List<DxfBlockTableRecord>();
            bool model = false;
            bool paper = false;

            foreach (DxfItem item in file.TableItems("BLOCK_RECORD"))
            {
                string name = DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty);

                // The blocks by name, letters of either case the same, the first of a name kept: looked up, not searched.
                if (file.Blocks.TryGetValue(name, out DxfFileBlock block))
                {
                    taken.Add(block);
                }

                records.Add(Record(item, block, name, ref model, ref paper));
            }

            foreach (DxfFileBlock block in blocks)
            {
                if (!taken.Contains(block))
                {
                    records.Add(Record(null, block, block.Name, ref model, ref paper));
                }
            }

            if (!paper)
            {
                records.Insert(0, _database.PaperSpace);
            }

            if (!model)
            {
                records.Insert(0, _database.ModelSpace);
            }

            _records = records;
            return records;
        }

        private DxfBlockTableRecord Record(DxfItem item, DxfFileBlock block, string name, ref bool model, ref bool paper)
        {
            if (!model && DxfBlockTableRecord.IsModelName(name))
            {
                model = true;
                return _database.ModelSpace.Joined(item, block);
            }

            if (!paper && DxfBlockTableRecord.IsPaperName(name))
            {
                paper = true;
                return _database.PaperSpace.Joined(item, block);
            }

            // The record an INSERT has given for the block already is the one: one object for one block.
            return block?.Items.Record ?? new DxfBlockTableRecord(_database.File, item, block);
        }
    }

    /// <summary>
    /// A block of a DXF file, as AutoCAD's <c>BlockTableRecord</c>: model space, paper space or a block; its name, its
    /// origin, what kind it is, and its entities, enumerated, appended to and erased.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>db.ModelSpace.AppendEntity(new DxfLine(a, b))</c> adds a line to model space: its record is written at the end of
    /// the ENTITIES section, a block's before its ENDBLK, in the file's own style, with a handle of its own above every handle
    /// of the file and the block as its owner, as AutoCAD writes one. An entity is erased by its own
    /// <see cref="DxfEntity.Erase"/>.
    /// </para>
    /// <para>
    /// The entities are enumerated in the order of the file, those appended after them, each the same object every time; a
    /// POLYLINE is one entity with its vertices and an INSERT one with its attributes, as AutoCAD keeps them together. An
    /// enumeration walks the entities the block holds when it begins: those erased on the way are passed over, those appended
    /// on the way are not reached, and nothing throws, so a loop may erase or append as it goes.
    /// </para>
    /// </remarks>
    public sealed class DxfBlockTableRecord : DxfSymbolTableRecord, IEnumerable<DxfEntity>
    {
        /// <summary>The name AutoCAD gives the block of model space, as AutoCAD's <c>BlockTableRecord.ModelSpace</c>.</summary>
        public const string ModelSpace = "*Model_Space";

        /// <summary>The name AutoCAD gives the block of paper space, as AutoCAD's <c>BlockTableRecord.PaperSpace</c>.</summary>
        public const string PaperSpace = "*Paper_Space";

        // The groups of AcDbBlockBegin in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> BlockOrder = Array.AsReadOnly(new[] { 100, 2, 70, 10, 20, 30, 3, 1, 4 });

        // The groups of AcDbBlockTableRecord in the order AutoCAD writes them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> RecordOrder = Array.AsReadOnly(new[] { 100, 100, 2, 340, 70, 280, 281, 310 });

        // The sections and groups that name a block: INSERTs, dimensions and tables (2), its BLOCK (2 and 3), its
        // BLOCK_RECORD (2), and the arrows of a dimension style of R12 (5, 6 and 7).
        private static readonly string[] Naming = { "TABLES", "ENTITIES", "BLOCKS" };

        private DxfFile _file;
        private DxfFileBlock _block;
        private DxfSpace _space;
        private DxfSpaceKind _kind = DxfSpaceKind.Block;
        private GeoPoint3 _origin;
        private string _comments = string.Empty;
        private DxfUnits _units;
        private bool _explodable = true;
        private DxfBlockScaling _scaling;

        /// <summary>
        /// Initializes a block made in code, to be added to a <see cref="DxfBlockTable"/> with its
        /// <see cref="DxfSymbolTableRecord.Name"/> and <see cref="Origin"/> given.
        /// </summary>
        public DxfBlockTableRecord()
        {
        }

        /// <summary>A block of a file: its BLOCK_RECORD where the file has one, and its block of the BLOCKS section.</summary>
        internal DxfBlockTableRecord(DxfFile file, DxfItem record, DxfFileBlock block)
            : base(record ?? block?.Head)
        {
            _file = file;
            _block = block;
            _space = block?.Items;

            if (_space != null)
            {
                _space.Record = this;
            }
        }

        /// <summary>Model space or paper space, whose entities are those of the ENTITIES section, read when first asked for.</summary>
        internal DxfBlockTableRecord(DxfFile file, DxfSpaceKind kind)
        {
            _file = file;
            _kind = kind;
        }

        /// <summary>The entities of the block; those of the ENTITIES section for model and paper space, read when first asked for.</summary>
        private DxfSpace Space
        {
            get
            {
                if (_space == null && _file != null && _kind != DxfSpaceKind.Block)
                {
                    DxfSpace space = _kind == DxfSpaceKind.Model ? _file.ModelSpace : _file.PaperSpace;
                    space.Record = this;
                    _space = space;
                }

                return _space;
            }
        }

        /// <summary>
        /// Gets or sets the name of the block, as <see cref="DxfSymbolTableRecord.Name"/>: AutoCAD's <c>*Model_Space</c> and
        /// <c>*Paper_Space</c> for model space and paper space, whatever the file calls them, as a file of R12 calls them
        /// <c>$Model_Space</c> and <c>$Paper_Space</c>.
        /// </summary>
        /// <value>
        /// Set on a block made in code, before it is added; on a block of a file, as AutoCAD renames one: its BLOCK_RECORD's 2,
        /// its BLOCK's 2 and 3, the 2 of every INSERT of it, in model space, paper space and the blocks, and of a dimension or a
        /// table drawn by it, the arrows of the dimension styles and of the header (<c>$DIMBLK</c>, <c>$DIMBLK1</c>,
        /// <c>$DIMBLK2</c>, <c>$DIMLDRBLK</c>) that name it; every other record keeping its bytes. The name it has changes
        /// nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is one AutoCAD does not take, begins with *, or is that of
        /// another block, letters of either case the same.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on the block of a layout, on one AutoCAD names itself (a
        /// name that begins with *), on one of an external reference, or on one erased.</exception>
        public override string Name
        {
            get
            {
                if (_kind == DxfSpaceKind.Model)
                {
                    return ModelSpace;
                }

                if (_kind == DxfSpaceKind.Paper)
                {
                    return PaperSpace;
                }

                if (Item != null && Item.Type == "BLOCK_RECORD")
                {
                    return base.Name;
                }

                return _block != null ? _block.Name : base.Name;
            }

            set
            {
                if (_file == null)
                {
                    base.Name = value;
                }
                else if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                else if (!string.Equals(value, Name, StringComparison.Ordinal))
                {
                    Rename(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the block (group 4 of its BLOCK), as AutoCAD's <c>Comments</c>; empty where it gives
        /// none.
        /// </summary>
        /// <value>Set, written as the BLOCK's 4, after its 1; an empty one takes the group out. The description it has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the description set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the description set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on model space or paper space of a file with no BLOCK of
        /// them, on a block of a file older than R2000 (AC1015), which has no descriptions, or on one erased.</exception>
        public string Comments
        {
            get => _file == null ? _comments : _block?.Head == null ? string.Empty : _file.ValueOf(_block.Head, 4) ?? string.Empty;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    throw new ArgumentException("A description is one line, with no line break in it.", nameof(value));
                }

                if (string.Equals(value, Comments, StringComparison.Ordinal))
                {
                    return;
                }

                if (_file == null)
                {
                    _comments = value;
                    return;
                }

                DxfItem head = _block?.Head ?? throw new InvalidOperationException("The " + Name + " of this file has no BLOCK to hold a description.");
                _file.Requires(1015, "descriptions of blocks", "R2000 (AC1015)");
                _file.SetValues(head, new[] { new KeyValuePair<int, string>(4, value.Length == 0 ? null : value) }, BlockOrder, false);
            }
        }

        /// <summary>
        /// Gets or sets the unit the block is drawn in (group 70 of its BLOCK_RECORD), as AutoCAD's <c>Units</c>: an INSERT of a
        /// block in millimetres into a drawing in metres scales it by a thousandth; <see cref="DxfUnits.Undefined"/> where it
        /// gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set is not one of <see cref="DxfUnits"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a block of a file older than R2007 (AC1021), which
        /// writes no unit of a block, or on one erased.</exception>
        public DxfUnits Units
        {
            get
            {
                if (_file == null)
                {
                    return _units;
                }

                int units = RecordWhole(70, 0);
                return units >= 0 && units <= 24 ? (DxfUnits)units : DxfUnits.Undefined;
            }

            set
            {
                if ((int)value < 0 || (int)value > 24)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A unit is one of DxfUnits, 0 to 24.");
                }

                if (value == Units)
                {
                    return;
                }

                if (_file == null)
                {
                    _units = value;
                    return;
                }

                SetRecord(70, ((int)value).ToString(CultureInfo.InvariantCulture), "units of blocks");
            }
        }

        /// <summary>
        /// Gets or sets whether the INSERTs of the block may be exploded (group 280 of its BLOCK_RECORD), as AutoCAD's
        /// <c>Explodable</c>; true where it gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a block of a file older than R2007 (AC1021), or on one erased.</exception>
        public bool Explodable
        {
            get => _file == null ? _explodable : RecordWhole(280, 1) != 0;

            set
            {
                if (value == Explodable)
                {
                    return;
                }

                if (_file == null)
                {
                    _explodable = value;
                    return;
                }

                SetRecord(280, value ? "1" : "0", "blocks kept from exploding");
            }
        }

        /// <summary>
        /// Gets or sets how the INSERTs of the block may scale it (group 281 of its BLOCK_RECORD), as AutoCAD's
        /// <c>BlockScaling</c>; <see cref="DxfBlockScaling.Any"/> where it gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaling set is not one of <see cref="DxfBlockScaling"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a block of a file older than R2007 (AC1021), or on one erased.</exception>
        public DxfBlockScaling BlockScaling
        {
            get => _file == null ? _scaling : RecordWhole(281, 0) == 1 ? DxfBlockScaling.Uniform : DxfBlockScaling.Any;

            set
            {
                if (value != DxfBlockScaling.Any && value != DxfBlockScaling.Uniform)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A block's scaling is Any or Uniform.");
                }

                if (value == BlockScaling)
                {
                    return;
                }

                if (_file == null)
                {
                    _scaling = value;
                    return;
                }

                SetRecord(281, value == DxfBlockScaling.Uniform ? "1" : "0", "uniform scaling of blocks");
            }
        }

        /// <summary>Gets whether the block has a preview picture (groups 310 of its BLOCK_RECORD), as AutoCAD's <c>HasPreviewIcon</c>.</summary>
        public bool HasPreviewIcon => PreviewIcon != null;

        /// <summary>
        /// Gets or sets the preview picture of the block, as AutoCAD's <c>PreviewIcon</c> gives it as a bitmap: here the bytes its
        /// BLOCK_RECORD keeps, the groups 310 one after another, a bitmap of Windows without the file's header, as AutoCAD writes
        /// it; null where it has none.
        /// </summary>
        /// <value>Set, written as groups 310 of 127 bytes each, after the record's other groups of AcDbBlockTableRecord; null takes them out. Bytes the same as those it has change nothing.</value>
        /// <exception cref="InvalidOperationException">Thrown when set on a block made in code, on one of a file older than R2000
        /// (AC1015), which has no previews, or on one erased.</exception>
        public byte[] PreviewIcon
        {
            get
            {
                if (_file == null || Item == null || Item.Type != "BLOCK_RECORD")
                {
                    return null;
                }

                var bytes = new List<byte>();
                bool any = false;

                foreach (DxfTypedValue group in _file.OwnGroupsOf(Item, 0))
                {
                    if (group.TypeCode == 310)
                    {
                        any = true;
                        bytes.AddRange(DxfBinary.Bytes(group.Value));
                    }
                }

                return any ? bytes.ToArray() : null;
            }

            set
            {
                byte[] now = PreviewIcon;

                if (value == null ? now == null : now != null && DxfBinary.Same(value, now))
                {
                    return;
                }

                if (_file == null)
                {
                    throw new InvalidOperationException("A block made in code is given its preview once it is added to a file.");
                }

                _file.Requires(1015, "previews of blocks", "R2000 (AC1015)");

                if (Item == null || Item.Type != "BLOCK_RECORD")
                {
                    throw new InvalidOperationException("The block " + Name + " has no BLOCK_RECORD to hold a preview.");
                }

                var groups = new List<KeyValuePair<int, string>>();

                foreach (string line in DxfBinary.Lines(value ?? new byte[0]))
                {
                    groups.Add(new KeyValuePair<int, string>(310, line));
                }

                _file.ReplaceOwn(Item, 0, new HashSet<int> { 310 }, groups, RecordOrder, false);
            }
        }

        /// <summary>Gets whether the block is a dynamic block, as AutoCAD's <c>IsDynamicBlock</c>: its BLOCK_RECORD has the extended data of the application <c>AcDbDynamicBlockGUID</c>, as AutoCAD gives a dynamic block.</summary>
        public bool IsDynamicBlock => Item != null && Item.Type == "BLOCK_RECORD" && GetXDataForApplication("AcDbDynamicBlockGUID") != null;

        /// <summary>
        /// Gets the hyperlinks of the block, as AutoCAD's <c>Hyperlinks</c>: those of the extended data of the application
        /// PE_URL of its BLOCK_RECORD, as <see cref="DxfHyperLinkCollection"/> says.
        /// </summary>
        public DxfHyperLinkCollection Hyperlinks => new DxfHyperLinkCollection(this);

        /// <summary>
        /// Gets the INSERTs of the block, as AutoCAD's <c>GetBlockReferenceIds</c> gives their ids: every
        /// <see cref="DxfBlockReference"/> of it, a MINSERT among them, in model space, paper space and every block, in the order
        /// of the block table and then of each block.
        /// </summary>
        /// <param name="directOnly">Whether only the INSERTs of this block are given; false gives also those of every block that
        /// holds one of it, at any depth, as AutoCAD's does.</param>
        /// <returns>The INSERTs; none for a block made in code.</returns>
        public IReadOnlyList<DxfBlockReference> GetBlockReferences(bool directOnly)
        {
            var references = new List<DxfBlockReference>();
            DxfDatabase database = _file?.Database;

            if (database == null)
            {
                return references.AsReadOnly();
            }

            // One walk of every block: its INSERTs, in the order of the table, and which blocks hold an INSERT of each name.
            var inserts = new List<KeyValuePair<DxfBlockReference, string>>();
            var holders = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (!(entity is DxfBlockReference reference))
                    {
                        continue;
                    }

                    string inserted = reference.Name;
                    inserts.Add(new KeyValuePair<DxfBlockReference, string>(reference, inserted));

                    if (!directOnly && !block.IsLayout)
                    {
                        if (!holders.TryGetValue(inserted, out List<string> by))
                        {
                            by = new List<string>();
                            holders.Add(inserted, by);
                        }

                        by.Add(block.Name);
                    }
                }
            }

            // The blocks that hold one of this one, at any depth: up the graph from this block, each once.
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Name };
            var queue = new Queue<string>();
            queue.Enqueue(Name);

            while (!directOnly && queue.Count > 0)
            {
                if (!holders.TryGetValue(queue.Dequeue(), out List<string> by))
                {
                    continue;
                }

                foreach (string holder in by)
                {
                    if (named.Add(holder))
                    {
                        queue.Enqueue(holder);
                    }
                }
            }

            foreach (KeyValuePair<DxfBlockReference, string> insert in inserts)
            {
                if (named.Contains(insert.Value))
                {
                    references.Add(insert.Key);
                }
            }

            return references.AsReadOnly();
        }

        /// <summary>
        /// Gets the blocks AutoCAD made of this dynamic block for its INSERTs, as its <c>GetAnonymousBlockIds</c> gives their
        /// ids: each block of the table that names this one as the block it represents, by the extended data of the application
        /// <c>AcDbBlockRepBTag</c> (its 1005) or by the object <c>AcDbRepData</c> of the dictionary
        /// <c>AcDbBlockRepresentation</c> of its extension dictionary (its 340), in the order of the block table.
        /// </summary>
        /// <returns>The blocks; none for a block that is not dynamic, or made in code.</returns>
        public IReadOnlyList<DxfBlockTableRecord> GetAnonymousBlocks()
        {
            var blocks = new List<DxfBlockTableRecord>();
            DxfDatabase database = _file?.Database;
            string handle = Handle;

            if (database == null || handle == null)
            {
                return blocks.AsReadOnly();
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (block != this && block.Represents(handle))
                {
                    blocks.Add(block);
                }
            }

            return blocks.AsReadOnly();
        }

        /// <summary>
        /// Moves entities of the drawing into the block, as AutoCAD's <c>AssumeOwnershipOf</c>: each taken out of the block it is
        /// in and written at the end of this one, before its ENDBLK, or at the end of the ENTITIES section for model and paper
        /// space, its handle kept, this block as its owner and its group 67 as the space has it; the same object stands for it.
        /// </summary>
        /// <param name="entitiesToMove">The entities, of this block's drawing; one this block holds already stays where it is.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities, or one of them, are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entity is in no file or another, follows another (a vertex, an
        /// attribute), or is an INSERT that would make the block insert itself; nothing is moved then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this block is in no file, or an entity is erased.</exception>
        public void AssumeOwnershipOf(IEnumerable<DxfEntity> entitiesToMove)
        {
            if (entitiesToMove == null)
            {
                throw new ArgumentNullException(nameof(entitiesToMove));
            }

            DxfSpace space = Space;

            if (space == null)
            {
                throw new InvalidOperationException("The block " + Name + " is in no file: add it to a block table first.");
            }

            var moving = new List<DxfEntity>();

            foreach (DxfEntity entity in entitiesToMove)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entitiesToMove), "An entity to move is null.");
                }

                if (entity.IsErased)
                {
                    throw new InvalidOperationException("The " + entity.Item.Type + " " + (entity.Handle ?? string.Empty) + " is erased, and is not moved.");
                }

                if (!entity.IsResident || entity.Item.File != _file)
                {
                    throw new ArgumentException("The " + entity.Item.Type + " is not of this block's drawing: append a copy of it.", nameof(entitiesToMove));
                }

                if (entity.Item.Parent != null)
                {
                    throw new ArgumentException("A " + entity.Item.Type + " belongs to the entity it follows, and moves with it.", nameof(entitiesToMove));
                }

                if (entity is DxfBlockReference reference && !IsLayout && reference.BlockTableRecord is DxfBlockTableRecord inserted
                    && (string.Equals(inserted.Name, Name, StringComparison.OrdinalIgnoreCase) || Holds(inserted, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Name }, new HashSet<string>(StringComparer.OrdinalIgnoreCase))))
                {
                    throw new ArgumentException("The INSERT of " + inserted.Name + " would make the block " + Name + " insert itself.", nameof(entitiesToMove));
                }

                if (entity.Item.Space != space && !moving.Contains(entity))
                {
                    moving.Add(entity);
                }
            }

            foreach (DxfEntity entity in moving)
            {
                entity.Bind(_file.Move(entity.Item, space));
            }
        }

        /// <summary>
        /// Erases the block from the drawing, as AutoCAD's <c>Erase</c> of a block nothing inserts: its BLOCK, its entities and its
        /// ENDBLK taken out of the BLOCKS section and its BLOCK_RECORD out of the block table, each handle not given again and
        /// what they owned removed with them when the file is saved.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the block is in no file or is erased already; when it is the
        /// block of a layout; when an INSERT, a dimension or a table of the drawing is drawn by it, or a dimension style or the
        /// header names it as an arrow; or when an object of the file points at it.</exception>
        public override void Erase()
        {
            if (_file == null || _block == null)
            {
                throw new InvalidOperationException("The block " + Name + " is in no file to be erased from, or is model or paper space.");
            }

            if (IsErased || (_block.Head != null && _block.Head.IsRemoved))
            {
                throw new InvalidOperationException("The block " + Name + " is erased already.");
            }

            string name = Name;

            if (IsLayout)
            {
                throw new InvalidOperationException("The block " + name + " is the block of a layout, and goes with its layout.");
            }

            if (_file.Names(Naming, (type, code) => code == 2 && (type == "INSERT" || type == "DIMENSION" || type == "ACAD_TABLE"), name) || NamedAsArrow(name))
            {
                throw new InvalidOperationException("The block " + name + " is drawn by an INSERT, a dimension or a table, or named as an arrow, and is not erased, as AutoCAD erases a block only when nothing uses it.");
            }

            if (Handle != null && _file.PointedAt(Handle))
            {
                throw new InvalidOperationException("The block " + name + " is pointed at by an object of the file, as a dimension style's arrow, and is not erased.");
            }

            _file.RemoveBlock(_block);

            if (Item != null && Item.Type == "BLOCK_RECORD")
            {
                _file.RemoveTableRecord(Item, "BLOCK_RECORD");
            }

            _file.Database?.BlockTable.Forget(this);
        }

        /// <summary>Gets whether the block has been erased from its drawing: its BLOCK_RECORD, or its BLOCK in a file of R12.</summary>
        internal bool Gone => IsErased || (_block?.Head != null && _block.Head.IsRemoved);

        /// <summary>Renames the block of a file with every reference to it, as <see cref="Name"/> says.</summary>
        private protected override void Rename(string name)
        {
            DxfSymbolTable.CheckName(name, "value");
            string old = Name;

            if (Gone)
            {
                throw new InvalidOperationException("The block " + old + " is erased, and is not changed.");
            }

            if (IsLayout || _block == null)
            {
                throw new InvalidOperationException("The block " + old + " is the block of a layout, and is renamed with its layout.");
            }

            if (old.StartsWith("*", StringComparison.Ordinal) || name.StartsWith("*", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A block whose name begins with * is one AutoCAD names itself, and is not renamed.");
            }

            if (IsDependent)
            {
                throw new InvalidOperationException("The block " + old + " comes from an external reference, and is renamed in its own drawing.");
            }

            DxfDatabase database = _file.Database;

            if (database != null && database.BlockTable.Has(name) && !ReferenceEquals(database.BlockTable[name], this))
            {
                throw new ArgumentException("The block table has a block named " + name + " already, letters of either case the same.", "value");
            }

            _file.Rename(Naming, (type, code) => (code == 2 && (type == "INSERT" || type == "DIMENSION" || type == "ACAD_TABLE" || type == "BLOCK" || type == "BLOCK_RECORD"))
                || (code == 3 && type == "BLOCK") || (type == "DIMSTYLE" && code >= 5 && code <= 7), 0, old, name);

            foreach (string variable in new[] { "$DIMBLK", "$DIMBLK1", "$DIMBLK2", "$DIMLDRBLK" })
            {
                IReadOnlyList<DxfTypedValue> groups = _file.HeaderOf(variable);

                if (groups != null && groups.Count == 1 && string.Equals(DxfReader.NameOf(groups[0].Value), old, StringComparison.OrdinalIgnoreCase))
                {
                    _file.SetHeader(variable, new[] { new DxfTypedValue(groups[0].TypeCode, name) });
                }
            }

            _file.Renamed(_block, name);
            GeometryHelperLog.Info("DxfDatabase: the block " + old + " was renamed " + name + ", with every INSERT of it.");
        }

        /// <summary>Whether a dimension style of R12 or the header names a block as an arrow: <c>$DIMBLK</c> and its kin, DIMSTYLE's 5, 6 and 7.</summary>
        private bool NamedAsArrow(string name)
        {
            foreach (string variable in new[] { "$DIMBLK", "$DIMBLK1", "$DIMBLK2", "$DIMLDRBLK" })
            {
                IReadOnlyList<DxfTypedValue> groups = _file.HeaderOf(variable);

                if (groups != null && groups.Count > 0 && string.Equals(DxfReader.NameOf(groups[0].Value), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return _file.Names(Naming, (type, code) => type == "DIMSTYLE" && code >= 5 && code <= 7, name);
        }

        /// <summary>Whether a block holds an INSERT of a block of the names given, at any depth.</summary>
        private static bool Holds(DxfBlockTableRecord block, HashSet<string> named, HashSet<string> seen = null)
        {
            seen = seen ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!seen.Add(block.Name))
            {
                return false;
            }

            foreach (DxfEntity entity in block)
            {
                if (entity is DxfBlockReference reference)
                {
                    if (named.Contains(reference.Name))
                    {
                        return true;
                    }

                    if (reference.BlockTableRecord is DxfBlockTableRecord inner && Holds(inner, named, seen))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether the block is an anonymous one AutoCAD made of a dynamic block of a handle, as <see cref="GetAnonymousBlocks"/> finds them.</summary>
        private bool Represents(string handle)
        {
            if (!IsAnonymous || Item == null || Item.Type != "BLOCK_RECORD")
            {
                return false;
            }

            IReadOnlyList<DxfTypedValue> tag = GetXDataForApplication("AcDbBlockRepBTag");

            for (int i = 1; tag != null && i < tag.Count; i++)
            {
                if (tag[i].TypeCode == 1005 && string.Equals(tag[i].Value.Trim(), handle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            DxfDBDictionary extension = ExtensionDictionary;

            if (extension == null || !extension.Contains("AcDbBlockRepresentation") || !(extension.GetAt("AcDbBlockRepresentation") is DxfDBDictionary representation)
                || !representation.Contains("AcDbRepData") || !(representation.GetAt("AcDbRepData") is DxfDBObject data))
            {
                return false;
            }

            foreach (DxfTypedValue group in _file.ObjectGroupsOf(data.Item))
            {
                if (group.TypeCode == 340 && string.Equals(group.Value.Trim(), handle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The whole number of a group of the block's BLOCK_RECORD; the fallback where it has none or no BLOCK_RECORD.</summary>
        private int RecordWhole(int code, int fallback)
            => Item != null && Item.Type == "BLOCK_RECORD" && int.TryParse(_file.ValueOf(Item, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>Sets a group of the block's BLOCK_RECORD, which a file of R2007 or later has.</summary>
        private void SetRecord(int code, string value, string what)
        {
            _file.Requires(1021, what, "R2007 (AC1021)");

            if (Item == null || Item.Type != "BLOCK_RECORD")
            {
                throw new InvalidOperationException("The block " + Name + " has no BLOCK_RECORD to hold its " + what + ".");
            }

            _file.SetValues(Item, new[] { new KeyValuePair<int, string>(code, value) }, RecordOrder, false);
        }

        /// <summary>
        /// Gets or sets the origin of the block (groups 10, 20, 30 of its BLOCK), as AutoCAD's <c>Origin</c>: the point of the
        /// block an INSERT puts at its insertion point; the origin of the world for model and paper space.
        /// </summary>
        /// <value>Set on a block made in code, before it is added, or on a block of a file, which moves where its INSERTs place it.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on model space or paper space of a file of R12, which has no BLOCK of them.</exception>
        public GeoPoint3 Origin
        {
            get
            {
                if (_file == null)
                {
                    return _origin;
                }

                DxfItem head = _block?.Head;
                return head == null ? default(GeoPoint3) : PointOf(head, 10);
            }

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("An origin has to be of finite numbers: " + value + " is not.", nameof(value));
                }

                if (_file == null)
                {
                    _origin = value;
                    return;
                }

                GeoPoint3 now = Origin;

                if (Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z))
                {
                    return;
                }

                DxfItem head = _block?.Head ?? throw new InvalidOperationException("The " + Name + " of this file has no BLOCK to give an origin.");
                var groups = new List<KeyValuePair<int, string>>();
                double[] values = { value.X, value.Y, value.Z };

                for (int i = 0; i < 3; i++)
                {
                    groups.Add(new KeyValuePair<int, string>(10 + 10 * i, Numbers.Exact(values[i] + 0.0)));
                }

                _file.SetValues(head, groups, BlockOrder, false);
            }
        }

        /// <summary>
        /// Gets whether the block is that of a layout, as AutoCAD's <c>IsLayout</c>: model space, paper space, or the block of
        /// another layout, <c>*Paper_Space0</c> and on, or one whose BLOCK_RECORD names a layout (group 340).
        /// </summary>
        public bool IsLayout
        {
            get
            {
                string name = Name ?? string.Empty;

                if (_kind != DxfSpaceKind.Block || IsModelName(name) || name.StartsWith(PaperSpace, StringComparison.OrdinalIgnoreCase) || name.StartsWith("$Paper_Space", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string layout = Item != null && Item.Type == "BLOCK_RECORD" ? Item.File.ValueOf(Item, 340)?.Trim() : null;
                return !string.IsNullOrEmpty(layout) && layout != "0";
            }
        }

        /// <summary>Gets whether the block is one AutoCAD names itself, as a dimension's <c>*D1</c> (flag 1 of its BLOCK's group 70), as AutoCAD's <c>IsAnonymous</c>.</summary>
        public bool IsAnonymous => (Flags & 1) != 0;

        /// <summary>Gets whether the block is an external reference (flag 4), as AutoCAD's <c>IsFromExternalReference</c>.</summary>
        public bool IsFromExternalReference => (Flags & 4) != 0;

        /// <summary>Gets whether the block is an external reference overlaid (flag 8), as AutoCAD's <c>IsFromOverlayReference</c>.</summary>
        public bool IsFromOverlayReference => (Flags & 8) != 0;

        /// <summary>Gets whether the block has attribute definitions (flag 2), as AutoCAD's <c>HasAttributeDefinitions</c>.</summary>
        public bool HasAttributeDefinitions => (Flags & 2) != 0;

        /// <summary>Gets the path of the drawing an external reference comes from (group 1 of its BLOCK), as AutoCAD's <c>PathName</c>; empty for a block that is none.</summary>
        public string PathName => _block?.Head == null || _file == null ? string.Empty : _file.ValueOf(_block.Head, 1) ?? string.Empty;

        /// <summary>The file the block is of; null for a block made in code and not yet added.</summary>
        internal DxfFile File => _file;

        /// <summary>Model or paper space of a file with no record of it, as one of R12 with no block of it.</summary>
        internal override bool HasNoRecord => _file != null && Item == null;

        /// <summary>
        /// Gets how many entities the block holds, a POLYLINE with its vertices one, an MTEXT with its linked columns one, as
        /// <see cref="GetEnumerator"/> walks them.
        /// </summary>
        public int Count
        {
            get
            {
                DxfSpace space = Space;

                if (space == null)
                {
                    return 0;
                }

                DxfItem[] items = space.Snapshot();
                HashSet<string> columns = LinkedColumns(items);

                if (columns == null)
                {
                    return space.Count;
                }

                int count = 0;

                foreach (DxfItem item in items)
                {
                    count += item.IsRemoved || IsColumn(item, columns) ? 0 : 1;
                }

                return count;
            }
        }

        /// <summary>
        /// Appends an entity made in code to the block, as AutoCAD's <c>AppendEntity</c>: written at the end of the ENTITIES
        /// section for model space and paper space, before the block's ENDBLK for a block, in the file's own style. The same
        /// object is the entity of the block from then on, with its handle and its owner.
        /// </summary>
        /// <param name="entity">The entity, made in code and in no file yet.</param>
        /// <remarks>
        /// <para>
        /// Its handle is the next above every handle of the file and above <c>$HANDSEED</c> less one, <c>$HANDSEED</c> set above
        /// it, and its owner the block's BLOCK_RECORD from R13 on; an entity of paper space is written with its group 67 of 1. A
        /// file whose records have no handles, as many writers leave them, gives it none. Its layer is added to the layer table
        /// where the table does not list it, and the applications of its extended data to the table of applications. The
        /// extents of the header grow to hold an entity of model or paper space.
        /// </para>
        /// <para>
        /// A file of R12 has no SPLINE, ELLIPSE, RAY, XLINE nor LWPOLYLINE, which are refused, nor lineweights or true colours.
        /// Nor has it MTEXT: an MTEXT is written there as one TEXT for each paragraph (<c>\P</c>, a column break <c>\N</c> taken
        /// as one), its formatting and the heights of its codes left out, not wrapped at its width, which AutoCAD's SAVEAS to R12
        /// wraps by the letters of the font, which the library does not read; and so is each MTEXT of a block copied there with an
        /// entity of another drawing, a dimension's <c>*D</c> among them. Each TEXT is in the MTEXT's text style where the drawing
        /// has it or is given it by name with the copy, else in Standard; a line of more than 255 letters is written whole, not
        /// split, a length older readers of R12 may not take. The TEXTs are appended in the MTEXT's place, and the MTEXT stays in no file: an MTEXT
        /// of two lines, 2.5 high and held at its top left at (10, 20), comes out as two TEXTs on the baselines 17.5 and 13.33
        /// high. An entity of R13 or later is written in it in the forms of R12, as AutoCAD saves a drawing as R12: each
        /// record keeps the groups R12 has for its type, each read from the part of the record it belongs to (a centred
        /// ATTRIB's 72 from its AcDbText, not the 72 its AcDbAttribute writes again), its markers, owner and an object embedded
        /// after a group 101 left out; a type with no form of R12 the library writes, as a HATCH, is refused. An entity of R12
        /// in a file of R13 or later is given AutoCAD's subclass markers in their order.
        /// </para>
        /// <para>
        /// An entity copied from another drawing (<see cref="DxfEntity.Clone"/>, <see cref="DxfEntity.GetTransformedCopy"/>)
        /// has its pointers taken to this drawing as AutoCAD's deep clone between databases takes them: a material, plot style
        /// or visual style to the one of the same dictionary and entry names, a record of a table to the one of its name; a
        /// pointer to an object this drawing has no namesake of is left out, so that its default holds, and a handle of
        /// extended data (1005) is left out, as AutoCAD's mapping leaves it null, but for the records the overrides of a dimension
        /// style name (ACAD DSTYLE), which name those of the same name here. The records of the tables a copy names that this
        /// drawing has not (its layers, linetypes, text styles and dimension styles, and their arrow blocks) are copied with it by
        /// name, and a block AutoCAD names itself it draws by (a dimension's <c>*D</c>, an INSERT's <c>*U</c>) under a name of its
        /// own, as AutoCAD's deep clone does; an INSERT's named block where this drawing has none of its name, under that name, with
        /// the blocks it inserts, as AutoCAD's <c>WblockCloneObjects</c> brings it, and every block a copy points to by a hard
        /// pointer so too (a MULTILEADER's block and arrow blocks, the block of an ACAD_TABLE's cell), as AutoCAD's deep clone
        /// follows its pointers to them. What the entities of the blocks copied point
        /// to among themselves (a LEADER to its MTEXT, a HATCH to its boundary) is kept between their copies, whichever comes first
        /// in its block, as AutoCAD's deep clone translates its map of ids once every object is copied, and so is every pointer to
        /// a block copied with it after its own (a MULTILEADER's block and arrow blocks, an ACAD_TABLE's picture and the blocks of
        /// its cells, the arrow block an override of a dimension style names). A copy appended to its own drawing keeps its pointers, but for the
        /// value a MULTILEADER gives an attribute definition erased since, which is left out with the definition's handle. The
        /// extents of the header are not grown by what is appended to a block.
        /// </para>
        /// <para>
        /// A drawing of an older version than the entity's is given what it can hold, as AutoCAD's SAVEAS to that version gives
        /// it: the overrides of a dimension style (ACAD DSTYLE) of a variable the version has not are left out, as DIMLTYPE of
        /// R2007 on a dimension added to a drawing of R2000, and so are the groups the version has not of the records of the
        /// tables copied by name with it, as a DIMSTYLE's DIMFXLEN (49) and a LAYER's material (347). A table's block cell keeps
        /// the values it gives the attributes of a block that comes with it, its pointers (331) naming the copies of their
        /// definitions; a LEADER whose annotation does not come with it points to none.
        /// <see cref="DxfDatabase.WblockCloneObjects"/> copies entities together, what points from one to another kept.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the file's version cannot hold the entity's type or look, or its
        /// linetype is not in the file's linetype table.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is in a file already, or this block is not.</exception>
        public void AppendEntity(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.IsResident)
            {
                throw new InvalidOperationException("The " + entity.Item.Type + " " + (entity.Handle ?? string.Empty) + " is in a file already: append a copy of it.");
            }

            DxfSpace space = Space;

            if (space == null)
            {
                throw new InvalidOperationException("The block " + Name + " is in no file: add it to a block table first.");
            }

            entity.Appending(this);

            // A drawing of R12 has no MTEXT: it is written as the TEXTs of its lines, which are appended in its place.
            if (entity is DxfMText mtext && !_file.HasMarkers)
            {
                foreach (DxfEntity text in mtext.R12Texts(_file))
                {
                    text.Appending(this);
                    text.Bind(_file.Adopt(space, text.Item));
                }

                return;
            }

            entity.Bind(_file.Adopt(space, entity.Item));
        }

        /// <summary>
        /// Gets an enumerator over the entities, in the order of the file, those appended after them: over the entities the
        /// block holds when the walk begins, so that <c>foreach (DxfEntity e in db.ModelSpace) e.Erase();</c> erases them all,
        /// as it does in AutoCAD. An entity erased during the walk is passed over, and one appended is not reached.
        /// </summary>
        /// <remarks>
        /// The linked columns of an MTEXT of a file before R2018, the MTEXT records its extended data of ACAD lists between
        /// <c>ACAD_MTEXT_COLUMNS_BEGIN</c> and <c>ACAD_MTEXT_COLUMNS_END</c> (1005), are part of that MTEXT, as they are in
        /// AutoCAD's database, and are not walked of their own: the MTEXT that lists them carries them, as <see cref="DxfMText"/> says, so
        /// that a walk that moves every MTEXT moves each column once. An MTEXT that lists its own handle among them is walked still.
        /// </remarks>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfEntity> GetEnumerator()
        {
            DxfSpace space = Space;

            if (space == null)
            {
                yield break;
            }

            DxfItem[] items = space.Snapshot();
            HashSet<string> columns = LinkedColumns(items);

            foreach (DxfItem item in items)
            {
                if (!item.IsRemoved && !IsColumn(item, columns))
                {
                    yield return DxfEntity.Of(item);
                }
            }
        }

        /// <summary>
        /// The handles of the linked columns the MTEXTs of entities list, letters of either case the same, found in one pass;
        /// null where none lists any.
        /// </summary>
        private static HashSet<string> LinkedColumns(DxfItem[] items)
        {
            HashSet<string> columns = null;

            foreach (DxfItem item in items)
            {
                if (item.IsRemoved || item.Type != "MTEXT")
                {
                    continue;
                }

                bool acad = false;
                bool listing = false;
                string own = item.Handle;

                foreach (DxfTypedValue group in item.File.XDataOf(item))
                {
                    if (group.TypeCode == 1001)
                    {
                        acad = string.Equals(group.Value.Trim(), "ACAD", StringComparison.OrdinalIgnoreCase);
                        listing = false;
                    }
                    else if (acad && group.TypeCode == 1000)
                    {
                        string said = group.Value.Trim();
                        listing = said == "ACAD_MTEXT_COLUMNS_BEGIN" || (listing && said != "ACAD_MTEXT_COLUMNS_END");
                    }
                    else if (listing && group.TypeCode == 1005 && !string.Equals(group.Value.Trim(), own, StringComparison.OrdinalIgnoreCase))
                    {
                        // Only another MTEXT's columns: one that lists itself stays an entity of the block.
                        columns = columns ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        columns.Add(group.Value.Trim());
                    }
                }
            }

            return columns;
        }

        /// <summary>
        /// Every entity the block holds, in the order of the file, the linked columns of an MTEXT among them, which
        /// <see cref="GetEnumerator"/> leaves to their MTEXT: for what reaches the columns through their MTEXT.
        /// </summary>
        internal IEnumerable<DxfEntity> AllEntities()
        {
            DxfSpace space = Space;

            if (space == null)
            {
                yield break;
            }

            foreach (DxfItem item in space.Snapshot())
            {
                if (!item.IsRemoved)
                {
                    yield return DxfEntity.Of(item);
                }
            }
        }

        /// <summary>
        /// The entity of a handle among those the block holds, letters of either case the same, a linked column of an MTEXT
        /// among them; null where it holds none of that handle.
        /// </summary>
        internal DxfEntity EntityByHandle(string handle)
        {
            DxfSpace space = Space;

            if (space == null || handle == null)
            {
                return null;
            }

            foreach (DxfItem item in space.Snapshot())
            {
                if (!item.IsRemoved && string.Equals(item.Handle, handle.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return DxfEntity.Of(item);
                }
            }

            return null;
        }

        /// <summary>Whether an entity is an MTEXT another lists as one of its linked columns.</summary>
        private static bool IsColumn(DxfItem item, HashSet<string> columns)
            => columns != null && item.Type == "MTEXT" && item.Handle != null && columns.Contains(item.Handle);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>The flags of the block's BLOCK (group 70); nought for model and paper space with none.</summary>
        private protected override int Flags => _block?.Head == null || _file == null ? 0 : FlagsOf(_block.Head);

        /// <summary>Whether a name is that of model space, as AutoCAD (<c>*Model_Space</c>) or ezdxf's files of R12 (<c>$Model_Space</c>) spell it.</summary>
        internal static bool IsModelName(string name) => string.Equals(name, ModelSpace, StringComparison.OrdinalIgnoreCase) || string.Equals(name, "$Model_Space", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether a name is that of paper space, the layout last shown.</summary>
        internal static bool IsPaperName(string name) => string.Equals(name, PaperSpace, StringComparison.OrdinalIgnoreCase) || string.Equals(name, "$Paper_Space", StringComparison.OrdinalIgnoreCase);

        /// <summary>Model space or paper space given its record and its block, where the file has them.</summary>
        internal DxfBlockTableRecord Joined(DxfItem record, DxfFileBlock block)
        {
            _block = block;
            DxfItem item = record ?? block?.Head;

            if (item != null && Item == null)
            {
                Bind(item);
            }

            return this;
        }

        /// <summary>Writes the block made in code into a file, and stands for its records from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            if ((_units != DxfUnits.Undefined || !_explodable || _scaling != DxfBlockScaling.Any) && !file.Supports(1021))
            {
                throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no units, explodability or scaling of a block, which came with R2007 (AC1021).", "value");
            }

            if (_comments.Length > 0 && !file.Supports(1015))
            {
                throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no descriptions of blocks, which came with R2000 (AC1015).", "value");
            }

            DxfFileBlock block = file.AddBlock(Name, _origin, (int)_units, _explodable, _scaling == DxfBlockScaling.Uniform, _comments, out DxfItem record);
            _file = file;
            _block = block;
            _space = block.Items;
            _space.Record = this;
            Bind(record ?? block.Head);
        }

        private GeoPoint3 PointOf(DxfItem item, int code)
        {
            bool any = false;
            double[] values = new double[3];

            for (int i = 0; i < 3; i++)
            {
                if (Numbers.TryRead(_file.ValueOf(item, code + 10 * i), out double value) && Guard.IsFinite(value))
                {
                    values[i] = value;
                    any = true;
                }
            }

            return any ? new GeoPoint3(values[0], values[1], values[2]) : default(GeoPoint3);
        }

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }

    internal static class Enumerables
    {
        /// <summary>An enumerator over a list taken as it stands.</summary>
        internal static IEnumerator<T> AsEnumerable<T>(this T[] items) => ((IEnumerable<T>)items).GetEnumerator();
    }
}
