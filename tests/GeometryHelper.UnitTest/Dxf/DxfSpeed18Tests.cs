using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables8: the TABLECONTENT of a table written into the bytes of the cells and rows an edit changes (the rest
    /// copied as it stands) where each edit read and wrote its 100 000 groups, and the layout of a table kept after the record
    /// is written, where rows and columns put in and taken out parsed it again. The speed is held by the bytes allocated, which
    /// do not vary with the machine as a time does; the bytes saved are held to what the library gave before.
    /// </summary>
    public class DxfSpeed18Tests
    {
        // The SHA-256 (24 hex) of the file each run of 120 edits saves, as the library gave it before the work of tables8.
        private static readonly string[] RunsBefore =
        {
            "AC1032 seed 1 text plain 2BA7760926F361E03BCB242D",
            "AC1032 seed 1 text content DAF5B022E0B1CB4CEAE20D9A",
            "AC1032 seed 1 blocks plain E97F92AA1B0C8442CEA67A78",
            "AC1032 seed 1 blocks content 89774ACF5AE1E49A5F536ACE",
            "AC1032 seed 2 text plain 20CB251437E5FFEFD00B90ED",
            "AC1032 seed 2 text content 82F7264F37F0A7DB4B329626",
            "AC1032 seed 2 blocks plain BF261AC984F3441EF4CAABD0",
            "AC1032 seed 2 blocks content C85315A4FA10754C3F9C7F06",
            "AC1032 seed 3 text plain 9C82284C59BBBF231DF586AC",
            "AC1032 seed 3 text content E8B41450D34131B60259CFE6",
            "AC1032 seed 3 blocks plain 1DC03D8315538D58ADE2027F",
            "AC1032 seed 3 blocks content 1B864C300A795E474890A684",
            "AC1032 seed 4 text plain 060C166DE5E742739842E455",
            "AC1032 seed 4 text content 21087F2C625AA84F79A499E7",
            "AC1032 seed 4 blocks plain 5CFA26680E380C4042AADCD2",
            "AC1032 seed 4 blocks content F57B33798E753EEF12BD4282",
            "AC1027 seed 1 text plain 2D692B68E8A9125FB7764422",
            "AC1027 seed 1 text content 0D95F2F0F77B49E4753C20BD",
            "AC1027 seed 1 blocks plain 1C520945B90D3C17DC15EBD8",
            "AC1027 seed 1 blocks content 7A6841BCCF909D066198B020",
            "AC1027 seed 2 text plain AD5ECC0A67FF337258A8A87B",
            "AC1027 seed 2 text content F90AB749FBB08C945C70C628",
            "AC1027 seed 2 blocks plain E082607C9C21201BFA4874BF",
            "AC1027 seed 2 blocks content 4B8FE1FB403CA92BC2AA4E52",
            "AC1027 seed 3 text plain 835944A501B93BF137A80380",
            "AC1027 seed 3 text content 077A78E0F7537D2D1C4A359E",
            "AC1027 seed 3 blocks plain 146496E76DE2E8FB5DDD4092",
            "AC1027 seed 3 blocks content 903D2FFFD8934DAC15DDB449",
            "AC1027 seed 4 text plain 4D13F72AA55C9E244FA093ED",
            "AC1027 seed 4 text content FFC064DF2BE81B566BE1B8C3",
            "AC1027 seed 4 blocks plain A4905101FB3507FF124854CC",
            "AC1027 seed 4 blocks content B16355CFAAB12B321DEC5E28",
            "AC1021 seed 1 text plain 23B3254E6EA9B025D229C58E",
            "AC1021 seed 1 text content A8E83EEDE32360A8ED9858BA",
            "AC1021 seed 1 blocks plain DAC6030C32BB4ADF635F5360",
            "AC1021 seed 1 blocks content F01837B3C101765308B096E0",
            "AC1021 seed 2 text plain 582E0CF33462ABF965F68132",
            "AC1021 seed 2 text content 618C20AA40E1CE3130988E1D",
            "AC1021 seed 2 blocks plain AD81E551E0282783813AA788",
            "AC1021 seed 2 blocks content 1B42E66EFD1250E3DF00784D",
            "AC1021 seed 3 text plain EDB21C53B1E607ED6747A483",
            "AC1021 seed 3 text content F42837951BB805DE161DEB1E",
            "AC1021 seed 3 blocks plain E08BFE4CA50D134499F0CD57",
            "AC1021 seed 3 blocks content 6A20E86C742A3FC1DE39F343",
            "AC1021 seed 4 text plain A12FD2D08C6B06A068A64EEE",
            "AC1021 seed 4 text content 79C463E63CE24AF899F746F2",
            "AC1021 seed 4 blocks plain 4EB026084C62A844CEB380B1",
            "AC1021 seed 4 blocks content BE113C3BC9DBECBAB8F1761E",
            "AC1018 seed 1 text plain BD2D16B376E630654A1556A1",
            "AC1018 seed 1 text content BD2D16B376E630654A1556A1",
            "AC1018 seed 1 blocks plain 0973E18D33CC5A1C1E7F4B92",
            "AC1018 seed 1 blocks content 0973E18D33CC5A1C1E7F4B92",
            "AC1018 seed 2 text plain 2174D690C32B4B949632242A",
            "AC1018 seed 2 text content 2174D690C32B4B949632242A",
            "AC1018 seed 2 blocks plain 0DAACB675568AD5EB219D921",
            "AC1018 seed 2 blocks content 0DAACB675568AD5EB219D921",
            "AC1018 seed 3 text plain B91A39DE058BE814A2134EDA",
            "AC1018 seed 3 text content B91A39DE058BE814A2134EDA",
            "AC1018 seed 3 blocks plain 1520D1B7F5D470B95FE1FAE0",
            "AC1018 seed 3 blocks content 1520D1B7F5D470B95FE1FAE0",
            "AC1018 seed 4 text plain 3071DDDF18D8CA105610970D",
            "AC1018 seed 4 text content 3071DDDF18D8CA105610970D",
            "AC1018 seed 4 blocks plain 03C4061F02E639559FC28B9A",
            "AC1018 seed 4 blocks content 03C4061F02E639559FC28B9A",
        };

        // The same for each run of 60 edits of the table AutoCAD 2021 wrote linked to data.
        private static readonly string[] LinkedRunsBefore =
        {
            "R2018 rows 3 plain seed 1 67B09ED37C12321C81832651",
            "R2018 rows 3 plain seed 2 00F47B351541908EB268554F",
            "R2018 rows 3 plain seed 3 D9ED0DA13A4BD89DCB986FC0",
            "R2018 rows 3 plain seed 4 0D0731C6E8ADA1F90F792325",
            "R2018 rows 3 field seed 1 DB3036D3C8F7DBDA0E57CA53",
            "R2018 rows 3 field seed 2 00F47B351541908EB268554F",
            "R2018 rows 3 field seed 3 D9ED0DA13A4BD89DCB986FC0",
            "R2018 rows 3 field seed 4 0D0731C6E8ADA1F90F792325",
            "R2018 rows 6 plain seed 1 DA56B7E251BD6D72450809E5",
            "R2018 rows 6 plain seed 2 2ECD8E72A08DA90B1AFD14CE",
            "R2018 rows 6 plain seed 3 5C19F4D487E8E9A3DE92F918",
            "R2018 rows 6 plain seed 4 1A2CE67C0CEA6EEDB26CD314",
            "R2018 rows 6 field seed 1 E4C20DC00990F70304094D4B",
            "R2018 rows 6 field seed 2 2ECD8E72A08DA90B1AFD14CE",
            "R2018 rows 6 field seed 3 5C19F4D487E8E9A3DE92F918",
            "R2018 rows 6 field seed 4 1A2CE67C0CEA6EEDB26CD314",
            "R2007 rows 3 plain seed 1 C22BB8165EC81C620813E1CF",
            "R2007 rows 3 plain seed 2 FBC10A48437915105A4C403A",
            "R2007 rows 3 plain seed 3 A4CDAD0839E1DBBD2C1F8469",
            "R2007 rows 3 plain seed 4 E364DDAA1E8E4780AB902255",
            "R2007 rows 3 field seed 1 4A91F2CEB23FF1DAAE13E282",
            "R2007 rows 3 field seed 2 FBC10A48437915105A4C403A",
            "R2007 rows 3 field seed 3 A4CDAD0839E1DBBD2C1F8469",
            "R2007 rows 3 field seed 4 E364DDAA1E8E4780AB902255",
            "R2007 rows 6 plain seed 1 7287886D6A7CA6F2B0C773C3",
            "R2007 rows 6 plain seed 2 8EEAC62AE6FBBDF880381B34",
            "R2007 rows 6 plain seed 3 F0D0A1574F03D06073794CC2",
            "R2007 rows 6 plain seed 4 0AAC7692B09DF3F002C4BB02",
            "R2007 rows 6 field seed 1 F512A8D7F0FFFC0E19A23749",
            "R2007 rows 6 field seed 2 8EEAC62AE6FBBDF880381B34",
            "R2007 rows 6 field seed 3 F0D0A1574F03D06073794CC2",
            "R2007 rows 6 field seed 4 0AAC7692B09DF3F002C4BB02",
        };

        [Fact]
        public void RunsOfEdits_WithRowsColumnsMergesAndResets_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed18Scenarios.Runs());
        }

        [Fact]
        public void RunsOfEditsOfATableAutoCadWroteLinked_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(LinkedRunsBefore, DxfSpeed18Scenarios.LinkedRuns());
        }

        [Theory]
        [InlineData("AC1032", 3, false, false)]
        [InlineData("AC1032", 11, true, false)]
        [InlineData("AC1032", 24, false, true)]
        [InlineData("AC1032", 37, true, true)]
        [InlineData("AC1032", 41, true, false)]
        [InlineData("AC1027", 6, true, false)]
        [InlineData("AC1027", 19, true, true)]
        [InlineData("AC1021", 5, true, false)]
        [InlineData("AC1021", 29, false, true)]
        [InlineData("AC1021", 43, true, true)]
        [InlineData("AC1018", 12, false, false)]
        [InlineData("AC1018", 33, false, true)]
        public void RunsOfEdits_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed,
            bool content,
            bool blocks
        )
        {
            Assert.Equal(
                DxfSpeed18Scenarios.Run(version, seed, 60, true, 8, 6, content, blocks, false),
                DxfSpeed18Scenarios.Run(version, seed, 60, false, 8, 6, content, blocks, false)
            );
        }

        [Theory]
        [InlineData(3, false, 5)]
        [InlineData(3, true, 9)]
        [InlineData(6, false, 14)]
        [InlineData(6, true, 17)]
        public void RunsOfEditsOfATableAutoCadWroteLinked_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            int rows,
            bool formula,
            int seed
        )
        {
            Assert.Equal(
                DxfSpeed18Scenarios.RunLinked(DxfDimensionDrawings.R2018, rows, formula, seed, 40, true, false),
                DxfSpeed18Scenarios.RunLinked(DxfDimensionDrawings.R2018, rows, formula, seed, 40, false, false)
            );
        }

        private const int Rows = 49;
        private const int Columns = 19;

        private static DxfTable TableOf(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        /// <summary>The bytes the edits given allocate on a table of 49 rows and 19 columns, the first edit of it (which writes the rows AutoCAD merges on load) made before.</summary>
        private static long Allocated(DxfDatabase db, Action<DxfTable> edits)
        {
            DxfTable table = TableOf(db);
            table.SetTextHeight(0, 1, 2.0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            edits(table);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void TheResetOfEveryBlockCellOf931CellsOfAFile_AllocatesLessThan1500Megabytes()
        {
            // Each reset took the block's content out of the TABLECONTENT, which read and wrote its 100 000 groups: 12.7 GB for
            // these 456 resets of a table of 49 rows by 19 columns (0.5 GB now, the bytes of the TABLECONTENT copied once each).
            DxfDatabase db = DxfSpeed18Scenarios.Table("AC1032", Rows, Columns, true, true);
            long allocated = Allocated(db, t =>
            {
                for (int r = 1; r < Rows; r++)
                {
                    for (int c = 0; c < Columns; c++)
                    {
                        if ((r + c) % 2 == 0)
                        {
                            t.ResetValue(r, c);
                        }
                    }
                }
            });

            Assert.True(allocated < 1_500_000_000L, "resets allocated " + allocated);
        }

        [Fact]
        public void TwentyRowsPutInAndTakenOutOf931CellsOfAFile_AllocateLessThan400Megabytes()
        {
            // The rows go into the TABLECONTENT and the record, each read and written whole: 1.8 GB for these 40 edits (0.3 GB
            // now).
            DxfDatabase db = DxfSpeed18Scenarios.Table("AC1032", Rows, Columns, true, false);
            long allocated = Allocated(db, t =>
            {
                for (int k = 0; k < 20; k++)
                {
                    t.InsertRows(5 + (k * 3) % 20, 8.0, 1);
                }

                for (int k = 0; k < 20; k++)
                {
                    t.DeleteRows(10 + k % 7, 1);
                }
            });

            Assert.Equal(Rows, TableOf(db).NumRows);
            Assert.True(allocated < 400_000_000L, "rows allocated " + allocated);
        }

        [Fact]
        public void EightColumnsPutInAndTakenOutOf931CellsOfAFile_AllocateLessThan250Megabytes()
        {
            // 0.68 GB for these 16 edits (0.13 GB now).
            DxfDatabase db = DxfSpeed18Scenarios.Table("AC1032", Rows, Columns, true, false);
            long allocated = Allocated(db, t =>
            {
                for (int k = 0; k < 8; k++)
                {
                    t.InsertColumns(2 + (k * 3) % 9, 30.0, 1);
                }

                for (int k = 0; k < 8; k++)
                {
                    t.DeleteColumns(3 + k % 4, 1);
                }
            });

            Assert.Equal(Columns, TableOf(db).NumColumns);
            Assert.True(allocated < 250_000_000L, "columns allocated " + allocated);
        }

        /// <summary>
        /// The same edits made on a table in place and on a table read anew from its text before each one, the text of the
        /// drawing compared after each edit.
        /// </summary>
        private static void SameAsReadAnew(Func<DxfDatabase> make, params Action<DxfTable>[] edits)
        {
            DxfDatabase inPlace = make();
            DxfDatabase anew = make();
            DxfTable table = TableOf(inPlace);

            foreach (Action<DxfTable> edit in edits)
            {
                anew = DxfDatabase.Parse(anew.ToText());
                edit(TableOf(anew));
                edit(table);
                Assert.Equal(anew.ToText(), inPlace.ToText());
            }
        }

        [Fact]
        public void ACellOfATableContentThatHoldsAFieldMergedAway_LosesItsField_AsOnARecordReadAnew()
        {
            // The linked cell (1, 0) of the table AutoCAD wrote holds a field in its TABLECONTENT and in the ACAD_TABLE; a merge
            // whose top left cell is (0, 0) empties it, the fields it owned let go from the list after the rows and erased.
            string drawing = DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018, 4, true);
            Func<DxfDatabase> make = () => DxfDatabase.Parse(drawing);
            int Fields(DxfDatabase db) => Regex.Matches(db.ToText(), @"\r?\n  0\r?\nFIELD\r?\n").Count;
            DxfDatabase made = make();
            Assert.Equal(2, Fields(made));

            TableOf(made).MergeCells(new DxfCellRange(0, 0, 1, 1));

            // The file the library gave before the work of tables8.
            Assert.Equal(0, Fields(made));
            Assert.Equal(
                "B381A2744848D21892E19496",
                DxfSpeed12Scenarios.Hash(System.Text.Encoding.UTF8.GetBytes(made.ToText()))
            );

            SameAsReadAnew(
                make,
                t => t.MergeCells(new DxfCellRange(0, 0, 1, 1)),
                t => t.DeleteRows(2, 1),
                t => t.InsertRows(2, 8.0, 1),
                t => t.UnmergeCells(new DxfCellRange(0, 0, 1, 1))
            );
        }

        [Fact]
        public void ATableContentWithAComment_IsWrittenWholeByRowsColumnsAndResets_AsOnARecordReadAnew()
        {
            // A comment among the groups of the TABLECONTENT is not among those the file writes: each edit writes the record
            // whole, which leaves it without the comment.
            Func<DxfDatabase> make = () =>
            {
                string text = DxfSpeed18Scenarios.Table("AC1032", 6, 4, true, true).ToText();
                int at = Regex.Match(text, @"\r?\n  0\r?\nTABLECONTENT\r?\n").Index;
                int row = text.IndexOf("301\nROW\n", at, StringComparison.Ordinal);
                Assert.True(row > 0);
                return DxfDatabase.Parse(text.Insert(row, "999\nnote\n"));
            };

            SameAsReadAnew(
                make,
                t => t.SetTextString(2, 1, "x"),
                t => t.ResetValue(2, 2),
                t => t.InsertRows(3, 8.0, 2),
                t => t.DeleteRows(2, 1),
                t => t.InsertColumns(2, 30.0, 1),
                t => t.MergeCells(new DxfCellRange(3, 0, 4, 1)),
                t => t.DeleteColumns(1, 1)
            );
        }

        [Fact]
        public void ABlockCellReset_BetweenEditsOfTheStructure_LeavesTheTableContentAsOneReadAnew()
        {
            SameAsReadAnew(
                () => DxfSpeed18Scenarios.Table("AC1032", 8, 6, true, true),
                t => t.ResetValue(2, 2),
                t => t.InsertRows(3, 8.0, 1),
                t => t.ResetValue(4, 3),
                t => t.MergeCells(new DxfCellRange(2, 1, 3, 2)),
                t => t.InsertColumnsAndInherit(2, 1, 1),
                t => t.ResetValue(5, 1),
                t => t.DeleteRows(3, 2),
                t => t.UnmergeCells(new DxfCellRange(2, 1, 2, 1)),
                t => t.InsertRowsAndInherit(4, 3, 2),
                t => t.DeleteColumns(2, 1)
            );
        }
    }
}
