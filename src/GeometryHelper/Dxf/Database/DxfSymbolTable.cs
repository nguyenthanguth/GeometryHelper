using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A table of a DXF file, as AutoCAD's <c>SymbolTable</c>: the layers, the linetypes, the blocks, each record found by its
    /// name, letters of either case the same, as AutoCAD finds one.
    /// </summary>
    /// <remarks>
    /// Each table is a class of its own, whose indexer and enumerator give its own records: <see cref="DxfLayerTable"/>,
    /// <see cref="DxfLinetypeTable"/>, <see cref="DxfTextStyleTable"/> and <see cref="DxfBlockTable"/>. A record is added by <see cref="Add"/>, as AutoCAD
    /// adds one: <c>db.LayerTable.Add(new DxfLayerTableRecord { Name = "steel", ColorIndex = 1 })</c>.
    /// </remarks>
    public abstract class DxfSymbolTable : DxfDBObject, IEnumerable
    {
        private protected DxfSymbolTable(DxfItem item)
        {
            if (item != null)
            {
                Bind(item);
            }
        }

        /// <summary>
        /// Gets whether the table has a record of a name, as AutoCAD's <c>Has</c>, letters of either case the same.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>Whether there is one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public bool Has(string key) => Find(key) != null;

        /// <summary>
        /// Gets the record of a name, as AutoCAD's indexer, letters of either case the same: the first of the table where two
        /// differ only in case.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>The record.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no record of the name.</exception>
        public DxfSymbolTableRecord this[string key] => Find(key) ?? throw new KeyNotFoundException("The table has no record named " + key + ".");

        /// <summary>
        /// Adds a record made in code to the table, as AutoCAD's <c>Add</c>: its record written before the table's ENDTAB in
        /// the file's own style, with a handle of its own, the table as its owner and the table's count raised by one. The same
        /// object is the record of the table from then on.
        /// </summary>
        /// <param name="value">The record, of the table's own kind, with its <c>Name</c> given.</param>
        /// <exception cref="ArgumentNullException">Thrown when the record is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the record is of another kind of table, has no name or one AutoCAD
        /// does not take, or its name is the table's already.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the record is in a file already, or the file has no such table.</exception>
        public void Add(DxfSymbolTableRecord value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Item != null)
            {
                throw new InvalidOperationException("The record " + value.Name + " is in a table already.");
            }

            string name = value.Name;

            if (name == null)
            {
                throw new ArgumentException("A record is added with its name given.", nameof(value));
            }

            CheckName(name, nameof(value));

            if (Has(name))
            {
                throw new ArgumentException("The table has a record named " + name + " already, letters of either case the same.", nameof(value));
            }

            AddRecord(value);
        }

        /// <summary>Gets an enumerator over the records, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator GetEnumerator() => Records().GetEnumerator();

        /// <summary>The records, in the order of the file.</summary>
        private protected abstract IEnumerable<DxfSymbolTableRecord> Records();

        /// <summary>Adds a record of the table's own kind, its name checked.</summary>
        private protected abstract void AddRecord(DxfSymbolTableRecord record);

        /// <summary>The first record of a name, letters of either case the same; null where there is none.</summary>
        private protected virtual DxfSymbolTableRecord Find(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string name = key.Trim();

            foreach (DxfSymbolTableRecord record in Records())
            {
                if (string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        /// <summary>A name AutoCAD takes: not empty, no space at either end, no control character and none of &lt;&gt;/":;?*|=`.</summary>
        internal static void CheckName(string name, string parameter)
        {
            if (name.Length == 0 || name.Trim().Length != name.Length)
            {
                throw new ArgumentException("A name has to be given, with no space at either end: '" + name + "' is not one.", parameter);
            }

            foreach (char c in name)
            {
                if (c < ' ' || "<>/\\\":;?*|=`".IndexOf(c) >= 0)
                {
                    throw new ArgumentException("A name cannot have a control character or any of < > / \\ \" : ; ? * | = ` in it, as AutoCAD does not take them: '" + name + "' has one.", parameter);
                }
            }
        }
    }

    /// <summary>
    /// A record of a table of a DXF file, as AutoCAD's <c>SymbolTableRecord</c>: a layer, a linetype, a block; with its name.
    /// </summary>
    public abstract class DxfSymbolTableRecord : DxfDBObject
    {
        private string _name;

        /// <summary>A record made in code, to be added to its table.</summary>
        private protected DxfSymbolTableRecord()
        {
        }

        /// <summary>The object of a record of a file.</summary>
        private protected DxfSymbolTableRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the name of the record (group 2), as AutoCAD's <c>Name</c>: as the file spells it, the spaces around it
        /// taken off and its escapes (<c>\U+00E9</c>) read as letters.
        /// </summary>
        /// <value>
        /// Set on a record made in code, before it is added to its table; on a layer or a block of a file, it is renamed with
        /// every reference to it, as <see cref="DxfLayerTableRecord"/> and <see cref="DxfBlockTableRecord"/> say. The name it
        /// has changes nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set on a record of a file is one AutoCAD does not take, or
        /// that of another record of its table.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a record of a file of a kind not renamed yet, a
        /// linetype or a text style, or on one AutoCAD does not rename, as layer 0.</exception>
        public virtual string Name
        {
            get => Item == null ? _name : DxfReader.NameOf(Item.File.ValueOf(Item, 2) ?? string.Empty);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (Item != null)
                {
                    if (string.Equals(value, Name, StringComparison.Ordinal))
                    {
                        return;
                    }

                    Rename(value);
                    return;
                }

                _name = value;
            }
        }

        /// <summary>Renames a record of a file with every reference to it, the name checked; refused by a kind of record not renamed yet.</summary>
        /// <exception cref="InvalidOperationException">Thrown by a kind of record not renamed yet.</exception>
        private protected virtual void Rename(string name)
            => throw new InvalidOperationException("The record " + Name + " is of a file, and is not renamed yet: a later release renames one with every reference to it.");

        /// <summary>
        /// Gets whether the record comes from an external reference (flag 16 of group 70, or a name with a | in it), as
        /// AutoCAD's <c>IsDependent</c>.
        /// </summary>
        public bool IsDependent => (Flags & 16) != 0 || (Name ?? string.Empty).IndexOf('|') >= 0;

        /// <summary>Gets whether the external reference the record comes from was resolved (flag 32 of group 70), as AutoCAD's <c>IsResolved</c>.</summary>
        public bool IsResolved => (Flags & 32) != 0;

        /// <summary>The flags of the record (group 70); nought for one made in code.</summary>
        private protected virtual int Flags => Item == null ? 0 : FlagsOf(Item);

        /// <summary>The whole number of the first group 70 of the own part of a record; nought where it has none.</summary>
        private protected static int FlagsOf(DxfItem item)
            => int.TryParse(item.File.ValueOf(item, 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) ? flags : 0;

        /// <summary>
        /// Gives the record as a line for debugging: its kind and its name.
        /// </summary>
        /// <returns>The record as a line, such as <c>DxfLayerTableRecord steel</c>.</returns>
        public override string ToString() => GetType().Name + " " + Name;
    }
}
