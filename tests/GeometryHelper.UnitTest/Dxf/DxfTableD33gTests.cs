using System;
using System.Globalization;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33g runs of 2 November 2026 (headless, AutoCAD's own edits saved and
    /// read back): AutoCAD keeps the looks of each row with the row (its ROWTABLEFORMAT), the table's own looks of a kind those
    /// of its first row of that kind, so that a row put in keeps none, a row put in taking the look of another keeps that
    /// row's, a column put in gives each new cell its row's, and the first row of data taken out gives the table the looks of
    /// the next back. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33gTests
    {
        // The head of the table of its own header and data looks, and of it once the data's are gone (D28c o0).
        private const int AllLooks = 6886016;
        private const int HeaderLooks = 2101760;

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        /// <summary>
        /// The R2018 drawing of D33b's table of its own header and data looks, its cells B-12 and M12 holding the data's own looks
        /// of 9, GH_TXT, red and top left as their own (D28c o0); with the TABLECONTENT AutoCAD 2021 writes, which keeps the kind
        /// of each row, where asked.
        /// </summary>
        private static DxfDatabase RowLooks(bool content = false)
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            string text = DxfDimensionDrawings.R2018.Replace(Head, " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n");
            DxfDatabase db = DxfDatabase.Parse(content ? DxfAutoCadChecksD16Tests.LinkedByAutoCad(text.Replace("\n 91\n0\n178\n", "\n 91\n262144\n178\n").Replace("\n 91\n49\n178\n", "\n 91\n262193\n178\n")) : text);
            DxfTable table = Table(db);

            for (int column = 0; column < 2; column++)
            {
                table.SetAlignment(2, column, DxfCellAlignment.TopLeft);
                table.SetTextStyle(2, column, db.TextStyleTable["GH_TXT"]);
                table.SetContentColor(2, column, Aci(1));
                table.SetTextHeight(2, column, 9.0);
            }

            return DxfDatabase.Parse(db.ToText());
        }

        /// <summary>A whole number of a cell's head, before its value, by the cell's index (-1 for the table's head): its flags (91), the table's (93).</summary>
        private static int Head(DxfDatabase db, int cell, int code)
        {
            string record = DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6");
            string groups = record.Split(new[] { "\n171\n" }, StringSplitOptions.None)[cell + 1];
            int value = groups.IndexOf("301\nCELL_VALUE\n", StringComparison.Ordinal);
            Match m = Regex.Match(value >= 0 ? groups.Substring(0, value) : groups, "(?:^|\n) *" + code + "\n(-?\\d+)\n");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }

        [Fact]
        public void ARowPutInAfterTheData_KeepsNoneOfTheTablesLooksOfTheData_ThroughAClearOfItsCells()
        {
            // D33f a04, D33g e01, h03: AutoCAD 2021 gave a row put in by InsertRows or SetSize after the row of data no looks of
            // its own (its ROWTABLEFORMAT empty), so that ClearTableStyleOverrides(2) left its cells none (91 0), and a row put in
            // taking its look none either; B-12 above kept GH_TXT, red, 9 and top left. The library gave them the table's.
            foreach ((Action<DxfTable> Edit, int Row) edit in new (Action<DxfTable>, int)[]
            {
                (t => { t.InsertRows(3, 9.0, 1); t.ClearTableStyleOverrides(2); }, 3),
                (t => { t.SetSize(4, 2); t.ClearTableStyleOverrides(2); }, 3),
                (t => { t.InsertRows(3, 9.0, 1); t.InsertRowsAndInherit(4, 3, 1); t.ClearTableStyleOverrides(2); }, 4),
            })
            {
                DxfDatabase db = RowLooks();
                edit.Edit(Table(db));
                DxfTable read = Reread(db);
                Assert.Equal(0, Head(db, edit.Row * 2, 91));
                Assert.Equal(0, Head(db, edit.Row * 2 + 1, 91));
                Assert.Null(read.Alignment(edit.Row, 0));
                Assert.Null(read.TextHeight(edit.Row, 0));
                Assert.Equal(0x39, Head(db, 4, 91) & 0xFFFF);
                Assert.Equal(9.0, read.TextHeight(2, 0));
            }

            // D33g g01: a column put in gives the new cell of that row none, the one of the row of data the table's (91 57).
            DxfDatabase columns = RowLooks();
            Table(columns).InsertRows(3, 9.0, 1);
            Table(columns).InsertColumns(1, 20.0, 1);
            Assert.Equal(0, Head(columns, 3 * 3 + 1, 91));
            Assert.Equal(0x39, Head(columns, 2 * 3 + 1, 91));
        }

        [Fact]
        public void AColumnPutIn_GivesEachNewCellTheLooksItsRowKeeps()
        {
            // D33f a09, D33g g02: the table given a row at 0, the row of data keeps the table's looks of the data, and a column
            // put in gave its cell there GH_TXT, red, 9 and top left (91 57), through a clear of 2 too; the title moved down
            // gave its new cell the title's 170 5 and 140 6.0 (91 33), the header the header's 170 5, its green and its 9
            // (91 39), the new row none. The library gave the new cells the table's looks of the data, which were gone.
            DxfDatabase db = RowLooks(true);
            DxfTable table = Table(db);
            table.InsertRows(0, 9.0, 1);
            table.InsertColumns(1, 20.0, 1);
            Assert.Equal(0, Head(db, 0 * 3 + 1, 91));
            Assert.Equal(0x21, Head(db, 1 * 3 + 1, 91));
            Assert.Equal(0x27, Head(db, 2 * 3 + 1, 91));
            Assert.Equal(0x39, Head(db, 3 * 3 + 1, 91));
            DxfTable read = Reread(db);
            Assert.Equal(6.0, read.TextHeight(1, 1));
            Assert.Equal(9.0, read.TextHeight(2, 1));
            Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(3, 1));
            Assert.Equal("GH_TXT", read.TextStyle(3, 1).Name);

            table.ClearTableStyleOverrides(2);
            Assert.Equal(0x39, Head(db, 3 * 3 + 1, 91));
            Assert.Equal(9.0, Reread(db).TextHeight(3, 1));
        }

        [Fact]
        public void ARowPutInTakingTheLookOfARowOfData_KeepsTheLooksThatRowKeeps()
        {
            // D33g h01: the row put in after B-12 taking its look, the table given a row at 0 before, kept GH_TXT, red, 9 and
            // top left through a clear of 2, as B-12 did. The library gave it none.
            DxfDatabase db = RowLooks();
            DxfTable table = Table(db);
            table.InsertRows(0, 9.0, 1);
            table.InsertRowsAndInherit(4, 3, 1);
            table.ClearTableStyleOverrides(2);
            Assert.Equal(0x39, Head(db, 4 * 2, 91) & 0xFFFF);
            Assert.Equal(0x39, Head(db, 4 * 2 + 1, 91) & 0xFFFF);
            Assert.Equal(9.0, Reread(db).TextHeight(4, 0));
            Assert.Equal(0, Head(db, 0, 91));
        }

        [Fact]
        public void TheFirstRowOfDataTakenOut_GivesTheTableTheLooksOfTheNext()
        {
            // D33f a07, D33g k02, k06: the row put in at 0 taken out again, AutoCAD 2021 read the table's own looks of the data
            // from B-12's row again and wrote them in its head as before (93 6886016), the two rows put in taken out together
            // alike. The library left them gone.
            foreach (Action<DxfTable> edit in new Action<DxfTable>[]
            {
                t => { t.InsertRows(0, 9.0, 1); t.DeleteRows(0, 1); },
                t => { t.InsertRows(0, 9.0, 1); t.InsertRows(0, 9.0, 1); t.DeleteRows(0, 2); },
            })
            {
                DxfDatabase db = RowLooks(true);
                edit(Table(db));
                Assert.Equal(AllLooks, Head(db, -1, 93));
                Table(db).ClearTableStyleOverrides(2);
                Assert.Equal(0x39, Head(db, 4, 91) & 0xFFFF);
                Assert.Equal(DxfCellAlignment.TopLeft, Reread(db).Alignment(2, 0));
            }

            // D33g k05, k03: B-12's row taken out after a row put in after it, the table's looks of the data went, as that row
            // kept none (93 2101760), and its cells cleared of 2 have none.
            DxfDatabase gone = RowLooks(true);
            Table(gone).InsertRows(3, 9.0, 1);
            Table(gone).DeleteRows(2, 1);
            Assert.Equal(HeaderLooks, Head(gone, -1, 93));
            Table(gone).ClearTableStyleOverrides(2);
            Assert.Equal(0, Head(gone, 4, 91));

            // D33g k01, k04: a row put in at 0 again, after the looks came back, takes them from the table again, the rows
            // keeping them as before; one of two rows put in at 0 taken out leaves them gone.
            foreach (Action<DxfTable> edit in new Action<DxfTable>[]
            {
                t => { t.InsertRows(0, 9.0, 1); t.DeleteRows(0, 1); t.InsertRows(0, 9.0, 1); },
                t => { t.InsertRows(0, 9.0, 1); t.InsertRows(0, 9.0, 1); t.DeleteRows(0, 1); },
            })
            {
                DxfDatabase db = RowLooks(true);
                edit(Table(db));
                Table(db).ClearTableStyleOverrides(2);
                Assert.Equal(HeaderLooks | 3, Head(db, -1, 93));
                Assert.Equal(0, Head(db, 0, 91));
                Assert.Equal(0x39, Head(db, 3 * 2, 91) & 0xFFFF);
                Assert.Equal(9.0, Reread(db).TextHeight(3, 0));
            }
        }

        [Fact]
        public void TheTitlesHeightAMovedTitleKeeps_IsWrittenAsAutoCadWritesIt()
        {
            // D33f a12, D33g g02: AutoCAD 2021 wrote the title moved down by a row put in before it 140 6.0, the library 140 6.
            DxfDatabase db = RowLooks(true);
            Table(db).InsertRows(0, 9.0, 1);
            string record = DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6");
            Assert.Contains("\n170\n5\n140\n6.0\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n140\n6\n", record, StringComparison.Ordinal);
        }
    }
}
