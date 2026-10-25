using System;
using System.Collections.Generic;

namespace GeometryHelper.Export
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
