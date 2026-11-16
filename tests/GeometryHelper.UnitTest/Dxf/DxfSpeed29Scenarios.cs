using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Runs of edits for the work of tables12, whose saved bytes are held to what the library gave before it: tables read from
    /// a file with their TABLECONTENT (<see cref="DxfSpeed25Scenarios.Table"/>), edited by data links put in and taken away
    /// among rows put in and taken out at the title, the header and further down, heights, texts, widths, merges and formulas,
    /// with what the table says of its links and of the kinds of its rows asked between the edits, often several times, so that
    /// what is kept of the TABLECONTENT between two questions is asked for after edits that leave it as it is and after edits
    /// that change it. Public members only, so that the same scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed29Scenarios
    {
        private const string Parts = "FFF01";
        private const string Prices = "FFF03";

        /// <summary>One edit of a table, and now and then what the table says of its links and of the kinds of its rows appended to <paramref name="said"/>.</summary>
        internal static void Edit(DxfDatabase db, DxfTable table, long[] state, StringBuilder said)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            if (table.NumRows < 6)
            {
                table.InsertRows(table.NumRows - 1, 8.0, 3);
                return;
            }

            if (table.NumColumns < 4)
            {
                table.InsertColumns(table.NumColumns - 1, 30.0, 2);
                return;
            }

            // The rows at the top, where the kinds of the rows move, as often as the rest.
            int r = Next(3) == 0 ? Next(3) : 1 + Next(table.NumRows - 1);
            int c = Next(table.NumColumns);
            var range = new DxfCellRange(
                r,
                c,
                Math.Min(r + Next(3), table.NumRows - 1),
                Math.Min(c + Next(3), table.NumColumns - 1)
            );
            var link = (DxfDataLink)db.GetObject(Next(2) == 0 ? Parts : Prices);
            int pick = Next(24);

            if (pick < 4)
            {
                table.SetDataLink(range, link);
            }
            else if (pick < 5)
            {
                table.SetDataLink(r, c, link);
            }
            else if (pick < 6)
            {
                table.RemoveDataLink();
            }
            else if (pick < 8)
            {
                table.RemoveDataLink(r, c);
            }
            else if (pick == 8 || pick == 9)
            {
                table.InsertRows(r, 8.0, 1 + Next(2));
            }
            else if (pick == 10 || pick == 11)
            {
                table.DeleteRows(r, 1 + Next(2));
            }
            else if (pick == 12)
            {
                table.InsertRowsAndInherit(r, Math.Min(r + 1, table.NumRows - 1), 1);
            }
            else if (pick == 13)
            {
                table.InsertColumns(c, 30.0, 1);
            }
            else if (pick == 14)
            {
                table.DeleteColumns(c, 1);
            }
            else if (pick == 15)
            {
                table.MergeCells(range);
            }
            else if (pick == 16)
            {
                table.UnmergeCells(range);
            }
            else if (pick == 17)
            {
                table.SetTextString(r, c, "x" + Next(1000));
            }
            else if (pick == 18 || pick == 19)
            {
                table.SetRowHeight(r, 6.0 + Next(9));
            }
            else if (pick == 20)
            {
                table.SetTextHeight(r, c, 2.0 + Next(4));
            }
            else if (pick == 21)
            {
                table.SetFormula(r, c, 0, "=A1+B1");
            }
            else
            {
                table.SetColumnWidth(c, 20.0 + Next(15));
            }

            // What the table says, asked several times, between edits that change the TABLECONTENT and ones that do not.
            for (int ask = 0; ask < 1 + Next(3); ask++)
            {
                int rr = Next(table.NumRows);
                int cc = Next(table.NumColumns);

                if (rr == 0 && Next(2) == 0)
                {
                    rr = 1;
                }

                said.Append(Says(table, rr, cc));
            }
        }

        /// <summary>What a table says of a cell: whether it is linked and the range it is linked over, how many links the table has, and the kind of the cell's row and of the row after the title.</summary>
        private static string Says(DxfTable table, int rr, int cc)
        {
            DxfCellRange linked = table.GetDataLinkRange(rr, cc);
            return rr
                + ","
                + cc
                + (table.IsLinked(rr, cc) ? " L " : " - ")
                + (linked == null ? "none" : linked.ToString())
                + " "
                + table.GetDataLink().Count
                + " "
                + table.RowType(rr)
                + " "
                + table.RowType(Math.Min(1, table.NumRows - 1))
                + "\n";
        }

        /// <summary>
        /// A run of edits of a table, after each of which what the table says of six cells and of its rows is compared with what
        /// a table read anew from the drawing's text says, so that what is kept of the TABLECONTENT between two questions is
        /// asked after every kind of edit; the first differences, or nothing where there are none.
        /// </summary>
        internal static string Mismatches(string version, int seed, int edits, int rows, int columns, int form = 0)
        {
            DxfDatabase db = DxfSpeed25Scenarios.Table(version, rows, columns, form);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            long[] state = { seed * 2654435761L + 77777 };
            var said = new StringBuilder();
            var different = new StringBuilder();

            for (int i = 0; i < edits; i++)
            {
                try
                {
                    Edit(db, table, state, said);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }

                DxfTable fresh = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfTable>().Single();

                foreach (
                    int[] cell in new[]
                    {
                        new[] { 0, 0 },
                        new[] { 1, 0 },
                        new[] { 2, 2 },
                        new[] { 4, 1 },
                        new[] { table.NumRows / 2, 3 },
                        new[] { table.NumRows - 1, table.NumColumns - 1 },
                    }
                )
                {
                    int row = Math.Min(cell[0], table.NumRows - 1);
                    int column = Math.Min(cell[1], table.NumColumns - 1);
                    string kept = Says(table, row, column);
                    string read = Says(fresh, row, column);

                    if (kept != read && different.Length < 600)
                    {
                        different.Append("edit ").Append(i).Append(" kept ").Append(kept).Append(" read ").Append(read);
                    }
                }
            }

            return different.ToString();
        }

        /// <summary>A run of edits of a table, saved as it stands with what the table said.</summary>
        internal static string Run(string version, int seed, int edits, int rows, int columns, int form = 0)
        {
            DxfDatabase db = DxfSpeed25Scenarios.Table(version, rows, columns, form);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            long[] state = { seed * 2654435761L + 77777 };
            var said = new StringBuilder();

            for (int i = 0; i < edits; i++)
            {
                try
                {
                    Edit(db, table, state, said);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText() + said;
        }

        /// <summary>The first link put into a table, which reads the TABLECONTENT for the links it names and for the cell.</summary>
        internal static void FirstLink(DxfDatabase db, DxfTable table) =>
            table.SetDataLink(new DxfCellRange(10, 1, 11, 2), (DxfDataLink)db.GetObject(Parts));

        /// <summary>Twenty heights of rows set, each followed by what the table says of a cell: whether it is linked, and its range.</summary>
        internal static void HeightsThenAsked(DxfDatabase db, DxfTable table)
        {
            for (int k = 0; k < 20; k++)
            {
                table.SetRowHeight(40 + k, 9.0 + k % 7);
                table.IsLinked(50 + k, 2);
                table.GetDataLinkRange(60 + k, 1);
            }
        }

        /// <summary>The SHA-256 of what each run of 100 edits saves and says.</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1027", "AC1021" })
            {
                for (int seed = 1; seed <= 8; seed++)
                {
                    string run = Run(version, seed, 100, 9, 6);
                    lines.Add(version + " seed " + seed + " " + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run)));
                }
            }

            foreach (int form in new[] { 1, 2, 3, 4, 5, 6 })
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    // A text with an escape or a letter beyond ASCII among the custom data of a cell is in the TABLECONTENT until its row
                    // is taken out, so that those two forms are run for fewer edits.
                    string run = Run("AC1032", seed, form >= 5 ? 25 : 100, 9, 6, form);
                    lines.Add(
                        "form " + form + " seed " + seed + " " + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run))
                    );
                }
            }

            return lines;
        }
    }
}
