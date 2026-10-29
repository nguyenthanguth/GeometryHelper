using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The data links of the ACAD_TABLE of <see cref="DxfDimensionDrawings"/> (R2007 and R2018, three rows 10, 8 and 8 high,
    /// two columns 40 and 30 wide, its title Parts merged across both, a block cell of BOLT at (2, 1)) as AutoCAD's
    /// <c>Table</c> keeps them: a range of cells linked to a DATALINK of the drawing's ACAD_DATALINK, named by its top left
    /// cell, read, moved with the rows and columns, and let go of. The library writes no new link: the drawings here are
    /// written as text.
    /// </summary>
    public class DxfTableDataLinkTests
    {
        // The drawing of R2018 read as one of R2007, whose cells AutoCAD writes in the same form.
        private static readonly string R2007 = DxfDimensionDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1021\n");

        private static readonly string[] Versions = { R2007, DxfDimensionDrawings.R2018 };

        // The cells (1, 0) Mark, (1, 1) Qty and (2, 0) B-12, before their values.
        private const string MarkCell = " 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n";
        private const string QtyCell = " 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n";
        private const string B12Cell = " 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n";

        // The block cell (2, 1) of BOLT: its 92 and the 340 of its block.
        private const string BoltCell = " 92\n0\n340\n48\n";

        // The options of a link: PersistCache, and Anonymous with it.
        private const int Named = 2;
        private const int Anonymous = 3;

        /// <summary>
        /// A drawing with a data link in the layout this library guesses AutoCAD writes, not one AutoCAD has been seen to write:
        /// ACAD_DATALINK (A00) holding Parts (A01), its groups as LibreDWG gives a DATALINK in DXF, of the options given, updated
        /// on 20 October 2026 at 13:31:10.484 and owning an XRECORD (A02); each cell given names it after its 92, made odd, by
        /// the run LibreDWG gives a TABLECONTENT cell's linked data (340, 93, 94, 96), of the rows and columns given.
        /// </summary>
        private static string Linked(string text, int option, params (string Cell, int Rows, int Columns)[] cells)
        {
            string linked = text
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\nA03\n")
                .Replace("  3\nEZDXF_META\n350\n2D\n", "  3\nACAD_DATALINK\n350\nA00\n  3\nEZDXF_META\n350\n2D\n")
                .Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nDICTIONARY\n  5\nA00\n330\nA\n100\nAcDbDictionary\n281\n1\n  3\nParts\n350\nA01\n"
                    + "  0\nDATALINK\n  5\nA01\n102\n{ACAD_REACTORS\n330\nA00\n102\n}\n330\nA00\n100\nAcDbDataLink\n  1\nAcExcel\n300\nBill of parts\n301\nData source file: parts.xlsx\n302\nparts.xlsx!Sheet1!A1:A2\n"
                    + " 90\n" + option.ToString(CultureInfo.InvariantCulture) + "\n 91\n1179649\n 92\n1\n170\n2026\n171\n10\n172\n20\n173\n13\n174\n31\n175\n10\n176\n484\n177\n1\n 93\n0\n304\nUpdated\n"
                    + " 94\n0\n305\nCUSTOMDATA\n  1\nDATAMAP_BEGIN\n309\nDATAMAP_END\n360\nA02\n"
                    + "  0\nXRECORD\n  5\nA02\n330\nA01\n100\nAcDbXrecord\n280\n1\n  1\ncached\n  0\nENDSEC\n  0\nEOF\n");

            foreach ((string cell, int rows, int columns) in cells)
            {
                linked = linked.Replace(cell, cell.Replace(" 92\n0\n", " 92\n1\n340\nA01\n 93\n" + rows.ToString(CultureInfo.InvariantCulture) + "\n 94\n" + columns.ToString(CultureInfo.InvariantCulture) + "\n 96\n0\n"));
            }

            return linked;
        }

        /// <summary>A drawing whose table has, after its last cell, a 93 and a 280 of its own, where LibreDWG writes a table's overrides.</summary>
        private static string WithTableOverride(string text)
        {
            int record = text.IndexOf("\nACAD_TABLE\n  5\nF6\n", StringComparison.Ordinal);
            int end = text.IndexOf("\n  0\n", record + 1, StringComparison.Ordinal) + 1;
            return text.Substring(0, end) + " 93\n1\n280\n1\n" + text.Substring(end);
        }

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().First();

        private static string TableRecord(string text) => Records(text).Single(r => HandleOf(r) == "F6");

        private static string Record(string text, string handle) => Records(text).SingleOrDefault(r => HandleOf(r) == handle);

        /// <summary>How many cells of a table's record name a link: its groups end in a 96 of nought before the cell's value.</summary>
        private static int LinksNamed(string record) => record.Split(new[] { "\n 96\n0\n301\nCELL_VALUE\n" }, StringSplitOptions.None).Length - 1;

        [Fact]
        public void ADataLinkOfTheLayoutGuessed_IsReadFromEachCellOfItsRange_WithItsUpdate()
        {
            foreach (string version in Versions)
            {
                string text = Linked(version, Named, (MarkCell, 2, 2));
                DxfDatabase db = DxfDatabase.Parse(text);
                Assert.Equal(text, db.ToText());
                DxfTable table = Table(db);
                var link = (DxfDataLink)db.GetObject("A01");
                Assert.Same(link, db.DataLinkDictionary["Parts"]);
                Assert.Equal("Parts", link.Name);
                Assert.Equal("DATALINK A01 Parts", link.ToString());
                Assert.Equal("AcExcel", link.DataAdapterId);
                Assert.Equal("Bill of parts", link.Description);
                Assert.Equal("Data source file: parts.xlsx", link.ToolTip);
                Assert.Equal("parts.xlsx!Sheet1!A1:A2", link.ConnectionString);
                Assert.Equal(DxfDataLinkOption.PersistCache, link.DataLinkOption);
                Assert.Equal(1179649, link.UpdateOption);
                Assert.True(link.IsValid);
                Assert.Equal(0, link.GetUpdateStatus(out DxfUpdateDirection direction, out DateTime time, out string message));
                Assert.Equal(DxfUpdateDirection.SourceToData, direction);
                Assert.Equal(new DateTime(2026, 10, 20, 13, 31, 10, 484), time);
                Assert.Equal("Updated", message);

                Assert.Same(link, table.GetDataLink(2, 1));
                Assert.True(table.IsLinked(1, 0));
                Assert.True(table.IsLinked(2, 1));
                Assert.False(table.IsLinked(0, 1));
                Assert.Null(table.GetDataLink(0, 0));
                Assert.Equal(new DxfCellRange(1, 0, 2, 1), table.GetDataLinkRange(2, 0));
                Assert.Equal("B-12", table.GetDataLinkRange(2, 0)[2, 0].TextString);
                Assert.Null(table.GetDataLinkRange(0, 1));
                Assert.Equal(new[] { link }, table.GetDataLink());
                Assert.Equal(new[] { link }, table.GetDataLink(new DxfCellRange(0, 1, 1, 1)));
                Assert.Empty(table.GetDataLink(new DxfCellRange(0, 0, 0, 1)));
                Assert.Equal(new[] { table }, link.GetTargets());

                // The cells keep what they hold: the library reads no source.
                Assert.Equal("Mark", table.TextString(1, 0));
                Assert.Equal(DxfTableCellType.BlockCell, table.CellType(2, 1));
                Assert.Equal("BOLT", table.GetBlockTableRecord(2, 1).Name);

                // Renamed, the entry is; the same name again writes nothing.
                link.Name = "Plates";
                string renamed = db.ToText();
                link.Name = "Plates";
                Assert.Equal(renamed, db.ToText());
                Assert.Equal("Plates", ((DxfDataLink)DxfDatabase.Parse(renamed).DataLinkDictionary["Plates"]).Name);
                Assert.False(DxfDatabase.Parse(renamed).DataLinkDictionary.Contains("Parts"));
            }
        }

        [Fact]
        public void ADataLink_IsReadAndTakenAway_ItsWritingHeldForTheAutoCADChecks()
        {
            Assert.Null(typeof(DxfDataLink).GetConstructor(Type.EmptyTypes));
            Assert.Empty(typeof(DxfTable).GetMember("SetDataLink"));

            foreach (string name in new[] { "DataAdapterId", "Description", "ToolTip", "ConnectionString", "DataLinkOption", "UpdateOption" })
            {
                Assert.Null(typeof(DxfDataLink).GetProperty(name).GetSetMethod());
            }

            Assert.NotNull(typeof(DxfDataLink).GetProperty("Name").GetSetMethod());

            // A link read from a drawing is in it already: no dictionary takes it.
            DxfDatabase db = DxfDatabase.Parse(Linked(DxfDimensionDrawings.R2018, Named, (MarkCell, 1, 1)));
            string before = db.ToText();
            Refused<ArgumentException>(() => db.GroupDictionary.SetAt("X", db.GetObject("A01")), "in no file yet");
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ANamedDataLinkTakenAway_StaysInTheDrawing_AsAutoCADsDataLinkManagerKeepsIt()
        {
            string text = Linked(DxfDimensionDrawings.R2018, Named, (MarkCell, 2, 1));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.GetObject("A01");
            Assert.Equal(new DxfCellRange(1, 0, 2, 0), table.GetDataLinkRange(2, 0));
            Assert.False(table.IsLinked(1, 1));

            // A cell outside the link takes nothing away.
            table.RemoveDataLink(1, 1);
            Assert.Equal(text, db.ToText());

            table.RemoveDataLink(2, 0);

            string saved = db.ToText();
            Assert.Equal(TableRecord(DxfDimensionDrawings.R2018), TableRecord(saved));
            Assert.Empty(table.GetDataLink());
            Assert.False(link.IsErased);
            Assert.Empty(link.GetTargets());
            Assert.Equal(1, db.DataLinkDictionary.Count);
            Assert.Equal(Record(text, "A01"), Record(saved, "A01"));
            Assert.Equal(Record(text, "A02"), Record(saved, "A02"));
        }

        [Fact]
        public void AnAnonymousDataLinkLetGoByRemoveDataLink_StaysInTheDrawing_AsAutoCADKeepsIt()
        {
            foreach (string version in Versions)
            {
                string text = Linked(version, Anonymous, (MarkCell, 1, 1), (B12Cell, 1, 1));
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                var link = (DxfDataLink)db.GetObject("A01");
                Assert.Equal(DxfDataLinkOption.Anonymous | DxfDataLinkOption.PersistCache, link.DataLinkOption);

                // Two ranges of the table: the one let go of keeps the link the other holds.
                table.RemoveDataLink(1, 0);
                Assert.False(link.IsErased);
                Assert.Equal(new[] { link }, table.GetDataLink());
                Assert.Equal(new DxfCellRange(2, 0, 2, 0), table.GetDataLinkRange(2, 0));

                // A copy of the table in its drawing holds it too.
                var copy = (DxfTable)table.Clone();
                db.ModelSpace.AppendEntity(copy);
                Assert.Equal(new[] { table, copy }, link.GetTargets());
                table.RemoveDataLink();
                Assert.False(link.IsErased);
                Assert.Equal(new[] { copy }, link.GetTargets());
                Assert.Equal(TableRecord(version), TableRecord(db.ToText()));

                // Let go of by every table, it stays, as AutoCAD 2021's RemoveDataLink keeps an anonymous link (check 77).
                copy.RemoveDataLink();
                string saved = db.ToText();
                Assert.False(link.IsErased);
                Assert.Empty(link.GetTargets());
                Assert.Equal(1, db.DataLinkDictionary.Count);
                Assert.Equal(Record(text, "A01"), Record(saved, "A01"));
                Assert.Equal(Record(text, "A02"), Record(saved, "A02"));

                // A table of no links takes nothing away and writes nothing.
                table.RemoveDataLink();
                table.RemoveDataLink(1, 0);
                Assert.Equal(saved, db.ToText());
            }
        }

        [Fact]
        public void RowsAndColumnsPutInAndTakenOut_MoveGrowAndShrinkALinkedRange_AsAMergeIsMoved()
        {
            foreach (string version in Versions)
            {
                foreach (int option in new[] { Named, Anonymous })
                {
                    DxfDatabase db = DxfDatabase.Parse(Linked(version, option, (MarkCell, 2, 1)));
                    DxfTable table = Table(db);
                    var link = (DxfDataLink)db.GetObject("A01");

                    // Put in above, it moves down; within, it grows.
                    table.InsertRows(0, 5.0, 1);
                    Assert.Equal(new DxfCellRange(2, 0, 3, 0), table.GetDataLinkRange(3, 0));
                    Assert.False(table.IsLinked(1, 0));
                    table.InsertRows(3, 5.0, 2);
                    Assert.Equal(new DxfCellRange(2, 0, 5, 0), table.GetDataLinkRange(2, 0));
                    table.InsertColumns(0, 20.0, 1);
                    Assert.Equal(new DxfCellRange(2, 1, 5, 1), table.GetDataLinkRange(5, 1));
                    Assert.False(table.IsLinked(2, 0));

                    // Taken out, it shrinks, its link named by its new top left cell.
                    table.DeleteRows(1, 2);
                    DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
                    Assert.Equal(new DxfCellRange(1, 1, 3, 1), read.GetDataLinkRange(1, 1));
                    Assert.Equal("Parts", read.GetDataLink(3, 1).Name);
                    Assert.Equal(1, LinksNamed(TableRecord(db.ToText())));
                    Assert.Equal("BOLT", read.GetBlockTableRecord(3, 2).Name);

                    table.SetSize(2, 2);
                    Assert.Equal(new DxfCellRange(1, 1, 1, 1), table.GetDataLinkRange(1, 1));
                    Assert.False(link.IsErased);

                    // Every row of it taken out, the link goes, and its DATALINK with it where it is anonymous.
                    table.DeleteRows(1, 1);
                    Assert.Empty(table.GetDataLink());
                    Assert.Equal(option == Anonymous, link.IsErased);
                    Assert.Equal(option == Anonymous ? 0 : 1, db.DataLinkDictionary.Count);
                    Assert.DoesNotContain("\n 96\n0\n301\n", TableRecord(db.ToText()), StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void ALinkedRangeWhoseTopLeftCellComesToBeABlockCell_LosesItsLink_AndTheBlockCellKeepsItsBlock()
        {
            foreach (string version in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(Linked(version, Anonymous, (QtyCell, 2, 1)));
                DxfTable table = Table(db);
                var link = (DxfDataLink)db.GetObject("A01");
                Assert.Equal(new DxfCellRange(1, 1, 2, 1), table.GetDataLinkRange(2, 1));

                // Its top row taken out, its top left cell is the block cell of BOLT, whose 340 names its block.
                table.DeleteRows(1, 1);

                Assert.Empty(table.GetDataLink());
                Assert.True(link.IsErased);
                DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
                Assert.Equal("BOLT", Table(readDb).GetBlockTableRecord(1, 1).Name);
                Assert.Equal(0, LinksNamed(TableRecord(readDb.ToText())));
            }
        }

        [Fact]
        public void ABlockCellWhose92IsOdd_KeepsItsBlock_AsRowsAndColumnsGoInAndOut_AndLinksAreTakenAway()
        {
            foreach (string version in Versions)
            {
                string odd = version.Replace(BoltCell, " 92\n1\n340\n48\n");
                Assert.NotEqual(version, odd);
                Assert.Empty(Table(DxfDatabase.Parse(odd)).GetDataLink());
                Assert.False(Table(DxfDatabase.Parse(odd)).IsLinked(2, 1));

                var changes = new (Action<DxfTable> Change, int Row, int Column)[]
                {
                    (t => t.InsertRows(0, 5.0, 1), 3, 1),
                    (t => t.DeleteRows(1, 1), 1, 1),
                    (t => t.InsertColumns(1, 7.0, 1), 2, 2),
                    (t => t.RemoveDataLink(), 2, 1),
                    (t => t.RemoveDataLink(2, 1), 2, 1),
                };

                foreach ((Action<DxfTable> change, int row, int column) in changes)
                {
                    DxfDatabase db = DxfDatabase.Parse(odd);
                    change(Table(db));
                    string saved = db.ToText();
                    Assert.Contains(" 92\n1\n340\n48\n144\n", TableRecord(saved), StringComparison.Ordinal);
                    Assert.Equal("BOLT", Table(DxfDatabase.Parse(saved)).GetBlockTableRecord(row, column).Name);
                }

                // Taking links away from a table of none writes nothing.
                DxfDatabase same = DxfDatabase.Parse(odd);
                Table(same).RemoveDataLink();
                Assert.Equal(odd, same.ToText());
            }
        }

        [Fact]
        public void ATableOverrideAfterTheLastCell_StaysTheTables_AsRowsGoInAndOut()
        {
            foreach (string version in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                string text = WithTableOverride(version);
                Assert.EndsWith("\n 93\n1\n280\n1\n", TableRecord(text), StringComparison.Ordinal);
                Assert.Equal(text, DxfDatabase.Parse(text).ToText());

                DxfDatabase deleted = DxfDatabase.Parse(text);
                Table(deleted).DeleteRows(2, 1);
                Assert.EndsWith("\n 93\n1\n280\n1\n", TableRecord(deleted.ToText()), StringComparison.Ordinal);
                Assert.Equal(2, Table(DxfDatabase.Parse(deleted.ToText())).NumRows);

                DxfDatabase inserted = DxfDatabase.Parse(text);
                Table(inserted).InsertRows(3, 5.0, 1);
                Assert.EndsWith("\n 93\n1\n280\n1\n", TableRecord(inserted.ToText()), StringComparison.Ordinal);
                Assert.Equal(4, Table(DxfDatabase.Parse(inserted.ToText())).NumRows);
            }
        }

        [Fact]
        public void AFormulaSetOnTheTopLeftCellOfALinkedRange_GoesAfterItsLink_WhichStillReadsAndIsTakenAway()
        {
            foreach (string version in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(Linked(version, Named, (MarkCell, 2, 1)));
                DxfTable table = Table(db);
                var link = (DxfDataLink)db.GetObject("A01");

                table.SetFormula(1, 0, 0, "=1+2");

                string saved = db.ToText();
                Assert.Contains(" 92\n1\n340\nA01\n 93\n2\n 94\n1\n 96\n0\n344\n", TableRecord(saved), StringComparison.Ordinal);
                DxfTable read = Table(DxfDatabase.Parse(saved));
                Assert.True(read.IsLinked(1, 0));
                Assert.True(read.IsLinked(2, 0));
                Assert.Equal("Parts", read.GetDataLink(1, 0).Name);
                Assert.Equal(new DxfCellRange(1, 0, 2, 0), read.GetDataLinkRange(1, 0));
                Assert.Equal("=1+2", read.GetFormula(1, 0, 0));

                table.RemoveDataLink();

                string after = db.ToText();
                Assert.DoesNotContain("\n340\nA01\n", TableRecord(after), StringComparison.Ordinal);
                Assert.Contains(" 92\n0\n344\n", TableRecord(after), StringComparison.Ordinal);
                Assert.Empty(table.GetDataLink());
                Assert.Equal("=1+2", Table(DxfDatabase.Parse(after)).GetFormula(1, 0, 0));
                Assert.False(link.IsErased);
            }
        }

        [Fact]
        public void CellsCopiedAndFilled_BringTheLinksWhollyWithin_UnlessSkipDataLink()
        {
            foreach (string version in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(Linked(version, Named, (MarkCell, 1, 1)));
                DxfTable table = Table(db);
                var link = (DxfDataLink)db.GetObject("A01");

                table.CopyFrom(table, DxfTableCopyOptions.ExpandOrContractTable, new DxfCellRange(1, 0, 1, 1), new DxfCellRange(3, 0, 3, 0));
                Assert.Equal(new DxfCellRange(3, 0, 3, 0), table.GetDataLinkRange(3, 0));
                Assert.Same(link, table.GetDataLink(3, 0));
                Assert.Equal(new[] { link }, table.GetDataLink());

                // SkipDataLink keeps this table's links and copies none: (3, 0) keeps its link, (2, 0) is given none.
                table.CopyFrom(table, DxfTableCopyOptions.SkipDataLink, new DxfCellRange(1, 0, 1, 0), new DxfCellRange(3, 0, 3, 0));
                Assert.True(table.IsLinked(3, 0));
                table.CopyFrom(table, DxfTableCopyOptions.SkipDataLink, new DxfCellRange(1, 0, 1, 0), new DxfCellRange(2, 0, 2, 0));
                Assert.False(table.IsLinked(2, 0));
                Assert.Equal("Mark", table.TextString(2, 0));

                // A copy over a linked range takes its link out; one wholly within carries it.
                table.CopyFrom(table, DxfTableCopyOptions.None, new DxfCellRange(0, 0, 0, 1), new DxfCellRange(3, 0, 3, 0));
                Assert.False(table.IsLinked(3, 0));
                Assert.True(table.IsLinked(1, 0));

                // Cells filled from a linked one are copies of its content, not of its link.
                table.Fill(new DxfCellRange(1, 0, 2, 0), new DxfCellRange(1, 0, 1, 0), DxfTableFillOptions.None);
                Assert.Equal(new DxfCellRange(1, 0, 1, 0), table.GetDataLinkRange(1, 0));
                Assert.False(table.IsLinked(2, 0));
                Assert.Equal(1, LinksNamed(TableRecord(db.ToText())));
                Assert.False(link.IsErased);
            }
        }

        [Fact]
        public void TheLinksOfACell_AreRefusedForACellOrRangeNotOfTheTable_AndATableOfR2000HasNone()
        {
            DxfDatabase db = DxfDatabase.Parse(Linked(DxfDimensionDrawings.R2018, Named, (MarkCell, 1, 2)));
            DxfTable table = Table(db);
            string before = db.ToText();

            Refused<ArgumentOutOfRangeException>(() => table.IsLinked(0, 2), "columns");
            Refused<ArgumentOutOfRangeException>(() => table.GetDataLink(3, 0), "rows");
            Refused<ArgumentOutOfRangeException>(() => table.GetDataLinkRange(0, 2), "columns");
            Refused<ArgumentOutOfRangeException>(() => table.RemoveDataLink(3, 0), "rows");
            Refused<ArgumentOutOfRangeException>(() => table.GetDataLink(new DxfCellRange(0, 0, 0, 2)), "reaches past");
            Refused<ArgumentNullException>(() => table.GetDataLink(null), "range");
            Assert.Equal(before, db.ToText());

            DxfDatabase old = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            Assert.Empty(Table(old).GetDataLink());
            Assert.False(Table(old).IsLinked(1, 0));
        }

        [Fact]
        public void ACellNamingNoDataLinkOfTheDrawing_IsReadAsLinkedToNone_AndTakenAwayStill()
        {
            string dangling = DxfDimensionDrawings.R2018.Replace(MarkCell, " 92\n1\n340\nFFF0\n 93\n1\n 94\n1\n 96\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n");
            DxfDatabase db = DxfDatabase.Parse(dangling);
            DxfTable table = Table(db);
            Assert.False(table.IsLinked(1, 0));
            Assert.Null(table.GetDataLink(1, 0));
            Assert.Null(table.GetDataLinkRange(1, 0));
            Assert.Empty(table.GetDataLink());

            table.RemoveDataLink();

            Assert.Equal(TableRecord(DxfDimensionDrawings.R2018), TableRecord(db.ToText()));
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }
    }
}
