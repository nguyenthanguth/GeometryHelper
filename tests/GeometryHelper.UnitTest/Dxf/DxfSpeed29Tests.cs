using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables12: the first data link put into a table read from a file reading its TABLECONTENT for the links from
    /// the bytes of the cells it keeps, making the strings of the markers alone; the links read of it kept while its record
    /// stands, so that a height or a text set in the table's own record does not read it again; the kinds of the rows read of
    /// the rows the table keeps of its TABLECONTENT, not of the bytes of its record. The saved bytes and what the table says are
    /// held to what the library gave before, the speed to what it allocates, which does not vary with the machine as a time
    /// does.
    /// </summary>
    public class DxfSpeed29Tests
    {
        // The SHA-256 (24 hex) of what each run of 100 edits saves and says, as the library gave it before the work of tables12.
        private static readonly string[] RunsBefore =
        {
            "AC1032 seed 1 197CCCDD9514BB0B8FF0B679",
            "AC1032 seed 2 D363CD9931DD96CD50AAC040",
            "AC1032 seed 3 6CF745F35F96506052891636",
            "AC1032 seed 4 F3ED44D67156809BA23C5240",
            "AC1032 seed 5 F586A3EDD418436FD774F4C2",
            "AC1032 seed 6 1BF87C905AFFDACB2B7DF042",
            "AC1032 seed 7 6EE6F3A9F1F63C949CCDED75",
            "AC1032 seed 8 162749032D3E582A5C6264FD",
            "AC1027 seed 1 2D265800BD42CF54122DDADA",
            "AC1027 seed 2 F6D01A28F71A37981ECEBE0D",
            "AC1027 seed 3 3396A27B1D11923C5089A531",
            "AC1027 seed 4 FA77E0923899C9DF682E44A6",
            "AC1027 seed 5 FB4F659F41FB68541BE4D9F2",
            "AC1027 seed 6 B5482E2506A54E255C84481F",
            "AC1027 seed 7 E901DC2BDE0C1F53CBC9351D",
            "AC1027 seed 8 9FF2409EA04E040AC35ECA22",
            "AC1021 seed 1 284B8B10A6F4788E4352DF6F",
            "AC1021 seed 2 D5EEA98D29C895826FF295BC",
            "AC1021 seed 3 1F934CE96920683AF4E04C45",
            "AC1021 seed 4 576EC8448A9DD0473A2280BA",
            "AC1021 seed 5 003DB885EAA1FE0DFD7C0E5E",
            "AC1021 seed 6 BE6B87904FC72A120B9C1EEC",
            "AC1021 seed 7 468F77CBC0701957617C54FD",
            "AC1021 seed 8 6A084D9F4171010D6B4BC61B",
            "form 1 seed 1 B3D471D027DCD6D4A408E784",
            "form 1 seed 2 480C710B87D2DE72EA44CAAB",
            "form 1 seed 3 9B60F9DAEFC74CE02372617B",
            "form 2 seed 1 BC2817FDC879A7261862B972",
            "form 2 seed 2 AC56EA434B105DB0E86182ED",
            "form 2 seed 3 3E5FAE7C5DB52FC2A902D2AE",
            "form 3 seed 1 37F3729BB0C058D8BCB48BAC",
            "form 3 seed 2 F19D19EAF6112BA6B3516602",
            "form 3 seed 3 ACFBDDB9B0354DE5DADFB195",
            "form 4 seed 1 7257C5699CBD77F3507A7E1A",
            "form 4 seed 2 5B9EECDF10C114FC907C2927",
            "form 4 seed 3 F3E2EC659B2D8182D521C1FE",
            "form 5 seed 1 403D27936A9C2DA9D7C3FEFD",
            "form 5 seed 2 98870B2A1E859BCE674BE8B7",
            "form 5 seed 3 9B370326507FBD50C00E95E0",
            "form 6 seed 1 403D27936A9C2DA9D7C3FEFD",
            "form 6 seed 2 B485BA763BC1424B95FAE106",
            "form 6 seed 3 9B370326507FBD50C00E95E0",
        };

        [Fact]
        public void RunsOfEdits_AmongLinksHeightsAndRowsAtTheTop_SaveAndSayWhatTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed29Scenarios.Runs());
        }

        [Theory]
        [InlineData("AC1032", 3, 0)]
        [InlineData("AC1032", 7, 0)]
        [InlineData("AC1021", 5, 0)]
        [InlineData("AC1027", 11, 0)]
        [InlineData("AC1032", 4, 1)]
        [InlineData("AC1032", 6, 2)]
        [InlineData("AC1032", 8, 3)]
        [InlineData("AC1021", 9, 4)]
        [InlineData("AC1032", 12, 5)]
        [InlineData("AC1021", 13, 6)]
        public void WhatATableSaysOfItsLinksAndRows_AfterEachEditOfARun_IsWhatATableReadAnewFromItsTextSays(
            string version,
            int seed,
            int form
        )
        {
            // The form is how the TABLECONTENT stands in the file: as the library writes it, with a comment among its groups,
            // with extended data after, with carriage returns, with its codes not padded, with an escape in a text, and with a
            // letter beyond ASCII in one. A table read anew has nothing kept of its TABLECONTENT, so that what is kept of it
            // between two questions of the table edited is found stale where the two differ.
            Assert.Equal(string.Empty, DxfSpeed29Scenarios.Mismatches(version, seed, 60, 9, 6, form));
        }

        [Fact]
        public void TheFirstLink_PutIntoATableOf1800CellsReadFromAFile_AllocatesLessThan24Megabytes()
        {
            // The links of the TABLECONTENT were read from the groups of every cell, 150 000 strings of them, to put the first link
            // in: 31.0 MB on .NET Framework 4.8 (18.3 MB now, the groups of the cells that are not changed left in their bytes).
            long allocated = DxfSpeed25Scenarios.Allocated(DxfSpeed29Scenarios.FirstLink, false);

            Assert.True(allocated < 24_000_000L, "first link allocated " + allocated);
        }

        [Fact]
        public void TwentyHeightsSetEachFollowedByWhatTheTableSaysOfItsLinks_OfATableOf1800Cells_AllocateLessThan100Megabytes()
        {
            // What the table said of its links was read again from the TABLECONTENT after each height set, though the height is in
            // the table's own record: 336.3 MB for these 20 heights and 40 questions, of which the heights are 41 MB (59.6 MB now,
            // the TABLECONTENT read for its links once).
            long allocated = DxfSpeed25Scenarios.Allocated(DxfSpeed29Scenarios.HeightsThenAsked, false);

            Assert.True(allocated < 100_000_000L, "heights and questions allocated " + allocated);
        }

        [Fact]
        public void ALinkPutIn_ThenAHeightSetAndARowAboveItTakenOut_IsOnTheCellsItMovedTo()
        {
            DxfDatabase db = DxfSpeed25Scenarios.Table("AC1032", 20, 5);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            var link = (DxfDataLink)db.GetObject("FFF01");
            table.SetDataLink(new DxfCellRange(4, 1, 5, 2), link);

            table.SetRowHeight(7, 12.0);

            Assert.Same(link, table.GetDataLink(5, 2));
            Assert.Equal(new DxfCellRange(4, 1, 5, 2), table.GetDataLinkRange(5, 1));

            table.DeleteRows(1, 1);

            Assert.True(table.IsLinked(3, 1));
            Assert.Same(link, table.GetDataLink(4, 2));
            Assert.Equal(new DxfCellRange(3, 1, 4, 2), table.GetDataLinkRange(4, 1));
        }
    }
}
