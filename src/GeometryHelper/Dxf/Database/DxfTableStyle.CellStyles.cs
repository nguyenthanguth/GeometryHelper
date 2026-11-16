using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The cell styles of a table style beyond the title, the header and the data: the ones a user makes with Create New Cell
    /// Style, which AutoCAD 2021 keeps in the table style's CELLSTYLEMAP (under the entry
    /// <c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c> of its extension dictionary), each with the table format it was made of,
    /// its id (101 on) and its name; and the cell style a cell takes, the id in its TABLECONTENT.
    /// </summary>
    public sealed partial class DxfTableStyle
    {
        private const int FirstUserCellStyle = 101;

        /// <summary>The names of the three cell styles every table style has.</summary>
        private static readonly string[] BuiltInCellStyles = { "Title", "Header", "Data" };

        private sealed class MapEntry
        {
            internal int From;
            internal int To;
            internal int Id;
            internal int Type;
            internal string Name;
        }

        /// <summary>
        /// Gets the names of the cell styles of the style: Title, Header and Data, then those a user made in the order they were
        /// made. A style whose CELLSTYLEMAP is not made yet, as the library makes it when the drawing is saved, has the three.
        /// </summary>
        /// <returns>The names.</returns>
        public IReadOnlyList<string> GetCellStyleNames()
        {
            var names = new List<string>(BuiltInCellStyles);
            DxfItem map = CellStyleMapItem();

            if (map != null)
            {
                foreach (MapEntry entry in Entries(Item.File.TailOf(map, 0), out _))
                {
                    if (entry.Id >= FirstUserCellStyle)
                    {
                        names.Add(entry.Name);
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Makes a cell style of a name from one of the style's, as Create New Cell Style does: the new style takes the look of
        /// the one named (its table format copied into the table style's CELLSTYLEMAP, which a style the library holds is given
        /// first as <see cref="DxfDatabase.Save(string)"/> gives it), its id the next of 101 on.
        /// </summary>
        /// <param name="name">The name of the new cell style.</param>
        /// <param name="startWith">The name of the cell style it starts from: Title, Header, Data or one a user made.</param>
        /// <returns>The id of the new cell style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, is that of a cell style the style has, or the style to start from is none of its.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased or not in a drawing of R2007 or later, or its extension dictionary holds no map of cell styles.</exception>
        public int AddCellStyle(string name, string startWith)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (startWith == null)
            {
                throw new ArgumentNullException(nameof(startWith));
            }

            string made = name.Trim();

            if (made.Length == 0 || made.IndexOf('\n') >= 0 || made.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A cell style has a name of one line.", nameof(name));
            }

            if (HasCellStyle(made))
            {
                throw new ArgumentException("The table style has a cell style " + made + " already.", nameof(name));
            }

            DxfItem map = MapToChange();
            List<DxfTypedValue> groups = Item.File.TailOf(map, 0);
            List<MapEntry> entries = Entries(groups, out int first);
            MapEntry basis = EntryNamed(entries, startWith);

            if (basis == null)
            {
                throw new ArgumentException("The table style has no cell style " + startWith + ".", nameof(startWith));
            }

            int id = FirstUserCellStyle - 1;

            foreach (MapEntry entry in entries)
            {
                id = Math.Max(id, entry.Id);
            }

            id++;
            var copy = new List<KeyValuePair<int, string>>();

            for (int i = basis.From; i <= basis.To; i++)
            {
                copy.Add(new KeyValuePair<int, string>(groups[i].TypeCode, groups[i].Value));
            }

            Retag(copy, id, made);
            Write(map, groups, first, entries.Count + 1, entries, copy, -1);
            return id;
        }

        /// <summary>Whether the style has a cell style of the name (Title, Header, Data or one a user made), whatever the case.</summary>
        /// <param name="name">The name.</param>
        /// <returns>Whether it has.</returns>
        public bool HasCellStyle(string name)
        {
            foreach (string held in GetCellStyleNames())
            {
                if (string.Equals(held, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Takes a cell style a user made out of the style, as Delete does in Manage Cell Styles: its entry out of the
        /// CELLSTYLEMAP. The cells that took it keep the look they were given, and name no style any more.
        /// </summary>
        /// <param name="name">The name of the cell style.</param>
        /// <exception cref="ArgumentException">Thrown when the style has no such cell style of a user, which Title, Header and Data are not.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased or not in a drawing.</exception>
        public void RemoveCellStyle(string name)
        {
            DxfItem map = MapToChange();
            List<DxfTypedValue> groups = Item.File.TailOf(map, 0);
            List<MapEntry> entries = Entries(groups, out int first);
            MapEntry gone = EntryNamed(entries, name);

            if (gone == null || gone.Id < FirstUserCellStyle)
            {
                throw new ArgumentException(
                    "The table style has no cell style of a user named " + name + ".",
                    nameof(name)
                );
            }

            Write(map, groups, first, entries.Count - 1, entries, null, entries.IndexOf(gone));
        }

        /// <summary>Renames a cell style a user made, as Rename does in Manage Cell Styles.</summary>
        /// <param name="name">The name of the cell style.</param>
        /// <param name="newName">The new name.</param>
        /// <exception cref="ArgumentException">Thrown when the style has no such cell style of a user, or has one of the new name already.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased or not in a drawing.</exception>
        public void RenameCellStyle(string name, string newName)
        {
            if (newName == null)
            {
                throw new ArgumentNullException(nameof(newName));
            }

            DxfItem map = MapToChange();
            List<DxfTypedValue> groups = Item.File.TailOf(map, 0);
            List<MapEntry> entries = Entries(groups, out int first);
            MapEntry entry = EntryNamed(entries, name);
            string made = newName.Trim();

            if (entry == null || entry.Id < FirstUserCellStyle)
            {
                throw new ArgumentException(
                    "The table style has no cell style of a user named " + name + ".",
                    nameof(name)
                );
            }

            if (
                made.Length == 0
                || (HasCellStyle(made) && !string.Equals(name, made, StringComparison.OrdinalIgnoreCase))
            )
            {
                throw new ArgumentException(
                    "A cell style has a name of its own: " + newName + " is empty or taken.",
                    nameof(newName)
                );
            }

            var copy = new List<KeyValuePair<int, string>>();

            for (int i = entry.From; i <= entry.To; i++)
            {
                copy.Add(new KeyValuePair<int, string>(groups[i].TypeCode, groups[i].Value));
            }

            Retag(copy, entry.Id, made);
            Write(map, groups, first, entries.Count, entries, copy, entries.IndexOf(entry));
        }

        /// <summary>
        /// Gets the id of a cell style by its name: 1 for Title, 2 for Header, 3 for Data, 101 on for those a user made; the id
        /// a cell of the TABLECONTENT names (<see cref="DxfTable.GetCellStyleId"/>).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The id; 0 where the style has no cell style of the name.</returns>
        public int GetCellStyleId(string name)
        {
            for (int k = 0; k < BuiltInCellStyles.Length; k++)
            {
                if (string.Equals(BuiltInCellStyles[k], name, StringComparison.OrdinalIgnoreCase))
                {
                    return k + 1;
                }
            }

            DxfItem map = CellStyleMapItem();
            MapEntry entry = map == null ? null : EntryNamed(Entries(Item.File.TailOf(map, 0), out _), name);
            return entry?.Id ?? 0;
        }

        /// <summary>Gets the name of the cell style of an id (<see cref="GetCellStyleId"/>); null where the style has none.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The name, or null.</returns>
        public string GetCellStyleName(int id)
        {
            if (id >= 1 && id <= BuiltInCellStyles.Length)
            {
                return BuiltInCellStyles[id - 1];
            }

            DxfItem map = CellStyleMapItem();

            if (map != null)
            {
                foreach (MapEntry entry in Entries(Item.File.TailOf(map, 0), out _))
                {
                    if (entry.Id == id)
                    {
                        return entry.Name;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets what a cell style gives its cells: the look of Title, Header or Data as the style holds it, or of a cell style a
        /// user made as its table format in the CELLSTYLEMAP holds it, the lines by the numbers of their entries.
        /// </summary>
        /// <param name="name">The name of the cell style.</param>
        /// <returns>The look; null where the style has no such cell style.</returns>
        public DxfCellStyleLook GetCellStyleLook(string name)
        {
            for (int k = 0; k < BuiltInCellStyles.Length; k++)
            {
                if (string.Equals(BuiltInCellStyles[k], name, StringComparison.OrdinalIgnoreCase))
                {
                    DxfRowType kind =
                        k == 0 ? DxfRowType.TitleRow
                        : k == 1 ? DxfRowType.HeaderRow
                        : DxfRowType.DataRow;
                    var built = new DxfCellStyleLook
                    {
                        Alignment = Alignment(kind),
                        TextHeight = TextHeight(kind),
                        TextStyleName = TextStyleName(kind),
                        ContentColor = Color(kind),
                        BackgroundColor = IsBackgroundColorNone(kind) ? null : BackgroundColor(kind),
                    };

                    for (int line = 0; line < 6; line++)
                    {
                        built.LineColors[line] = GridColor(DxfCellStyleLook.Lines[line], kind);
                        built.LineWeights[line] = GridLineWeight(DxfCellStyleLook.Lines[line], kind);
                        built.LineShown[line] = GridVisibility(DxfCellStyleLook.Lines[line], kind);
                    }

                    return built;
                }
            }

            DxfItem map = CellStyleMapItem();

            if (map == null)
            {
                return null;
            }

            List<DxfTypedValue> groups = Item.File.TailOf(map, 0);
            MapEntry entry = EntryNamed(Entries(groups, out _), name);
            return entry == null ? null : Look(groups, entry);
        }

        private DxfCellStyleLook Look(List<DxfTypedValue> groups, MapEntry entry)
        {
            var look = new DxfCellStyleLook();
            bool inContent = false;
            bool afterContent = false;
            int line = -1;
            int background = 257;

            for (int i = entry.From; i <= entry.To; i++)
            {
                int code = groups[i].TypeCode;
                string value = groups[i].Value.Trim();
                int number = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int read)
                    ? read
                    : 0;

                if (code == 1 && value == "CONTENTFORMAT_BEGIN")
                {
                    inContent = true;
                }
                else if (code == 309 && value == "CONTENTFORMAT_END")
                {
                    inContent = false;
                    afterContent = true;
                }
                else if (inContent)
                {
                    if (code == 94)
                    {
                        look.Alignment = number >= 1 && number <= 9 ? (DxfCellAlignment)number : look.Alignment;
                    }
                    else if (code == 62)
                    {
                        look.ContentColor = DxfColor.OfIndex(number);
                    }
                    else if (code == 340)
                    {
                        DxfDatabase database = Item.File.Database;

                        foreach (DxfTextStyleTableRecord style in database.TextStyleTable)
                        {
                            if (string.Equals(style.Handle, value, StringComparison.OrdinalIgnoreCase))
                            {
                                look.TextStyleName = style.Name;
                            }
                        }
                    }
                    else if (code == 144)
                    {
                        look.TextHeight = double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double h
                        )
                            ? h
                            : 0.0;
                    }
                }
                else if (!afterContent && code == 62)
                {
                    background = number;
                }
                else if (code == 95)
                {
                    line = Array.IndexOf(DxfCellStyleLook.Lines, (DxfGridLineType)number);
                }
                else if (line >= 0 && code == 62)
                {
                    look.LineColors[line] = DxfColor.OfIndex(number);
                }
                else if (line >= 0 && code == 92)
                {
                    look.LineWeights[line] = (DxfLineWeight)number;
                }
                else if (line >= 0 && code == 93)
                {
                    look.LineShown[line] = number == 0;
                }
            }

            look.BackgroundColor = background == 257 ? null : DxfColor.OfIndex(background);
            return look;
        }

        private DxfItem CellStyleMapItem()
        {
            if (Item == null || !IsResident || IsErased)
            {
                return null;
            }

            DxfDBDictionary dictionary = ExtensionDictionary;
            DxfDBObject map =
                dictionary != null && dictionary.Contains(CellStyleMapName) ? dictionary.GetAt(CellStyleMapName) : null;
            return map?.Item;
        }

        /// <summary>The map of cell styles, made first where the style has none and needs one.</summary>
        private DxfItem MapToChange()
        {
            if (Item == null || !IsResident || IsErased || !Item.File.Supports(1021))
            {
                throw new InvalidOperationException(
                    "The table style is erased, or not in a drawing of R2007 or later, which holds the map of its cell styles."
                );
            }

            DxfItem map = CellStyleMapItem();

            if (map == null)
            {
                MakeCellStyleMap();
                map = CellStyleMapItem();
            }

            return map
                ?? throw new InvalidOperationException(
                    "The extension dictionary of the table style holds no map of cell styles to put a cell style in."
                );
        }

        /// <summary>The entries of the map: each from its 300 CELLSTYLE to its 309 CELLSTYLE_END, with its id (90), type (91) and name (300) after its 1 CELLSTYLE_BEGIN; <paramref name="first"/> is where the groups after the subclass marker begin.</summary>
        private static List<MapEntry> Entries(List<DxfTypedValue> groups, out int first)
        {
            var entries = new List<MapEntry>();
            first = 0;

            while (
                first < groups.Count
                && !(groups[first].TypeCode == 100 && groups[first].Value.Trim() == "AcDbCellStyleMap")
            )
            {
                first++;
            }

            first++;

            for (int i = first; i < groups.Count; i++)
            {
                if (groups[i].TypeCode != 300 || groups[i].Value.Trim() != "CELLSTYLE")
                {
                    continue;
                }

                var entry = new MapEntry { From = i, Name = string.Empty };
                bool named = false;

                for (int j = i; j < groups.Count; j++)
                {
                    if (groups[j].TypeCode == 1 && groups[j].Value.Trim() == "CELLSTYLE_BEGIN")
                    {
                        named = true;
                    }
                    else if (named && groups[j].TypeCode == 90)
                    {
                        int.TryParse(
                            groups[j].Value.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out entry.Id
                        );
                    }
                    else if (named && groups[j].TypeCode == 91)
                    {
                        int.TryParse(
                            groups[j].Value.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out entry.Type
                        );
                    }
                    else if (named && groups[j].TypeCode == 300)
                    {
                        entry.Name = groups[j].Value;
                    }
                    else if (groups[j].TypeCode == 309 && groups[j].Value.Trim() == "CELLSTYLE_END")
                    {
                        entry.To = j;
                        i = j;
                        break;
                    }
                }

                entries.Add(entry);
            }

            return entries;
        }

        private static MapEntry EntryNamed(List<MapEntry> entries, string name)
        {
            for (int k = 0; k < BuiltInCellStyles.Length; k++)
            {
                if (string.Equals(BuiltInCellStyles[k], name, StringComparison.OrdinalIgnoreCase))
                {
                    return entries.Find(e => e.Id == k + 1);
                }
            }

            return entries.Find(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>An entry's id (the 90 after its 1 CELLSTYLE_BEGIN) and name (its 300) set.</summary>
        private static void Retag(List<KeyValuePair<int, string>> entry, int id, string name)
        {
            bool named = false;
            bool numbered = false;

            for (int i = 0; i < entry.Count; i++)
            {
                if (entry[i].Key == 1 && entry[i].Value.Trim() == "CELLSTYLE_BEGIN")
                {
                    named = true;
                }
                else if (named && !numbered && entry[i].Key == 90)
                {
                    entry[i] = new KeyValuePair<int, string>(90, id.ToString(CultureInfo.InvariantCulture));
                    numbered = true;
                }
                else if (named && entry[i].Key == 300)
                {
                    entry[i] = new KeyValuePair<int, string>(300, name);
                }
            }
        }

        /// <summary>
        /// Writes the map again: its count (90) and its entries, one replaced (<paramref name="changed"/> at the place given), one
        /// added where <paramref name="changed"/> is given and no place, one taken out where there is a place and none given.
        /// </summary>
        private void Write(
            DxfItem map,
            List<DxfTypedValue> groups,
            int first,
            int count,
            List<MapEntry> entries,
            List<KeyValuePair<int, string>> changed,
            int at
        )
        {
            var tail = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(90, count.ToString(CultureInfo.InvariantCulture)),
            };

            for (int k = 0; k < entries.Count; k++)
            {
                if (k == at && changed == null)
                {
                    continue;
                }

                if (k == at)
                {
                    tail.AddRange(changed);
                    continue;
                }

                for (int i = entries[k].From; i <= entries[k].To; i++)
                {
                    tail.Add(new KeyValuePair<int, string>(groups[i].TypeCode, groups[i].Value));
                }
            }

            if (at < 0 && changed != null)
            {
                tail.AddRange(changed);
            }

            Item.File.ReplaceTail(map, first, tail);
        }
    }
}
