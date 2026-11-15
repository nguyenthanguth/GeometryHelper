using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables7: <c>MergeCells</c> and <c>UnmergeCells</c> written into the bytes of the cells they change and of the
    /// table's TABLECONTENT, where each rebuilt the whole record (192 merges of two cells in a table of 931 cells took 1.6 s and
    /// 1.8 GB, and 6 s and 6.8 GB with a TABLECONTENT, which AutoCAD writes). The speed is held by the bytes allocated, which do
    /// not vary with the machine as a time does; the bytes saved are held to what the library gave before.
    /// </summary>
    public class DxfSpeed17Tests
    {
        // The SHA-256 (24 hex) of the file each run of 120 edits saves, as the library gave it before the work of tables7.
        private static readonly string[] RunsBefore =
        {
            "AC1032 seed 1 text 47A834428F7775AB7C0B7B86",
            "AC1032 seed 1 content 30FC0CE9CBBD45D7E5F9417C",
            "AC1032 seed 1 blocks F7D17CF89A5164E610B3A4D2",
            "AC1032 seed 2 text C679136B9462A59EEDC4D67E",
            "AC1032 seed 2 content 91DB2997EF4EE1D55470E92F",
            "AC1032 seed 2 blocks 47C0B3B1A1D6145E70DD3B4F",
            "AC1032 seed 3 text 7B8F40F758F7C3099E3899C8",
            "AC1032 seed 3 content E9918320C87CCA088A56A65E",
            "AC1032 seed 3 blocks 4BF23ACB331A13FE663634E3",
            "AC1032 seed 4 text 77DF21F818584C546CF32C0D",
            "AC1032 seed 4 content 8A8F32089346028FE0A585E9",
            "AC1032 seed 4 blocks 56747299978748B1AF1BEC1A",
            "AC1027 seed 1 text 88C1E48BDE4BF03F5572EA32",
            "AC1027 seed 1 content 8BD64EFFA69540393C9F28E9",
            "AC1027 seed 1 blocks A6BDA3F34308BF3FEAD82C2B",
            "AC1027 seed 2 text 765A5438E7584861FF3E6989",
            "AC1027 seed 2 content EBBE121AFDCE62414ACADEAB",
            "AC1027 seed 2 blocks 22AAD0EC86440698377EF099",
            "AC1027 seed 3 text BC03FD44CC9C8346597F5A7B",
            "AC1027 seed 3 content E2A550E1AD84F8B7C11CEA52",
            "AC1027 seed 3 blocks E2D93A3E45C1570E74F7CCE2",
            "AC1027 seed 4 text 2DD24F5D0F5FF3BC2FBA9E83",
            "AC1027 seed 4 content 62D4DBB540A319CA28F214AF",
            "AC1027 seed 4 blocks CB2C21328BB57BE655A62BFA",
            "AC1021 seed 1 text 07600111AAB9DD857D8663A8",
            "AC1021 seed 1 content B00270F091E76B1B69C5034D",
            "AC1021 seed 1 blocks 6D07AE657BD3583FD8C71BE9",
            "AC1021 seed 2 text F1428FAB3A37FBED11ECE6A3",
            "AC1021 seed 2 content 56E1C652367650E1E6D09655",
            "AC1021 seed 2 blocks DECD237806616C2AA301A5EC",
            "AC1021 seed 3 text AA51F3DD4BB39C0E576363F5",
            "AC1021 seed 3 content 227588FFC7718DF2FCFFB5A9",
            "AC1021 seed 3 blocks EAE30BCB522FE86279EBA1E4",
            "AC1021 seed 4 text 6F7AE3B0382D88CC1F990FF2",
            "AC1021 seed 4 content 9BA5285A40BF658A9E82FC6C",
            "AC1021 seed 4 blocks 3C356A0F71BDCCB3D77050D5",
            "AC1018 seed 1 text A8D8A8675907BDD859A54223",
            "AC1018 seed 1 content A8D8A8675907BDD859A54223",
            "AC1018 seed 1 blocks F8F870642E9B9E9D96950997",
            "AC1018 seed 2 text 06B75BF09DBAA0012D669F72",
            "AC1018 seed 2 content 06B75BF09DBAA0012D669F72",
            "AC1018 seed 2 blocks BF3FABA7E5502A1A24442F6E",
            "AC1018 seed 3 text 38AAD9CB548F737CD3A4EB10",
            "AC1018 seed 3 content 38AAD9CB548F737CD3A4EB10",
            "AC1018 seed 3 blocks D8E466F6085C85B0DF3111A3",
            "AC1018 seed 4 text 9E188740C88BB5B823801A78",
            "AC1018 seed 4 content 9E188740C88BB5B823801A78",
            "AC1018 seed 4 blocks E25C98A8D57F590EE944187B",
        };

        [Fact]
        public void RunsOfEdits_WithMergesAmongTheSettersAndTheStructure_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed17Scenarios.Runs());
        }

        [Theory]
        [InlineData("AC1032", 3, false, false)]
        [InlineData("AC1032", 11, true, false)]
        [InlineData("AC1032", 24, false, true)]
        [InlineData("AC1032", 37, true, false)]
        [InlineData("AC1027", 6, true, false)]
        [InlineData("AC1027", 19, false, false)]
        [InlineData("AC1021", 8, true, false)]
        [InlineData("AC1021", 29, false, true)]
        [InlineData("AC1018", 12, false, false)]
        [InlineData("AC1018", 33, false, true)]
        public void RunsOfEdits_WithMergesAmongTheSettersAndTheStructure_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed,
            bool content,
            bool blocks
        )
        {
            Assert.Equal(
                DxfSpeed17Scenarios.Run(version, seed, 60, true, 8, 6, content, blocks),
                DxfSpeed17Scenarios.Run(version, seed, 60, false, 8, 6, content, blocks)
            );
        }

        private const int Rows = 49;
        private const int Columns = 19;

        /// <summary>Merges the two cells at the left of each of four places of every row but the first of the table, 192 merges.</summary>
        private static void MergeEachRow(DxfTable table)
        {
            for (int r = 1; r < Rows; r++)
            {
                for (int c = 0; c + 1 < Columns; c += 5)
                {
                    table.MergeCells(new DxfCellRange(r, c, r, c + 1));
                }
            }
        }

        private static void UnmergeEachRow(DxfTable table)
        {
            for (int r = 1; r < Rows; r++)
            {
                for (int c = 0; c + 1 < Columns; c += 5)
                {
                    table.UnmergeCells(new DxfCellRange(r, c, r, c + 1));
                }
            }
        }

        // The merges and unmerges are timed by the bytes they allocate; what they made is checked after them.
        private static void Measure(DxfDatabase db, long limit)
        {
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();

            // The first edit of a table merges the rows AutoCAD merges on load and writes the record whole.
            table.SetTextHeight(0, 1, 2.0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            MergeEachRow(table);
            long merged = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(table.IsMergedCell(30, 5, out DxfCellRange range));
            Assert.Equal(new DxfCellRange(30, 5, 30, 6), range);
            Assert.False(table.IsMergedCell(30, 7, out _));

            before = GC.GetAllocatedBytesForCurrentThread();
            UnmergeEachRow(table);
            long unmerged = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.False(table.IsMergedCell(30, 5, out _));
            Assert.True(merged < limit, "merges allocated " + merged);
            Assert.True(unmerged < limit, "unmerges allocated " + unmerged);
        }

        [Fact]
        public void TheMergeOfTwoCellsInEachRowOf931Cells_AllocatesLessThan150Megabytes()
        {
            // 1.8 GB for these 192 merges, and as much for the unmerges (53 and 29 MB now).
            Measure(DxfSpeed17Scenarios.TextTable("AC1032", Rows, Columns, false), 150_000_000L);
        }

        [Fact]
        public void TheMergeOfTwoCellsInEachRowOf931CellsOfAFile_AllocatesLessThan600Megabytes()
        {
            // The table of a file holds its TABLECONTENT, which each merge read and wrote whole: 6.8 GB for these 192 merges,
            // and 7.0 GB for the unmerges (235 and 196 MB now, 130 MB of them the one reading of the TABLECONTENT).
            Measure(DxfSpeed17Scenarios.TextTable("AC1032", Rows, Columns, true), 600_000_000L);
        }

        [Fact]
        public void TheMergeOfTwoCellsInEachRowOf931CellsOfBlockAndTextCells_AllocatesLessThan150Megabytes()
        {
            Measure(DxfSpeed16Scenarios.WithBlockCells("AC1032", Rows, Columns), 150_000_000L);
        }

        /// <summary>
        /// The same edits made on a table in place and on a table read anew from its text before each one (which writes its
        /// record whole), the text of the drawing compared at the end and after each edit.
        /// </summary>
        private static void SameAsReadAnew(Func<DxfDatabase> make, params Action<DxfTable>[] edits)
        {
            DxfDatabase inPlace = make();
            DxfDatabase anew = make();
            DxfTable table = inPlace.ModelSpace.OfType<DxfTable>().Single();

            foreach (Action<DxfTable> edit in edits)
            {
                anew = DxfDatabase.Parse(anew.ToText());
                edit(anew.ModelSpace.OfType<DxfTable>().Single());
                edit(table);
                Assert.Equal(anew.ToText(), inPlace.ToText());
            }
        }

        [Fact]
        public void ABlockCellSetToText_BetweenMerges_LeavesTheNextMergeAsOnARecordReadAnew()
        {
            // A cell set to another kind changes what the kept blocks of merged cells stand on; the merges after it read them again.
            SameAsReadAnew(
                () => DxfSpeed16Scenarios.WithBlockCells("AC1032", 6, 5),
                t => t.SetTextString(1, 2, "x"),
                t => t.MergeCells(new DxfCellRange(1, 0, 1, 1)),
                t => t.SetCellType(2, 0, DxfTableCellType.TextCell),
                t => t.MergeCells(new DxfCellRange(2, 0, 3, 1)),
                t => t.SetTextString(2, 1, "y"),
                t => t.UnmergeCells(new DxfCellRange(1, 0, 1, 0)),
                t => t.MergeCells(new DxfCellRange(1, 0, 3, 2)),
                t => t.UnmergeCells(new DxfCellRange(2, 1, 2, 1)),
                t => t.MergeCells(new DxfCellRange(4, 3, 5, 4))
            );
        }

        [Fact]
        public void ACellMergedInAFileBySomeFlagNoBlockHolds_IsLeftAsAWholeWritingLeavesIt()
        {
            // A cell of 173 of 1 among no block: the first merge writes the record whole and sets it right, as it always did;
            // the merges after it are written in place and leave the table as one read anew would.
            Func<DxfDatabase> make = () =>
            {
                string text = DxfSpeed17Scenarios.TextTable("AC1032", 6, 4, false).ToText();
                int at = Regex.Match(text, @"\r?\n  0\r?\nACAD_TABLE\r?\n").Index;
                int cell = 0;
                string stray = Regex.Replace(
                    text.Substring(at),
                    @"(\r?\n173\r?\n)0(\r?\n)",
                    m => ++cell == 6 ? m.Groups[1].Value + "1" + m.Groups[2].Value : m.Value,
                    RegexOptions.None
                );
                return DxfDatabase.Parse(text.Substring(0, at) + stray);
            };

            DxfTable table = make().ModelSpace.OfType<DxfTable>().Single();
            Assert.False(table.IsMergedCell(1, 1, out _));

            SameAsReadAnew(
                make,
                t => t.SetTextString(2, 2, "x"),
                t => t.MergeCells(new DxfCellRange(2, 0, 2, 1)),
                t => t.MergeCells(new DxfCellRange(3, 0, 4, 1)),
                t => t.UnmergeCells(new DxfCellRange(2, 0, 2, 0)),
                t => t.MergeCells(new DxfCellRange(3, 2, 5, 3))
            );
        }

        [Fact]
        public void ATableContentWithAComment_IsWrittenWholeByAMerge_AsOnARecordReadAnew()
        {
            // A comment among the groups of the TABLECONTENT is not among the groups the file writes: the merges are written by
            // the whole writing, which leaves the record without it.
            Func<DxfDatabase> make = () =>
            {
                string text = DxfSpeed17Scenarios.TextTable("AC1032", 6, 4, true).ToText();
                int at = Regex.Match(text, @"\r?\n  0\r?\nTABLECONTENT\r?\n").Index;
                int row = text.IndexOf("301\nROW\n", at, StringComparison.Ordinal);
                Assert.True(row > 0);
                return DxfDatabase.Parse(text.Insert(row, "999\nnote\n"));
            };

            SameAsReadAnew(
                make,
                t => t.SetTextString(2, 2, "x"),
                t => t.MergeCells(new DxfCellRange(2, 0, 2, 1)),
                t => t.MergeCells(new DxfCellRange(3, 0, 4, 1)),
                t => t.UnmergeCells(new DxfCellRange(2, 0, 2, 0)),
                t => t.MergeCells(new DxfCellRange(3, 2, 5, 3))
            );
        }

        [Fact]
        public void ACellWithAFormulaMergedAway_LosesItsField_AsOnARecordReadAnew()
        {
            int Fields(DxfDatabase db) => Regex.Matches(db.ToText(), @"\r?\n  0\r?\nFIELD\r?\n").Count;

            DxfDatabase db = DxfSpeed17Scenarios.TextTable("AC1032", 6, 4, false);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            table.SetFormula(2, 1, 0, "=A1+B1");
            table.SetFormula(2, 2, 0, "=C2");
            table.MergeCells(new DxfCellRange(4, 0, 4, 1));
            int before = Fields(db);
            Assert.Equal(2, before);

            table.MergeCells(new DxfCellRange(2, 0, 2, 1));
            Assert.Equal(1, Fields(db));

            SameAsReadAnew(
                () =>
                {
                    DxfDatabase made = DxfSpeed17Scenarios.TextTable("AC1032", 6, 4, true);
                    DxfTable t = made.ModelSpace.OfType<DxfTable>().Single();
                    t.SetFormula(2, 1, 0, "=A1+B1");
                    t.SetFormula(2, 2, 0, "=C2");
                    return DxfDatabase.Parse(made.ToText());
                },
                t => t.MergeCells(new DxfCellRange(4, 0, 4, 1)),
                t => t.MergeCells(new DxfCellRange(2, 0, 2, 1)),
                t => t.UnmergeCells(new DxfCellRange(2, 0, 2, 1))
            );
        }
    }
}
