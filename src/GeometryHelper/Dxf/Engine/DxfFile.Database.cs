using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        // The file an entity made in code stands in until it is added to one: R2018, so that any type and look can be held.
        private static readonly byte[] TemplateBytes = Encoding.ASCII.GetBytes("  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1032\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n");

        private static readonly DxfFile Template = new DxfFile(TemplateBytes, TemplateBytes.Length, null);

        private readonly bool _detached;
        private readonly DxfFile _origin;
        private DxfFile _holder;

        /// <summary>
        /// A file that holds entities not yet in a file of their own: made in code, or copied from another. It shares the bytes
        /// and style of the file it is made from, writes no handles and no owners, adds no layer, takes any linetype and grows
        /// no extents; what is put in it is never saved. One such file holds every entity made from the same file while its
        /// text is read the same way (<see cref="Holder"/>), and keeps none of them: each entity holds its own records alone.
        /// </summary>
        private DxfFile(DxfFile from)
        {
            _bytes = from._bytes;
            _length = from._length;
            _starts = from._starts;
            _types = from._types;
            _sections = from._sections;
            _decoding = from._decoding;
            _header = from._header;
            _namedPage = from._namedPage;
            Version = from.Version;
            CodePage = from.CodePage;
            Warnings = from.Warnings;
            _detached = true;
            _origin = from._origin ?? from;

            _drawing = new Lazy<Drawing>(ReadDrawing);
            _blocks = new Lazy<IReadOnlyDictionary<string, DxfFileBlock>>(ReadBlocks);
            _layers = NewLayers();
            _placer = new Lazy<DxfReader.Placer>(NewPlacer);
        }

        /// <summary>Whether the file holds entities not yet in a file of their own.</summary>
        internal bool IsDetached => _detached;

        /// <summary>Whether the file writes subclass markers: from R13 on.</summary>
        internal bool HasMarkers => Style.Markers;

        #region Entities not yet in a file

        /// <summary>
        /// The file that holds the entities made from this one and not yet in a file: those copied from it, and from what it
        /// holds itself where it holds such entities. One for the file and every copy made from it, made anew only where this
        /// file reads its text otherwise than that one does, as after a letter beyond ASCII was first written into it, so that
        /// each copy is read as it was before it was copied. A few hundred bytes for each copy, its records, where a file of its
        /// own was some thousands.
        /// </summary>
        private DxfFile Holder()
        {
            DxfFile origin = _origin ?? this;
            DxfFile holder = Volatile.Read(ref origin._holder);

            if (holder != null && ReferenceEquals(holder._decoding, _decoding))
            {
                return holder;
            }

            holder = new DxfFile(this);
            Volatile.Write(ref origin._holder, holder);
            return holder;
        }

        /// <summary>A shape written as an entity of its own, on layer 0 with no look, as the writer writes it in a file of R2018.</summary>
        internal static DxfItem Detached(object shape)
        {
            DxfFile file = Template.Holder();
            return file.Add(file.ModelSpace, shape, null, null, null, null, null, 0.0, 0);
        }

        /// <summary>
        /// A shape written as an entity of its own, as <see cref="Detached(object)"/> writes it, with a look written in the same
        /// writing, as AutoCAD writes it: its layer, its linetype, colour, lineweight and true colour where given.
        /// </summary>
        internal static DxfItem Detached(object shape, string layer, int? colour, int? trueColour, string linetype, int? lineweight)
        {
            DxfFile file = Template.Holder();
            return file.Add(file.ModelSpace, shape, layer, colour, trueColour, linetype, lineweight, 0.0, 0);
        }

        /// <summary>
        /// An entity of its own written from its groups: its type, the marker AcDbEntity, layer 0, then the groups given, which
        /// begin with its subclass marker.
        /// </summary>
        internal static DxfItem Detached(string type, IEnumerable<DxfTypedValue> groups)
        {
            DxfFile file = Template.Holder();
            var b = new Builder(file);
            b.Group(0, type);
            b.Group(100, "AcDbEntity");
            b.Group(8, "0");

            foreach (DxfTypedValue group in groups)
            {
                b.Group(group.TypeCode, group.Value);
            }

            return file.Hold(new List<Rec> { b.Done(type) }, type);
        }

        /// <summary>
        /// A copy of an entity, not in the file: every record of it with its groups as written, but its handle, its owner, the
        /// groups of the applications (102: reactors and the extension dictionary), its space (67) and its layout (410). A
        /// MULTILEADER keeps the handles of the attribute definitions its values are given for (its 330s after its own marker),
        /// which name the definitions of the block it places, as AutoCAD's copy keeps them.
        /// </summary>
        internal DxfItem CopyOf(DxfItem item) => CopyOf(item, false);

        /// <summary>
        /// A copy of an entity, not in the file, as <see cref="CopyOf(DxfItem)"/> gives one; where asked, a HATCH keeping the
        /// handles of the objects its loops follow (its 330s after its own marker), for a copy whose boundary comes with it.
        /// </summary>
        internal DxfItem CopyOf(DxfItem item, bool keepLinks)
        {
            DxfFile file = Holder();
            var records = new List<Rec>();

            foreach (Rec record in RecordsOf(item))
            {
                var b = new Builder(file);
                List<Pair> pairs = PairsOf(record);
                b.Raw(record, pairs[0]);
                bool braced = false;
                int body = (keepLinks && record.Type == "HATCH") || record.Type == "MULTILEADER" ? BodyAt(record, pairs, DataAt(record, pairs)) : -1;

                for (int i = 1; i < pairs.Count; i++)
                {
                    Pair pair = pairs[i];

                    if (pair.Code == 102)
                    {
                        braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                        continue;
                    }

                    bool data = pair.Code >= 1000;
                    bool link = body >= 0 && i > body && pair.Code == 330;

                    if (braced || (!data && !link && (pair.Code == 5 || pair.Code == 105 || pair.Code == 330 || pair.Code == 360 || pair.Code == 67 || pair.Code == 410)))
                    {
                        continue;
                    }

                    b.Raw(record, pair);
                }

                records.Add(file.Recounted(b.Done(record.Type)));
            }

            DxfItem copy = file.Hold(records, records[0].Type);

            // A copy of an INSERT keeps to the block it places, as AutoCAD's keeps its id: renamed in this drawing, it is still it.
            if (copy.Type == "INSERT")
            {
                copy.OriginBlock = item.OriginBlock ?? (_detached ? null : BlockNamed(ValueOf(item, 2, true)));
            }

            return copy;
        }

        /// <summary>
        /// A TEXT made of an MTEXT of this file, not in a file, as the MTEXT's copies are held: the MTEXT's common groups, its
        /// layer, linetype, colour and the rest of its look, as it has them, but its handle, owner, groups of the applications,
        /// space and layout; then the TEXT's own groups given, which begin with its subclass marker. No extended data.
        /// </summary>
        internal DxfItem TextOf(DxfItem mtext, IEnumerable<KeyValuePair<int, string>> own)
        {
            DxfFile file = Holder();
            Rec head = HeadOf(mtext);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            int body = BodyAt(head, pairs, data);
            var b = new Builder(file);
            b.Group(0, "TEXT");
            bool braced = false;

            for (int i = 1; i < (body >= 0 ? body : data); i++)
            {
                Pair pair = pairs[i];

                if (pair.Code == 102)
                {
                    braced = Text(head, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (!braced && !Placed(pair.Code) && pair.Code != 999)
                {
                    b.Raw(head, pair);
                }
            }

            foreach (KeyValuePair<int, string> group in own)
            {
                b.Group(group.Key, group.Value);
            }

            return file.Hold(new List<Rec> { b.Done("TEXT") }, "TEXT");
        }

        /// <summary>The block of a name as an INSERT writes it, letters of either case the same; null where the file has none.</summary>
        private DxfFileBlock BlockNamed(string written)
            => written != null && Blocks.TryGetValue(DxfReader.NameOf(written), out DxfFileBlock block) ? block : null;

        /// <summary>Records of an entity held by a file that is not saved: the entity's own, which the file does not keep.</summary>
        private DxfItem Hold(List<Rec> records, string type) => new DxfItem(this, new Piece(records), type);

        /// <summary>
        /// Adds an entity not yet in a file to a space of this one, as AutoCAD appends one: its records written in the file's
        /// own style, with handles of their own above every handle of the file, the space's owner, its space (67) in paper
        /// space, no subclass markers in a file of R12, and every other group as the entity has it; its layer added to the
        /// layer table where it is not listed, and the applications of its extended data to the table of applications.
        /// </summary>
        internal DxfItem Adopt(DxfSpace space, DxfItem from)
        {
            DxfFile source = from.File;
            List<Rec> records = source.RecordsOf(from);

            // A copy of an INSERT is written naming its block by the name the block has now.
            DxfFileBlock originBlock = from.OriginBlock;

            if (originBlock != null && !originBlock.Head.IsRemoved && source.WithGroup(records[0], 2, originBlock.Name, false, out Rec named)
                && !string.Equals(DxfReader.NameOf(source.ValueOf(from, 2, true) ?? string.Empty), originBlock.Name, StringComparison.Ordinal))
            {
                records[0] = named;
            }

            Rec head = records[0];
            string type = head.Type;
            Writing style = Style;
            bool marking = style.Markers && !source.Style.Markers;
            bool unmarking = !style.Markers && source.Style.Markers;
            DxfFile origin = source.Origin;
            bool foreign = origin != null && origin != Template && origin != this;
            CheckAdoptable(source, records, out string layer, out List<string> applications, foreign ? origin : null);

            // A copy of an entity of another drawing that draws by a block AutoCAD names itself (a DIMENSION's *D, an ACAD_TABLE's
            // *T, an INSERT's *U) takes a copy of that block, under a name of its own here, as AutoCAD's deep clone between
            // databases does; and a block such a block inserts that this drawing has not of its name, under that name. The
            // records of the tables it and they name that this drawing has not, its layers, linetypes, text styles and dimension
            // styles, are copied by name too. Every entity of those blocks is checked before anything is written.
            var needs = new RecordsToCopy();
            List<DxfFileBlock> blocks = foreign ? BlocksToCopy(origin, records, needs) : null;
            bool outer = _blockCopies == null && blocks != null && blocks.Count > 0;

            if (outer)
            {
                // Blocks that would come to insert themselves once written are refused before anything is.
                string owner = space.Kind == DxfSpaceKind.Block && space.Block != null && !IsLayoutName(space.Block.Name) ? space.Block.Name : null;
                var placed = new List<string>();

                if (type == "INSERT")
                {
                    placed.Add(DxfReader.NameOf(source.ValueOf(from, 2) ?? string.Empty));
                }

                CheckRounds(origin, blocks, owner, placed, "entity");
                _blockCopies = new Dictionary<DxfFileBlock, string>();
                _entityCopies = new Dictionary<ulong, string>();
                _copiedItems = new Dictionary<string, DxfItem>(StringComparer.OrdinalIgnoreCase);
                _deferred = new List<DxfItem>();
            }

            try
            {
                if (foreign)
                {
                    CopyRecords(origin, needs.Styles, "STYLE");
                    CopyRecords(origin, needs.Linetypes, "LTYPE");
                    CopyRecords(origin, needs.Layers, "LAYER");
                }

                EnsureLayer(layer == null || layer.Trim().Length == 0 ? "0" : layer.Trim());

                foreach (string application in applications)
                {
                    EnsureApplication(application);
                }

                if (blocks != null && blocks.Count > 0)
                {
                    CopyBlocks(origin, blocks);
                }

                if (foreign)
                {
                    CopyRecords(origin, needs.DimStyles, "DIMSTYLE");
                }

                DxfFileBlock drawn = foreign && _blockCopies != null ? origin.BlockDrawnBy(records[0]) : null;

                if (drawn != null && _blockCopies.TryGetValue(drawn, out string copied) && source.WithGroup(records[0], 2, copied, false, out Rec renamed))
                {
                    records[0] = renamed;
                }

                return Write(space, from, source, records, foreign ? origin : null, marking, unmarking);
            }
            finally
            {
                if (outer)
                {
                    // The pointers between the entities copied, held back until every one of them is written, are taken to
                    // their copies now, as AutoCAD's deep clone translates its map of ids at the end.
                    try
                    {
                        TranslateDeferred();
                    }
                    finally
                    {
                        _blockCopies = null;
                        _entityCopies = null;
                        _copiedItems = null;
                        _deferred = null;
                        _blocksToCome = null;
                    }
                }
            }
        }

        /// <summary>
        /// Checks that an entity not yet in a file can be added to this one, as <see cref="Adopt"/> checks it before it writes
        /// anything, the blocks it brings with it among what is checked: so that several entities are checked before any of them
        /// is written. Given the blocks of the other drawing checked so far, by the checks of entities before it while nothing
        /// was written, those are not checked again, nor the blocks they insert, which were checked with them: so that the
        /// 3 000 blocks of a drawing nested one inside the next are checked once each, not once for each block above them. The
        /// blocks it would bring are added to <paramref name="brought"/>, where given, for <see cref="CheckRounds"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown where <see cref="Adopt"/> would refuse the entity.</exception>
        internal void CheckAdopt(DxfItem from, HashSet<DxfFileBlock> checkedBlocks = null, List<DxfFileBlock> brought = null)
        {
            DxfFile source = from.File;
            List<Rec> records = source.RecordsOf(from);
            DxfFile origin = source.Origin;
            bool foreign = origin != null && origin != Template && origin != this;
            CheckAdoptable(source, records, out _, out _, foreign ? origin : null);

            if (foreign)
            {
                List<DxfFileBlock> blocks = BlocksToCopy(origin, records, new RecordsToCopy(), checkedBlocks);
                brought?.AddRange(blocks);
            }
        }

        /// <summary>
        /// Checks that the records of an entity of a file can be added to this one, as <see cref="Adopt"/> adds them, and gives the
        /// layer the entity is on and the applications of its extended data.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown where this file's version cannot hold the entity's type or look, or its
        /// linetype is not in this file's linetype table.</exception>
        private void CheckAdoptable(DxfFile source, List<Rec> records, out string layer, out List<string> applications, DxfFile origin = null)
        {
            Rec head = records[0];
            string type = head.Type;
            Writing style = Style;

            // A record that follows an entity is not one of a block: AutoCAD refuses a vertex appended to a block table record.
            if (type == "VERTEX" || type == "SEQEND" || type == "ATTRIB")
            {
                throw new ArgumentException("A " + type + " belongs to the entity it follows, a POLYLINE or an INSERT: add it by that entity, not to a block.", "entity");
            }

            if (!style.Markers && SinceR13.Contains(type))
            {
                throw new ArgumentException("A " + type + " cannot be added to a DXF " + VersionSaid() + ", which has no such type: R13 (AC1012) brought it.", "entity");
            }

            if (type == "LWPOLYLINE" && !style.Lightweight)
            {
                throw new ArgumentException("A LWPOLYLINE cannot be added to a DXF " + VersionSaid() + ", which has no lightweight polylines: R14 (AC1014) brought them. Add a DxfPolyline2d.", "entity");
            }

            // An entity of R12 is written in the forms of R13 on: each of its records needs AutoCAD's subclass markers. One of
            // R13 or later is written in the forms of R12 in a file of R12: each record keeps the groups R12 has, each from its
            // own part.
            bool marking = style.Markers && !source.Style.Markers;
            bool unmarking = !style.Markers && source.Style.Markers;

            foreach (Rec record in marking ? records : new List<Rec>())
            {
                if (MarkersOf(record, PairsOf(record), out _, out _) == null)
                {
                    throw new ArgumentException("A " + record.Type + " of a DXF of R12, which has no subclass markers, cannot be added to a DXF " + VersionSaid() + " yet.", "entity");
                }
            }

            foreach (Rec record in unmarking ? records : new List<Rec>())
            {
                if (!R12Groups.ContainsKey(record.Type))
                {
                    throw new ArgumentException("A " + record.Type + " of a DXF of R13 or later has no form of R12 the library writes, so it cannot be added to a DXF " + VersionSaid() + ".", "entity");
                }
            }

            layer = null;
            string linetype = null;
            bool weighted = false;
            bool coloured = false;
            bool ownLook = false;
            applications = new List<string>();
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);

            for (int i = 1; i < pairs.Count; i++)
            {
                string value = source.Text(head, pairs[i]);

                if (i >= data)
                {
                    if (pairs[i].Code == 1001)
                    {
                        applications.Add(value.Trim());
                    }

                    continue;
                }

                switch (pairs[i].Code)
                {
                    case 8: layer = layer ?? value; break;
                    case 6: linetype = linetype ?? value; break;
                    case 370: weighted = true; break;
                    case 420:
                    case 430:
                    case 440: coloured = true; break;
                    case 48:
                    case 60: ownLook = true; break;
                }
            }

            if (ownLook && !style.Markers)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no linetype scale or visibility of an entity's own, which came with R13 (AC1012).", "entity");
            }

            if (weighted && !style.Lineweights)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no lineweights, which came with R2000 (AC1015).", "entity");
            }

            if (coloured && !style.TrueColours)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no true colours, colour books or transparency, which came with R2004 (AC1018).", "entity");
            }

            // A copy of another drawing takes a linetype this one has not with it, where that one has it.
            if (linetype != null && !HasLinetype(linetype) && !(origin != null && RecordsCopy(origin) && origin.HasLinetype(linetype)))
            {
                throw new ArgumentException("The linetype " + DxfReader.NameOf(linetype) + " is not in the file's linetype table: give one of it, BYLAYER or BYBLOCK.", "entity");
            }
        }

        /// <summary>
        /// Writes the records of an entity checked by <see cref="CheckAdoptable"/> into a space of this file, as <see cref="Adopt"/>
        /// says; a copy of another drawing (<paramref name="origin"/>) with its pointers taken to this one.
        /// </summary>
        private DxfItem Write(DxfSpace space, DxfItem from, DxfFile source, List<Rec> records, DxfFile origin, bool marking, bool unmarking)
        {
            Writing style = Style;
            string type = records[0].Type;

            // The overrides of a dimension style this drawing's version has no variable of are left out, as AutoCAD's SAVEAS to
            // an older version leaves them out.
            if (style.Version < source.Style.Version)
            {
                records[0] = source.WithoutLaterOverrides(records[0], this);
            }

            // A copy of an entity of another drawing points at objects of that drawing: each pointer is taken to the object of
            // the same name here, or left out, as AutoCAD's deep clone between databases maps or drops them.
            bool deferred = false;

            if (origin != null)
            {
                for (int r = 0; r < records.Count; r++)
                {
                    records[r] = source.Pointed(records[r], origin, this, ref deferred);
                }
            }
            else if (type == "MULTILEADER" && source.Origin == this)
            {
                records[0] = source.WithoutLostAttributes(records[0], this);
            }

            Rec head = records[0];
            bool paper = space.Kind == DxfSpaceKind.Paper;
            bool sheet = InPaperSpace(space);
            string handle = UsesHandles() ? NewHandle() : null;
            string owner = style.Markers ? OwnerOf(space) : null;
            var written = new List<Rec> { Written(source, head, handle, owner, sheet, marking, unmarking, records.Count > 1 && records[1].Type != "SEQEND") };

            for (int r = 1; r < records.Count; r++)
            {
                string own = UsesHandles() ? NewHandle() : null;
                written.Add(Written(source, records[r], own, style.Markers ? handle : null, sheet, marking, unmarking, false));
            }

            var piece = new Piece(written);
            var item = new DxfItem(this, piece, type) { Home = Place(space, piece), IsPaper = paper, Block = space.Block?.Name };
            TakeFollowers(from, item);
            space.Append(item);

            if (deferred)
            {
                _deferred.Add(item);
            }

            if (space.Kind != DxfSpaceKind.Block)
            {
                GrowBy(item);
            }

            Edited();
            return item;
        }

        /// <summary>
        /// Whether the entities of a space are in paper space, written with their group 67 of 1: those of paper space, and those
        /// of the block of another layout of it, <c>*Paper_Space0</c> and on, as AutoCAD and ezdxf write them.
        /// </summary>
        private static bool InPaperSpace(DxfSpace space)
            => space.Kind == DxfSpaceKind.Paper
            || (space.Kind == DxfSpaceKind.Block && space.Block?.Name != null && space.Block.Name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase));

        // The blocks of another drawing being copied into this one by the entity being added, each with the name it is given here.
        private Dictionary<DxfFileBlock, string> _blockCopies;

        /// <summary>
        /// While <c>DxfDatabase.Insert</c> merges the named blocks of another drawing into this one, the blocks found, by the walk
        /// of an INSERT appended to a block merged, to insert none merged at any depth and to change no more while the merge
        /// lasts, which the next walks pass over; null at any other time.
        /// </summary>
        internal HashSet<DxfBlockTableRecord> Roundless { get; set; }

        // The entities of those blocks copied so far, by their handles there, each with the handle of its copy here: what a
        // pointer of the entity being added to one of them is taken to, as a table's cell to an attribute definition (331).
        private Dictionary<ulong, string> _entityCopies;

        /// <summary>
        /// The block of this file an entity draws by: the one its group 2 names, of an INSERT, a dimension or an ACAD_TABLE;
        /// null for any other type, or where the file has no block of the name.
        /// </summary>
        private DxfFileBlock BlockDrawnBy(Rec record)
        {
            switch (record.Type)
            {
                case "INSERT":
                case "DIMENSION":
                case "ARC_DIMENSION":
                case "LARGE_RADIAL_DIMENSION":
                case "ACAD_TABLE":
                    string name = Record(record).Text(2);
                    return name != null && Blocks.TryGetValue(DxfReader.NameOf(name), out DxfFileBlock block) ? block : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The blocks of the drawing a copy came from (<paramref name="origin"/>) to be copied into this one with it: the block the
        /// entity draws by where AutoCAD names it itself or this drawing has no block of its name (an INSERT's named block), and,
        /// within each block copied, every block an entity of it draws by that is so; each once, those copied already by the entity being added left out. Every
        /// entity of them is checked to be one this file can hold, before anything is written, and the records of the tables they
        /// and the entity name that this drawing has not are noted in <paramref name="needs"/>, an arrow block of a dimension style
        /// among the blocks. A block among <paramref name="checkedBlocks"/>, checked whole with the blocks it brings by a check
        /// before, is passed over, as <see cref="CheckAdopt"/> says, and each block checked here is added to them.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown where an entity of a block to be copied cannot be added to this file, as
        /// <see cref="Adopt"/> says.</exception>
        private List<DxfFileBlock> BlocksToCopy(DxfFile origin, List<Rec> records, RecordsToCopy needs, HashSet<DxfFileBlock> checkedBlocks = null)
        {
            var order = new List<DxfFileBlock>();
            Collect(origin, records[0], order);
            CollectPlaced(origin, records[0], order, needs);
            NeedsOf(origin, records, needs, order);

            for (int k = 0; k < order.Count; k++)
            {
                if (checkedBlocks != null && !checkedBlocks.Add(order[k]))
                {
                    continue;
                }

                foreach (DxfItem item in order[k].Items.Snapshot())
                {
                    if (item.IsRemoved)
                    {
                        continue;
                    }

                    List<Rec> own = origin.RecordsOf(item);

                    // An MTEXT goes into a drawing of R12 as the TEXTs it is written as there.
                    List<DxfEntity> texts = AsTexts(item);

                    if (texts == null)
                    {
                        CheckAdoptable(origin, own, out _, out _, origin);
                    }

                    foreach (DxfEntity text in texts ?? new List<DxfEntity>())
                    {
                        CheckAdoptable(text.Item.File, text.Item.File.RecordsOf(text.Item), out _, out _, origin);
                    }

                    Collect(origin, own[0], order);
                    CollectPlaced(origin, own[0], order, needs);
                    NeedsOf(origin, own, needs, order);
                }
            }

            return order;
        }

        /// <summary>Adds to the blocks to copy the one a record of the origin draws by, where it is to be copied, as <see cref="BlocksToCopy"/> says.</summary>
        private void Collect(DxfFile origin, Rec record, List<DxfFileBlock> order) => Collect(origin.BlockDrawnBy(record), order);

        /// <summary>
        /// Adds to the blocks to copy those a record of the origin points to by a hard pointer of its own groups (340 to 349, 360
        /// to 369) to a BLOCK_RECORD of the origin, as an INSERT's block is added
        /// (<see cref="Collect(DxfFile, Rec, List{DxfFileBlock})"/>), as AutoCAD's deep clone follows hard pointers: a
        /// MULTILEADER's content (341 of its context data, 344) and arrows (342, 345), an ACAD_TABLE's picture (343) and the
        /// block of a cell (340). So that an entity copied alone, or before an INSERT of its block, brings that block with it.
        /// The entities of the other types are not read for it: they name their blocks (an INSERT's or a dimension's group 2).
        /// </summary>
        private void CollectPlaced(DxfFile origin, Rec record, List<DxfFileBlock> order, RecordsToCopy needs)
        {
            // The other types point to no block by handle: reading each of them cost 4 % of a copy of 22 000 lines (.NET Framework).
            if (record.Type != "MULTILEADER" && record.Type != "ACAD_TABLE")
            {
                return;
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            bool braced = false;

            for (int i = 1; i < data; i++)
            {
                int code = pairs[i].Code;

                if (code == 102)
                {
                    braced = origin.Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced || !IsHardPointer(code) || !TryHex(origin.Text(record, pairs[i]).Trim(), out ulong handle))
                {
                    continue;
                }

                needs.Objects = needs.Objects ?? ObjectsOf(origin);

                if (needs.Objects.TryGetValue(handle, out KeyValuePair<Rec, bool> pointed) && pointed.Value && pointed.Key.Type == "BLOCK_RECORD"
                    && origin.Blocks.TryGetValue(DxfReader.NameOf(origin.Record(pointed.Key).Text(2) ?? string.Empty), out DxfFileBlock block)
                    && !DxfBlockTableRecord.IsModelName(block.Name) && !block.Name.StartsWith(DxfBlockTableRecord.PaperSpace, StringComparison.OrdinalIgnoreCase))
                {
                    Collect(block, order);
                }
            }
        }

        /// <summary>Adds a block of the origin to the blocks to copy, where it is to be copied, as <see cref="BlocksToCopy"/> says.</summary>
        private void Collect(DxfFileBlock block, List<DxfFileBlock> order)
        {
            if (block == null || order.Contains(block) || (_blockCopies != null && _blockCopies.ContainsKey(block)))
            {
                return;
            }

            // A block AutoCAD names itself is copied under a name of its own; a named one where this drawing has none of its
            // name, the INSERT copied placing it among them, as AutoCAD's WblockCloneObjects brings it; one this drawing has of
            // the name is this drawing's own.
            if (IsAnonymous(block.Name) || !Blocks.ContainsKey(block.Name))
            {
                order.Add(block);
            }
        }

        /// <summary>Whether a block's name is one AutoCAD gives itself, as <c>*D1</c> or <c>*U4</c>; not model space nor paper space.</summary>
        private static bool IsAnonymous(string name)
            => name.StartsWith("*", StringComparison.Ordinal) && !DxfBlockTableRecord.IsModelName(name) && !name.StartsWith(DxfBlockTableRecord.PaperSpace, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Copies blocks of another drawing into this one, as AutoCAD's deep clone between databases copies those an entity draws
        /// by: each named first (a block AutoCAD names itself the first name of its letter, <c>*D</c>, <c>*U</c>, <c>*T</c>, and
        /// a number, this file has not; any other by its own name), then written with its BLOCK_RECORD, BLOCK and ENDBLK, its
        /// flags of a block named by AutoCAD and of attribute definitions, its base point, a named block its units, explodability,
        /// scaling and description, and each of its entities copied into it, new handles and owners given, an entity that draws
        /// by another of these blocks naming its copy.
        /// </summary>
        private void CopyBlocks(DxfFile origin, List<DxfFileBlock> blocks)
        {
            HashSet<string> kept = null;
            HashSet<string> now = null;

            foreach (DxfFileBlock block in blocks)
            {
                _blockCopies[block] = IsAnonymous(block.Name) ? FreeAnonymousName(block.Name, kept = kept ?? AnonymousNamesKept(origin, blocks), ref now) : block.Name;
            }

            foreach (DxfFileBlock block in blocks)
            {
                // A named block keeps what its record says of it; one AutoCAD names itself is written as before. An external
                // reference stays one, flagged and naming its drawing as it was (4, 8 and its 1).
                DxfBlockTableRecord own = IsAnonymous(block.Name) || origin.Database == null || !origin.Database.BlockTable.Has(block.Name) ? null : origin.Database.BlockTable[block.Name];
                bool xref = (block.Flags & 4) != 0 && block.Head != null;
                int flags = block.Flags & (xref ? 15 : 3);
                string path = xref ? origin.ValueOf(block.Head, 1) ?? string.Empty : null;
                DxfItem record;
                DxfFileBlock made = own == null
                    ? AddBlock(_blockCopies[block], block.BasePoint, 0, true, false, string.Empty, out record, flags, path)
                    : AddBlock(_blockCopies[block], block.BasePoint, (int)own.Units, own.Explodable, own.BlockScaling == DxfBlockScaling.Uniform, own.Comments ?? string.Empty, out record, flags, path);
                Database?.BlockTable.Adopted(made, record);

                foreach (DxfItem item in block.Items.Snapshot())
                {
                    if (item.IsRemoved)
                    {
                        continue;
                    }

                    List<DxfEntity> texts = AsTexts(item);

                    if (texts != null)
                    {
                        foreach (DxfEntity text in texts)
                        {
                            Adopt(made.Items, text.Item);
                        }

                        continue;
                    }

                    // A HATCH keeps the handles of its boundary, which comes with it in the block, to follow its copies.
                    DxfItem copy = Adopt(made.Items, origin.CopyOf(item, true));

                    if (copy != null && TryHex(item.Handle ?? string.Empty, out ulong was) && was != 0 && copy.Handle != null)
                    {
                        _entityCopies[was] = copy.Handle;
                        _copiedItems[copy.Handle] = copy;
                    }
                }
            }
        }

        /// <summary>
        /// Refuses blocks of another drawing (<paramref name="origin"/>) to be copied into this one where one of them, once
        /// written, would insert itself, by its own INSERTs or theirs: the INSERTs of the blocks brought placing one another and
        /// this drawing's blocks of the names they give, and an INSERT of this drawing that names a block it has not placing the
        /// one brought of that name; or where an INSERT added to a block that is no layout (<paramref name="owner"/>, of the
        /// names <paramref name="placed"/> gives, as the origin names them) would come back to that block through them. This
        /// drawing's P holding an INSERT of M, which it has not, and the other's M one of P, the M brought would place P and P
        /// that M, without end, which no reader can place. The blocks are walked as the INSERTs placing them will be, each once
        /// (Tarjan's strongly connected components), on a stack of their own; a round among this drawing's blocks alone is
        /// left as it is. A block brought that AutoCAD names itself is reached only by the copied INSERTs that place it, as
        /// its copy takes no name an INSERT gives (<see cref="FreeAnonymousName"/>). With no block brought, a block not yet
        /// made (<paramref name="owner"/>, none of its name in the drawing) is walked so with the INSERTs it is to hold, as
        /// <see cref="DxfBlockReference.ConvertToStaticBlock(string)"/> walks it before it adds it.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown where a block brought, or the block added to, would insert itself.</exception>
        internal void CheckRounds(DxfFile origin, List<DxfFileBlock> brought, string owner, List<string> placed, string parameter)
        {
            var nodes = new List<RoundNode>();
            var byBlock = new Dictionary<DxfFileBlock, int>();
            var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfFileBlock block in brought)
            {
                if (!byBlock.ContainsKey(block))
                {
                    byBlock.Add(block, nodes.Count);

                    // A named block comes under its own name, which this drawing's INSERTs of it then place.
                    if (!IsAnonymous(block.Name))
                    {
                        byName[block.Name] = nodes.Count;
                    }

                    nodes.Add(new RoundNode(block.Name, block));
                }
            }

            int broughtCount = nodes.Count;
            int ownerAt = owner == null ? -1 : Named(owner);

            if (broughtCount == 0 && ownerAt < 0)
            {
                return;
            }

            var ownerNext = new List<int>();

            foreach (string name in placed)
            {
                ownerNext.Add(OfOrigin(name));
            }

            int counter = 0;
            var component = new Stack<int>();
            var path = new Stack<KeyValuePair<int, int>>();

            for (int root = 0; root <= broughtCount; root++)
            {
                int start = root < broughtCount ? root : ownerAt;

                if (start < 0 || nodes[start].Index >= 0)
                {
                    continue;
                }

                Visit(start);

                while (path.Count > 0)
                {
                    KeyValuePair<int, int> top = path.Pop();
                    RoundNode node = nodes[top.Key];

                    if (top.Value < node.Next.Count)
                    {
                        path.Push(new KeyValuePair<int, int>(top.Key, top.Value + 1));
                        int next = node.Next[top.Value];

                        if (nodes[next].Index < 0)
                        {
                            Visit(next);
                        }
                        else if (nodes[next].OnStack)
                        {
                            node.Low = Math.Min(node.Low, nodes[next].Index);
                        }

                        continue;
                    }

                    if (path.Count > 0)
                    {
                        RoundNode above = nodes[path.Peek().Key];
                        above.Low = Math.Min(above.Low, node.Low);
                    }

                    if (node.Low != node.Index)
                    {
                        continue;
                    }

                    // The blocks that reach one another: a round where there are two or more, or one that inserts itself.
                    var members = new List<int>();
                    int member;

                    do
                    {
                        member = component.Pop();
                        nodes[member].OnStack = false;
                        members.Add(member);
                    }
                    while (member != top.Key);

                    if (members.Count == 1 && !node.Next.Contains(top.Key))
                    {
                        continue;
                    }

                    members.Sort();

                    if (members[0] < broughtCount)
                    {
                        throw new ArgumentException("The block " + nodes[members[0]].Name + " the copies bring would insert itself, by its own INSERTs or theirs, through the blocks of this drawing of the names they give: AutoCAD's AUDIT takes such an INSERT out, and nothing is copied.", parameter);
                    }

                    if (ownerAt >= 0 && members.BinarySearch(ownerAt) >= 0 && ownerNext.Exists(n => members.BinarySearch(n) >= 0))
                    {
                        throw new ArgumentException("The block " + owner + " would insert itself, by the INSERTs added to it or the blocks they bring: AutoCAD's AUDIT takes such an INSERT out, and nothing is copied.", parameter);
                    }
                }
            }

            // The node of a block of a name in this drawing once the blocks are brought: the one brought of the name, or this
            // drawing's, or none, an INSERT of which places nothing.
            int Named(string name)
            {
                if (!byName.TryGetValue(name, out int at))
                {
                    at = nodes.Count;
                    byName.Add(name, at);
                    nodes.Add(new RoundNode(name, null));
                }

                return at;
            }

            // The node of the block an INSERT of the origin names: its copy, where it is brought, else the block of the name here.
            int OfOrigin(string name)
                => origin.Blocks.TryGetValue(name, out DxfFileBlock block) && byBlock.TryGetValue(block, out int at) ? at : Named(name);

            void Visit(int at)
            {
                RoundNode node = nodes[at];
                node.Index = counter;
                node.Low = counter;
                counter++;
                component.Push(at);
                node.OnStack = true;
                node.Next = new List<int>();

                if (node.Brought != null)
                {
                    foreach (DxfItem item in node.Brought.Items.Snapshot())
                    {
                        if (!item.IsRemoved && item.Type == "INSERT")
                        {
                            node.Next.Add(OfOrigin(DxfReader.NameOf(origin.ValueOf(item, 2) ?? string.Empty)));
                        }
                    }
                }
                else
                {
                    if (Blocks.TryGetValue(node.Name, out DxfFileBlock block) && (block.Head == null || !block.Head.IsRemoved))
                    {
                        foreach (DxfItem item in block.Items.Snapshot())
                        {
                            if (!item.IsRemoved && item.Type == "INSERT")
                            {
                                node.Next.Add(Named(DxfReader.NameOf(ValueOf(item, 2) ?? string.Empty)));
                            }
                        }
                    }

                    if (at == ownerAt)
                    {
                        node.Next.AddRange(ownerNext);
                    }
                }

                path.Push(new KeyValuePair<int, int>(at, 0));
            }
        }

        /// <summary>
        /// The names AutoCAD gives blocks itself (<c>*U1</c>) that the INSERTs of blocks of the origin to be copied give as they
        /// are written: of a block the origin has not, as an INSERT of a block among them is written naming its copy.
        /// </summary>
        private static HashSet<string> AnonymousNamesKept(DxfFile origin, List<DxfFileBlock> blocks)
        {
            var copied = new HashSet<DxfFileBlock>(blocks);
            var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfFileBlock block in blocks)
            {
                foreach (DxfItem item in block.Items.Snapshot())
                {
                    if (!item.IsRemoved && item.Type == "INSERT")
                    {
                        string name = DxfReader.NameOf(origin.ValueOf(item, 2) ?? string.Empty);

                        if (IsAnonymous(name) && !(origin.Blocks.TryGetValue(name, out DxfFileBlock named) && copied.Contains(named)))
                        {
                            kept.Add(name);
                        }
                    }
                }
            }

            return kept;
        }

        /// <summary>Whether a block's name is that of a layout: model space, or paper space and its other layouts (<c>*Paper_Space0</c> and on).</summary>
        private static bool IsLayoutName(string name)
            => DxfBlockTableRecord.IsModelName(name) || name.StartsWith(DxfBlockTableRecord.PaperSpace, StringComparison.OrdinalIgnoreCase);

        /// <summary>A block on the walk of <see cref="CheckRounds"/>: its name here, the block of the origin it is a copy of where it is brought, the blocks its INSERTs place, and where the walk found it.</summary>
        private sealed class RoundNode
        {
            internal RoundNode(string name, DxfFileBlock brought)
            {
                Name = name;
                Brought = brought;
            }

            internal string Name { get; }

            internal DxfFileBlock Brought { get; }

            internal List<int> Next { get; set; }

            internal int Index { get; set; } = -1;

            internal int Low { get; set; }

            internal bool OnStack { get; set; }
        }

        /// <summary>
        /// The TEXTs an MTEXT of a block of another drawing is written as in this drawing of R12, which has no MTEXT, as
        /// <see cref="DxfMText"/> makes them; null for any other entity, or a drawing of R13 or later.
        /// </summary>
        private List<DxfEntity> AsTexts(DxfItem item)
            => !Style.Markers && item.Type == "MTEXT" && DxfEntity.Of(item) is DxfMText mtext ? mtext.R12Texts(this) : null;

        /// <summary>The names of the records of the tables a copy of another drawing needs and this drawing has not, by table, in the order found.</summary>
        private sealed class RecordsToCopy
        {
            internal List<string> Styles { get; } = new List<string>();

            internal List<string> Linetypes { get; } = new List<string>();

            internal List<string> Layers { get; } = new List<string>();

            internal List<string> DimStyles { get; } = new List<string>();

            /// <summary>The records of the tables and objects of the origin by handle, read once for the pointers of the records copied.</summary>
            internal Dictionary<ulong, KeyValuePair<Rec, bool>> Objects { get; set; }
        }

        /// <summary>
        /// Whether a record of a table, named by a copy of an entity made from another drawing (<paramref name="origin"/>), is
        /// copied into this one with it when it is added, as <see cref="Adopt"/> copies one this drawing has not: the origin has
        /// it, and its records are copied as they are.
        /// </summary>
        internal bool CopiesRecordFrom(DxfFile origin, string table, string name)
            => origin != null && origin != Template && origin != this && (RecordsCopy(origin) || CopiesAcross(table)) && FindRecord(origin, table, DxfReader.NameOf(name).Trim()) != null;

        /// <summary>Whether the records of a table are copied by name between drawings of R12 and of R13 or later: a layer and a text style, which both forms hold alike.</summary>
        private static bool CopiesAcross(string table) => table == "LAYER" || table == "STYLE";

        /// <summary>
        /// Whether a dimension style of the origin, of R13 or later, is copied into this drawing of R12 in the form of R12, as
        /// <see cref="CopyDimStyleIntoR12"/> writes it: so that a dimension copied into R12 names a style the drawing has.
        /// </summary>
        private bool DimStyleIntoR12(DxfFile origin, string table) => table == "DIMSTYLE" && !Style.Markers && origin.Style.Markers;

        /// <summary>Whether the records of the tables of another drawing are copied into this one as they are: both of R12, or both of R13 or later.</summary>
        private bool RecordsCopy(DxfFile origin) => origin.Style.Markers == Style.Markers;

        /// <summary>
        /// Notes the records of the tables the records of an entity of the origin name that this drawing has not: its layer (8)
        /// and linetype (6), the text style of a text (7), the dimension style of a dimension, a leader or a tolerance (3).
        /// </summary>
        private void NeedsOf(DxfFile origin, List<Rec> records, RecordsToCopy needs, List<DxfFileBlock> blocks)
        {
            foreach (Rec record in records)
            {
                DxfReader.Record read = origin.Record(record);
                Need(origin, "LAYER", read.Text(8), needs, blocks);
                Need(origin, "LTYPE", read.Text(6), needs, blocks);

                switch (record.Type)
                {
                    case "TEXT":
                    case "MTEXT":
                    case "ATTRIB":
                    case "ATTDEF":
                        Need(origin, "STYLE", read.Text(7), needs, blocks);
                        break;
                    case "DIMENSION":
                    case "ARC_DIMENSION":
                    case "LARGE_RADIAL_DIMENSION":
                    case "LEADER":
                    case "TOLERANCE":
                        Need(origin, "DIMSTYLE", read.Text(3), needs, blocks);

                        // The records its overrides of the style name (DIMBLK, DIMLTYPE, DIMTXSTY ...), as 1005s of ACAD DSTYLE; none
                        // for a variable this drawing's version has not, whose override is left out.
                        if (RecordsCopy(origin))
                        {
                            List<Pair> pairs = PairsOf(record);
                            bool overrides = false;

                            for (int i = DataAt(record, pairs); i < pairs.Count; i++)
                            {
                                overrides = origin.DimensionOverrides(record, pairs, i, overrides);

                                if (overrides && pairs[i].Code == 1005 && HasVariable(origin, record, pairs[i - 1]))
                                {
                                    NeedPointed(origin, origin.Text(record, pairs[i]), needs, blocks);
                                }
                            }
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Notes a record of a table of the origin that this drawing has not of its name, where the origin has it, and what it
        /// names in turn: a layer's linetype, a linetype's text styles, a dimension style's text style, linetypes and arrow blocks.
        /// A layer and a text style are noted between drawings of any versions; the others where <see cref="RecordsCopy"/> says.
        /// </summary>
        private void Need(DxfFile origin, string table, string written, RecordsToCopy needs, List<DxfFileBlock> blocks)
        {
            string name = written == null ? string.Empty : DxfReader.NameOf(written).Trim();
            List<string> list = table == "STYLE" ? needs.Styles : table == "LTYPE" ? needs.Linetypes : table == "LAYER" ? needs.Layers : needs.DimStyles;

            if (name.Length == 0 || list.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) || (!CopiesAcross(table) && !RecordsCopy(origin) && !DimStyleIntoR12(origin, table))
                || (table == "LTYPE" && (string.Equals(name, "BYLAYER", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "BYBLOCK", StringComparison.OrdinalIgnoreCase)))
                || FindRecord(this, table, name) != null)
            {
                return;
            }

            Rec? theirs = FindRecord(origin, table, name);

            if (theirs == null)
            {
                return;
            }

            list.Add(name);

            if (!RecordsCopy(origin))
            {
                return;
            }

            // What the record copied points at, by handle, or names, a layer its linetype; not by a group this drawing's
            // version has not, which is left out of the copy.
            List<Pair> pairs = PairsOf(theirs.Value);

            for (int i = 1; i < pairs.Count; i++)
            {
                int code = pairs[i].Code;

                if (table == "LAYER" && code == 6)
                {
                    Need(origin, "LTYPE", origin.Text(theirs.Value, pairs[i]), needs, blocks);
                }

                if (IsPointer(code) && SinceOf(table, code, true) <= Style.Version)
                {
                    NeedPointed(origin, origin.Text(theirs.Value, pairs[i]), needs, blocks);
                }
            }
        }

        /// <summary>
        /// Notes the record of the origin a handle points at, where this drawing has not one of its name: a text style or a
        /// linetype as <see cref="Need"/> notes it, a block (an arrow of a dimension style) among the blocks to copy.
        /// </summary>
        private void NeedPointed(DxfFile origin, string pointer, RecordsToCopy needs, List<DxfFileBlock> blocks)
        {
            if (!TryHex(pointer.Trim(), out ulong handle))
            {
                return;
            }

            needs.Objects = needs.Objects ?? ObjectsOf(origin);

            if (!needs.Objects.TryGetValue(handle, out KeyValuePair<Rec, bool> pointed) || !pointed.Value)
            {
                return;
            }

            string named = origin.Record(pointed.Key).Text(2);

            switch (pointed.Key.Type)
            {
                case "STYLE":
                case "LTYPE":
                    Need(origin, pointed.Key.Type, named, needs, blocks);
                    break;
                case "BLOCK_RECORD":
                    // An arrow block of a dimension style, copied under its name where this drawing has not one of it.
                    if (named != null && origin.Blocks.TryGetValue(DxfReader.NameOf(named), out DxfFileBlock arrow) && !blocks.Contains(arrow)
                        && !Blocks.ContainsKey(arrow.Name) && (_blockCopies == null || !_blockCopies.ContainsKey(arrow)))
                    {
                        blocks.Add(arrow);
                    }

                    break;
            }
        }

        /// <summary>The record of a table of a file by its name, letters of either case the same; null where the table has none.</summary>
        private Rec? FindRecord(DxfFile file, string table, string name)
        {
            if (file == this || _tableNames == null)
            {
                return file.TableRecord(table, name, true);
            }

            // A drawing copied from is read, never written: what is read of it is kept here, for this call.
            CopiedDrawing copied = CopiedFrom(file);

            if (!copied.Names.TryGetValue(table, out TableNames names))
            {
                names = file.ReadTableNames(table);
                copied.Names.Add(table, names);
            }

            return name != null && names.Trimmed.TryGetValue(name, out Rec found) ? found : (Rec?)null;
        }

        /// <summary>
        /// The records of the TABLES and OBJECTS sections of a file by handle, as <see cref="Objects"/> reads them: this file's
        /// own, or another's, a drawing copied from, kept here while a call holds the records of the tables by name, that
        /// drawing only read, so that several threads may copy from it at once.
        /// </summary>
        private Dictionary<ulong, KeyValuePair<Rec, bool>> ObjectsOf(DxfFile file)
        {
            if (file == this || _tableNames == null)
            {
                return file.Objects();
            }

            CopiedDrawing copied = CopiedFrom(file);
            return copied.Objects ?? (copied.Objects = file.ReadObjects());
        }

        /// <summary>What is kept of a drawing copied from while a call holds the records of the tables by name: read again where it was written since.</summary>
        private CopiedDrawing CopiedFrom(DxfFile file)
        {
            int at = Volatile.Read(ref file._editCount);

            if (!_copiedFrom.TryGetValue(file, out CopiedDrawing copied) || copied.At != at)
            {
                copied = new CopiedDrawing(at);
                _copiedFrom[file] = copied;
            }

            return copied;
        }

        /// <summary>The records of the tables by name and the objects by handle of a drawing copied from, as it stood when they were read.</summary>
        private sealed class CopiedDrawing
        {
            internal CopiedDrawing(int at)
            {
                At = at;
            }

            /// <summary>How many changes that drawing had had.</summary>
            internal int At { get; }

            /// <summary>Its records of the tables by name, type by type.</summary>
            internal Dictionary<string, TableNames> Names { get; } = new Dictionary<string, TableNames>(StringComparer.Ordinal);

            /// <summary>Its objects by handle; null until read.</summary>
            internal Dictionary<ulong, KeyValuePair<Rec, bool>> Objects { get; set; }
        }

        /// <summary>
        /// Copies records of a table of another drawing into this one by their names, as AutoCAD's deep clone between databases
        /// copies those an entity names: each with a handle of its own, this drawing's table as its owner, its groups as they are
        /// but its handle, owner, reactors and extension dictionary; each pointer taken to the record of the same name here, or
        /// left out; the applications of its extended data registered. Between drawings of R12 and of R13 or later, a layer is
        /// added with its colour, linetype and flags, a text style with its flags, height, width, slant and fonts, and the other
        /// records are not copied.
        /// </summary>
        private void CopyRecords(DxfFile origin, List<string> names, string table)
        {
            foreach (string name in names)
            {
                if (FindRecord(this, table, name) != null)
                {
                    continue;
                }

                Rec? found = FindRecord(origin, table, name);

                if (found == null)
                {
                    continue;
                }

                Rec record = found.Value;

                if (!RecordsCopy(origin))
                {
                    if (table == "LAYER")
                    {
                        DxfReader.Record read = origin.Record(record);
                        int colour = int.TryParse(read.Text(62)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) && c != 0 ? c : 7;
                        string linetype = read.Text(6)?.Trim();
                        int flags = int.TryParse(read.Text(70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f & 7 : 0;
                        AddLayer(name, colour, null, linetype != null && HasLinetype(linetype) ? linetype : "Continuous", -3, flags, true);
                    }
                    else if (DimStyleIntoR12(origin, table))
                    {
                        CopyDimStyleIntoR12(origin, record, name);
                    }
                    else if (table == "STYLE")
                    {
                        // A text style with its flags, fixed height, width, slant and fonts, which R12 holds as later versions do.
                        DxfReader.Record read = origin.Record(record);
                        int flags = int.TryParse(read.Text(70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f & 5 : 0;
                        double Real(int code, double fallback) => Numbers.TryRead(read.Text(code), out double value) && Guard.IsFinite(value) ? value : fallback;

                        if (AddTextStyle(name, flags, Real(40, 0.0), Real(41, 1.0), Real(50, 0.0), read.Text(3) ?? string.Empty, read.Text(4) ?? string.Empty) != null)
                        {
                            Edited();
                            GeometryHelperLog.Info("DxfFile: the STYLE " + name + " was copied with an entity of another drawing.");
                        }
                    }

                    continue;
                }

                List<Pair> pairs = PairsOf(record);
                int data = DataAt(record, pairs);
                var groups = new List<KeyValuePair<int, string>>();
                var applications = new List<string>();
                string marker = null;
                int markers = 0;
                bool braced = false;
                bool owned = false;
                bool older = Style.Version < origin.Style.Version;
                int seventies = 0;
                var dropped = new List<string>();

                for (int i = 1; i < pairs.Count; i++)
                {
                    int code = pairs[i].Code;
                    string text = origin.Text(record, pairs[i]);

                    if (code == 102)
                    {
                        braced = text.Trim().StartsWith("{", StringComparison.Ordinal);
                        continue;
                    }

                    if (braced || code == 999)
                    {
                        continue;
                    }

                    if (i >= data)
                    {
                        if (code == 1001)
                        {
                            applications.Add(text.Trim());
                        }

                        if (code != 1005)
                        {
                            groups.Add(new KeyValuePair<int, string>(code, text));
                        }

                        continue;
                    }

                    // The handle (a DIMSTYLE's 105; its 5 is an arrow block of R12), the owner and the markers are this drawing's.
                    if (code == 105 || (code == 5 && table != "DIMSTYLE") || (code == 330 && !owned))
                    {
                        owned |= code == 330;
                        continue;
                    }

                    if (code == 100)
                    {
                        if (++markers == 2)
                        {
                            marker = text.Trim();
                        }

                        continue;
                    }

                    // A group this drawing's version has not is left out, as AutoCAD's SAVEAS to that version leaves it out: a
                    // DIMSTYLE's DIMFXLEN (49) and a LAYER's material (347), which R2007 brought, in a drawing of R2000.
                    seventies += code == 70 ? 1 : 0;

                    if (older && SinceOf(table, code, seventies > 1) > Style.Version)
                    {
                        dropped.Add(code.ToString(CultureInfo.InvariantCulture));
                        continue;
                    }

                    if (IsPointer(code))
                    {
                        string now = Equivalent(origin, text);

                        if (now != null)
                        {
                            groups.Add(new KeyValuePair<int, string>(code, now));
                        }

                        continue;
                    }

                    groups.Add(new KeyValuePair<int, string>(code, text));
                }

                foreach (string application in applications)
                {
                    EnsureApplication(application);
                }

                DxfItem made = AddTableRecord(table, marker ?? MarkerOf(table), b =>
                {
                    foreach (KeyValuePair<int, string> group in groups)
                    {
                        b.Group(group.Key, group.Value);
                    }
                });

                if (made != null)
                {
                    Edited();
                    GeometryHelperLog.Info("DxfFile: the " + table + " " + name + " was copied with an entity of another drawing.");

                    if (dropped.Count > 0)
                    {
                        GeometryHelperLog.Debug("DxfFile: the " + table + " " + name + " was copied without its groups " + string.Join(", ", dropped) + ", a DXF " + VersionSaid() + " having none.");
                    }
                }
            }
        }

        /// <summary>
        /// Copies a dimension style of the origin, of R13 or later, into this drawing of R12, as AutoCAD's SAVEAS to R12 writes
        /// one: its name, its flags and the variables R12 has, in their places; its arrow blocks (DIMBLK, DIMBLK1, DIMBLK2, by
        /// handle 342 to 344) by name (5, 6, 7) after its texts where this drawing has a block of the name; the rest (its text
        /// style, lineweights, the variables of later versions) left out, said in the log at Debug.
        /// </summary>
        private void CopyDimStyleIntoR12(DxfFile origin, Rec record, string name)
        {
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var groups = new List<KeyValuePair<int, string>>();
            var arrows = new List<KeyValuePair<int, string>>();
            var dropped = new List<string>();
            bool braced = false;
            int seventies = 0;
            int heads = 0;

            for (int i = 1; i < data; i++)
            {
                int code = pairs[i].Code;
                string text = origin.Text(record, pairs[i]);

                if (code == 102)
                {
                    braced = text.Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced || code == 999 || code == 105 || code == 5 || code == 330 || code == 100)
                {
                    continue;
                }

                seventies += code == 70 ? 1 : 0;

                if (code >= 340 && code <= 347)
                {
                    if (R12Override(origin, code.ToString(CultureInfo.InvariantCulture), text, out int r12Code, out string arrow))
                    {
                        arrows.Add(new KeyValuePair<int, string>(r12Code, arrow));
                    }
                    else
                    {
                        dropped.Add(code.ToString(CultureInfo.InvariantCulture));
                    }

                    continue;
                }

                if (SinceOf("DIMSTYLE", code, seventies > 1) > Style.Version || IsPointer(code))
                {
                    dropped.Add(code.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                groups.Add(new KeyValuePair<int, string>(code, text));

                // The arrows by name go after the name, the flags and the texts (3, 4), as R12 writes them.
                if (code == 2 || (code == 70 && seventies == 1) || code == 3 || code == 4)
                {
                    heads = groups.Count;
                }
            }

            arrows.Sort((a, b) => a.Key.CompareTo(b.Key));
            groups.InsertRange(heads, arrows);
            DxfItem made = AddTableRecord("DIMSTYLE", MarkerOf("DIMSTYLE"), b =>
            {
                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });

            if (made != null)
            {
                Edited();
                GeometryHelperLog.Info("DxfFile: the DIMSTYLE " + name + " was copied with an entity of another drawing, in the form of R12.");

                if (dropped.Count > 0)
                {
                    GeometryHelperLog.Debug("DxfFile: the DIMSTYLE " + name + " was copied without its groups " + string.Join(", ", dropped) + ", a DXF " + VersionSaid() + " having none.");
                }
            }
        }

        /// <summary>The subclass marker of a record of a table.</summary>
        private static string MarkerOf(string table)
        {
            switch (table)
            {
                case "STYLE": return "AcDbTextStyleTableRecord";
                case "LTYPE": return "AcDbLinetypeTableRecord";
                case "LAYER": return "AcDbLayerTableRecord";
                default: return "AcDbDimStyleTableRecord";
            }
        }

        /// <summary>
        /// The first name of an anonymous block's letter and a number, from 1 up, that this file has no block of and no block
        /// being copied is given: sought from the number last given, while a call holds the records of the tables by name, as
        /// names are only taken then. Nor is it a name an INSERT of this file gives, or one of the blocks being copied gives
        /// as it is written (<paramref name="kept"/>), of a block neither drawing has: such an INSERT would come to place the
        /// copy, which may insert that INSERT's block, a *U1 copied as *U1 placing itself.
        /// The names this file's INSERTs give are read once into <paramref name="now"/> for all the blocks being named.
        /// </summary>
        private string FreeAnonymousName(string name, HashSet<string> kept, ref HashSet<string> now)
        {
            int digits = 1;

            while (digits < name.Length && !char.IsDigit(name[digits]))
            {
                digits++;
            }

            string prefix = name.Substring(0, digits);
            int from = _anonymousFrom != null && _anonymousFrom.TryGetValue(prefix, out int taken) ? taken : 1;

            for (int n = from; ; n++)
            {
                string candidate = prefix + n.ToString(CultureInfo.InvariantCulture);

                if (!Blocks.ContainsKey(candidate) && !_blockCopies.Values.Any(v => string.Equals(v, candidate, StringComparison.OrdinalIgnoreCase))
                    && !kept.Contains(candidate) && !InsertNames(candidate, ref now))
                {
                    if (_anonymousFrom != null)
                    {
                        _anonymousFrom[prefix] = n;
                    }

                    return candidate;
                }
            }
        }

        /// <summary>
        /// A record of another file written in this one's style: its type, the handle and owner given, its space in paper space,
        /// and its groups as they are, but its subclass markers in a file of R12.
        /// </summary>
        private Rec Copied(DxfFile source, Rec record, string handle, string owner, bool paper)
        {
            Writing style = Style;
            var b = new Builder(this);
            b.Group(0, record.Type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (owner != null)
            {
                b.Group(330, owner);
            }

            if (paper && !style.Markers)
            {
                b.Group(67, "1");
            }

            List<Pair> pairs = PairsOf(record);

            for (int i = 1; i < pairs.Count; i++)
            {
                string value = source.Text(record, pairs[i]);

                if (pairs[i].Code == 100 && !style.Markers)
                {
                    continue;
                }

                b.Group(pairs[i].Code, value);

                if (paper && style.Markers && pairs[i].Code == 100 && value.Trim() == "AcDbEntity")
                {
                    b.Group(67, "1");
                }
            }

            return b.Done(record.Type);
        }

        /// <summary>
        /// A record of a file of R12 written in the forms of R13 on, as AutoCAD writes it: its type, the handle and owner given,
        /// the marker AcDbEntity with its space in paper space and the groups all entities have, then each subclass marker of
        /// its type in AutoCAD's order with the groups of its own that go under it, then its extended data; every value as the
        /// record has it.
        /// </summary>
        private Rec Marked(DxfFile source, Rec record, string handle, string owner, bool paper)
        {
            var b = new Builder(this);
            b.Group(0, record.Type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (owner != null)
            {
                b.Group(330, owner);
            }

            b.Group(100, "AcDbEntity");

            if (paper)
            {
                b.Group(67, "1");
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            string[] markers = MarkersOf(record, pairs, out HashSet<int> late, out bool allLate);

            for (int i = 1; i < data; i++)
            {
                if (IsCommon(pairs[i].Code) && !Placed(pairs[i].Code))
                {
                    b.Group(pairs[i].Code, source.Text(record, pairs[i]));
                }
            }

            for (int m = 0; m < markers.Length; m++)
            {
                b.Group(100, markers[m]);
                bool last = m == markers.Length - 1;

                for (int i = 1; i < data; i++)
                {
                    int code = pairs[i].Code;

                    if (IsCommon(code) || Placed(code) || code == 100 || code == 102)
                    {
                        continue;
                    }

                    bool lateCode = allLate || (late != null && late.Contains(code));

                    if (markers.Length == 1 || lateCode == last)
                    {
                        b.Group(code, source.Text(record, pairs[i]));
                    }
                }
            }

            for (int i = data; i < pairs.Count; i++)
            {
                b.Group(pairs[i].Code, source.Text(record, pairs[i]));
            }

            return b.Done(record.Type);
        }

        /// <summary>Whether a group is one a record written anew is given afresh: its handle, owner and space.</summary>
        private static bool Placed(int code) => code == 5 || code == 105 || code == 330 || code == 360 || code == 67 || code == 410;

        /// <summary>
        /// The subclass markers AutoCAD writes a record of a type of R12 with from R13 on, in its order, as ezdxf writes them; the
        /// codes that go under the last where the type has two (an ARC's angles, a TEXT's vertical justification, an ATTRIB's
        /// tag, flags, length and vertical justification), or whether all go under the last (a VERTEX, whose AcDbVertex has
        /// none). Null for a type with no form of R13 here, as a DIMENSION.
        /// </summary>
        private string[] MarkersOf(Rec record, List<Pair> pairs, out HashSet<int> late, out bool allLate)
        {
            late = null;
            allLate = false;

            switch (record.Type)
            {
                case "LINE": return new[] { "AcDbLine" };
                case "POINT": return new[] { "AcDbPoint" };
                case "CIRCLE": return new[] { "AcDbCircle" };
                case "SOLID":
                case "TRACE": return new[] { "AcDbTrace" };
                case "3DFACE": return new[] { "AcDbFace" };
                case "SHAPE": return new[] { "AcDbShape" };
                case "SEQEND": return new string[0];
                case "ARC":
                    late = new HashSet<int> { 50, 51 };
                    return new[] { "AcDbCircle", "AcDbArc" };
                case "TEXT":
                    late = new HashSet<int> { 73 };
                    return new[] { "AcDbText", "AcDbText" };
                case "ATTDEF":
                    late = new HashSet<int> { 3, 2, 70, 73, 74, 280 };
                    return new[] { "AcDbText", "AcDbAttributeDefinition" };
                case "ATTRIB":
                    late = new HashSet<int> { 2, 70, 73, 74, 280 };
                    return new[] { "AcDbText", "AcDbAttribute" };
                case "INSERT":
                    return new[] { FirstWhole(record, pairs, 70) > 1 || FirstWhole(record, pairs, 71) > 1 ? "AcDbMInsertBlock" : "AcDbBlockReference" };
                case "POLYLINE":
                {
                    int flags = FirstWhole(record, pairs, 70);
                    return new[] { (flags & 8) != 0 ? "AcDb3dPolyline" : (flags & 16) != 0 ? "AcDbPolygonMesh" : (flags & 64) != 0 ? "AcDbPolyFaceMesh" : "AcDb2dPolyline" };
                }

                case "VERTEX":
                {
                    int flags = FirstWhole(record, pairs, 70);

                    if ((flags & 192) == 128)
                    {
                        return new[] { "AcDbFaceRecord" };
                    }

                    allLate = true;
                    return new[] { "AcDbVertex", (flags & 32) != 0 ? "AcDb3dPolylineVertex" : (flags & 128) != 0 ? "AcDbPolyFaceMeshVertex" : (flags & 64) != 0 ? "AcDbPolygonMeshVertex" : "AcDb2dVertex" };
                }

                default: return null;
            }
        }

        /// <summary>A record of another file in this one's forms: given AutoCAD's markers, written in the forms of R12, or copied as it is.</summary>
        private Rec Written(DxfFile source, Rec record, string handle, string owner, bool paper, bool marking, bool unmarking, bool follows)
        {
            if (marking)
            {
                return Marked(source, record, handle, owner, paper);
            }

            return unmarking ? Unmarked(source, record, handle, paper, follows) : Copied(source, record, handle, owner, paper);
        }

        // The groups each type of R12 has of its own, beside its layer, linetype and colour, as the DXF reference of R12 and
        // ezdxf give them; an ATTRIB's and an ATTDEF's tag, prompt, flags, length and vertical justification from their part
        // AcDbAttribute or AcDbAttributeDefinition, every other group of a text from its part AcDbText.
        private static readonly Dictionary<string, HashSet<int>> R12Groups = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal)
        {
            ["LINE"] = new HashSet<int> { 39, 10, 20, 30, 11, 21, 31, 210, 220, 230 },
            ["POINT"] = new HashSet<int> { 39, 10, 20, 30, 50, 210, 220, 230 },
            ["CIRCLE"] = new HashSet<int> { 39, 10, 20, 30, 40, 210, 220, 230 },
            ["ARC"] = new HashSet<int> { 39, 10, 20, 30, 40, 50, 51, 210, 220, 230 },
            ["SOLID"] = new HashSet<int> { 39, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 210, 220, 230 },
            ["TRACE"] = new HashSet<int> { 39, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 210, 220, 230 },
            ["3DFACE"] = new HashSet<int> { 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 70 },
            ["SHAPE"] = new HashSet<int> { 39, 10, 20, 30, 40, 2, 50, 41, 51, 210, 220, 230 },
            ["TEXT"] = new HashSet<int> { 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 73, 11, 21, 31, 210, 220, 230 },
            ["ATTDEF"] = new HashSet<int> { 39, 10, 20, 30, 40, 1, 3, 2, 70, 73, 50, 41, 51, 7, 71, 72, 74, 11, 21, 31, 210, 220, 230 },
            ["ATTRIB"] = new HashSet<int> { 39, 10, 20, 30, 40, 1, 2, 70, 73, 50, 41, 51, 7, 71, 72, 74, 11, 21, 31, 210, 220, 230 },
            ["INSERT"] = new HashSet<int> { 66, 2, 10, 20, 30, 41, 42, 43, 50, 70, 71, 44, 45, 210, 220, 230 },
            ["POLYLINE"] = new HashSet<int> { 66, 10, 20, 30, 39, 70, 40, 41, 71, 72, 73, 74, 75, 210, 220, 230 },
            ["VERTEX"] = new HashSet<int> { 10, 20, 30, 40, 41, 42, 70, 50, 71, 72, 73, 74 },
            ["DIMENSION"] = new HashSet<int> { 2, 3, 10, 20, 30, 11, 21, 31, 12, 22, 32, 70, 1, 13, 23, 33, 14, 24, 34, 15, 25, 35, 16, 26, 36, 40, 50, 51, 52, 53, 210, 220, 230 },
            ["SEQEND"] = new HashSet<int>(),
        };

        // The groups of an attribute that stand in its part AcDbAttribute or AcDbAttributeDefinition, not in AcDbText.
        private static readonly HashSet<int> AttributeGroups = new HashSet<int> { 2, 3, 70, 73, 74 };

        /// <summary>
        /// A record of R13 or later written in the forms of R12, as AutoCAD saves a drawing as R12: its type, the handle given,
        /// its space in paper space, its layer, linetype and colour, then the groups R12 has for its type, each the first of its
        /// code from the part it belongs to, in the order the record has them; its extended data. Left out: the subclass
        /// markers, the groups of the applications, the owner, every group R12 has not (lineweight, true colour, material,
        /// plot style), and an object embedded after a group 101, as an MTEXT attribute's. Groups whose codes the parts share,
        /// as an ATTRIB's 71 and 72 of AcDbText and of AcDbAttribute, are read from the part R12 means. A POLYLINE or an INSERT
        /// followed by its vertices or attributes says so (66 of 1).
        /// </summary>
        private Rec Unmarked(DxfFile source, Rec record, string handle, bool paper, bool follows)
        {
            HashSet<int> own = R12Groups[record.Type];
            bool attribute = record.Type == "ATTRIB" || record.Type == "ATTDEF";
            var b = new Builder(this);
            b.Group(0, record.Type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (paper)
            {
                b.Group(67, "1");
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var written = new HashSet<int>();
            string part = string.Empty;
            bool braced = false;
            bool embedded = false;
            bool followsWritten = false;

            for (int i = 1; i < data; i++)
            {
                int code = pairs[i].Code;
                string value = source.Text(record, pairs[i]);

                if (code == 102)
                {
                    braced = value.Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced || embedded || code == 999)
                {
                    continue;
                }

                if (code == 101)
                {
                    embedded = true;
                    continue;
                }

                if (code == 100)
                {
                    part = value.Trim();
                    continue;
                }

                bool common = part.Length == 0 || part == "AcDbEntity";

                if (common)
                {
                    if ((code == 8 || code == 6 || code == 62) && written.Add(code))
                    {
                        b.Group(code, value);
                    }

                    continue;
                }

                if (!own.Contains(code))
                {
                    continue;
                }

                if (attribute && AttributeGroups.Contains(code) != part.StartsWith("AcDbAttribute", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!written.Add(code))
                {
                    continue;
                }

                if (code == 66)
                {
                    followsWritten = true;
                    b.Group(66, follows ? "1" : value);
                    continue;
                }

                // The vertices or attributes that follow are said to follow before the rest of the head's own groups.
                if (follows && !followsWritten && own.Contains(66))
                {
                    followsWritten = written.Add(66);
                    b.Group(66, "1");
                }

                b.Group(code, value);
            }

            if (follows && !followsWritten && own.Contains(66) && written.Add(66))
            {
                b.Group(66, "1");
            }

            for (int i = data; i < pairs.Count; i++)
            {
                if (pairs[i].Code != 999)
                {
                    b.Group(pairs[i].Code, source.Text(record, pairs[i]));
                }
            }

            return b.Done(record.Type);
        }

        /// <summary>The file a copy was made from, whose objects its pointers name; null for a file read or made in code.</summary>
        internal DxfFile Origin => _origin;

        /// <summary>
        /// The drawing whose objects the handles of an entity of this file name: this one; for a copy in no file, the drawing it
        /// was copied from; null for an entity made in code, whose file is no drawing's.
        /// </summary>
        internal DxfFile NamingDrawing => !_detached ? this : _origin != null && _origin != Template ? _origin : null;

        /// <summary>
        /// The file whose block table holds the block an INSERT places: this one for an INSERT in it; for a copy in no file, the
        /// drawing it was copied from while the block it was copied with is still there (renamed or not); null where that block
        /// was erased, or the copy was made from none.
        /// </summary>
        internal DxfFile BlockSource(DxfItem insert)
            => !_detached ? this : insert.OriginBlock != null && !insert.OriginBlock.Head.IsRemoved ? _origin : null;

        /// <summary>
        /// A record of a copy with its pointers into the drawing it was copied from taken to this one (<paramref name="target"/>):
        /// each soft or hard pointer (320 to 369, 390 to 399, 480 and 481) to the object of the same name there, a table's record
        /// by its name, an object of the named object dictionary (a material, a plot style, a visual style) by the names of its
        /// dictionary and of its entry; left out where the target has none of that name, so that the target's default holds. A
        /// handle of extended data (1005) is kept in its place as null (0), as AutoCAD's mapping of objects between drawings
        /// leaves it, or held back for the copy of an entity copied with it while blocks are being copied; but one
        /// of the overrides of a dimension style (ACAD DSTYLE, a 1070 of the variable then a 1005 of its record: DIMBLK, DIMLTYPE,
        /// DIMTXSTY and the others) names the record of the same name in the target, the two left out where it has none, said in
        /// the log; in a target of R12 an arrow block's (DIMBLK, DIMBLK1, DIMBLK2) names the block by its name, a 1070 of 5, 6 or
        /// 7 then a 1000, as R12 writes it, where the target has a block of that name. A pointer to an entity copied with the one being added, as a table cell's to an attribute definition (331) of the block
        /// copied with the table, names its copy; such a pointer left out takes its value (300) with it, the cell's count (179)
        /// made to agree. A LEADER whose annotation (340) is left out points to none (73 of 3). While blocks are being copied
        /// with an entity, a pointer to an entity of the drawing copied from, and a HATCH's handle of a boundary object (330),
        /// is held back, marked, to be taken to its copy once every entity is written (<see cref="TranslateDeferred"/>), and
        /// <paramref name="deferred"/> says so. A MULTILEADER's attribute definition (its 330 after its own marker) names the
        /// copy of the definition, or the definition of the same tag in the block here of the name of its block, as the block it
        /// places is the one of that name here; where there is none, the value given for it (its 177, 44 and 302) goes with it.
        /// A pointer to a block being copied with it whose record is not written yet is held back so too, to name the copy
        /// (<see cref="BlockToCome"/>): a MULTILEADER's content block (341 of the context, 344) and arrow blocks (342, 345), an
        /// ACAD_TABLE's picture (343) and the block of a cell (340), and the arrow block an override of a dimension style names
        /// (DIMBLK, DIMBLK1, DIMBLK2, DIMLDRBLK), whose 1070 waits with it. The record itself where nothing changes.
        /// </summary>
        private Rec Pointed(Rec record, DxfFile origin, DxfFile target, ref bool deferred)
        {
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int body = record.Type == "HATCH" || record.Type == "MULTILEADER" ? BodyAt(record, pairs, data) : -1;
            var b = new Builder(this);
            b.Raw(record, pairs[0]);
            bool changed = false;
            bool braced = false;
            bool overrides = false;
            bool cells = false;
            bool unannotated = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];
                int code = pair.Code;

                if (i >= data)
                {
                    overrides = DimensionOverrides(record, pairs, i, overrides);
                }

                // An override of a dimension style naming a record (DIMBLK, DIMLTYPE, DIMTXSTY and the others: a 1070 of its
                // code, then a 1005 of the record) names the record of the same name here; the two go where there is none.
                if (overrides && code == 1070 && i + 1 < pairs.Count && pairs[i + 1].Code == 1005)
                {
                    string now = target.Equivalent(origin, Text(record, pairs[i + 1]));
                    changed = true;

                    if (now != null)
                    {
                        b.Raw(record, pair);
                        b.Group(1005, now);
                    }
                    else if (target.R12Override(origin, Text(record, pair), Text(record, pairs[i + 1]), out int r12Code, out string arrow))
                    {
                        // An arrow block in a drawing of R12 is named, by the variable's group of R12 (5, 6, 7), as R12 writes it.
                        b.Group(1070, r12Code.ToString(CultureInfo.InvariantCulture));
                        b.Group(1000, arrow);
                    }
                    else if (target.BlockToCome(origin, Text(record, pairs[i + 1])))
                    {
                        // An arrow block copied with it but not yet written here waits for it.
                        b.Raw(record, pair);
                        b.Group(1005, DeferredMark + Text(record, pairs[i + 1]).Trim());
                        deferred = true;
                    }
                    else
                    {
                        GeometryHelperLog.Debug("DxfFile: the " + record.Type + "'s override " + Text(record, pair).Trim() + " names a record the drawing it is copied into has none of, and is left out.");
                    }

                    i++;
                    continue;
                }

                if (code == 102)
                {
                    braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (i >= data && code == 1005)
                {
                    // A handle of extended data keeps its place, as AutoCAD's copy into another drawing keeps it: null (0) where
                    // what it names stays behind, the copy's handle where that is copied with it (WblockCloneObjects, or the
                    // entities of a block copied, held back until every one is written).
                    string was = Text(record, pair);
                    changed = true;

                    if (target._deferred != null && origin.IsEntity(was))
                    {
                        b.Group(code, DeferredMark + was.Trim());
                        deferred = true;
                    }
                    else
                    {
                        b.Group(code, "0");
                    }

                    continue;
                }
                else if (i < data && !braced && (IsPointer(code) || (body >= 0 && i > body && code == 330)))
                {
                    string was = Text(record, pair);
                    changed = true;

                    // A multileader's attribute definition: its copy, which waits while its block is being copied, or its namesake.
                    if (code == 330 && record.Type == "MULTILEADER")
                    {
                        string attribute = target.CopyHandle(was);

                        if (attribute == null && target._deferred != null && target.CopyingBlockOf(origin, was))
                        {
                            b.Group(code, DeferredMark + was.Trim());
                            deferred = true;
                            continue;
                        }

                        attribute = attribute ?? target.AttributeNamesake(origin, was);

                        if (attribute != null)
                        {
                            b.Group(code, attribute);
                        }
                        else
                        {
                            i = AttributeEnd(pairs, i, data);
                        }

                        continue;
                    }

                    // A pointer to an entity, while blocks are being copied, waits for the copy of that entity.
                    if (target._deferred != null && origin.IsEntity(was))
                    {
                        b.Group(code, DeferredMark + was.Trim());
                        deferred = true;
                        continue;
                    }

                    // A boundary object's handle no copy is made of goes, the HATCH's counts made to agree.
                    if (code == 330)
                    {
                        continue;
                    }

                    string now = target.CopyHandle(was) ?? target.Equivalent(origin, was);

                    if (now != null)
                    {
                        b.Group(code, now);
                    }
                    else if (target.BlockToCome(origin, was))
                    {
                        // A block copied with it but not yet written here (a multileader's content or arrows, a table's picture
                        // or a cell's block) waits for it.
                        b.Group(code, DeferredMark + was.Trim());
                        deferred = true;
                    }
                    else if (code == 331 && record.Type == "ACAD_TABLE")
                    {
                        // A cell's attribute definition left out takes its value (300) with it, the cell's count made to agree.
                        cells = true;
                        i += i + 1 < data && pairs[i + 1].Code == 300 ? 1 : 0;
                    }
                    else if (code == 340 && record.Type == "LEADER")
                    {
                        unannotated = true;
                    }

                    continue;
                }

                b.Raw(record, pair);
            }

            if (!changed)
            {
                return record;
            }

            Rec made = Recounted(b.Done(record.Type));
            made = cells ? CellsRecounted(made) : made;

            // A leader whose annotation is left out points to none (73 of 3), as AutoCAD's copy of a leader alone leaves it.
            return unannotated && WithGroup(made, 73, "3", false, out Rec bare) ? bare : made;
        }

        /// <summary>
        /// The form of R12 of an override of an arrow block (DIMBLK, DIMBLK1, DIMBLK2: a 1070 of 342 to 344, then the 1005 of the
        /// block) for this drawing of R12: the variable's group of R12 (5, 6, 7) and the block's name, where this drawing has a
        /// block of that name or is given one by the copying; false otherwise, or where this drawing is not of R12.
        /// </summary>
        private bool R12Override(DxfFile origin, string variable, string pointer, out int r12Code, out string name)
        {
            r12Code = 0;
            name = null;

            if (Style.Markers || !origin.Style.Markers || !int.TryParse(variable.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                || !(DxfDimVariable.OfCode(code, false) is DxfDimVariable known) || known.Kind != DxfDimKind.Block || known.R12Code == 0
                || !TryHex(pointer.Trim(), out ulong handle) || !ObjectsOf(origin).TryGetValue(handle, out KeyValuePair<Rec, bool> found)
                || !found.Value || found.Key.Type != "BLOCK_RECORD")
            {
                return false;
            }

            name = DxfReader.NameOf(origin.Record(found.Key).Text(2) ?? string.Empty);

            // A block being copied with the entity is named so though it is written after it.
            bool coming = false;

            if (_blockCopies != null && origin.Blocks.TryGetValue(name, out DxfFileBlock copied) && _blockCopies.TryGetValue(copied, out string given))
            {
                name = given;
                coming = true;
            }

            r12Code = known.R12Code;
            return name.Length > 0 && (coming || Blocks.ContainsKey(name));
        }

        /// <summary>
        /// Whether a group of the extended data of a record stands in the list of overrides of a dimension style, that of ACAD
        /// opened by <c>1000 DSTYLE</c> and <c>1002 {</c> and closed by its <c>1002 }</c>: given whether the group before did.
        /// </summary>
        private bool DimensionOverrides(Rec record, List<Pair> pairs, int at, bool before)
        {
            Pair pair = pairs[at];

            if (pair.Code == 1001)
            {
                return false;
            }

            if (before)
            {
                return !(pair.Code == 1002 && Text(record, pair).Trim() == "}");
            }

            // Opened by its 1002 {, after 1000 DSTYLE of the application ACAD.
            if (pair.Code != 1002 || Text(record, pair).Trim() != "{" || at < 2 || pairs[at - 1].Code != 1000 || Text(record, pairs[at - 1]).Trim() != "DSTYLE")
            {
                return false;
            }

            for (int k = at - 2; k >= 0; k--)
            {
                if (pairs[k].Code == 1001)
                {
                    return string.Equals(Text(record, pairs[k]).Trim(), "ACAD", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        /// <summary>
        /// A record of a dimension, a leader or a tolerance of this file with the overrides of its dimension style (ACAD DSTYLE) that
        /// a drawing of an older version (<paramref name="target"/>) has no variable of left out, each a 1070 naming the variable
        /// and the value after it, as AutoCAD's SAVEAS to that version leaves them out: DIMLTYPE and DIMFXLEN, which R2007 brought,
        /// of a dimension added to a drawing of R2000, DIMDEC of one added to a drawing of R12. Said once in the log, at Debug. The
        /// record itself where nothing goes, or it is of another type.
        /// </summary>
        private Rec WithoutLaterOverrides(Rec record, DxfFile target)
        {
            if (!HasStyleOverrides(record.Type))
            {
                return record;
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            bool r12 = !Style.Markers;
            int version = target.Style.Version;
            var drop = new bool[pairs.Count];
            var dropped = new List<string>();
            bool overrides = false;

            for (int i = data; i < pairs.Count; i++)
            {
                bool before = overrides;
                overrides = DimensionOverrides(record, pairs, i, overrides);

                // Within the list, past its opening brace: a 1070 naming the variable, then its value.
                if (!before || !overrides || pairs[i].Code != 1070 || i + 1 >= pairs.Count)
                {
                    continue;
                }

                DxfDimVariable variable = int.TryParse(Text(record, pairs[i]).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                    ? DxfDimVariable.OfCode(code, r12)
                    : null;

                if (variable != null && variable.Since > version)
                {
                    drop[i] = true;
                    drop[i + 1] = true;
                    dropped.Add(variable.Name.ToUpperInvariant());
                }

                i++;
            }

            if (dropped.Count == 0)
            {
                return record;
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (!drop[i])
                {
                    b.Raw(record, pairs[i]);
                }
            }

            GeometryHelperLog.Debug("DxfFile: the " + record.Type + "'s overrides of " + string.Join(", ", dropped) + " were left out, a DXF " + target.VersionSaid() + " having no such variables.");
            return b.Done(record.Type);
        }

        /// <summary>Whether a type keeps overrides of its dimension style in its extended data of ACAD (DSTYLE): a dimension, a leader or a tolerance.</summary>
        private static bool HasStyleOverrides(string type)
            => type == "DIMENSION" || type == "ARC_DIMENSION" || type == "LARGE_RADIAL_DIMENSION" || type == "LEADER" || type == "TOLERANCE";

        /// <summary>
        /// Whether an override of a dimension style, in the list DSTYLE of a record of a file (<paramref name="origin"/>), is one of
        /// a variable this file's version has: by the 1070 that names it, at the index given; true for a variable the library does
        /// not know.
        /// </summary>
        private bool HasVariable(DxfFile origin, Rec record, Pair named)
        {
            DxfDimVariable variable = int.TryParse(origin.Text(record, named).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                ? DxfDimVariable.OfCode(code, !origin.Style.Markers)
                : null;
            return variable == null || variable.Since <= Style.Version;
        }

        /// <summary>
        /// The version that brought a group of a record of a table, as ezdxf gives it: of a LAYER, its plotting (290), lineweight
        /// (370) and plot style (390) R2000, its true colour and colour book (420, 430) R2004, its material (347, 348) R2007; of
        /// a DIMSTYLE, the version of the variable of the group, its handles (340 to 347) R2000 at the earliest, its second 70
        /// being DIMTFILLCLR of R2007, and DIMTXTDIRECTION (294) R2010. Nought for any other group, every version of R13 on having it.
        /// </summary>
        private static int SinceOf(string table, int code, bool second)
        {
            if (table == "LAYER")
            {
                switch (code)
                {
                    case 290:
                    case 370:
                    case 390: return 1015;
                    case 420:
                    case 430: return 1018;
                    case 347:
                    case 348: return 1021;
                    default: return 0;
                }
            }

            if (table != "DIMSTYLE" || (code == 70 && !second))
            {
                return 0;
            }

            DxfDimVariable variable = DxfDimVariable.OfCode(code, false);
            int since = variable == null ? 0 : variable.Since;
            return code >= 340 && code <= 347 ? Math.Max(since, 1015) : since > 1009 ? since : 0;
        }

        /// <summary>
        /// A HATCH whose boundary objects were left out of a copy, its counts made to agree with what is left: each path's count
        /// of boundary objects (97) the number of pointers (330) that follow it, nought where none does, as AutoCAD reads that
        /// many after it, a spline edge's own 97, its count of fit points, kept; and, with no boundary object left in the whole record, its associativity (71) nought, as AutoCAD's
        /// COPY into another drawing leaves a hatch that keeps none. Any other record as it is.
        /// </summary>
        private Rec Recounted(Rec record)
        {
            if (record.Type != "HATCH")
            {
                return record;
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var counts = new Dictionary<int, int>();
            int kept = 0;
            int associative = -1;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 71 && associative < 0)
                {
                    associative = i;
                }

                // A spline edge's count of fit points (97) is followed by its fit points (11) or tangents (12, 13), not by the
                // handles of boundary objects: it is the edge's, and kept.
                if (pairs[i].Code != 97 || (i + 1 < data && (pairs[i + 1].Code == 11 || pairs[i + 1].Code == 12 || pairs[i + 1].Code == 13)))
                {
                    continue;
                }

                int following = 0;

                while (i + 1 + following < data && pairs[i + 1 + following].Code == 330)
                {
                    following++;
                }

                kept += following;
                int written = int.TryParse(Text(record, pairs[i]).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;

                if (written != following)
                {
                    counts.Add(i, following);
                }
            }

            bool unlink = kept == 0 && associative > 0 && Text(record, pairs[associative]).Trim() != "0";

            if (counts.Count == 0 && !unlink)
            {
                return record;
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (counts.TryGetValue(i, out int count))
                {
                    b.Group(97, count.ToString(CultureInfo.InvariantCulture));
                }
                else if (unlink && i == associative)
                {
                    b.Group(71, "0");
                }
                else
                {
                    b.Raw(record, pairs[i]);
                }
            }

            return b.Done(record.Type);
        }

        /// <summary>
        /// An ACAD_TABLE whose cells' attribute definitions were left out of a copy, each block cell's count of them (179) made
        /// the number of pointers (331, each with its value 300) that follow it, as AutoCAD reads that many pairs after it. The
        /// record itself where every count agrees.
        /// </summary>
        private Rec CellsRecounted(Rec record)
        {
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            Dictionary<int, int> counts = null;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code != 179)
                {
                    continue;
                }

                int following = 0;
                int j = i + 1;

                while (j < data && pairs[j].Code == 331)
                {
                    following++;
                    j += j + 1 < data && pairs[j + 1].Code == 300 ? 2 : 1;
                }

                int written = int.TryParse(Text(record, pairs[i]).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;

                if (written != following)
                {
                    (counts = counts ?? new Dictionary<int, int>()).Add(i, following);
                }
            }

            if (counts == null)
            {
                return record;
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (counts.TryGetValue(i, out int count))
                {
                    b.Group(179, count.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    b.Raw(record, pairs[i]);
                }
            }

            return b.Done(record.Type);
        }

        private static bool IsPointer(int code) => (code >= 320 && code <= 369) || (code >= 390 && code <= 399) || code == 480 || code == 481;

        /// <summary>Whether a group is a hard pointer or a hard owner (340 to 349, 360 to 369), which AutoCAD's deep clone follows.</summary>
        private static bool IsHardPointer(int code) => (code >= 340 && code <= 349) || (code >= 360 && code <= 369);

        /// <summary>
        /// The handle in this file of the copy of an entity of another drawing copied with the entity being added, as an attribute
        /// definition of a block copied with a table; null where it was not copied.
        /// </summary>
        private string CopyHandle(string pointer)
            => _entityCopies != null && TryHex(pointer.Trim(), out ulong handle) && _entityCopies.TryGetValue(handle, out string copy) ? copy : null;

        /// <summary>
        /// The index of the last group of a MULTILEADER's value of an attribute that begins at its definition (330): the index,
        /// width and text after it (177, 44, 302), which go with the definition where it is left out.
        /// </summary>
        private static int AttributeEnd(List<Pair> pairs, int at, int data)
        {
            while (at + 1 < data && (pairs[at + 1].Code == 177 || pairs[at + 1].Code == 44 || pairs[at + 1].Code == 302))
            {
                at++;
            }

            return at;
        }

        /// <summary>
        /// A MULTILEADER's record copied back into its own drawing (<paramref name="drawing"/>) without the value it gives to an
        /// attribute definition the drawing no longer has, erased since: the definition's handle (its 330 after its own marker)
        /// with the index, width and text after it (177, 44, 302), as <see cref="Pointed"/> leaves out one another drawing has
        /// no definition for. The record itself where every one is there, or the drawing is no database's.
        /// </summary>
        private Rec WithoutLostAttributes(Rec record, DxfFile drawing)
        {
            if (drawing.Database == null)
            {
                return record;
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            var b = new Builder(this);
            b.Raw(record, pairs[0]);
            bool changed = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                if (body >= 0 && i > body && i < data && pairs[i].Code == 330 && Definition(drawing, Text(record, pairs[i])) == null)
                {
                    i = AttributeEnd(pairs, i, data);
                    changed = true;
                    continue;
                }

                b.Raw(record, pairs[i]);
            }

            return changed ? b.Done(record.Type) : record;
        }

        /// <summary>The attribute definition of a handle of another drawing (<paramref name="origin"/>); null where it has none.</summary>
        private static DxfItem Definition(DxfFile origin, string pointer)
        {
            DxfItem definition = origin.Database?.ItemByHandle(pointer.Trim());
            return definition != null && !definition.IsRemoved && definition.Type == "ATTDEF" && definition.Block != null ? definition : null;
        }

        /// <summary>Whether the block of an attribute definition of another drawing is being copied into this one with the entity being added.</summary>
        private bool CopyingBlockOf(DxfFile origin, string pointer)
            => _blockCopies != null && Definition(origin, pointer) is DxfItem definition && origin.Blocks.TryGetValue(definition.Block, out DxfFileBlock block) && _blockCopies.ContainsKey(block);

        // The blocks of the drawing copied from that a pointer held back names, by the handle of their BLOCK_RECORD there, each
        // with the name of its copy here: what the pointer is taken to once every block is written.
        private Dictionary<ulong, string> _blocksToCome;

        /// <summary>
        /// Whether a handle of another drawing names the BLOCK_RECORD of a block being copied into this one with the entity being
        /// added, which a pointer is to wait for (<see cref="TranslateDeferred"/>): a block copied after the one that holds the
        /// pointer, as GKTAG after the block W whose multileader places it, or whose leader's override DIMLDRBLK names it. The
        /// name of its copy is kept for the pointer.
        /// </summary>
        private bool BlockToCome(DxfFile origin, string pointer)
        {
            if (_deferred == null || _blockCopies == null || !TryHex(pointer.Trim(), out ulong handle) || !ObjectsOf(origin).TryGetValue(handle, out KeyValuePair<Rec, bool> found)
                || !found.Value || found.Key.Type != "BLOCK_RECORD" || !origin.Blocks.TryGetValue(DxfReader.NameOf(origin.Record(found.Key).Text(2) ?? string.Empty), out DxfFileBlock block)
                || !_blockCopies.TryGetValue(block, out string name))
            {
                return false;
            }

            (_blocksToCome = _blocksToCome ?? new Dictionary<ulong, string>())[handle] = name;
            return true;
        }

        /// <summary>The handle here of the BLOCK_RECORD of the copy of a block a pointer held back names (<see cref="BlockToCome"/>); null where there is none.</summary>
        private string BlockCopyHandle(string pointer)
            => _blocksToCome != null && TryHex(pointer.Trim(), out ulong handle) && _blocksToCome.TryGetValue(handle, out string name) ? BlockRecordHandle(name) : null;

        /// <summary>
        /// The handle here of the attribute definition of the same tag as one of another drawing, in the block here of the name
        /// of its block (the name of the block's copy, where it was copied): what a multileader's value given for that definition
        /// is given for here, as the block it places is the one of that name here. Null where there is none.
        /// </summary>
        private string AttributeNamesake(DxfFile origin, string pointer)
        {
            DxfItem definition = Definition(origin, pointer);
            string tag = definition == null ? null : origin.ValueOf(definition, 2)?.Trim();
            string name = definition?.Block;

            if (tag == null || name == null)
            {
                return null;
            }

            if (_blockCopies != null && origin.Blocks.TryGetValue(name, out DxfFileBlock copied) && _blockCopies.TryGetValue(copied, out string given))
            {
                name = given;
            }

            if (!Blocks.TryGetValue(name, out DxfFileBlock block))
            {
                return null;
            }

            foreach (DxfItem item in block.Items.Snapshot())
            {
                if (!item.IsRemoved && item.Type == "ATTDEF" && string.Equals(ValueOf(item, 2)?.Trim(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Handle;
                }
            }

            return null;
        }

        // What marks a pointer held back for the copy of the entity it names: no handle begins so.
        private const string DeferredMark = "?";

        // The entities written while blocks are being copied with an entity, by their handles here; and those written with a
        // pointer held back, to be taken to its copy when every entity is written.
        private Dictionary<string, DxfItem> _copiedItems;
        private List<DxfItem> _deferred;

        /// <summary>Whether a handle is that of an entity of this file's drawing, of model space, paper space or a block.</summary>
        private bool IsEntity(string handle) => Database != null && Database.ItemByHandle(handle.Trim()) != null;

        /// <summary>
        /// Takes each pointer held back while blocks were copied (<see cref="Pointed"/>) to the copy of the entity it names, as
        /// AutoCAD's deep clone translates its map of ids once every object is copied, so that a LEADER written before its MTEXT
        /// still points to its copy, and an entity written before a block it points to (a MULTILEADER's block, a table's picture,
        /// the arrow block of a leader's override) to the copy of that block: a pointer whose entity was not copied is left out,
        /// as one left out at once is (a table cell's with its value and count, a LEADER's annotation with its kind, a HATCH's
        /// boundary with its count and association, an override of a dimension style with the 1070 naming its variable).
        /// The HATCH or LEADER whose pointer is kept is put among the reactors of the copy it points to, as AutoCAD keeps it.
        /// </summary>
        private void TranslateDeferred()
        {
            if (_deferred.Count == 0)
            {
                return;
            }

            foreach (DxfItem item in _deferred)
            {
                if (item.IsRemoved)
                {
                    continue;
                }

                List<Rec> records = RecordsOf(item);
                Rec record = records[0];
                List<Pair> pairs = PairsOf(record);
                int data = DataAt(record, pairs);
                var b = new Builder(this);
                var reactTo = new List<DxfItem>();
                bool cells = false;
                bool unannotated = false;

                for (int i = 0; i < pairs.Count; i++)
                {
                    string text = i > 0 && i < data ? Text(record, pairs[i]).Trim() : null;
                    int code = pairs[i].Code;

                    // An override of a dimension style naming an arrow block held back: the block's copy, or the two left out.
                    if (i >= data && code == 1070 && i + 1 < pairs.Count && pairs[i + 1].Code == 1005 && Text(record, pairs[i + 1]).Trim().StartsWith(DeferredMark, StringComparison.Ordinal))
                    {
                        string arrow = BlockCopyHandle(Text(record, pairs[i + 1]).Trim().Substring(DeferredMark.Length));

                        if (arrow != null)
                        {
                            b.Raw(record, pairs[i]);
                            b.Group(1005, arrow);
                        }

                        i++;
                        continue;
                    }

                    // A handle of extended data held back: the copy's, or null where its entity stayed behind.
                    if (i >= data && code == 1005 && Text(record, pairs[i]).Trim().StartsWith(DeferredMark, StringComparison.Ordinal))
                    {
                        b.Group(1005, CopyHandle(Text(record, pairs[i]).Trim().Substring(DeferredMark.Length)) ?? "0");
                        continue;
                    }

                    if (text == null || !text.StartsWith(DeferredMark, StringComparison.Ordinal) || !(IsPointer(code) || code == 330))
                    {
                        b.Raw(record, pairs[i]);
                        continue;
                    }

                    string copy = CopyHandle(text.Substring(DeferredMark.Length)) ?? BlockCopyHandle(text.Substring(DeferredMark.Length));

                    if (copy != null)
                    {
                        b.Group(code, copy);

                        if (((record.Type == "HATCH" && code == 330) || (record.Type == "LEADER" && code == 340)) && _copiedItems.TryGetValue(copy, out DxfItem pointed))
                        {
                            reactTo.Add(pointed);
                        }
                    }
                    else if (code == 331 && record.Type == "ACAD_TABLE")
                    {
                        cells = true;
                        i += i + 1 < data && pairs[i + 1].Code == 300 ? 1 : 0;
                    }
                    else if (code == 340 && record.Type == "LEADER")
                    {
                        unannotated = true;
                    }
                    else if (code == 330 && record.Type == "MULTILEADER")
                    {
                        i = AttributeEnd(pairs, i, data);
                    }
                }

                Rec made = Recounted(b.Done(record.Type));
                made = cells ? CellsRecounted(made) : made;
                made = unannotated && WithGroup(made, 73, "3", false, out Rec bare) ? bare : made;
                records[0] = made;
                SetRecords(item, records);
                item.Changed(record.Type);

                foreach (DxfItem pointed in reactTo)
                {
                    SetReactor(pointed, item.Handle, true);
                }
            }

            Edited();
        }

        /// <summary>
        /// The handle in this file of the object a handle of another file names: the record of a table of the same name, or the
        /// object of the named object dictionary under the same dictionary and entry names; null where there is none.
        /// </summary>
        private string Equivalent(DxfFile origin, string pointer)
        {
            if (!TryHex(pointer.Trim(), out ulong handle) || handle == 0)
            {
                return null;
            }

            Dictionary<ulong, KeyValuePair<Rec, bool>> theirs = ObjectsOf(origin);

            if (!theirs.TryGetValue(handle, out KeyValuePair<Rec, bool> found))
            {
                return null;
            }

            Rec record = found.Key;

            if (found.Value)
            {
                string name = DxfReader.NameOf(origin.Record(record).Text(2) ?? string.Empty);

                // A block copied with the entity, as a table's *T, is pointed at by the name of its copy.
                if (record.Type == "BLOCK_RECORD" && _blockCopies != null && origin.Blocks.TryGetValue(name, out DxfFileBlock copiedBlock)
                    && _blockCopies.TryGetValue(copiedBlock, out string copiedName))
                {
                    name = copiedName;
                }

                Rec? mine = TableRecord(record.Type, name, false);
                return mine != null && HandleOf(mine.Value, out ulong own) ? own.ToString("X", CultureInfo.InvariantCulture) : null;
            }

            if (!origin.NamedPath(handle, out string dictionary, out string entry))
            {
                return null;
            }

            Dictionary<ulong, KeyValuePair<Rec, bool>> ours = Objects();
            string under = Entry(RootDictionary(), dictionary);

            if (under == null || !TryHex(under, out ulong dictionaryHandle) || !ours.TryGetValue(dictionaryHandle, out KeyValuePair<Rec, bool> dictionaryRecord))
            {
                return null;
            }

            return Entry(dictionaryRecord.Key, entry);
        }

        /// <summary>
        /// The records of the TABLES and OBJECTS sections as they stand, by handle, each with whether it is a table's: kept while
        /// a call holds the records of the tables by name, until a record of those sections is written.
        /// </summary>
        private Dictionary<ulong, KeyValuePair<Rec, bool>> Objects()
        {
            if (_objects != null)
            {
                return _objects;
            }

            Dictionary<ulong, KeyValuePair<Rec, bool>> objects = ReadObjects();

            if (_tableNames != null)
            {
                _objects = objects;
            }

            return objects;
        }

        /// <summary>The records of the TABLES and OBJECTS sections as they stand, by handle, each with whether it is a table's: read, nothing of the file written.</summary>
        private Dictionary<ulong, KeyValuePair<Rec, bool>> ReadObjects()
        {
            var objects = new Dictionary<ulong, KeyValuePair<Rec, bool>>();

            foreach (Section section in _sections)
            {
                if (section.Name != "TABLES" && section.Name != "OBJECTS")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    if (record.Type != "TABLE" && record.Type != "ENDTAB" && HandleOf(record, out ulong handle) && !objects.ContainsKey(handle))
                    {
                        objects.Add(handle, new KeyValuePair<Rec, bool>(record, section.Name == "TABLES"));
                    }
                }
            }

            return objects;
        }

        /// <summary>
        /// The first DICTIONARY of the OBJECTS section, the named object dictionary; null where there is none. Walked record by
        /// record from the section's start, as <see cref="Inside"/> lists them, and left at the first, so that an edited file is
        /// not copied whole to find it; counted among <see cref="WholeReadings"/> as before.
        /// </summary>
        private Rec? RootDictionary()
        {
            WholeReadings++;

            foreach (Section section in _sections)
            {
                if (section.Name != "OBJECTS")
                {
                    continue;
                }

                for (int i = section.First + 1; i < section.Last; i++)
                {
                    List<Piece> slot = _slots?[i];

                    if (slot == null)
                    {
                        Rec record = Original(i);

                        if (record.Type == "DICTIONARY")
                        {
                            return record;
                        }

                        continue;
                    }

                    Rec? found = FirstDictionary(slot, slot.Count);

                    if (found != null)
                    {
                        return found;
                    }
                }

                List<Piece> last = _slots?[section.Last];
                Rec? added = last == null ? null : FirstDictionary(last, last.Count - 1);

                if (added != null)
                {
                    return added;
                }
            }

            return null;
        }

        /// <summary>The first DICTIONARY among the records of the first pieces of a slot; null where there is none.</summary>
        private static Rec? FirstDictionary(List<Piece> slot, int pieces)
        {
            for (int p = 0; p < pieces; p++)
            {
                foreach (Rec record in slot[p].Records)
                {
                    if (record.Type == "DICTIONARY")
                    {
                        return record;
                    }
                }
            }

            return null;
        }

        /// <summary>The handle a DICTIONARY gives an entry name (its 3, then 350 or 360), letters of either case the same; null where it has none.</summary>
        private string Entry(Rec? dictionary, string name)
        {
            if (dictionary == null)
            {
                return null;
            }

            Rec record = dictionary.Value;
            string last = null;

            foreach (Pair pair in PairsOf(record))
            {
                if (pair.Code == 3)
                {
                    last = Text(record, pair);
                }
                else if ((pair.Code == 350 || pair.Code == 360) && last != null)
                {
                    if (string.Equals(last.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return Text(record, pair).Trim();
                    }

                    last = null;
                }
            }

            return null;
        }

        /// <summary>The names under which the named object dictionary holds an object: its dictionary's and its entry's; false where it holds none.</summary>
        private bool NamedPath(ulong handle, out string dictionary, out string entry)
        {
            dictionary = null;
            entry = null;
            Rec? root = RootDictionary();

            if (root == null)
            {
                return false;
            }

            foreach (KeyValuePair<Rec, bool> found in Objects().Values)
            {
                Rec record = found.Key;

                if (found.Value || record.Type != "DICTIONARY")
                {
                    continue;
                }

                string last = null;

                foreach (Pair pair in PairsOf(record))
                {
                    if (pair.Code == 3)
                    {
                        last = Text(record, pair);
                    }
                    else if ((pair.Code == 350 || pair.Code == 360) && last != null)
                    {
                        if (TryHex(Text(record, pair).Trim(), out ulong value) && value == handle && HandleOf(record, out ulong dictionaryHandle))
                        {
                            Rec rootRecord = root.Value;
                            string parent = null;

                            foreach (Pair rootPair in PairsOf(rootRecord))
                            {
                                if (rootPair.Code == 3)
                                {
                                    parent = Text(rootRecord, rootPair);
                                }
                                else if ((rootPair.Code == 350 || rootPair.Code == 360) && parent != null)
                                {
                                    if (TryHex(Text(rootRecord, rootPair).Trim(), out ulong rootValue) && rootValue == dictionaryHandle)
                                    {
                                        dictionary = parent.Trim();
                                        entry = last.Trim();
                                        return true;
                                    }

                                    parent = null;
                                }
                            }
                        }

                        last = null;
                    }
                }
            }

            return false;
        }

        /// <summary>Puts the records of an entity at the end of a space: the ENTITIES section for model and paper space, before its ENDBLK for a block.</summary>
        private List<Piece> Place(DxfSpace space, Piece piece)
        {
            DxfFileBlock block = space.Block;

            if (block == null)
            {
                return PutBefore(EntitiesEnd(), piece);
            }

            if (block.Home != null)
            {
                block.Home.Insert(IndexOfPiece(block.Home, block.EndPiece), piece);

                if (_insertNames != null)
                {
                    InsertsWritten(piece.Records);
                }

                if (_usage != null)
                {
                    UsagePut(piece.Records);
                }

                return block.Home;
            }

            return PutBefore(block.End, piece);
        }

        /// <summary>
        /// Where a piece stands in a list it is in once, as <see cref="List{T}.IndexOf(T)"/> finds it; -1 where it is in none:
        /// sought from both ends at once, as the blocks added in code share one list, so that appending to the first of 10 000
        /// or to the last reads few pieces.
        /// </summary>
        private static int IndexOfPiece(List<Piece> pieces, Piece piece)
        {
            for (int first = 0, last = pieces.Count - 1; first <= last; first++, last--)
            {
                if (ReferenceEquals(pieces[first], piece))
                {
                    return first;
                }

                if (ReferenceEquals(pieces[last], piece))
                {
                    return last;
                }
            }

            return -1;
        }

        /// <summary>The handle of what owns the entities of a space: the BLOCK_RECORD of model space, of paper space, or of the block.</summary>
        private string OwnerOf(DxfSpace space)
        {
            switch (space.Kind)
            {
                case DxfSpaceKind.Model: return ModelOwner();
                case DxfSpaceKind.Paper: return BlockRecordHandle("*Paper_Space");
                default: return space.Block.Head != null ? OwnerOf(space.Block.Head) ?? BlockRecordHandle(space.Block.Name) : BlockRecordHandle(space.Block.Name);
            }
        }

        /// <summary>The handle of the BLOCK_RECORD of a name, letters of either case the same; null where there is none.</summary>
        private string BlockRecordHandle(string name)
        {
            Rec? record = TableRecord("BLOCK_RECORD", name, false);
            return record == null ? null : Record(record.Value).Text(5)?.Trim();
        }

        /// <summary>
        /// Grows the extents the header is to hold by those of an entity's shapes. An INSERT is grown by what it places, its
        /// GeometricExtents, at the next reading of the header: its block placed once then, however often it was written
        /// before, and as the drawing then has it, a block copied after the INSERT that places it included. It is placed anew
        /// at each reading of the header after the INSERT or one of its attributes was written, there being no reading of a
        /// block kept from one change of the drawing to the next.
        /// </summary>
        private void GrowBy(DxfItem item)
        {
            // A record that follows an entity is drawn as part of it.
            item = item.Parent ?? item;

            if (item.Type == "INSERT")
            {
                GrowLater(item);
                return;
            }

            GrowNow(item);
        }

        /// <summary>Grows the extents by an entity's shapes as it stands: an INSERT's those it places, each cell of a MINSERT's array.</summary>
        private void GrowNow(DxfItem item)
        {
            item = item.Parent ?? item;

            if (_detached || item.Block != null)
            {
                return;
            }

            foreach (object shape in item.Shapes)
            {
                if (shape is DxfInsert insert)
                {
                    // Placed only where the header holds the extents it would grow: a block of 1 000 000 lines takes over a
                    // second to place.
                    if (!(item.IsPaper ? HoldsExtents("$PEXTMIN", "$PEXTMAX") : HoldsExtents("$EXTMIN", "$EXTMAX")))
                    {
                        continue;
                    }

                    foreach (DxfShape placed in insert.GetPlacedEntities())
                    {
                        GrowByShape(placed.Shape, item.IsPaper);
                    }
                }
                else
                {
                    GrowByShape(shape, item.IsPaper);
                }
            }
        }

        /// <summary>Grows the extents of model or paper space by a shape, as <see cref="DxfWriter"/> writes it; not by one it does not write.</summary>
        private void GrowByShape(object shape, bool paper)
        {
            if (DxfWriter.CanWrite(shape))
            {
                DxfWriter.RecordsOf(shape, 0, 0.0, true, true, out double[] extents);
                Grow(extents, paper);
            }
        }

        /// <summary>
        /// Leaves the growing of the extents by an entity to the next reading of the header, for a write to one record that
        /// follows it (a VERTEX put in or set): the entity is grown by once then, as it stands, however many such writes came
        /// before, where growing by it at each would read the whole entity each time, 20 000 vertices appended one by one
        /// costing 20 000 readings of up to 20 000 vertices.
        /// </summary>
        private void GrowLater(DxfItem item)
        {
            if (_detached || item.Block != null)
            {
                return;
            }

            if (_growingLater == null)
            {
                _growingLater = new HashSet<DxfItem>();
                _growLater = new List<DxfItem>();
            }

            if (_growingLater.Add(item))
            {
                _growLater.Add(item);
                _headerStale = true;
            }
        }

        /// <summary>Grows the extents by each entity left to <see cref="GrowLater"/>, in the order they were left, those erased since left out.</summary>
        private void GrowPending()
        {
            if (_growLater == null)
            {
                return;
            }

            List<DxfItem> items = _growLater;
            _growLater = null;
            _growingLater = null;

            foreach (DxfItem item in items)
            {
                if (!item.IsRemoved)
                {
                    GrowNow(item);
                }
            }
        }

        /// <summary>The type a shape is written as in this file, checked as <see cref="DxfItem.Shape"/> checks it.</summary>
        internal string TypeWritten(object shape) => Written(shape, 0, 0.0, "shape", out _)[0].Key;

        /// <summary>Removes an entity from its space, as <see cref="DxfSpace.Remove(DxfItem)"/> does.</summary>
        internal bool Erase(DxfItem item) => item.Space != null && Remove(item.Space, item);

        #endregion

        #region Records that follow an entity

        /// <summary>
        /// The item of a record that follows an entity (a VERTEX or the SEQEND of a POLYLINE, an ATTRIB or the SEQEND of an
        /// INSERT): the same item for the record while it is there, read and written through the entity, owned by it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not that of a record that follows the entity.</exception>
        internal DxfItem FollowerOf(DxfItem parent, int index)
        {
            List<DxfItem> followers = FollowersOf(parent);

            if (index < 1 || index >= followers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The records that follow the " + parent.Type + " are 1 to " + Number(followers.Count - 1) + ".");
            }

            return followers[index] ?? (followers[index] = new DxfItem(parent, index));
        }

        /// <summary>
        /// The list of the items of the records that follow an entity, one place for each record (none at 0); made anew, the
        /// items made before taken out, where the records were written anew by other means than these.
        /// </summary>
        private List<DxfItem> FollowersOf(DxfItem parent)
        {
            int count = RecordCount(parent);
            List<DxfItem> followers = parent.Followers;

            if (followers == null || followers.Count != count)
            {
                RetireFollowers(parent);
                followers = new List<DxfItem>(new DxfItem[count]);
                parent.Followers = followers;
            }

            return followers;
        }

        /// <summary>Takes out the items of the records that follow an entity, whose records were all written anew: each reads as it was.</summary>
        internal void RetireFollowers(DxfItem parent)
        {
            List<DxfItem> followers = parent.Followers;

            if (followers == null)
            {
                return;
            }

            parent.Followers = null;

            foreach (DxfItem follower in followers)
            {
                if (follower != null)
                {
                    Detach(follower);
                }
            }
        }

        /// <summary>A follower's item no longer of its entity: removed, reading the record it was last.</summary>
        private void Detach(DxfItem follower)
        {
            Rec last = RecordAt(follower, 0);
            follower.Parent = null;
            follower.Piece = new Piece(new List<Rec> { last });
            follower.IsRemoved = true;
            follower.Changed(last.Type);
        }

        /// <summary>The items of the records that follow an entity made in code go with it to the file it is added to.</summary>
        private static void TakeFollowers(DxfItem from, DxfItem to)
        {
            to.Followers = from.Followers;
            from.Followers = null;

            if (to.Followers == null)
            {
                return;
            }

            foreach (DxfItem follower in to.Followers)
            {
                if (follower != null)
                {
                    follower.Parent = to;
                    follower.IsPaper = to.IsPaper;
                    follower.Block = to.Block;
                    follower.Changed(follower.Type);
                }
            }
        }

        /// <summary>
        /// Puts a record made in code among those that follow an entity, before the one at an index (the count of the records to
        /// put it last, before a SEQEND where there is one, which it may not follow): its type and own groups, its look and
        /// extended data as made; a handle of its own where the file uses them, the entity as its owner and the marker
        /// AcDbEntity from R13 on, the entity's space and layer, its subclass markers left out in a file of R12. Every other
        /// record keeps its handle and bytes. The item of the new record, which the object made in code then stands for.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 1 to the place of the SEQEND.</exception>
        /// <exception cref="ArgumentException">Thrown when the record has a lineweight or true colour the file's version has not,
        /// or a linetype its table does not list.</exception>
        internal DxfItem InsertFollower(DxfItem parent, int index, DxfItem made)
        {
            Editable(parent);

            // The records of an entity written anew already are put in in place, not copied: a copy for each of 20 000
            // vertices appended one by one would be a copy of up to 20 000 records each time.
            List<Rec> records = parent.Piece != null ? parent.Piece.Records : RecordsOf(parent);
            int last = records.Count > 1 && records[records.Count - 1].Type == "SEQEND" ? records.Count - 1 : records.Count;

            if (index < 1 || index > last)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A record goes in from 1 to " + Number(last) + ", before the SEQEND of the " + parent.Type + ".");
            }

            DxfFile source = made.File;
            Rec from = source.RecordAt(made, 0);
            List<Pair> pairs = PairsOf(from);
            int data = DataAt(from, pairs);
            Writing style = Style;
            Rec head = records[0];
            List<Pair> headPairs = PairsOf(head);
            int headData = DataAt(head, headPairs);

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 370 && !style.Lineweights)
                {
                    throw new ArgumentException("A DXF " + VersionSaid() + " has no lineweights, which came with R2000 (AC1015).", "value");
                }

                if (pairs[i].Code == 420 && !style.TrueColours)
                {
                    throw new ArgumentException("A DXF " + VersionSaid() + " has no true colours, which came with R2004 (AC1018).", "value");
                }

                if (pairs[i].Code == 6 && !HasLinetype(source.Text(from, pairs[i])))
                {
                    throw new ArgumentException("The linetype " + DxfReader.NameOf(source.Text(from, pairs[i])) + " is not in the file's linetype table: give one of it, BYLAYER or BYBLOCK.", "value");
                }
            }

            var b = new Builder(this);
            b.Group(0, from.Type);

            if (UsesHandles())
            {
                b.Group(5, NewHandle());
            }

            string owner = parent.Handle;

            if (style.Markers && owner != null)
            {
                b.Group(330, owner);
            }

            if (style.Markers)
            {
                b.Group(100, "AcDbEntity");
            }

            // A vertex takes its polyline's look, as AutoCAD's AUDIT wants it, in the place of its own.
            bool face = source.IsFaceRecord(from);
            bool vertex = from.Type == "VERTEX" && head.Type == "POLYLINE";
            KeepSpaceAndLayer(b, head, headPairs, headData, from.Type, face);
            bool braced = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];
                string value = source.Text(from, pair);

                if (i >= data)
                {
                    if (pair.Code == 1001)
                    {
                        EnsureApplication(value.Trim());
                    }

                    b.Group(pair.Code, value);
                    continue;
                }

                if (pair.Code == 102)
                {
                    braced = value.Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                bool kept = !braced && pair.Code != 5 && pair.Code != 105 && pair.Code != 330 && pair.Code != 360 && pair.Code != 67 && pair.Code != 410 && pair.Code != 8
                    && !(pair.Code == 100 && (!style.Markers || value.Trim() == "AcDbEntity")) && !(vertex && TakesLook(pair.Code, face));

                if (kept)
                {
                    b.Group(pair.Code, value);
                }
            }

            Rec put = b.Done(from.Type);
            List<DxfItem> followers = FollowersOf(parent);
            records.Insert(index, put);
            SetRecords(parent, records);
            parent.Changed(head.Type);

            var item = new DxfItem(parent, index);
            followers.Insert(index, item);

            for (int i = index + 1; i < followers.Count; i++)
            {
                if (followers[i] != null)
                {
                    followers[i].Index = i;
                }
            }

            GrowLater(parent);
            Edited();
            return item;
        }

        /// <summary>
        /// Takes one record that follows an entity out of it, as a VERTEX erased: its handle not given again and what points at
        /// it seen to on saving; every other record keeps its handle and bytes. The item reads as it was, erased.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the record or its entity is erased, or the record is the
        /// SEQEND, which the entity keeps.</exception>
        internal void RemoveFollower(DxfItem follower)
        {
            Editable(follower);
            DxfItem parent = follower.Parent;
            List<Rec> records = parent.Piece != null ? parent.Piece.Records : RecordsOf(parent);
            int index = follower.Index;

            if (records[index].Type == "SEQEND")
            {
                throw new InvalidOperationException("The SEQEND of a " + parent.Type + " ends its records, and is kept.");
            }

            List<DxfItem> followers = FollowersOf(parent);
            Detach(follower);
            Retire(records[index]);
            records.RemoveAt(index);
            SetRecords(parent, records);
            parent.Changed(records[0].Type);
            followers.RemoveAt(index);

            for (int i = index; i < followers.Count; i++)
            {
                if (followers[i] != null)
                {
                    followers[i].Index = i;
                }
            }

            Edited();
        }

        /// <summary>
        /// Takes several records out of those that follow an entity in one writing of it, each as <see cref="RemoveFollower"/>
        /// takes one: the vertices a fitting made, 80 000 of them, at the cost of one, where one at a time would move the
        /// records after each. Every other record keeps its handle, bytes and item.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an index is not that of a record that follows the entity.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or a record is the SEQEND, which the entity keeps.</exception>
        internal void RemoveFollowers(DxfItem parent, IReadOnlyList<int> indices)
        {
            Editable(parent);

            if (indices.Count == 0)
            {
                return;
            }

            List<Rec> records = parent.Piece != null ? parent.Piece.Records : RecordsOf(parent);
            List<DxfItem> followers = FollowersOf(parent);
            var gone = new bool[records.Count];

            foreach (int index in indices)
            {
                if (index < 1 || index >= records.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), index, "The records that follow the " + parent.Type + " are 1 to " + Number(records.Count - 1) + ".");
                }

                if (records[index].Type == "SEQEND")
                {
                    throw new InvalidOperationException("The SEQEND of a " + parent.Type + " ends its records, and is kept.");
                }

                gone[index] = true;
            }

            var kept = new List<Rec>(records.Count);
            var items = new List<DxfItem>(records.Count);

            for (int i = 0; i < records.Count; i++)
            {
                if (gone[i])
                {
                    if (followers[i] != null)
                    {
                        Detach(followers[i]);
                    }

                    Retire(records[i]);
                    continue;
                }

                kept.Add(records[i]);
                items.Add(followers[i]);
            }

            for (int i = 1; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    items[i].Index = i;
                }
            }

            SetRecords(parent, kept);
            parent.Changed(kept[0].Type);
            parent.Followers = items;
            Edited();
        }

        /// <summary>
        /// Sets groups of the own parts of several records of an entity in one writing of it, each record's as
        /// <see cref="SetValuesAt"/> sets one's: for a change to every vertex of a POLYLINE at the cost of one. Nothing is
        /// written where every value is as it was.
        /// </summary>
        internal void SetValuesAtMany(DxfItem item, IList<KeyValuePair<int, IList<KeyValuePair<int, string>>>> values, IReadOnlyList<int> order, bool geometry)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            bool changed = false;

            foreach (KeyValuePair<int, IList<KeyValuePair<int, string>>> record in values)
            {
                Rec now = records[record.Key];

                foreach (KeyValuePair<int, string> value in record.Value)
                {
                    if (WithOwnGroup(now, value.Key, value.Value, order, out Rec next))
                    {
                        now = next;
                        changed = true;
                    }
                }

                records[record.Key] = now;
            }

            if (changed)
            {
                Rewritten(item, records, geometry);
            }
        }

        #endregion

        #region Groups of a record

        /// <summary>
        /// The value of the first group of a code in the part of a record its type has of its own: from its first subclass
        /// marker other than AcDbEntity, or from its start in a file of R12, outside the groups of the applications and before
        /// its extended data, as the file writes it; null where it has none.
        /// </summary>
        internal string ValueOf(DxfItem item, int code) => ValueOf(item, code, false);

        /// <summary>
        /// The value of the first group of a code of the own part of a record, as <see cref="ValueOf(DxfItem, int)"/> reads it; for
        /// the name (2) of a copy of an INSERT in no file, the name its block has now in the drawing it was copied from, unless
        /// the record as written is asked for.
        /// </summary>
        internal string ValueOf(DxfItem item, int code, bool written)
        {
            if (!written && code == 2 && item.OriginBlock != null && !item.OriginBlock.Head.IsRemoved)
            {
                return item.OriginBlock.Name;
            }

            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            int at = OwnIndexOf(head, pairs, code);
            return at < 0 ? null : Text(head, pairs[at]);
        }

        /// <summary>
        /// The value of the first group of a code in the part all entities have, before the type's own part, outside the groups
        /// of the applications; null where it has none.
        /// </summary>
        internal string CommonValueOf(DxfItem item, int code)
        {
            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            int body = BodyAt(head, pairs, data);
            int end = body >= 0 ? body : data;
            bool braced = false;

            for (int i = 1; i < end; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(head, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == code)
                {
                    return Text(head, pairs[i]);
                }
            }

            return null;
        }

        /// <summary>The index of the first group of a code in the type's own part of a record, as <see cref="ValueOf(DxfItem, int)"/> reads it; -1 where it has none.</summary>
        private int OwnIndexOf(Rec record, List<Pair> pairs, int code)
        {
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            bool braced = false;

            for (int i = body >= 0 ? body + 1 : 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == code)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Sets groups of the type's own part of a record, each the first of its code: its value in place, its code line and
        /// line end kept and a whole number padded as the old one was; a group the record has not put where the order of the
        /// type's groups puts it; null takes a group out. The record is written anew once, the extents grown to hold the
        /// entity's shapes where asked; nothing is written where every value is as it was.
        /// </summary>
        /// <param name="item">The entity or table record.</param>
        /// <param name="values">The codes and values, in the order they are set.</param>
        /// <param name="order">The codes of the type's own part in the order AutoCAD writes them, a 100 where a subclass marker stands.</param>
        /// <param name="geometry">Whether the extents grow to hold the entity's shapes.</param>
        internal void SetValues(DxfItem item, IList<KeyValuePair<int, string>> values, IReadOnlyList<int> order, bool geometry)
            => SetValuesAt(item, 0, values, order, geometry);

        /// <summary>Sets groups of the own part of one record of an entity, the head (0) or one that follows it, as <see cref="SetValues"/> sets the head's.</summary>
        internal void SetValuesAt(DxfItem item, int index, IList<KeyValuePair<int, string>> values, IReadOnlyList<int> order, bool geometry)
        {
            Editable(item);

            // A copy of an INSERT given a block's name places that block, by its name, from then on.
            if (index == 0 && item.OriginBlock != null)
            {
                foreach (KeyValuePair<int, string> value in values)
                {
                    if (value.Key == 2)
                    {
                        item.OriginBlock = null;
                    }
                }
            }
            List<Rec> records = item.Parent == null && item.Piece != null ? item.Piece.Records : RecordsOf(item);
            Rec record = records[index];
            bool changed = false;

            foreach (KeyValuePair<int, string> value in values)
            {
                if (WithOwnGroup(record, value.Key, value.Value, order, out Rec next))
                {
                    record = next;
                    changed = true;
                }
            }

            if (changed)
            {
                records[index] = record;
                Rewritten(item, records, geometry);
            }
        }

        /// <summary>An entity's records written anew: its look and shapes read again, the extents grown where asked.</summary>
        private void Rewritten(DxfItem item, List<Rec> records, bool geometry)
        {
            SetRecords(item, records);
            item.Changed(records[0].Type);

            if (geometry && item.Parent != null)
            {
                GrowLater(item.Parent);
            }
            else if (geometry && (item.Type == "LEADER" || item.Type == "MULTILEADER"))
            {
                // Read as their curves, which a splined one is fitted anew for: once at the next reading of the header, not at
                // each edit, 3 000 vertices appended one by one to a splined leader taking 15.4 s where they took 6.3 s.
                GrowLater(item);
            }
            else if (geometry)
            {
                GrowBy(item);
            }

            TableEdited(item);
        }

        /// <summary>How many records an entity is made of: its own, and a POLYLINE's vertices and SEQEND, an INSERT's attributes and SEQEND.</summary>
        internal int RecordCount(DxfItem item)
            => item.Parent != null ? 1 : item.Piece != null ? item.Piece.Records.Count : _slots?[item.First] == null ? item.Count : RecordsOf(item).Count;

        /// <summary>The type of one record of an entity: its own (0), or one that follows it, as VERTEX or SEQEND.</summary>
        internal string RecordType(DxfItem item, int index) => RecordAt(item, index).Type;

        /// <summary>
        /// The groups of the own part of one record of an entity, in their order: from its first subclass marker other than
        /// AcDbEntity (that marker and those after it among them), or from its start in a file of R12, outside the groups of the
        /// applications and before its extended data; comments left out.
        /// </summary>
        internal IReadOnlyList<DxfTypedValue> OwnGroupsOf(DxfItem item, int index) => OwnGroupsOf(item, index, null);

        /// <summary>
        /// The own groups as <see cref="OwnGroupsOf(DxfItem, int)"/> gives them, and where each stands in the record where asked:
        /// two numbers a group, where its code line begins and where the next group begins, counted in bytes from the record's
        /// start, for <see cref="SpliceRecord"/>.
        /// </summary>
        internal IReadOnlyList<DxfTypedValue> OwnGroupsOf(DxfItem item, int index, List<int> places)
        {
            Rec record = RecordAt(item, index);
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            var groups = new List<DxfTypedValue>();
            bool braced = false;

            for (int i = body >= 0 ? body : 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code != 999)
                {
                    groups.Add(DxfTypedValue.Read(pairs[i].Code, Text(record, pairs[i])));
                    places?.Add(pairs[i].Start - record.Start);
                    places?.Add(pairs[i].End - record.Start);
                }
            }

            return groups.AsReadOnly();
        }

        /// <summary>
        /// Replaces every group of some codes in the own part of one record of an entity by the groups given, put where the
        /// first of those was, or where the order puts the first given where there was none; nothing is written where the
        /// groups given are those taken, code for code and text for text. For the runs of a record, as the vertices of a
        /// LWPOLYLINE or the knots of a SPLINE.
        /// </summary>
        internal void ReplaceOwn(DxfItem item, int index, ICollection<int> codes, IList<KeyValuePair<int, string>> groups, IReadOnlyList<int> order, bool geometry)
        {
            Editable(item);
            List<Rec> records = item.Parent == null && item.Piece != null ? item.Piece.Records : RecordsOf(item);
            Rec record = records[index];
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            var taken = new List<int>();
            bool braced = false;

            for (int i = body >= 0 ? body + 1 : 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && codes.Contains(pairs[i].Code))
                {
                    taken.Add(i);
                }
            }

            if (taken.Count == groups.Count)
            {
                bool same = true;

                for (int k = 0; k < taken.Count && same; k++)
                {
                    same = pairs[taken[k]].Code == groups[k].Key && string.Equals(Text(record, pairs[taken[k]]), groups[k].Value, StringComparison.Ordinal);
                }

                if (same)
                {
                    return;
                }
            }

            int put = taken.Count > 0 ? taken[0] : groups.Count > 0 ? OwnPlaceOf(record, pairs, groups[0].Key, order) : pairs.Count;
            var dropped = new HashSet<int>(taken);
            var b = new Builder(this);

            for (int i = 0; i <= pairs.Count; i++)
            {
                if (i == put)
                {
                    foreach (KeyValuePair<int, string> group in groups)
                    {
                        b.Group(group.Key, group.Value);
                    }
                }

                if (i < pairs.Count && !dropped.Contains(i))
                {
                    b.Raw(record, pairs[i]);
                }
            }

            records[index] = b.Done(record.Type);
            Rewritten(item, records, false);

            if (geometry)
            {
                GrowLater(item.Parent ?? item);
            }
        }

        /// <summary>
        /// Replaces the bytes of one record of an entity from <paramref name="from"/> to <paramref name="to"/>, counted from the
        /// record's start as <see cref="OwnGroupsOf(DxfItem, int, List{int})"/> places its groups, by the groups given, the bytes
        /// before and after copied as they are: a vertex of a LWPOLYLINE put in, set or taken out costs a copy of the bytes of
        /// its record, not a reading of every group of it. The extents grow by the entity at the next reading of the header
        /// where asked.
        /// </summary>
        /// <returns>How many bytes the groups given were written in.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bytes are not within the record.</exception>
        internal int SpliceRecord(DxfItem item, int index, int from, int to, IList<KeyValuePair<int, string>> groups, bool geometry)
        {
            Editable(item);
            List<Rec> records = item.Parent == null && item.Piece != null ? item.Piece.Records : RecordsOf(item);
            Rec record = records[index];

            if (from < 0 || to < from || record.Start + to > record.End)
            {
                throw new ArgumentOutOfRangeException(nameof(from), from, "The bytes are not within the " + Number(record.End - record.Start) + " of the record.");
            }

            var b = new Builder(this);
            b.Raw(record, record.Start, record.Start + from);
            int before = b.Length;

            foreach (KeyValuePair<int, string> group in groups)
            {
                b.Group(group.Key, group.Value);
            }

            int written = b.Length - before;
            b.Raw(record, record.Start + to, record.End);
            records[index] = b.Done(record.Type);
            Rewritten(item, records, false);

            if (geometry)
            {
                GrowLater(item.Parent ?? item);
            }

            return written;
        }

        /// <summary>
        /// Writes the records that follow an entity anew, as the vertices of a POLYLINE: each of the type and own groups given,
        /// with a handle of its own where the file uses handles, the entity as its owner and the marker AcDbEntity from R13 on,
        /// the entity's space and layer, then its groups, their subclass markers left out in a file of R12; the entity's SEQEND
        /// kept, and the handles of the records put out not given again.
        /// </summary>
        internal void SetFollowers(DxfItem item, IList<KeyValuePair<string, IList<KeyValuePair<int, string>>>> followers)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            bool hasEnd = records.Count > 1 && records[records.Count - 1].Type == "SEQEND";
            Writing style = Style;
            var result = new List<Rec> { head };

            foreach (KeyValuePair<string, IList<KeyValuePair<int, string>>> follower in followers)
            {
                var groups = new List<DxfTypedValue>();

                foreach (KeyValuePair<int, string> group in follower.Value)
                {
                    if (group.Key != 100 || style.Markers)
                    {
                        groups.Add(DxfTypedValue.Read(group.Key, group.Value));
                    }
                }

                bool face = IsFaceRecord(follower.Key, groups);
                result.Add(Follower(new KeyValuePair<string, List<DxfTypedValue>>(follower.Key, groups), item.Handle, f => KeepSpaceAndLayer(f, head, pairs, data, follower.Key, face)));
            }

            for (int r = 1; r < records.Count; r++)
            {
                if (!(hasEnd && r == records.Count - 1))
                {
                    Retire(records[r]);
                }
            }

            if (hasEnd)
            {
                result.Add(records[records.Count - 1]);
            }

            RetireFollowers(item);
            Rewritten(item, result, true);
        }

        /// <summary>A record with the first group of a code of its own part given a value, as <see cref="SetValues"/> sets it; false where nothing changes.</summary>
        private bool WithOwnGroup(Rec record, int code, string value, IReadOnlyList<int> order, out Rec changed)
        {
            changed = record;
            List<Pair> pairs = PairsOf(record);
            int at = OwnIndexOf(record, pairs, code);
            int put = -1;

            if (at < 0)
            {
                if (value == null)
                {
                    return false;
                }

                put = OwnPlaceOf(record, pairs, code, order);
            }
            else if (value != null)
            {
                string was = Text(record, pairs[at]);

                if (string.Equals(was, value, StringComparison.Ordinal))
                {
                    return false;
                }

                // A whole number right-aligned, as AutoCAD writes its flags, stays so.
                if (IsWhole(code) && was.Length > value.Length && was.StartsWith(" ", StringComparison.Ordinal))
                {
                    value = value.PadLeft(was.Length);
                }
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (i == put)
                {
                    b.Group(code, value);
                }

                if (i != at)
                {
                    b.Raw(record, pairs[i]);
                }
                else if (value != null)
                {
                    b.Raw(record, pairs[i].Start, pairs[i].ValueStart);
                    b.Value(value);
                    b.Raw(record, pairs[i].ValueEnd, pairs[i].End);
                }
            }

            if (put == pairs.Count)
            {
                b.Group(code, value);
            }

            changed = b.Done(record.Type);
            return true;
        }

        /// <summary>
        /// Where a group a record has not goes in its own part: after the last group of its subclass that comes before it in
        /// the order of the type's groups, else just after the marker of its subclass; in a record with no markers, after the
        /// last group that comes before it in the order; else, or for a code the order does not list, before the extended data.
        /// </summary>
        private int OwnPlaceOf(Rec record, List<Pair> pairs, int code, IReadOnlyList<int> order)
        {
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            int rank = -1;
            int segment = -1;
            int segments = 0;

            for (int k = 0; k < order.Count && rank < 0; k++)
            {
                if (order[k] == 100)
                {
                    segments++;
                }
                else if (order[k] == code)
                {
                    rank = k;
                    segment = Math.Max(0, segments - 1);
                }
            }

            if (rank < 0)
            {
                return data;
            }

            int after = -1;
            int marker = -1;
            int current = -1;
            bool braced = false;

            for (int i = body >= 0 ? body : 1; i < data; i++)
            {
                Pair pair = pairs[i];

                if (pair.Code == 102)
                {
                    braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced)
                {
                    continue;
                }

                if (body >= 0 && pair.Code == 100)
                {
                    current++;

                    if (current == segment)
                    {
                        marker = i;
                    }

                    continue;
                }

                if (body >= 0 && current != segment)
                {
                    continue;
                }

                int their = RankOf(order, pair.Code);

                if (their >= 0 && their < rank)
                {
                    after = i;
                }
            }

            if (after >= 0)
            {
                return after + 1;
            }

            return marker >= 0 ? marker + 1 : data;
        }

        private static int RankOf(IReadOnlyList<int> order, int code)
        {
            for (int k = 0; k < order.Count; k++)
            {
                if (order[k] == code && code != 100)
                {
                    return k;
                }
            }

            return -1;
        }

        /// <summary>Sets a group of the part all entities have, where AutoCAD writes it, as the look setters set theirs; null takes it out.</summary>
        internal void SetCommon(DxfItem item, int code, string value)
        {
            Editable(item);
            SetGroup(item, code, value, false);
        }

        /// <summary>The handle of what owns a record: its first group 330 outside the groups of the applications; null where it has none.</summary>
        internal string OwnerOf(DxfItem item)
        {
            // A record that follows an entity, an ATTRIB, is owned by the entity from R13 on, whatever its 330 says.
            if (item.Parent != null && HasMarkers)
            {
                return item.Parent.Handle;
            }

            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            bool braced = false;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(head, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == 330)
                {
                    return Text(head, pairs[i]).Trim();
                }
            }

            return null;
        }

        /// <summary>The handle of a record's extension dictionary: the 360 in its group {ACAD_XDICTIONARY; null where it has none.</summary>
        internal string ExtensionDictionaryOf(DxfItem item)
        {
            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            bool inDictionary = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 102)
                {
                    inDictionary = Text(head, pairs[i]).Trim() == "{ACAD_XDICTIONARY";
                }
                else if (inDictionary && pairs[i].Code == 360)
                {
                    return Text(head, pairs[i]).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The extended data of a record: its groups from the first 1001 to its end, comments left out; the texts of the
        /// application's name (1001), a string (1000) and a layer's name (1003) with their escapes (<c>\U+00E9</c>) read as
        /// letters, as a layer's name is read.
        /// </summary>
        internal IReadOnlyList<DxfTypedValue> XDataOf(DxfItem item) => XDataOf(item, 0);

        /// <summary>The extended data of a record of an entity, 0 its own, as <see cref="XDataOf(DxfItem)"/> reads it.</summary>
        internal IReadOnlyList<DxfTypedValue> XDataOf(DxfItem item, int index)
        {
            Rec head = RecordAt(item, index);
            List<Pair> pairs = PairsOf(head);
            var groups = new List<DxfTypedValue>();

            for (int i = DataAt(head, pairs); i < pairs.Count; i++)
            {
                if (pairs[i].Code != 999)
                {
                    groups.Add(DxfTypedValue.Read(pairs[i].Code, DataText(head, pairs[i])));
                }
            }

            return groups.AsReadOnly();
        }

        /// <summary>The value of a group of extended data as it is read: a text with its escapes read as letters.</summary>
        private string DataText(Rec record, Pair pair)
        {
            string text = Text(record, pair);
            return pair.Code == 1000 || pair.Code == 1001 || pair.Code == 1003 ? DxfReader.Decode(text) : text;
        }

        /// <summary>
        /// Gives a record the extended data given in the place of its own, each application named added to the table of
        /// applications where it is not listed; nothing is written where the data is as it was read, and a group read as it is
        /// given keeps its bytes.
        /// </summary>
        internal void SetXData(DxfItem item, IReadOnlyList<DxfTypedValue> groups)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];

            if (!WithData(head, groups, out Rec changed))
            {
                return;
            }

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 1001)
                {
                    EnsureApplication(group.Value.Trim());
                }
            }

            records[0] = changed;
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
        }

        /// <summary>
        /// A record with the extended data given in the place of its own, each group read as it is given keeping its bytes;
        /// false where the data is as it was read.
        /// </summary>
        private bool WithData(Rec head, IReadOnlyList<DxfTypedValue> groups, out Rec changed)
        {
            changed = head;
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            var existing = new List<Pair>();

            for (int i = data; i < pairs.Count; i++)
            {
                if (pairs[i].Code != 999)
                {
                    existing.Add(pairs[i]);
                }
            }

            bool same = existing.Count == groups.Count;

            for (int k = 0; k < existing.Count && same; k++)
            {
                same = existing[k].Code == groups[k].TypeCode && string.Equals(DataText(head, existing[k]), groups[k].Value, StringComparison.Ordinal);
            }

            if (same)
            {
                return false;
            }

            var b = new Builder(this);

            for (int i = 0; i < data; i++)
            {
                b.Raw(head, pairs[i]);
            }

            for (int k = 0; k < groups.Count; k++)
            {
                DxfTypedValue group = groups[k];

                if (k < existing.Count && existing[k].Code == group.TypeCode && string.Equals(DataText(head, existing[k]), group.Value, StringComparison.Ordinal))
                {
                    b.Raw(head, existing[k]);
                }
                else
                {
                    b.Group(group.TypeCode, group.Value);
                }
            }

            changed = b.Done(head.Type);
            return true;
        }

        /// <summary>
        /// Gives the records of an entity the extended data left for them (<see cref="DxfItem.PendingRecords"/>), in the writing of
        /// its records under way: an entity moved has its records written once, their world points of extended data moved with it.
        /// </summary>
        private void TakePendingData(DxfItem item, List<Rec> records)
        {
            List<DxfItem.PendingData> pending = item.PendingRecords;

            if (pending == null)
            {
                return;
            }

            item.PendingRecords = null;

            foreach (DxfItem.PendingData data in pending)
            {
                // A record of the type it was, where it was: records written anew in other places are given none of it.
                if (data.Index < records.Count && records[data.Index].Type == data.Type && WithData(records[data.Index], data.Groups, out Rec changed))
                {
                    records[data.Index] = changed;
                }
            }
        }

        /// <summary>The indices of the records of an entity (0 its own) that have extended data an entity moved moves, its records read once.</summary>
        internal List<int> WorldDataRecords(DxfItem item)
        {
            var found = new List<int>();
            List<Rec> records = RecordsOf(item);

            for (int r = 0; r < records.Count; r++)
            {
                if (HasWorldData(records[r]))
                {
                    found.Add(r);
                }
            }

            return found;
        }

        /// <summary>Whether a record has extended data an entity moved moves: a world point, displacement or direction (1011 to 1013), a distance (1041) or a scale factor (1042).</summary>
        private static bool HasWorldData(Rec record)
        {
            var walk = new GroupWalk(record);

            while (walk.Next(out int code, out _, out _))
            {
                if ((code >= 1011 && code <= 1013) || code == 1041 || code == 1042)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Tables

        /// <summary>
        /// Adds an application to the table of applications (APPID) where it does not list it, letters of either case the
        /// same, as AutoCAD registers one before its extended data is written: a plain APPID record before the table's ENDTAB.
        /// </summary>
        private void EnsureApplication(string name)
        {
            if (_detached)
            {
                return;
            }

            if (TableRecord("APPID", name, false) != null)
            {
                return;
            }

            if (AddTableRecord("APPID", "AcDbRegAppTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, "0");
            }) != null)
            {
                GeometryHelperLog.Info("DxfFile: the application " + name + " was not in the table of applications, so an APPID record was added for it.");
            }
        }

        /// <summary>
        /// Adds a record to a table, before its ENDTAB: its type, a handle of its own where the file uses handles, the table as
        /// its owner and the markers of a table record from R13 on, then the groups given; the table's count raised by one.
        /// Null where the file has no such table.
        /// </summary>
        internal DxfItem AddTableRecord(string table, string marker, Action<Builder> groups)
        {
            int head = -1;
            int end = -1;

            foreach (Section section in _sections)
            {
                for (int i = section.First + 1; section.Name == "TABLES" && i < section.Last && end < 0; i++)
                {
                    if (head < 0 && _types[i] == "TABLE" && string.Equals(DxfReader.NameOf(Record(Original(i)).Text(2) ?? string.Empty), table, StringComparison.OrdinalIgnoreCase))
                    {
                        head = i;
                    }
                    else if (head >= 0 && _types[i] == "ENDTAB")
                    {
                        end = i;
                    }
                }
            }

            if (end < 0)
            {
                GeometryHelperLog.Debug("DxfFile: the file has no " + table + " table to add a record to.");
                return null;
            }

            Writing style = Style;
            Rec header = Current(head, head + 1)[0];
            string tableHandle = Record(header).Text(5)?.Trim();
            var b = new Builder(this);
            b.Group(0, table);

            // A DIMSTYLE gives its handle as 105, its 5 being the arrow block DIMBLK of R12.
            if (UsesHandles())
            {
                b.Group(table == "DIMSTYLE" ? 105 : 5, NewHandle());
            }

            if (style.Markers)
            {
                if (!string.IsNullOrEmpty(tableHandle))
                {
                    b.Group(330, tableHandle);
                }

                b.Group(100, "AcDbSymbolTableRecord");
                b.Group(100, marker);
            }

            groups(b);

            var piece = new Piece(new List<Rec> { b.Done(table) });
            List<Piece> home = PutBefore(end, piece);
            List<Pair> pairs = PairsOf(header);
            int count = FirstWhole(header, pairs, 70);

            if (WithGroup(header, 70, (count + 1).ToString(CultureInfo.InvariantCulture), false, out Rec counted))
            {
                Put(head, new List<Piece> { new Piece(new List<Rec> { counted }) });
            }

            return Listed(table, piece, home);
        }

        private Dictionary<string, List<DxfItem>> _tableItems;
        private readonly object _tableItemsMade = new object();

        /// <summary>
        /// The records of a table, in the order of the file, those added after them: each the same object every time, so that
        /// the editing model has one object for each. Made under a lock, as several threads may read a drawing at once, each
        /// copying it into one of its own, so that two of them never make a list each.
        /// </summary>
        internal List<DxfItem> TableItems(string table)
        {
            lock (_tableItemsMade)
            {
                if (_tableItems == null)
                {
                    _tableItems = new Dictionary<string, List<DxfItem>>(StringComparer.Ordinal);
                }

                if (_tableItems.TryGetValue(table, out List<DxfItem> items))
                {
                    return items;
                }

                items = new List<DxfItem>();

                foreach (Section section in _sections)
                {
                    if (section.Name != "TABLES")
                    {
                        continue;
                    }

                    for (int i = section.First + 1; i < section.Last; i++)
                    {
                        if (_types[i] == table)
                        {
                            items.Add(new DxfItem(this, i, 1, table, null));
                        }
                    }
                }

                // Those added before the list was first asked for, by an entity set on a layer not listed.
                if (_added != null && _added.TryGetValue(table, out List<DxfItem> added))
                {
                    items.AddRange(added);
                }

                _tableItems.Add(table, items);
                return items;
            }
        }

        private Dictionary<string, List<DxfItem>> _added;

        /// <summary>A record added to a table, listed with its table's records.</summary>
        private DxfItem Listed(string table, Piece piece, List<Piece> home)
        {
            var item = new DxfItem(this, piece, table) { Home = home };

            if (_tableItems != null && _tableItems.TryGetValue(table, out List<DxfItem> items))
            {
                items.Add(item);
            }
            else
            {
                if (_added == null)
                {
                    _added = new Dictionary<string, List<DxfItem>>(StringComparer.Ordinal);
                }

                if (!_added.TryGetValue(table, out List<DxfItem> added))
                {
                    added = new List<DxfItem>();
                    _added.Add(table, added);
                }

                added.Add(item);
            }

            if (table == "LAYER")
            {
                _layers = NewLayers();
                LayerNames().Add(DxfReader.NameOf(ValueOf(item, 2) ?? string.Empty));
            }
            else if (table == "LTYPE")
            {
                _linetypes = null;
            }

            return item;
        }

        private HashSet<string> _layerNames;
        private Rec? _zeroLayer;
        private bool _zeroLayerKnown;

        /// <summary>
        /// The names of the layers of the layer table, letters of either case the same, read once and kept as layers are
        /// added, so that a layer is looked up in it at no cost.
        /// </summary>
        private HashSet<string> LayerNames()
        {
            if (_layerNames == null)
            {
                _layerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfLayer layer in LayerTable)
                {
                    _layerNames.Add(layer.Name);
                }
            }

            return _layerNames;
        }

        /// <summary>Writes the lineweight (where asked) and the plot style of layer 0 into a new LAYER, as the file has them; found once.</summary>
        private void ZeroLayerLook(Builder b, bool weight)
        {
            if (!_zeroLayerKnown)
            {
                _zeroLayerKnown = true;

                foreach (Rec record in TableRecords("LAYER"))
                {
                    if (DxfReader.NameOf(Record(record).Text(2) ?? string.Empty) == "0")
                    {
                        _zeroLayer = record;
                        break;
                    }
                }
            }

            if (_zeroLayer == null)
            {
                return;
            }

            Rec zero = _zeroLayer.Value;

            foreach (Pair pair in PairsOf(zero))
            {
                if ((weight && pair.Code == 370) || pair.Code == 390)
                {
                    b.Raw(zero, pair);
                }
            }
        }

        /// <summary>What a change to a table record leaves to be read again: the layers, the linetypes and the placing.</summary>
        internal void TableEdited(DxfItem item)
        {
            if (item.Type == "LAYER")
            {
                _layers = NewLayers();
            }
            else if (item.Type == "LTYPE")
            {
                _linetypes = null;
            }

            Edited();
        }

        /// <summary>The TABLE record of a table, which holds its handle; null where the file has no such table.</summary>
        internal DxfItem TableItem(string table)
        {
            foreach (Section section in _sections)
            {
                for (int i = section.First + 1; section.Name == "TABLES" && i < section.Last; i++)
                {
                    if (_types[i] == "TABLE" && string.Equals(DxfReader.NameOf(Record(Original(i)).Text(2) ?? string.Empty), table, StringComparison.OrdinalIgnoreCase))
                    {
                        return new DxfItem(this, i, 1, "TABLE", null);
                    }
                }
            }

            return null;
        }

        /// <summary>Adds a layer to the layer table where it does not list it, as an entity set on it does.</summary>
        internal void LayerNeeded(string name) => EnsureLayer(name);

        /// <summary>Whether a linetype is in the linetype table, letters of either case the same, or is BYLAYER or BYBLOCK.</summary>
        internal bool LinetypeListed(string name) => HasLinetype(name);

        /// <summary>Whether the file's version is that of AutoCAD's number given or later: 1015 for R2000.</summary>
        internal bool Supports(int version) => Style.Version >= version;

        /// <summary>Refuses what the file's version has not: a group or variable that came with a later one, as R2000 (AC1015) brought lineweights.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the file is older than the version given.</exception>
        internal void Requires(int version, string what, string since)
        {
            if (!Supports(version))
            {
                throw new InvalidOperationException("A DXF " + VersionSaid() + " has no " + what + ", which came with " + since + ".");
            }
        }

        /// <summary>The drawing the file is opened as; null for a file that holds entities not yet in one.</summary>
        internal DxfDatabase Database { get; set; }

        /// <summary>
        /// Adds a LAYER record of a layer made in code: its name, flags, colour (below nought where it is off), true colour,
        /// linetype, plotting, lineweight and the plot style of layer 0, each where the file's version has it; null where the
        /// file has no layer table.
        /// </summary>
        internal DxfItem AddLayer(string name, int colour, int? trueColour, string linetype, int lineweight, int flags, bool plotted)
        {
            Writing style = Style;
            string spelled = Linetypes().Find(l => string.Equals(DxfReader.NameOf(l), DxfReader.NameOf(linetype), StringComparison.OrdinalIgnoreCase)) ?? linetype;

            DxfItem item = AddTableRecord("LAYER", "AcDbLayerTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, flags.ToString(CultureInfo.InvariantCulture));
                b.Group(62, colour.ToString(CultureInfo.InvariantCulture));

                if (trueColour.HasValue)
                {
                    b.Group(420, trueColour.Value.ToString(CultureInfo.InvariantCulture));
                }

                b.Group(6, spelled);

                if (style.Lineweights)
                {
                    if (!plotted)
                    {
                        b.Group(290, "0");
                    }

                    b.Group(370, lineweight.ToString(CultureInfo.InvariantCulture));

                    // The plot style of layer 0, which a layer of R2000 on has.
                    ZeroLayerLook(b, false);
                }
            });

            if (item != null)
            {
                Edited();
            }

            return item;
        }

        /// <summary>
        /// Adds an LTYPE record of a linetype made in code: its name, description, the alignment A, its count of dashes, the
        /// length of its pattern, then the groups of its dashes as given, each its 49, its 74 from R13 on and those of its shape
        /// or text; null where the file has no linetype table.
        /// </summary>
        internal DxfItem AddLinetype(string name, string description, double length, int count, IList<KeyValuePair<int, string>> dashes)
        {
            return AddTableRecord("LTYPE", "AcDbLinetypeTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, "0");
                b.Group(3, description);
                b.Group(72, "65");
                b.Group(73, count.ToString(CultureInfo.InvariantCulture));
                b.Group(40, Numbers.Exact(length + 0.0));

                foreach (KeyValuePair<int, string> group in dashes)
                {
                    b.Group(group.Key, group.Value);
                }
            });
        }

        /// <summary>
        /// Adds a STYLE record of a text style made in code, as AutoCAD writes one: its name, flags, fixed height, width, slant
        /// in degrees, generation flags 0, the last height 2.5, its font's file and its big font's; null where the file has no
        /// text style table.
        /// </summary>
        internal DxfItem AddTextStyle(string name, int flags, double height, double width, double obliqueDegrees, string font, string bigFont)
        {
            return AddTableRecord("STYLE", "AcDbTextStyleTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, flags.ToString(CultureInfo.InvariantCulture));
                b.Group(40, Numbers.Exact(height + 0.0));
                b.Group(41, Numbers.Exact(width + 0.0));
                b.Group(50, Numbers.Exact(obliqueDegrees + 0.0));
                b.Group(71, "0");
                b.Group(42, "2.5");
                b.Group(3, font);
                b.Group(4, bigFont);
            });
        }

        /// <summary>
        /// Adds a block made in code, as AutoCAD writes one: from R13 on its BLOCK_RECORD in the block table, then its BLOCK and
        /// ENDBLK at the end of the BLOCKS section, owned by that record, on layer 0, with handles where the file uses them; its
        /// BLOCK's flags (70) those given, 1 for a block AutoCAD names itself, as a dimension's <c>*D1</c>, and its path (1)
        /// that given for an external reference, in a file of R12 too.
        /// </summary>
        internal DxfFileBlock AddBlock(string name, Geometry.GeoPoint3 origin, int units, bool explodable, bool uniform, string comments, out DxfItem record, int flags = 0, string path = null)
        {
            int end = -1;

            foreach (Section section in _sections)
            {
                if (section.Name == "BLOCKS")
                {
                    end = section.Last;
                }
            }

            if (end < 0)
            {
                throw new InvalidOperationException("The file has no BLOCKS section to add the block " + name + " to.");
            }

            Writing style = Style;
            record = null;

            if (style.Markers)
            {
                record = AddTableRecord("BLOCK_RECORD", "AcDbBlockTableRecord", b =>
                {
                    b.Group(2, name);
                    b.Group(340, "0");

                    if (style.Version >= 1021)
                    {
                        b.Group(70, units.ToString(CultureInfo.InvariantCulture));
                        b.Group(280, explodable ? "1" : "0");
                        b.Group(281, uniform ? "1" : "0");
                    }
                });
            }

            string owner = record?.Handle;
            var head = new Builder(this);
            head.Group(0, "BLOCK");

            if (UsesHandles())
            {
                head.Group(5, NewHandle());
            }

            if (style.Markers)
            {
                if (owner != null)
                {
                    head.Group(330, owner);
                }

                head.Group(100, "AcDbEntity");
            }

            head.Group(8, "0");

            if (style.Markers)
            {
                head.Group(100, "AcDbBlockBegin");
            }

            head.Group(2, name);
            head.Group(70, flags.ToString(CultureInfo.InvariantCulture));
            head.Group(10, Numbers.Exact(origin.X + 0.0));
            head.Group(20, Numbers.Exact(origin.Y + 0.0));
            head.Group(30, Numbers.Exact(origin.Z + 0.0));
            head.Group(3, name);

            if (path != null || style.Markers)
            {
                head.Group(1, path ?? string.Empty);
            }

            if (comments.Length > 0)
            {
                head.Group(4, comments);
            }

            var tail = new Builder(this);
            tail.Group(0, "ENDBLK");

            if (UsesHandles())
            {
                tail.Group(5, NewHandle());
            }

            if (style.Markers)
            {
                if (owner != null)
                {
                    tail.Group(330, owner);
                }

                tail.Group(100, "AcDbEntity");
            }

            tail.Group(8, "0");

            if (style.Markers)
            {
                tail.Group(100, "AcDbBlockEnd");
            }

            var headPiece = new Piece(new List<Rec> { head.Done("BLOCK") });
            var endPiece = new Piece(new List<Rec> { tail.Done("ENDBLK") });
            List<Piece> home = PutBefore(end, headPiece);
            PutBefore(end, endPiece);
            var headItem = new DxfItem(this, headPiece, "BLOCK") { Home = home };
            var block = new DxfFileBlock(this, name, origin, flags, new List<DxfItem>(), headItem, -1) { EndPiece = endPiece, Home = home };

            // Found by its name from then on, as a block of the file is, by the INSERTs of it among the rest.
            if (!Blocks.ContainsKey(name))
            {
                _blockIndex.Add(name, block);
            }

            Edited();
            GeometryHelperLog.Info("DxfFile: the block " + name + " was added.");
            return block;
        }

        #endregion

        #region The header

        /// <summary>The groups of a variable of the header as the file would be saved; null where it has none.</summary>
        internal IReadOnlyList<DxfTypedValue> HeaderOf(string name)
        {
            FlushHeader();
            return _header.TryGetValue(name, out IReadOnlyList<DxfTypedValue> groups) ? groups : null;
        }

        /// <summary>
        /// Gives a variable of the header its groups, where it is last given: each value in place where the codes are as they
        /// were, else its groups written anew; at the end of the header where it is not given. Nothing is written where the
        /// groups are as they were.
        /// </summary>
        internal void SetHeader(string name, IReadOnlyList<DxfTypedValue> groups)
        {
            FlushHeader();
            int at = -1;
            int nine = -1;
            int last = -1;

            foreach (Section section in _sections)
            {
                for (int i = section.First; section.Name == "HEADER" && i < section.Last; i++)
                {
                    List<Rec> now = Current(i, i + 1);

                    if (now.Count != 1)
                    {
                        continue;
                    }

                    last = i;
                    List<Pair> found = PairsOf(now[0]);

                    for (int p = 1; p < found.Count; p++)
                    {
                        if (found[p].Code == 9 && Text(now[0], found[p]).Trim() == name)
                        {
                            at = i;
                            nine = p;
                        }
                    }
                }
            }

            if (last < 0)
            {
                throw new InvalidOperationException("The file has no HEADER section to set " + name + " in.");
            }

            Rec record = Current(at >= 0 ? at : last, (at >= 0 ? at : last) + 1)[0];
            List<Pair> pairs = PairsOf(record);
            var b = new Builder(this);

            if (at < 0)
            {
                foreach (Pair pair in pairs)
                {
                    b.Raw(record, pair);
                }

                b.Group(9, name);

                foreach (DxfTypedValue group in groups)
                {
                    b.Group(group.TypeCode, group.Value);
                }

                at = last;
            }
            else
            {
                int end = nine + 1;

                while (end < pairs.Count && pairs[end].Code != 9)
                {
                    end++;
                }

                bool sameCodes = end - nine - 1 == groups.Count;

                for (int k = 0; sameCodes && k < groups.Count; k++)
                {
                    sameCodes = pairs[nine + 1 + k].Code == groups[k].TypeCode;
                }

                bool changed = false;

                for (int p = 0; p < pairs.Count; p++)
                {
                    if (p <= nine || p >= end)
                    {
                        b.Raw(record, pairs[p]);
                        continue;
                    }

                    if (!sameCodes)
                    {
                        if (p == nine + 1)
                        {
                            foreach (DxfTypedValue group in groups)
                            {
                                b.Group(group.TypeCode, group.Value);
                            }

                            changed = true;
                        }

                        continue;
                    }

                    string value = groups[p - nine - 1].Value;

                    if (string.Equals(Text(record, pairs[p]), value, StringComparison.Ordinal))
                    {
                        b.Raw(record, pairs[p]);
                        continue;
                    }

                    b.Raw(record, pairs[p].Start, pairs[p].ValueStart);
                    b.Value(value);
                    b.Raw(record, pairs[p].ValueEnd, pairs[p].End);
                    changed = true;
                }

                if (!changed && sameCodes)
                {
                    return;
                }

                if (!changed)
                {
                    foreach (DxfTypedValue group in groups)
                    {
                        b.Group(group.TypeCode, group.Value);
                    }
                }
            }

            Put(at, new List<Piece> { new Piece(new List<Rec> { b.Done(record.Type) }) });
            ReadHeader();
        }

        #endregion
    }
}
