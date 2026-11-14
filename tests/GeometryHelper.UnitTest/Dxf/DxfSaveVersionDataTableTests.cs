using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The round trip of the cells of a table going from R2007 or later down to R2000 or R2004 (ACAD_ROUNDTRIP_PRE2007_TABLE): the
    /// data table AutoCAD 2021 keeps beside the table, one row for each cell holding a checksum of its text, the mask of what it
    /// overrides and an XRECORD with its value. The forms are those AutoCAD 2021 wrote when it saved the same drawings as R2000
    /// and R2004 (a table of 3 rows by 2 columns with an override cell and a block cell, and tables of 3 by 3 with one border
    /// on one cell), and the cells of a table of the older forms going up again as AutoCAD 2021 writes them when it has the data
    /// table to read.
    /// </summary>
    public class DxfSaveVersionDataTableTests
    {
        #region Helpers

        private static string G(int code, string value) =>
            code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static byte[] Saved(string text, string version, out IReadOnlyList<string> said)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                said = db.Save(stream, version);
                return stream.ToArray();
            }
        }

        private static byte[] Saved(string text, string version) => Saved(text, version, out _);

        private static string Text(byte[] dxf) => Encoding.GetEncoding(28591).GetString(dxf);

        private static DwgAnswerKey.Record Record(byte[] dxf, string handle) =>
            DwgAnswerKey.Parse(dxf).Single(r => r.Key == "X:" + handle);

        private static List<DwgAnswerKey.Record> Tables(byte[] dxf) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Section == "ENTITIES" && r.Type == "ACAD_TABLE").ToList();

        private static string Show(IEnumerable<KeyValuePair<int, string>> groups) =>
            string.Join(" ", groups.Select(g => g.Key + "=" + g.Value.Trim()));

        /// <summary>The value of the first group of the code after the first group of the code <paramref name="after"/> with the value.</summary>
        private static string Pointer(DwgAnswerKey.Record record, string name, int code)
        {
            List<KeyValuePair<int, string>> groups = record.Groups;
            int at = groups.FindIndex(g => g.Value.Trim() == name);
            Assert.True(at >= 0, name + " is not in " + record.Key);
            return groups.Skip(at).First(g => g.Key == code).Value.Trim();
        }

        /// <summary>The data table of a table, with the XRECORD of its round trip that holds it.</summary>
        private static void RoundTripOf(
            byte[] dxf,
            DwgAnswerKey.Record table,
            out DwgAnswerKey.Record xrecord,
            out DwgAnswerKey.Record data
        )
        {
            DwgAnswerKey.Record dictionary = Record(dxf, Pointer(table, "{ACAD_XDICTIONARY", 360));
            xrecord = Record(dxf, Pointer(dictionary, "ACAD_XREC_ROUNDTRIP", 360));
            data = Record(dxf, Pointer(xrecord, "ACAD_ROUNDTRIP_PRE2007_TABLECELL", 360));
        }

        /// <summary>The columns of a data table: its groups but its handles, its owner and the pointers to its cells.</summary>
        private static string Columns(DwgAnswerKey.Record data) =>
            Show(data.Groups.Where(g => g.Key != 5 && g.Key != 330 && g.Key != 360));

        /// <summary>The XRECORDs of the cells of a data table, each as its groups but its handle and owner.</summary>
        private static List<string> Values(byte[] dxf, DwgAnswerKey.Record data) =>
            data
                .Groups.Where(g => g.Key == 360)
                .Select(g => Record(dxf, g.Value.Trim()))
                .Select(r => Show(r.Groups.Where(g => g.Key != 5 && g.Key != 330)))
                .ToList();

        private static string Unset(string value) =>
            value.Replace("93=6 90=4 1= 94=0", "93=7 90=0 94=0").Replace("93=4 90=4", "93=6 90=4");

        private static string Zeros(int count, int code) => string.Join(" ", Enumerable.Repeat(code + "=0", count));

        private static string Column(int[] values) => string.Join(" ", values.Select(v => "93=" + v));

        #endregion

        #region A table with overrides and a block cell

        [Theory]
        [InlineData("AC1015", "ACDBDATATABLE", "ACDBDATATABLE")]
        [InlineData("AC1018", "DATATABLE", "AcDbDataTable")]
        public void ATableGoingToR2000OrR2004_HasTheDataTableAutoCadAddsOfItsCells(
            string version,
            string type,
            string subclass
        )
        {
            byte[] saved = Saved(DxfDimensionDrawings.R2018, version, out IReadOnlyList<string> said);

            Assert.Empty(said);
            DwgAnswerKey.Record table = Assert.Single(Tables(saved));
            RoundTripOf(saved, table, out DwgAnswerKey.Record xrecord, out DwgAnswerKey.Record data);

            // The data table of AutoCAD's own R2000 of this table: a checksum of each text (the sum of its characters, each
            // times its place from 1), the mask of the cell Mark that has an alignment of its own, 0 in the column after it,
            // and the values.
            Assert.Equal(type, data.Type);
            Assert.Equal(
                subclass
                    + " 70=2 90=4 91=6 1= 92=2 2= 40=1655.0 40=0.0 40=1041.0 40=676.0 40=503.0 40=0.0 92=1 2= "
                    + Column(new[] { 0, 0, 1, 0, 0, 0 })
                    + " 92=1 2= "
                    + Column(new[] { 0, 0, 0, 0, 0, 0 })
                    + " 92=6 2=",
                Columns(data).Replace("100=", string.Empty)
            );
            Assert.Equal(xrecord.Key.Substring(2), data.Owner);
            Assert.Equal(table.Key.Substring(2), Record(saved, Pointer(table, "{ACAD_XDICTIONARY", 360)).Owner);
            Assert.Equal(
                "90=3 91=2",
                Show(xrecord.Groups.SkipWhile(g => g.Value != "ACAD_ROUNDTRIP_PRE2007_TABLE").Skip(1).Take(2))
            );
            Assert.Equal(
                new[]
                {
                    "100=AcDbXrecord 280=1 93=6 90=4 1=Parts 94=0 300= 302=Parts",
                    "100=AcDbXrecord 280=1 93=6 90=4 1= 94=0 300= 302=",
                    "100=AcDbXrecord 280=1 93=6 90=4 1=Mark 94=0 300= 302=Mark",
                    "100=AcDbXrecord 280=1 93=6 90=4 1=Qty 94=0 300= 302=Qty",
                    "100=AcDbXrecord 280=1 93=6 90=4 1=B-12 94=0 300= 302=B-12",
                    "100=AcDbXrecord 280=1 93=3 90=0 94=0 300= 302=",
                },
                Values(saved, data)
            );
            Assert.All(data.Groups.Where(g => g.Key == 360), g =>
                Assert.Equal(data.Key.Substring(2), Record(saved, g.Value.Trim()).Owner)
            );
        }

        [Fact]
        public void TheDataTableOfATable_HasItsClass_AsAutoCadListsIt()
        {
            byte[] as2000 = Saved(DxfDimensionDrawings.R2018, "AC1015");
            byte[] as2004 = Saved(DxfDimensionDrawings.R2018, "AC1018");

            DwgAnswerKey.Record old = DwgAnswerKey.Parse(as2000).Single(r => r.Key == "C:ACDBDATATABLE");
            DwgAnswerKey.Record current = DwgAnswerKey.Parse(as2004).Single(r => r.Key == "C:DATATABLE");

            // AutoCAD's: "1 ACDBDATATABLE 2 AcDbDataTable 3 ObjectDBX Classes 90 0 280 0 281 0" and, from R2004, 91 the count.
            Assert.Equal("1=ACDBDATATABLE 2=AcDbDataTable 3=ObjectDBX Classes 90=0 280=0 281=0", Show(old.Groups));
            Assert.Equal("1=DATATABLE 2=AcDbDataTable 3=ObjectDBX Classes 90=0 91=1 280=0 281=0", Show(current.Groups));
        }

        // The cells of the table F6 of the drawing of R2018 that hold Qty and B-12, given the flags and overrides the cells of the
        // old form hold; the other cells are those of the drawing (Mark has an alignment, a text style and a height).
        private static string Cell(string text, string cell, string flags, string overrides) =>
            text.Replace(
                G(91, "0")
                    + G(178, "0")
                    + G(145, "0.0")
                    + G(92, "0")
                    + G(301, "CELL_VALUE")
                    + G(93, "6")
                    + G(90, "4")
                    + G(1, cell),
                G(91, flags)
                    + G(178, "0")
                    + G(145, "0.0")
                    + string.Concat(
                        overrides
                            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(g => G(int.Parse(g.Split('=')[0], CultureInfo.InvariantCulture), g.Split('=')[1]))
                    )
                    + G(92, "0")
                    + G(301, "CELL_VALUE")
                    + G(93, "6")
                    + G(90, "4")
                    + G(1, cell)
            );

        [Theory]
        // Qty with an alignment of its own (flag 1) and B-12 with a height (flag 0x20): the alignment is in the mask, the height not.
        [InlineData("262145", "170=5", "262176", "140=3.5", "0 0 1 1 0 0")]
        // Qty with a text style (0x10) and B-12 with a background colour and its fill (0x2 and 0x4): the colours are in it.
        [InlineData("262160", "7=GH_TXT", "262150", "63=1 283=1", "0 0 1 0 6 0")]
        // Qty with a content colour (0x8): not in the mask.
        [InlineData("262152", "64=3", "262144", "", "0 0 1 0 0 0")]
        public void TheMaskOfACell_HoldsItsAlignmentAndItsBackground_AndNotItsTextStyleHeightOrColour(
            string qtyFlags,
            string qty,
            string bFlags,
            string b,
            string mask
        )
        {
            string text = Cell(Cell(DxfDimensionDrawings.R2018, "Qty", qtyFlags, qty), "B-12", bFlags, b);
            byte[] saved = Saved(text, "AC1015");
            DwgAnswerKey.Record table = Assert.Single(Tables(saved));
            RoundTripOf(saved, table, out _, out DwgAnswerKey.Record data);

            Assert.Contains(
                " 92=1 2= " + string.Join(" ", mask.Split(' ').Select(v => "93=" + v)) + " 92=1 2=",
                Columns(data),
                StringComparison.Ordinal
            );
        }

        #endregion

        #region The borders

        /// <summary>A cell of a table in the form of R2007 and later: its flags, its overrides, and its text (null for a cell that holds none).</summary>
        private static string NewCell(string text, int flags, string overrides = "")
        {
            var cell = new StringBuilder();
            cell.Append(G(171, "1"))
                .Append(G(172, "0"))
                .Append(G(173, "0"))
                .Append(G(174, "0"))
                .Append(G(175, "1"))
                .Append(G(176, "1"))
                .Append(G(91, flags.ToString(CultureInfo.InvariantCulture)))
                .Append(G(178, "0"))
                .Append(G(145, "0.0"))
                .Append(
                    string.Concat(
                        overrides
                            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(g => G(int.Parse(g.Split('=')[0], CultureInfo.InvariantCulture), g.Split('=')[1]))
                    )
                )
                .Append(G(92, "0"))
                .Append(G(301, "CELL_VALUE"));

            if (text == null)
            {
                cell.Append(G(93, "3")).Append(G(90, "0")).Append(G(94, "0")).Append(G(300, string.Empty));
                cell.Append(G(302, string.Empty));
            }
            else if (text.Length > 250)
            {
                // AutoCAD cuts a text of more than 250 characters: the first 250 in 2, the rest in 1, and the value again in 303 and 302.
                cell.Append(G(93, "6")).Append(G(90, "4")).Append(G(2, text.Substring(0, 250)));
                cell.Append(G(1, text.Substring(250))).Append(G(94, "0")).Append(G(300, string.Empty));
                cell.Append(G(303, text.Substring(0, 250))).Append(G(302, text.Substring(250)));
            }
            else
            {
                cell.Append(G(93, "6")).Append(G(90, "4")).Append(G(1, text)).Append(G(94, "0"));
                cell.Append(G(300, string.Empty)).Append(G(302, text));
            }

            return cell.Append(G(304, "ACVALUE_END")).ToString();
        }

        /// <summary>A cell of the form of R2004 and before: its flags, its text and its overrides.</summary>
        private static string OldCell(string text, int flags, string overrides = "")
        {
            var cell = new StringBuilder();
            cell.Append(G(171, "1"))
                .Append(G(172, "0"))
                .Append(G(173, "0"))
                .Append(G(174, "0"))
                .Append(G(175, "1"))
                .Append(G(176, "1"))
                .Append(G(177, flags.ToString(CultureInfo.InvariantCulture)))
                .Append(G(178, "0"))
                .Append(G(145, "0.0"));

            if (text.Length > 250)
            {
                cell.Append(G(3, text.Substring(0, 250))).Append(G(1, text.Substring(250)));
            }
            else
            {
                cell.Append(G(1, text));
            }

            return cell.Append(
                    string.Concat(
                        overrides
                            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(g => G(int.Parse(g.Split('=')[0], CultureInfo.InvariantCulture), g.Split('=')[1]))
                    )
                )
                .ToString();
        }

        /// <summary>A drawing of the version with one table of 3 rows by 3 columns made of the cells, in the order of its rows.</summary>
        private static string Table3(string version, IEnumerable<string> cells)
        {
            var text = new StringBuilder();
            text.Append(G(0, "SECTION")).Append(G(2, "HEADER")).Append(G(9, "$ACADVER")).Append(G(1, version));
            text.Append(G(9, "$HANDSEED")).Append(G(5, "1000")).Append(G(0, "ENDSEC"));
            text.Append(G(0, "SECTION")).Append(G(2, "CLASSES")).Append(G(0, "ENDSEC"));
            text.Append(G(0, "SECTION")).Append(G(2, "ENTITIES"));
            text.Append(G(0, "ACAD_TABLE")).Append(G(5, "7D")).Append(G(330, "70")).Append(G(100, "AcDbEntity"));
            text.Append(G(8, "0")).Append(G(100, "AcDbBlockReference")).Append(G(2, "*T2"));
            text.Append(G(10, "0.0")).Append(G(20, "0.0")).Append(G(30, "0.0")).Append(G(100, "AcDbTable"));
            text.Append(G(280, "0")).Append(G(342, "65")).Append(G(343, "7F"));
            text.Append(G(11, "1.0")).Append(G(21, "0.0")).Append(G(31, "0.0")).Append(G(90, "22"));
            text.Append(G(91, "3")).Append(G(92, "3")).Append(G(93, "0")).Append(G(94, "0")).Append(G(95, "0"));
            text.Append(G(96, "0"));

            for (int i = 0; i < 3; i++)
            {
                text.Append(G(141, "8.0"));
            }

            for (int i = 0; i < 3; i++)
            {
                text.Append(G(142, "20.0"));
            }

            foreach (string cell in cells)
            {
                text.Append(cell);
            }

            text.Append(G(0, "ENDSEC")).Append(G(0, "SECTION")).Append(G(2, "OBJECTS"));
            text.Append(G(0, "DICTIONARY")).Append(G(5, "C")).Append(G(330, "0")).Append(G(100, "AcDbDictionary"));
            text.Append(G(281, "1")).Append(G(0, "ENDSEC")).Append(G(0, "EOF"));
            return text.ToString();
        }

        /// <summary>The nine cells of a 3 by 3 table: the cell (1, 1) holds x with the flags and overrides, the cell given with its own, the others none.</summary>
        private static List<string> Nine(
            int flags,
            string overrides,
            int other = -1,
            int otherFlags = 0,
            string otherOverrides = ""
        )
        {
            var cells = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                cells.Add(
                    i == 4 ? NewCell("x", flags, overrides)
                    : i == other ? NewCell(null, otherFlags, otherOverrides)
                    : NewCell(null, 0)
                );
            }

            return cells;
        }

        [Theory]
        // The 3 by 3 tables AutoCAD 2021 wrote with one border changed, as its R2018 and its R2000: the cell and the edge it shares
        // (a top edge is the bottom of the cell above, and the other three likewise), with the flags of a side in the mask: top
        // 0x4440, left 0x22200, right 0x8880 and bottom 0x11100.
        // The top of the cell (1, 1): 0x4440 for it, the bottom of the cell above, 0x11100.
        [InlineData(279616, "279=-2 69=1", -1, 0, "", "0 69888 0 0 17472 0 0 0 0")]
        // The bottom edge, which AutoCAD keeps as the top of the cell below it (2, 1).
        [InlineData(262144, "", 7, 17472, "279=-2 69=1", "0 0 0 0 69888 0 0 17472 0")]
        // The left of (1, 1): 0x22200 for it and, for the cell to its left, the right side 0x8880.
        [InlineData(401920, "278=-2 68=1", -1, 0, "", "0 0 0 34944 139776 0 0 0 0")]
        // The right edge, kept as the left of the cell to its right (1, 2).
        [InlineData(262144, "", 5, 139776, "278=-2 68=1", "0 0 0 0 34944 139776 0 0 0")]
        public void TheMaskOfACellWithABorder_HoldsTheSideItIsOn_AndTheOppositeSideOfItsNeighbour(
            int flags,
            string overrides,
            int other,
            int otherFlags,
            string otherOverrides,
            string mask
        )
        {
            byte[] saved = Saved(Table3("AC1032", Nine(flags, overrides, other, otherFlags, otherOverrides)), "AC1015");
            DwgAnswerKey.Record table = Assert.Single(Tables(saved));
            RoundTripOf(saved, table, out _, out DwgAnswerKey.Record data);

            Assert.Contains(
                " 92=1 2= " + string.Join(" ", mask.Split(' ').Select(v => "93=" + v)) + " 92=1 2=",
                Columns(data),
                StringComparison.Ordinal
            );
            Assert.Contains(
                "92=2 2= 40=0.0 40=0.0 40=0.0 40=0.0 40=120.0 40=0.0",
                Columns(data),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void TheMaskOfCellsWithBordersOnTheirBottomAndRight_IsAutoCads_WhereTheEdgeIsTheCellsOwn()
        {
            // AutoCAD 2021's 3 by 3 table with a border on the right of its middle cell, the top of it, and on the left and bottom of
            // the cell in the corner (group 276 is the bottom, 0x11100, and 278 the left, 0x22200); the cell across each edge holds the
            // side that faces it.
            var cells = new List<string>
            {
                NewCell("a", 262144),
                NewCell(null, 0),
                NewCell(null, 0),
                NewCell(null, 0),
                NewCell(null, 17472, "279=100 69=0"),
                NewCell(null, 139776, "278=-2 68=2"),
                NewCell(null, 0),
                NewCell(null, 0),
                NewCell(null, 209664, "276=-2 66=6 278=-2 68=5"),
            };
            byte[] saved = Saved(Table3("AC1032", cells), "AC1015");
            RoundTripOf(saved, Assert.Single(Tables(saved)), out _, out DwgAnswerKey.Record data);

            Assert.Contains(
                " 92=1 2= 93=0 93=69888 93=0 93=0 93=52416 93=139776 93=0 93=34944 93=209664 92=1 2=",
                Columns(data),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void TheMaskOfCellsWithABorderOnEveryEdge_HasEverySide_AsAutoCadKeepsIt()
        {
            // AutoCAD 2021's 3 by 3 table with all four sides of each cell given a colour: the R2018 cells keep each edge once
            // (the right edge of the top two rows at the corner, the bottom of the last row...), and the mask of every cell has the four
            // sides, 262 080 (0x3FFC0), since a cell holds the side of its neighbour that faces it. The corner cells' own groups are
            // 275 (the right) and 276 (the bottom).
            var cells = new List<string>
            {
                NewCell("t00", 419392, "278=-2 68=3 279=-2 69=1"),
                NewCell(null, 157248, "278=-2 68=3 279=-2 69=1"),
                NewCell("t02", 454336, "275=-2 65=4 278=-2 68=3 279=-2 69=1"),
                NewCell(null, 157248, "278=-2 68=3 279=-2 69=2"),
                NewCell("t11", 419392, "278=-2 68=3 279=-2 69=2"),
                NewCell(null, 192192, "275=-2 65=5 278=-2 68=3 279=-2 69=2"),
                NewCell("t20", 489280, "276=-2 66=2 278=-2 68=3 279=-2 69=3"),
                NewCell(null, 227136, "276=-2 66=3 278=-2 68=3 279=-2 69=3"),
                NewCell("t22", 524224, "275=-2 65=6 276=-2 66=4 278=-2 68=3 279=-2 69=3"),
            };
            byte[] saved = Saved(Table3("AC1032", cells), "AC1018");
            RoundTripOf(saved, Assert.Single(Tables(saved)), out _, out DwgAnswerKey.Record data);

            Assert.Contains(
                " 92=1 2= " + string.Join(" ", Enumerable.Repeat("93=262080", 9)) + " 92=1 2=",
                Columns(data),
                StringComparison.Ordinal
            );
        }

        #endregion

        #region A text of more than 250 characters

        private static readonly string Long = new string('z', 300);

        [Fact]
        public void ATextOfMoreThan250Characters_StaysATableGoingDown_InTheGroupsAutoCadWritesAndWithItsChecksum()
        {
            var cells = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                cells.Add(i == 6 ? NewCell(Long, 262144) : NewCell(null, 0));
            }

            byte[] saved = Saved(Table3("AC1032", cells), "AC1015", out IReadOnlyList<string> said);

            Assert.Empty(said);
            DwgAnswerKey.Record table = Assert.Single(Tables(saved));
            RoundTripOf(saved, table, out _, out DwgAnswerKey.Record data);

            // AutoCAD's R2000 cell: the first 250 in 3 and the rest in 1; the data table's value holds the text whole.
            Assert.Contains(
                "171=1 172=0 173=0 174=0 175=1 176=1 177=0 178=0 145=0.0 3="
                    + Long.Substring(0, 250)
                    + " 1="
                    + Long.Substring(250),
                Show(table.Groups)
            );
            Assert.Contains("40=5508300.0", Columns(data), StringComparison.Ordinal);
            Assert.Equal(
                "100=AcDbXrecord 280=1 93=6 90=4 1=" + Long + " 94=0 300= 302=" + Long,
                Values(saved, data)[6]
            );
        }

        [Fact]
        public void ATextOfMoreThan250CharactersOfR2000_GoingUp_IsTheCellAutoCadWritesInR2018()
        {
            var cells = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                cells.Add(i == 6 ? OldCell(Long, 0) : OldCell(string.Empty, 0));
            }

            byte[] saved = Saved(Table3("AC1015", cells), "AC1032", out IReadOnlyList<string> said);

            Assert.Empty(said);
            string table = Show(Assert.Single(Tables(saved)).Groups);

            Assert.Contains(
                "91=262144 178=0 145=0.0 92=0 301=CELL_VALUE 93=6 90=4 2="
                    + Long.Substring(0, 250)
                    + " 1="
                    + Long.Substring(250)
                    + " 94=0 300= 303="
                    + Long.Substring(0, 250)
                    + " 302="
                    + Long.Substring(250)
                    + " 304=ACVALUE_END",
                table
            );
        }

        #endregion

        #region A table that keeps its table content

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void ATableWithATableContent_GoingDown_HasTheDataTableInItsRoundTripRecord(string version)
        {
            byte[] saved = Saved(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018), version);
            DwgAnswerKey.Record table = Assert.Single(Tables(saved));
            DwgAnswerKey.Record xrecord = Record(saved, "B02");

            // AutoCAD's: the part follows the pointer to the table geometry, in the same XRECORD, which owns the data table.
            Assert.Equal(
                "102=ACAD_ROUNDTRIP_PRE2007_TABLE 90=3 91=2 102=ACAD_ROUNDTRIP_PRE2007_TABLECELL",
                Show(xrecord.Groups.SkipWhile(g => g.Value != "ACAD_ROUNDTRIP_PRE2007_TABLE").Take(4))
            );
            RoundTripOf(saved, table, out DwgAnswerKey.Record found, out DwgAnswerKey.Record data);
            Assert.Equal("B02", found.Key.Substring(2));
            Assert.Equal("B02", data.Owner);
            Assert.Equal(6, data.Groups.Count(g => g.Key == 360));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void TheTablesOfAnR2018DrawingAutoCadWrote_GoingDown_HaveTheDataTablesAutoCadWritesOfThem(string version)
        {
            // The key holds two tables: cells of text only, and merged cells with a table content that has its round trip already.
            byte[] answer = DwgAnswerKey.Key("table_" + version);
            byte[] saved = Saved(Text(DwgAnswerKey.Key("table_AC1032")), version, out IReadOnlyList<string> said);
            List<DwgAnswerKey.Record> tables = Tables(saved);
            List<DwgAnswerKey.Record> key = Tables(answer);

            Assert.Empty(said);
            Assert.Equal(2, tables.Count);

            for (int i = 0; i < 2; i++)
            {
                RoundTripOf(saved, tables[i], out DwgAnswerKey.Record xrecord, out DwgAnswerKey.Record data);
                RoundTripOf(answer, key[i], out DwgAnswerKey.Record keyRecord, out DwgAnswerKey.Record keyData);

                Assert.Equal(Columns(keyData), Columns(data));
                // AutoCAD wrote an empty cell of its own R2000 as a text of nothing and a text with the flag 4; saving the R2018 file as R2000 it writes a value that is not set and the flag 6.
                Assert.Equal(Values(answer, keyData).Select(Unset), Values(saved, data).Select(Unset));
                Assert.Equal(
                    Show(keyRecord.Groups.SkipWhile(g => g.Value != "ACAD_ROUNDTRIP_PRE2007_TABLE").Take(3)),
                    Show(xrecord.Groups.SkipWhile(g => g.Value != "ACAD_ROUNDTRIP_PRE2007_TABLE").Take(3))
                );
            }

            // The class of AutoCAD's own R2004 counts 4 of the 2 data tables in its file; saving the R2018 file as R2004 it counts the 2.
            string name = version == "AC1015" ? "ACDBDATATABLE" : "DATATABLE";

            Assert.Equal(
                Show(DwgAnswerKey.Parse(answer).Single(r => r.Key == "C:" + name).Groups.Where(g => g.Key != 91)),
                Show(DwgAnswerKey.Parse(saved).Single(r => r.Key == "C:" + name).Groups.Where(g => g.Key != 91))
            );
        }

        #endregion

        #region Going up again

        [Fact]
        public void ABlockCellOfR2000_GoingUp_HasNoContentFlag_AsAutoCadWritesIt()
        {
            // AutoCAD 2021, saving its own R2000 table with a block cell as R2018: 91 is 0 whether the block has an attribute value
            // or not, where the cell of a text has the 0x40000.
            string block =
                G(171, "2")
                + G(172, "0")
                + G(173, "0")
                + G(174, "1")
                + G(175, "1")
                + G(176, "1")
                + G(177, "0")
                + G(178, "0")
                + G(145, "0.0")
                + G(340, "7D")
                + G(144, "1.0")
                + G(179, "1")
                + G(331, "81")
                + G(300, "M12");
            var cells = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                cells.Add(i == 4 ? block : OldCell(string.Empty, 0));
            }

            byte[] saved = Saved(Table3("AC1015", cells), "AC1032");

            Assert.Contains(
                "171=2 172=0 173=0 174=1 175=1 176=1 91=0 178=0 145=0.0 340=7D 144=1.0 179=1 331=81 300=M12 92=0 301=CELL_VALUE 93=1 90=4",
                Show(Assert.Single(Tables(saved)).Groups)
            );
        }

        [Fact]
        public void AnEmptyCellOfR2000WithABorderOnItsTop_GoingUp_HasNoContentFlag()
        {
            // AutoCAD 2021: the cell below an edge changed keeps 91 at 17 472 (0x4440); only a cell with text has the 0x40000 as well.
            var cells = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                cells.Add(i == 6 ? OldCell(string.Empty, 17472, "279=100 69=0") : OldCell(string.Empty, 0));
            }

            byte[] saved = Saved(Table3("AC1015", cells), "AC1032");

            Assert.Contains("91=17472 178=0 145=0.0 279=100 69=0 92=0", Show(Assert.Single(Tables(saved)).Groups));
        }

        [Fact]
        public void AnEmptyCellOfR2000WithABorderOnItsRight_GoingUp_HasTheContentFlag_AsAutoCadWritesIt()
        {
            // AutoCAD 2021, saving its own R2000 cell with no text and a border on the right (275), left (278) and top (279) as R2018:
            // 454 336 (0x6EE40), the 0x40000 added to the 0x2EE40 of the flags (-4416 is 0xEE40) where the right border is.
            var cells = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                cells.Add(
                    i == 5
                        ? OldCell(string.Empty, -4416, "275=-2 65=5 278=-2 68=3 279=-2 69=2")
                        : OldCell(string.Empty, 0)
                );
            }

            byte[] saved = Saved(Table3("AC1015", cells), "AC1032");

            Assert.Contains(
                "91=454336 178=0 145=0.0 275=-2 65=5 278=-2 68=3 279=-2 69=2 92=0",
                Show(Assert.Single(Tables(saved)).Groups)
            );
        }

        #endregion
    }
}
