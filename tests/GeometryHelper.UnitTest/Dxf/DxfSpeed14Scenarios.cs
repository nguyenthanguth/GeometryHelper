using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings made in code for the work of dxfver14, whose saved bytes are held to what the library gave before it: runs of
    /// edits of a table's cells and of the lines of its rows. Public members only, so that the same scenarios run against an
    /// older build.
    /// </summary>
    internal static class DxfSpeed14Scenarios
    {
        private static DxfColor Aci(int index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);

        /// <summary>
        /// One edit of a table of 5 rows and 4 columns, the cell and the kind picked by a generator of numbers of its own, the
        /// last cell among them: the setters that write a cell, and those of the lines of the rows of a kind.
        /// </summary>
        internal static void Edit(DxfTable table, long[] state, int i)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            int r = Next(table.NumRows);
            int c = Next(table.NumColumns);

            if (Next(4) == 0)
            {
                r = table.NumRows - 1;
                c = table.NumColumns - 1;
            }

            int p = Next(1000);
            var lines = new[]
            {
                DxfGridLineType.HorizontalTop,
                DxfGridLineType.HorizontalBottom,
                DxfGridLineType.VerticalLeft,
                DxfGridLineType.VerticalRight,
            };
            var borders = (DxfGridLineType)(1 + Next(63));
            var rows = (DxfRowType)(1 + Next(7));
            var weights = new[] { DxfLineWeight.LineWeight025, DxfLineWeight.LineWeight050, DxfLineWeight.ByLayer };

            switch (Next(20))
            {
                case 0:
                case 1:
                    table.SetTextString(r, c, "cell " + r + "," + c + " #" + i);
                    break;
                case 2:
                    table.SetValue(r, c, r * 10 + c + p / 7.0);
                    break;
                case 3:
                    table.SetTextHeight(r, c, 1.5 + (p % 5));
                    break;
                case 4:
                    table.SetContentColor(r, c, Aci(1 + (p % 7)));
                    break;
                case 5:
                    table.SetOverrides(r, c, 0, (DxfCellProperties)(p % 256));
                    break;
                case 6:
                    table.ResetValue(r, c);
                    break;
                case 7:
                    table.SetCellType(r, c, DxfTableCellType.TextCell);
                    break;
                case 8:
                    table.SetGridColor(r, c, lines[p % 4], Aci(1 + (p % 7)));
                    break;
                case 9:
                    table.SetGridVisibility(r, c, lines[p % 4], p % 2 == 0);
                    break;
                case 10:
                    table.SetFormat(r, c, p % 2 == 0 ? "%lu2%pr2" : "%lu4%pr2");
                    break;
                case 11:
                    table.SetDataType(r, c, DxfDataType.Double, DxfUnitType.Distance);
                    break;
                case 12:
                case 13:
                case 14:
                    table.SetGridColor(Aci(1 + (p % 7)), borders, rows);
                    break;
                case 15:
                case 16:
                    table.SetGridLineWeight(weights[p % 3], borders, rows);
                    break;
                case 17:
                case 18:
                    table.SetGridVisibility(p % 2 == 0, borders, rows);
                    break;
                default:
                    table.SetGridProperty(
                        new DxfCellRange(
                            r,
                            c,
                            Math.Min(r + 1, table.NumRows - 1),
                            Math.Min(c + 1, table.NumColumns - 1)
                        ),
                        DxfGridLineType.AllGridLines,
                        new DxfGridPropertyParameter
                        {
                            PropertyMask = DxfGridProperties.Color | DxfGridProperties.Visibility,
                            Color = Aci(1 + (p % 7)),
                            Visible = p % 2 == 0,
                        }
                    );
                    break;
            }
        }

        /// <summary>
        /// A run of edits of a table, saved as it stands. <paramref name="reopen"/> opens the drawing anew from its text before
        /// each edit, so that the table's record is not one the table wrote, and each edit reads the whole of it.
        /// </summary>
        internal static string Run(string version, int seed, int edits, bool reopen, bool extended = false)
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric(version);
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 5, 4, 8.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            long[] state = { seed * 2654435761L + 12345 };

            // Extended data after the table's own groups, set at the start and changed part way: the bytes the table's own groups
            // do not reach.
            if (extended)
            {
                table.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "B-12") };
            }

            for (int i = 0; i < edits; i++)
            {
                if (reopen)
                {
                    db = DxfDatabase.Parse(db.ToText());
                    table = db.ModelSpace.OfType<DxfTable>().Single();
                }

                if (extended && i == edits / 2)
                {
                    table.XData = new[]
                    {
                        new DxfTypedValue(1001, "GHPROBE"),
                        new DxfTypedValue(1000, "a longer text than the one before it"),
                        new DxfTypedValue(1040, "2.5"),
                    };
                }

                try
                {
                    Edit(table, state, i);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>
        /// A drawing read from the text of a table made in code, whose every cell is a block cell: the 171 of each cell (its kind,
        /// 1 for a text) of the table's record given 2, and a block, a scale and no attribute flag put after it, as AutoCAD writes
        /// a block cell. The library makes no block cell, so the text of the record is changed before it is read.
        /// </summary>
        internal static DxfDatabase WithBlockCells(int rows, int columns)
        {
            DxfDatabase made = DxfSpeed12Scenarios.Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0);
            made.ModelSpace.AppendEntity(table);
            string text = made.ToText();
            Match start = Regex.Match(text, @"\r?\n  0\r?\nACAD_TABLE\r?\n");
            int from = start.Index + start.Length;
            int to = Regex.Match(text.Substring(from), @"\r?\n  0\r?\n").Index + from;
            string record = text.Substring(from, to - from);
            string blocked = Regex.Replace(
                record,
                @"171(\r?\n)\s*1(\r?\n)",
                "171${1}     2${2}340${1}ABCD${2}144${1}1.0${2}179${1}0${2}"
            );
            return DxfDatabase.Parse(text.Substring(0, from) + blocked + text.Substring(to));
        }

        /// <summary>The SHA-256 of the file saved after every cell of a table of block cells is made a text cell, one by one.</summary>
        internal static string BlockCellsMadeText()
        {
            DxfDatabase db = WithBlockCells(12, 5);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();

            for (int r = 0; r < 12; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    table.SetCellType(r, c, DxfTableCellType.TextCell);

                    if ((r + c) % 7 == 0)
                    {
                        table.SetTextString(r, c, "text " + r + "," + c);
                    }
                }
            }

            return DxfSpeed12Scenarios.Hash(DxfSpeed12Scenarios.Saved(db));
        }

        /// <summary>The SHA-256 of the file each run of edits saves, in every version a table is written in.</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1027", "AC1021", "AC1018", "AC1015" })
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    DxfDatabase db = DxfSpeed12Scenarios.Metric(version);
                    var table = new DxfTable(new GeoPoint3(0, 0, 0), 5, 4, 8.0, 30.0);
                    db.ModelSpace.AppendEntity(table);
                    long[] state = { seed * 2654435761L + 12345 };

                    for (int i = 0; i < 120; i++)
                    {
                        try
                        {
                            Edit(table, state, i);
                        }
                        catch (InvalidOperationException) { }
                        catch (ArgumentException) { }
                    }

                    lines.Add(
                        version + " seed " + seed + " " + DxfSpeed12Scenarios.Hash(DxfSpeed12Scenarios.Saved(db))
                    );
                }
            }

            return lines;
        }
    }
}
