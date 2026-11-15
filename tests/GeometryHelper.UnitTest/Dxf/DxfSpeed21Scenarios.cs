using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Runs of edits for the work of tables9, whose saved bytes are held to what the library gave before it: the edits of
    /// <see cref="DxfSpeed18Scenarios"/> (rows and columns put in and taken out, merges across a field, resets, setters of a
    /// cell) with the heights of rows and the widths of columns set among them, which the record's heads were written whole
    /// for. Public members only, so that the same scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed21Scenarios
    {
        /// <summary>One edit: a height of a row or a width of a column set in one of four, else an edit of <see cref="DxfSpeed18Scenarios"/>.</summary>
        internal static void Edit(DxfTable table, long[] state, bool first)
        {
            long saved = state[0];
            state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
            int pick = (int)((state[0] >> 33) % 8);
            int value = (int)((state[0] >> 40) % 1000);

            if (pick == 0)
            {
                table.SetRowHeight(first ? value % table.NumRows : 1 + value % (table.NumRows - 1), 6.0 + value % 9);
            }
            else if (pick == 1)
            {
                table.SetColumnWidth(value % table.NumColumns, 20.0 + value % 15);
            }
            else
            {
                state[0] = saved;
                DxfSpeed18Scenarios.Edit(table, state, first);
            }
        }

        /// <summary>A run of edits of a table, saved as it stands; with <paramref name="reopen"/> the drawing is opened anew from its text before each edit.</summary>
        internal static string Run(
            string version,
            int seed,
            int edits,
            bool reopen,
            int rows,
            int columns,
            bool content,
            bool blocks,
            bool first
        )
        {
            DxfDatabase db = DxfSpeed18Scenarios.Table(version, rows, columns, content, blocks);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            long[] state = { seed * 2654435761L + 98765 };

            for (int i = 0; i < edits; i++)
            {
                if (reopen)
                {
                    db = DxfDatabase.Parse(db.ToText());
                    table = db.ModelSpace.OfType<DxfTable>().Single();
                }

                try
                {
                    Edit(table, state, first);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>The SHA-256 of the file each run of 120 edits saves.</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1027", "AC1021", "AC1018" })
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    foreach (bool blocks in new[] { false, true })
                    {
                        foreach (bool content in new[] { false, true })
                        {
                            string run = Run(version, seed, 120, false, 8, 6, content, blocks, true);
                            lines.Add(
                                version
                                    + " seed "
                                    + seed
                                    + (blocks ? " blocks" : " text")
                                    + (content ? " content " : " plain ")
                                    + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run))
                            );
                        }
                    }
                }
            }

            return lines;
        }

        /// <summary>The bytes the edits given allocate on a table of 49 rows and 19 columns, the first edit of it (which writes the rows AutoCAD merges on load) made before.</summary>
        internal static long Allocated(DxfDatabase db, Action<DxfTable> edits)
        {
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            table.SetTextHeight(0, 1, 2.0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            edits(table);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>Ten rounds of a height set, a row put in, a width set, cells merged, a text set and the row taken out, on 931 cells.</summary>
        internal static void SetThenEdit(DxfTable table)
        {
            for (int k = 0; k < 10; k++)
            {
                int r = 3 + k;
                int c = (k * 2) % 17;
                table.SetRowHeight(r, 9.5 + k);
                table.MergeCells(new DxfCellRange(r, c, r, c + 1));
                table.SetColumnWidth(c, 31.0 + k);
                table.InsertRows(r, 8.0, 1);
                table.SetTextString(r, c, "x" + k);
                table.DeleteRows(r, 1);
            }
        }

        /// <summary>Twenty rows put in and twenty taken out, on 931 cells.</summary>
        internal static void RowsInAndOut(DxfTable table)
        {
            for (int k = 0; k < 20; k++)
            {
                table.InsertRows(5 + (k * 3) % 20, 8.0, 1);
            }

            for (int k = 0; k < 20; k++)
            {
                table.DeleteRows(10 + k % 7, 1);
            }
        }

        /// <summary>Twenty merges across a cell that holds a field, which the merge lets go of, on 931 cells.</summary>
        internal static void MergesAcrossFields(DxfTable table)
        {
            for (int k = 0; k < 20; k++)
            {
                int r = 3 + k;
                int c = (k * 2) % 17;
                table.SetFormula(r, c + 1, 0, "=A1+B1");
                table.MergeCells(new DxfCellRange(r, c, r, c + 1));
            }
        }
    }
}
