using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of dxfver14: the lines of the rows of a kind (<c>SetGridColor(color, borders, rows)</c> and its siblings)
    /// written into the table's own groups alone, as the lines of a cell are written into the cell's bytes, where each call
    /// read and wrote every group of the record. The speed is held by the bytes allocated, which do not vary with the machine
    /// as a time does.
    /// </summary>
    public class DxfSpeed14Tests
    {
        private static DxfColor Aci(int index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);

        private static DxfTable Filled(DxfDatabase db, int rows, int columns)
        {
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    table.SetTextString(r, c, "cell " + r + "," + c);
                }
            }

            return table;
        }

        #region The lines of the rows of a kind

        // The SHA-256 (24 hex) of the file each run of 120 edits saves, as the library gave it before the work of dxfver14.
        private static readonly string[] RunsLines =
        {
            "AC1032 seed 1 67213B37A448D62E28091E08",
            "AC1032 seed 2 5449A505CFFC7959ADE4FA79",
            "AC1032 seed 3 5572D5F62EDDF900B4942A58",
            "AC1032 seed 4 B64C11780EFBC85F4D9BDD4F",
            "AC1027 seed 1 D1DE31B566011D46EA8FD340",
            "AC1027 seed 2 E79D21FBCBD5DA0402ACB4A9",
            "AC1027 seed 3 20BAED86F7ACF678B2E8D895",
            "AC1027 seed 4 9DBE72E83BC91D45EDDDCC11",
            "AC1021 seed 1 8D3D94B63F732FB26A6D23F0",
            "AC1021 seed 2 8808AB7C64B89B811E2CF5FE",
            "AC1021 seed 3 AB9D1D439FB8D1408C281657",
            "AC1021 seed 4 9646F54E1C65F7E504B24ABE",
            "AC1018 seed 1 2DB72B14625EDB4C8303B93F",
            "AC1018 seed 2 A506628E58B59ED434E0D39A",
            "AC1018 seed 3 5C2082655F26289BB94E3E48",
            "AC1018 seed 4 9FB3FDD7DD8E995CC9E7A6E7",
            "AC1015 seed 1 8350A708D0180E38F6CAB963",
            "AC1015 seed 2 2A9FAE1ECA4892365923ABD2",
            "AC1015 seed 3 7CB2ADB960B717BF42E9B357",
            "AC1015 seed 4 71788A3C1EF713444CC320E5",
        };

        [Fact]
        public void RunsOfEdits_WithTheLinesOfTheRowsAndOfTheCells_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(RunsLines, DxfSpeed14Scenarios.Runs());
        }

        [Theory]
        [InlineData("AC1032", 7)]
        [InlineData("AC1032", 18)]
        [InlineData("AC1032", 31)]
        [InlineData("AC1027", 5)]
        [InlineData("AC1027", 22)]
        [InlineData("AC1021", 9)]
        [InlineData("AC1021", 27)]
        [InlineData("AC1018", 14)]
        [InlineData("AC1015", 3)]
        [InlineData("AC1015", 40)]
        public void RunsOfEdits_WithTheLinesOfTheRowsAndOfTheCells_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed
        )
        {
            Assert.Equal(
                DxfSpeed14Scenarios.Run(version, seed, 60, true),
                DxfSpeed14Scenarios.Run(version, seed, 60, false)
            );
        }

        [Theory]
        [InlineData("AC1032", 4)]
        [InlineData("AC1032", 19)]
        [InlineData("AC1027", 12)]
        [InlineData("AC1021", 8)]
        [InlineData("AC1015", 21)]
        public void RunsOfEdits_OfATableWithExtendedData_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed
        )
        {
            Assert.Equal(
                DxfSpeed14Scenarios.Run(version, seed, 60, true, true),
                DxfSpeed14Scenarios.Run(version, seed, 60, false, true)
            );
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1021")]
        [InlineData("AC1015")]
        public void TheLinesOfTheRows_SetBetweenCellsSetApart_AreReadBackAsSet_AndNoCellIsLost(string version)
        {
            // The cells set and kept apart from the record, then the lines of the rows (written into the table's own groups),
            // then more cells: each is read back as set, and the file saved and read again holds them all.
            DxfDatabase db = DxfSpeed12Scenarios.Metric(version);
            DxfTable table = Filled(db, 12, 5);
            table.SetTextString(3, 2, "before");
            table.SetGridColor(
                Aci(3),
                DxfGridLineType.HorizontalInside | DxfGridLineType.VerticalInside,
                DxfRowType.DataRow
            );
            table.SetTextString(11, 4, "last");
            table.SetGridLineWeight(DxfLineWeight.LineWeight035, DxfGridLineType.OuterGridLines, DxfRowType.TitleRow);
            table.SetGridVisibility(false, DxfGridLineType.HorizontalBottom, DxfRowType.HeaderRow);
            table.SetTextString(0, 0, "first");
            table.SetGridColor(Aci(5), DxfGridLineType.HorizontalInside, DxfRowType.DataRow);

            Assert.Equal(5, table.GridColor(DxfGridLineType.HorizontalInside, DxfRowType.DataRow).ColorIndex);
            Assert.Equal(3, table.GridColor(DxfGridLineType.VerticalInside, DxfRowType.DataRow).ColorIndex);
            Assert.Equal(
                DxfLineWeight.LineWeight035,
                table.GridLineWeight(DxfGridLineType.VerticalLeft, DxfRowType.TitleRow)
            );
            Assert.False(table.GridVisibility(DxfGridLineType.HorizontalBottom, DxfRowType.HeaderRow));
            Assert.Equal("before", table.TextString(3, 2));
            Assert.Equal("last", table.TextString(11, 4));
            Assert.Equal("first", table.TextString(0, 0));
            Assert.Equal("cell 7,1", table.TextString(7, 1));

            DxfTable again = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal(5, again.GridColor(DxfGridLineType.HorizontalInside, DxfRowType.DataRow).ColorIndex);
            Assert.Equal("last", again.TextString(11, 4));
            Assert.Equal("first", again.TextString(0, 0));
        }

        [Fact]
        public void TheLinesOfTheRowsOfEveryKind_SetOnATableOf931Cells_AllocateLessThan60Megabytes()
        {
            // Each call read and wrote every group of the record: 108 calls took 530 ms and 500 MB for these 931 cells.
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 49, 19);
            table.SetGridColor(0, 0, DxfGridLineType.HorizontalTop, Aci(2));
            var kinds = new[] { DxfRowType.TitleRow, DxfRowType.HeaderRow, DxfRowType.DataRow };
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (DxfRowType kind in kinds)
                {
                    for (int line = 0; line < 6; line++)
                    {
                        var lineType = (DxfGridLineType)(1 << line);
                        table.SetGridColor(Aci(1 + ((line + pass) % 7)), lineType, kind);
                        table.SetGridLineWeight(
                            pass == 0 ? DxfLineWeight.LineWeight035 : DxfLineWeight.LineWeight050,
                            lineType,
                            kind
                        );
                        table.SetGridVisibility((line + pass) % 2 == 0, lineType, kind);
                    }
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(3, table.GridColor(DxfGridLineType.HorizontalInside, DxfRowType.DataRow).ColorIndex);
            Assert.True(used < 60_000_000L, "allocated " + used);
        }

        #endregion

        #region The walk over the records

        // The code line as the two-pass read takes it: up to the first line feed (a carriage return before it left out), spaces
        // and tabs about the number left out, an optional minus, one to nine digits.
        private static bool TwoPass(byte[] bytes, int length, out int code, out int next)
        {
            code = 0;
            int feed = Array.IndexOf(bytes, (byte)'\n', 0, length);
            int end = feed < 0 ? length : feed;
            next = feed < 0 ? length : feed + 1;
            end = end > 0 && bytes[end - 1] == (byte)'\r' ? end - 1 : end;
            string text = System.Text.Encoding.ASCII.GetString(bytes, 0, end).Trim(' ', '\t');
            bool negative = text.StartsWith("-", StringComparison.Ordinal);
            text = negative ? text.Substring(1) : text;

            if (text.Length == 0 || text.Length > 9 || !text.All(c => c >= '0' && c <= '9'))
            {
                return false;
            }

            code = int.Parse(text, System.Globalization.CultureInfo.InvariantCulture) * (negative ? -1 : 1);
            return true;
        }

        [Fact]
        public void ACodeLineTakenInOnePass_GivesTheCodeAndTheNextLineTheTwoPassReadGives()
        {
            var random = new Random(14);
            string[] pieces =
            {
                " ",
                "  ",
                "\t",
                "-",
                "0",
                "7",
                "10",
                "100",
                "1001",
                "999",
                "1234567890",
                "123456789",
                "a",
                "\r",
                "",
                "5",
            };
            string[] ends = { "\n", "\r\n", "", "\r", "\n\n", "\r\r\n" };
            int plain = 0;

            for (int i = 0; i < 40_000; i++)
            {
                string line = string.Empty;

                for (int k = random.Next(1, 5); k > 0; k--)
                {
                    line += pieces[random.Next(pieces.Length)];
                }

                string text = line + ends[random.Next(ends.Length)] + (random.Next(2) == 0 ? "VALUE\n" : string.Empty);
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);

                if (DxfFile.TryPlainCode(bytes, 0, bytes.Length, out int code, out int next))
                {
                    plain++;
                    Assert.True(
                        TwoPass(bytes, bytes.Length, out int expected, out int expectedNext),
                        "line '" + line + "'"
                    );
                    Assert.Equal(expected, code);
                    Assert.Equal(expectedNext, next);
                }
            }

            Assert.True(plain > 2_000, "plain " + plain);
        }

        [Fact]
        public void TheTypesOfRecords_PaddedLongerAndOfTheSameKey_ArePickedOutByTheWalkAsTheyAre()
        {
            // Words that fall in one slot of the walk's memory of the words it saw (the same length, first and last letter), a
            // word with spaces, tabs and a form feed about it, and one beyond ASCII: each record is the type it names.
            string[] names =
            {
                "LINE",
                "LANE",
                "LINE",
                " LINE ",
                "\tLANE\t",
                "LINE\f",
                "LÍNE",
                "LINE",
                "CIRCLE",
                "LANE",
                "LÍNE",
                "LINE",
            };
            var text = new System.Text.StringBuilder("  0\nSECTION\n  2\nENTITIES\n");

            foreach (string name in names)
            {
                text.Append("  0\n").Append(name).Append("\n  5\n1\n  8\n0\n");
            }

            text.Append("  0\nENDSEC\n  0\nEOF\n");

            DxfFile file = DxfFile.Parse(text.ToString());

            Assert.Equal(
                names.Select(n => n.Trim()).ToList(),
                Enumerable.Range(0, file.ModelSpace.Count).Select(i => file.ModelSpace[i].Type).ToList()
            );
        }

        #endregion

        #region A block cell made a text cell

        [Fact]
        public void EveryCellOfATableOfBlockCells_MadeATextCell_SavesToTheBytesItDidBefore()
        {
            Assert.Equal("E5DAC4477906F8C763237398", DxfSpeed14Scenarios.BlockCellsMadeText());
        }

        [Fact]
        public void EveryCellOfATableOf931BlockCells_MadeATextCell_AllocatesLessThan100Megabytes()
        {
            // The library sets no block, so each cell is made a block cell in the text of the table before it is read; the write
            // path of SetCellType is then the one of a table opened from a file: 24 MB and 13 ms for these 931 cells.
            DxfDatabase db = DxfSpeed14Scenarios.WithBlockCells(49, 19);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(30, 7));
            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(48, 18));
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int r = 0; r < 49; r++)
            {
                for (int c = 0; c < 19; c++)
                {
                    table.SetCellType(r, c, DxfTableCellType.TextCell);
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(DxfTableCellType.TextCell, table.CellType(0, 0));
            Assert.Equal(DxfTableCellType.TextCell, table.CellType(30, 7));
            Assert.Equal(DxfTableCellType.TextCell, table.CellType(48, 18));
            Assert.DoesNotContain("ABCD", db.ToText());
            Assert.True(used < 100_000_000L, "allocated " + used);
        }

        #endregion
    }
}
