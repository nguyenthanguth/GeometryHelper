using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An entry of a <see cref="DxfDBDictionary"/>, as AutoCAD's <c>DBDictionaryEntry</c>: its name and the object it names.
    /// </summary>
    public readonly struct DxfDBDictionaryEntry
    {
        /// <summary>
        /// Initializes an entry, as AutoCAD's <c>DBDictionaryEntry(key, value)</c>.
        /// </summary>
        /// <param name="key">The name of the entry.</param>
        /// <param name="value">The object it names.</param>
        public DxfDBDictionaryEntry(string key, DxfDBObject value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>Gets the name of the entry (group 3), as AutoCAD's <c>Key</c>, the spaces around it taken off.</summary>
        public string Key { get; }

        /// <summary>
        /// Gets the object the entry names (group 350 or 360), as AutoCAD's <c>Value</c> gives its id: a <see cref="DxfDBDictionary"/>
        /// for a DICTIONARY, a <see cref="DxfDBObject"/> of its handle, owner and extended data for a type the library has no class
        /// for yet; null where the file has no object of the handle.
        /// </summary>
        public DxfDBObject Value { get; }

        /// <summary>Gives the entry as a line for debugging: its name and its object.</summary>
        /// <returns>The line, such as <c>GH_DATA DICTIONARY 2F</c>.</returns>
        public override string ToString() => Key + " " + (Value == null ? "(none)" : Value.ToString());
    }

    /// <summary>
    /// A dictionary of a DXF file, as AutoCAD's <c>DBDictionary</c>: a DICTIONARY of the OBJECTS section, its entries each a name
    /// and the object it names, found by name, letters of either case the same, as AutoCAD finds one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The extension dictionary an entity is given by <see cref="DxfDBObject.CreateExtensionDictionary"/> is one, reached by
    /// <see cref="DxfDBObject.ExtensionDictionary"/>: <c>line.ExtensionDictionary.SetAt("GH_DATA", new DxfDBDictionary())</c>
    /// adds a dictionary named GH_DATA to it, owned by it, as AutoCAD writes one: its handle above every handle of the file, its
    /// owner as its reactor and its owner (330), the marker AcDbDictionary and its cloning (281 of 1); the entry is written
    /// last among those of the owner, its name as a 3 and the new dictionary's handle as a 350, a 360 where the owner owns its
    /// entries hard (280 of 1), as an extension dictionary does.
    /// </para>
    /// <para>
    /// A dictionary holds objects of the library's classes as they come: a <see cref="DxfDBDictionary"/>, a
    /// <see cref="DxfXrecord"/>, a <see cref="DxfGroup"/>, a <see cref="DxfLayout"/>, a <see cref="DxfPlotSettings"/> or a
    /// <see cref="DxfField"/>, which <see cref="DxfDBObject.SetField(DxfField)"/> adds; the
    /// other objects of the OBJECTS section, as a MATERIAL, are read as they are. The drawing's own dictionaries are reached
    /// from <see cref="DxfDatabase.NamedObjectsDictionary"/>, <see cref="DxfDatabase.GroupDictionary"/>,
    /// <see cref="DxfDatabase.LayoutDictionary"/> and the others of the database.
    /// </para>
    /// <para>
    /// An entry is taken out of the record when its object is erased, so there are no erased entries to walk: AutoCAD's
    /// <c>IncludingErased</c> has nothing to add here.
    /// </para>
    /// </remarks>
    public sealed class DxfDBDictionary : DxfDBObject, IEnumerable<DxfDBDictionaryEntry>
    {
        // The groups of AcDbDictionary in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 280, 281, 3, 350, 360 });

        private static readonly int[] EntryCodes = { 3, 350, 360 };

        private bool _madeHard;

        /// <summary>
        /// Initializes a dictionary made in code, empty, to be added to another by <see cref="SetAt"/>, as AutoCAD's
        /// <c>DBDictionary()</c> is.
        /// </summary>
        public DxfDBDictionary()
        {
        }

        internal DxfDBDictionary(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets how many entries the dictionary has, as AutoCAD's <c>Count</c>; nought for one made in code.</summary>
        public int Count => Entries().Count;

        /// <summary>
        /// Gets or sets the object of an entry, as AutoCAD's indexer: <see cref="GetAt"/> and <see cref="SetAt"/>.
        /// </summary>
        /// <param name="key">The name of the entry, letters of either case the same.</param>
        /// <returns>The object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the object set is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the dictionary has no entry of the name.</exception>
        public DxfDBObject this[string key]
        {
            get => GetAt(key);
            set => SetAt(key, value);
        }

        /// <summary>Gets whether the dictionary has an entry of a name, as AutoCAD's <c>Contains</c>, letters of either case the same.</summary>
        /// <param name="entryName">The name.</param>
        /// <returns>Whether it has one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public bool Contains(string entryName) => Find(entryName) >= 0;

        /// <summary>Gets whether an entry of the dictionary names an object, as AutoCAD's <c>Contains</c> of an id: by its handle.</summary>
        /// <param name="value">The object.</param>
        /// <returns>Whether one does.</returns>
        public bool Contains(DxfDBObject value) => value?.Handle != null && Entries().Exists(e => string.Equals(e.Handle, value.Handle, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets the object an entry names, as AutoCAD's <c>GetAt</c> gives its id.
        /// </summary>
        /// <param name="entryName">The name of the entry, letters of either case the same.</param>
        /// <returns>The object; null where the file has no object of the entry's handle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the dictionary has no entry of the name, as AutoCAD answers eKeyNotFound.</exception>
        public DxfDBObject GetAt(string entryName)
        {
            int at = Find(entryName);

            if (at < 0)
            {
                throw new KeyNotFoundException("The dictionary has no entry named " + entryName + ".");
            }

            return ObjectOf(Entries()[at].Handle);
        }

        /// <summary>
        /// Gets the name of the entry that names an object, as AutoCAD's <c>NameAt</c>.
        /// </summary>
        /// <param name="value">The object.</param>
        /// <returns>The name of its entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the object is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no entry names it.</exception>
        public string NameAt(DxfDBObject value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            foreach (Entry entry in Entries())
            {
                if (value.Handle != null && string.Equals(entry.Handle, value.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    return DxfReader.NameOf(entry.Key);
                }
            }

            throw new KeyNotFoundException("No entry of the dictionary names the object " + value + ".");
        }

        /// <summary>
        /// Adds an object under a name, as AutoCAD's <c>SetAt</c>: an object made in code written into the file as the
        /// dictionary's own, last among its entries, as AutoCAD writes it; where the name has an entry already, the object it
        /// named is erased, with what it owns, and the entry names the new one, as AutoCAD replaces it.
        /// </summary>
        /// <remarks>
        /// A <see cref="DxfDBDictionary"/>, a <see cref="DxfXrecord"/> with its data and a <see cref="DxfGroup"/> with its
        /// description are added: <c>db.GroupDictionary.SetAt("BEAMS", new DxfGroup("Beams of grid A", true))</c> adds the group
        /// BEAMS, empty until entities are appended to it. A group added under the name <c>*</c> is anonymous, as AutoCAD names
        /// one: it is given the first name <c>*A1</c>, <c>*A2</c> and on the dictionary has not.
        /// </remarks>
        /// <param name="searchKey">The name, letters of either case the same as an entry's.</param>
        /// <param name="newValue">The object, made in code and in no file yet.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the object is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a control character in it, or the object is
        /// in a file already or of a class the library does not add: a <see cref="DxfLayout"/> or one of no class of its own.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this dictionary is in no file, or is erased.</exception>
        public void SetAt(string searchKey, DxfDBObject newValue)
        {
            CheckKey(searchKey, nameof(searchKey));

            if (newValue == null)
            {
                throw new ArgumentNullException(nameof(newValue));
            }

            if (newValue.Item != null)
            {
                throw new ArgumentException("A dictionary takes an object made in code and in no file yet: the " + newValue + " is in one.", nameof(newValue));
            }

            switch (newValue)
            {
                case DxfDBDictionary dictionary:
                    bool hard = dictionary._madeHard;
                    newValue.Bind(Add(searchKey, "DICTIONARY", b =>
                    {
                        b.Group(100, "AcDbDictionary");

                        if (hard)
                        {
                            b.Group(280, "1");
                        }

                        b.Group(281, "1");
                    }));
                    break;
                case DxfXrecord xrecord:
                    bool flagged = Resident().Supports(1015);
                    newValue.Bind(Add(searchKey, "XRECORD", b =>
                    {
                        b.Group(100, "AcDbXrecord");

                        if (flagged)
                        {
                            b.Group(280, "1");
                        }

                        foreach (DxfTypedValue group in xrecord.MadeData)
                        {
                            b.Group(group.TypeCode, group.Value);
                        }
                    }));
                    break;
                case DxfGroup group:
                    bool anonymous = searchKey.Trim() == "*" || group.MadeAnonymous;
                    string key = anonymous ? AnonymousName() : searchKey;
                    newValue.Bind(Add(key, "GROUP", b =>
                    {
                        b.Group(100, "AcDbGroup");
                        b.Group(300, group.MadeDescription);
                        b.Group(70, anonymous ? "1" : "0");
                        b.Group(71, group.MadeSelectable ? "1" : "0");
                    }));
                    newValue.Item.File.GroupMayBeEmpty(newValue.Item);
                    break;
                default:
                    throw new ArgumentException("A dictionary takes a DxfDBDictionary, a DxfXrecord or a DxfGroup made in code: a " + newValue.GetType().Name + " is not added.", nameof(newValue));
            }
        }

        /// <summary>
        /// Gets or sets whether the dictionary owns its entries hard (group 280 of 1), as AutoCAD's <c>TreatElementsAsHard</c>:
        /// an extension dictionary does; a hard owner names its entries by groups 360, a soft one by 350. False where the record
        /// gives none.
        /// </summary>
        /// <value>Set, the flag written and every entry named anew by the code it gives; the flag the dictionary has changes nothing.</value>
        /// <exception cref="InvalidOperationException">Thrown when the dictionary is erased.</exception>
        public bool TreatElementsAsHard
        {
            get
            {
                if (Item == null)
                {
                    return _madeHard;
                }

                return Item.File.ValueOf(Item, 280)?.Trim() == "1";
            }

            set
            {
                if (value == TreatElementsAsHard)
                {
                    return;
                }

                if (Item == null)
                {
                    _madeHard = value;
                    return;
                }

                DxfFile file = Resident();
                List<Entry> entries = Entries();
                file.SetValues(Item, new[] { new KeyValuePair<int, string>(280, value ? "1" : "0") }, Order, false);

                for (int i = 0; i < entries.Count; i++)
                {
                    entries[i] = new Entry(entries[i].Key, entries[i].Handle, 0);
                }

                Write(entries);
            }
        }

        /// <summary>
        /// Copies the entries into an array from an index on, as AutoCAD's <c>CopyTo</c>, in the order of the record.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="index">Where in the array the first entry goes.</param>
        /// <exception cref="ArgumentNullException">Thrown when the array is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the array has no room for every entry from the index on.</exception>
        public void CopyTo(DxfDBDictionaryEntry[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The index is nought or more.");
            }

            var entries = new List<DxfDBDictionaryEntry>(this);

            if (array.Length - index < entries.Count)
            {
                throw new ArgumentException("The array has room for " + Math.Max(0, array.Length - index).ToString(System.Globalization.CultureInfo.InvariantCulture) + " entries from the index on, and the dictionary has " + entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".", nameof(array));
            }

            entries.CopyTo(array, index);
        }

        /// <summary>The first name <c>*A1</c>, <c>*A2</c> and on the dictionary has no entry of, as AutoCAD names an anonymous group.</summary>
        internal string AnonymousName()
        {
            for (int n = 1; ; n++)
            {
                string key = "*A" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (Find(key) < 0)
                {
                    return key;
                }
            }
        }

        /// <summary>
        /// Erases an object of the OBJECTS section, as AutoCAD erases one: the entry of the dictionary that owns it taken out with
        /// it, as <see cref="Remove(string)"/> takes one out; an object no dictionary names taken out alone.
        /// </summary>
        internal static void EraseFromOwner(DxfDBObject value, DxfFile file)
        {
            DxfItem owner = file.ObjectItem(value.OwnerHandle);

            if (owner != null && OfObject(owner) is DxfDBDictionary dictionary && dictionary.Contains(value))
            {
                dictionary.Remove(dictionary.NameAt(value));
                return;
            }

            file.RemoveRecord(value.Item);
        }

        /// <summary>Renames the entry that names an object, where the dictionary has one; for the name of a group or a layout set on the object.</summary>
        internal void Rename(DxfDBObject value, string newName) => SetName(NameAt(value), newName);

        /// <summary>The name of the entry that names an object, as <see cref="NameAt"/>; null where no entry names it.</summary>
        internal string NameOf(DxfDBObject value)
        {
            foreach (Entry entry in Entries())
            {
                if (value.Handle != null && string.Equals(entry.Handle, value.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    return DxfReader.NameOf(entry.Key);
                }
            }

            return null;
        }

        /// <summary>
        /// Adds an object of a type, owned by the dictionary, under a name: its record written as <see cref="DxfFile.AddObject"/>
        /// writes one, then the entry, last, or in the place of the entry of the name, whose object is erased.
        /// </summary>
        internal DxfItem Add(string searchKey, string type, Action<DxfFile.Builder> groups)
        {
            DxfFile file = Resident();
            List<Entry> entries = Entries();
            int at = Find(searchKey);
            DxfItem made = file.AddObject(type, Handle, groups);

            if (at >= 0)
            {
                DxfItem old = file.ObjectItem(entries[at].Handle);

                if (old != null)
                {
                    EraseOwned(file, old);
                }

                entries[at] = new Entry(entries[at].Key, made.Handle, entries[at].Code);
            }
            else
            {
                entries.Add(new Entry(searchKey.Trim(), made.Handle, 0));
            }

            Write(entries);
            return made;
        }

        /// <summary>
        /// Takes out the entry of a name, as AutoCAD's <c>Remove</c>, and erases the object it names with what that owns, as an
        /// object of a DXF file is owned by something.
        /// </summary>
        /// <param name="key">The name, letters of either case the same.</param>
        /// <returns>The object, erased; null where the file had no object of the entry's handle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the dictionary has no entry of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dictionary is erased.</exception>
        public DxfDBObject Remove(string key)
        {
            int at = Find(key);

            if (at < 0)
            {
                throw new KeyNotFoundException("The dictionary has no entry named " + key + ".");
            }

            DxfFile file = Resident();
            List<Entry> entries = Entries();
            DxfDBObject value = ObjectOf(entries[at].Handle);
            entries.RemoveAt(at);
            Write(entries);

            if (value?.Item != null && !value.IsErased)
            {
                EraseOwned(file, value.Item);
            }

            return value;
        }

        /// <summary>Takes out the entry that names an object, as AutoCAD's <c>Remove</c> of an id, and erases the object.</summary>
        /// <param name="value">The object.</param>
        /// <exception cref="ArgumentNullException">Thrown when the object is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no entry names it.</exception>
        public void Remove(DxfDBObject value) => Remove(NameAt(value));

        /// <summary>
        /// Renames an entry, as AutoCAD's <c>SetName</c>; nothing where the name is the one it has.
        /// </summary>
        /// <param name="oldName">The entry's name, letters of either case the same.</param>
        /// <param name="newName">Its new name.</param>
        /// <exception cref="ArgumentNullException">Thrown when a name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the new name is empty or has a control character in it, or names
        /// another entry already.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the dictionary has no entry of the old name.</exception>
        public void SetName(string oldName, string newName)
        {
            CheckKey(newName, nameof(newName));
            int at = Find(oldName);

            if (at < 0)
            {
                throw new KeyNotFoundException("The dictionary has no entry named " + oldName + ".");
            }

            List<Entry> entries = Entries();

            if (string.Equals(DxfReader.NameOf(entries[at].Key), newName, StringComparison.Ordinal))
            {
                return;
            }

            int other = Find(newName);

            if (other >= 0 && other != at)
            {
                throw new ArgumentException("The dictionary has an entry named " + newName + " already, letters of either case the same.", nameof(newName));
            }

            Resident();
            entries[at] = new Entry(newName, entries[at].Handle, entries[at].Code);
            Write(entries);
        }

        /// <summary>Gets an enumerator over the entries, in the order of the record.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfDBDictionaryEntry> GetEnumerator()
        {
            foreach (Entry entry in Entries())
            {
                yield return new DxfDBDictionaryEntry(DxfReader.NameOf(entry.Key), ObjectOf(entry.Handle));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Gives the dictionary as a line for debugging: its type, its handle and how many entries it has.</summary>
        /// <returns>The line, such as <c>DICTIONARY 2F, 3 entries</c>.</returns>
        public override string ToString() => "DICTIONARY " + (Handle ?? string.Empty) + ", " + Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " entries";

        /// <summary>The entries of the record, each its name as written, the handle it names and the code it names it by, in their order.</summary>
        private List<Entry> Entries()
        {
            var entries = new List<Entry>();

            if (Item == null)
            {
                return entries;
            }

            string name = null;

            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == 3)
                {
                    name = group.Value;
                }
                else if ((group.TypeCode == 350 || group.TypeCode == 360) && name != null)
                {
                    entries.Add(new Entry(name, group.Value.Trim(), group.TypeCode));
                    name = null;
                }
            }

            return entries;
        }

        /// <summary>The index of the entry of a name, letters of either case the same; -1 where there is none.</summary>
        private int Find(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return Entries().FindIndex(e => string.Equals(DxfReader.NameOf(e.Key), key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Writes the entries as the record's groups 3 and 350 or 360: each entry read by the code it was read with, a new one by
        /// 360 where the dictionary owns its entries hard (280 of 1), else 350.
        /// </summary>
        private void Write(List<Entry> entries)
        {
            bool hard = false;

            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                hard |= group.TypeCode == 280 && group.Value.Trim() == "1";
            }

            var groups = new List<KeyValuePair<int, string>>();

            foreach (Entry entry in entries)
            {
                groups.Add(new KeyValuePair<int, string>(3, entry.Key));
                groups.Add(new KeyValuePair<int, string>(entry.Code != 0 ? entry.Code : hard ? 360 : 350, entry.Handle));
            }

            Item.File.ReplaceOwn(Item, 0, EntryCodes, groups, Order, false);
        }

        /// <summary>
        /// Erases an object and, where it is a dictionary, every entry it owns (its 330 the dictionary's handle), at any depth,
        /// from the deepest up: a dictionary taken out takes its sub-dictionaries and records with it at once.
        /// </summary>
        private static void EraseOwned(DxfFile file, DxfItem item)
            => EraseOwned(file, item, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// <see cref="EraseOwned(DxfFile, DxfItem)"/> with the handles of the objects reached so far: each is taken once, so that
        /// a file whose owners run round in a ring, a dictionary owned by one of its own entries, ends rather than going round.
        /// </summary>
        private static void EraseOwned(DxfFile file, DxfItem item, HashSet<string> reached)
        {
            if (item.Handle != null && !reached.Add(item.Handle))
            {
                return;
            }

            if (item.Type == "DICTIONARY" || item.Type == "ACDBDICTIONARYWDFLT")
            {
                string handle = item.Handle;
                string name = null;

                foreach (DxfTypedValue group in file.ObjectGroupsOf(item))
                {
                    if (group.TypeCode == 3)
                    {
                        name = group.Value;
                    }
                    else if ((group.TypeCode == 350 || group.TypeCode == 360) && name != null)
                    {
                        DxfItem child = file.ObjectItem(group.Value.Trim());

                        if (child != null && string.Equals(file.OwnerOf(child), handle, StringComparison.OrdinalIgnoreCase))
                        {
                            EraseOwned(file, child, reached);
                        }

                        name = null;
                    }
                }
            }

            if (!item.IsRemoved)
            {
                file.RemoveRecord(item);
            }
        }

        /// <summary>An entry of the record: its name as written, the handle it names, and the code it names it by (350 or 360; nought for one new).</summary>
        private readonly struct Entry
        {
            internal Entry(string key, string handle, int code)
            {
                Key = key;
                Handle = handle;
                Code = code;
            }

            internal string Key { get; }

            internal string Handle { get; }

            internal int Code { get; }
        }

        /// <summary>The file of the dictionary, which it is in and not erased from.</summary>
        private DxfFile Resident()
        {
            if (Item == null)
            {
                throw new InvalidOperationException("The dictionary is in no file: add it to one first.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The dictionary " + Handle + " is erased, and is not changed.");
            }

            return Item.File;
        }

        /// <summary>The object of a handle of the file: a dictionary, or one of no class of its own; null where there is none.</summary>
        private DxfDBObject ObjectOf(string handle)
        {
            DxfItem item = Item?.File.ObjectItem(handle);
            return item == null ? null : OfObject(item);
        }

        private static void CheckKey(string key, string parameter)
        {
            if (key == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (key.Trim().Length == 0)
            {
                throw new ArgumentException("The name of an entry has to be given.", parameter);
            }

            foreach (char c in key)
            {
                if (c < ' ')
                {
                    throw new ArgumentException("The name of an entry has no control character in it: '" + key + "' has one.", parameter);
                }
            }
        }
    }
}
