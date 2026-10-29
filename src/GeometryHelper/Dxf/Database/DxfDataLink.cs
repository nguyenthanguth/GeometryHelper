using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A DATALINK of a DXF file, as AutoCAD's <c>DataLink</c>: the link of a table's cells to the data of another file, a
    /// spreadsheet as a rule, kept under its name in the dictionary of data links (<see cref="DxfDatabase.DataLinkDictionary"/>,
    /// ACAD_DATALINK) and named by the cells of the tables it fills (<see cref="DxfTable.GetDataLink(int, int)"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A link is read from the file, from R2007 (AC1021) on, which brought DATALINK: <c>db.DataLinkDictionary["Parts"]</c> is
    /// the link Parts. Only its name, the dictionary's entry, is set; the library makes no new link and changes no group of
    /// one, as no link it wrote has yet been opened in AutoCAD.
    /// </para>
    /// <para>
    /// The DXF reference does not describe a DATALINK, and ezdxf keeps one as it reads it. The groups read are those LibreDWG
    /// gives AcDbDataLink: the data adapter (1), the description (300), the tooltip (301), the connection string (302), the
    /// options (90) and the update options (91); a 92 LibreDWG names no further, read here as which way the link was last
    /// updated; when (170 to 176: year, month, day, hour, minute, second, millisecond); a 177 LibreDWG calls the option of the
    /// paths; a 93 it names no further, read here as how the update went; and its status (304). AutoCAD 2021 writes them so,
    /// with no 70, and then the count (94) of the objects the link is tied to, each a 330: the table it fills and that table's
    /// TABLECONTENT, <c>94 2</c>, <c>330 F6</c>, <c>330 149</c>; then what the link owns (360, 0 for nothing), and its custom
    /// data between <c>305 CUSTOMDATA</c>, <c>1 DATAMAP_BEGIN</c>, a count (90) and <c>309 DATAMAP_END</c>. A table's link
    /// taken away (<see cref="DxfTable.RemoveDataLink()"/>) takes the table and what it owns out of that list, as AutoCAD's
    /// <c>RemoveDataLink</c> does.
    /// </para>
    /// <para>
    /// The library does not read the source: updating a link, its source files and their paths, AutoCAD's <c>Update</c>,
    /// <c>GetSourceFiles</c> and <c>RepathSourceFiles</c>, are AutoCAD's data adapters' work, and are not here.
    /// </para>
    /// </remarks>
    public sealed class DxfDataLink : DxfDBObject
    {
        internal DxfDataLink(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the name of the link, as AutoCAD's <c>Name</c>: the name of its entry in the dictionary that holds it,
        /// such as <c>Parts</c>; null for a link in no dictionary.
        /// </summary>
        /// <value>Set, the entry renamed, as <see cref="DxfDBDictionary.SetName"/> renames one; the name the link has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is empty, has a control character in it, or names another
        /// entry of the dictionary.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the link is in no dictionary, or is erased.</exception>
        public string Name
        {
            get => Dictionary()?.NameOf(this);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, Name, StringComparison.Ordinal))
                {
                    return;
                }

                InFile("be renamed");
                DxfDBDictionary dictionary = Dictionary() ?? throw new InvalidOperationException("The data link " + Handle + " is in no dictionary to be named in.");
                dictionary.Rename(this, value);
            }
        }

        /// <summary>
        /// Gets the data adapter that reads the link's source (group 1), as AutoCAD's <c>DataAdapterId</c>: <c>AcExcel</c> for a
        /// spreadsheet; empty where the record gives none.
        /// </summary>
        public string DataAdapterId => Text(1);

        /// <summary>Gets the description of the link (group 300), as AutoCAD's <c>Description</c>; empty where the record gives none.</summary>
        public string Description => Text(300);

        /// <summary>
        /// Gets the tooltip the linked cells show (group 301), as AutoCAD's <c>ToolTip</c>, such as <c>Data source file:
        /// parts.xlsx</c>; empty where the record gives none.
        /// </summary>
        public string ToolTip => Text(301);

        /// <summary>
        /// Gets where the data is (group 302), as AutoCAD's <c>ConnectionString</c>: for a spreadsheet its file, its sheet and
        /// its cells, <c>parts.xlsx!Sheet1!A1:B3</c>; empty where the record gives none.
        /// </summary>
        public string ConnectionString => Text(302);

        /// <summary>
        /// Gets how the link is kept (group 90), as AutoCAD's <c>DataLinkOption</c>: 3 for <see cref="DxfDataLinkOption.Anonymous"/>
        /// and <see cref="DxfDataLinkOption.PersistCache"/>; <see cref="DxfDataLinkOption.None"/> where the record gives none.
        /// </summary>
        public DxfDataLinkOption DataLinkOption => (DxfDataLinkOption)Whole(90, 0);

        /// <summary>
        /// Gets how the link is updated (group 91), as AutoCAD's <c>UpdateOption</c>, the flags as one number; nought where the
        /// record gives none. LibreDWG's example gives 1 179 649: AutoCAD's SkipFormat (0x20000) and AllowSourceUpdate
        /// (0x100000) make 1 179 648, and the 0x1 left over is no flag of AutoCAD's <c>UpdateOption</c>.
        /// </summary>
        public int UpdateOption => Whole(91, 0);

        /// <summary>
        /// Gets whether the link can be updated, as AutoCAD's <c>IsValid</c> tells it: it names a data adapter
        /// (<see cref="DataAdapterId"/>) and where its data is (<see cref="ConnectionString"/>), and is not erased. Whether the
        /// source is there is not looked at.
        /// </summary>
        public bool IsValid => !IsErased && DataAdapterId.Trim().Length > 0 && ConnectionString.Trim().Length > 0;

        /// <summary>
        /// Gets how the link was last updated, as AutoCAD's <c>GetUpdateStatus</c>: which way (group 92), when (170 to 176),
        /// how it went (93, nought for one that went well, as AutoCAD's <c>ErrorStatus</c> numbers it) and the message of that
        /// (the 304 before the count 94). LibreDWG names the 92 and the 93 no further, so which way and how it went are the
        /// library's reading of them. A link never updated, its year nought, gives <see cref="DateTime.MinValue"/>.
        /// </summary>
        /// <param name="direction">Which way, from the source where the record gives none.</param>
        /// <param name="time">When, to the millisecond; <see cref="DateTime.MinValue"/> where the record gives no date that reads as one.</param>
        /// <param name="errorMessage">The message; empty where the record gives none.</param>
        /// <returns>How it went: AutoCAD's number of its <c>ErrorStatus</c>, nought where the record gives none.</returns>
        public int GetUpdateStatus(out DxfUpdateDirection direction, out DateTime time, out string errorMessage)
        {
            direction = Whole(92, 1) == 2 ? DxfUpdateDirection.DataToSource : DxfUpdateDirection.SourceToData;
            int year = Whole(170, 0);
            int month = Whole(171, 0);
            int day = Whole(172, 0);
            int hour = Whole(173, 0);
            int minute = Whole(174, 0);
            int second = Whole(175, 0);
            int millisecond = Whole(176, 0);
            bool dated = year >= 1 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
                && hour >= 0 && hour < 24 && minute >= 0 && minute < 60 && second >= 0 && second < 60 && millisecond >= 0 && millisecond < 1000;
            time = dated ? new DateTime(year, month, day, hour, minute, second, millisecond) : DateTime.MinValue;
            errorMessage = StatusMessage();
            return Whole(93, 0);
        }

        /// <summary>
        /// Gets the tables the link fills, as AutoCAD's <c>GetTargets</c> gives their ids: every table of the drawing, in model
        /// space, paper space or a block, a range of whose cells names the link, in the order of the blocks and of their
        /// entities; none for a link erased. The drawing is walked once.
        /// </summary>
        /// <returns>The tables, a new list.</returns>
        public IReadOnlyList<DxfTable> GetTargets()
        {
            var targets = new List<DxfTable>();
            DxfDatabase database = IsResident && !IsErased ? Item.File.Database : null;

            if (database == null)
            {
                return targets.AsReadOnly();
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfTable table && table.LinksTo(Handle))
                    {
                        targets.Add(table);
                    }
                }
            }

            return targets.AsReadOnly();
        }

        /// <summary>Gives the link as a line for debugging: its type, its handle and its name.</summary>
        /// <returns>The line, such as <c>DATALINK 2F Parts</c>.</returns>
        public override string ToString() => "DATALINK " + (Handle ?? string.Empty) + " " + (Name ?? string.Empty);

        /// <summary>The dictionary that holds the link and names it; null where none does.</summary>
        private DxfDBDictionary Dictionary()
        {
            if (!IsResident)
            {
                return null;
            }

            DxfItem owner = Item.File.ObjectItem(OwnerHandle);
            return owner != null && OfObject(owner) is DxfDBDictionary dictionary && dictionary.NameOf(this) != null ? dictionary : null;
        }

        /// <summary>A text of AcDbDataLink, the first group of its code; empty where the record has none.</summary>
        private string Text(int code) => DxfReader.Decode(Item.File.ValueOf(Item, code) ?? string.Empty);

        private int Whole(int code, int fallback)
            => int.TryParse(Item.File.ValueOf(Item, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>The message of the last update: the 304 before the count of custom data (94), the texts of the custom data being after it.</summary>
        private string StatusMessage()
        {
            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == 94)
                {
                    break;
                }

                if (group.TypeCode == 304)
                {
                    return DxfReader.Decode(group.Value);
                }
            }

            return string.Empty;
        }
    }
}
