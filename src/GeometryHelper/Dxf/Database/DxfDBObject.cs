using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An object of a DXF file, as AutoCAD's <c>DBObject</c> is one of a drawing: an entity, a table, a record of a table.
    /// It has its handle, its owner's handle, its extended data and the handle of its extension dictionary, read from its
    /// record as the file writes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Objects are used directly, as AutoCAD's are inside a transaction, with no <c>ObjectId</c> and no transaction: an object
    /// read from a <see cref="DxfDatabase"/> is the same object each time it is reached, and a change to it is written into
    /// its record at once, every other record of the file keeping its bytes.
    /// </para>
    /// <para>
    /// An object made in code, such as <c>new DxfLine(start, end)</c>, is in no file until it is added to one: it has no
    /// handle and no owner then, and is given both when it is added.
    /// </para>
    /// </remarks>
    public abstract partial class DxfDBObject
    {
        private DxfItem _item;
        private List<DxfTypedValue> _pendingXData;

        /// <summary>An object made in code, given its record when it is added to a file.</summary>
        private protected DxfDBObject()
        {
        }

        /// <summary>An object of a record, the one that stands for it from now on.</summary>
        private protected DxfDBObject(DxfItem item)
        {
            Bind(item);
        }

        /// <summary>The record the object stands for: in its file, or in the file of its own an entity made in code is held in; null for a table record made in code.</summary>
        internal DxfItem Item => _item;

        /// <summary>Whether the object is of a file that has no record of it, as model space of a file of R12 with no block $MODEL_SPACE: its extended data has nowhere to go.</summary>
        internal virtual bool HasNoRecord => false;

        /// <summary>Whether the object is in a file to be saved, not one made in code and not yet added.</summary>
        internal bool IsResident => _item != null && !_item.File.IsDetached;

        /// <summary>Makes the object stand for a record: one it was read from, or the one written when it was added to a file.</summary>
        internal void Bind(DxfItem item)
        {
            _item = item;
            item.KeepWrapper(this);

            if (_pendingXData != null)
            {
                List<DxfTypedValue> data = _pendingXData;
                _pendingXData = null;
                XData = data;
            }
        }

        /// <summary>
        /// Gets the handle of the object (group 5, 105 for a DIMSTYLE) as the file writes it in hexadecimal, the spaces around
        /// it taken off, such as <c>2F</c>; null for an object in no file, and in a file that writes none, as many of R12 do.
        /// </summary>
        public string Handle => IsResident ? _item.Handle : null;

        /// <summary>
        /// Gets the handle of the object's owner (its first group 330, outside the groups of the applications): the
        /// BLOCK_RECORD of model space for an entity of model space, the table for a record of a table. Null for an object in
        /// no file, and in a file of R12, which writes no owners.
        /// </summary>
        public string OwnerHandle => IsResident ? _item.File.OwnerOf(_item) : null;

        /// <summary>
        /// Gets whether the object has been erased from its file: it is read still, as it was, but is no longer saved and is
        /// not changed.
        /// </summary>
        public bool IsErased => _item != null && _item.IsRemoved;

        /// <summary>
        /// Gets the object's extension dictionary, as AutoCAD's <c>ExtensionDictionary</c> gives its id: the DICTIONARY of the
        /// OBJECTS section its group <c>{ACAD_XDICTIONARY</c> names (360), which holds what applications keep with it; null
        /// where it has none, or names one the file has not.
        /// </summary>
        public DxfDBDictionary ExtensionDictionary
        {
            get
            {
                string handle = _item == null ? null : _item.File.ExtensionDictionaryOf(_item);
                DxfItem dictionary = handle == null ? null : _item.File.ObjectItem(handle);
                return dictionary == null ? null : OfObject(dictionary) as DxfDBDictionary;
            }
        }

        /// <summary>
        /// Gives the object an extension dictionary, as AutoCAD's <c>CreateExtensionDictionary</c>: an empty DICTIONARY at the end
        /// of the OBJECTS section, owned hard by the object (280 of 1) and naming it as its reactor and owner, its handle written
        /// into the object's record as the group <c>{ACAD_XDICTIONARY</c> after its handle and reactors, as AutoCAD writes it.
        /// Nothing where it has one.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the object is in no file, is erased, or its file is one of R12,
        /// which has no OBJECTS section, or writes no handles.</exception>
        public void CreateExtensionDictionary()
        {
            if (ExtensionDictionary != null)
            {
                return;
            }

            DxfFile file = InFile("be given an extension dictionary");
            DxfItem made = file.AddObject("DICTIONARY", Handle, b =>
            {
                b.Group(100, "AcDbDictionary");
                b.Group(280, "1");
                b.Group(281, "1");
            });

            file.SetExtensionDictionary(_item, made.Handle);
            OfObject(made);
        }

        /// <summary>
        /// Takes out the object's extension dictionary where it is empty, as AutoCAD's <c>ReleaseExtensionDictionary</c>: the
        /// DICTIONARY erased and the group <c>{ACAD_XDICTIONARY</c> taken out of the object's record. Nothing where it has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the dictionary has entries, as AutoCAD answers
        /// eContainerNotEmpty, or the object is erased.</exception>
        public void ReleaseExtensionDictionary()
        {
            DxfDBDictionary dictionary = ExtensionDictionary;

            if (dictionary == null)
            {
                return;
            }

            if (dictionary.Count > 0)
            {
                throw new InvalidOperationException("The extension dictionary of the object has " + dictionary.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " entries, and is released only when empty, as AutoCAD answers eContainerNotEmpty.");
            }

            DxfFile file = InFile("release its extension dictionary");
            file.SetExtensionDictionary(_item, null);
            file.RemoveRecord(dictionary.Item);
        }

        /// <summary>
        /// Gets the bytes the object keeps under a key, as AutoCAD's <c>GetBinaryDataForKey</c>: those of the XRECORD of that name
        /// in its extension dictionary, its groups 310 one after another.
        /// </summary>
        /// <param name="key">The name, letters of either case the same.</param>
        /// <returns>The bytes; null where the object has no extension dictionary, no entry of the name, or one that is not an XRECORD.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
        public byte[] GetBinaryDataForKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            DxfDBDictionary dictionary = ExtensionDictionary;

            if (dictionary == null || !dictionary.Contains(key) || !(dictionary.GetAt(key) is DxfDBObject data) || data.Item.Type != "XRECORD")
            {
                return null;
            }

            var bytes = new List<byte>();

            foreach (DxfTypedValue group in data.Item.File.ObjectGroupsOf(data.Item))
            {
                if (group.TypeCode == 310)
                {
                    bytes.AddRange(DxfBinary.Bytes(group.Value));
                }
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// Keeps bytes with the object under a key, as AutoCAD's <c>SetBinaryDataForKey</c>: an XRECORD of that name in its
        /// extension dictionary, made where it has none, its bytes written as groups 310 of 127 bytes each, as AutoCAD writes
        /// binary data; the bytes of an XRECORD of the name replaced. Bytes the same as those kept change nothing.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <param name="chunk">The bytes.</param>
        /// <exception cref="ArgumentNullException">Thrown when the key or the bytes are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the key is empty or has a control character in it, or names an entry
        /// that is not an XRECORD.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is in no file, is erased, or its file has no
        /// OBJECTS section.</exception>
        public void SetBinaryDataForKey(string key, byte[] chunk)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            byte[] now = GetBinaryDataForKey(key);

            if (now != null && DxfBinary.Same(now, chunk))
            {
                return;
            }

            DxfFile file = InFile("keep binary data");
            var groups = new List<KeyValuePair<int, string>>();

            foreach (string line in DxfBinary.Lines(chunk))
            {
                groups.Add(new KeyValuePair<int, string>(310, line));
            }

            CreateExtensionDictionary();
            DxfDBDictionary dictionary = ExtensionDictionary;

            if (dictionary.Contains(key))
            {
                DxfDBObject data = dictionary.GetAt(key);

                if (data == null || data.Item.Type != "XRECORD")
                {
                    throw new ArgumentException("The entry " + key + " of the extension dictionary is not an XRECORD, which binary data is kept in.", nameof(key));
                }

                file.ReplaceOwn(data.Item, 0, new HashSet<int> { 310 }, groups, XrecordOrder, false);
                return;
            }

            dictionary.Add(key, "XRECORD", b =>
            {
                b.Group(100, "AcDbXrecord");
                b.Group(280, "1");

                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });
        }

        // The groups of AcDbXrecord in the order AutoCAD writes them: its cloning, then its data.
        private static readonly IReadOnlyList<int> XrecordOrder = Array.AsReadOnly(new[] { 100, 280, 310 });

        /// <summary>
        /// Moves, turns, scales or mirrors the object's extended data by a transformation, as AutoCAD's <c>XDataTransformBy</c>:
        /// a position of the world (1011, 1021, 1031) is moved, a displacement (1012) turned and scaled but not moved, a direction
        /// (1013) turned but kept its length, a distance (1041) and a scale factor (1042) scaled by the transformation's scale, the
        /// greatest stretch of its axes; a plain point (1010) and every other group kept, as AutoCAD keeps them. A value the
        /// transformation leaves as it was keeps its text, and an object whose data it leaves is not written.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public void XDataTransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            List<DxfTypedValue> data = TransformedData(transform);

            if (data != null)
            {
                XData = data;
            }
        }

        /// <summary>The extended data moved by a transformation, as <see cref="XDataTransformBy"/> moves it; null where it leaves it as it is.</summary>
        private protected List<DxfTypedValue> TransformedData(GeoTransform3 transform) => TransformedData(XData, transform);

        /// <summary>Extended data moved by a transformation, as <see cref="XDataTransformBy"/> moves it; null where it leaves it as it is.</summary>
        private protected static List<DxfTypedValue> TransformedData(IReadOnlyList<DxfTypedValue> xdata, GeoTransform3 transform)
        {
            var data = new List<DxfTypedValue>(xdata);
            double scale = 0.0;

            for (int c = 0; c < 3; c++)
            {
                scale = Math.Max(scale, Math.Sqrt(transform[0, c] * transform[0, c] + transform[1, c] * transform[1, c] + transform[2, c] * transform[2, c]));
            }

            bool changed = false;

            for (int i = 0; i < data.Count; i++)
            {
                int code = data[i].TypeCode;

                if (code == 1041 || code == 1042)
                {
                    if (Numbers.TryRead(data[i].Value, out double value) && Guard.IsFinite(value))
                    {
                        changed |= Moved(data, i, value * scale, value);
                    }

                    continue;
                }

                if (code < 1011 || code > 1013)
                {
                    continue;
                }

                int y = i + 1 < data.Count && data[i + 1].TypeCode == code + 10 ? i + 1 : -1;
                int z = y >= 0 && y + 1 < data.Count && data[y + 1].TypeCode == code + 20 ? y + 1 : -1;
                double[] was = { Read(data, i), Read(data, y), Read(data, z) };
                double[] now;

                if (code == 1011)
                {
                    GeoPoint3 moved = transform.Transform(new GeoPoint3(was[0], was[1], was[2]));
                    now = new[] { moved.X, moved.Y, moved.Z };
                }
                else
                {
                    var vector = new GeoVector3(was[0], was[1], was[2]);
                    GeoVector3 turned = transform.Transform(vector);

                    if (code == 1013)
                    {
                        double length = turned.Length;
                        turned = length > 0.0 ? turned.Multiply(vector.Length / length) : vector;
                    }

                    now = new[] { turned.X, turned.Y, turned.Z };
                }

                changed |= Moved(data, i, now[0], was[0]);

                if (y >= 0)
                {
                    changed |= Moved(data, y, now[1], was[1]);
                }

                if (z >= 0)
                {
                    changed |= Moved(data, z, now[2], was[2]);
                }

                i = Math.Max(i, Math.Max(y, z));
            }

            return changed ? data : null;
        }

        /// <summary>The number of a group of extended data; nought where there is none or it is not a finite number.</summary>
        private static double Read(List<DxfTypedValue> data, int at)
            => at >= 0 && Numbers.TryRead(data[at].Value, out double value) && Guard.IsFinite(value) ? value : 0.0;

        /// <summary>A group of extended data given a number, where it differs to the bit from the one it has.</summary>
        private static bool Moved(List<DxfTypedValue> data, int at, double now, double was)
        {
            if (BitConverter.DoubleToInt64Bits(now) == BitConverter.DoubleToInt64Bits(was))
            {
                return false;
            }

            data[at] = new DxfTypedValue(data[at].TypeCode, Numbers.Exact(now + 0.0));
            return true;
        }

        /// <summary>The file of the object, which it is in and not erased from, for what is to be done.</summary>
        private protected DxfFile InFile(string what)
        {
            if (_item == null || !IsResident || HasNoRecord)
            {
                throw new InvalidOperationException("The " + GetType().Name + " is in no file to " + what + ": add it to one first.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The " + GetType().Name + " " + (Handle ?? string.Empty) + " is erased, and is not changed.");
            }

            return _item.File;
        }

        /// <summary>
        /// The object of a record of the OBJECTS section, made once: a <see cref="DxfDBDictionary"/> for a DICTIONARY, a
        /// <see cref="DxfXrecord"/> for an XRECORD, a <see cref="DxfGroup"/> for a GROUP, a <see cref="DxfLayout"/> for a LAYOUT,
        /// a <see cref="DxfPlotSettings"/> for a PLOTSETTINGS, a <see cref="DxfMLeaderStyle"/> for an MLEADERSTYLE, a
        /// <see cref="DxfField"/> for a FIELD, a <see cref="DxfMlineStyle"/> for an MLINESTYLE, a <see cref="DxfSun"/> for a SUN,
        /// a <see cref="DxfGeoData"/> for a GEODATA; else one of no class of its own.
        /// </summary>
        internal static DxfDBObject OfObject(DxfItem item)
        {
            if (item.Wrapper is DxfDBObject kept)
            {
                return kept;
            }

            DxfDBObject made;

            switch (item.Type)
            {
                case "DICTIONARY":
                case "ACDBDICTIONARYWDFLT":
                    made = new DxfDBDictionary(item);
                    break;
                case "XRECORD":
                    made = new DxfXrecord(item);
                    break;
                case "GROUP":
                    made = new DxfGroup(item);
                    break;
                case "LAYOUT":
                    made = new DxfLayout(item);
                    break;
                case "PLOTSETTINGS":
                    made = new DxfPlotSettings(item);
                    break;
                case "MLEADERSTYLE":
                    made = new DxfMLeaderStyle(item);
                    break;
                case "FIELD":
                    made = new DxfField(item);
                    break;
                case "MLINESTYLE":
                    made = new DxfMlineStyle(item);
                    break;
                case "SUN":
                    made = new DxfSun(item);
                    break;
                case "GEODATA":
                    made = new DxfGeoData(item);
                    break;
                default:
                    made = new DxfUnknownObject(item);
                    break;
            }

            return (DxfDBObject)item.KeepWrapper(made);
        }

        /// <summary>
        /// Gets or sets the extended data of the object, as AutoCAD's <c>XData</c> does: each application's data opened by its
        /// name in a group 1001, then its groups 1000 to 1071, as the file writes them.
        /// </summary>
        /// <value>
        /// <para>
        /// Get, every application's data in the order of the record, empty where it has none, a new list on each call. The
        /// texts of an application's name (1001), a string (1000) and a layer's name (1003) have their escapes read as letters,
        /// as a layer's name has: a letter the file's code page has not, written as <c>\U+0110</c>, comes back as the letter it
        /// was set as.
        /// </para>
        /// <para>
        /// Set, as AutoCAD sets it: each application named in the groups given has its data replaced by what follows its 1001,
        /// and an application given with nothing after its 1001 has its data taken out; the data of the applications not named
        /// is kept. <c>new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "B-12"), new DxfTypedValue(1040, "2.5") }</c>
        /// gives the application GHPROBE a text and a real. An application the file's table of applications does not list is
        /// added to it, as AutoCAD registers one. Data the same as the object has changes nothing.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the groups set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the groups set do not begin with a 1001, have a code outside 1000 to
        /// 1071, or name an application with no name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased, or is model or paper space of a file
        /// that has no record of it, as one of R12 with no block of it.</exception>
        public IReadOnlyList<DxfTypedValue> XData
        {
            get
            {
                if (_item == null)
                {
                    return (_pendingXData ?? new List<DxfTypedValue>()).AsReadOnly();
                }

                return _item.File.XDataOf(_item);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                // The data it has changes nothing, whatever the file holds, an application of no name among it.
                if (SameData(value, XData))
                {
                    return;
                }

                List<KeyValuePair<string, List<DxfTypedValue>>> given = Applications(value, nameof(value));
                List<KeyValuePair<string, List<DxfTypedValue>>> merged = Applications(XData, nameof(value));

                foreach (KeyValuePair<string, List<DxfTypedValue>> application in given)
                {
                    int at = merged.FindIndex(a => string.Equals(a.Key, application.Key, StringComparison.OrdinalIgnoreCase));

                    if (application.Value.Count == 1)
                    {
                        if (at >= 0)
                        {
                            merged.RemoveAt(at);
                        }
                    }
                    else if (at >= 0)
                    {
                        merged[at] = application;
                    }
                    else
                    {
                        merged.Add(application);
                    }
                }

                var groups = new List<DxfTypedValue>();

                foreach (KeyValuePair<string, List<DxfTypedValue>> application in merged)
                {
                    groups.AddRange(application.Value);
                }

                if (_item == null)
                {
                    if (HasNoRecord)
                    {
                        throw new InvalidOperationException("The " + GetType().Name + " has no record in its file to hold extended data: a file of R12 with no block of model or paper space has none.");
                    }

                    _pendingXData = groups;
                    return;
                }

                _item.File.SetXData(_item, groups);
            }
        }

        /// <summary>
        /// Gets the extended data of one application, as AutoCAD's <c>GetXDataForApplication</c> does: its 1001 and the groups
        /// after it up to the next application's.
        /// </summary>
        /// <param name="applicationName">The name of the application, letters of either case the same.</param>
        /// <returns>The application's groups, its 1001 first; null where the object has no data of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public IReadOnlyList<DxfTypedValue> GetXDataForApplication(string applicationName)
        {
            if (applicationName == null)
            {
                throw new ArgumentNullException(nameof(applicationName));
            }

            foreach (KeyValuePair<string, List<DxfTypedValue>> application in Applications(XData, null))
            {
                if (string.Equals(application.Key, applicationName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return application.Value.AsReadOnly();
                }
            }

            return null;
        }

        /// <summary>
        /// Erases the object from its file, as AutoCAD's <c>Erase</c> does: an entity is taken out of its block with every
        /// record of it, as <see cref="DxfEntity.Erase"/> says; a layer, a linetype or a block nothing uses out of its table, as
        /// their own <c>Erase</c> says; an XRECORD (<see cref="DxfXrecord"/>), a GROUP (<see cref="DxfGroup"/>) and a GEODATA
        /// (<see cref="DxfGeoData"/>) with the entry of the dictionary that names it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the object is in no file, or is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown for an object not erased yet: a table, a text style, a dictionary, a
        /// layout.</exception>
        public virtual void Erase()
        {
            throw new NotSupportedException("A " + GetType().Name + " is not erased yet: entities, layers, linetypes, blocks, XRECORDs and GROUPs are.");
        }

        /// <summary>
        /// Takes away what the object holds of its own over its style, as AutoCAD's <c>SetFromStyle</c>, so that it is drawn as
        /// its style draws it: a dimension and a leader lose their overrides of the dimension style (the list DSTYLE of their
        /// extended data of ACAD), a <see cref="DxfDimension"/> of <c>Dimasz</c> 4.5 of its own over a style of 3.5 reading
        /// 3.5 again; a <see cref="DxfMLeader"/> takes the values of its multileader style, as its own
        /// <see cref="DxfMLeader.SetFromStyle"/> says; a <see cref="DxfTable"/> loses its own overrides and its cells', as its own
        /// <see cref="DxfTable.SetFromStyle"/> says. An object of no style changes nothing.
        /// </summary>
        /// <returns>Whether the object has a style it is set from, as AutoCAD answers: true for a table, and for a dimension, a
        /// leader and a multileader of a style its drawing has (for a copy in no file, the drawing it was copied from); false,
        /// and nothing changed, for one of a style its drawing has not, or in no drawing, and for every other object.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the object has overrides to take away and is erased.</exception>
        public virtual bool SetFromStyle() => false;

        /// <summary>
        /// Puts an object made in code in this one's place in the drawing, as AutoCAD's <c>HandOverTo</c> hands an object's id
        /// over: the new object's records written where this one's stand, in its block and at its place among the block's
        /// entities, with this one's handle, owner and reactors, so that what pointed at this one points at the new one; this
        /// one is in no file from then on, a copy of what it was with no handle and no owner, to be appended again or let go.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A LINE handed over to a <c>new DxfCircle(centre, normal, 5)</c> on the layer holes is that circle under the LINE's
        /// handle: a GROUP that held the line holds the circle, and the circle is on holes in its own colour, as made. The new
        /// object is written as <see cref="DxfBlockTableRecord.AppendEntity"/> writes one, its layer added to the table where
        /// the table has none of it and what it brings from another drawing copied with it; a record that follows it, as a
        /// VERTEX, is given a handle of its own and owned by the handle it takes. The records that followed this one, as the
        /// vertices of a POLYLINE, go, their handles not given again.
        /// </para>
        /// <para>
        /// Entities alone hand their place over: a record of a table, a dictionary and the other objects of the OBJECTS section
        /// are refused, as is a VERTEX or an ATTRIB, which belongs to the entity it follows.
        /// </para>
        /// </remarks>
        /// <param name="newPointer">The object to take the place: an entity made in code, or copied, and in no file.</param>
        /// <param name="keepXData">Whether the new object takes this one's extended data, which then replaces its own; false
        /// keeps the new object's own.</param>
        /// <param name="keepExtensionDictionary">Whether the new object takes this one's extension dictionary; false takes the
        /// dictionary out of the drawing, with what it holds.</param>
        /// <exception cref="ArgumentNullException">Thrown when the new object is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the new object is this one, is in a file already or erased, or is one
        /// this drawing's version cannot hold, as <see cref="DxfBlockTableRecord.AppendEntity"/> refuses it; nothing is
        /// written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this object is in no file, or is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when either object is not an entity, or this one follows another.</exception>
        public void HandOverTo(DxfDBObject newPointer, bool keepXData, bool keepExtensionDictionary)
        {
            if (newPointer == null)
            {
                throw new ArgumentNullException(nameof(newPointer));
            }

            if (ReferenceEquals(newPointer, this))
            {
                throw new ArgumentException("An object hands its place over to another, not to itself.", nameof(newPointer));
            }

            DxfFile file = InFile("hand its place over");

            if (!(this is DxfEntity entity) || !(newPointer is DxfEntity made))
            {
                throw new NotSupportedException("An entity hands its place over to another entity; a " + GetType().Name + " and a " + newPointer.GetType().Name + " are not handed over yet.");
            }

            if (_item.Parent != null)
            {
                throw new NotSupportedException("A " + _item.Type + " belongs to the entity it follows, and does not hand its place over.");
            }

            if (newPointer.IsResident || newPointer.IsErased)
            {
                throw new ArgumentException("The object to take the place is to be in no file: the " + newPointer + " is in one, or erased.", nameof(newPointer));
            }

            made.Appending(entity.Owner as DxfBlockTableRecord);
            DxfItem item = _item;
            DxfItem snapshot = file.CopyOf(item);
            file.HandOver(item, made.Item, keepXData, keepExtensionDictionary);
            item.ReplaceWrapper(made);
            made.Bind(item);
            Bind(snapshot);
            GeometryHelperLog.Info("DxfDatabase: the " + item.Type + " " + (item.Handle ?? string.Empty) + " took the place of a " + snapshot.Type + ".");
        }

        /// <summary>Whether two runs of extended data are the same, group for group, code and text.</summary>
        private static bool SameData(IReadOnlyList<DxfTypedValue> a, IReadOnlyList<DxfTypedValue> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].TypeCode != b[i].TypeCode || !string.Equals(a[i].Value, b[i].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The data of extended data, application by application, each with its 1001 first; checked where a parameter is named.</summary>
        private static List<KeyValuePair<string, List<DxfTypedValue>>> Applications(IReadOnlyList<DxfTypedValue> groups, string parameter)
        {
            var applications = new List<KeyValuePair<string, List<DxfTypedValue>>>();
            List<DxfTypedValue> current = null;

            foreach (DxfTypedValue group in groups)
            {
                if (group.Value == null || group.TypeCode < 1000 || group.TypeCode > 1071)
                {
                    if (parameter == null)
                    {
                        continue;
                    }

                    throw new ArgumentException("Extended data has groups from 1000 to 1071 alone: " + group + " is not one.", parameter);
                }

                if (group.TypeCode == 1001)
                {
                    string name = group.Value.Trim();

                    if (name.Length == 0 && parameter != null)
                    {
                        throw new ArgumentException("The group 1001 that opens an application's extended data names it, and this one names none.", parameter);
                    }

                    current = new List<DxfTypedValue> { group };
                    applications.Add(new KeyValuePair<string, List<DxfTypedValue>>(name, current));
                    continue;
                }

                if (current == null)
                {
                    if (parameter == null)
                    {
                        continue;
                    }

                    throw new ArgumentException("Extended data begins with a group 1001 that names its application: " + group + " stands before any.", parameter);
                }

                current.Add(group);
            }

            return applications;
        }
    }
}
