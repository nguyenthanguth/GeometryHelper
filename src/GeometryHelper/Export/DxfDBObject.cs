using System;
using System.Collections.Generic;

namespace GeometryHelper.Export
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
    public abstract class DxfDBObject
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
        /// Gets the handle of the object's extension dictionary (the 360 in its group <c>{ACAD_XDICTIONARY</c>), the DICTIONARY
        /// of the OBJECTS section that holds what applications keep with it; null where it has none.
        /// </summary>
        public string ExtensionDictionary => _item == null ? null : _item.File.ExtensionDictionaryOf(_item);

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
        /// record of it, as <see cref="DxfEntity.Erase"/> says.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the object is in no file, or is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown for a table or a record of a table, whose removal comes later.</exception>
        public virtual void Erase()
        {
            throw new NotSupportedException("A " + GetType().Name + " is not erased yet: entities are.");
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
