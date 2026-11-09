using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The block table of a DXF file, as AutoCAD's <c>BlockTable</c>: model space, paper space and every block, each a
    /// <see cref="DxfBlockTableRecord"/>, in the order of the file, found by name and added to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>db.BlockTable.Add(new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(1, 2, 0) })</c> adds the block BOLT
    /// drawn about (1, 2, 0), empty until entities are appended to it. In a file of R13 or later the table is its BLOCK_RECORD
    /// table, each record with its BLOCK; in one of R12 it is the blocks of the BLOCKS section, model space and paper space
    /// among them where the file has them as blocks and first where it does not.
    /// </para>
    /// <para>
    /// A block named <c>*D</c>, <c>*U</c> or <c>*T</c> is added as AutoCAD's <c>BlockTable.Add</c> adds one so named: an anonymous block
    /// (<see cref="DxfBlockTableRecord.IsAnonymous"/>, flag 1 of its BLOCK's group 70), named by the table with the first
    /// number from 1 up that no block of the drawing and no INSERT, dimension or table names. A drawing with no dimension
    /// given <c>var picture = new DxfBlockTableRecord { Name = "*D" }</c> names it <c>*D1</c>, the next <c>*D2</c>; one whose
    /// dimensions ezdxf drew as <c>*D1</c> to <c>*D11</c> names it <c>*D12</c>. Its lines and texts are appended to it as to
    /// any block, and it is the <see cref="DxfDimension.DimBlock"/> of a dimension made in code, so that the dimension is drawn
    /// before AutoCAD draws it anew.
    /// </para>
    /// </remarks>
    public sealed class DxfBlockTable : DxfSymbolTable, IEnumerable<DxfBlockTableRecord>
    {
        private readonly DxfDatabase _database;
        private List<DxfBlockTableRecord> _records;

        // The first block of each name in the order of the table, letters of either case the same: made when a block is first
        // looked up by name, and again after one is renamed, or erased where another has its name, so that a lookup does not
        // read every name of the table.
        private Dictionary<string, DxfBlockTableRecord> _named;

        // The names more than one block of the table has, letters of either case the same, found with the blocks by name: so that
        // a block erased that alone had its name is taken out of them, not all of them made again.
        private HashSet<string> _twice;

        // The blocks by the handle of their record, letters of either case the same: made when a block is first looked up by
        // handle, and again after a block is added or erased.
        private Dictionary<string, DxfBlockTableRecord> _byHandle;

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

            if (DxfBlockTableRecord.IsPaperName(key.Trim()))
            {
                return _database.PaperSpace;
            }

            return Named().TryGetValue(key.Trim(), out DxfBlockTableRecord block) ? block : null;
        }

        /// <summary>The first block of each name in the order of the table, as the table's own <c>Find</c> finds one by reading them in turn.</summary>
        private Dictionary<string, DxfBlockTableRecord> Named()
        {
            Dictionary<string, DxfBlockTableRecord> named = Volatile.Read(ref _named);

            if (named == null)
            {
                // Filled whole before it is kept, so that a lookup of another thread never meets it half filled.
                named = new Dictionary<string, DxfBlockTableRecord>(StringComparer.OrdinalIgnoreCase);
                var twice = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfBlockTableRecord record in Read())
                {
                    Index(named, twice, record);
                }

                Volatile.Write(ref _twice, twice);
                Volatile.Write(ref _named, named);
            }

            return named;
        }

        /// <summary>
        /// The block whose record has a handle, the first of the table where two have it; null where none has. The blocks are
        /// read by handle once, as by name, so that the dynamic block each of 4 000 INSERTs copied one by one stands for is found
        /// without reading every record of the table each time.
        /// </summary>
        internal DxfBlockTableRecord ByHandle(string handle)
        {
            Dictionary<string, DxfBlockTableRecord> byHandle = Volatile.Read(ref _byHandle);

            if (byHandle == null)
            {
                // Filled whole before it is kept, as the blocks by name are.
                byHandle = new Dictionary<string, DxfBlockTableRecord>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfBlockTableRecord record in Read())
                {
                    string own = record.Handle;

                    if (own != null && !byHandle.ContainsKey(own))
                    {
                        byHandle.Add(own, record);
                    }
                }

                Volatile.Write(ref _byHandle, byHandle);
            }

            return handle != null && byHandle.TryGetValue(handle.Trim(), out DxfBlockTableRecord block) ? block : null;
        }

        /// <summary>Takes a block last in the order of the table into the blocks by name, where none before it has its name; its name among those of more than one where one has.</summary>
        private static void Index(Dictionary<string, DxfBlockTableRecord> named, HashSet<string> twice, DxfBlockTableRecord record)
        {
            string name = record.Name;

            if (named != null && name != null && !named.ContainsKey(name))
            {
                named.Add(name, record);
            }
            else if (named != null && name != null)
            {
                twice.Add(name);
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfBlockTableRecord block))
            {
                throw new ArgumentException("The block table takes a DxfBlockTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            List<DxfBlockTableRecord> records = Read();

            // A block named *U or *D is anonymous, named by the table as AutoCAD names it: *D1, the first the drawing has not;
            // given its name back where the file refuses it.
            string given = block.Name;
            int flags = block.MadeFlags;

            if (IsAnonymousName(given))
            {
                block.Name = FreeAnonymousName("*" + char.ToUpperInvariant(given[1]).ToString());
                block.MadeFlags = flags | 1;
            }

            try
            {
                block.AddTo(_database.File);
            }
            catch
            {
                block.Name = given;
                block.MadeFlags = flags;
                throw;
            }

            records.Add(block);
            Index(_named, _twice, block);
            _byHandle = null;
        }

        /// <summary>Whether a name is <c>*U</c>, <c>*D</c> or <c>*T</c>, letters of either case, which AutoCAD takes for an anonymous block it names itself.</summary>
        private protected override bool IsAnonymousName(string name)
            => name != null && name.Length == 2 && name[0] == '*' && "UuDdTt".IndexOf(name[1]) >= 0;

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

            var adopted = new DxfBlockTableRecord(_database.File, record, block);
            _records.Add(adopted);
            Index(_named, _twice, adopted);
            _byHandle = null;
        }

        /// <summary>A block erased, no longer of the table.</summary>
        internal void Forget(DxfBlockTableRecord block)
        {
            Read().Remove(block);
            _byHandle = null;
            Dictionary<string, DxfBlockTableRecord> named = _named;
            HashSet<string> twice = _twice;
            string name = block.Name;

            // A block that alone had its name goes out of the blocks by name, as they would be made again without it; where
            // another has its name too, they are made again when next looked up.
            if (named != null && twice != null && name != null && !twice.Contains(name) && named.TryGetValue(name, out DxfBlockTableRecord first) && ReferenceEquals(first, block))
            {
                named.Remove(name);
            }
            else
            {
                _named = null;
            }
        }

        /// <summary>A block renamed: the blocks by name are made again when next looked up.</summary>
        internal void Renamed() => _named = null;

        /// <summary>Adds a block made in code under a name AutoCAD gives itself, as <c>*U4</c>, which <see cref="DxfSymbolTable.Add"/> refuses.</summary>
        internal void AddAnonymous(DxfBlockTableRecord block) => AddRecord(block);

        /// <summary>
        /// The first name of a letter of AutoCAD's anonymous blocks and a number, from 1 up, the table has no block of and no
        /// INSERT of the drawing gives: <c>*U1</c>, <c>*U2</c> and on. An INSERT of *U1, a block the drawing has not, would place
        /// a new *U1, which may insert that INSERT's block.
        /// </summary>
        internal string FreeAnonymousName(string prefix)
        {
            HashSet<string> now = null;

            for (int n = 1; ; n++)
            {
                string name = prefix + n.ToString(CultureInfo.InvariantCulture);

                if (!Has(name) && !_database.File.InsertNames(name, ref now))
                {
                    return name;
                }
            }
        }

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

        // The line breaks a label or a description of a dynamic parameter may not hold, which would end its group.
        private static readonly char[] LineBreaks = { '\r', '\n' };

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
        /// <c>$DIMBLK2</c>, <c>$DIMLDRBLK</c>) that name it, and the arrows the overrides of a dimension of R12 name by name
        /// (DIMBLK, DIMBLK1 and DIMBLK2, <c>1070 5</c>, <c>6</c> or <c>7</c> then <c>1000</c> the name, in ACAD's DSTYLE);
        /// every other record keeping its bytes. A drawing of R2000 or later names an override's arrow by its handle
        /// (<c>1005</c>), which renaming leaves as it is. The name it has changes nothing.
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

        /// <summary>
        /// Gets whether the block is a dynamic block, as AutoCAD's <c>IsDynamicBlock</c>: its extension dictionary holds the
        /// evaluation graph of its parameters and actions (the entry ACAD_ENHANCEDBLOCK). The extended data
        /// <c>AcDbDynamicBlockGUID</c> does not make it one: AutoCAD 2021 read the Bore, PVI, Station and Valve blocks of its
        /// Dynamic Blocks samples, which keep the GUID over an empty extension dictionary, as not dynamic, and a block whose GUID
        /// was taken out and its graph kept as dynamic (plugin D26b).
        /// </summary>
        public bool IsDynamicBlock => Item != null && Item.Type == "BLOCK_RECORD" && HasEvaluationGraph;

        /// <summary>Whether the block's extension dictionary has the entry ACAD_ENHANCEDBLOCK, its evaluation graph.</summary>
        private bool HasEvaluationGraph => ExtensionDictionary?.Contains("ACAD_ENHANCEDBLOCK") == true;

        /// <summary>
        /// Whether the block keeps what a dynamic block is written with, its extended data <c>AcDbDynamicBlockGUID</c> or its
        /// evaluation graph: a copy brings that with it, whether or not it makes the block dynamic, as a copy keeps any data.
        /// </summary>
        internal bool KeepsDynamicData => Item != null && Item.Type == "BLOCK_RECORD" && (GetXDataForApplication("AcDbDynamicBlockGUID") != null || HasEvaluationGraph);

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
        /// Makes the block dynamic, where it is not, and gives it a linear parameter: a distance from a base point to an end
        /// point that each INSERT of the block shows as the dynamic property of its name, AutoCAD's Linear parameter of two grips
        /// as its Block Editor makes one. Tie the parameter to the entities it moves with
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/>,
        /// <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/> and <see cref="AddScaleAction"/>.
        /// </summary>
        /// <param name="name">The label of the parameter, the name of the property, as Width.</param>
        /// <param name="description">The description of the property; null or empty for none.</param>
        /// <param name="basePoint">The point the distance is measured from, in the block's coordinates.</param>
        /// <param name="endPoint">The point the distance is measured to, which the property moves.</param>
        /// <param name="labelOffset">How far the label stands off the line, to its left looking from the base point to the end
        /// point: -20 puts the label of a Width along x 20 below it.</param>
        /// <param name="valueSet">The values the distance may take (<see cref="DxfBlockParameterValueSet.Increments"/>,
        /// <see cref="DxfBlockParameterValueSet.List"/>); null for any.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// A rectangle 100 by 50 from the origin given <c>AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0),
        /// new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10))</c> and a stretch of its right edge
        /// is the block AutoCAD 2021 read as dynamic (plugin v17): every INSERT of it shows Width 100, and a Width set to 200
        /// moves the right edge to x = 200; 75 goes to 70, 77 to 80 and 400 to 300 by the value set.
        /// </para>
        /// <para>
        /// The first parameter makes the block dynamic, as AutoCAD's Block Editor saves it: its BLOCK_RECORD is given the
        /// extended data AcDbDynamicBlockTrueName (its name), AcDbDynamicBlockGUID (a new GUID) and AcDbBlockRepETag, each of its
        /// entities a tag of AcDbBlockRepETag, and its extension dictionary the evaluation graph (ACAD_ENHANCEDBLOCK) and the
        /// purge preventer; the classes of the objects are listed in the CLASSES section. Each parameter is written as AutoCAD
        /// writes one, with its end grip and base grip, each with its two location components: seven nodes of the graph and six
        /// edges. Its element is named Linear, the next Linear1 and on, as AutoCAD names them. The block may be given several,
        /// each a property of its INSERTs in the order they were added.
        /// </para>
        /// <para>
        /// An INSERT of the block shows the block's own distance until its property is set, by AutoCAD or by
        /// <see cref="DxfDynamicBlockReferenceProperty.Value"/>, which works the block out into an anonymous copy the INSERT
        /// places; <see cref="DxfBlockReference.DynamicBlockReferencePropertyCollection"/> reads either. An entity appended
        /// after the last parameter or action has no tag, which AutoCAD 2021 did not need (plugin v17, 24a-c).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name or a point is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break,
        /// the base point and the end point are within the point tolerance of <see cref="Tolerance.Default"/>, or the value set
        /// does not hold the distance between them, the block's own value.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is in no file, is erased, is model space, paper
        /// space or the block of a layout, is anonymous or of an external reference, or its drawing is older than R2010
        /// (AC1024), the oldest AutoCAD was seen to read such a block of.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, which is written alone in its
        /// block (<see cref="AddFlipParameter"/>). Nothing is written then.</exception>
        public DxfBlockLinearParameter AddLinearParameter(string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint, double labelOffset, DxfBlockParameterValueSet valueSet)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (basePoint == null)
            {
                throw new ArgumentNullException(nameof(basePoint));
            }

            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            description = description ?? string.Empty;

            if (name.Trim().Length == 0 || name.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("A parameter is labelled by a name of one line at least one letter long.", nameof(name));
            }

            if (description.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("The description of a parameter is of one line.", nameof(description));
            }

            Guard.Finite(labelOffset, nameof(labelOffset), "The offset of a label is a finite number.");
            double distance = basePoint.GetVectorTo(endPoint).Length;

            if (!(distance > Tolerance.Default.EqualPoint))
            {
                throw new ArgumentException("The end point of a linear parameter is apart from its base point, beyond the point tolerance.", nameof(endPoint));
            }

            if (valueSet != null && !valueSet.Holds(distance))
            {
                throw new ArgumentException("The value set " + (valueSet.IsList ? "lists" : "steps through") + " no distance of "
                    + distance.ToString("R", CultureInfo.InvariantCulture) + ", the block's own between the base point and the end point.", nameof(valueSet));
            }

            Dynamic("be given a parameter");
            Alone(false);
            return DxfDynamicBlockWriter.AddLinear(this, name.Trim(), description, basePoint, endPoint, labelOffset, valueSet);
        }

        /// <summary>
        /// Gives the block a stretch action tied to a linear parameter of it, AutoCAD's Stretch action as its Block Editor makes
        /// one: as the parameter's distance grows by an amount, the points of the entities inside the frame move by that amount
        /// along the line from the parameter's base point to its end point and the points outside stay, so an entity across the
        /// frame stretches and one inside it moves whole.
        /// </summary>
        /// <param name="parameter">The parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, each with one point in the frame at least: a LINE's
        /// (<see cref="DxfLine"/>) start or end, a LWPOLYLINE's (<see cref="DxfPolyline"/>) vertex, or the one point of the other
        /// kinds the remarks list.</param>
        /// <remarks>
        /// <para>
        /// The rectangle 100 by 50 of the lines (100, 0)-(100, 50), (0, 0)-(100, 0), (0, 50)-(100, 50) and (0, 50)-(0, 0), its
        /// Width from (0, 0, 0) to (100, 0, 0), given the frame from (90, -10) to (110, 60) and the first three lines, holds the
        /// right edge whole and the right ends of the bottom and the top: AutoCAD 2021 set Width to 200 and moved them to
        /// x = 200, the left edge staying at x = 0 (plugin v17). A point is in the frame within the point tolerance of
        /// <see cref="Tolerance.Default"/>, on its border among them. The action is written as AutoCAD writes one: its frame
        /// as two corners, the least first, each entity with the indices of its points in the frame (0 a line's start, 1 its
        /// end, a polyline's vertex by its index), and the edge from the parameter in the evaluation graph; its element is
        /// named Stretch, the next Stretch1 and on. A parameter may be followed by several actions.
        /// </para>
        /// <para>
        /// A polyline's vertices are taken in the block's coordinates (<see cref="DxfPolyline.GetPoint3dAt"/>), their x and y.
        /// An entity of one point moves whole where its point is in the frame, as AutoCAD's STRETCH moves it: a circle
        /// (<see cref="DxfCircle"/>) by its centre, a text (<see cref="DxfDBText"/>) by the point it is held by (its position
        /// where it stands left on its baseline, else its alignment point), a multiline text (<see cref="DxfMText"/>) by its
        /// location, an INSERT (<see cref="DxfBlockReference"/>) by its insertion point; each is written as its point 0, as
        /// AutoCAD's files write the one point of a text. An arc (<see cref="DxfArc"/>) is numbered by its ends, 0 its start
        /// and 1 its end as AutoCAD numbers them (plugin v20, 24d-k): with both in the frame it moves whole; with one, that
        /// end moves and the other stays, the arc keeping the height of its middle off its chord, as AutoCAD 2021 kept 2.929
        /// for the arc of the radius 10 from (100, 25) to (90, 35) whose start went to (150, 25) and then to (200, 25), its
        /// radius becoming 159.37 and then 522.13. An arc held by one end lies flat in the block, its normal along z. A text
        /// aligned or fitted between two points and an INSERT of rows and columns are refused. An attribute definition
        /// (<see cref="DxfAttributeDefinition"/>) locked in the block (<see cref="DxfAttributeDefinition.LockPositionInBlock"/>)
        /// is held by its point as a text is, and each INSERT's ATTRIB of its tag moves with it: MARK at (100, 10) stands at
        /// (150, 10) when Width is set from 100 to 150, as AutoCAD 2021 moved it (check D30); AutoCAD moved one not locked in
        /// the block but left its ATTRIB, and such a one is refused, as AutoCAD's Block Editor refuses it. An entity given twice
        /// is taken once.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the parameter is of another block or no longer in its graph, the
        /// frame is no wider or no higher than the point tolerance, there are no entities, or an entity is not of this block,
        /// is erased, is of a kind not taken, is an attribute definition not locked in the block, has no point in the frame,
        /// or is an arc with one end only in it whose normal is not along z. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip action, or an entity is also copied by an
        /// array action or turned by a polar stretch, or turned or scaled by a rotate or a scale action: AutoCAD 2021 worked a flip with a stretch out from
        /// each INSERT's last state and did not flip an INSERT of such a block itself (plugins v20 and v20b), and stretched a
        /// line it turned or scaled from where its last set left it (plugin v21, 24e-g). Nothing is written then.</exception>
        public void AddStretchAction(DxfBlockLinearParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities)
            => AddStretchAction(parameter, frameCorner, oppositeCorner, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a stretch action tied to a linear parameter of it, as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> does, of a
        /// distance multiplier and an angle offset, AutoCAD's Distance Multiplier and Angle Offset of the action: the points in
        /// the frame move by the change of the parameter's end point times the multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, as the overload of no multiplier takes them.</param>
        /// <param name="distanceMultiplier">What the change of the end point is multiplied by, a finite number: 0.5 moves the
        /// points in the frame half as far as the end point; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number: Math.PI / 2
        /// turns a move along x to one along y; nought keeps it.</param>
        /// <remarks>
        /// The rectangle 100 by 50 whose right edge is stretched by a Width from (0, 0, 0) to (100, 0, 0) of the multiplier 0.5
        /// has its right edge at x = 150 when Width is set to 200. The multiplier and the offset are written as AutoCAD writes
        /// them (140 and 141 of the action); 1 and nought, the values of the overload of none, write the action that overload
        /// writes. An INSERT whose value is set keeps the offset in the record of the action in its cache (40), as AutoCAD 2021
        /// kept it for a move; it moved along x, unturned, a circle of the offset of a quarter turn on an INSERT whose record
        /// held nought (plugin v21, 24e-b).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        public void AddStretchAction(DxfBlockLinearParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Stretch(parameter.Block, parameter.Node, parameter.Name, frameCorner, oppositeCorner, entities, distanceMultiplier, angleOffset);
        }

        /// <summary>
        /// Gives the block a stretch action tied to an XY parameter of it, AutoCAD's Stretch action of the XY type as its Block
        /// Editor makes one: as the parameter's end point moves, by its distance along x and along y, the points of the entities
        /// inside the frame move with it both ways and the points outside stay.
        /// </summary>
        /// <param name="parameter">The XY parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, of the kinds and with the points that
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> takes.</param>
        /// <remarks>
        /// A rectangle 100 by 50 from the origin given an XY parameter from (0, 0, 0) to (100, 50, 0) and the frame from (90, 40)
        /// to (110, 60) over its top right corner holds the corner: Width set to 160 and Depth to 80 move it to (160, 80), the
        /// right edge and the top following it and the bottom left corner staying. The action is written as the stretch of a
        /// linear parameter is, its connections EndXDelta and EndYDelta of the XY parameter, of the XY type (280 of nought).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        public void AddStretchAction(DxfBlockXYParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities)
            => AddStretchAction(parameter, frameCorner, oppositeCorner, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a stretch action tied to an XY parameter of it, as
        /// <see cref="AddStretchAction(DxfBlockXYParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> does, of a distance
        /// multiplier and an angle offset: the points in the frame move by the move of the parameter's end point times the
        /// multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The XY parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, as the overload of no multiplier takes them.</param>
        /// <param name="distanceMultiplier">What the move of the end point is multiplied by, a finite number; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number; nought keeps it.</param>
        /// <remarks>
        /// The top right corner (100, 50) of a rectangle stretched by an XY parameter from (0, 0, 0) to (100, 50, 0) of the
        /// multiplier 2 goes to (220, 110) when Width is set to 160 and Depth to 80.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        public void AddStretchAction(DxfBlockXYParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Stretch(parameter.Block, parameter.Node, parameter.XName, frameCorner, oppositeCorner, entities, distanceMultiplier, angleOffset);
        }

        /// <summary>
        /// Gives the block a stretch action tied to a polar parameter of it, AutoCAD's Stretch action as its Block Editor makes
        /// one on a polar parameter: as the parameter's end point moves, by its distance and its angle, the points of the
        /// entities inside the frame move with it, along x and along y, unturned, and the points outside stay.
        /// </summary>
        /// <param name="parameter">The polar parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, of the kinds and with the points that
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> takes.</param>
        /// <remarks>
        /// A rectangle 100 by 50 whose right edge a polar parameter from (0, 0, 0) to (100, 0, 0) stretches by the frame from (90,
        /// -10) to (110, 60) has its right edge from (0, 150) to (0, 200) when Length is set to 150 and Angle to a quarter turn:
        /// the end point moves from (100, 0) to (0, 150), and the edge with it, upright still, as AutoCAD 2021 drew it (plugin
        /// D25b). The action is written as the stretch of a linear parameter is, its connections EndXDelta and EndYDelta of the
        /// polar parameter. Where a polar stretch should turn the entities too, <see cref="AddPolarStretchAction"/> does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        public void AddStretchAction(DxfBlockPolarParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities)
            => AddStretchAction(parameter, frameCorner, oppositeCorner, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a stretch action tied to a polar parameter of it, as
        /// <see cref="AddStretchAction(DxfBlockPolarParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> does, of a
        /// distance multiplier and an angle offset: the points in the frame move by the move of the parameter's end point times
        /// the multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The polar parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, as the overload of no multiplier takes them.</param>
        /// <param name="distanceMultiplier">What the move of the end point is multiplied by, a finite number; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number; nought keeps it.</param>
        /// <remarks>
        /// The right edge (100, 0)-(100, 50) of a rectangle stretched by a polar parameter from (0, 0, 0) to (100, 0, 0) of the
        /// multiplier 0.5 goes to (50, 75)-(50, 125) when Length is set to 150 and Angle to a quarter turn, half the end point's
        /// move from (100, 0) to (0, 150).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> says. Nothing is
        /// written then.</exception>
        public void AddStretchAction(DxfBlockPolarParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Stretch(parameter.Block, parameter.Node, parameter.DistanceName, frameCorner, oppositeCorner, entities, distanceMultiplier, angleOffset);
        }

        /// <summary>Writes a stretch action of a parameter of a block, by its node, once the frame and the entities are checked.</summary>
        private void Stretch(DxfBlockTableRecord block, int node, string name, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities, double multiplier, double angle)
        {
            if (frameCorner == null)
            {
                throw new ArgumentNullException(nameof(frameCorner));
            }

            if (oppositeCorner == null)
            {
                throw new ArgumentNullException(nameof(oppositeCorner));
            }

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            Own(block, name);
            Factors(multiplier, angle);
            double slack = Tolerance.Default.EqualPoint;
            var low = new GeoPoint2(Math.Min(frameCorner.X, oppositeCorner.X), Math.Min(frameCorner.Y, oppositeCorner.Y));
            var high = new GeoPoint2(Math.Max(frameCorner.X, oppositeCorner.X), Math.Max(frameCorner.Y, oppositeCorner.Y));

            if (!(high.X - low.X > slack) || !(high.Y - low.Y > slack))
            {
                throw new ArgumentException("A stretch frame is wider and higher than the point tolerance.", nameof(oppositeCorner));
            }

            Dynamic("be given an action");
            var held = new List<KeyValuePair<DxfEntity, List<int>>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in entities)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entities), "An entity to stretch is null.");
                }

                if (entity.IsErased || entity.Handle == null || !string.Equals(entity.OwnerHandle, Handle, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The entity " + (entity.Handle ?? string.Empty) + " to stretch is not of the block " + Name + ", or is erased.", nameof(entities));
                }

                List<GeoPoint3> points = StretchPoints(entity, true)
                    ?? throw new ArgumentException("A stretch action takes lines, polylines, arcs, circles, texts and attribute definitions held by one point, multiline texts and INSERTs; the entity " + entity.Handle + " is a " + entity.GetType().Name + " of none of those.", nameof(entities));
                Locked(entity, "stretch");
                var inside = new List<int>();

                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i].X >= low.X - slack && points[i].X <= high.X + slack && points[i].Y >= low.Y - slack && points[i].Y <= high.Y + slack)
                    {
                        inside.Add(i);
                    }
                }

                if (inside.Count == 0)
                {
                    throw new ArgumentException("The entity " + entity.Handle + " has no point in the stretch frame, so nothing of it would move.", nameof(entities));
                }

                if (entity is DxfArc arc && inside.Count < points.Count && !Flat(arc.Normal))
                {
                    throw new ArgumentException("The arc " + entity.Handle + " has one end only in the stretch frame and does not lie flat in the block: an arc held by one end has its normal along z.", nameof(entities));
                }

                if (seen.Add(entity.Handle))
                {
                    held.Add(new KeyValuePair<DxfEntity, List<int>>(entity, inside));
                }
            }

            if (held.Count == 0)
            {
                throw new ArgumentException("A stretch action is given one entity at least.", nameof(entities));
            }

            Unflipped(false);
            Unarrayed(held.ConvertAll(h => h.Key), false);
            Unturned(held.ConvertAll(h => h.Key), false);
            Unpolar(held.ConvertAll(h => h.Key), false);
            DxfDynamicBlockWriter.AddStretch(this, node, name, low, high, held, multiplier, angle);
        }

        /// <summary>Whether a normal lies along z, within the vector tolerance of <see cref="Tolerance.Default"/>: the entity lies flat in the block.</summary>
        internal static bool Flat(GeoVector3 normal) => Math.Abs(normal.X) <= Tolerance.Default.EqualVector && Math.Abs(normal.Y) <= Tolerance.Default.EqualVector;

        /// <summary>Refuses a distance multiplier or an angle offset that is not a finite number.</summary>
        private static void Factors(double multiplier, double angle)
        {
            Guard.Finite(multiplier, "distanceMultiplier", "The distance multiplier of an action is a finite number.");
            Guard.Finite(angle, "angleOffset", "The angle offset of an action is a finite number of radians.");
        }

        /// <summary>
        /// The points a stretch action numbers of an entity, in AutoCAD's order where its files show it: a line's start and end,
        /// a polyline's vertices, an arc's two ends, the centre of a circle, the point a text is held by, the location of a
        /// multiline text, the insertion point of an INSERT, and that of an attribute definition, as a text's, where attributes
        /// are taken (a stretch's, not a polar stretch's); null for a kind not taken.
        /// </summary>
        internal static List<GeoPoint3> StretchPoints(DxfEntity entity, bool attributes)
        {
            switch (entity)
            {
                case DxfLine line:
                    return new List<GeoPoint3> { line.StartPoint, line.EndPoint };
                case DxfPolyline polyline:
                    var vertices = new List<GeoPoint3>();

                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        vertices.Add(polyline.GetPoint3dAt(i));
                    }

                    return vertices;
                case DxfArc arc:
                    return new List<GeoPoint3> { arc.StartPoint, arc.EndPoint };
                case DxfCircle circle:
                    return new List<GeoPoint3> { circle.Center };
                case DxfAttributeDefinition _ when !attributes:
                    return null;
                case DxfDBText text:
                    if (text.HorizontalMode == DxfTextHorizontalMode.TextAlign || text.HorizontalMode == DxfTextHorizontalMode.TextFit)
                    {
                        return null;
                    }

                    bool left = text.HorizontalMode == DxfTextHorizontalMode.TextLeft && text.VerticalMode == DxfTextVerticalMode.TextBase;
                    return new List<GeoPoint3> { left ? text.Position : text.AlignmentPoint };
                case DxfMText mtext:
                    return new List<GeoPoint3> { mtext.Location };
                case DxfMInsertBlock _:
                    return null;
                case DxfBlockReference insert:
                    return new List<GeoPoint3> { insert.Position };
                default:
                    return null;
            }
        }

        /// <summary>Refuses a parameter of another block than this one.</summary>
        private void Own(DxfBlockTableRecord block, string name)
        {
            if (block != this)
            {
                throw new ArgumentException("The parameter " + name + " is of the block " + block.Name + ", not of " + Name + ".", "parameter");
            }
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a visibility parameter: named states, each showing some of the
        /// block's entities and hiding the others the states name, which each INSERT of the block shows as the dynamic property of
        /// the parameter's name, AutoCAD's Visibility parameter as its Block Editor makes one. Each INSERT shows the first state
        /// until its property is set (<see cref="DxfDynamicBlockReferenceProperty.Value"/>).
        /// </summary>
        /// <param name="name">The label of the parameter, the name of the property, as Mark.</param>
        /// <param name="description">The description of the property; null or empty for none.</param>
        /// <param name="location">Where the parameter's grip stands, in the block's coordinates.</param>
        /// <param name="states">The states, the first the one the block is kept in; each a name and the entities of this block
        /// shown in it (<see cref="DxfBlockVisibilityState"/>).</param>
        /// <remarks>
        /// <para>
        /// A sign of a frame, a cross and a tick given <c>AddVisibilityParameter("Mark", "What the sign shows", new GeoPoint3(20,
        /// -10, 0), ...)</c> with the states Cross (the cross), Tick (the tick) and Blank (nothing) gives every INSERT the property
        /// Mark of the value Cross, allowed Cross, Tick and Blank. The entities a state names are those the parameter governs:
        /// each is shown in the states that name it and hidden in the others; an entity named in no state, the frame, is not
        /// governed and is shown in every state. The block itself is kept in its first state, as AutoCAD keeps it: each governed
        /// entity not named in the first state is made invisible (<see cref="DxfEntity.Visible"/> false), the tick here.
        /// </para>
        /// <para>
        /// The parameter is written as AutoCAD writes one, as AutoCAD 2021 saved the visibility parameter of ACadSharp's sample:
        /// the parameter (its element named Visibility State) at the location, with the governed entities and each state with
        /// the entities shown in it, its grip and the grip's two location components, four nodes of the evaluation graph and
        /// three edges, the classes listed; the block is made dynamic as <see cref="AddLinearParameter"/> makes it. Linear
        /// parameters and their stretches may come before or after it.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name, the states or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break,
        /// a coordinate of the location is not a finite number, there are no states, two states have one name (letters of either case the same), or an entity of a state is not of
        /// this block or is erased. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block has a visibility parameter already, as AutoCAD's
        /// Block Editor gives a block one, or cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public void AddVisibilityParameter(string name, string description, GeoPoint3 location, IEnumerable<DxfBlockVisibilityState> states)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            description = description ?? string.Empty;

            if (name.Trim().Length == 0 || name.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("A parameter is labelled by a name of one line at least one letter long.", nameof(name));
            }

            if (description.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("The description of a parameter is of one line.", nameof(description));
            }

            if (!location.IsValid)
            {
                throw new ArgumentException("The location of a parameter is of finite numbers: " + location + " is not.", nameof(location));
            }

            var given = new List<DxfBlockVisibilityState>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfBlockVisibilityState state in states)
            {
                if (state == null)
                {
                    throw new ArgumentNullException(nameof(states), "A visibility state is null.");
                }

                if (!names.Add(state.Name))
                {
                    throw new ArgumentException("Two visibility states are named " + state.Name + ", letters of either case the same.", nameof(states));
                }

                foreach (DxfEntity entity in state.Entities)
                {
                    if (entity.IsErased || entity.Handle == null || !string.Equals(entity.OwnerHandle, Handle, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("The entity " + (entity.Handle ?? string.Empty) + " of the state " + state.Name + " is not of the block " + Name + ", or is erased.", nameof(states));
                    }
                }

                given.Add(state);
            }

            if (given.Count == 0)
            {
                throw new ArgumentException("A visibility parameter is given one state at least.", nameof(states));
            }

            Dynamic("be given a parameter");
            Alone(false);
            DxfDynamicBlockWriter.AddVisibility(this, name.Trim(), description, location, given);
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a point parameter: a position that each INSERT of the block
        /// shows as two dynamic properties, the parameter's name with " X" and " Y" after it, AutoCAD's Point parameter as its
        /// Block Editor makes one. Tie the parameter to the entities it moves with
        /// <see cref="AddMoveAction(DxfBlockPointParameter, IEnumerable{DxfEntity})"/>.
        /// </summary>
        /// <param name="name">The label of the parameter, as Tag: the properties are named Tag X and Tag Y.</param>
        /// <param name="description">The description of both properties; null or empty for none.</param>
        /// <param name="location">The position, in the block's coordinates, where the parameter's grip stands.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// A tag circle of the radius 5 at (80, 20) in a rectangle 100 by 50, given <c>AddPointParameter("Tag", "Where the tag
        /// stands", new GeoPoint3(80, 20, 0))</c> and a move action of the circle, gives every INSERT the properties Tag X of 80
        /// and Tag Y of 20; Tag X set to 120 moves the circle's centre to (120, 20).
        /// </para>
        /// <para>
        /// The parameter is written as AutoCAD saved the point parameter of ACadSharp's sample: the parameter (its element named
        /// Point, the next Point1 and on) with its label at the position, its grip (an XY grip) and the grip's two location
        /// components, four nodes of the evaluation graph and three edges; the block is made dynamic as
        /// <see cref="AddLinearParameter"/> makes it.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break,
        /// or a coordinate of the location is not a finite number. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public DxfBlockPointParameter AddPointParameter(string name, string description, GeoPoint3 location)
        {
            description = Labelled(name, description);
            Placed(location, nameof(location));
            Dynamic("be given a parameter");
            Alone(false);
            return DxfDynamicBlockWriter.AddPoint(this, name.Trim(), description, location);
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a rotation parameter: an angle about a base point that each
        /// INSERT of the block shows as the dynamic property of its name, in radians, AutoCAD's Rotation parameter as its Block
        /// Editor makes one. Tie the parameter to the entities it turns with <see cref="AddRotateAction"/>.
        /// </summary>
        /// <param name="name">The label of the parameter, the name of the property, as Angle.</param>
        /// <param name="description">The description of the property; null or empty for none.</param>
        /// <param name="basePoint">The point the angle turns about, in the block's coordinates.</param>
        /// <param name="radius">How far the parameter's grip stands from the base point, above the point tolerance.</param>
        /// <param name="angle">The block's own angle, in radians counter-clockwise from the block's x axis: the grip stands at
        /// the radius in that direction.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// An arrow along x from (50, 25) given <c>AddRotationParameter("Angle", "Turn of the arrow", new GeoPoint3(50, 25, 0),
        /// 30, 0)</c> and a rotate action of the arrow gives every INSERT the property Angle of nought; set to Math.PI / 2, the
        /// arrow turns a quarter turn counter-clockwise about (50, 25) and points along y. A value is taken into a turn, from
        /// nought up to 2 pi, as the property reads it.
        /// </para>
        /// <para>
        /// The parameter is written as AutoCAD saved the rotation parameter of ACadSharp's sample: the base point, the end point
        /// at the radius and the angle, the base angle point at the radius along the block's x axis (from which the angle is
        /// measured), the value set of none (the flags 3, from nought to a whole turn), its element named Rotation; its grip at
        /// the end point with the grip's two location components; four nodes of the evaluation graph and three edges.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break,
        /// or a coordinate of the base point is not a finite number. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a finite number above the point
        /// tolerance of <see cref="Tolerance.Default"/>, or the angle is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public DxfBlockRotationParameter AddRotationParameter(string name, string description, GeoPoint3 basePoint, double radius, double angle)
            => AddRotationParameter(name, description, basePoint, radius, angle, null);

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a rotation parameter, as
        /// <see cref="AddRotationParameter(string, string, GeoPoint3, double, double)"/> does, of the angles it may take, AutoCAD's
        /// Value Set of the parameter: from a minimum to a maximum in steps of an increment, or one of a list, in radians.
        /// </summary>
        /// <param name="name">The label of the parameter, the name of the property, as Angle.</param>
        /// <param name="description">The description of the property; null or empty for none.</param>
        /// <param name="basePoint">The point the angle turns about, in the block's coordinates.</param>
        /// <param name="radius">How far the parameter's grip stands from the base point, above the point tolerance.</param>
        /// <param name="angle">The block's own angle, in radians counter-clockwise from the block's x axis.</param>
        /// <param name="valueSet">The angles the property may take, in radians within a turn
        /// (<see cref="DxfBlockParameterValueSet.Increments"/>, <see cref="DxfBlockParameterValueSet.List"/>); null for any.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// An arrow along x from (50, 25) given <c>AddRotationParameter("Angle", null, new GeoPoint3(50, 25, 0), 30, 0,
        /// DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12))</c> turns in steps of 15 degrees: Angle set to 40
        /// degrees turns it 45, 370 degrees 15 and -10 degrees 345, as AutoCAD 2021 took them (plugin D25). An angle is taken
        /// round the circle: of the steps of 15 from 0 to 90 degrees, 120 goes to 90 and 300 to 0, the bound nearer round the
        /// circle; of the list 0, 90, 180 and 270, 338 goes to 0. A tie goes to the value behind the angle asked, clockwise, as
        /// <see cref="DxfDynamicBlockReferenceProperty.Value"/> says.
        /// </para>
        /// <para>
        /// The set is written as AutoCAD writes the set of a linear parameter, which AutoCAD 2021 read and kept: an increment of
        /// the flags 7, its minimum, maximum and increment (141 to 143); a list of the flags 8, its angles counted by 175 (144).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break,
        /// a coordinate of the base point is not a finite number, or the value set reaches beyond a turn (2 pi) or does not hold
        /// the angle, taken into a turn, within the vector tolerance of <see cref="Tolerance.Default"/>. Nothing is written
        /// then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a finite number above the point
        /// tolerance of <see cref="Tolerance.Default"/>, or the angle is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public DxfBlockRotationParameter AddRotationParameter(string name, string description, GeoPoint3 basePoint, double radius, double angle, DxfBlockParameterValueSet valueSet)
        {
            description = Labelled(name, description);
            Placed(basePoint, nameof(basePoint));

            if (!Guard.IsFinite(radius) || !(radius > Tolerance.Default.EqualPoint))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "The radius of a rotation parameter is a finite number above the point tolerance.");
            }

            Guard.Finite(angle, nameof(angle), "The angle of a rotation parameter is a finite number of radians.");
            Angled(valueSet, angle, nameof(valueSet));
            Dynamic("be given a parameter");
            Alone(false);
            return DxfDynamicBlockWriter.AddRotation(this, name.Trim(), description, basePoint, radius, angle, valueSet);
        }

        /// <summary>
        /// Refuses a value set of an angle that reaches beyond a turn, or does not hold the angle given taken into a turn, within
        /// the vector tolerance of <see cref="Tolerance.Default"/>; a whole turn holds an angle of nought.
        /// </summary>
        private static void Angled(DxfBlockParameterValueSet set, double angle, string name)
        {
            if (set == null)
            {
                return;
            }

            const double turn = 2.0 * Math.PI;
            double slack = Tolerance.Default.EqualVector;
            double most = set.IsList ? set.Values[set.Values.Count - 1] : set.Maximum;

            if (most > turn + slack)
            {
                throw new ArgumentException("The value set of an angle lies within a turn, from nought to 2 pi radians: "
                    + most.ToString("R", CultureInfo.InvariantCulture) + " is beyond it.", name);
            }

            double turned = angle % turn;
            turned = turned < 0.0 ? turned + turn : turned;

            if (!HoldsAngle(set, angle))
            {
                throw new ArgumentException("The value set " + (set.IsList ? "lists" : "steps through") + " no angle of "
                    + turned.ToString("R", CultureInfo.InvariantCulture) + " radians, the block's own.", name);
            }
        }

        /// <summary>Whether a value set of an angle holds an angle, taken into a turn, a whole turn either way, within the vector tolerance of <see cref="Tolerance.Default"/>.</summary>
        private static bool HoldsAngle(DxfBlockParameterValueSet set, double angle)
        {
            const double turn = 2.0 * Math.PI;
            double slack = Tolerance.Default.EqualVector;
            double turned = angle % turn;
            turned = turned < 0.0 ? turned + turn : turned;
            return set.Holds(turned, slack) || set.Holds(turned + turn, slack) || set.Holds(turned - turn, slack);
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a flip parameter: a reflection line that each INSERT of the
        /// block flips about as the dynamic property of its name says, 0 not flipped and 1 flipped, AutoCAD's Flip parameter as
        /// its Block Editor makes one. Tie the parameter to the entities it flips with <see cref="AddFlipAction"/>.
        /// </summary>
        /// <param name="name">The label of the parameter, the name of the property, as Hand.</param>
        /// <param name="description">The description of the property; null or empty for none.</param>
        /// <param name="basePoint">The point the reflection line starts at, in the block's coordinates.</param>
        /// <param name="endPoint">The point the reflection line runs to.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// A door leaf from (0, 0) to (90, 0) given <c>AddFlipParameter("Hand", "Which way the door opens", new GeoPoint3(0, 0,
        /// 0), new GeoPoint3(0, 50, 0))</c> and a flip action of the leaf gives every INSERT the property Hand of 0, allowed 0
        /// and 1; set to 1, the leaf is mirrored about the y axis and runs from (0, 0) to (-90, 0).
        /// </para>
        /// <para>
        /// The parameter is written as AutoCAD saved the flip parameter of ACadSharp's sample: the line from the base point to
        /// the end point, its states labelled Not flipped and Flipped, its label and its grip at the end point, the grip's
        /// three components (the state, the base point's x and y), five nodes of the evaluation graph and four edges; its
        /// element is named Flip.
        /// </para>
        /// <para>
        /// A flip parameter is the only parameter of its block, either way round: AutoCAD 2021 flipped a door of the flip alone
        /// both ways (plugin v20, 24d-g), but set Hand to 1 on the INSERTs of a door that also had a linear parameter of no
        /// action, or of a panel whose stretch held other entities, it read 0 back each time and left the leaf as it was
        /// (plugins v20b and v21, 24d-i and 24e-h).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break, a
        /// coordinate is not a finite number, or the two points are within the point tolerance of <see cref="Tolerance.Default"/>
        /// of each other seen along z. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a parameter already, of any kind, a flip among them.
        /// Nothing is written then.</exception>
        public DxfBlockFlipParameter AddFlipParameter(string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            description = Labelled(name, description);
            Placed(basePoint, nameof(basePoint));
            Placed(endPoint, nameof(endPoint));

            if (!(Math.Sqrt((endPoint.X - basePoint.X) * (endPoint.X - basePoint.X) + (endPoint.Y - basePoint.Y) * (endPoint.Y - basePoint.Y)) > Tolerance.Default.EqualPoint))
            {
                throw new ArgumentException("The end point of a reflection line is apart from its base point along x or y, beyond the point tolerance.", nameof(endPoint));
            }

            Dynamic("be given a parameter");
            Alone(true);
            return DxfDynamicBlockWriter.AddFlip(this, name.Trim(), description, basePoint, endPoint);
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it an XY parameter: the distances along x and along y from a base
        /// point to an end point, which each INSERT of the block shows as two dynamic properties, AutoCAD's XY parameter of four
        /// grips as its Block Editor makes one. Tie the parameter to the entities it moves with
        /// <see cref="AddStretchAction(DxfBlockXYParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> and
        /// <see cref="AddMoveAction(DxfBlockXYParameter, IEnumerable{DxfEntity})"/>.
        /// </summary>
        /// <param name="xName">The label of the distance along x, the name of its property, as Width.</param>
        /// <param name="xDescription">The description of the distance along x; null or empty for none.</param>
        /// <param name="yName">The label of the distance along y, the name of its property, as Depth.</param>
        /// <param name="yDescription">The description of the distance along y; null or empty for none.</param>
        /// <param name="basePoint">The point the distances are measured from, in the block's coordinates.</param>
        /// <param name="endPoint">The point the distances are measured to, which the properties move.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// A rectangle 100 by 50 from the origin given <c>AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new
        /// GeoPoint3(100, 50, 0))</c> gives every INSERT the properties Width of 100 and Depth of 50, in that order, as AutoCAD
        /// gives them. Each distance is measured from the base point the way the end point lies: an end point left of the base
        /// point stays left of it.
        /// </para>
        /// <para>
        /// The parameter is written as AutoCAD saved the XY parameter of ACadSharp's sample: the label of y at 305 and of x at
        /// 306, their descriptions at 307 and 308, the label offsets nought, the value sets of none; its element named XY; its
        /// grips at the end point, the base point, (base x, end y) and (end x, base y), each with its two location components,
        /// thirteen nodes of the evaluation graph and twelve edges.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when a name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a name is empty, a name or a description holds a line break, a
        /// coordinate is not a finite number, or the end point is not apart from the base point along x and along y beyond the
        /// point tolerance of <see cref="Tolerance.Default"/>. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public DxfBlockXYParameter AddXYParameter(string xName, string xDescription, string yName, string yDescription, GeoPoint3 basePoint, GeoPoint3 endPoint)
            => AddXYParameter(xName, xDescription, yName, yDescription, basePoint, endPoint, null, null);

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it an XY parameter, as
        /// <see cref="AddXYParameter(string, string, string, string, GeoPoint3, GeoPoint3)"/> does, of the distances each of its
        /// properties may take, AutoCAD's Horizontal and Vertical Value Sets of the parameter.
        /// </summary>
        /// <param name="xName">The label of the distance along x, the name of its property, as Width.</param>
        /// <param name="xDescription">The description of the distance along x; null or empty for none.</param>
        /// <param name="yName">The label of the distance along y, the name of its property, as Depth.</param>
        /// <param name="yDescription">The description of the distance along y; null or empty for none.</param>
        /// <param name="basePoint">The point the distances are measured from, in the block's coordinates.</param>
        /// <param name="endPoint">The point the distances are measured to, which the properties move.</param>
        /// <param name="xValueSet">The distances along x the property may take; null for any.</param>
        /// <param name="yValueSet">The distances along y the property may take; null for any.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// A plate 100 by 50 from the origin given <c>AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new
        /// GeoPoint3(100, 50, 0), DxfBlockParameterValueSet.Increments(50, 300, 10), DxfBlockParameterValueSet.List(new[] { 50.0,
        /// 80, 120 }))</c> takes a Width of 77 to 80, 75 to 70 and 400 to 300, and a Depth of 90 to 80, 65 to 50 and 100 to 80,
        /// as AutoCAD 2021 took them from every value before (plugins D25 and D25b): a tie of a list goes to the block's own
        /// value where it is one of the two, else to the lesser, as <see cref="DxfDynamicBlockReferenceProperty.Value"/> says. Each set is written as AutoCAD writes the set of a
        /// linear parameter, the set of y first (97, 146 to 149, 176) and then the set of x (96, 142 to 145, 175), as AutoCAD's
        /// samples order them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when a name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a name is empty, a name or a description holds a line break, a
        /// coordinate is not a finite number, the end point is not apart from the base point along x and along y beyond the
        /// point tolerance of <see cref="Tolerance.Default"/>, or a value set does not hold its distance, the block's own.
        /// Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public DxfBlockXYParameter AddXYParameter(string xName, string xDescription, string yName, string yDescription, GeoPoint3 basePoint, GeoPoint3 endPoint,
            DxfBlockParameterValueSet xValueSet, DxfBlockParameterValueSet yValueSet)
        {
            xDescription = Labelled(xName, xDescription);
            yDescription = Labelled(yName, yDescription);
            Placed(basePoint, nameof(basePoint));
            Placed(endPoint, nameof(endPoint));
            double slack = Tolerance.Default.EqualPoint;

            if (!(Math.Abs(endPoint.X - basePoint.X) > slack) || !(Math.Abs(endPoint.Y - basePoint.Y) > slack))
            {
                throw new ArgumentException("The end point of an XY parameter is apart from its base point along x and along y, beyond the point tolerance.", nameof(endPoint));
            }

            Holding(xValueSet, Math.Abs(endPoint.X - basePoint.X), nameof(xValueSet));
            Holding(yValueSet, Math.Abs(endPoint.Y - basePoint.Y), nameof(yValueSet));
            Dynamic("be given a parameter");
            Alone(false);
            return DxfDynamicBlockWriter.AddXY(this, xName.Trim(), xDescription, yName.Trim(), yDescription, basePoint, endPoint, xValueSet, yValueSet);
        }

        /// <summary>Refuses a value set of a distance that does not hold the distance given, the block's own, within the point tolerance of <see cref="Tolerance.Default"/>.</summary>
        private static void Holding(DxfBlockParameterValueSet set, double distance, string name)
        {
            if (set != null && !set.Holds(distance))
            {
                throw new ArgumentException("The value set " + (set.IsList ? "lists" : "steps through") + " no distance of "
                    + distance.ToString("R", CultureInfo.InvariantCulture) + ", the block's own.", name);
            }
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a polar parameter: the distance from a base point to an end
        /// point and the angle of that line from the block's x axis, which each INSERT of the block shows as two dynamic
        /// properties, AutoCAD's Polar parameter of two grips as its Block Editor makes one. Tie the parameter to the entities
        /// it turns and stretches with <see cref="AddPolarStretchAction"/>, and those that follow its end point unturned with
        /// <see cref="AddStretchAction(DxfBlockPolarParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> and
        /// <see cref="AddMoveAction(DxfBlockPolarParameter, IEnumerable{DxfEntity})"/>.
        /// </summary>
        /// <param name="distanceName">The label of the distance, the name of its property, as Length.</param>
        /// <param name="distanceDescription">The description of the distance; null or empty for none.</param>
        /// <param name="angleName">The label of the angle, the name of its property, as Angle.</param>
        /// <param name="angleDescription">The description of the angle; null or empty for none.</param>
        /// <param name="basePoint">The point the distance is measured from and the angle turns about, in the block's coordinates.</param>
        /// <param name="endPoint">The point the distance is measured to, which the properties move, level with the base point along z.</param>
        /// <param name="labelOffset">How far the label stands off the line, to its left looking from the base point to the end
        /// point.</param>
        /// <param name="distanceValueSet">The distances the property may take; null for any.</param>
        /// <param name="angleValueSet">The angles the property may take, in radians within a turn; null for any.</param>
        /// <returns>The parameter, as written.</returns>
        /// <remarks>
        /// <para>
        /// A pointer, a line from (0, 0) to (100, 0) with a circle of the radius 5 at its tip, given <c>AddPolarParameter("Length",
        /// null, "Angle", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null, null)</c> and a polar stretch over
        /// its tip gives every INSERT the properties Length of 100 and Angle of nought, in that order, as AutoCAD gives them;
        /// Length set to 150 and Angle to a quarter turn take the tip to (0, 150).
        /// </para>
        /// <para>
        /// The parameter is written as AutoCAD saved the polar parameter of ACadSharp's sample: the base point and the end point,
        /// its grips and their connections as a linear parameter's, the label of the distance and of the angle at 305 and 307,
        /// their descriptions at 306 and 308, the label offset (140), the distance's value set (96, 141 to 144, 175), of none
        /// the flags 1, and the angle's (97, 145 to 148, 176), of none the flags 3, nought to a whole turn; its element named
        /// Polar; its end grip and base grip, each with its two location components; seven nodes of the evaluation graph and
        /// six edges. AutoCAD 2021 read every INSERT of such a block as dynamic, of the properties Length and Angle, set them,
        /// and kept them through a DXF saved and opened again (plugin D25).
        /// </para>
        /// <para>
        /// The value sets are taken as AutoCAD 2021 took them: of a Length from 50 to 300 by 10, 75 goes to 70 and 400 to 300;
        /// of an Angle in steps of 15 degrees, 40 goes to 45 and -10 to 345; of a Length of 50, 100, 150 or 200 for the block's
        /// own 100, 75 and 125 go to 100 and 175 to 150, from every value before.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when a name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a name is empty, a name or a description holds a line break, a
        /// coordinate is not a finite number, the end point is not apart from the base point along x or y beyond the point
        /// tolerance of <see cref="Tolerance.Default"/> or not level with it along z within it, the distance's value set does not
        /// hold the distance, or the angle's reaches beyond a turn or does not hold the angle, the block's own. Nothing is
        /// written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public DxfBlockPolarParameter AddPolarParameter(string distanceName, string distanceDescription, string angleName, string angleDescription, GeoPoint3 basePoint, GeoPoint3 endPoint,
            double labelOffset, DxfBlockParameterValueSet distanceValueSet, DxfBlockParameterValueSet angleValueSet)
        {
            distanceDescription = Labelled(distanceName, distanceDescription);
            angleDescription = Labelled(angleName, angleDescription);
            Placed(basePoint, nameof(basePoint));
            Placed(endPoint, nameof(endPoint));
            Guard.Finite(labelOffset, nameof(labelOffset), "The offset of a label is a finite number.");
            double slack = Tolerance.Default.EqualPoint;
            double dx = endPoint.X - basePoint.X, dy = endPoint.Y - basePoint.Y;

            if (!(Math.Sqrt(dx * dx + dy * dy) > slack) || !(Math.Abs(endPoint.Z - basePoint.Z) <= slack))
            {
                throw new ArgumentException("The end point of a polar parameter is apart from its base point along x or y, beyond the point tolerance, and level with it along z.", nameof(endPoint));
            }

            Holding(distanceValueSet, basePoint.GetVectorTo(endPoint).Length, nameof(distanceValueSet));
            Angled(angleValueSet, Math.Atan2(dy, dx), nameof(angleValueSet));
            Dynamic("be given a parameter");
            Alone(false);
            return DxfDynamicBlockWriter.AddPolar(this, distanceName.Trim(), distanceDescription, angleName.Trim(), angleDescription, basePoint, endPoint, labelOffset, distanceValueSet, angleValueSet);
        }

        /// <summary>
        /// Gives the block a polar stretch action tied to a polar parameter of it, AutoCAD's Polar Stretch action as its Block
        /// Editor makes one: as the parameter's angle turns and its distance grows, the entities turn about the parameter's base
        /// point by the change of the angle, and the points of the entities inside the frame move besides along the new line by
        /// the change of the distance, so an entity across the frame stretches as it turns; the entities to turn only turn whole
        /// about the base point.
        /// </summary>
        /// <param name="parameter">The polar parameter of this block the action follows, by its end point.</param>
        /// <param name="frameCorner">A corner of the stretch frame, a rectangle along the block's x and y.</param>
        /// <param name="oppositeCorner">The corner across from it.</param>
        /// <param name="entities">The entities of the block to stretch, each with one point in the frame at least: a LINE's
        /// start or end, a LWPOLYLINE's vertex, an arc's two ends, or the one point of a circle, a text or an attribute
        /// definition held by one point, a multiline text or an INSERT, as
        /// <see cref="AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, IEnumerable{DxfEntity})"/> takes them.</param>
        /// <param name="rotateOnly">The entities of the block to turn only, AutoCAD's Objects to Rotate Only; none for none.</param>
        /// <remarks>
        /// <para>
        /// The pointer from (0, 0) to (100, 0) of a Length and an Angle from (0, 0, 0) to (100, 0, 0), given the frame from (90,
        /// -10) to (110, 10), the line, the circle of the radius 5 at its tip and a polyline from (50, -5) to (100, -5), and a tick
        /// from (20, 0) to (20, 10) to turn only, is drawn as AutoCAD 2021 drew it (plugin D25) when Length is set to 150 and
        /// Angle to a quarter turn: the line's start stays at the base point and its end goes to (0, 150), the circle with it,
        /// the polyline's end held in the frame turns and moves to (5, 150) while its start, outside it, turns alone to (5, 50),
        /// and the tick turns alone to (0, 20)-(-10, 20). A circle, a text, a multiline text or an INSERT held by its point, and
        /// an arc held by both ends, turn and move whole, a text turned with the line. AutoCAD 2021 worked each set out from the
        /// block's own values, not from the last set, and an INSERT scaled by 2 in its own units. A point is in the frame within
        /// the point tolerance of <see cref="Tolerance.Default"/>, on its border among them.
        /// </para>
        /// <para>
        /// An attribute definition locked in its block (<see cref="DxfAttributeDefinition.LockPositionInBlock"/>) is taken to
        /// stretch or to turn only, and each INSERT's ATTRIB of it turns and moves with it, as AutoCAD 2021 worked them out
        /// (check D34): a left one at (100, 10) in the frame above goes, at a Distance of 150 and an Angle of 30 degrees, to
        /// (124.904, 83.660), turned by 30 degrees, and one centred on (100, -10) to turn only to (91.603, 41.340).
        /// </para>
        /// <para>
        /// The action is written as AutoCAD saved the polar stretch of ACadSharp's sample and of AutoCAD's own Annotation sample:
        /// the entities it stretches (330), its six connections of the parameter's end point, its frame of two corners (73, 1011
        /// and 1021), the entities it turns only (72, 331), each entity it stretches with the indices of its points in the frame
        /// (74, 332, 75 and 76, numbered as a stretch numbers them), the distance multiplier 1 (141) and the angle offset nought
        /// (140); its element named Polar Stretch, the next Polar Stretch1 and on; the edge from the parameter, of six
        /// connections. An entity to turn only is not among the 330s: named there too, AutoCAD 2021 turned the tick and moved
        /// it by the change of the distance besides, to (70, 0)-(70, 10) for a Length of 150. An entity given twice is taken
        /// once. The INSERT's cache keeps the record of the parameter (its base point, its end point, the normal down and the
        /// base point 10 along x, as AutoCAD keeps it) and of the action (its angle offset), as AutoCAD 2021 wrote them.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, a corner, the entities, the entities to turn only
        /// or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the parameter is of another block or no longer in its graph, the
        /// frame is no wider or no higher than the point tolerance, there are no entities to stretch, an entity is not of this
        /// block, is erased, is of a kind not taken, has no point in the frame, is an arc with one end only in it, or is an
        /// attribute definition not locked in its block, or an entity to turn only is not of this block, is erased, is of a
        /// type the library keeps as it is, is an attribute definition not locked in its block, or is among the entities to
        /// stretch. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when an entity is also worked on by another action of the block, other
        /// than a lookup: no sample of AutoCAD shows the order they would take. Nothing is written then.</exception>
        public void AddPolarStretchAction(DxfBlockPolarParameter parameter, GeoPoint2 frameCorner, GeoPoint2 oppositeCorner, IEnumerable<DxfEntity> entities, IEnumerable<DxfEntity> rotateOnly)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (frameCorner == null)
            {
                throw new ArgumentNullException(nameof(frameCorner));
            }

            if (oppositeCorner == null)
            {
                throw new ArgumentNullException(nameof(oppositeCorner));
            }

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (rotateOnly == null)
            {
                throw new ArgumentNullException(nameof(rotateOnly));
            }

            Own(parameter.Block, parameter.DistanceName);
            double slack = Tolerance.Default.EqualPoint;
            var low = new GeoPoint2(Math.Min(frameCorner.X, oppositeCorner.X), Math.Min(frameCorner.Y, oppositeCorner.Y));
            var high = new GeoPoint2(Math.Max(frameCorner.X, oppositeCorner.X), Math.Max(frameCorner.Y, oppositeCorner.Y));

            if (!(high.X - low.X > slack) || !(high.Y - low.Y > slack))
            {
                throw new ArgumentException("A stretch frame is wider and higher than the point tolerance.", nameof(oppositeCorner));
            }

            Dynamic("be given an action");
            var held = new List<KeyValuePair<DxfEntity, List<int>>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in entities)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entities), "An entity to stretch is null.");
                }

                if (entity.IsErased || entity.Handle == null || !string.Equals(entity.OwnerHandle, Handle, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The entity " + (entity.Handle ?? string.Empty) + " to stretch is not of the block " + Name + ", or is erased.", nameof(entities));
                }

                List<GeoPoint3> points = StretchPoints(entity, true)
                    ?? throw new ArgumentException("A polar stretch takes lines, polylines, arcs, circles, texts and attribute definitions held by one point, multiline texts and INSERTs; the entity " + entity.Handle + " is a " + entity.GetType().Name + " of none of those.", nameof(entities));
                Locked(entity, "stretch");
                var inside = new List<int>();

                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i].X >= low.X - slack && points[i].X <= high.X + slack && points[i].Y >= low.Y - slack && points[i].Y <= high.Y + slack)
                    {
                        inside.Add(i);
                    }
                }

                if (inside.Count == 0)
                {
                    throw new ArgumentException("The entity " + entity.Handle + " has no point in the stretch frame: give an entity to turn only among the entities to turn only.", nameof(entities));
                }

                if (entity is DxfArc && inside.Count < points.Count)
                {
                    throw new ArgumentException("The arc " + entity.Handle + " has one end only in the stretch frame: a polar stretch holds an arc by both ends.", nameof(entities));
                }

                if (seen.Add(entity.Handle))
                {
                    held.Add(new KeyValuePair<DxfEntity, List<int>>(entity, inside));
                }
            }

            if (held.Count == 0)
            {
                throw new ArgumentException("A polar stretch action is given one entity to stretch at least.", nameof(entities));
            }

            var turned = new List<DxfEntity>();

            foreach (DxfEntity entity in rotateOnly)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(rotateOnly), "An entity to turn only is null.");
                }

                if (entity.IsErased || entity.Handle == null || !string.Equals(entity.OwnerHandle, Handle, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The entity " + (entity.Handle ?? string.Empty) + " to turn only is not of the block " + Name + ", or is erased.", nameof(rotateOnly));
                }

                if (entity is DxfUnknownEntity || entity is DxfOle2Frame)
                {
                    throw new ArgumentException("The entity " + entity.Handle + " to turn only is of a type the library keeps as it is, which an action of the library does not take.", nameof(rotateOnly));
                }

                if (entity is DxfAttributeDefinition definition && !definition.LockPositionInBlock)
                {
                    throw new ArgumentException("The attribute definition " + definition.Tag + " (" + entity.Handle + ") to turn only is not locked in its block: lock its position (LockPositionInBlock), as AutoCAD's Block Editor asks.", nameof(rotateOnly));
                }

                if (seen.Contains(entity.Handle) && !turned.Exists(e => e.Handle == entity.Handle))
                {
                    throw new ArgumentException("The entity " + entity.Handle + " is given to stretch and to turn only.", nameof(rotateOnly));
                }

                if (seen.Add(entity.Handle))
                {
                    turned.Add(entity);
                }
            }

            var all = held.ConvertAll(h => h.Key);
            all.AddRange(turned);
            Unflipped(false);
            Unpolar(all, true);
            DxfDynamicBlockWriter.AddPolarStretch(parameter, low, high, held, turned);
        }

        /// <summary>
        /// Refuses an entity that a polar stretch of the block works on and another action works on too, but a lookup, either way
        /// round: the turn and the stretch would follow an order of the actions no sample of AutoCAD shows.
        /// </summary>
        private void Unpolar(IEnumerable<DxfEntity> entities, bool polar)
        {
            HashSet<string> other = DxfDynamicBlockWriter.ActedOn(this, type => polar ? type != "BLOCKLOOKUPACTION" : type == "BLOCKPOLARSTRETCHACTION");

            foreach (DxfEntity entity in entities)
            {
                if (other.Contains(entity.Handle))
                {
                    throw new NotSupportedException("The entity " + entity.Handle + " is " + (polar ? "worked on by another action" : "turned by a polar stretch") + " of the block " + Name
                        + ", and an entity a polar stretch works on is not written with another action on it: no sample of AutoCAD shows the order they would take.");
                }
            }
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a lookup parameter with its lookup action: a table of rows,
        /// each a label and the values it gives other parameters' properties, which each INSERT of the block shows as the
        /// dynamic property of the parameter's name, AutoCAD's Lookup parameter and Lookup action as its Block Editor makes
        /// them. Set to a row's label, the property sets the other properties to the row's values; any of those set, it shows
        /// the label of the row of their values, or Custom where no row holds them.
        /// </summary>
        /// <param name="name">The label of the parameter, the name of the property, as Size.</param>
        /// <param name="description">The description of the property; null or empty for none.</param>
        /// <param name="location">Where the parameter's grip stands, in the block's coordinates.</param>
        /// <param name="inputs">The input columns of the table, each the property of another parameter of this block
        /// (<see cref="DxfBlockLookupInput.Distance(DxfBlockLinearParameter)"/>, <see cref="DxfBlockLookupInput.X(DxfBlockPointParameter)"/>,
        /// <see cref="DxfBlockLookupInput.Y(DxfBlockPointParameter)"/>, <see cref="DxfBlockLookupInput.X(DxfBlockXYParameter)"/>,
        /// <see cref="DxfBlockLookupInput.Y(DxfBlockXYParameter)"/>, <see cref="DxfBlockLookupInput.Distance(DxfBlockPolarParameter)"/>,
        /// <see cref="DxfBlockLookupInput.Angle(DxfBlockPolarParameter)"/>, <see cref="DxfBlockLookupInput.Angle(DxfBlockRotationParameter)"/>),
        /// each once.</param>
        /// <param name="rows">The rows, each a label and a value for each input column (<see cref="DxfBlockLookupRow"/>), the
        /// labels apart, letters of either case the same.</param>
        /// <remarks>
        /// <para>
        /// A panel of a Width and a Height given <c>AddLookupParameter("Size", "Panel size", new GeoPoint3(50, -20, 0), new[] {
        /// DxfBlockLookupInput.Distance(width), DxfBlockLookupInput.Distance(height) }, new[] { new DxfBlockLookupRow("Small",
        /// 100, 50), new DxfBlockLookupRow("Large", 200, 80) })</c> gives every INSERT the property Size, allowed Small and
        /// Large; set to Large, Width goes to 200 and Height to 80, stretching what they stretch; Width set to 150 afterwards,
        /// Size reads Custom. The lookup property is set and read as AutoCAD keeps it: by its label, a string, letters of
        /// either case the same.
        /// </para>
        /// <para>
        /// The parameter and the action are written as AutoCAD saved those of ACadSharp's lookup sample: the parameter (its
        /// element named Lookup, the next Lookup1 and on) with its grip and the grip's two location components; the action
        /// (named after it, Lookup1 for the first) with the table row by row, each input column of its parameter's node, of
        /// reals, labelled Custom where no row matches, and the lookup column of the parameter, of text, looking back
        /// (reverse lookup on), so that the property may be set; five nodes of the evaluation graph and three edges, and a
        /// pair of edges each way between the action and each parameter it reads or sets, as AutoCAD pairs them, each of the
        /// flags 4 (93) as the sample's: AutoCAD 2021 read a block whose pairs were of the flags nought as dynamic, and none of
        /// its INSERTs (plugin v21, 24e-e). An INSERT keeps the label it shows in its cache, as AutoCAD keeps it; one of the
        /// block itself, of no cache, shows the label of the first row of the block's own values, Small for its Width 100 and
        /// Height 50, else Custom, as AutoCAD 2021 read it (plugin v21b) and as
        /// <see cref="DxfBlockReference.DynamicBlockReferencePropertyCollection"/> reads it.
        /// </para>
        /// <para>
        /// A column of an XY parameter's distance along x or along y is written of the connection UpdatedDistanceX or
        /// UpdatedDistanceY, of a polar parameter's distance UpdatedDistance, and of a polar or a rotation parameter's angle
        /// UpdatedAngle, of the units 1 (96) where a distance's are 2, its values in radians. AutoCAD 2021 read and set them as
        /// the library does (plugin D25b): a needle's Way of the rows East 0, North a quarter turn and West pi, set to North,
        /// turned it a quarter turn, and its Angle set to pi read West, to 1 Custom. An angle is taken into a turn and matched
        /// within the vector tolerance, not round the circle: a row of 2 pi sets nought and reads Custom, and one of -pi / 2
        /// sets 3 pi / 2 and reads the row of 3 pi / 2, as AutoCAD 2021 read them.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name, the inputs, the rows or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the name or the description holds a line break, a
        /// coordinate of the location is not a finite number, there are no inputs or no rows, an input is of another block or
        /// given twice, two rows have one label or a row is labelled Custom, a row has not one value for each input, or a
        /// distance is not above the point tolerance of <see cref="Tolerance.Default"/> or not of its parameter's value set,
        /// or an angle is not of its parameter's value set.
        /// Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public void AddLookupParameter(string name, string description, GeoPoint3 location, IEnumerable<DxfBlockLookupInput> inputs, IEnumerable<DxfBlockLookupRow> rows)
        {
            description = Labelled(name, description);
            Placed(location, nameof(location));

            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            var columns = new List<DxfBlockLookupInput>();

            foreach (DxfBlockLookupInput input in inputs)
            {
                if (input == null)
                {
                    throw new ArgumentNullException(nameof(inputs), "An input of a lookup table is null.");
                }

                if (input.Block != this)
                {
                    throw new ArgumentException("The input " + input.Name + " is of the block " + input.Block.Name + ", not of " + Name + ".", nameof(inputs));
                }

                if (columns.Exists(c => c.Node == input.Node && c.Connection == input.Connection))
                {
                    throw new ArgumentException("The input " + input.Name + " is given twice.", nameof(inputs));
                }

                columns.Add(input);
            }

            if (columns.Count == 0)
            {
                throw new ArgumentException("A lookup table has one input column at least.", nameof(inputs));
            }

            var table = new List<DxfBlockLookupRow>();
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DxfDynamicBlockWriter.Unmatched };

            foreach (DxfBlockLookupRow row in rows)
            {
                if (row == null)
                {
                    throw new ArgumentNullException(nameof(rows), "A row of a lookup table is null.");
                }

                if (!labels.Add(row.Label))
                {
                    throw new ArgumentException("The label " + row.Label + " is of two rows, or is the label " + DxfDynamicBlockWriter.Unmatched + " of no row, letters of either case the same.", nameof(rows));
                }

                if (row.Values.Count != columns.Count)
                {
                    throw new ArgumentException("The row " + row.Label + " has " + row.Values.Count.ToString(CultureInfo.InvariantCulture) + " values for " + columns.Count.ToString(CultureInfo.InvariantCulture) + " input columns.", nameof(rows));
                }

                for (int i = 0; i < columns.Count; i++)
                {
                    bool distance = columns[i].Connection == "UpdatedDistance" || columns[i].Connection == "UpdatedDistanceX" || columns[i].Connection == "UpdatedDistanceY";

                    if (distance && (!(row.Values[i] > Tolerance.Default.EqualPoint) || (columns[i].ValueSet != null && !columns[i].ValueSet.Holds(row.Values[i]))))
                    {
                        throw new ArgumentException("The row " + row.Label + " gives " + columns[i].Name + " a distance not above the point tolerance, or not of its value set.", nameof(rows));
                    }

                    if (columns[i].IsAngle && columns[i].ValueSet != null && !HoldsAngle(columns[i].ValueSet, row.Values[i]))
                    {
                        throw new ArgumentException("The row " + row.Label + " gives " + columns[i].Name + " an angle not of its value set.", nameof(rows));
                    }
                }

                table.Add(row);
            }

            if (table.Count == 0)
            {
                throw new ArgumentException("A lookup table has one row at least.", nameof(rows));
            }

            Dynamic("be given a parameter");
            Alone(false);
            DxfDynamicBlockWriter.AddLookup(this, name.Trim(), description, location, columns, table);
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a block properties table: rows of values, each setting several
        /// properties of the block's parameters and attributes at once, which each INSERT of the block shows as the dynamic
        /// property Block Table1, the index of its row, AutoCAD's Block Properties Table as its Block Editor makes one. Set to a
        /// row's index, the property sets each column to the row's value; it reads the index of the first row of the values
        /// the INSERT shows, else -1.
        /// </summary>
        /// <param name="location">Where the table's grip stands, in the block's coordinates.</param>
        /// <param name="columns">The columns of the table, each the property of a parameter of this block
        /// (<see cref="DxfBlockPropertiesTableColumn.Distance(DxfBlockLinearParameter)"/> and the others), its visibility state
        /// (<see cref="DxfBlockPropertiesTableColumn.Visibility"/>) or an attribute definition of it
        /// (<see cref="DxfBlockPropertiesTableColumn.Attribute"/>), each once.</param>
        /// <param name="rows">The rows, each a value for each column (<see cref="DxfBlockPropertiesTableRow"/>): a number for a
        /// parameter's column, the name of a state for the visibility's, a text or null, an empty cell, for an attribute
        /// definition's; no two rows the same.</param>
        /// <remarks>
        /// <para>
        /// A plate of a Width and a Height and the attribute MARK given <c>AddBlockPropertiesTable(new GeoPoint3(0, -20, 0),
        /// new[] { DxfBlockPropertiesTableColumn.Distance(width), DxfBlockPropertiesTableColumn.Distance(height),
        /// DxfBlockPropertiesTableColumn.Attribute(mark) }, new[] { new DxfBlockPropertiesTableRow(100.0, 50.0, "P100"), new
        /// DxfBlockPropertiesTableRow(200.0, 80.0, "P200") })</c> gives every INSERT the property Block Table1, a
        /// <see cref="short"/>; set to 1, Width goes to 200 and Height to 80, stretching what they stretch, and the INSERT's
        /// attribute MARK reads P200. Width set to 150 afterwards, Block Table1 reads -1.
        /// </para>
        /// <para>
        /// The table is written as AutoCAD 2021 keeps one (plugin D26, AutoCAD's Parking sample and a column added through its
        /// <c>BlockPropertiesTable</c>): its element named Block Table and its property Block Table1, its grip and the grip's
        /// two location components, four nodes of the evaluation graph; the edge of the grip to the table and of the table to
        /// each component; a pair of edges each way between the table and each parameter of a column, of the flags 4, the table
        /// first; the graph's extended data AcadBPTGraphNodeId naming the table. Each column names its parameter's node, or the
        /// attribute definition, of no connection; each cell is a real, a text, of the type 40 and 1 as AutoCAD writes them.
        /// The rows need not match, and no column is constant. An empty cell, of an attribute definition's column only, is
        /// written of the type -9999 as AutoCAD writes one; set to its row, the INSERT's attribute reads an empty text, as
        /// AutoCAD 2021 gave MARK, and its audit of the table found no fault, where it refused one of an empty cell of a
        /// parameter's column (eNotApplicable) and took the property Block Table1 away once a row was set (plugin D26b). A
        /// column of the visibility state is written as AutoCAD 2021's <c>AddItem</c> wrote one, its cells texts; a column of an
        /// angle is flagged so (293 of 1), as AutoCAD wrote the one it added of a rotation parameter (plugin D26b).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the columns, the rows or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate of the location is not a finite number, there are no
        /// columns or no rows, a column is of another block or given twice, an attribute definition is erased or constant, a
        /// row has not one value for each column or a value not of its column's kind (an empty cell outside an attribute
        /// definition's column, a text naming no state of the visibility parameter among them), a distance is not above the point
        /// tolerance of <see cref="Tolerance.Default"/> or not of its parameter's value set, an angle is not of its parameter's
        /// value set, or two rows are the same. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block has a block properties table already, as AutoCAD's
        /// Block Editor gives a block one, or cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public void AddBlockPropertiesTable(GeoPoint3 location, IEnumerable<DxfBlockPropertiesTableColumn> columns, IEnumerable<DxfBlockPropertiesTableRow> rows)
        {
            Placed(location, nameof(location));

            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            var given = new List<DxfBlockPropertiesTableColumn>();

            foreach (DxfBlockPropertiesTableColumn column in columns)
            {
                if (column == null)
                {
                    throw new ArgumentNullException(nameof(columns), "A column of a block properties table is null.");
                }

                if (column.Definition != null)
                {
                    DxfAttributeDefinition definition = column.Definition;

                    if (definition.IsErased || definition.Handle == null || !string.Equals(definition.OwnerHandle, Handle, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("The attribute definition " + column.Name + " is not of the block " + Name + ", or is erased.", nameof(columns));
                    }

                    if (definition.Constant)
                    {
                        throw new ArgumentException("The attribute definition " + column.Name + " is constant, and its attribute takes no value from a row.", nameof(columns));
                    }
                }
                else if (column.Block != this)
                {
                    throw new ArgumentException("The column " + column.Name + " is of the block " + column.Block.Name + ", not of " + Name + ".", nameof(columns));
                }

                if (given.Exists(c => string.Equals(c.Handle, column.Handle, StringComparison.OrdinalIgnoreCase) && c.Connection == column.Connection))
                {
                    throw new ArgumentException("The column " + column.Name + " is given twice.", nameof(columns));
                }

                given.Add(column);
            }

            if (given.Count == 0)
            {
                throw new ArgumentException("A block properties table has one column at least.", nameof(columns));
            }

            var table = new List<DxfBlockPropertiesTableRow>();

            foreach (DxfBlockPropertiesTableRow row in rows)
            {
                if (row == null)
                {
                    throw new ArgumentNullException(nameof(rows), "A row of a block properties table is null.");
                }

                string at = "The row " + table.Count.ToString(CultureInfo.InvariantCulture);

                if (row.Values.Count != given.Count)
                {
                    throw new ArgumentException(at + " has " + row.Values.Count.ToString(CultureInfo.InvariantCulture) + " values for " + given.Count.ToString(CultureInfo.InvariantCulture) + " columns.", nameof(rows));
                }

                for (int i = 0; i < given.Count; i++)
                {
                    DxfBlockPropertiesTableColumn column = given[i];
                    object value = row.Values[i];

                    if (column.Definition != null)
                    {
                        if (value is double)
                        {
                            throw new ArgumentException(at + " gives the attribute " + column.Name + " a number, not a text.", nameof(rows));
                        }

                        continue;
                    }

                    if (column.States != null)
                    {
                        if (!(value is string state) || !new List<string>(column.States).Contains(state))
                        {
                            throw new ArgumentException(at + " gives the visibility parameter " + column.Name + " no name of one of its states.", nameof(rows));
                        }

                        continue;
                    }

                    if (value == null)
                    {
                        throw new ArgumentException(at + " leaves " + column.Name + " empty, which AutoCAD's audit of a table refuses.", nameof(rows));
                    }

                    if (!(value is double number))
                    {
                        throw new ArgumentException(at + " gives " + column.Name + " a text, not a number.", nameof(rows));
                    }

                    if (!column.IsAngle && (!(number > Tolerance.Default.EqualPoint) || (column.ValueSet != null && !column.ValueSet.Holds(number))))
                    {
                        throw new ArgumentException(at + " gives " + column.Name + " a distance not above the point tolerance, or not of its value set.", nameof(rows));
                    }

                    if (column.IsAngle && column.ValueSet != null && !HoldsAngle(column.ValueSet, number))
                    {
                        throw new ArgumentException(at + " gives " + column.Name + " an angle not of its value set.", nameof(rows));
                    }
                }

                if (table.Exists(r => Same(r, row, given)))
                {
                    throw new ArgumentException(at + " is the same as a row before it.", nameof(rows));
                }

                table.Add(row);
            }

            if (table.Count == 0)
            {
                throw new ArgumentException("A block properties table has one row at least.", nameof(rows));
            }

            if (DxfDynamicBlockWriter.Has(this, type => type == "BLOCKPROPERTIESTABLE"))
            {
                throw new InvalidOperationException("The block " + Name + " has a block properties table already, and AutoCAD's Block Editor gives a block one.");
            }

            Dynamic("be given a block properties table");
            Alone(false);
            DxfDynamicBlockWriter.AddPropertiesTable(this, location, given, table);
        }

        /// <summary>
        /// Gets the block properties table of the dynamic block, as the drawing keeps it: the name of its property, the name of
        /// each column and the rows (<see cref="DxfBlockPropertiesTable"/>), read anew on each call; null where the block has
        /// none, or one the library cannot read whole (a cell of a kind other than a real or a text).
        /// </summary>
        /// <remarks>
        /// The table <see cref="AddBlockPropertiesTable"/> wrote of the columns Width, Height and the attribute MARK reads back
        /// as Block Table1 of the columns Width, Height and MARK and its rows, P200 the text of the row 1. AutoCAD's Fluorescent
        /// sample reads Block Table1 of Width and Height and nine rows from 500 by 250 to 2000 by 1000.
        /// </remarks>
        public DxfBlockPropertiesTable BlockPropertiesTable => _file == null || Item == null || IsErased ? null : DxfDynamicProperties.Table(this);

        /// <summary>Whether two rows of a block properties table give each column the same: numbers within the tolerance of their kind, texts alike, empty cells alike.</summary>
        private static bool Same(DxfBlockPropertiesTableRow a, DxfBlockPropertiesTableRow b, List<DxfBlockPropertiesTableColumn> columns)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (!(a.Values[i] is double) || !(b.Values[i] is double))
                {
                    if (!string.Equals(a.Values[i] as string, b.Values[i] as string, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
                else if (Math.Abs((double)a.Values[i] - (double)b.Values[i]) > (columns[i].IsAngle ? Tolerance.Default.EqualVector : Tolerance.Default.EqualPoint))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it an alignment parameter, AutoCAD's Alignment parameter as its
        /// Block Editor makes one: a grip at the base point that turns an INSERT dragged by it in AutoCAD to lie along the
        /// entity it is dragged over, along the line from the base point to the end point. It gives no property.
        /// </summary>
        /// <param name="basePoint">The point the INSERT is aligned about, where the grip stands, in the block's coordinates.</param>
        /// <param name="endPoint">The point the alignment line runs to from the base point.</param>
        /// <remarks>
        /// A block of a pipe along x from the origin given <c>AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(50,
        /// 0, 0))</c> has a grip at the origin that lays the pipe along a wall it is dragged over in AutoCAD. The parameter is
        /// written as AutoCAD saved the one of ACadSharp's alignment sample: the line from the base point to the end point,
        /// its element named Alignment, of the alignment type 1 (280) as the sample; its grip at the base point with the
        /// direction to the end point and its two location components; four nodes of the evaluation graph and three edges. A
        /// block has one alignment parameter, as AutoCAD's Block Editor gives it.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, or the two points are within
        /// the point tolerance of <see cref="Tolerance.Default"/> of each other seen along z. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block has an alignment parameter already, or cannot be
        /// dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public void AddAlignmentParameter(GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            Placed(basePoint, nameof(basePoint));
            Placed(endPoint, nameof(endPoint));

            if (!(Math.Sqrt((endPoint.X - basePoint.X) * (endPoint.X - basePoint.X) + (endPoint.Y - basePoint.Y) * (endPoint.Y - basePoint.Y)) > Tolerance.Default.EqualPoint))
            {
                throw new ArgumentException("The end point of an alignment line is apart from its base point along x or y, beyond the point tolerance.", nameof(endPoint));
            }

            Dynamic("be given a parameter");
            Alone(false);
            DxfDynamicBlockWriter.AddAlignment(this, basePoint, endPoint);
        }

        /// <summary>
        /// Makes the block dynamic, where it is not, and gives it a base point parameter, AutoCAD's Base Point parameter as its
        /// Block Editor makes one: a base point for the INSERTs of the block apart from its origin. It gives no property.
        /// </summary>
        /// <param name="location">The base point, in the block's coordinates.</param>
        /// <remarks>
        /// A block given <c>AddBasePointParameter(new GeoPoint3(5, 5, 0))</c> holds a Base Point at (5, 5). The parameter is
        /// written as AutoCAD saved the one of ACadSharp's base point sample: its point, its element named Base Point, and its
        /// own two points (1011, 1012) nought; one node of the evaluation graph and no edge. The library keeps the block's
        /// entities where they are, as AutoCAD 2021 drew them: an INSERT at (300, 0) of a panel 100 by 50 of a base point at
        /// (20, 10) over x 300 to 400 (plugin v21, 24e-f). A block has one base point parameter, as AutoCAD's Block Editor
        /// gives it.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block has a base point parameter already, or cannot be
        /// dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip parameter, as <see cref="AddLinearParameter"/>
        /// says. Nothing is written then.</exception>
        public void AddBasePointParameter(GeoPoint3 location)
        {
            Placed(location, nameof(location));
            Dynamic("be given a parameter");
            Alone(false);
            DxfDynamicBlockWriter.AddBasePoint(this, location);
        }

        /// <summary>
        /// Gives the block a move action tied to a linear parameter of it, AutoCAD's Move action as its Block Editor makes one:
        /// as the parameter's distance grows by an amount, the entities move whole by that amount along the line from the
        /// parameter's base point to its end point, following the end point.
        /// </summary>
        /// <param name="parameter">The parameter of this block the action follows, by its end point.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <remarks>
        /// A rectangle 100 by 50 whose Width runs from (0, 0, 0) to (100, 0, 0), given a move action of a circle at (90, 25), moves
        /// the circle to (190, 25) as Width is set to 200, while a stretch of the right edge takes the edge to x = 200. The
        /// action is written as AutoCAD writes one: the entities it moves, its connections EndXDelta and EndYDelta, the distance
        /// multiplier 1 and the angle offset nought, and the edge from the parameter in the evaluation graph; its element is
        /// named Move, the next Move1 and on. An entity given twice is taken once. An attribute definition locked in the block
        /// (<see cref="DxfAttributeDefinition.LockPositionInBlock"/>) moves with the rest, and each INSERT's ATTRIB of its tag
        /// with it, by the move its INSERT's placing gives: MARK at (100, 10) stands at (150, 10) as Width is set from 100 to
        /// 150, and an INSERT turned a quarter turn at the scale 2, whose Width reads 200, takes its MARK 50 down along y as
        /// Width is set to 150, as AutoCAD 2021 moved them (check D30).
        /// AutoCAD left one not locked where it was, and the library refuses it, as AutoCAD's Block Editor does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the parameter is of another block or no longer in its graph, there
        /// are no entities, or an entity is not of this block, is erased, is an attribute definition not locked in the block, or
        /// is of a type the library keeps as it is (<see cref="DxfUnknownEntity"/>, and an OLE2FRAME, which it does not move yet).
        /// Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip action, or an entity is also copied by an
        /// array action or turned by a polar stretch, or turned or scaled by a rotate or a scale action: AutoCAD 2021 worked a flip with a stretch out from
        /// each INSERT's last state and did not flip an INSERT of such a block itself (plugins v20 and v20b), and stretched a
        /// line it turned or scaled from where its last set left it (plugin v21, 24e-g). Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockLinearParameter parameter, IEnumerable<DxfEntity> entities) => AddMoveAction(parameter, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a move action tied to a linear parameter of it, as
        /// <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/> does, of a distance multiplier and an
        /// angle offset, AutoCAD's Distance Multiplier and Angle Offset of the action: the entities move by the change of the
        /// parameter's end point times the multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The parameter of this block the action follows, by its end point.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <param name="distanceMultiplier">What the change of the end point is multiplied by, a finite number: 0.5 moves the
        /// entities half as far, keeping a mark in the middle of a stretched panel; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number: Math.PI / 2
        /// turns a move along x to one along y; nought keeps it.</param>
        /// <remarks>
        /// A circle at (50, 25), the middle of a rectangle 100 by 50 whose Width runs from (0, 0, 0) to (100, 0, 0), moved of the
        /// multiplier 0.5 stands at (100, 25), the middle still, when Width is set to 200; of the multiplier 1 and the offset
        /// Math.PI / 2 it stands at (50, 125). The multiplier and the offset are written as AutoCAD writes them (140 and 141 of
        /// the action); 1 and nought write the action the overload of none writes.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockLinearParameter parameter, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Factors(distanceMultiplier, angleOffset);
            List<DxfEntity> acted = Acted(parameter.Block, parameter.Name, entities, "move");
            DxfDynamicBlockWriter.AddMove(this, parameter.Node, parameter.Name, "EndXDelta", "EndYDelta", parameter.EndPoint, acted, distanceMultiplier, angleOffset);
        }

        /// <summary>
        /// Gives the block a move action tied to a point parameter of it, AutoCAD's Move action as its Block Editor makes one:
        /// the entities move whole as the parameter's position moves.
        /// </summary>
        /// <param name="parameter">The point parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <remarks>
        /// A tag circle at (80, 20) whose point parameter Tag stands at its centre moves to (120, 35) as Tag X is set to 120 and
        /// Tag Y to 35. The action is written as AutoCAD saved the move of ACadSharp's point sample: the entities it moves, its
        /// connections XDelta and YDelta, the distance multiplier 1 and the angle offset nought; its element named Move.
        /// AutoCAD's sample also lists the parameter's own grip among what it moves; such a block, of AutoCAD's or the
        /// user's, is worked out by <see cref="DxfDynamicBlockReferenceProperty.Value"/> as the grip follows its parameter
        /// anyway, and the library writes the entities only, as AutoCAD 2021 read them (plugin v20).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockPointParameter parameter, IEnumerable<DxfEntity> entities) => AddMoveAction(parameter, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a move action tied to a point parameter of it, as
        /// <see cref="AddMoveAction(DxfBlockPointParameter, IEnumerable{DxfEntity})"/> does, of a distance multiplier and an
        /// angle offset: the entities move by the move of the parameter's position times the multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The point parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <param name="distanceMultiplier">What the move of the position is multiplied by, a finite number; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number; nought keeps it.</param>
        /// <remarks>
        /// A circle at (80, 20) moved of the multiplier 0.5 by a Tag at (80, 20) stands at (100, 20) when Tag X is set to 120.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockPointParameter parameter, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Factors(distanceMultiplier, angleOffset);
            List<DxfEntity> acted = Acted(parameter.Block, parameter.Name, entities, "move");
            DxfDynamicBlockWriter.AddMove(this, parameter.Node, parameter.Name, "XDelta", "YDelta", parameter.Location, acted, distanceMultiplier, angleOffset);
        }

        /// <summary>
        /// Gives the block a move action tied to an XY parameter of it, AutoCAD's Move action of the XY type as its Block Editor
        /// makes one: the entities move whole with the parameter's end point, along x and along y.
        /// </summary>
        /// <param name="parameter">The XY parameter of this block the action follows, by its end point.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <remarks>
        /// A circle at the top right corner (100, 50) of a rectangle whose XY parameter runs from (0, 0, 0) to (100, 50, 0) moves
        /// to (160, 80) as Width is set to 160 and Depth to 80. The action is written as the move of a linear parameter is, its
        /// connections EndXDelta and EndYDelta of the XY parameter, of the XY type (280 of nought).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockXYParameter parameter, IEnumerable<DxfEntity> entities) => AddMoveAction(parameter, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a move action tied to an XY parameter of it, as
        /// <see cref="AddMoveAction(DxfBlockXYParameter, IEnumerable{DxfEntity})"/> does, of a distance multiplier and an angle
        /// offset: the entities move by the move of the parameter's end point times the multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The XY parameter of this block the action follows, by its end point.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <param name="distanceMultiplier">What the move of the end point is multiplied by, a finite number; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number; nought keeps it.</param>
        /// <remarks>
        /// A circle at (50, 25), the middle of a rectangle whose XY parameter runs from (0, 0, 0) to (100, 50, 0), moved of the
        /// multiplier 0.5 stands at (80, 40), the middle still, when Width is set to 160 and Depth to 80.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockXYParameter parameter, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Factors(distanceMultiplier, angleOffset);
            List<DxfEntity> acted = Acted(parameter.Block, parameter.XName, entities, "move");
            DxfDynamicBlockWriter.AddMove(this, parameter.Node, parameter.XName, "EndXDelta", "EndYDelta", parameter.EndPoint, acted, distanceMultiplier, angleOffset);
        }

        /// <summary>
        /// Gives the block a move action tied to a polar parameter of it, AutoCAD's Move action as its Block Editor makes one on
        /// a polar parameter: the entities move whole with the parameter's end point, along x and along y, unturned.
        /// </summary>
        /// <param name="parameter">The polar parameter of this block the action follows, by its end point.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <remarks>
        /// A circle at the end point (100, 0) of a polar parameter from (0, 0, 0) moves to (0, 150) as Length is set to 150 and
        /// Angle to a quarter turn, and a text at (95, 3) by it to (-5, 153), unturned, as AutoCAD 2021 moved them (plugin
        /// D25b). The action is written as the move of a linear parameter is, its connections EndXDelta and EndYDelta of the
        /// polar parameter.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockPolarParameter parameter, IEnumerable<DxfEntity> entities) => AddMoveAction(parameter, entities, 1.0, 0.0);

        /// <summary>
        /// Gives the block a move action tied to a polar parameter of it, as
        /// <see cref="AddMoveAction(DxfBlockPolarParameter, IEnumerable{DxfEntity})"/> does, of a distance multiplier and an
        /// angle offset: the entities move by the move of the parameter's end point times the multiplier, turned by the offset.
        /// </summary>
        /// <param name="parameter">The polar parameter of this block the action follows, by its end point.</param>
        /// <param name="entities">The entities of the block to move.</param>
        /// <param name="distanceMultiplier">What the move of the end point is multiplied by, a finite number; 1 as far.</param>
        /// <param name="angleOffset">The angle the move is turned by, counter-clockwise in radians, a finite number; nought keeps it.</param>
        /// <remarks>
        /// A circle at (50, 0), halfway along a polar parameter from (0, 0, 0) to (100, 0, 0), moved of the multiplier 0.5 stands
        /// at (0, 75), halfway still, when Length is set to 150 and Angle to a quarter turn.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier or the offset is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        public void AddMoveAction(DxfBlockPolarParameter parameter, IEnumerable<DxfEntity> entities, double distanceMultiplier, double angleOffset)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Factors(distanceMultiplier, angleOffset);
            List<DxfEntity> acted = Acted(parameter.Block, parameter.DistanceName, entities, "move");
            DxfDynamicBlockWriter.AddMove(this, parameter.Node, parameter.DistanceName, "EndXDelta", "EndYDelta", parameter.EndPoint, acted, distanceMultiplier, angleOffset);
        }

        /// <summary>
        /// Gives the block a scale action tied to a linear parameter of it, AutoCAD's Scale action as its Block Editor makes one:
        /// the entities are scaled alike in every direction about the parameter's base point, by the parameter's distance over
        /// the block's own.
        /// </summary>
        /// <param name="parameter">The parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to scale.</param>
        /// <remarks>
        /// A circle of the radius 25 at (50, 25) whose block has a Size from (0, 0, 0) to (100, 0, 0) is scaled by 2 about the
        /// origin as Size is set to 200: its centre goes to (100, 50) and its radius to 50. The action is written as AutoCAD
        /// saved the scale of ACadSharp's linear sample: the entities it scales, its base the parameter's base point (dependent,
        /// of no offset), its connections Scale, XScale and YScale; its element named Scale. An entity given twice is taken once;
        /// one a stretch or a move holds is not scaled, either way round. An attribute definition locked in the block
        /// (<see cref="DxfAttributeDefinition.LockPositionInBlock"/>) is scaled too, its height with it, and each INSERT's
        /// ATTRIB of its tag: LEFT at (100, 10), 5 high, stands at (150, 15), 7.5 high, as Size is set from 100 to 150, as
        /// AutoCAD 2021 scaled it (check D30); one not locked is refused, as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/> says.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip action, or an entity is also copied by an
        /// array action or turned by a polar stretch, or held by a stretch or a move: AutoCAD 2021 scaled a line it also stretched from where its last set
        /// left it, to (40, 20)-(180, 20) for a Width of 140 set after 120 where the block's values give (40, 20)-(200, 20)
        /// (plugin v21, 24e-g). Nothing is written then.</exception>
        public void AddScaleAction(DxfBlockLinearParameter parameter, IEnumerable<DxfEntity> entities)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            List<DxfEntity> acted = Acted(parameter.Block, parameter.Name, entities, "scale");
            DxfDynamicBlockWriter.AddScale(parameter, acted);
        }

        /// <summary>
        /// Gives the block a rotate action tied to a rotation parameter of it, AutoCAD's Rotate action as its Block Editor makes
        /// one: the entities turn about the parameter's base point by the parameter's angle less the block's own.
        /// </summary>
        /// <param name="parameter">The rotation parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to turn.</param>
        /// <remarks>
        /// An arrow from (50, 25) to (80, 25) whose Angle turns about (50, 25) from nought runs to (50, 55) as Angle is set to
        /// Math.PI / 2. The action is written as AutoCAD saved the rotate of ACadSharp's rotation sample: the entities it turns,
        /// its base the parameter's base point (dependent, of no offset), its connection AngleDelta; its element named Rotate.
        /// An entity given twice is taken once; one a stretch or a move holds is not turned, either way round. An attribute
        /// definition locked in the block (<see cref="DxfAttributeDefinition.LockPositionInBlock"/>) turns too, and each
        /// INSERT's ATTRIB of its tag: LEFT at (100, 10), turned about the origin by a quarter turn, stands at (-10, 100),
        /// turned a quarter turn, as AutoCAD 2021 turned it (check D30); one not locked is refused, as
        /// <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/> says.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip action, or an entity is also copied by an
        /// array action or turned by a polar stretch, or held by a stretch or a move: AutoCAD 2021 stretched a shaft it had turned a quarter turn along x,
        /// to (0, 155) for a Reach of 150 set after 200, where the block's values give (50, 105) (plugin v21, 24e-g). Nothing is
        /// written then.</exception>
        public void AddRotateAction(DxfBlockRotationParameter parameter, IEnumerable<DxfEntity> entities)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            List<DxfEntity> acted = Acted(parameter.Block, parameter.Name, entities, "turn");
            DxfDynamicBlockWriter.AddRotate(parameter, acted);
        }

        /// <summary>
        /// Gives the block a flip action tied to a flip parameter of it, AutoCAD's Flip action as its Block Editor makes one: the
        /// entities are mirrored about the parameter's reflection line where the parameter's state is 1, flipped.
        /// </summary>
        /// <param name="parameter">The flip parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to flip.</param>
        /// <remarks>
        /// A door leaf from (0, 0) to (90, 0) whose Hand runs along the y axis runs from (0, 0) to (-90, 0) as Hand is set to 1,
        /// and back as it is set to 0. The mirror is taken in the block's plane, about the vertical plane through the line. The
        /// action is written as AutoCAD saved the flip of ACadSharp's sample: the entities it flips, its connections Flip,
        /// UpdatedFlip, UpdatedBase and UpdatedEnd; its element named Flip1 after the parameter's Flip. An entity given twice is
        /// taken once. A block of flip actions has no stretch, move, scale, rotate or array action, and its flip parameter no
        /// other parameter beside it (<see cref="AddFlipParameter"/>): AutoCAD 2021 stretched a flipped panel from where its
        /// last flip left it (plugin v20), and did not flip an INSERT of a block of a flip beside another parameter, reading
        /// Hand back as 0 (plugins v20b and v21, 24d-i and 24e-h), though it flipped one of the block of the flip alone (24d-g).
        /// A locked attribute definition whose words the drawing's <see cref="DxfDatabase.FontSearchPath"/> measures is flipped
        /// as AutoCAD 2021 flips it, kept readable, upright and turned as before, its INSERTs' ATTRIBs moved with it (check F1b):
        /// one aligned right, aligned or fitted on its baseline so that its first point is its second mirrored; one held by the
        /// middle of its words so that its second point is mirrored; any other so that the box of its ink is mirrored, "L1" of
        /// txt.shx 5 high at (20, 60, 0), its ink 6.67 wide, about x = 45 at (63.33, 60, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says, and when an entity is an attribute definition whose words the drawing's fonts do not measure, locked or not.
        /// Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a stretch, polar stretch, move, scale, rotate or array
        /// action. Nothing is written then.</exception>
        public void AddFlipAction(DxfBlockFlipParameter parameter, IEnumerable<DxfEntity> entities)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            List<DxfEntity> acted = Acted(parameter.Block, parameter.Name, entities, "flip");
            DxfDynamicBlockWriter.AddFlipAction(parameter, acted);
        }

        /// <summary>
        /// Gives the block an array action tied to a linear parameter of it, AutoCAD's Array action as its Block Editor makes
        /// one: as the parameter's distance grows, the entities are copied along the line from the parameter's base point to
        /// its end point, a copy at each column spacing, as many in all as the spacing goes whole into the distance.
        /// </summary>
        /// <param name="parameter">The parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to copy; the first copy is each entity itself.</param>
        /// <param name="columnSpacing">The distance from one copy to the next along the parameter, above the point tolerance of
        /// <see cref="Tolerance.Default"/>.</param>
        /// <remarks>
        /// <para>
        /// A post, a circle of the radius 5 at (0, 0), of a fence whose Length runs from (0, 0, 0) to (100, 0, 0), given an
        /// array of the column spacing 60, is one post, as the block draws it; Length set to 250, the INSERT shows four, at
        /// x = 0, 60, 120 and 180, as AutoCAD 2021 drew them, and one post for 119 (plugin v21, 24e-c). The count is the
        /// distance over the spacing, rounded down, one at least, as AutoCAD's save of ACadSharp's XY sample shows too: four
        /// columns of the spacing 25 in 100.59, five rows of 20 in 104.30. A block whose own distance holds the spacing twice
        /// or more draws one copy still, which its INSERTs show until a value is set.
        /// </para>
        /// <para>
        /// The action is written as AutoCAD saved the array of that sample: the entities it copies, its connections Base, End,
        /// UpdatedBase and UpdatedEnd, the row offset (140) nought and the column offset (141) the spacing; its element named
        /// Array; the edge from the parameter of four connections; no record of the INSERT's cache. A linear parameter's
        /// array is written as the XY sample's with no rows, which AutoCAD 2021 worked out as above. Each copy but the entity
        /// itself is tagged as AutoCAD tags the copies of its arrays (AcDbBlockArrayActionTag, 1070 1), without which AutoCAD
        /// 2021 refused to set again an INSERT whose value had been set (eInvalidInput, plugin v21). An entity given twice is
        /// taken once; one an array copies is moved by no other action.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says, and when an entity is an attribute definition, locked or not. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a finite number above the point tolerance.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block has a flip action, or an entity is also moved, stretched,
        /// scaled, turned or copied by another action. Nothing is written then.</exception>
        public void AddArrayAction(DxfBlockLinearParameter parameter, IEnumerable<DxfEntity> entities, double columnSpacing)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Spacing(columnSpacing, nameof(columnSpacing));
            List<DxfEntity> acted = Acted(parameter.Block, parameter.Name, entities, "copy");
            DxfDynamicBlockWriter.AddArray(this, parameter.Node, parameter.Name, parameter.EndPoint, acted, 0.0, columnSpacing);
        }

        /// <summary>
        /// Gives the block an array action tied to an XY parameter of it, AutoCAD's Array action as its Block Editor makes one:
        /// as the parameter's end point moves, the entities are copied in rows and columns from the base point the way the end
        /// point lies, a column at each column spacing along x and a row at each row spacing along y.
        /// </summary>
        /// <param name="parameter">The XY parameter of this block the action follows.</param>
        /// <param name="entities">The entities of the block to copy; the first copy is each entity itself.</param>
        /// <param name="columnSpacing">The distance from one column to the next along x, above the point tolerance of
        /// <see cref="Tolerance.Default"/>.</param>
        /// <param name="rowSpacing">The distance from one row to the next along y, above the point tolerance.</param>
        /// <remarks>
        /// <para>
        /// A bolt, a circle of the radius 5 at (0, 0), of a plate whose XY parameter runs from (0, 0, 0) to (40, 30, 0), given an
        /// array of the column spacing 25 and the row spacing 20, is one bolt; Width set to 100 and Depth to 70 show four
        /// columns of three rows, the bolts at x = 0, 25, 50, 75 and y = 0, 20, 40. Each count is the distance over its spacing,
        /// rounded down, as AutoCAD's save of ACadSharp's XY sample shows: four columns of 25 in 100.59, five rows of 20 in
        /// 104.30, the rows running down from the base point to an end point below it; AutoCAD 2021 drew one column of three
        /// rows for 40 by 70 and two columns of one row for 60 by 30 (plugin v21, 24e-d). Where a count is nought nothing is
        /// copied, the entity standing alone, as AutoCAD 2021 drew the one circle of that sample set to 80 by 5 (24e-s2).
        /// </para>
        /// <para>
        /// The action is written as AutoCAD saved the array of that sample: the entities it copies, its connections Base, End,
        /// UpdatedBase and UpdatedEnd, the row offset (140) and the column offset (141); its element named Array; the edge from
        /// the parameter of four connections; no record of the INSERT's cache. Each copy but the entity itself is tagged as
        /// AutoCAD tags the copies of its arrays, as
        /// <see cref="AddArrayAction(DxfBlockLinearParameter, IEnumerable{DxfEntity}, double)"/> says. An entity given twice is
        /// taken once; one an array copies is moved by no other action.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the parameter, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown as <see cref="AddMoveAction(DxfBlockLinearParameter, IEnumerable{DxfEntity})"/>
        /// says, and when an entity is an attribute definition, locked or not. Nothing is written then.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a spacing is not a finite number above the point tolerance.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block cannot be dynamic, as <see cref="AddLinearParameter"/> says.</exception>
        /// <exception cref="NotSupportedException">Thrown as
        /// <see cref="AddArrayAction(DxfBlockLinearParameter, IEnumerable{DxfEntity}, double)"/> says. Nothing is written then.</exception>
        public void AddArrayAction(DxfBlockXYParameter parameter, IEnumerable<DxfEntity> entities, double columnSpacing, double rowSpacing)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Spacing(columnSpacing, nameof(columnSpacing));
            Spacing(rowSpacing, nameof(rowSpacing));
            List<DxfEntity> acted = Acted(parameter.Block, parameter.XName, entities, "copy");
            DxfDynamicBlockWriter.AddArray(this, parameter.Node, parameter.XName, parameter.EndPoint, acted, rowSpacing, columnSpacing);
        }

        /// <summary>Refuses a spacing of an array that is not a finite number above the point tolerance.</summary>
        private static void Spacing(double spacing, string name)
        {
            if (!Guard.IsFinite(spacing) || !(spacing > Tolerance.Default.EqualPoint))
            {
                throw new ArgumentOutOfRangeException(name, spacing, "The spacing of an array is a finite number above the point tolerance.");
            }
        }

        /// <summary>Refuses a label that is null, empty or of more than one line, and a description of more than one line; gives the description, empty for null.</summary>
        private static string Labelled(string name, string description)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0 || name.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("A parameter is labelled by a name of one line at least one letter long.", nameof(name));
            }

            description = description ?? string.Empty;

            if (description.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("The description of a parameter is of one line.", nameof(description));
            }

            return description;
        }

        /// <summary>Refuses a point not of finite numbers.</summary>
        private static void Placed(GeoPoint3 point, string name)
        {
            if (!point.IsValid)
            {
                throw new ArgumentException("A point of a parameter is of finite numbers: " + point + " is not.", name);
            }
        }

        /// <summary>
        /// The entities an action works on, each once in the order given, once the parameter is found of this block and the block
        /// able to be dynamic: each of this block, not erased, nor of a type kept as it is, and an attribute definition only one
        /// locked in the block, of a move, a scale or a turn.
        /// </summary>
        private List<DxfEntity> Acted(DxfBlockTableRecord block, string name, IEnumerable<DxfEntity> entities, string what)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            Own(block, name);
            Dynamic("be given an action");
            var acted = new List<DxfEntity>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in entities)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entities), "An entity to " + what + " is null.");
                }

                if (entity.IsErased || entity.Handle == null || !string.Equals(entity.OwnerHandle, Handle, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The entity " + (entity.Handle ?? string.Empty) + " to " + what + " is not of the block " + Name + ", or is erased.", nameof(entities));
                }

                // A move, a scale and a rotate take an attribute definition locked in the block, as AutoCAD's Block Editor does; a
                // flip one whose words the drawing's fonts measure, which it keeps readable (check D34).
                if ((entity is DxfAttributeDefinition definition && what != "move" && what != "scale" && what != "turn" && !(what == "flip" && definition.IsMeasured)) || entity is DxfUnknownEntity || entity is DxfOle2Frame)
                {
                    throw new ArgumentException("The entity " + entity.Handle + " to " + what + " is an attribute definition or of a type the library keeps as it is, which an action of the library does not take.", nameof(entities));
                }

                Locked(entity, what);

                if (seen.Add(entity.Handle))
                {
                    acted.Add(entity);
                }
            }

            if (acted.Count == 0)
            {
                throw new ArgumentException("An action is given one entity at least.", nameof(entities));
            }

            Unflipped(what == "flip");
            Unarrayed(acted, what == "copy");
            Unpolar(acted, false);

            if (what == "move" || what == "scale" || what == "turn")
            {
                Unturned(acted, what != "move");
            }

            return acted;
        }

        /// <summary>
        /// Refuses an attribute definition whose position is not locked in the block
        /// (<see cref="DxfAttributeDefinition.LockPositionInBlock"/>): AutoCAD's Block Editor takes into an action only an
        /// attribute locked so, as are all those an action holds in AutoCAD's Dynamic Blocks samples, and AutoCAD 2021 left an unlocked one
        /// of the library's move, scale and rotate where it was, and the ATTRIB of one a stretch moved (check D30).
        /// </summary>
        private static void Locked(DxfEntity entity, string what)
        {
            if (entity is DxfAttributeDefinition definition && !definition.LockPositionInBlock)
            {
                throw new ArgumentException("The attribute definition " + definition.Tag + " (" + entity.Handle + ") to " + what + " is not locked in its block: lock its position (LockPositionInBlock), as AutoCAD's Block Editor asks, for AutoCAD 2021 left an unlocked one where it was.", "entities");
            }
        }

        /// <summary>
        /// Refuses a flip parameter in a block of any other parameter, either way round: AutoCAD 2021 did not flip an INSERT of
        /// a door whose block held a linear parameter of no action beside the flip, reading its Hand back as 0 each time it was
        /// set to 1 (plugin v21, 24e-h), nor an INSERT of a panel whose stretch held other entities (plugins v20b and v21, 24d-i,
        /// and AutoCAD's own save of it), though it flipped a door of the flip alone both ways (plugin v20, 24d-g). It read Hand
        /// back as 0 again on the library's door of a flip beside a Width, stretching a jamb or of no action, once the library
        /// kept an INSERT's history (plugin D30, x1).
        /// </summary>
        private void Alone(bool flip)
        {
            if (DxfDynamicBlockWriter.Has(this, type => flip ? type.EndsWith("PARAMETER", StringComparison.Ordinal) : type == "BLOCKFLIPPARAMETER"))
            {
                throw new NotSupportedException("The block " + Name + " has " + (flip ? "a parameter" : "a flip parameter")
                    + ", and a flip parameter is written alone in its block: AutoCAD 2021 did not flip an INSERT of a block of a flip beside another parameter.");
            }
        }

        /// <summary>
        /// Refuses an entity that a rotate or a scale of the block works on and a stretch or a move holds too, either way round:
        /// AutoCAD 2021 worked such an entity out from where its last set left it, stretching a shaft turned a quarter turn
        /// along x to (0, 155) for a Reach of 150 set after 200, and scaling a line it stretched from its last length (plugin
        /// v21, 24e-g), so no value of such a block tells its shape.
        /// </summary>
        private void Unturned(IEnumerable<DxfEntity> entities, bool turn)
        {
            HashSet<string> other = DxfDynamicBlockWriter.ActedOn(this, type => turn
                ? type == "BLOCKSTRETCHACTION" || type == "BLOCKMOVEACTION"
                : type == "BLOCKROTATEACTION" || type == "BLOCKSCALEACTION");

            foreach (DxfEntity entity in entities)
            {
                if (other.Contains(entity.Handle))
                {
                    throw new NotSupportedException("The entity " + entity.Handle + " is " + (turn ? "stretched or moved" : "turned or scaled") + " by another action of the block " + Name
                        + ", and a rotate or a scale is not written with a stretch or a move of one entity: AutoCAD 2021 worked such an entity out from its last state.");
                }
            }
        }

        /// <summary>
        /// Refuses a flip action in a block of a stretch, move, scale, rotate or array action, either way round: AutoCAD 2021
        /// stretched the edges of a panel flipped about the y axis from where its last flip left them (to x = -250 for a Width
        /// of 150, set after 200; plugin v20, 24d-i), and, the flip and the stretch holding other entities, did not flip an
        /// INSERT of the block itself, reading its Hand back as 0 (plugin v20b), so such a block is not one AutoCAD works out
        /// from its values.
        /// </summary>
        private void Unflipped(bool flip)
        {
            HashSet<string> other = DxfDynamicBlockWriter.ActedOn(this, type => (type == "BLOCKFLIPACTION") != flip && type != "BLOCKLOOKUPACTION");

            if (other.Count > 0)
            {
                throw new NotSupportedException("The block " + Name + " has " + (flip ? "a stretch, move, scale, rotate or array action" : "a flip action")
                    + ", and a flip is not written with the others: AutoCAD 2021 worked such a block out from the last state of each INSERT and did not flip an INSERT of the block itself.");
            }
        }

        /// <summary>
        /// Refuses an entity that an array action of the block copies and another action works on, either way round: the copies
        /// would follow an order of the actions no sample of AutoCAD shows.
        /// </summary>
        private void Unarrayed(IEnumerable<DxfEntity> entities, bool array)
        {
            HashSet<string> arrayed = DxfDynamicBlockWriter.ActedOn(this, type => type == "BLOCKARRAYACTION");
            HashSet<string> other = array ? DxfDynamicBlockWriter.ActedOn(this, type => type != "BLOCKLOOKUPACTION") : arrayed;

            foreach (DxfEntity entity in entities)
            {
                if (other.Contains(entity.Handle))
                {
                    throw new NotSupportedException("The entity " + entity.Handle + " is " + (array ? "worked on by another action" : "copied by an array action") + " of the block " + Name
                        + ", and an entity an array copies is not written with another action on it: no sample of AutoCAD shows the order they would take.");
                }
            }
        }

        /// <summary>Refuses a block that cannot be made dynamic: one of no file, erased, a layout's, anonymous, of an external reference, or of a drawing older than R2010.</summary>
        private void Dynamic(string what)
        {
            if (_file == null || Item == null || Item.Type != "BLOCK_RECORD" || IsErased)
            {
                throw new InvalidOperationException("The block " + Name + " is in no file of R2010 or later, or is erased, and cannot " + what + ".");
            }

            if (IsLayout || IsAnonymous || IsFromExternalReference)
            {
                throw new InvalidOperationException("The block " + Name + " is a layout's, anonymous or of an external reference, which AutoCAD does not make dynamic.");
            }

            if (!_file.Supports(1024))
            {
                throw new InvalidOperationException("A dynamic block is written in a DXF of R2010 (AC1024) or later, the oldest AutoCAD 2021 was seen to read one of; this drawing is " + (_file.Version ?? "of R12") + ".");
            }
        }

        /// <summary>
        /// Moves entities of the drawing into the block, as AutoCAD's <c>AssumeOwnershipOf</c>: each taken out of the block it is
        /// in and written at the end of this one, before its ENDBLK, or at the end of the ENTITIES section for model and paper
        /// space, its handle kept, this block as its owner and its group 67, and that of each record that follows it (a VERTEX,
        /// an ATTRIB, its SEQEND), as the space has it; the same object stands for it.
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
                    && (string.Equals(inserted.Name, Name, StringComparison.OrdinalIgnoreCase) || Holds(inserted, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Name })))
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
        /// block of a layout; when an INSERT, a dimension or a table of the drawing is drawn by it, or a dimension style, the
        /// override of a dimension of R12 or the header names it as an arrow; or when an object of the file points at it.</exception>
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

            // What the records use, counted, says at once that nothing uses a block erased after the blocks that did, one by one:
            // where a record may use it, the records are read for it, as before.
            DxfFile.Usage usage = _file.UsageOfRecords();

            if ((usage.MayDraw(name) && _file.Names(Naming, (type, code) => code == 2 && (type == "INSERT" || type == "DIMENSION" || type == "ACAD_TABLE"), name)) || NamedAsArrow(name, usage))
            {
                throw new InvalidOperationException("The block " + name + " is drawn by an INSERT, a dimension or a table, or named as an arrow, and is not erased, as AutoCAD erases a block only when nothing uses it.");
            }

            if (Handle != null && usage.MayPointAt(Handle) && _file.PointedAt(Handle))
            {
                throw new InvalidOperationException("The block " + name + " is pointed at by an object of the file, as a dimension style's arrow, and is not erased.");
            }

            Remove();
        }

        /// <summary>Takes the block out of its drawing, BLOCK, entities, ENDBLK and BLOCK_RECORD, as <see cref="Erase"/> does once nothing uses it.</summary>
        internal void Remove()
        {
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
                || (code == 3 && type == "BLOCK") || (type == "DIMSTYLE" && code >= 5 && code <= 7), 0, old, name, true);

            foreach (string variable in new[] { "$DIMBLK", "$DIMBLK1", "$DIMBLK2", "$DIMLDRBLK" })
            {
                IReadOnlyList<DxfTypedValue> groups = _file.HeaderOf(variable);

                if (groups != null && groups.Count == 1 && string.Equals(DxfReader.NameOf(groups[0].Value), old, StringComparison.OrdinalIgnoreCase))
                {
                    _file.SetHeader(variable, new[] { new DxfTypedValue(groups[0].TypeCode, name) });
                }
            }

            _file.Renamed(_block, name);
            database?.BlockTable.Renamed();
            GeometryHelperLog.Info("DxfDatabase: the block " + old + " was renamed " + name + ", with every INSERT of it.");
        }

        /// <summary>
        /// Whether a dimension style, the override of a dimension of R12 or the header names a block as an arrow: <c>$DIMBLK</c>
        /// and its kin, DIMSTYLE's 5, 6 and 7, and an override's 1070 5, 6 or 7 with the name in its 1000; the records read
        /// only where what they use, counted, may give the name.
        /// </summary>
        private bool NamedAsArrow(string name, DxfFile.Usage usage)
        {
            foreach (string variable in new[] { "$DIMBLK", "$DIMBLK1", "$DIMBLK2", "$DIMLDRBLK" })
            {
                IReadOnlyList<DxfTypedValue> groups = _file.HeaderOf(variable);

                if (groups != null && groups.Count > 0 && string.Equals(DxfReader.NameOf(groups[0].Value), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return usage.MayBeArrow(name) && (_file.Names(Naming, (type, code) => type == "DIMSTYLE" && code >= 5 && code <= 7, name) || _file.NamesArrowInOverrides(name));
        }

        /// <summary>
        /// Whether a block holds an INSERT of a block of the names given, at any depth: each block walked once, depth first in the
        /// order of its entities, on a stack of its own rather than the thread's, so that 3 000 blocks nested one inside the next
        /// are walked as one is.
        /// </summary>
        private static bool Holds(DxfBlockTableRecord block, HashSet<string> named)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = new Stack<IEnumerator<DxfEntity>>();
            DxfBlockTableRecord next = block;

            try
            {
                while (true)
                {
                    if (next != null && seen.Add(next.Name))
                    {
                        path.Push(next.GetEnumerator());
                    }

                    next = null;

                    if (path.Count == 0)
                    {
                        return false;
                    }

                    IEnumerator<DxfEntity> entities = path.Peek();

                    if (!entities.MoveNext())
                    {
                        path.Pop().Dispose();
                        continue;
                    }

                    if (entities.Current is DxfBlockReference reference)
                    {
                        if (named.Contains(reference.Name))
                        {
                            return true;
                        }

                        next = reference.BlockTableRecord;
                    }
                }
            }
            finally
            {
                while (path.Count > 0)
                {
                    path.Pop().Dispose();
                }
            }
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

        /// <summary>
        /// Gets whether the block is one AutoCAD names itself, as a dimension's <c>*D1</c> (flag 1 of its BLOCK's group 70), as
        /// AutoCAD's <c>IsAnonymous</c>: true of a block added named <c>*D</c>, <c>*U</c> or <c>*T</c>, which the table names
        /// <c>*D1</c>, <c>*U1</c> or <c>*T1</c> and on.
        /// </summary>
        public bool IsAnonymous => (Flags & 1) != 0;

        /// <summary>Gets whether the block is an external reference (flag 4), as AutoCAD's <c>IsFromExternalReference</c>.</summary>
        public bool IsFromExternalReference => (Flags & 4) != 0;

        /// <summary>Gets whether the block is an external reference overlaid (flag 8), as AutoCAD's <c>IsFromOverlayReference</c>.</summary>
        public bool IsFromOverlayReference => (Flags & 8) != 0;

        /// <summary>
        /// Gets whether the block has attribute definitions (flag 2), as AutoCAD's <c>HasAttributeDefinitions</c>: set when one is
        /// appended (<see cref="AppendEntity(DxfEntity)"/>), constant or not, in model space too, and kept when it is erased, as AutoCAD 2021
        /// sets and saves it; a block read from a file says what its file says.
        /// </summary>
        public bool HasAttributeDefinitions => (Flags & 2) != 0;

        /// <summary>
        /// Gets or sets the path of the drawing an external reference comes from (group 1 of its BLOCK), as AutoCAD's
        /// <c>PathName</c>; empty for a block that is none.
        /// </summary>
        /// <value>
        /// Set, written as the BLOCK's 1 in place of the path it had, as AutoCAD's -XREF Path writes the new one: the
        /// reference <c>column.dxf</c> given <c>..\xref\column.dxf</c> names that from then on. The path it has changes nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the path set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path set is blank or has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a block that is no external reference.</exception>
        public string PathName
        {
            get => _block?.Head == null || _file == null ? string.Empty : _file.ValueOf(_block.Head, 1) ?? string.Empty;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Trim().Length == 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    throw new ArgumentException("The path of an external reference is given, on one line: '" + value + "' is not one.", nameof(value));
                }

                if (!IsFromExternalReference && (MadeFlags & 4) == 0)
                {
                    throw new InvalidOperationException("The block " + Name + " is no external reference, so it has no path.");
                }

                if (_file == null)
                {
                    MadePath = value;
                    return;
                }

                if (string.Equals(value, PathName, StringComparison.Ordinal))
                {
                    return;
                }

                DxfItem head = _block?.Head ?? throw new InvalidOperationException("The block " + Name + " has no BLOCK to hold a path.");
                _file.SetValues(head, new[] { new KeyValuePair<int, string>(1, value) }, BlockOrder, false);
            }
        }

        /// <summary>
        /// Gets where the external reference stands, as AutoCAD's <c>XrefStatus</c>, from its BLOCK's flags and its path:
        /// <see cref="DxfXrefStatus.NotAnXref"/> for a block that is none (flag 4 clear); <see cref="DxfXrefStatus.Unreferenced"/>
        /// where no INSERT places it; <see cref="DxfXrefStatus.FileNotFound"/> where its <see cref="PathName"/> is no file, as
        /// written or from the folder of the drawing's <see cref="DxfDatabase.Filename"/>; <see cref="DxfXrefStatus.Resolved"/>
        /// where AutoCAD saved it resolved (flag 32); <see cref="DxfXrefStatus.Unresolved"/> otherwise, as one attached here
        /// stands until AutoCAD opens the drawing and resolves it. <see cref="DxfXrefStatus.Unloaded"/> is never given: no group
        /// of a DXF file says an external reference is unloaded.
        /// </summary>
        /// <remarks>
        /// The external reference <c>column.dxf</c> attached by <see cref="DxfDatabase.AttachXref"/> to a drawing saved in the
        /// folder that holds column.dxf is Unreferenced, then Unresolved once an INSERT places it, and FileNotFound once
        /// column.dxf is taken away.
        /// </remarks>
        public DxfXrefStatus XrefStatus
        {
            get
            {
                if (!IsFromExternalReference)
                {
                    return DxfXrefStatus.NotAnXref;
                }

                if (GetBlockReferences(true).Count == 0)
                {
                    return DxfXrefStatus.Unreferenced;
                }

                if (!PathFound(PathName, _file?.Database?.Filename))
                {
                    return DxfXrefStatus.FileNotFound;
                }

                return (Flags & 32) != 0 ? DxfXrefStatus.Resolved : DxfXrefStatus.Unresolved;
            }
        }

        /// <summary>Whether a path names a file: as written, or from the folder of a drawing where it is not rooted.</summary>
        private static bool PathFound(string path, string drawing)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (System.IO.File.Exists(path))
                {
                    return true;
                }

                string folder = drawing == null ? null : System.IO.Path.GetDirectoryName(drawing);
                return !System.IO.Path.IsPathRooted(path) && folder != null && System.IO.File.Exists(System.IO.Path.Combine(folder, path));
            }
            catch (ArgumentException)
            {
                // A path with a character no path may hold names no file.
                return false;
            }
            catch (System.IO.PathTooLongException)
            {
                return false;
            }
        }

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
        /// Gets the draw order of the block, as AutoCAD's <c>DrawOrderTableId</c> names it: the SORTENTSTABLE under ACAD_SORTENTS
        /// in the block's extension dictionary; null where the block has none, its entities then drawn in the order of the block.
        /// </summary>
        public DxfDrawOrderTable DrawOrderTable
        {
            get
            {
                DxfDBDictionary dictionary = ExtensionDictionary;
                return dictionary != null && dictionary.Contains(DxfDrawOrderTable.Key) && dictionary.GetAt(DxfDrawOrderTable.Key) is DxfDrawOrderTable table ? table.For(this) : null;
            }
        }

        /// <summary>
        /// Gives the block a draw order where it has none, as AutoCAD makes one when its <c>DrawOrderTableId</c> is asked for: an
        /// empty SORTENTSTABLE under ACAD_SORTENTS in the block's extension dictionary, made where it has none, owned by that
        /// dictionary and naming the block (330). The table the block has, where it has one.
        /// </summary>
        /// <returns>The table.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the block is in no file, is erased, or has no handle, as in a
        /// file of R12.</exception>
        public DxfDrawOrderTable CreateDrawOrderTable()
        {
            DxfDrawOrderTable table = DrawOrderTable;

            if (table != null)
            {
                return table;
            }

            string block = Handle ?? throw new InvalidOperationException("The block " + Name + " has no handle to be given a draw order by.");
            CreateExtensionDictionary();
            DxfItem made = ExtensionDictionary.Add(DxfDrawOrderTable.Key, "SORTENTSTABLE", b =>
            {
                b.Group(100, "AcDbSortentsTable");
                b.Group(330, block);
            });

            return ((DxfDrawOrderTable)OfObject(made)).For(this);
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
        /// it, and its owner the block's BLOCK_RECORD from R13 on; an entity of paper space, or of the block of another layout of
        /// it (<c>*Paper_Space0</c> and on), is written with its group 67 of 1, as AutoCAD and ezdxf write it. A
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
        /// pointer to an object this drawing has no namesake of is left out, so that its default holds, but the IMAGEDEF of an
        /// IMAGE, which comes with it under its name in the dictionary of images (ACAD_IMAGE_DICT, made where this drawing has
        /// none), one IMAGEDEF brought for all the images that show it, as AutoCAD 2021's <c>WblockCloneObjects</c> brings it,
        /// with the CLASS rows of IMAGE and IMAGEDEF, or of WIPEOUT, where the drawing lists none, each counted 1, and a row of
        /// them counted 0 counted 1, without which AutoCAD 2021 ignores them; an IMAGE that
        /// <see cref="DxfRasterImage.AssociateRasterDef"/> gave an IMAGEDEF of this drawing shows that one, given an
        /// IMAGEDEF_REACTOR of its own, nothing brought for it; and a handle of
        /// extended data (1005) is left out, as AutoCAD's mapping leaves it null, but for the records the overrides of a dimension
        /// style name (ACAD DSTYLE), which name those of the same name here. The records of the tables a copy names that this
        /// drawing has not (its layers, linetypes, text styles and dimension styles, and their arrow blocks) are copied with it by
        /// name, and a block AutoCAD names itself it draws by (a dimension's <c>*D</c>, an INSERT's <c>*U</c>) under a name of its
        /// own, as AutoCAD's deep clone does: one no INSERT, dimension or table of this drawing gives of a block it has not, so
        /// that a DIMENSION here naming a <c>*D1</c> the drawing has not does not come to draw by the copy, which comes as
        /// <c>*D2</c>; an INSERT's named block where this drawing has none of its name, under that name, with
        /// the blocks it inserts, as AutoCAD's <c>WblockCloneObjects</c> brings it, and every block a copy points to by a hard
        /// pointer so too (a MULTILEADER's block and arrow blocks, the block of an ACAD_TABLE's cell), as AutoCAD's deep clone
        /// follows its pointers to them. What the entities of the blocks copied point
        /// to among themselves (a LEADER to its MTEXT, a HATCH to its boundary) is kept between their copies, whichever comes first
        /// in its block, as AutoCAD's deep clone translates its map of ids once every object is copied, and so is every pointer to
        /// a block copied with it after its own (a MULTILEADER's block and arrow blocks, an ACAD_TABLE's picture and the blocks of
        /// its cells, the arrow block an override of a dimension style names). A copy appended to its own drawing keeps its pointers, but for the
        /// value a MULTILEADER gives an attribute definition erased since, which is left out with the definition's handle. A copy
        /// of an annotative entity appended is given the contexts it carries, and its ATTRIBs theirs, each under an extension
        /// dictionary as AutoCAD writes its copy's (check D35c). Appended to another drawing, each context is put on the scale
        /// of its scale's name there, as AutoCAD 2021's <c>WblockCloneObjects</c> and <c>Insert</c> put it: one of the same
        /// units taken as it is, one the drawing's list has not added to it, and one of the name of other units added as the
        /// name and <c>_1</c> (or <c>_2</c> and on, the first the list has not): a context of S7 of 1:3 copied into a drawing
        /// whose S7 is 1:7 stands on an S7_1 of 1:3 added for it (check D35d). A drawing that holds no list of scales, older
        /// than R2007, takes the copy without them, given the placing it is read at. The extents of the header are not grown by
        /// what is appended to a block.
        /// </para>
        /// <para>
        /// A drawing of an older version than the entity's is given what it can hold, as AutoCAD's SAVEAS to that version gives
        /// it: the overrides of a dimension style (ACAD DSTYLE) of a variable the version has not are left out, as DIMLTYPE of
        /// R2007 on a dimension added to a drawing of R2000, and so are the groups the version has not of the records of the
        /// tables copied by name with it, as a DIMSTYLE's DIMFXLEN (49) and a LAYER's material (347). A table's block cell keeps
        /// the values it gives the attributes of a block that comes with it, its pointers (331) naming the copies of their
        /// definitions; a LEADER whose annotation does not come with it points to none. An INSERT in a block brought that names
        /// a block its drawing has not, as a drawing read from a file may hold, is copied as it is, naming it, and said in one
        /// Debug line of <see cref="GeometryHelperLog"/>: the other drawing's M holding INSERTs of Z and K, which it has not,
        /// an INSERT of M copied in says that two INSERTs name a block their drawing has not, K and Z.
        /// <see cref="DxfDatabase.WblockCloneObjects"/> copies entities together, what points from one to another kept.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the file's version cannot hold the entity's type or look, or its
        /// linetype is not in the file's linetype table; or when it is an INSERT that would make this block insert itself at
        /// any depth; or when it is a copy of another drawing's entity whose blocks would, once brought, insert themselves or
        /// this block, an INSERT here of a block this drawing has not placing the one brought of that name: an INSERT, or a
        /// dimension whose <c>*D</c> holds an INSERT that closes such a round. Nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is in a file already, or this block is not; or it is
        /// a <see cref="DxfWipeout"/> made in code and given no polygon by <see cref="DxfWipeout.SetFrom"/>, or a copy of one of no
        /// boundary, as AutoCAD 2021 cannot open a drawing that holds one.</exception>
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

            // A copy carries its original's contexts: written here, on this drawing's scales of their names where this is
            // another drawing (check D35c, D35d).
            List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> carried = entity.TakeCopiedContexts(_file);
            DxfFile origin = entity.Item.File.Origin;
            bool madeInCode = entity.Item.File.IsMadeInCode;
            entity.Bind(_file.Adopt(space, entity.Item));
            DxfDBObject.WriteCopiedContexts(entity.Item, carried, origin);
            entity.Appended(madeInCode);

            // A block given an attribute definition says so (flag 2 of its BLOCK), as AutoCAD 2021 marks one, model space too,
            // the definition constant or not, and keeps the mark when the definition goes: without it AutoCAD takes no value
            // a multileader gives the block's attributes (check P4b).
            if (entity is DxfAttributeDefinition && _block?.Head != null && (Flags & 2) == 0)
            {
                _file.SetValues(_block.Head, new[] { new KeyValuePair<int, string>(70, (Flags | 2).ToString(CultureInfo.InvariantCulture)) }, BlockOrder, false);
            }

            // A text made in code is placed about its second point by the drawing's fonts, where it has them; a copy keeps its place.
            if (madeInCode && entity is DxfDBText placed)
            {
                placed.Realign();
            }
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

        /// <summary>The flags of the BLOCK of a block made in code (group 70): 1 for one AutoCAD names itself, 2 with attribute definitions, 4 an external reference, 8 one overlaid.</summary>
        internal int MadeFlags { get; set; }

        /// <summary>The path of the drawing an external reference made in code comes from (group 1 of its BLOCK); null for any other block.</summary>
        internal string MadePath { get; set; }

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

            DxfFileBlock block = file.AddBlock(Name, _origin, (int)_units, _explodable, _scaling == DxfBlockScaling.Uniform, _comments, out DxfItem record, MadeFlags, MadePath);
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
