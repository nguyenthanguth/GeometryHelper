using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        private Dictionary<ulong, DxfItem> _objectItems;

        #region Objects of the OBJECTS section

        /// <summary>
        /// The item of an object of the OBJECTS section by its handle, the same item each time while the record is there: the
        /// first record of the handle, as the reader takes the first; null where there is none, it was removed, or the file was
        /// saved without it.
        /// </summary>
        internal DxfItem ObjectItem(string handle)
        {
            if (handle == null || !TryHex(handle.Trim(), out ulong key) || key == 0)
            {
                return null;
            }

            if (!ObjectIndex().TryGetValue(key, out DxfItem item) || item.IsRemoved)
            {
                return null;
            }

            // A record taken out on saving, as an object whose owner was erased, is gone.
            return RecordsOf(item).Count == 0 ? null : item;
        }

        /// <summary>How many objects of the OBJECTS section there are, as <see cref="ObjectItem"/> finds them: those removed, or taken out on saving, left out.</summary>
        internal int ObjectCount()
        {
            int count = 0;

            foreach (DxfItem item in ObjectIndex().Values)
            {
                if (!item.IsRemoved && RecordsOf(item).Count > 0)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The items of the objects by handle, those of the file as read made once, those added put in as they are added.</summary>
        private Dictionary<ulong, DxfItem> ObjectIndex()
        {
            if (_objectItems != null)
            {
                return _objectItems;
            }

            var items = new Dictionary<ulong, DxfItem>();

            foreach (Section section in _sections)
            {
                for (int i = section.First + 1; section.Name == "OBJECTS" && i < section.Last; i++)
                {
                    if (HandleOf(Original(i), out ulong handle) && handle != 0 && !items.ContainsKey(handle))
                    {
                        items.Add(handle, new DxfItem(this, i, 1, _types[i], null));
                    }
                }
            }

            _objectItems = items;
            return items;
        }

        /// <summary>
        /// An object taken out as what owned it went, or as a GROUP left empty: its item, where one was made, reads as it was,
        /// erased.
        /// </summary>
        private void ObjectGone(ulong handle, Rec record)
        {
            if (_objectItems != null && _objectItems.TryGetValue(handle, out DxfItem item) && !item.IsRemoved)
            {
                item.Piece = new Piece(new List<Rec> { record });
                item.IsRemoved = true;
            }
        }

        /// <summary>The ENDSEC of the last OBJECTS section, before which new objects go; -1 where there is none.</summary>
        private int ObjectsEnd()
        {
            int end = -1;

            foreach (Section section in _sections)
            {
                if (section.Name == "OBJECTS")
                {
                    end = section.Last;
                }
            }

            return end;
        }

        /// <summary>
        /// Adds an object at the end of the OBJECTS section, as AutoCAD writes a new one: its type, a handle of its own above every
        /// handle of the file, the list of reactors that names its owner and the owner (330), then the groups given, which begin
        /// with its subclass marker.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the file has no OBJECTS section, or writes no handles.</exception>
        internal DxfItem AddObject(string type, string owner, Action<Builder> groups)
        {
            int end = ObjectsEnd();

            if (end < 0 || !Style.Markers)
            {
                throw new InvalidOperationException("A DXF " + VersionSaid() + " has no OBJECTS section to hold a " + type + ": R13 (AC1012) brought it.");
            }

            if (!UsesHandles())
            {
                throw new InvalidOperationException("The file writes no handles, which an object of the OBJECTS section needs to be pointed at.");
            }

            Dictionary<ulong, DxfItem> index = ObjectIndex();
            string handle = NewHandle();
            var b = new Builder(this);
            b.Group(0, type);
            b.Group(5, handle);

            if (owner != null)
            {
                b.Group(102, "{ACAD_REACTORS");
                b.Group(330, owner);
                b.Group(102, "}");
                b.Group(330, owner);
            }

            groups(b);

            var piece = new Piece(new List<Rec> { b.Done(type) });
            var item = new DxfItem(this, piece, type) { Home = PutBefore(end, piece) };
            TryHex(handle, out ulong key);
            index[key] = item;
            Edited();
            return item;
        }

        /// <summary>
        /// Takes a record out of the file, an object or a record of a table: its handle not given again and what points at it
        /// seen to on saving, the objects it owns going with it; the item reads as it was, removed.
        /// </summary>
        internal void RemoveRecord(DxfItem item)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);

            foreach (Rec record in records)
            {
                Retire(record);
            }

            if (item.First < 0)
            {
                item.Home.Remove(item.Piece);
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
            Edited();
        }

        /// <summary>
        /// Gives a record its extension dictionary, the group <c>{ACAD_XDICTIONARY</c> with the 360 of its handle, where AutoCAD
        /// writes it: after the handle and the groups of the applications that follow it, as its reactors, before the owner; or
        /// takes the group out, the handle being null. Nothing is written where the record is so already.
        /// </summary>
        internal void SetExtensionDictionary(DxfItem item, string dictionary)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            int open = -1;
            int close = -1;

            for (int i = 1; i < data && close < 0; i++)
            {
                if (pairs[i].Code != 102)
                {
                    continue;
                }

                string said = Text(head, pairs[i]).Trim();

                if (said == "{ACAD_XDICTIONARY")
                {
                    open = i;
                }
                else if (said == "}" && open >= 0)
                {
                    close = i;
                }
            }

            if ((dictionary == null) == (open < 0))
            {
                return;
            }

            var b = new Builder(this);

            if (dictionary == null)
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (i < open || i > close)
                    {
                        b.Raw(head, pairs[i]);
                    }
                }
            }
            else
            {
                int put = 1;

                for (int i = 1; i < data && pairs[i].Code != 330 && pairs[i].Code != 100; i++)
                {
                    if (pairs[i].Code == 5 || pairs[i].Code == 105)
                    {
                        put = i + 1;
                    }
                }

                // The groups of the applications after the handle, as its reactors, stay before it.
                while (put < data && pairs[put].Code == 102 && Text(head, pairs[put]).Trim().StartsWith("{", StringComparison.Ordinal))
                {
                    while (put < data && !(pairs[put].Code == 102 && Text(head, pairs[put]).Trim() == "}"))
                    {
                        put++;
                    }

                    put++;
                }

                for (int i = 0; i <= pairs.Count; i++)
                {
                    if (i == put)
                    {
                        b.Group(102, "{ACAD_XDICTIONARY");
                        b.Group(360, dictionary);
                        b.Group(102, "}");
                    }

                    if (i < pairs.Count)
                    {
                        b.Raw(head, pairs[i]);
                    }
                }
            }

            records[0] = b.Done(head.Type);
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
        }

        /// <summary>
        /// Puts a handle in the list of reactors of a record (330 in <c>{ACAD_REACTORS</c>), or takes it out, as AutoCAD writes a
        /// group into the reactors of its members: a list made where the record has none, just after its handle, as AutoCAD
        /// writes one, the handle last in a list it has; a list left empty taken out. Nothing is written where the record is so
        /// already.
        /// </summary>
        internal void SetReactor(DxfItem item, string reactor, bool present)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            int open = -1;
            int close = -1;
            int others = 0;
            var mine = new HashSet<int>();

            for (int i = 1; i < data && close < 0; i++)
            {
                if (pairs[i].Code == 102)
                {
                    string said = Text(head, pairs[i]).Trim();

                    if (open < 0 && said == "{ACAD_REACTORS")
                    {
                        open = i;
                    }
                    else if (open >= 0 && said == "}")
                    {
                        close = i;
                    }
                }
                else if (open >= 0 && pairs[i].Code == 330)
                {
                    if (string.Equals(Text(head, pairs[i]).Trim(), reactor, StringComparison.OrdinalIgnoreCase))
                    {
                        mine.Add(i);
                    }
                    else
                    {
                        others++;
                    }
                }
            }

            if (open >= 0 && close < 0)
            {
                open = -1;
                mine.Clear();
            }

            if (present == (mine.Count > 0))
            {
                return;
            }

            var b = new Builder(this);

            if (present && open >= 0)
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (i == close)
                    {
                        b.Group(330, reactor);
                    }

                    b.Raw(head, pairs[i]);
                }
            }
            else if (present)
            {
                int put = 1;

                for (int i = 1; i < data && pairs[i].Code != 330 && pairs[i].Code != 100 && pairs[i].Code != 102; i++)
                {
                    if (pairs[i].Code == 5 || pairs[i].Code == 105)
                    {
                        put = i + 1;
                    }
                }

                for (int i = 0; i < pairs.Count; i++)
                {
                    if (i == put)
                    {
                        b.Group(102, "{ACAD_REACTORS");
                        b.Group(330, reactor);
                        b.Group(102, "}");
                    }

                    b.Raw(head, pairs[i]);
                }
            }
            else
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    bool emptied = others == 0 && (i == open || i == close);

                    if (!mine.Contains(i) && !emptied)
                    {
                        b.Raw(head, pairs[i]);
                    }
                }
            }

            records[0] = b.Done(head.Type);
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
        }

        /// <summary>
        /// The groups of a record from one of them (counted as <see cref="GroupsOf"/> counts them, after its group 0) to its
        /// extended data, comments left out, each text (codes 1 to 9 and 300 to 309) with its escapes (<c>\U+00E9</c>) read as
        /// letters: the data of an XRECORD, whose groups 102 belong to it.
        /// </summary>
        internal List<DxfTypedValue> TailOf(DxfItem item, int from)
        {
            Rec head = RecordAt(item, 0);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            var groups = new List<DxfTypedValue>();
            int counted = 0;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code != 999 && counted++ >= from)
                {
                    groups.Add(DxfTypedValue.Read(pairs[i].Code, TailText(head, pairs[i])));
                }
            }

            return groups;
        }

        /// <summary>The value of a group of the data of an object as it is read: a text with its escapes read as letters.</summary>
        private string TailText(Rec record, Pair pair)
        {
            string text = Text(record, pair);
            return (pair.Code >= 1 && pair.Code <= 9) || (pair.Code >= 300 && pair.Code <= 309) ? DxfReader.Decode(text) : text;
        }

        /// <summary>
        /// Replaces the groups of a record from one of them (counted as <see cref="GroupsOf"/> counts them, after its group 0) to
        /// its extended data by the groups given, every group before and the extended data kept as they are: the data of an
        /// XRECORD, whose groups 102 belong to it. A group given as <see cref="TailOf"/> reads the one in its place keeps its
        /// bytes, and nothing is written where every group is so.
        /// </summary>
        internal void ReplaceTail(DxfItem item, int from, IList<KeyValuePair<int, string>> groups)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            int start = data;
            int counted = 0;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 999)
                {
                    continue;
                }

                if (counted == from)
                {
                    start = i;
                    break;
                }

                counted++;
            }

            var taken = new List<int>();

            for (int i = start; i < data; i++)
            {
                if (pairs[i].Code != 999)
                {
                    taken.Add(i);
                }
            }

            bool same = taken.Count == groups.Count;

            for (int k = 0; k < taken.Count && k < groups.Count && same; k++)
            {
                same = SameTail(head, pairs[taken[k]], groups[k]);
            }

            if (same)
            {
                return;
            }

            var b = new Builder(this);

            for (int i = 0; i < start; i++)
            {
                b.Raw(head, pairs[i]);
            }

            for (int k = 0; k < groups.Count; k++)
            {
                if (k < taken.Count && SameTail(head, pairs[taken[k]], groups[k]))
                {
                    b.Raw(head, pairs[taken[k]]);
                }
                else
                {
                    b.Group(groups[k].Key, groups[k].Value);
                }
            }

            for (int i = data; i < pairs.Count; i++)
            {
                b.Raw(head, pairs[i]);
            }

            records[0] = b.Done(head.Type);
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
        }

        /// <summary>Whether a group of a record is one given, its code and its text as <see cref="TailOf"/> reads it.</summary>
        private bool SameTail(Rec record, Pair pair, KeyValuePair<int, string> group)
            => pair.Code == group.Key && string.Equals(TailText(record, pair), group.Value, StringComparison.Ordinal);

        /// <summary>
        /// Sets the value of one group of a record (counted as <see cref="GroupsOf"/> counts them, after its group 0), its code
        /// line and line end kept: for a group the code of which a record has twice, in two of its subclasses, as the name (1)
        /// of a LAYOUT. Nothing is written where the value is the one it has.
        /// </summary>
        internal void SetValueAt(DxfItem item, int index, string value)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int counted = 0;

            for (int i = 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 999)
                {
                    continue;
                }

                if (counted++ != index)
                {
                    continue;
                }

                if (string.Equals(Text(head, pairs[i]), value, StringComparison.Ordinal))
                {
                    return;
                }

                var b = new Builder(this);

                for (int k = 0; k < pairs.Count; k++)
                {
                    if (k != i)
                    {
                        b.Raw(head, pairs[k]);
                        continue;
                    }

                    b.Raw(head, pairs[k].Start, pairs[k].ValueStart);
                    b.Value(value);
                    b.Raw(head, pairs[k].ValueEnd, pairs[k].End);
                }

                records[0] = b.Done(head.Type);
                SetRecords(item, records);
                item.Changed(head.Type);
                Edited();
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(index), index, "The record has no group " + Number(index) + ".");
        }

        /// <summary>
        /// Takes one group of a record out (counted as <see cref="GroupsOf"/> counts them, after its group 0), every other group
        /// keeping its bytes: the last active viewport (331) of a LAYOUT whose viewport was erased.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the record has no group of the index.</exception>
        internal void RemoveGroupAt(DxfItem item, int index)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int counted = 0;

            for (int i = 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 999 || counted++ != index)
                {
                    continue;
                }

                var b = new Builder(this);

                for (int k = 0; k < pairs.Count; k++)
                {
                    if (k != i)
                    {
                        b.Raw(head, pairs[k]);
                    }
                }

                records[0] = b.Done(head.Type);
                SetRecords(item, records);
                item.Changed(head.Type);
                Edited();
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(index), index, "The record has no group " + Number(index) + ".");
        }

        /// <summary>How many changes the records have had, so that what is worked out of them all is kept until the next.</summary>
        internal int EditCount => _editCount;

        // The entities that joined a space since they were last taken, kept once someone asks for them.
        private List<DxfItem> _joined;
        private bool _joinedKept;

        /// <summary>Notes an entity that joined a space, for <see cref="TakeJoined"/>; nothing until that is first called.</summary>
        internal void Joined(DxfItem item)
        {
            if (_joinedKept)
            {
                (_joined = _joined ?? new List<DxfItem>()).Add(item);
            }
        }

        /// <summary>The entities that joined a space since this was last called, in their order; none the first time, from which they are kept.</summary>
        internal List<DxfItem> TakeJoined()
        {
            List<DxfItem> joined = _joined ?? new List<DxfItem>();
            _joined = null;
            _joinedKept = true;
            return joined;
        }

        /// <summary>How many records have been taken out of the file since it was read.</summary>
        internal int RemovedCount => _removed?.Count ?? 0;

        // The GROUPs added or emptied since the file was read, to be dropped on saving where they hold no member then.
        private List<DxfItem> _maybeEmptyGroups;

        /// <summary>
        /// Notes a GROUP added, or written with no member, to be dropped when the file is saved where it holds no member then,
        /// as AutoCAD drops an empty group. A group the file was read with, empty, is kept as it was.
        /// </summary>
        internal void GroupMayBeEmpty(DxfItem group)
        {
            _maybeEmptyGroups = _maybeEmptyGroups ?? new List<DxfItem>();

            if (!_maybeEmptyGroups.Contains(group))
            {
                _maybeEmptyGroups.Add(group);
            }
        }

        /// <summary>Takes out each GROUP noted by <see cref="GroupMayBeEmpty"/> that holds no member, its handle with what was removed, so that its entry and the reactors that name it go too.</summary>
        private void DropEmptyGroups()
        {
            if (_maybeEmptyGroups == null)
            {
                return;
            }

            foreach (DxfItem group in _maybeEmptyGroups)
            {
                if (group.IsRemoved)
                {
                    continue;
                }

                bool member = false;

                foreach (DxfTypedValue value in ObjectGroupsOf(group))
                {
                    member |= value.TypeCode == 340;
                }

                if (!member)
                {
                    RemoveRecord(group);
                }
            }

            _maybeEmptyGroups = null;
        }

        /// <summary>Whether a record of a handle has been taken out of the file since it was opened, an entity erased among them.</summary>
        internal bool WasRemoved(string handle)
            => _removed != null && handle != null && TryHex(handle.Trim(), out ulong key) && _removed.Contains(key);

        /// <summary>The groups of the own part of an object (from its first subclass marker), as <see cref="OwnGroupsOf(DxfItem, int)"/> reads an entity's.</summary>
        internal IReadOnlyList<DxfTypedValue> ObjectGroupsOf(DxfItem item) => OwnGroupsOf(item, 0);

        /// <summary>Whether the file holds objects of the OBJECTS section: from R13 on, with such a section.</summary>
        internal bool HoldsObjects => Style.Markers && ObjectsEnd() >= 0;

        /// <summary>The bytes of the THUMBNAILIMAGE section: its groups 310 one after another; null where the file has none.</summary>
        internal byte[] Thumbnail()
        {
            foreach (Section section in _sections)
            {
                if (section.Name != "THUMBNAILIMAGE")
                {
                    continue;
                }

                var bytes = new List<byte>();

                foreach (Rec record in Current(section.First, section.Last))
                {
                    EachGroup(record, (code, start, end) =>
                    {
                        if (code == 310)
                        {
                            bytes.AddRange(DxfBinary.Bytes(Text(record, start, end)));
                        }
                    });
                }

                return bytes.ToArray();
            }

            return null;
        }

        #endregion
    }
}
