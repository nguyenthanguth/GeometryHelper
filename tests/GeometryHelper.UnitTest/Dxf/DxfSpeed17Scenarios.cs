using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings made in code for the work of tables7, whose saved bytes are held to what the library gave before it: tables of
    /// text cells and of block cells edited by runs of the merging of cells among the setters of a cell and the structure of
    /// the table, with and without the TABLECONTENT AutoCAD 2021 writes. Public members only, so that the same scenarios run
    /// against an older build.
    /// </summary>
    internal static class DxfSpeed17Scenarios
    {
        /// <summary>
        /// A drawing with a table of text cells of rows by columns. With <paramref name="content"/> the drawing is saved and read
        /// again, so that the table is one read from a file, as AutoCAD writes it: from R2007 on it holds its TABLECONTENT.
        /// </summary>
        internal static DxfDatabase TextTable(string version, int rows, int columns, bool content)
        {
            DxfDatabase made = DxfSpeed12Scenarios.Metric(version);
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0);
            made.ModelSpace.AppendEntity(table);

            if (!content)
            {
                return made;
            }

            using (var stream = new MemoryStream())
            {
                made.Save(stream);
                return DxfDatabase.Parse(Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        /// <summary>
        /// One edit of a table, the cell and the kind picked by a generator of numbers of its own, the last cell among them: the
        /// merging and unmerging of cells in the greater part, with a few setters of a cell and of the structure. From the second
        /// row on: the first row, once its cells hold no value, is merged across by a table read anew (as AutoCAD merges it on
        /// load), which a table edited in place does not do again.
        /// </summary>
        internal static void Edit(DxfTable table, long[] state, int i)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            if (table.NumRows < 4)
            {
                table.InsertRows(table.NumRows - 1, 8.0, 2);
                return;
            }

            if (table.NumColumns < 3)
            {
                table.InsertColumns(table.NumColumns - 1, 30.0, 2);
                return;
            }

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
                Math.Min(r + Next(3), table.NumRows - 1),
                Math.Min(c + Next(4), table.NumColumns - 1)
            );
            string[] formulas = { "=A1+B1", "=Sum(A1:B2)", "=(1+2)*3", "=C2" };

            switch (Next(24))
            {
                case 0:
                case 1:
                    table.SetTextString(r, c, "t" + p);
                    break;
                case 2:
                    table.SetFormula(r, c, 0, formulas[p % formulas.Length]);
                    break;
                case 3:
                case 4:
                case 5:
                    table.SetTextHeight(r, c, 1 + p % 5);
                    break;
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13:
                    table.MergeCells(range);
                    break;
                case 14:
                case 15:
                case 16:
                    table.UnmergeCells(range);
                    break;
                case 17:
                    table.ResetValue(r, c);
                    break;
                case 18:
                    table.SetValue(r, c, r * 10 + c + p / 7.0);
                    break;
                case 19:
                    table.SetCellType(r, c, DxfTableCellType.TextCell);
                    break;
                case 20:
                    table.InsertRows(r, 8.0, 1);
                    break;
                case 21:
                    table.InsertColumns(c, 30.0, 1);
                    break;
                case 22:
                    table.DeleteRows(r, 1);
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

        /// <summary>
        /// A run of edits of a table, saved as it stands. <paramref name="reopen"/> opens the drawing anew from its text before
        /// each edit, so that the table's record is not one the table wrote, and each edit reads the whole of it.
        /// </summary>
        internal static string Run(
            string version,
            int seed,
            int edits,
            bool reopen,
            int rows,
            int columns,
            bool content,
            bool blocks
        )
        {
            DxfDatabase db = blocks
                ? DxfSpeed16Scenarios.WithBlockCells(version, rows, columns)
                : TextTable(version, rows, columns, content);
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
                    Edit(table, state, i);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>The SHA-256 of the file each run of edits saves, in every version a table is written in from R2004 on, of text cells and of block cells, with and without a TABLECONTENT.</summary>
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
                            if (blocks && content)
                            {
                                continue;
                            }

                            string run = Run(version, seed, 120, false, 8, 6, content, blocks);
                            lines.Add(
                                version
                                    + " seed "
                                    + seed
                                    + (
                                        blocks ? " blocks "
                                        : content ? " content "
                                        : " text "
                                    )
                                    + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run))
                            );
                        }
                    }
                }
            }

            return lines;
        }
    }
}
