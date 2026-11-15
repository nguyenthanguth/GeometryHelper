using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings made in code whose saved bytes are held to what they were before the table, clone and converter work of
    /// dxfver11: each scenario returns the SHA-256 of what it saves, and the tests hold them to the hashes the library gave
    /// before that work. Public members only, so that the same scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed11Scenarios
    {
        private static DxfDatabase Metric(string version = "AC1032")
        {
            var db = new DxfDatabase(version);
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TXT", FileName = "arial.ttf" });
            return db;
        }

        internal static byte[] Saved(DxfDatabase db, string version = null)
        {
            using (var stream = new MemoryStream())
            {
                if (version == null)
                {
                    db.Save(stream);
                }
                else
                {
                    db.Save(stream, version);
                }

                return stream.ToArray();
            }
        }

        internal static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).Substring(0, 24);
            }
        }

        private static DxfColor Aci(int index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);

        /// <summary>The edits of the cells of a table of 6 rows and 5 columns, one by one, of every kind the cell setters make.</summary>
        internal static void EditCells(DxfDatabase db, DxfTable table, int round)
        {
            DxfTextStyleTableRecord style = (DxfTextStyleTableRecord)db.TextStyleTable["GH_TXT"];

            for (int r = 0; r < table.NumRows; r++)
            {
                for (int c = 0; c < table.NumColumns; c++)
                {
                    switch ((r * 7 + c * 3 + round) % 11)
                    {
                        case 0:
                            table.SetTextString(r, c, "cell " + r + "," + c);
                            break;
                        case 1:
                            table.SetValue(r, c, r * 10 + c);
                            break;
                        case 2:
                            table.SetValue(r, c, r + c / 7.0);
                            break;
                        case 3:
                            table.SetAlignment(r, c, (DxfCellAlignment)(1 + (r + c) % 9));
                            break;
                        case 4:
                            table.SetTextHeight(r, c, 1.5 + r);
                            break;
                        case 5:
                            table.SetContentColor(r, c, Aci(1 + (r + c) % 7));
                            break;
                        case 6:
                            table.SetBackgroundColor(r, c, Aci(1 + (r * c) % 7));
                            break;
                        case 7:
                            table.SetTextStyle(r, c, style);
                            break;
                        case 8:
                            table.SetTextString(r, c, new string('w', 100 + 140 * ((r + c) % 3)));
                            break;
                        case 9:
                            table.SetTextString(r, c, "café Đ A\\PB");
                            break;
                        default:
                            table.SetValue(r, c, "text " + c);
                            break;
                    }
                }
            }
        }

        /// <summary>A table's cells edited round after round, saved in three versions between the rounds and at the end.</summary>
        internal static List<string> CellEdits()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1015", "AC1018", "AC1024" })
            {
                DxfDatabase db = Metric(version);
                var table = new DxfTable(new GeoPoint3(10, 20, 0), 6, 5, 8.0, 30.0);
                db.ModelSpace.AppendEntity(table);

                for (int round = 0; round < 3; round++)
                {
                    EditCells(db, table, round);
                    lines.Add(version + " round " + round + " native " + Hash(Saved(db)));
                    lines.Add(version + " round " + round + " R2013 " + Hash(Saved(db, "AC1027")));
                    lines.Add(version + " round " + round + " R2000 " + Hash(Saved(db, "AC1015")));
                }
            }

            return lines;
        }

        /// <summary>A table read back from its text, in each line end and padding, and edited cell by cell, the last cell among them.</summary>
        internal static List<string> CellEditsAfterReading()
        {
            var lines = new List<string>();
            DxfDatabase first = Metric();
            var made = new DxfTable(new GeoPoint3(0, 0, 0), 5, 4, 9.0, 25.0);
            first.ModelSpace.AppendEntity(made);
            made.SetTextString(1, 1, "one");
            string text = first.ToText();

            foreach (bool crlf in new[] { false, true })
            {
                foreach (bool padded in new[] { true, false })
                {
                    string[] parts = text.Replace("\r\n", "\n").Split('\n');

                    for (int i = 0; !padded && i < parts.Length; i += 2)
                    {
                        parts[i] = parts[i].Trim();
                    }

                    string joined = string.Join(crlf ? "\r\n" : "\n", parts);
                    DxfDatabase db = DxfDatabase.Parse(joined);
                    DxfTable table = null;

                    foreach (DxfEntity entity in db.ModelSpace)
                    {
                        table = table ?? entity as DxfTable;
                    }

                    for (int round = 0; round < 2; round++)
                    {
                        EditCells(db, table, round + 4);
                        table.SetTextString(table.NumRows - 1, table.NumColumns - 1, "last " + round);
                        table.SetAlignment(table.NumRows - 1, table.NumColumns - 1, DxfCellAlignment.MiddleCenter);
                        lines.Add(
                            "crlf " + crlf + " padded " + padded + " round " + round + " " + Hash(Saved(db, "AC1032"))
                        );
                    }

                    lines.Add("crlf " + crlf + " padded " + padded + " R2013 " + Hash(Saved(db, "AC1027")));
                }
            }

            return lines;
        }

        /// <summary>Tables added one after another, their cells set, saved as the drawing grows.</summary>
        internal static List<string> ManyTables()
        {
            var lines = new List<string>();
            DxfDatabase db = Metric();

            for (int t = 0; t < 30; t++)
            {
                var table = new DxfTable(new GeoPoint3(t * 100, 0, 0), 3 + t % 4, 2 + t % 3, 9.0, 30.0);
                db.ModelSpace.AppendEntity(table);
                table.SetTextString(1, 0, "t" + t);
                table.SetValue(2, 1, t);

                if (t % 10 == 9)
                {
                    lines.Add("tables " + (t + 1) + " " + Hash(Saved(db)));
                }
            }

            lines.Add("tables R2013 " + Hash(Saved(db, "AC1027")));
            return lines;
        }

        /// <summary>Tables copied to another drawing among many objects the first holds, saved.</summary>
        internal static List<string> Clones()
        {
            var lines = new List<string>();
            DxfDatabase source = Metric();

            for (int i = 0; i < 120; i++)
            {
                source.NamedObjectsDictionary.SetAt("OBJ" + i, new DxfXrecord());
            }

            var tables = new List<DxfEntity>();

            for (int t = 0; t < 25; t++)
            {
                var table = new DxfTable(new GeoPoint3(t * 100, 0, 0), 3, 2, 9.0, 30.0);
                source.ModelSpace.AppendEntity(table);
                table.SetTextString(1, 0, "t" + t);
                tables.Add(table);
            }

            DxfDatabase target = Metric();
            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(tables, target.ModelSpace);
            lines.Add("copies " + copies.Count + " " + Hash(Saved(target)));

            DxfDatabase again = Metric();
            source.WblockCloneObjects(tables, again.ModelSpace);
            source.WblockCloneObjects(tables, again.ModelSpace);
            lines.Add("twice " + Hash(Saved(again)) + " " + Hash(Saved(again, "AC1027")));
            return lines;
        }

        internal static string Digest(IEnumerable<string> lines) => string.Join("\n", lines);
    }
}
