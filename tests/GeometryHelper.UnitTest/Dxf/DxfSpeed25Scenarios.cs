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
    /// Runs of edits for the work of tables11, whose saved bytes are held to what the library gave before it: tables read
    /// from a file with their TABLECONTENT and two DATALINKs of the drawing (Parts and Prices), edited by data links put in and
    /// taken away among rows and columns put in and taken out, merges, texts and heights, with what the table says of its links
    /// asked between the edits. Public members only, so that the same scenarios run against an older build.
    /// </summary>
    internal static class DxfSpeed25Scenarios
    {
        private const string Parts = "FFF01";
        private const string Prices = "FFF03";

        // The forms of the TABLECONTENT a run starts from, by the number Table takes: as the file writes it, a comment among its groups, extended data after, the file with carriage returns, its codes not padded, a text with an escape in it, a text with a letter beyond ASCII.
        private static readonly string[] Forms =
        {
            "plain",
            "comment",
            "extended",
            "crlf",
            "unpadded",
            "escape",
            "letter",
        };

        /// <summary>
        /// A drawing with a table of rows by columns of text cells, saved and read again (so that the table is one read from a
        /// file, as AutoCAD writes it, with its TABLECONTENT), and the data links Parts and Prices in its ACAD_DATALINK, each
        /// owning an XRECORD, in the layout the tests of the data links guess.
        /// </summary>
        internal static DxfDatabase Table(string version, int rows, int columns, int form = 0)
        {
            DxfDatabase made = DxfSpeed12Scenarios.Metric(version);
            made.ModelSpace.AppendEntity(new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0));
            string text;

            using (var stream = new MemoryStream())
            {
                made.Save(stream);
                text = Encoding.UTF8.GetString(stream.ToArray());
            }

            string root = made.NamedObjectsDictionary.Handle;
            Match dictionary = Regex.Match(text, @"\r?\n  0\r?\nDICTIONARY\r?\n  5\r?\n" + root + @"\r?\n");
            Match first = Regex.Match(text.Substring(dictionary.Index + dictionary.Length), @"\r?\n  3\r?\n");
            int entry = dictionary.Index + dictionary.Length + first.Index + 1;
            text = text.Substring(0, entry) + "  3\nACAD_DATALINK\n350\nFFF00\n" + text.Substring(entry);
            text = Regex.Replace(text, @"(\$HANDSEED\r?\n\s*5\r?\n)[0-9A-Fa-f]+", "${1}FFF10");
            int at = text.LastIndexOf("  0\nENDSEC", StringComparison.Ordinal);
            string objects =
                "  0\nDICTIONARY\n  5\nFFF00\n330\n"
                + root
                + "\n100\nAcDbDictionary\n281\n1\n  3\nParts\n350\n"
                + Parts
                + "\n  3\nPrices\n350\n"
                + Prices
                + "\n"
                + Link(Parts, "FFF02", "parts.xlsx", "A1:A2")
                + Link(Prices, "FFF04", "prices.xlsx", "B1:C9");
            text = text.Substring(0, at) + objects + text.Substring(at);

            // The TABLECONTENT not as the file writes its groups (a comment between two of them), or with extended data after.
            int content = text.IndexOf("\n  0\nTABLECONTENT\n", StringComparison.Ordinal);

            if (form == 1 && content >= 0)
            {
                int linked = text.IndexOf("\n100\nAcDbLinkedTableData\n", content, StringComparison.Ordinal);
                int comment = linked + "\n100\nAcDbLinkedTableData\n".Length;
                text = text.Substring(0, comment) + "999\nnote\n" + text.Substring(comment);
            }
            else if (form == 2 && content >= 0)
            {
                int next = text.IndexOf("\n  0\n", content + 1, StringComparison.Ordinal) + 1;
                text = text.Substring(0, next) + "1001\nGHTEST\n1000\nnote\n" + text.Substring(next);
            }
            else if (form == 3 && content >= 0)
            {
                text = text.Replace("\n", "\r\n");
            }
            else if (form == 4 && content >= 0)
            {
                // The code lines of the TABLECONTENT with no spaces before them, where the file pads them to three places.
                int begin = content + "\n  0\nTABLECONTENT\n".Length;
                int finish = text.IndexOf("\n  0\n", begin, StringComparison.Ordinal) + 1;
                string[] lines = text.Substring(begin, finish - begin).Split('\n');

                for (int k = 0; k < lines.Length; k += 2)
                {
                    lines[k] = lines[k].Trim();
                }

                text = text.Substring(0, begin) + string.Join("\n", lines) + text.Substring(finish);
            }
            else if ((form == 5 || form == 6) && content >= 0)
            {
                // A text among the custom data of the twentieth cell with an escape in it, or with the letter itself.
                string cell = "309\nDATAMAP_END\n";
                int begin = content;

                for (int k = 0; k < 20; k++)
                {
                    begin = text.IndexOf(cell, begin + 1, StringComparison.Ordinal);
                }

                string note = form == 5 ? "  1\nCELLTEXT \\U+00E9 \\P x\n" : "  1\nCELLTEXT é x\n";
                text = text.Substring(0, begin) + note + text.Substring(begin);
            }

            return DxfDatabase.Parse(text);
        }

        private static string Link(string handle, string record, string file, string range) =>
            "  0\nDATALINK\n  5\n"
            + handle
            + "\n102\n{ACAD_REACTORS\n330\nFFF00\n102\n}\n330\nFFF00\n100\nAcDbDataLink\n  1\nAcExcel\n300\nBill of parts\n301\nData source file: "
            + file
            + "\n302\n"
            + file
            + "!Sheet1!"
            + range
            + "\n 90\n2\n 91\n1179649\n 92\n1\n170\n2026\n171\n10\n172\n20\n173\n13\n174\n31\n175\n10\n176\n484\n177\n1\n 93\n0\n304\nUpdated\n"
            + " 94\n0\n305\nCUSTOMDATA\n  1\nDATAMAP_BEGIN\n309\nDATAMAP_END\n360\n"
            + record
            + "\n  0\nXRECORD\n  5\n"
            + record
            + "\n330\n"
            + handle
            + "\n100\nAcDbXrecord\n280\n1\n  1\ncached\n";

        /// <summary>
        /// One edit of a table: a data link put in or taken away, in the greater part, among the rows, columns, merges, texts
        /// and heights, each cell and kind picked by a generator of numbers of its own; what the table says of its links is
        /// appended to <paramref name="said"/> now and then.
        /// </summary>
        internal static void Edit(DxfDatabase db, DxfTable table, long[] state, StringBuilder said)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            if (table.NumRows < 5)
            {
                table.InsertRows(table.NumRows - 1, 8.0, 2);
                return;
            }

            if (table.NumColumns < 4)
            {
                table.InsertColumns(table.NumColumns - 1, 30.0, 2);
                return;
            }

            int r = 1 + Next(table.NumRows - 1);
            int c = Next(table.NumColumns);
            var range = new DxfCellRange(
                r,
                c,
                Math.Min(r + Next(3), table.NumRows - 1),
                Math.Min(c + Next(3), table.NumColumns - 1)
            );
            var link = (DxfDataLink)db.GetObject(Next(2) == 0 ? Parts : Prices);
            int pick = Next(21);

            if (pick < 5)
            {
                table.SetDataLink(range, link);
            }
            else if (pick < 6)
            {
                table.SetDataLink(r, c, link);
            }
            else if (pick < 8)
            {
                table.RemoveDataLink();
            }
            else if (pick < 11)
            {
                table.RemoveDataLink(r, c);
            }
            else if (pick == 11)
            {
                table.InsertRows(r, 8.0, 1 + Next(2));
            }
            else if (pick == 12)
            {
                table.DeleteRows(r, 1);
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
            else if (pick == 18)
            {
                table.SetRowHeight(r, 6.0 + Next(9));
            }
            else if (pick == 19)
            {
                table.SetFormula(r, c, 0, "=A1+B1");
            }
            else
            {
                table.SetColumnWidth(c, 20.0 + Next(15));
            }

            if (Next(3) == 0)
            {
                int rr = 1 + Next(table.NumRows - 1);
                int cc = Next(table.NumColumns);
                DxfCellRange linked = table.GetDataLinkRange(rr, cc);
                said.Append(rr)
                    .Append(',')
                    .Append(cc)
                    .Append(table.IsLinked(rr, cc) ? " L " : " - ")
                    .Append(linked == null ? "none" : linked.ToString())
                    .Append(' ')
                    .Append(table.GetDataLink().Count)
                    .Append('\n');
            }
        }

        /// <summary>A run of edits of a table, saved as it stands with what the table said of its links; with <paramref name="reopen"/> the drawing is opened anew from its text before each edit.</summary>
        internal static string Run(
            string version,
            int seed,
            int edits,
            bool reopen,
            int rows,
            int columns,
            int form = 0
        )
        {
            DxfDatabase db = Table(version, rows, columns, form);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            long[] state = { seed * 2654435761L + 31337 };
            var said = new StringBuilder();

            for (int i = 0; i < edits; i++)
            {
                if (reopen)
                {
                    db = DxfDatabase.Parse(db.ToText());
                    table = db.ModelSpace.OfType<DxfTable>().Single();
                }

                try
                {
                    Edit(db, table, state, said);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText() + said;
        }

        /// <summary>The SHA-256 of what each run of 120 edits saves and says.</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1027", "AC1021" })
            {
                for (int seed = 1; seed <= 8; seed++)
                {
                    string run = Run(version, seed, 120, false, 8, 6);
                    lines.Add(version + " seed " + seed + " " + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run)));
                }
            }

            foreach (int form in new[] { 1, 2, 3, 4, 5, 6 })
            {
                foreach (string version in new[] { "AC1032", "AC1021" })
                {
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        string run = Run(version, seed, 120, false, 8, 6, form);
                        lines.Add(
                            version
                                + " "
                                + Forms[form]
                                + " seed "
                                + seed
                                + " "
                                + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run))
                        );
                    }
                }
            }

            return lines;
        }

        /// <summary>
        /// The bytes the edits given allocate on a table of 300 rows by 6 columns (1 800 cells) read from a file, the first edit of
        /// it (which writes the rows AutoCAD merges on load) and a first link put in made before, so that what is measured is the
        /// edit and not the first reading of the record.
        /// </summary>
        internal static long Allocated(Action<DxfDatabase, DxfTable> edits, bool warm = true)
        {
            DxfDatabase db = Table("AC1032", 300, 6);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            table.SetTextHeight(0, 1, 2.0);

            if (warm)
            {
                table.SetDataLink(new DxfCellRange(10, 1, 11, 2), (DxfDataLink)db.GetObject(Parts));
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            edits(db, table);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>Ten links put in and ten taken away again, one cell each, among others.</summary>
        internal static void LinksPutInAndTakenAway(DxfDatabase db, DxfTable table)
        {
            for (int k = 0; k < 10; k++)
            {
                table.SetDataLink(
                    new DxfCellRange(40 + k, 1 + k % 4, 40 + k, 1 + k % 4),
                    (DxfDataLink)db.GetObject(Prices)
                );
                table.RemoveDataLink(40 + k, 1 + k % 4);
            }
        }

        /// <summary>Twenty heights of rows set, one after another.</summary>
        internal static void HeightsSet(DxfDatabase db, DxfTable table)
        {
            for (int k = 0; k < 20; k++)
            {
                table.SetRowHeight(40 + k, 9.0 + k % 7);
            }
        }

        /// <summary>The first cells merged.</summary>
        internal static void FirstMerge(DxfDatabase db, DxfTable table) =>
            table.MergeCells(new DxfCellRange(20, 1, 20, 2));
    }
}
