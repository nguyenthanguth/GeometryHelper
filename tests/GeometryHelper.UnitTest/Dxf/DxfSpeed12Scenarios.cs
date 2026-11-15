using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings made in code whose saved bytes are held to what they were before the work of dxfver12 (the completed copy found
    /// without the drawing's entities, cells written apart from the record and put in it once, the setters of a format, a kind
    /// of value, a fill, overrides and a reset on the path of one cell): each scenario returns the SHA-256 of what it saves,
    /// and the tests hold them to the hashes the library gave before that work. Public members only, so that the same
    /// scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed12Scenarios
    {
        internal static DxfDatabase Metric(string version = "AC1032")
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

        /// <summary>
        /// A run of edits of the cells of a table of 8 rows and 6 columns, of every kind of setter there is, the cell and the
        /// kind picked by a generator of numbers of its own: the last cell among them. <paramref name="reads"/> reads a cell after
        /// each, which puts into the record whatever is kept apart from it.
        /// </summary>
        internal static void Run(DxfDatabase db, DxfTable table, int edits, int seed, bool reads)
        {
            var style = (DxfTextStyleTableRecord)db.TextStyleTable["GH_TXT"];
            long state = seed * 2654435761L + 12345;
            bool newer = db.Version != "AC1015" && db.Version != "AC1018";

            int Next(int below)
            {
                state = (state * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state >> 33) % below);
            }

            for (int i = 0; i < edits; i++)
            {
                int r = Next(table.NumRows);
                int c = Next(table.NumColumns);

                if (Next(30) == 0)
                {
                    r = table.NumRows - 1;
                    c = table.NumColumns - 1;
                }

                switch (Next(newer ? 16 : 9))
                {
                    case 0:
                    case 1:
                        table.SetTextString(r, c, "cell " + r + "," + c + " #" + i);
                        break;
                    case 2:
                        table.SetValue(r, c, r * 10 + c);
                        break;
                    case 3:
                        table.SetValue(r, c, r + c / 7.0);
                        break;
                    case 4:
                        table.SetAlignment(r, c, (DxfCellAlignment)(1 + (r + c + i) % 9));
                        break;
                    case 5:
                        table.SetTextHeight(r, c, 1.5 + (i % 5));
                        break;
                    case 6:
                        table.SetContentColor(r, c, Aci(1 + (r + c + i) % 7));
                        break;
                    case 7:
                        table.SetTextString(r, c, "café Đ A\\PB");
                        break;
                    case 8:
                        table.SetTextString(r, c, new string('w', 100 + 140 * ((r + c + i) % 3)));
                        break;
                    case 9:
                        table.SetFormat(r, c, i % 2 == 0 ? "%lu2%pr2" : "%lu4%pr2");
                        break;
                    case 10:
                        table.SetDataType(r, c, DxfDataType.String, DxfUnitType.Unitless);
                        break;
                    case 11:
                        table.SetDataType(r, c, DxfDataType.Double, DxfUnitType.Distance);
                        break;
                    case 12:
                        table.SetBackgroundColorNone(r, c, i % 3 == 0);
                        break;
                    case 13:
                        table.SetOverrides(r, c, 0, (DxfCellProperties)((i * 37) % 256));
                        break;
                    case 14:
                        table.ResetValue(r, c);
                        break;
                    default:
                        table.SetTextStyle(r, c, style);
                        break;
                }

                if (reads)
                {
                    _ = table.TextString(0, 0);
                }
            }
        }

        /// <summary>Runs of edits in every version a table is completed or written in, saved as they stand, in three versions.</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1027", "AC1021", "AC1018", "AC1015" })
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    DxfDatabase db = Metric(version);
                    var table = new DxfTable(new GeoPoint3(10, 20, 0), 8, 6, 8.0, 30.0);
                    db.ModelSpace.AppendEntity(table);
                    Run(db, table, 150, seed, false);
                    lines.Add(version + " seed " + seed + " " + Hash(Saved(db)) + " " + Hash(Saved(db, "AC1027")));
                    Run(db, table, 60, seed + 10, false);
                    lines.Add(
                        version + " seed " + seed + " again " + Hash(Saved(db)) + " " + Hash(Saved(db, "AC1015"))
                    );
                }
            }

            return lines;
        }

        /// <summary>Tables in paper space and in a block, written in the session, saved.</summary>
        internal static List<string> Placed()
        {
            var lines = new List<string>();
            DxfDatabase db = Metric();
            var block = new DxfBlockTableRecord { Name = "SCHEDULES" };
            db.BlockTable.Add(block);

            for (int t = 0; t < 6; t++)
            {
                var table = new DxfTable(new GeoPoint3(t * 100, 0, 0), 3 + t % 3, 2 + t % 2, 9.0, 30.0);

                if (t % 3 == 0)
                {
                    db.ModelSpace.AppendEntity(table);
                }
                else if (t % 3 == 1)
                {
                    db.PaperSpace.AppendEntity(table);
                }
                else
                {
                    block.AppendEntity(table);
                }

                table.SetTextString(1, 0, "t" + t);
                table.SetValue(2, 1, t);
                lines.Add("placed " + t + " " + Hash(Saved(db)) + " " + Hash(Saved(db, "AC1027")));
            }

            return lines;
        }

        internal static string Digest(IEnumerable<string> lines) => string.Join("\n", lines);
    }
}
