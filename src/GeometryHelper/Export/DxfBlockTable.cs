using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
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
        private static readonly IReadOnlyList<int> BlockOrder = Array.AsReadOnly(new[] { 100, 2, 70, 10, 20, 30, 3, 1 });

        private DxfFile _file;
        private DxfFileBlock _block;
        private DxfSpace _space;
        private DxfSpaceKind _kind = DxfSpaceKind.Block;
        private GeoPoint3 _origin;

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
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a block of a file: renaming one comes in a later release.</exception>
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
                else if (!string.Equals(value, Name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The block " + Name + " is of a file, and is not renamed yet: a later release renames one with every reference to it.");
                }
            }
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

        /// <summary>Gets how many entities the block holds, a POLYLINE with its vertices one.</summary>
        public int Count => Space?.Count ?? 0;

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
        /// A file of R12 has no SPLINE, ELLIPSE, MTEXT, RAY, XLINE nor LWPOLYLINE, which are refused, nor lineweights or true
        /// colours. An entity of R13 or later is written in it in the forms of R12, as AutoCAD saves a drawing as R12: each
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
        /// extended data (1005) is left out, as AutoCAD's mapping leaves it null. A copy appended to its own drawing keeps its
        /// pointers. The extents of the header are not grown by what is appended to a block.
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
            entity.Bind(_file.Adopt(space, entity.Item));
        }

        /// <summary>
        /// Gets an enumerator over the entities, in the order of the file, those appended after them: over the entities the
        /// block holds when the walk begins, so that <c>foreach (DxfEntity e in db.ModelSpace) e.Erase();</c> erases them all,
        /// as it does in AutoCAD. An entity erased during the walk is passed over, and one appended is not reached.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfEntity> GetEnumerator()
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
            DxfFileBlock block = file.AddBlock(Name, _origin, out DxfItem record);
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
