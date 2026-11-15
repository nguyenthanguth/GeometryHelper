using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region The version of a multileader

        private const string MLeaderApplication = "ACAD_MLEADERVER";

        /// <summary>
        /// Gives each multileader and each multileader style of a drawing saved before R2010 the extended data of the application
        /// <c>ACAD_MLEADERVER</c> (the version 2) that AutoCAD keeps on it in such a file, and registers the application at the end of
        /// the APPID table with the next handle of the drawing, as AutoCAD does. One that has the data already is left as it is, and
        /// so is a drawing with no APPID table or no handle to give (nothing is written that names no application).
        /// </summary>
        private void MLeadersVersion()
        {
            if (_target >= 1024)
            {
                return;
            }

            var lacking = new List<Record>();

            foreach (string name in new[] { "BLOCKS", "ENTITIES", "OBJECTS" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    if (
                        !record.Dropped
                        && (record.Type == "MULTILEADER" || record.Type == "MLEADERSTYLE")
                        && !record.Groups.Exists(g =>
                            g.Code == 1001
                            && string.Equals(g.Value.Trim(), MLeaderApplication, StringComparison.Ordinal)
                        )
                    )
                    {
                        lacking.Add(record);
                    }
                }
            }

            if (lacking.Count == 0 || !RegisterApplication(MLeaderApplication))
            {
                return;
            }

            foreach (Record record in lacking)
            {
                record.Groups.Add(new Group(1001, MLeaderApplication));
                record.Groups.Add(Whole(1070, 2));
            }
        }

        /// <summary>
        /// Puts the application <paramref name="name"/> at the end of the APPID table, its count one more, with the next handle of the
        /// drawing and the table as its owner. True where the table has the application already or it was put there; false where the
        /// drawing has no APPID table, or has handles and no seed to take one from.
        /// </summary>
        private bool RegisterApplication(string name)
        {
            Section tables = Find("TABLES");

            if (tables == null)
            {
                return false;
            }

            int head = tables.Records.FindIndex(r => r.Type == "TABLE" && TableName(r) == "APPID");

            if (head < 0)
            {
                return false;
            }

            int end = head + 1;

            while (
                end < tables.Records.Count
                && tables.Records[end].Type != "ENDTAB"
                && tables.Records[end].Type != "TABLE"
            )
            {
                if (
                    tables.Records[end].Type == "APPID"
                    && string.Equals(
                        tables.Records[end].Groups.Find(g => g.Code == 2).Value?.Trim(),
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }

                end++;
            }

            string table = HandleOf(tables.Records[head]);
            string handle = table == null ? null : HandleAfterTheLast();

            var application = new Record();
            application.Groups.Add(new Group(0, "APPID"));

            if (handle != null)
            {
                application.Groups.Add(new Group(5, handle));
                application.Groups.Add(new Group(330, table));
            }

            application.Groups.Add(new Group(100, "AcDbSymbolTableRecord"));
            application.Groups.Add(new Group(100, "AcDbRegAppTableRecord"));
            application.Groups.Add(new Group(2, name));
            application.Groups.Add(Whole(70, 0));
            tables.Records.Insert(end, application);

            List<Group> groups = tables.Records[head].Groups;
            int count = groups.FindIndex(g => g.Code == 70);

            if (count >= 0 && Number(groups[count].Value, out int entries))
            {
                groups[count] = Whole(70, entries + 1);
            }

            return true;
        }

        /// <summary>
        /// The handle after the highest of the records the file keeps (tables, blocks, entities and objects), as hexadecimal; the seed
        /// of the drawing (<c>$HANDSEED</c>) moves past it where it is not past it already, so that a seed that lags behind (a file of
        /// another maker's) does not hand it out twice.
        /// </summary>
        private string HandleAfterTheLast()
        {
            ulong highest = 0;

            foreach (string name in new[] { "TABLES", "BLOCKS", "ENTITIES", "OBJECTS" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    string text = record.Dropped ? null : HandleOf(record);

                    if (
                        text != null
                        && ulong.TryParse(
                            text,
                            NumberStyles.AllowHexSpecifier,
                            CultureInfo.InvariantCulture,
                            out ulong handle
                        )
                        && handle > highest
                    )
                    {
                        highest = handle;
                    }
                }
            }

            highest++;
            Record seed = _seed
                ?? (_seed = Find("HEADER")?.Records.Find(r => r.Groups[0].Value == "$HANDSEED" && r.Groups.Count > 1));

            if (
                seed != null
                && ulong.TryParse(
                    seed.Groups[1].Value.Trim(),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out ulong next
                )
                && next <= highest
            )
            {
                seed.Groups[1] = new Group(
                    seed.Groups[1].Code,
                    (highest + 1).ToString("X", CultureInfo.InvariantCulture)
                );
            }

            return highest.ToString("X", CultureInfo.InvariantCulture);
        }

        /// <summary>The name of the table a TABLE record opens (its group 2); null where it has none.</summary>
        private static string TableName(Record head)
        {
            int at = head.Groups.FindIndex(g => g.Code == 2);
            return at < 0 ? null : head.Groups[at].Value.Trim();
        }

        #endregion

        #region The letters of an escape

        /// <summary>
        /// Puts the letters of a drawing of R2004 or before in place of their escapes where it is saved as R2007 or later. A DXF of
        /// R2004 and before holds a letter beyond its code page as <c>\U+1EDB</c> and a letter of a multibyte code page as
        /// <c>\M+182A0</c> (the Shift-JIS a); a DXF of R2007 and later holds the letters themselves, and AutoCAD finds a layer's name
        /// that still has the escape "Un-normalized" (AUDIT). An escape typed in the text of a drawing of R2007 or later is the text,
        /// and is left.
        /// </summary>
        private void Letters()
        {
            if (_source >= 1021 || _target < 1021)
            {
                return;
            }

            foreach (Section section in _sections)
            {
                foreach (Record record in section.Records)
                {
                    if (record.Dropped)
                    {
                        continue;
                    }

                    List<Group> groups = record.Groups;

                    for (int i = 0; i < groups.Count; i++)
                    {
                        string value = groups[i].Value;

                        if (value.IndexOf('\\') < 0)
                        {
                            continue;
                        }

                        string letters = DxfReader.Decode(value);

                        if (!ReferenceEquals(letters, value))
                        {
                            groups[i] = new Group(groups[i].Code, letters);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
