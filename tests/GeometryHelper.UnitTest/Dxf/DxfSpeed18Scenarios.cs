using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings made in code for the work of tables8, whose saved bytes are held to what the library gave before it: tables of
    /// text cells and of block cells, with and without the TABLECONTENT AutoCAD 2021 writes, edited by runs of rows and
    /// columns put in and taken out, merges (of cells holding a field among them), resets of a cell's value and the setters of
    /// a cell. Public members only, so that the same scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed18Scenarios
    {
        /// <summary>
        /// A drawing with a table of rows by columns, saved and read again where <paramref name="content"/> asks (so that it
        /// holds the TABLECONTENT AutoCAD writes), whose cells where <c>(r + c) % 2 == 0</c> are block cells of a block BOLT of
        /// one attribute definition where <paramref name="blocks"/> asks, and text cells else. The library makes no block cell,
        /// so the text of the record is changed before it is read; the TABLECONTENT, where there is one, is left as it was.
        /// </summary>
        internal static DxfDatabase Table(string version, int rows, int columns, bool content, bool blocks)
        {
            if (!blocks)
            {
                return DxfSpeed17Scenarios.TextTable(version, rows, columns, content);
            }

            DxfDatabase made = DxfSpeed12Scenarios.Metric(version);
            var bolt = new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(0, 0, 0) };
            made.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0)));
            var size = new DxfAttributeDefinition(new GeoPoint3(22, 0, 0), "M12", "SIZE", "Size", null);
            bolt.AppendEntity(size);
            made.ModelSpace.AppendEntity(new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0));
            string text = made.ToText();

            if (content)
            {
                using (var stream = new MemoryStream())
                {
                    made.Save(stream);
                    text = Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            Match start = Regex.Match(text, @"\r?\n  0\r?\nACAD_TABLE\r?\n");
            int from = start.Index + start.Length;
            int to = Regex.Match(text.Substring(from), @"\r?\n  0\r?\n").Index + from;
            string record = text.Substring(from, to - from);
            int index = 0;
            string blocked = Regex.Replace(record, @"171(\r?\n)\s*1(\r?\n)", match =>
            {
                int cell = index++;

                if ((cell / columns + cell % columns) % 2 != 0)
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
                    + cell / columns
                    + "."
                    + cell % columns
                    + nl;
            });
            return DxfDatabase.Parse(text.Substring(0, from) + blocked + text.Substring(to));
        }

        /// <summary>
        /// One edit of a table, the cell and the kind picked by a generator of numbers of its own: rows and columns put in and
        /// taken out in the greater part (the plain ones and those that take the look of another), merges and unmerges, among
        /// them a merge across a cell that holds a field, resets of a cell's value, and a few setters of a cell. The table is
        /// kept between 5 and 14 rows and 4 and 9 columns. With <paramref name="first"/> false the first row and column are left
        /// alone: a table read anew merges the first row across when its cells hold no value, which a table edited in place does
        /// not do again.
        /// </summary>
        internal static void Edit(DxfTable table, long[] state, bool first)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            int rows = table.NumRows;
            int columns = table.NumColumns;
            int skip = first ? 0 : 1;

            if (rows < 5)
            {
                table.InsertRows(rows - 1, 8.0, 2);
                return;
            }

            if (columns < 4)
            {
                table.InsertColumns(columns - 1, 30.0, 2);
                return;
            }

            int r = skip + Next(rows - skip);
            int c = Next(columns);

            if (Next(4) == 0)
            {
                r = rows - 1;
                c = columns - 1;
            }

            int p = Next(1000);
            var range = new DxfCellRange(r, c, Math.Min(r + Next(3), rows - 1), Math.Min(c + Next(4), columns - 1));
            string[] formulas = { "=A1+B1", "=Sum(A1:B2)", "=(1+2)*3", "=C2" };
            int kind = Next(34);

            if (rows > 14 && kind >= 14 && kind < 26)
            {
                kind = 30;
            }

            if (columns > 9 && kind >= 14 && kind < 26)
            {
                kind = 31;
            }

            switch (kind)
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
                    table.SetTextHeight(r, c, 1 + p % 5);
                    break;
                case 5:
                case 6:
                case 7:
                case 8:
                    table.MergeCells(range);
                    break;
                case 9:
                case 10:
                    table.UnmergeCells(range);
                    break;
                case 11:
                case 12:
                    table.ResetValue(r, c);
                    break;
                case 13:
                    table.SetValue(r, c, r * 10 + c + p / 7.0);
                    break;
                case 14:
                case 15:
                    table.InsertRows(r, 8.0, 1 + p % 2);
                    break;
                case 16:
                    table.InsertRowsAndInherit(r, Next(rows), 1);
                    break;
                case 17:
                case 18:
                    table.InsertColumns(c, 30.0, 1 + p % 2);
                    break;
                case 19:
                    table.InsertColumnsAndInherit(c, Next(columns), 1);
                    break;
                case 20:
                case 21:
                    table.DeleteRows(r, 1 + p % 2);
                    break;
                case 22:
                case 23:
                    table.DeleteColumns(c, 1 + p % 2);
                    break;
                case 24:
                    table.InsertRows(rows, 8.0, 1);
                    break;
                case 25:
                    table.InsertColumns(columns, 30.0, 1);
                    break;
                case 26:
                case 27:
                case 28:
                    // A merge across a cell that holds a field, which the merge lets go of.
                    table.SetFormula(r, Math.Min(c + 1, columns - 1), 0, formulas[p % formulas.Length]);
                    table.MergeCells(
                        new DxfCellRange(r, c, Math.Min(r + p % 2, rows - 1), Math.Min(c + 1, columns - 1))
                    );
                    break;
                case 29:
                    table.SetCellType(r, c, DxfTableCellType.TextCell);
                    break;
                case 30:
                    table.DeleteRows(r, 1);
                    break;
                case 31:
                    table.DeleteColumns(c, 1);
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
            bool blocks,
            bool first
        )
        {
            DxfDatabase db = Table(version, rows, columns, content, blocks);
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
                    Edit(table, state, first);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>
        /// A run of edits of the table AutoCAD 2021 wrote linked to data (a DATALINK named by its cell (1, 0), and a field in that
        /// cell's content where <paramref name="formula"/> asks), as <see cref="Run"/> runs them, from the text of a drawing of R2018
        /// or R2007.
        /// </summary>
        internal static string RunLinked(
            string drawing,
            int rows,
            bool formula,
            int seed,
            int edits,
            bool reopen,
            bool first
        )
        {
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(drawing, rows, formula));
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
                    Edit(table, state, first);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>The SHA-256 of the file each run of 60 edits of the table AutoCAD wrote linked saves, of R2018 and R2007, of 3 and 6 rows, with and without a field in the linked cell.</summary>
        internal static List<string> LinkedRuns()
        {
            var lines = new List<string>();

            foreach (bool r2018 in new[] { true, false })
            {
                string drawing = r2018 ? DxfDimensionDrawings.R2018 : DxfAutoCadChecksD16Tests.R2007;

                foreach (int rows in new[] { 3, 6 })
                {
                    foreach (bool formula in new[] { false, true })
                    {
                        for (int seed = 1; seed <= 4; seed++)
                        {
                            string run = RunLinked(drawing, rows, formula, seed, 60, false, true);
                            lines.Add(
                                (r2018 ? "R2018" : "R2007")
                                    + " rows "
                                    + rows
                                    + (formula ? " field" : " plain")
                                    + " seed "
                                    + seed
                                    + " "
                                    + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run))
                            );
                        }
                    }
                }
            }

            return lines;
        }

        /// <summary>The SHA-256 of the file each run of 120 edits saves, in every version a table is written in from R2004 on, of text cells and of block cells, with and without a TABLECONTENT.</summary>
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
    }
}
