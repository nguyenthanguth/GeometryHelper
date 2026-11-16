using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables10: the search of a file for what points at a field a cell lets go of (<c>PointedAmong</c>) passing
    /// over the records that cannot hold a pointer to a handle of digits alone, as it did for a handle with a letter, and the
    /// rows and columns put in written with fewer copies. The speed is held by the records read group by group, which do not
    /// vary with the machine as a time does; the results are held to what the library gave before.
    /// </summary>
    public class DxfSpeed22Tests
    {
        // The SHA-256 (24 hex) of the file each run of 120 edits saves, as the library gave it before the work of tables10.
        private static readonly string[] RunsBefore =
        {
            "AC1032 from 9000 seed 1 text plain 0F5169134C66CA7A5AA43A11",
            "AC1032 from 9000 seed 1 text content 0329489B19C92A39758FD79E",
            "AC1032 from 9000 seed 1 blocks plain 4D8F9063264076B1EE9204A0",
            "AC1032 from 9000 seed 1 blocks content 692D6A5BCC6A42BEA76F8FF1",
            "AC1032 from 9000 seed 2 text plain 401357FE3844D2C0A0415257",
            "AC1032 from 9000 seed 2 text content 7E9802FFEC70F53E7E47D7FF",
            "AC1032 from 9000 seed 2 blocks plain FA2734F8838BA5A3D4B9000D",
            "AC1032 from 9000 seed 2 blocks content BA06A7996A11F1072FC7244A",
            "AC1032 from 70000 seed 1 text plain 39F29A42CC98DC155B2E05C6",
            "AC1032 from 70000 seed 1 text content E11BCDCB870125DDB0D3BDB0",
            "AC1032 from 70000 seed 1 blocks plain 70F0D6CCF6B47AF9D6925350",
            "AC1032 from 70000 seed 1 blocks content 64F2C74E355C34DC0652CF07",
            "AC1032 from 70000 seed 2 text plain 9946D88B84F821BB34DADCF0",
            "AC1032 from 70000 seed 2 text content 1FF7C9A6667E19F0A158E122",
            "AC1032 from 70000 seed 2 blocks plain 090A04D1BC4CBB1CE5A3D8BB",
            "AC1032 from 70000 seed 2 blocks content 0BD670C7A5527FF2C0618542",
            "AC1021 from 9000 seed 1 text plain 47119C5BD8D6983D5DDF20E4",
            "AC1021 from 9000 seed 1 text content 7EE3553D14BF65EFE0E500D4",
            "AC1021 from 9000 seed 1 blocks plain 1F8BD43EB3698E7DB2F3B09F",
            "AC1021 from 9000 seed 1 blocks content 976977AF0FC47B429904C991",
            "AC1021 from 9000 seed 2 text plain 6447404BE2B3087C53EF39D5",
            "AC1021 from 9000 seed 2 text content A591923C80CC86259CDF96BD",
            "AC1021 from 9000 seed 2 blocks plain 4AD9BC598F7665873B9E0788",
            "AC1021 from 9000 seed 2 blocks content DA9BBA03F2DC7447E8271791",
            "AC1021 from 70000 seed 1 text plain 971546A9ECEB54E237FBA082",
            "AC1021 from 70000 seed 1 text content FC8681778F11EB40FAFE31E2",
            "AC1021 from 70000 seed 1 blocks plain 0B38B5BB05BACC65443532A0",
            "AC1021 from 70000 seed 1 blocks content 0AD4213993C8271528CB0DA8",
            "AC1021 from 70000 seed 2 text plain 4D51489B00796645CF4ECF64",
            "AC1021 from 70000 seed 2 text content 834F6294336A32341FF54F75",
            "AC1021 from 70000 seed 2 blocks plain 096FAF7FE6E7A496AE2BDE4A",
            "AC1021 from 70000 seed 2 blocks content E2C340933CAC2C2965182693",
            "AC1018 from 9000 seed 1 text plain 1D656F15B9696E51E5D690E8",
            "AC1018 from 9000 seed 1 text content 1D656F15B9696E51E5D690E8",
            "AC1018 from 9000 seed 1 blocks plain E42D4C752B560B7A4A9BF201",
            "AC1018 from 9000 seed 1 blocks content E42D4C752B560B7A4A9BF201",
            "AC1018 from 9000 seed 2 text plain 019D22692BAF70F9462C94BE",
            "AC1018 from 9000 seed 2 text content 019D22692BAF70F9462C94BE",
            "AC1018 from 9000 seed 2 blocks plain 0898840FBDC76DDFAA57CD67",
            "AC1018 from 9000 seed 2 blocks content 0898840FBDC76DDFAA57CD67",
            "AC1018 from 70000 seed 1 text plain 08AA5A3707EA2C48B8D00DFB",
            "AC1018 from 70000 seed 1 text content 08AA5A3707EA2C48B8D00DFB",
            "AC1018 from 70000 seed 1 blocks plain EE9E7E3C50723C94D2B7C8E8",
            "AC1018 from 70000 seed 1 blocks content EE9E7E3C50723C94D2B7C8E8",
            "AC1018 from 70000 seed 2 text plain D026BF5B01AC64DFC4630BFD",
            "AC1018 from 70000 seed 2 text content D026BF5B01AC64DFC4630BFD",
            "AC1018 from 70000 seed 2 blocks plain 5089C2FE52E6516FDD1431B4",
            "AC1018 from 70000 seed 2 blocks content 5089C2FE52E6516FDD1431B4",
        };

        // The SHA-256 (24 hex) of what each search of a file of random records gave before the work of tables10.
        private static readonly string[] SearchesBefore =
        {
            "seed 1 wanted 1 45E27A0335EF42BD18A7CB08",
            "seed 1 wanted 2 52078BE19C573ADCD1B98CBC",
            "seed 1 wanted 3 FA3C0CDFCD2575B7742DB304",
            "seed 1 wanted 4 24D8AF1769444665FA0A0E36",
            "seed 1 wanted 5 7C6379F48B790473144A519A",
            "seed 1 wanted 6 77E658E396AFE277A98B662D",
            "seed 2 wanted 1 17F70085CE8F1648BBEBD690",
            "seed 2 wanted 2 7D980C9C1CBD5014FA40E09F",
            "seed 2 wanted 3 067EA43AFEDFC12CDED0918B",
            "seed 2 wanted 4 AF90474D8294B68D3158D1C2",
            "seed 2 wanted 5 2ADC090BA4181AE0A402193F",
            "seed 2 wanted 6 1EBD85F034295961A187EBAE",
            "seed 3 wanted 1 B009CE5089FF362D3E14757D",
            "seed 3 wanted 2 919A4965F3EE754E239B330C",
            "seed 3 wanted 3 020FF516B844CB8BC9FE8ECF",
            "seed 3 wanted 4 430F27042714F5DAA1CFACB1",
            "seed 3 wanted 5 071095218B689FDB20DD7D62",
            "seed 3 wanted 6 D53A64C7EB60466A51A10BE0",
            "seed 4 wanted 1 574BF4F36919AF2FFBBE586D",
            "seed 4 wanted 2 875C167834F6288CADD1E983",
            "seed 4 wanted 3 A38F7A4C66D3283D375797E2",
            "seed 4 wanted 4 682505AA7BA335E082702DA2",
            "seed 4 wanted 5 28C49B917733D389B849D48A",
            "seed 4 wanted 6 BBED14080FB8483CBA791259",
            "seed 5 wanted 1 067BFE80ADD17B4BD66D6A41",
            "seed 5 wanted 2 E936B72798B60D0920EF28AA",
            "seed 5 wanted 3 61A7F0EBE07FDA54C9737A7D",
            "seed 5 wanted 4 49E55AED9EEB0AF28CBA9A01",
            "seed 5 wanted 5 BC9045319DFDA9D909B8AE2F",
            "seed 5 wanted 6 BEF48BF3A7BA4D20DDB447A2",
            "seed 6 wanted 1 44E3E2F9B773CC27806B7978",
            "seed 6 wanted 2 BD4641B61A1913057ADA4E07",
            "seed 6 wanted 3 EA3C51DC1CDD95A9131730CB",
            "seed 6 wanted 4 F5A118B11F83A5326EB932C9",
            "seed 6 wanted 5 4FEA8891F8FCCA83C6D471B1",
            "seed 6 wanted 6 BF06E8735C0585605605770A",
            "seed 7 wanted 1 7066CF75598B47B57FA99FBD",
            "seed 7 wanted 2 7E6EC7616B0BB2DB741F8CED",
            "seed 7 wanted 3 3726B35069D4C8B6F6396F1B",
            "seed 7 wanted 4 CEAB025B55EEC36121125554",
            "seed 7 wanted 5 18554E974F5A08DE6B8EA830",
            "seed 7 wanted 6 C96D4F2CDD8F6EB7644EF120",
            "seed 8 wanted 1 0DFCCB24D552CFCFDE1E70F1",
            "seed 8 wanted 2 3390D3EBD095560E21433568",
            "seed 8 wanted 3 32FAF0ED11954F39F5C36D62",
            "seed 8 wanted 4 C416EC21950A5631402E9B16",
            "seed 8 wanted 5 E2910435F7E7E85C33ACE4B9",
            "seed 8 wanted 6 14412D62282578AF354710D1",
            "seed 9 wanted 1 BC63C5D0157E2926774C94B3",
            "seed 9 wanted 2 8CA1EECCB2E1D080692C47A2",
            "seed 9 wanted 3 8B81F834A317945E598FFE49",
            "seed 9 wanted 4 511F4C97EFE7C4038989D9AF",
            "seed 9 wanted 5 6888DFCF0A05476118921294",
            "seed 9 wanted 6 29984409B6B423E63FF893A4",
            "seed 10 wanted 1 30293D1F419D9C0F2D9B3B2C",
            "seed 10 wanted 2 2B201F5C031497B8FB832BDC",
            "seed 10 wanted 3 BA801CC86AAF7216203E6A0C",
            "seed 10 wanted 4 4D23CC4423FCAD9D2760198C",
            "seed 10 wanted 5 2CC88950CC4E897788A7D049",
            "seed 10 wanted 6 7F29AE690EEB08B723995238",
        };

        [Fact]
        public void RunsOfEdits_AmongFieldsNumberedFromDigitsAlone_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed22Scenarios.Runs());
        }

        [Fact]
        public void SearchesOfRandomRecords_ForHandlesOfDigitsAndLetters_GiveWhatTheyDidBefore()
        {
            Assert.Equal(SearchesBefore, DxfSpeed22Scenarios.PointedRuns());
        }

        [Theory]
        [InlineData("AC1032", 3, false, false, "9000")]
        [InlineData("AC1032", 8, true, false, "9000")]
        [InlineData("AC1032", 14, false, true, "70000")]
        [InlineData("AC1032", 21, true, true, "70000")]
        [InlineData("AC1027", 6, true, false, "300")]
        [InlineData("AC1021", 5, true, false, "9000")]
        [InlineData("AC1021", 17, true, true, "70000")]
        [InlineData("AC1018", 9, false, false, "9000")]
        public void RunsOfEdits_AmongFieldsNumberedFromDigitsAlone_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed,
            bool content,
            bool blocks,
            string nextHandle
        )
        {
            Assert.Equal(
                DxfSpeed22Scenarios.Run(version, seed, 60, true, content, blocks, false, nextHandle),
                DxfSpeed22Scenarios.Run(version, seed, 60, false, content, blocks, false, nextHandle)
            );
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
        [InlineData("901", "902", "901", true)]
        [InlineData("901", "902", "0901", true)]
        [InlineData("901", "902", "000901", true)]
        [InlineData("901", "902", " 901", true)]
        [InlineData("901", "902", "901 ", true)]
        [InlineData("901", "902", "\t901", true)]
        [InlineData("901", "902", "901\t", true)]
        [InlineData("901", "902", " 0901 ", true)]
        [InlineData("901", "902", "902", false)]
        [InlineData("901", "902", "9010", false)]
        [InlineData("901", "902", "1901", false)]
        [InlineData("901", "902", "901.5", false)]
        [InlineData("901", "902", "901A", false)]
        [InlineData("901", "902", "G901", false)]
        [InlineData("901", "902", "901901", false)]
        [InlineData("901", "902", null, false)]
        [InlineData("9001", "9002", "9001", true)]
        [InlineData("9001", "9002", "009001", true)]
        [InlineData("9001", "9002", "90010", false)]
        [InlineData("90001", "90002", "90001", true)]
        [InlineData("90001", "90002", "090001", true)]
        [InlineData("90001", "90002", "900010", false)]
        [InlineData("900001", "900002", "900001", true)]
        [InlineData("900001", "900002", "0900001", true)]
        [InlineData("900001", "900002", "9000011", false)]
        [InlineData("900001", "900002", "900002", false)]
        public void AFieldOfDigitsAloneACellLetsGo_IsLeftWhereARecordPointsAtItInAnyFormAValueCanTake(
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
        public void TwentyRowsPutInAndTakenOutOf931Cells_AllocateLessThan193Megabytes()
        {
            // Each cell put in made its 49 groups one by one, a number written out for each, and the layout of the record made was
            // copied once more than it had to be: 199.6 MB for these 40 edits (186.8 MB now).
            DxfDatabase db = DxfSpeed18Scenarios.Table("AC1032", 49, 19, true, false);
            long allocated = DxfSpeed21Scenarios.Allocated(db, DxfSpeed21Scenarios.RowsInAndOut);

            Assert.Equal(49, db.ModelSpace.OfType<DxfTable>().Single().NumRows);
            Assert.True(allocated < 193_000_000L, "rows allocated " + allocated);
        }

        [Fact]
        public void TwentyMergesAndUnmerges_OfCellsOf931_AllocateLessThan64Megabytes()
        {
            // The cells a merge wrote were put into the record (the 1.1 MB of it copied) to read the table's extension dictionary
            // from its first groups, which the cells do not follow: 66.9 MB for these 40 edits (61.9 MB now).
            DxfDatabase db = DxfSpeed18Scenarios.Table("AC1032", 49, 19, true, false);
            long allocated = DxfSpeed21Scenarios.Allocated(db, table =>
            {
                for (int k = 0; k < 20; k++)
                {
                    var range = new DxfCellRange(3 + k, 2, 3 + k, 3);
                    table.MergeCells(range);
                    table.UnmergeCells(range);
                }
            });

            Assert.True(allocated < 64_000_000L, "merges allocated " + allocated);
        }

        [Theory]
        [InlineData("700000", 10, 8)]
        [InlineData("7000A0", 10, 8)]
        public void TenMergesAcrossACellHoldingAField_ReadFewRecordsGroupByGroup_WhateverTheFieldsHandleIs(
            string nextHandle,
            int merges,
            int mostRecordsEach
        )
        {
            // Each merge lets a field go, and the search for what else points at it read every record of the file, the table's
            // two of megabytes among them, group by group, where its handle was of digits alone: about 300 records for each.
            DxfDatabase db = DxfSpeed22Scenarios.Table("AC1032", 49, 19, true, false, nextHandle);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            table.SetTextHeight(0, 1, 2.0);

            for (int k = 0; k < merges; k++)
            {
                table.SetFormula(3 + k, (k * 2) % 17 + 1, 0, "=A1+B1");
            }

            int before = db.File.RecordsWalked;

            for (int k = 0; k < merges; k++)
            {
                table.MergeCells(new DxfCellRange(3 + k, (k * 2) % 17, 3 + k, (k * 2) % 17 + 1));
            }

            Assert.InRange(db.File.RecordsWalked - before, 1, merges * mostRecordsEach);
        }
    }
}
