using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables9: the record of a table written from its own bytes where rows and columns are put in and taken out and
    /// where the heights of rows and the widths of columns are set (each cell that comes out as it was copied as it stands), the
    /// layout read taking the cells written one by one into it, and the fields a cell lets go of looked for in the file without
    /// reading the groups of a record that cannot name them. The speed is held by the bytes allocated, which do not vary with the
    /// machine as a time does; the bytes saved are held to what the library gave before.
    /// </summary>
    public class DxfSpeed21Tests
    {
        // The SHA-256 (24 hex) of the file each run of 120 edits saves, as the library gave it before the work of tables9.
        private static readonly string[] RunsBefore =
        {
            "AC1032 seed 1 text plain BBB92BDF3315B7C864720666",
            "AC1032 seed 1 text content 3AB333A0641EB373CE6151AF",
            "AC1032 seed 1 blocks plain 95048B596EA5C2EC5E2884CB",
            "AC1032 seed 1 blocks content C87798A26FA3C7B3FE9045AA",
            "AC1032 seed 2 text plain 71C4866BF5FB61C201562A79",
            "AC1032 seed 2 text content EB7173CEDDBFF0B2A3E36EEF",
            "AC1032 seed 2 blocks plain C511661854B1DD78D83A416F",
            "AC1032 seed 2 blocks content 41C7EBD4E12FE366C88A4886",
            "AC1032 seed 3 text plain 75CF9D4D0C4CDC050CA5DB12",
            "AC1032 seed 3 text content 58B226C0195939977654F930",
            "AC1032 seed 3 blocks plain 1C3AA5EAB59C50E280A4302E",
            "AC1032 seed 3 blocks content 07D02FAA09125C5513EB7180",
            "AC1032 seed 4 text plain 21B73CE33AFAD89E6A5BD7C2",
            "AC1032 seed 4 text content C90450DE167B87B6C295E003",
            "AC1032 seed 4 blocks plain 6048C8296FB7E710871980AB",
            "AC1032 seed 4 blocks content 1F4DDA8F61CBB8F2ECD980DB",
            "AC1027 seed 1 text plain D1CD6A8D2D415403CE1E099F",
            "AC1027 seed 1 text content E3F9DC686411FCD6D70DF61D",
            "AC1027 seed 1 blocks plain 0B1F95EF9B505A3908DF33A9",
            "AC1027 seed 1 blocks content 4008E5F1333EC681DF46478F",
            "AC1027 seed 2 text plain CFA951D4AFB90F38AE188CA4",
            "AC1027 seed 2 text content 12438F2513299226D026F40B",
            "AC1027 seed 2 blocks plain 4A88C42B5D073227EB66AD35",
            "AC1027 seed 2 blocks content 5B29CA4DFAE0ED987DDA7515",
            "AC1027 seed 3 text plain 25DE9B40BCCD9BEB165974AF",
            "AC1027 seed 3 text content 3089D5EF380DFE94291C2828",
            "AC1027 seed 3 blocks plain FFCF48ED9C78359B527BD4D3",
            "AC1027 seed 3 blocks content 67417FE8423B4B44FA5BEC86",
            "AC1027 seed 4 text plain 439A2DD641907B593D83CCE5",
            "AC1027 seed 4 text content EB1AC5C466E8E47B8B8823CF",
            "AC1027 seed 4 blocks plain 9D56E747922B15D888630A7D",
            "AC1027 seed 4 blocks content C1EA61AF0F4BB7CAC05AE039",
            "AC1021 seed 1 text plain 0C9A4F586D47C399C0FB2E83",
            "AC1021 seed 1 text content 842BCCC0D6CCF5A2E636C891",
            "AC1021 seed 1 blocks plain 772F2BC1B945A54427C2B5D5",
            "AC1021 seed 1 blocks content 3E997A50A4E24314CC9F2D44",
            "AC1021 seed 2 text plain 8C6C89CDA2F0D01B80228308",
            "AC1021 seed 2 text content BB0EB39A5C858101F8102901",
            "AC1021 seed 2 blocks plain F4FD93D1F7EB15A6D8DAD554",
            "AC1021 seed 2 blocks content F0500928A13682CB4AB4480D",
            "AC1021 seed 3 text plain 7CBA98840AC0F8539B7C84F6",
            "AC1021 seed 3 text content DEF1FA7FF7C4ABB335B6ED43",
            "AC1021 seed 3 blocks plain B894508DFE5D9F00AA1D3116",
            "AC1021 seed 3 blocks content DFC0CBF9B23DB4ABE3A328AD",
            "AC1021 seed 4 text plain 0349505608E32F11699374EE",
            "AC1021 seed 4 text content 91F9551EDDC62AB114FD55D3",
            "AC1021 seed 4 blocks plain D05E0EA10C9348E1C24A42F1",
            "AC1021 seed 4 blocks content 279C7D2D862560B8F6914E09",
            "AC1018 seed 1 text plain 54CF32FAA4992DD58B61E889",
            "AC1018 seed 1 text content 54CF32FAA4992DD58B61E889",
            "AC1018 seed 1 blocks plain 208D9FDF27FCF9BA11EDF7E6",
            "AC1018 seed 1 blocks content 208D9FDF27FCF9BA11EDF7E6",
            "AC1018 seed 2 text plain 6072CD09E89E63BEA8FCD84C",
            "AC1018 seed 2 text content 6072CD09E89E63BEA8FCD84C",
            "AC1018 seed 2 blocks plain B1E2335D55CCECE19564FD9A",
            "AC1018 seed 2 blocks content B1E2335D55CCECE19564FD9A",
            "AC1018 seed 3 text plain 0E499B664986ABBB10E6171E",
            "AC1018 seed 3 text content 0E499B664986ABBB10E6171E",
            "AC1018 seed 3 blocks plain 457527E6045AA6E05036F1D2",
            "AC1018 seed 3 blocks content 457527E6045AA6E05036F1D2",
            "AC1018 seed 4 text plain E53D955EB5C1AE88DCB23BC1",
            "AC1018 seed 4 text content E53D955EB5C1AE88DCB23BC1",
            "AC1018 seed 4 blocks plain B4860D602909F1EFEDEF5384",
            "AC1018 seed 4 blocks content B4860D602909F1EFEDEF5384",
        };

        [Fact]
        public void RunsOfEdits_WithHeightsAndWidthsSetAmongThem_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed21Scenarios.Runs());
        }

        [Theory]
        [InlineData("AC1032", 3, false, false)]
        [InlineData("AC1032", 8, true, false)]
        [InlineData("AC1032", 14, false, true)]
        [InlineData("AC1032", 21, true, true)]
        [InlineData("AC1027", 6, true, false)]
        [InlineData("AC1027", 19, true, true)]
        [InlineData("AC1021", 5, true, false)]
        [InlineData("AC1021", 12, false, true)]
        [InlineData("AC1021", 17, true, true)]
        [InlineData("AC1018", 9, false, false)]
        [InlineData("AC1018", 22, false, true)]
        public void RunsOfEdits_WithHeightsAndWidthsSetAmongThem_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed,
            bool content,
            bool blocks
        )
        {
            Assert.Equal(
                DxfSpeed21Scenarios.Run(version, seed, 60, true, 8, 6, content, blocks, false),
                DxfSpeed21Scenarios.Run(version, seed, 60, false, 8, 6, content, blocks, false)
            );
        }

        private static DxfDatabase Table931() => DxfSpeed18Scenarios.Table("AC1032", 49, 19, true, false);

        [Fact]
        public void HeightsAndWidthsSetBetweenEditsOf931Cells_AllocateLessThan250Megabytes()
        {
            // A height or a width set wrote the record whole and left the table to read it again for the next edit: 341 MB for
            // these ten rounds of six edits (151 MB now).
            long allocated = DxfSpeed21Scenarios.Allocated(Table931(), DxfSpeed21Scenarios.SetThenEdit);

            Assert.True(allocated < 250_000_000L, "edits allocated " + allocated);
        }

        [Fact]
        public void TwentyRowsPutInAndTakenOutOf931Cells_AllocateLessThan250Megabytes()
        {
            // The record was written whole for each row: 293 MB for these 40 edits (199 MB now, each cell that comes out as it was
            // copied from the bytes of the record).
            DxfDatabase db = Table931();
            long allocated = DxfSpeed21Scenarios.Allocated(db, DxfSpeed21Scenarios.RowsInAndOut);

            Assert.Equal(49, db.ModelSpace.OfType<DxfTable>().Single().NumRows);
            Assert.True(allocated < 250_000_000L, "rows allocated " + allocated);
        }

        [Fact]
        public void TwentyMergesAcrossACellHoldingAField_AllocateLessThan100Megabytes()
        {
            // The fields the cell let go of were looked for among the groups of every record of the file, a list of the groups
            // of each: 175 MB for these 20 merges (49 MB now).
            long allocated = DxfSpeed21Scenarios.Allocated(Table931(), DxfSpeed21Scenarios.MergesAcrossFields);

            Assert.True(allocated < 100_000_000L, "merges allocated " + allocated);
        }

        // A table of the drawing AutoCAD wrote, its cell B-12 holding the field given (a text field, its child below it), and
        // an XRECORD pointing at the text given where there is one.
        private static string WithField(string field, string child, string pointer) =>
            DxfDimensionDrawings
                .R2018.Replace(
                    "  0\nENDSEC\n  0\nEOF\n",
                    "  0\nFIELD\n  5\n"
                        + field
                        + "\n330\nF6\n100\nAcDbField\n  1\n_text\n  2\n%<\\_FldIdx 0>%\n 90\n1\n360\n"
                        + child
                        + "\n 97\n0\n"
                        + "  0\nFIELD\n  5\n"
                        + child
                        + "\n330\n"
                        + field
                        + "\n100\nAcDbField\n  1\nAcExpr\n  2\n\\AcExpr (1+2)\n 90\n0\n 97\n0\n"
                        + (
                            pointer == null
                                ? string.Empty
                                : "  0\nXRECORD\n  5\nFB2\n330\nA\n100\nAcDbXrecord\n280\n1\n340\n" + pointer + "\n"
                        )
                        + "  0\nENDSEC\n  0\nEOF\n"
                )
                .Replace(
                    " 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n",
                    " 92\n0\n344\n" + field + "\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n"
                );

        private static bool Holds(string text, string handle) =>
            text.IndexOf("  0\nFIELD\n  5\n" + handle + "\n", StringComparison.Ordinal) >= 0;

        [Theory]
        [InlineData("FA1", "FA2", "FA1", true)]
        [InlineData("FA1", "FA2", "fa1", true)]
        [InlineData("FA1", "FA2", "Fa1", true)]
        [InlineData("FA1", "FA2", "0FA1", true)]
        [InlineData("FA1", "FA2", " FA1", true)]
        [InlineData("FA1", "FA2", "000fa1", true)]
        [InlineData("FA1", "FA2", "FA3", false)]
        [InlineData("FA1", "FA2", "FA10", false)]
        [InlineData("FA1", "FA2", "A1", false)]
        [InlineData("FA1", "FA2", null, false)]
        [InlineData("70001", "70002", "70001", true)]
        [InlineData("70001", "70002", "0070001", true)]
        [InlineData("70001", "70002", "70003", false)]
        [InlineData("70001", "70002", null, false)]
        [InlineData("7FF01", "7FF02", "7ff01", true)]
        [InlineData("7FF01", "7FF02", "7FF02", false)]
        public void AFieldACellLetsGo_IsLeftWhereARecordPointsAtItInAnyCaseOrWithZerosBefore(
            string field,
            string child,
            string pointer,
            bool kept
        )
        {
            DxfDatabase db = DxfDatabase.Parse(WithField(field, child, pointer));

            db.ModelSpace.OfType<DxfTable>().Single().SetTextString(2, 0, "x");

            Assert.Equal(kept, Holds(db.ToText(), field));
        }

        [Fact]
        public void ARowHeightSetTwice_AndAColumnWidthSet_LeaveTheRecordAsOneReadAnewGives()
        {
            DxfDatabase db = DxfSpeed18Scenarios.Table("AC1032", 8, 6, true, false);
            db.ModelSpace.OfType<DxfTable>().Single().SetTextHeight(0, 1, 2.0);
            DxfDatabase anew = DxfDatabase.Parse(db.ToText());

            foreach (DxfDatabase d in new[] { db, anew })
            {
                DxfTable table = d.ModelSpace.OfType<DxfTable>().Single();
                table.SetRowHeight(3, 21.0);
                table.SetRowHeight(3, 22.5);
                table.SetColumnWidth(2, 41.0);
                table.SetTextString(4, 1, "after");
                table.InsertRows(2, 8.0, 1);
            }

            DxfTable set = db.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal(22.5, set.RowHeight(4));
            Assert.Equal(41.0, set.ColumnWidth(2));
            Assert.Equal("after", set.GetTextString(5, 1, 0));
            Assert.Equal(anew.ToText(), db.ToText());
        }
    }
}
