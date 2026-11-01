using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        #region Dynamic blocks of a drawing inserted

        // The entries of an extension dictionary copied: those that make a block dynamic, and the one that makes an anonymous
        // block or an INSERT stand for a dynamic block.
        private static readonly string[] DefinitionEntries = { "ACAD_ENHANCEDBLOCK", "AcDbDynamicBlockRoundTripPurgePreventer" };
        private static readonly string[] RepresentationEntries = { "AcDbBlockRepresentation" };

        // The applications of the extended data of a dynamic block's record, and of an anonymous block standing for one.
        private static readonly string[] DefinitionData = { "AcDbDynamicBlockTrueName", "AcDbDynamicBlockGUID", "AcDbBlockRepETag" };
        private static readonly string[] RepresentationData = { "AcDbBlockRepBTag" };

        /// <summary>
        /// While <c>DxfDatabase.Insert</c> copies another drawing into this one, or <c>WblockCloneObjects</c> copies entities of
        /// another into it, the entities and blocks it copied, for <see cref="CopyDynamicBlocks(DxfFile, InsertCopies)"/>; null
        /// at any other time.
        /// </summary>
        internal InsertCopies Copies { get; set; }

        /// <summary>Whether this drawing keeps a dynamic block copied into it dynamic: one of R2010 (AC1024) or later, with objects and handles.</summary>
        internal bool KeepsDynamicBlocks => Supports(1024) && HoldsObjects && UsesHandles();

        /// <summary>
        /// The dynamic block of the other drawing an INSERT of it places through an anonymous block standing for it, the 1005 of
        /// that block's AcDbBlockRepBTag naming its record, as <c>DxfBlockReference.DynamicBlockTableRecord</c> finds it; null
        /// where the INSERT places a named block or a block that stands for none. The dynamic block an INSERT places, itself or
        /// so, is noted in <see cref="Copies"/> (<see cref="InsertCopies.Dynamic"/>). The blocks are found by name and by handle
        /// through the other drawing's block table, which reads them once, not once for each call.
        /// </summary>
        private DxfFileBlock DynamicBlockPlaced(DxfFile origin, Rec insert)
        {
            DxfFileBlock block = origin.BlockDrawnBy(insert);
            DxfBlockTable table = origin.Database?.BlockTable;

            if (block == null || table == null || !table.Has(block.Name))
            {
                return null;
            }

            DxfBlockTableRecord record = table[block.Name];

            if (record.KeepsDynamicData)
            {
                Placed(record.Name);
                return null;
            }

            IReadOnlyList<DxfTypedValue> tags = IsAnonymous(block.Name) ? record.GetXDataForApplication("AcDbBlockRepBTag") : null;
            string tag = null;

            foreach (DxfTypedValue group in tags ?? new DxfTypedValue[0])
            {
                if (group.TypeCode == 1005)
                {
                    tag = group.Value;
                    break;
                }
            }

            DxfBlockTableRecord dynamic = tag == null ? null : table.ByHandle(tag);

            if (dynamic == null || !dynamic.KeepsDynamicData || !origin.Blocks.TryGetValue(dynamic.Name, out DxfFileBlock brought))
            {
                return null;
            }

            Placed(dynamic.Name);
            return brought;

            void Placed(string name)
            {
                if (!Copies.Dynamic.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                {
                    Copies.Dynamic.Add(name);
                }
            }
        }

        /// <summary>
        /// Copies what makes the dynamic blocks of another drawing dynamic into this one, after <c>DxfDatabase.Insert</c> or
        /// <c>WblockCloneObjects</c> copied its entities and blocks (<paramref name="copies"/>), as AutoCAD's insert keeps it:
        /// the extended data of each dynamic block copied (its true name, GUID and tags) and the entries ACAD_ENHANCEDBLOCK (the
        /// evaluation graph with its nodes) and AcDbDynamicBlockRoundTripPurgePreventer of its extension dictionary; the
        /// extended data AcDbBlockRepBTag and the entry AcDbBlockRepresentation of each copy of an anonymous block that stands
        /// for one; and the entry
        /// AcDbBlockRepresentation (the representation data and the cache of the values) of each INSERT copied that stands for
        /// one. Each object comes with every object it owns, given a handle of its own here, in the order the other drawing owns
        /// them, once for each owner here; every handle among them names the copy of what it named: an object copied with it,
        /// an entity copied, the block copied or this drawing's own of the name, a record of a table of the same name, else
        /// null (0). A dynamic block of a name this drawing has is this drawing's own: what stands for it stands for this
        /// drawing's where that is dynamic, else comes static. The classes of the objects are listed and the applications of
        /// their extended data registered. Nothing in a drawing older than R2010, in which the library writes no dynamic block,
        /// nor where the other drawing has none.
        /// </summary>
        internal void CopyDynamicBlocks(DxfFile origin, InsertCopies copies)
        {
            DxfDatabase source = origin.Database;
            DxfDatabase target = Database;

            if (source == null || target == null || !KeepsDynamicBlocks)
            {
                return;
            }

            // The block records of the other drawing by handle, each with the record here its pointers name (the copy of an
            // anonymous block, which the call copies once); and those of its dynamic blocks with the record here dynamic in its place.
            // WblockCloneObjects, which may copy one INSERT at a time from a drawing of thousands of blocks, gives those its
            // copies brought and the dynamic blocks they place, in the order of their handles; a pointer to any other block is
            // taken to the block of its name here, as a record of a table is.
            List<DxfBlockTableRecord> blocks = copies.BringsDynamicBlocks ? Named(source, copies) : new List<DxfBlockTableRecord>(source.BlockTable);

            if (!blocks.Exists(b => b.KeepsDynamicData))
            {
                return;
            }

            var records = new Dictionary<ulong, string>();
            var dynamic = new Dictionary<ulong, string>();

            foreach (DxfBlockTableRecord block in blocks)
            {
                bool copied = copies.Blocks.TryGetValue(block.Name, out List<string> names);
                string name = copied ? names[0] : block.Name;

                if (!TryHex(block.Handle ?? string.Empty, out ulong handle) || handle == 0 || (!copied && IsAnonymous(block.Name)) || !target.BlockTable.Has(name))
                {
                    continue;
                }

                DxfBlockTableRecord here = target.BlockTable[name];
                records[handle] = here.Handle;

                if (block.KeepsDynamicData && (copied || here.KeepsDynamicData))
                {
                    dynamic[handle] = here.Handle;
                }
            }

            if (dynamic.Count == 0)
            {
                return;
            }

            // The records of the tables and the objects of the other drawing are read once for the pointers taken by name.
            HoldTableNames();

            try
            {
                CopyDynamicBlocks(origin, copies, blocks, records, dynamic);
            }
            finally
            {
                ReleaseTableNames();
            }
        }

        /// <summary>
        /// The blocks of the other drawing <c>WblockCloneObjects</c> copied and the dynamic blocks its INSERTs place
        /// (<see cref="InsertCopies.Dynamic"/>), each once, in the order of their handles, then of their names.
        /// </summary>
        private static List<DxfBlockTableRecord> Named(DxfDatabase source, InsertCopies copies)
        {
            var blocks = new List<DxfBlockTableRecord>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in copies.Blocks.Keys.Concat(copies.Dynamic))
            {
                if (source.BlockTable.Has(name) && seen.Add(name))
                {
                    blocks.Add(source.BlockTable[name]);
                }
            }

            blocks.Sort((a, b) =>
            {
                TryHex(a.Handle ?? string.Empty, out ulong x);
                TryHex(b.Handle ?? string.Empty, out ulong y);
                int byHandle = x.CompareTo(y);
                return byHandle != 0 ? byHandle : string.CompareOrdinal(a.Name, b.Name);
            });

            return blocks;
        }

        /// <summary>
        /// Copies what makes the dynamic blocks of the other drawing dynamic, as <see cref="CopyDynamicBlocks(DxfFile, InsertCopies)"/>
        /// says, given its blocks, the records here its block records are taken to, and those of its dynamic blocks.
        /// </summary>
        private void CopyDynamicBlocks(DxfFile origin, InsertCopies copies, List<DxfBlockTableRecord> blocks, Dictionary<ulong, string> records, Dictionary<ulong, string> dynamic)
        {
            DxfDatabase source = origin.Database;
            DxfDatabase target = Database;
            var units = new List<Unit>();
            var none = new Dictionary<ulong, string>();

            // Each copy of a block that is dynamic, or stands for one that is dynamic here.
            foreach (DxfBlockTableRecord block in blocks)
            {
                if (!copies.Blocks.TryGetValue(block.Name, out List<string> names) || !TryHex(block.Handle ?? string.Empty, out ulong handle))
                {
                    continue;
                }

                bool definition = dynamic.ContainsKey(handle);
                string stood = definition ? null : origin.Represented(block.Item);

                if (!definition && (stood == null || !TryHex(stood, out ulong of) || !dynamic.ContainsKey(of)))
                {
                    continue;
                }

                var data = new List<DxfTypedValue>();

                foreach (string application in definition ? DefinitionData : RepresentationData)
                {
                    foreach (DxfTypedValue group in block.GetXDataForApplication(application) ?? new DxfTypedValue[0])
                    {
                        data.Add(group.TypeCode == 1005 ? new DxfTypedValue(1005, Mapped(group.Value, none, none, records, origin, copies)) : group);
                    }
                }

                string xdictionary = origin.Xdictionary(block.Item);

                foreach (string name in names)
                {
                    DxfBlockTableRecord here = target.BlockTable[name];

                    if (data.Count > 0)
                    {
                        here.XData = data;
                    }

                    Plan(origin, xdictionary, here.Item, definition ? DefinitionEntries : RepresentationEntries, units);
                }
            }

            // Each INSERT copied that stands for a block dynamic here, in the order of the copies.
            var inserts = new List<KeyValuePair<ulong, string>>(copies.Entities);
            inserts.Sort((a, b) =>
            {
                TryHex(a.Value, out ulong x);
                TryHex(b.Value, out ulong y);
                int byCopy = x.CompareTo(y);
                return byCopy != 0 ? byCopy : a.Key.CompareTo(b.Key);
            });

            foreach (KeyValuePair<ulong, string> copy in inserts)
            {
                DxfItem was = source.ItemByHandle(copy.Key.ToString("X", CultureInfo.InvariantCulture));
                string stood = was == null || was.Type != "INSERT" ? null : origin.Represented(was);
                DxfItem now = stood != null && TryHex(stood, out ulong of) && dynamic.ContainsKey(of) ? target.ItemByHandle(copy.Value) : null;

                if (now != null && !now.IsRemoved)
                {
                    Plan(origin, origin.Xdictionary(was), now, RepresentationEntries, units);
                }
            }

            // The first copy of each object, what a handle of another owner's objects names.
            var first = new Dictionary<ulong, string>();

            foreach (Unit unit in units)
            {
                foreach (KeyValuePair<ulong, string> made in unit.Made)
                {
                    if (!first.ContainsKey(made.Key))
                    {
                        first.Add(made.Key, made.Value);
                    }
                }
            }

            var applications = new List<string>();
            var classes = new List<string>();
            int nulled = 0;
            int written = 0;

            foreach (Unit unit in units)
            {
                foreach (DxfItem item in unit.Order)
                {
                    Rec record = CopiedObject(origin, item, item == unit.Root ? unit.Kept : null, unit.Made, first, records, copies, applications, ref nulled);
                    TryHex(item.Handle, out ulong was);
                    AddCopiedObject(record, unit.Made[was]);
                    written++;

                    if (!classes.Contains(record.Type))
                    {
                        classes.Add(record.Type);
                    }
                }

                TryHex(unit.Root.Handle, out ulong root);
                SetExtensionDictionary(unit.Owner, unit.Made[root]);
            }

            foreach (string application in applications)
            {
                EnsureApplication(application);
            }

            foreach (string type in classes)
            {
                origin.CopyClass(this, type);
            }

            GeometryHelperLog.Info("DxfFile: " + dynamic.Count.ToString(CultureInfo.InvariantCulture) + " dynamic blocks of the drawing inserted stay dynamic, with "
                + written.ToString(CultureInfo.InvariantCulture) + " objects copied.");

            if (nulled > 0)
            {
                GeometryHelperLog.Debug("DxfFile: " + nulled.ToString(CultureInfo.InvariantCulture) + " handles of the dynamic blocks' objects copied named nothing copied nor of a name here, and are null.");
            }
        }

        /// <summary>
        /// Plans the copy of an extension dictionary of the other drawing (its handle, null for none) for an owner here: the
        /// dictionary with the entries of the names given, and every object they own, each given a handle here; nothing where
        /// it has none of them.
        /// </summary>
        private void Plan(DxfFile origin, string xdictionary, DxfItem owner, string[] entries, List<Unit> units)
        {
            DxfItem root = origin.ObjectItem(xdictionary);

            if (root == null || root.Type != "DICTIONARY" || !Array.Exists(entries, e => origin.Entry(origin.RecordAt(root, 0), e) != null))
            {
                return;
            }

            var unit = new Unit(owner, root, entries);
            Collect(origin, root, entries, unit);
            units.Add(unit);
        }

        /// <summary>
        /// Adds an object of the other drawing to those to copy for an owner here, given a handle here, and then each object it
        /// owns (350, 360, its extension dictionary among them), depth first in the order of its groups; of a dictionary whose
        /// entries are chosen (<paramref name="kept"/>), only those. An object met again is copied once.
        /// </summary>
        private void Collect(DxfFile origin, DxfItem item, string[] kept, Unit unit)
        {
            if (!TryHex(item.Handle ?? string.Empty, out ulong handle) || handle == 0 || unit.Made.ContainsKey(handle))
            {
                return;
            }

            unit.Made.Add(handle, NewHandle());
            unit.Order.Add(item);
            Rec record = origin.RecordAt(item, 0);
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            bool skipped = false;

            for (int i = 1; i < data; i++)
            {
                int code = pairs[i].Code;

                if (kept != null && code == 3)
                {
                    skipped = Array.IndexOf(kept, origin.Text(record, pairs[i]).Trim()) < 0;
                }
                else if ((code == 350 || code == 360) && !skipped && origin.ObjectItem(origin.Text(record, pairs[i])) is DxfItem owned)
                {
                    Collect(origin, owned, null, unit);
                }
            }
        }

        /// <summary>
        /// The record of an object of the other drawing as it is copied here: its groups as they are but its handle, that of its
        /// copy; each pointer (320 to 369, 390 to 399, 480, 481) and handle of data (1005) the copy of what it names, as
        /// <see cref="Mapped"/> gives it, a reactor naming nothing left out; of a dictionary whose entries are chosen
        /// (<paramref name="kept"/>), only those. The applications of its extended data are added to those to register.
        /// </summary>
        private Rec CopiedObject(DxfFile origin, DxfItem item, string[] kept, Dictionary<ulong, string> own, Dictionary<ulong, string> first, Dictionary<ulong, string> records, InsertCopies copies, List<string> applications, ref int nulled)
        {
            Rec record = origin.RecordAt(item, 0);
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var b = new Builder(this);
            b.Group(0, record.Type);
            bool reactors = false;
            bool skipped = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                int code = pairs[i].Code;
                string text = origin.Text(record, pairs[i]);

                if (code == 999)
                {
                    continue;
                }

                if (i < data && code == 5)
                {
                    TryHex(text.Trim(), out ulong handle);
                    b.Group(5, own[handle]);
                    continue;
                }

                if (i < data && code == 102)
                {
                    reactors = text.Trim() == "{ACAD_REACTORS";
                }
                else if (i < data && kept != null && code == 3)
                {
                    skipped = Array.IndexOf(kept, text.Trim()) < 0;
                }

                if (skipped && i < data && (code == 3 || code == 350 || code == 360))
                {
                    continue;
                }

                if (i >= data && code == 1001)
                {
                    string application = text.Trim();

                    if (!applications.Exists(a => string.Equals(a, application, StringComparison.OrdinalIgnoreCase)))
                    {
                        applications.Add(application);
                    }
                }

                if ((i < data && IsPointer(code)) || code == 1005)
                {
                    string now = Mapped(text, own, first, records, origin, copies);

                    if (now == "0" && text.Trim() != "0")
                    {
                        if (reactors && code == 330)
                        {
                            continue;
                        }

                        nulled++;
                    }

                    b.Group(code, now);
                    continue;
                }

                b.Group(code, text);
            }

            return b.Done(record.Type);
        }

        /// <summary>
        /// The handle here a handle of the other drawing is taken to: the copy of an object copied for the same owner, else its
        /// first copy; of an entity copied; of a block record (the block copied, or this drawing's own of the name); else of a
        /// record of a table or an object of the named object dictionary of the same name; null (0) where there is none.
        /// </summary>
        private string Mapped(string pointer, Dictionary<ulong, string> own, Dictionary<ulong, string> first, Dictionary<ulong, string> records, DxfFile origin, InsertCopies copies)
        {
            if (!TryHex(pointer.Trim(), out ulong handle) || handle == 0)
            {
                return pointer.Trim();
            }

            if (own.TryGetValue(handle, out string copy) || first.TryGetValue(handle, out copy) || copies.Entities.TryGetValue(handle, out copy) || records.TryGetValue(handle, out copy))
            {
                return copy;
            }

            return Equivalent(origin, pointer) ?? "0";
        }

        /// <summary>Adds the record of an object copied at the end of the OBJECTS section, under its handle, as <see cref="AddObject"/> adds one.</summary>
        private void AddCopiedObject(Rec record, string handle)
        {
            var piece = new Piece(new List<Rec> { record });
            var item = new DxfItem(this, piece, record.Type) { Home = PutBefore(ObjectsEnd(), piece) };
            TryHex(handle, out ulong key);
            ObjectIndex()[key] = item;
            Edited();
        }

        /// <summary>The handle of the extension dictionary of a record (<c>{ACAD_XDICTIONARY</c>); null where it has none.</summary>
        private string Xdictionary(DxfItem item)
        {
            Rec record = RecordAt(item, 0);
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);

            for (int i = 1; i + 1 < data; i++)
            {
                if (pairs[i].Code == 102 && Text(record, pairs[i]).Trim() == "{ACAD_XDICTIONARY" && pairs[i + 1].Code == 360)
                {
                    return Text(record, pairs[i + 1]).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The handle of the dynamic block a block record or an INSERT stands for: the 1005 of its extended data
        /// AcDbBlockRepBTag, else the 340 of the AcDbRepData of the dictionary AcDbBlockRepresentation of its extension
        /// dictionary; null where it stands for none.
        /// </summary>
        private string Represented(DxfItem item)
        {
            Rec record = RecordAt(item, 0);
            List<Pair> pairs = PairsOf(record);
            bool tag = false;

            for (int i = DataAt(record, pairs); i < pairs.Count; i++)
            {
                if (pairs[i].Code == 1001)
                {
                    tag = string.Equals(Text(record, pairs[i]).Trim(), "AcDbBlockRepBTag", StringComparison.OrdinalIgnoreCase);
                }
                else if (tag && pairs[i].Code == 1005)
                {
                    return Text(record, pairs[i]).Trim();
                }
            }

            DxfItem dictionary = ObjectItem(Xdictionary(item));
            DxfItem representation = dictionary == null ? null : ObjectItem(Entry(RecordAt(dictionary, 0), "AcDbBlockRepresentation"));
            DxfItem data = representation == null ? null : ObjectItem(Entry(RecordAt(representation, 0), "AcDbRepData"));
            return data == null ? null : ValueOf(data, 340)?.Trim();
        }

        /// <summary>Lists in another file's CLASSES section the class of a DXF name this file lists, as this file lists it: its C++ name, application and proxy flags; nothing where this file lists none.</summary>
        private void CopyClass(DxfFile target, string dxfName)
        {
            foreach (Section section in _sections)
            {
                if (section.Name != "CLASSES")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    DxfReader.Record read = Record(record);

                    if (record.Type == "CLASS" && string.Equals((read.Text(1) ?? string.Empty).Trim(), dxfName, StringComparison.OrdinalIgnoreCase))
                    {
                        int flags = int.TryParse(read.Text(90)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
                        target.AddClass(dxfName, (read.Text(2) ?? string.Empty).Trim(), flags, (read.Text(3) ?? "ObjectDBX Classes").Trim());
                        return;
                    }
                }
            }
        }

        /// <summary>The objects of the other drawing to copy for one owner here: the owner, the extension dictionary copied for it and the entries kept of it, the objects in the order they are written, and the handle of each one's copy.</summary>
        private sealed class Unit
        {
            internal Unit(DxfItem owner, DxfItem root, string[] kept)
            {
                Owner = owner;
                Root = root;
                Kept = kept;
            }

            internal DxfItem Owner { get; }

            internal DxfItem Root { get; }

            internal string[] Kept { get; }

            internal List<DxfItem> Order { get; } = new List<DxfItem>();

            internal Dictionary<ulong, string> Made { get; } = new Dictionary<ulong, string>();
        }

        /// <summary>The entities and blocks <c>DxfDatabase.Insert</c> or <c>WblockCloneObjects</c> copied from another drawing into this one, while it lasts.</summary>
        internal sealed class InsertCopies
        {
            private readonly Dictionary<ulong, string> _entities = new Dictionary<ulong, string>();

            // The entities copied together and their copies, whose handles are read only where a dynamic block is copied: reading
            // the handle of each of 10 000 splines cost a twentieth of their insert.
            private readonly List<KeyValuePair<DxfItem, DxfItem>> _pending = new List<KeyValuePair<DxfItem, DxfItem>>();

            /// <summary>The handle there of each entity copied, with the handle of its copy here.</summary>
            internal Dictionary<ulong, string> Entities
            {
                get
                {
                    foreach (KeyValuePair<DxfItem, DxfItem> pair in _pending)
                    {
                        Entity(pair.Key.Handle, pair.Value.Handle);
                    }

                    _pending.Clear();
                    return _entities;
                }
            }

            /// <summary>
            /// The name there of each block copied, letters of either case the same, with the names of its copies here in the
            /// order they were made: an anonymous block placed by two INSERTs is copied once for the call, as AutoCAD keeps one.
            /// </summary>
            internal Dictionary<string, List<string>> Blocks { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Whether an INSERT copied brings the dynamic block its anonymous block stands for, as <c>WblockCloneObjects</c>
            /// copies into a drawing that keeps dynamic blocks; <c>DxfDatabase.Insert</c> merges every named block it needs.
            /// </summary>
            internal bool BringsDynamicBlocks { get; set; }

            /// <summary>
            /// The names there of the dynamic blocks the INSERTs copied place, itself or by an anonymous block standing for it, as
            /// found while <see cref="BringsDynamicBlocks"/> lasts, each once.
            /// </summary>
            internal List<string> Dynamic { get; } = new List<string>();

            /// <summary>Notes the copy of an entity, by the two handles.</summary>
            internal void Entity(string was, string copy)
            {
                if (was != null && copy != null && TryHex(was.Trim(), out ulong handle) && handle != 0)
                {
                    _entities[handle] = copy;
                }
            }

            /// <summary>Notes the copy of an entity, by the two items, their handles read when <see cref="Entities"/> is first asked for.</summary>
            internal void Entity(DxfItem was, DxfItem copy) => _pending.Add(new KeyValuePair<DxfItem, DxfItem>(was, copy));

            /// <summary>Notes a copy of a block, by its name there and the name of the copy.</summary>
            internal void Block(string was, string copy)
            {
                if (!Blocks.TryGetValue(was, out List<string> names))
                {
                    names = new List<string>();
                    Blocks.Add(was, names);
                }

                names.Add(copy);
            }
        }

        #endregion
    }
}
