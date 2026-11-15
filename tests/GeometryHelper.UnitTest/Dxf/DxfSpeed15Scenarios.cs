using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings made in code for the work of tables6, whose saved bytes are held to what the library gave before it: tables of
    /// block cells and text cells edited by the setters of a typed text, a formula, the fit and the scale and turn of a
    /// block, an attribute value, and the merging of cells. Public members, and the internal <c>SetValueFromText</c>, so that
    /// the same scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed15Scenarios
    {
        /// <summary>
        /// A drawing with a block BOLT of one attribute definition and a table of rows by columns, whose cells where
        /// <c>(r + c) % 2 == 0</c> are block cells of BOLT, each giving the definition a value, and whose others are text. The
        /// library makes no block cell, so the text of the record is changed before it is read.
        /// </summary>
        internal static DxfDatabase WithBlockCells(string version, int rows, int columns)
        {
            DxfDatabase made = DxfSpeed12Scenarios.Metric(version);
            var bolt = new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(0, 0, 0) };
            made.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0)));
            var size = new DxfAttributeDefinition(new GeoPoint3(22, 0, 0), "M12", "SIZE", "Size", null);
            bolt.AppendEntity(size);
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0);
            made.ModelSpace.AppendEntity(table);

            string text = made.ToText();
            Match start = Regex.Match(text, @"\r?\n  0\r?\nACAD_TABLE\r?\n");
            int from = start.Index + start.Length;
            int to = Regex.Match(text.Substring(from), @"\r?\n  0\r?\n").Index + from;
            string record = text.Substring(from, to - from);
            int index = 0;
            string blocked = Regex.Replace(record, @"171(\r?\n)\s*1(\r?\n)", match =>
            {
                int cell = index++;
                int r = cell / columns;
                int c = cell % columns;

                if ((r + c) % 2 != 0)
                {
                    return match.Value;
                }

                string nl = match.Groups[1].Value;
                return "171"
                    + nl
                    + "     2"
                    + nl
                    + "340"
                    + nl
                    + bolt.Handle
                    + nl
                    + "144"
                    + nl
                    + "1.0"
                    + nl
                    + "179"
                    + nl
                    + "1"
                    + nl
                    + "331"
                    + nl
                    + size.Handle
                    + nl
                    + "300"
                    + nl
                    + "V"
                    + r
                    + "."
                    + c
                    + nl;
            });
            return DxfDatabase.Parse(text.Substring(0, from) + blocked + text.Substring(to));
        }

        private static readonly string[] Typed =
        {
            "12",
            "1.5",
            "1/2",
            "1,5,7",
            "1,500",
            "12'6\"",
            "1e3",
            "words",
            "-3",
            "0",
            string.Empty,
        };

        /// <summary>
        /// One edit of a table of 5 rows and 4 columns, the cell and the kind picked by a generator of numbers of its own, the
        /// last cell among them: the setters of this work among a few of the older ones.
        /// </summary>
        internal static void Edit(DxfTable table, DxfAttributeDefinition size, long[] state, int i)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            // From the second row on: the first row, once its cells hold no value, is merged across by a table read anew (as AutoCAD
            // merges it on load), which a table edited in place does not do again.
            int r = 1 + Next(table.NumRows - 1);
            int c = Next(table.NumColumns);

            if (Next(4) == 0)
            {
                r = table.NumRows - 1;
                c = table.NumColumns - 1;
            }

            int p = Next(1000);
            var range = new DxfCellRange(
                r,
                c,
                Math.Min(r + Next(2), table.NumRows - 1),
                Math.Min(c + Next(3), table.NumColumns - 1)
            );
            string[] formulas = { "=A1+B1", "=Sum(A1:B2)", "=(1+2)*3", "=C2" };

            switch (Next(22))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    table.SetValueFromText(r, c, Typed[p % Typed.Length]);
                    break;
                case 4:
                case 5:
                case 6:
                    table.SetFormula(r, c, 0, formulas[p % formulas.Length]);
                    break;
                case 7:
                case 8:
                    table.SetAutoScale(r, c, p % 2 == 0);
                    break;
                case 9:
                case 10:
                    table.SetBlockScale(r, c, 0.5 + (p % 7) / 4.0);
                    break;
                case 11:
                    table.SetBlockRotation(r, c, (p % 12) * Math.PI / 6.0);
                    break;
                case 12:
                case 13:
                    table.SetBlockAttributeValue(r, c, size, "M" + (p % 30));
                    break;
                case 14:
                case 15:
                    table.MergeCells(range);
                    break;
                case 16:
                    table.UnmergeCells(range);
                    break;
                case 17:
                    table.SetTextString(r, c, "cell " + r + "," + c + " #" + i);
                    break;
                case 18:
                    table.SetValue(r, c, r * 10 + c + p / 7.0);
                    break;
                case 19:
                    table.ResetValue(r, c);
                    break;
                case 20:
                    table.SetCellType(r, c, DxfTableCellType.TextCell);
                    break;
                default:
                    table.SetGridColor(
                        r,
                        c,
                        DxfGridLineType.HorizontalBottom,
                        DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(1 + p % 7))
                    );
                    break;
            }
        }

        private static DxfAttributeDefinition SizeOf(DxfDatabase db) =>
            db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();

        /// <summary>
        /// A run of edits of a table, saved as it stands. <paramref name="reopen"/> opens the drawing anew from its text before
        /// each edit, so that the table's record is not one the table wrote, and each edit reads the whole of it.
        /// </summary>
        internal static string Run(string version, int seed, int edits, bool reopen)
        {
            DxfDatabase db = WithBlockCells(version, 5, 4);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            long[] state = { seed * 2654435761L + 12345 };

            for (int i = 0; i < edits; i++)
            {
                if (reopen)
                {
                    db = DxfDatabase.Parse(db.ToText());
                    table = db.ModelSpace.OfType<DxfTable>().Single();
                }

                try
                {
                    Edit(table, SizeOf(db), state, i);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>The SHA-256 of the file each run of edits saves, in every version a table is written in from R2004 on.</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1027", "AC1021", "AC1018" })
            {
                for (int seed = 1; seed <= 6; seed++)
                {
                    lines.Add(
                        version
                            + " seed "
                            + seed
                            + " "
                            + DxfSpeed12Scenarios.Hash(
                                System.Text.Encoding.UTF8.GetBytes(Run(version, seed, 120, false))
                            )
                    );
                }
            }

            return lines;
        }
    }
}
