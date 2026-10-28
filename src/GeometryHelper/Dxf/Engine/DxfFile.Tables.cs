using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        private SortedDictionary<string, DxfFileBlock> _blockIndex;

        #region Records of tables taken out and renamed

        /// <summary>
        /// The records of a section as they stand, between its SECTION and its ENDSEC: those of the file and those put in since,
        /// which stand in the place of the ENDSEC, before it, where what is added at the end of the section is put.
        /// </summary>
        private List<Rec> Inside(Section section)
        {
            List<Rec> records = Current(section.First + 1, section.Last);
            List<Piece> last = _slots?[section.Last];

            for (int p = 0; last != null && p < last.Count - 1; p++)
            {
                records.AddRange(last[p].Records);
            }

            return records;
        }

        private HashSet<string> _layersDrawnOn;
        private int _layersDrawnOnAt = -1;

        /// <summary>
        /// The layers an entity is on, letters of either case the same: the 8 of every record of model space, paper space and
        /// the blocks, vertices and attributes among them; worked out in one walk and kept until the records next change.
        /// </summary>
        internal HashSet<string> LayersDrawnOn()
        {
            if (_layersDrawnOn != null && _layersDrawnOnAt == _editCount)
            {
                return _layersDrawnOn;
            }

            var layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Section section in _sections)
            {
                if (section.Name != "ENTITIES" && section.Name != "BLOCKS")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    var walk = new GroupWalk(record);
                    bool braced = false;

                    while (walk.Next(out int code, out int start, out int end) && code != 1001)
                    {
                        if (code == 102)
                        {
                            braced = Text(record, start, end).Trim().StartsWith("{", StringComparison.Ordinal);
                        }
                        else if (code == 8 && !braced)
                        {
                            layers.Add(DxfReader.NameOf(Text(record, start, end)));
                        }
                    }
                }
            }

            _layersDrawnOn = layers;
            _layersDrawnOnAt = _editCount;
            return layers;
        }

        /// <summary>Goes through the records of a section as <see cref="Inside"/> gives them, rewriting or taking out each as asked.</summary>
        private void VisitInside(Section section, Rewrite rewrite)
        {
            Visit(section.First + 1, section.Last, rewrite);
            List<Piece> last = _slots?[section.Last];

            for (int p = 0; last != null && p < last.Count - 1; p++)
            {
                Piece piece = last[p];

                for (int k = 0; k < piece.Records.Count; k++)
                {
                    if (!rewrite(piece.Records[k], out Rec? now))
                    {
                        continue;
                    }

                    if (_tableNames != null && section.Name == "TABLES")
                    {
                        TableWritten(piece.Records[k], now);
                    }

                    if (section.Name == "TABLES" || section.Name == "OBJECTS")
                    {
                        _objects = null;
                    }

                    if (_insertNames != null && now.HasValue)
                    {
                        InsertsWritten(now.Value);
                    }

                    if (_usage != null)
                    {
                        CountUsage(_usage, piece.Records[k], -1);

                        if (now.HasValue)
                        {
                            CountUsage(_usage, now.Value, 1);
                        }
                    }

                    if (now.HasValue)
                    {
                        piece.Records[k] = now.Value;
                    }
                    else
                    {
                        piece.Records.RemoveAt(k);
                        k--;
                    }
                }
            }

            _editCount++;
        }

        /// <summary>The TABLE record of a table and its ENDTAB, as indices of the file; false where the file has no such table.</summary>
        private bool TableBounds(string table, out int head, out int end)
        {
            head = -1;
            end = -1;

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

            return end >= 0;
        }

        /// <summary>
        /// Takes a record of a table out, as AutoCAD erases one: its record put out, its handle not given again and what it owned
        /// removed with it on saving, the table's count lowered by one, and the table's list and what is read of it read again.
        /// </summary>
        internal void RemoveTableRecord(DxfItem item, string table)
        {
            RemoveRecord(item);

            if (TableBounds(table, out int head, out _))
            {
                Rec header = Current(head, head + 1)[0];
                int count = FirstWhole(header, PairsOf(header), 70);

                if (WithGroup(header, 70, Math.Max(0, count - 1).ToString(CultureInfo.InvariantCulture), false, out Rec counted))
                {
                    Put(head, new List<Piece> { new Piece(new List<Rec> { counted }) });
                }
            }

            if (_tableItems != null && _tableItems.TryGetValue(table, out List<DxfItem> items))
            {
                items.Remove(item);
            }

            if (_added != null && _added.TryGetValue(table, out List<DxfItem> added))
            {
                added.Remove(item);
            }

            if (table == "LAYER")
            {
                _layers = NewLayers();
                _layerNames = null;
            }
            else if (table == "LTYPE")
            {
                _linetypes = null;
            }
        }

        /// <summary>
        /// Writes a new name into every group that names something by its old name, letters of either case the same: in the
        /// records of the sections given, each group outside the groups of the applications and before the extended data that
        /// the test says names it, and each group of extended data of a code given (1003 for a layer); and, asked for a block, the
        /// name of an arrow an override of a dimension gives by name, as one of R12 gives it (<c>1070 5</c>, <c>6</c> or
        /// <c>7</c>, then <c>1000</c> the name, in ACAD's DSTYLE); every record not about it keeping its bytes. What is read of
        /// the entities is read again. The number of records written anew.
        /// </summary>
        internal int Rename(ICollection<string> sections, Func<string, int, bool> names, int dataCode, string from, string to, bool arrows = false)
        {
            int written = 0;

            foreach (Section section in _sections)
            {
                if (!sections.Contains(section.Name))
                {
                    continue;
                }

                // The overrides of the dimensions of model space, paper space and the blocks, as NamesArrowInOverrides reads them.
                bool dimensions = arrows && (section.Name == "ENTITIES" || section.Name == "BLOCKS");

                VisitInside(section, (Rec record, out Rec? now) =>
                {
                    now = null;
                    List<Pair> pairs = PairsOf(record);
                    int data = DataAt(record, pairs);
                    bool braced = false;
                    bool changed = false;
                    bool overrides = false;
                    var b = new Builder(this);

                    for (int i = 0; i < pairs.Count; i++)
                    {
                        Pair pair = pairs[i];
                        overrides = dimensions && i >= data && DimensionOverrides(record, pairs, i, overrides);

                        if (i > 0 && pair.Code == 102)
                        {
                            braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                        }
                        else if ((i > 0 && ((i < data && !braced && names(record.Type, pair.Code)) || (i >= data && pair.Code == dataCode))
                            && string.Equals(DxfReader.NameOf(Text(record, pair)), from, StringComparison.OrdinalIgnoreCase))
                            || (overrides && string.Equals(OverrideArrow(record, pairs, i), from, StringComparison.OrdinalIgnoreCase)))
                        {
                            b.Raw(record, pair.Start, pair.ValueStart);
                            b.Value(to);
                            b.Raw(record, pair.ValueEnd, pair.End);
                            changed = true;
                            continue;
                        }

                        b.Raw(record, pair);
                    }

                    if (!changed)
                    {
                        return false;
                    }

                    now = b.Done(record.Type);
                    written++;
                    return true;
                });
            }

            if (written > 0)
            {
                ForgetReadings();
                Edited();
            }

            // The names of the tables are read again, whatever was renamed, so that the old name is no longer found.
            _layers = NewLayers();
            _layerNames = null;
            _linetypes = null;
            return written;
        }

        // What the records of the file use, counted, as the erasing of a block asks of every record: read in one walk of the file
        // when a block is first erased, and kept from then on, each record put into the file counted on and each taken out
        // counted off; null again once a record is rewritten in its place, or the code page changes, to be read again.
        private Usage _usage;

        /// <summary>
        /// What the records of a file use, as <see cref="Names"/>, <see cref="NamesArrowInOverrides"/> and <see cref="PointedAt"/>
        /// read them, counted over every section: the names INSERTs, dimensions and tables draw by (2), the names dimension
        /// styles (5 to 7) and the overrides of dimensions give as arrows, and the handles pointers point at. A name or a handle
        /// not among them is used by no record, as each of them would find; one among them may be.
        /// </summary>
        internal sealed class Usage
        {
            /// <summary>The names an INSERT, a dimension or a table draws by, letters of either case the same.</summary>
            internal Dictionary<string, int> Drawn { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>The names a dimension style or the override of a dimension gives as an arrow, letters of either case the same.</summary>
            internal Dictionary<string, int> Arrows { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>The handles pointed at.</summary>
            internal Dictionary<ulong, int> Pointed { get; } = new Dictionary<ulong, int>();

            /// <summary>Whether a record may draw by a block of the name, as <see cref="Names"/> finds one for a block's erasing; false where none does.</summary>
            internal bool MayDraw(string name) => Drawn.ContainsKey(name);

            /// <summary>Whether a record may give a block of the name as an arrow, as a dimension style's 5 to 7 or an override does; false where none does.</summary>
            internal bool MayBeArrow(string name) => Arrows.ContainsKey(name);

            /// <summary>Whether a record may point at a handle, as <see cref="PointedAt"/> finds one; false where none does.</summary>
            internal bool MayPointAt(string handle) => !TryHex(handle.Trim(), out ulong wanted) || Pointed.ContainsKey(wanted);

            /// <summary>Counts a use on, or off.</summary>
            internal static void Count<T>(Dictionary<T, int> uses, T key, int by)
            {
                uses.TryGetValue(key, out int count);
                count += by;

                if (count > 0)
                {
                    uses[key] = count;
                }
                else
                {
                    uses.Remove(key);
                }
            }
        }

        /// <summary>What the records of the file use, as <see cref="Usage"/> says: read in one walk of the file where it is not known.</summary>
        internal Usage UsageOfRecords()
        {
            if (_usage == null)
            {
                var usage = new Usage();

                foreach (Rec record in Current(0, _starts.Length))
                {
                    CountUsage(usage, record, 1);
                }

                _usage = usage;
            }

            return _usage;
        }

        /// <summary>Counts what records put into the file use, where what is used is known.</summary>
        private void UsagePut(List<Rec> records)
        {
            foreach (Rec record in records)
            {
                CountUsage(_usage, record, 1);
            }
        }

        /// <summary>Counts off what records taken out of the file used, where what is used is known.</summary>
        private void UsageGone(List<Rec> records)
        {
            foreach (Rec record in records)
            {
                CountUsage(_usage, record, -1);
            }
        }

        /// <summary>Forgets what the records use, a record being rewritten in its place, to be read again when next asked for.</summary>
        private void UsageChanged() => _usage = null;

        /// <summary>
        /// Counts what a record uses on or off, as <see cref="Names"/> reads it for the erasing of a block (the own groups before
        /// the extended data, outside the groups of the applications: an INSERT's, a dimension's or a table's 2, a dimension
        /// style's 5 to 7), as <see cref="NamesArrowInOverrides"/> reads its extended data and as <see cref="PointedAt"/> reads
        /// its pointers.
        /// </summary>
        private void CountUsage(Usage usage, Rec record, int by)
        {
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            bool drawing = record.Type == "INSERT" || record.Type == "DIMENSION" || record.Type == "ACAD_TABLE";
            bool styled = record.Type == "DIMSTYLE";
            bool braced = false;

            for (int i = 1; (drawing || styled) && i < data; i++)
            {
                int code = pairs[i].Code;

                if (code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && ((drawing && code == 2) || (styled && code >= 5 && code <= 7)))
                {
                    Usage.Count(drawing ? usage.Drawn : usage.Arrows, DxfReader.NameOf(Text(record, pairs[i])), by);
                }
            }

            bool overrides = false;

            for (int i = data; i < pairs.Count; i++)
            {
                overrides = DimensionOverrides(record, pairs, i, overrides);
                string arrow = overrides ? OverrideArrow(record, pairs, i) : null;

                if (arrow != null)
                {
                    Usage.Count(usage.Arrows, arrow, by);
                }
            }

            braced = false;
            bool owner = false;

            for (int i = 1; i < pairs.Count; i++)
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

                if (pair.Code == 330 && !owner)
                {
                    owner = true;
                    continue;
                }

                bool pointer = (pair.Code >= 320 && pair.Code <= 369) || (pair.Code >= 390 && pair.Code <= 399) || pair.Code == 480 || pair.Code == 481 || pair.Code == 1005;

                if (pointer && TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong value))
                {
                    Usage.Count(usage.Pointed, value, by);
                }
            }
        }

        /// <summary>
        /// Whether a group of the records of the sections given names something by a name, letters of either case the same, as
        /// <see cref="Rename"/> finds one.
        /// </summary>
        internal bool Names(ICollection<string> sections, Func<string, int, bool> names, string name)
        {
            foreach (Section section in _sections)
            {
                if (!sections.Contains(section.Name))
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    List<Pair> pairs = PairsOf(record);
                    int data = DataAt(record, pairs);
                    bool braced = false;

                    for (int i = 1; i < data; i++)
                    {
                        if (pairs[i].Code == 102)
                        {
                            braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                        }
                        else if (!braced && names(record.Type, pairs[i].Code) && string.Equals(DxfReader.NameOf(Text(record, pairs[i])), name, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a record of the file points at a handle by a pointer (320 to 369, 390 to 399, 480, 481, or 1005 of extended
        /// data), its owner (the first 330) and the lists of reactors aside, which the removal of what they point at sees to.
        /// </summary>
        internal bool PointedAt(string handle)
        {
            if (handle == null || !TryHex(handle.Trim(), out ulong wanted) || wanted == 0)
            {
                return false;
            }

            foreach (Section section in _sections)
            {
                if (section.Name == "HEADER")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    List<Pair> pairs = PairsOf(record);
                    bool braced = false;
                    bool owner = false;

                    for (int i = 1; i < pairs.Count; i++)
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

                        if (pair.Code == 330 && !owner)
                        {
                            owner = true;
                            continue;
                        }

                        bool pointer = (pair.Code >= 320 && pair.Code <= 369) || (pair.Code >= 390 && pair.Code <= 399) || pair.Code == 480 || pair.Code == 481 || pair.Code == 1005;

                        if (pointer && TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong value) && value == wanted)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Which of the handles given a record of the file points at, by a pointer as <see cref="PointedAt"/> finds one, its owner
        /// and its reactors aside, and which a FIELD names as one of its own (360, in <paramref name="named"/>); the records of
        /// the handles to leave aside, and those of the types to leave aside, not read.
        /// One reading of the file for them all, so that the fields of a table of 16 000 cells are judged in one pass.
        /// </summary>
        /// <returns>The handles pointed at, of those given, as they were given, each with the handle of the first record of the
        /// file that points at it (empty for one of no handle).</returns>
        internal Dictionary<string, string> PointedAmong(IEnumerable<string> handles, IEnumerable<string> recordsAside, ICollection<string> typesAside, out Dictionary<string, string> named)
        {
            named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var wanted = new Dictionary<ulong, string>();
            var leftAside = new HashSet<ulong>();
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in handles)
            {
                if (handle != null && TryHex(handle.Trim(), out ulong key) && key != 0 && !wanted.ContainsKey(key))
                {
                    wanted.Add(key, handle);
                }
            }

            foreach (string handle in recordsAside)
            {
                if (handle != null && TryHex(handle.Trim(), out ulong key))
                {
                    leftAside.Add(key);
                }
            }

            if (wanted.Count == 0)
            {
                return found;
            }

            WholeReadings++;

            foreach (Section section in _sections)
            {
                if (section.Name == "HEADER")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    if (typesAside.Contains(record.Type))
                    {
                        continue;
                    }

                    List<Pair> pairs = PairsOf(record);
                    bool handled = pairs.Count > 1 && pairs[1].Code == 5;

                    if (handled && TryHex(record.Bytes, pairs[1].ValueStart, pairs[1].ValueEnd, out ulong own) && leftAside.Contains(own))
                    {
                        continue;
                    }

                    bool braced = false;
                    bool owner = false;

                    for (int i = 1; i < pairs.Count; i++)
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

                        if (pair.Code == 330 && !owner)
                        {
                            owner = true;
                            continue;
                        }

                        bool pointer = (pair.Code >= 320 && pair.Code <= 369) || (pair.Code >= 390 && pair.Code <= 399) || pair.Code == 480 || pair.Code == 481 || pair.Code == 1005;

                        if (pointer && TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong value) && wanted.TryGetValue(value, out string given) && !found.ContainsKey(given))
                        {
                            found.Add(given, handled ? Text(record, pairs[1]).Trim() : string.Empty);
                        }

                        // The first FIELD that names it as one of its own fields (360), which can own it.
                        if (pointer && pair.Code == 360 && handled && record.Type == "FIELD" && TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong child) && wanted.TryGetValue(child, out string owned) && !named.ContainsKey(owned))
                        {
                            named.Add(owned, Text(record, pairs[1]).Trim());
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// What is read of the entities, their look and their shapes, read again from their records: after a change written into
        /// many records at once, as a layer renamed. Those of model space, paper space and every block, each block's own record
        /// among them, as far as they were read.
        /// </summary>
        private void ForgetReadings()
        {
            if (_drawing.IsValueCreated)
            {
                Forget(ModelSpace);
                Forget(PaperSpace);
            }

            if (_blockIndex != null)
            {
                foreach (DxfFileBlock block in _blockIndex.Values)
                {
                    block.Head.Changed(block.Head.Type);
                    Forget(block.Items);
                }
            }

            _layers = NewLayers();
            _layerNames = null;
            _linetypes = null;

            void Forget(DxfSpace space)
            {
                foreach (DxfItem item in space.Snapshot())
                {
                    item.Changed(item.Type);
                }
            }
        }

        /// <summary>
        /// Gives a block a new name in the file's list of blocks, its entities named as of it: its records are renamed by
        /// <see cref="Rename"/>.
        /// </summary>
        internal void Renamed(DxfFileBlock block, string name)
        {
            if (_blockIndex != null && _blockIndex.TryGetValue(block.Name, out DxfFileBlock listed) && listed == block)
            {
                _blockIndex.Remove(block.Name);
                _blockIndex[name] = block;
            }

            _anonymousFrom?.Clear();
            block.Name = name;

            foreach (DxfItem item in block.Items.Snapshot())
            {
                item.Block = name;
            }

            Edited();
        }

        /// <summary>
        /// Takes a block out of the file, as AutoCAD erases one: its BLOCK, its entities and its ENDBLK put out, each handle not
        /// given again and what they owned removed with them on saving; each item reads as it was, removed. Its BLOCK_RECORD is
        /// taken out by <see cref="RemoveTableRecord"/>.
        /// </summary>
        internal void RemoveBlock(DxfFileBlock block)
        {
            var items = new List<DxfItem>(block.Items.Snapshot()) { block.Head };
            var kept = new List<List<Rec>>(items.Count);

            foreach (DxfItem item in items)
            {
                kept.Add(RecordsOf(item));
            }

            if (block.Home != null)
            {
                int from = block.Home.IndexOf(block.Head.Piece);
                int to = block.Home.IndexOf(block.EndPiece);

                for (int i = to; i >= from && from >= 0; i--)
                {
                    foreach (Rec record in block.Home[i].Records)
                    {
                        Retire(record);
                    }

                    if (_usage != null)
                    {
                        UsageGone(block.Home[i].Records);
                    }

                    block.Home.RemoveAt(i);
                }
            }
            else
            {
                // A block with no ENDBLK ends at the record after its last entity, which is another's: what was appended to the
                // block stands before that record, and goes; the record stays.
                bool ended = _types[block.End] == "ENDBLK";

                foreach (Rec record in Current(block.Head.First, block.End + (ended ? 1 : 0)))
                {
                    Retire(record);
                }

                for (int i = block.Head.First; i < block.End; i++)
                {
                    Put(i, new List<Piece>());
                }

                List<Piece> last = _slots?[block.End];

                if (ended)
                {
                    Put(block.End, new List<Piece>());
                }
                else if (last != null)
                {
                    for (int i = last.Count - 2; i >= 0; i--)
                    {
                        last[i].Records.ForEach(Retire);

                        if (_usage != null)
                        {
                            UsageGone(last[i].Records);
                        }

                        last.RemoveAt(i);
                    }
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                items[i].Piece = new Piece(kept[i]);
                items[i].IsRemoved = true;
            }

            if (_blockIndex != null && _blockIndex.TryGetValue(block.Name, out DxfFileBlock listed) && listed == block)
            {
                _blockIndex.Remove(block.Name);
            }

            _anonymousFrom?.Clear();
            Edited();
        }

        /// <summary>
        /// Moves an entity of the file into a space of it, as AutoCAD's <c>AssumeOwnershipOf</c>: its records taken out of where
        /// they are and put at the end of the space, its handles kept, its owner the space's from R13 on and its group 67 as the
        /// space has it. The item of the moved records, which the entity's object stands for from then on; the old one reads as
        /// it was, removed.
        /// </summary>
        internal DxfItem Move(DxfItem item, DxfSpace space)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            bool paper = space.Kind == DxfSpaceKind.Paper;
            bool sheet = InPaperSpace(space);
            string owner = Style.Markers ? OwnerOf(space) : null;
            string left = owner == null ? null : OwnerOf(item);
            string handle = item.Handle;
            var moved = new List<Rec>(records.Count);

            for (int r = 0; r < records.Count; r++)
            {
                Rec record = records[r];

                if (r == 0 && owner != null && WithGroup(record, 330, owner, false, out Rec owned))
                {
                    record = owned;
                }

                // A record that follows is in paper space as its entity is, as an appended one is written: given its 67 there,
                // and one it has taken out elsewhere.
                bool named = r == 0 || sheet || PairsOf(record).Exists(p => p.Code == 67);

                if (named && WithGroup(record, 67, sheet ? "1" : null, true, out Rec spaced))
                {
                    record = spaced;
                }

                moved.Add(record);
            }

            if (item.First < 0)
            {
                if (item.Home.Remove(item.Piece) && _usage != null)
                {
                    UsageGone(item.Piece.Records);
                }
            }
            else
            {
                for (int i = item.First; i < item.First + item.Count; i++)
                {
                    Put(i, new List<Piece>());
                }
            }

            item.Piece = new Piece(records);
            item.IsRemoved = true;
            item.Space?.Removed();

            var piece = new Piece(moved);
            var to = new DxfItem(this, piece, moved[0].Type) { Home = Place(space, piece), IsPaper = paper, Block = space.Block?.Name };
            TakeFollowers(item, to);
            space.Append(to);

            // Moved within its block, as a layout's viewports behind the viewport 1 put ahead of them, it keeps its place in the
            // block's sort table.
            if (owner != null && !string.Equals(left, owner, StringComparison.OrdinalIgnoreCase))
            {
                Resorted(handle, left, owner);
            }

            if (space.Kind != DxfSpaceKind.Block)
            {
                GrowBy(to);
            }

            Edited();
            return to;
        }

        /// <summary>
        /// Moves an entity between the sort tables of the blocks it leaves and enters, as AutoCAD's does: out of each
        /// SORTENTSTABLE of the block it left (its 331 and the 5 after it taken out), and last into that of the block it enters
        /// where that block has one, its own handle as its sort handle; every other table as it is.
        /// </summary>
        private void Resorted(string entity, string left, string entered)
        {
            if (entity == null)
            {
                return;
            }

            foreach (Section section in _sections)
            {
                if (section.Name != "OBJECTS")
                {
                    continue;
                }

                VisitInside(section, (Rec record, out Rec? now) =>
                {
                    now = null;

                    if (record.Type != "SORTENTSTABLE")
                    {
                        return false;
                    }

                    List<Pair> pairs = PairsOf(record);
                    int data = DataAt(record, pairs);
                    int body = BodyAt(record, pairs, data);
                    string block = null;

                    for (int i = body + 1; body >= 0 && i < data && block == null; i++)
                    {
                        if (pairs[i].Code == 330)
                        {
                            block = Text(record, pairs[i]).Trim();
                        }
                    }

                    bool leaves = string.Equals(block, left, StringComparison.OrdinalIgnoreCase);
                    bool enters = string.Equals(block, entered, StringComparison.OrdinalIgnoreCase);

                    if (!leaves && !enters)
                    {
                        return false;
                    }

                    var b = new Builder(this);
                    bool changed = false;

                    for (int i = 0; i < pairs.Count; i++)
                    {
                        if (i == data && enters)
                        {
                            b.Group(331, entity);
                            b.Group(5, entity);
                            changed = true;
                        }

                        if (leaves && i > body && i < data && pairs[i].Code == 331 && string.Equals(Text(record, pairs[i]).Trim(), entity, StringComparison.OrdinalIgnoreCase))
                        {
                            changed = true;

                            if (i + 1 < data && pairs[i + 1].Code == 5)
                            {
                                i++;
                            }

                            continue;
                        }

                        b.Raw(record, pairs[i]);
                    }

                    if (data == pairs.Count && enters)
                    {
                        b.Group(331, entity);
                        b.Group(5, entity);
                        changed = true;
                    }

                    if (!changed)
                    {
                        return false;
                    }

                    now = b.Done(record.Type);
                    return true;
                });
            }
        }

        /// <summary>Whether the file's CLASSES section lists a class of a DXF name (its group 1), as an object of a class not built in needs.</summary>
        internal bool HasClass(string dxfName)
        {
            foreach (Section section in _sections)
            {
                if (section.Name != "CLASSES")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    if (record.Type == "CLASS" && string.Equals((Record(record).Text(1) ?? string.Empty).Trim(), dxfName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The handle of the named object dictionary, the first DICTIONARY of the OBJECTS section; null where there is none.</summary>
        /// <summary>
        /// The handle of the root dictionary as <see cref="RootDictionaryHandle"/> finds it, kept while its record stands, so
        /// that a lookup through it, as the fields of 6 000 texts taken out each look for the drawing's list of fields, reads
        /// no section: the root is the first DICTIONARY of the OBJECTS section, and the library puts new objects after it.
        /// </summary>
        internal string KeptRootDictionaryHandle()
        {
            if (_keptRoot == null || !(ObjectItem(_keptRoot) is DxfItem item) || item.IsRemoved || item.Type != "DICTIONARY")
            {
                _keptRoot = RootDictionaryHandle();
            }

            return _keptRoot;
        }

        private string _keptRoot;

        internal string RootDictionaryHandle()
        {
            Rec? root = RootDictionary();
            return root != null && HandleOf(root.Value, out ulong handle) ? handle.ToString("X", CultureInfo.InvariantCulture) : null;
        }

        #endregion
    }
}
