using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33f runs of 2 November 2026 (headless, AutoCAD's own edits saved and
    /// read back): the looks a row of the title's kind out of its place keeps when its cells' overrides are taken away one by
    /// one, the title shown, and the kinds of the rows read of the TABLECONTENT anew where it changes. Each drawing is built in
    /// the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33fTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        /// <summary>The R2018 drawing of a table of 3 rows and 2 columns, each cell given the flag of its kind of value, with the TABLECONTENT AutoCAD 2021 writes, which keeps the kind of each row.</summary>
        private static DxfDatabase Kept() => DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018.Replace("\n 91\n0\n178\n", "\n 91\n262144\n178\n").Replace("\n 91\n49\n178\n", "\n 91\n262193\n178\n")));

        /// <summary>The R2018 drawing of D33b's table of its own header and data looks, its cells B-12 and M12 holding the data's own looks of 9, GH_TXT, red and top left as their own (D28c o0).</summary>
        private static DxfDatabase RowLooks()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Head, " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n"));
            DxfTable table = Table(db);

            for (int column = 0; column < 2; column++)
            {
                table.SetAlignment(2, column, DxfCellAlignment.TopLeft);
                table.SetTextStyle(2, column, db.TextStyleTable["GH_TXT"]);
                table.SetContentColor(2, column, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1));
                table.SetTextHeight(2, column, 9.0);
            }

            return DxfDatabase.Parse(db.ToText());
        }

        /// <summary>A whole number of a cell's head, before its value: its flags (91).</summary>
        private static int Head(DxfDatabase db, int cell, int code)
        {
            string record = DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6");
            string groups = record.Split(new[] { "\n171\n" }, StringSplitOptions.None)[cell + 1];
            int value = groups.IndexOf("301\nCELL_VALUE\n", StringComparison.Ordinal);
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(value >= 0 ? groups.Substring(0, value) : groups, "(?:^|\n) *" + code + "\n(-?\\d+)\n");
            return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
        }

        [Fact]
        public void ARowOfDataARowPutInBeforeItTookTheTablesDataLooksFrom_KeepsThem_ThroughAClearOfItsCells()
        {
            // D33e n05, D33f a01, a03, a05, a07, a08, a10, a11, a14: AutoCAD 2021 keeps the table's own looks of the data with
            // each row of data, so that a row put in before the first, which takes them from the table, leaves them with the
            // old rows: ClearTableStyleOverrides(2) and RemoveAllOverrides gave B-12 and M12 back GH_TXT, red, 9 and top left
            // (their flags 0x39, 91 57 here), wherever the rows went since and whatever B-12 was given since. The library had
            // moved them into the cells at the insert, and took them away.
            foreach ((Action<DxfTable> Edit, int Row) edit in new (Action<DxfTable>, int)[]
            {
                (t => { t.InsertRows(0, 9.0, 1); t.ClearTableStyleOverrides(2); }, 3),
                (t => { t.InsertRows(0, 9.0, 1); t.RemoveAllOverrides(3, 0); t.RemoveAllOverrides(3, 1); }, 3),
                (t => { t.InsertRows(2, 9.0, 1); t.ClearTableStyleOverrides(2); }, 3),
                (t => { t.InsertRows(0, 9.0, 1); t.InsertRows(0, 9.0, 1); t.ClearTableStyleOverrides(2); }, 4),
                (t => { t.InsertRows(0, 9.0, 1); t.DeleteRows(0, 1); t.ClearTableStyleOverrides(2); }, 2),
                (t => { t.InsertRows(0, 9.0, 1); t.SetTextHeight(3, 0, 5.0); t.ClearTableStyleOverrides(2); }, 3),
                (t => { t.InsertRows(0, 9.0, 1); t.ClearTableStyleOverrides(2); t.RemoveAllOverrides(3, 0); t.ClearTableStyleOverrides(2); }, 3),
            })
            {
                DxfDatabase db = RowLooks();
                edit.Edit(Table(db));
                DxfTable read = Reread(db);
                Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(edit.Row, 0));
                Assert.Equal(9.0, read.TextHeight(edit.Row, 0));
                Assert.Equal("GH_TXT", read.TextStyle(edit.Row, 0).Name);
                Assert.Equal(0x39, Head(db, edit.Row * 2, 91));
                Assert.Equal(0x39, Head(db, edit.Row * 2 + 1, 91));
            }

            // A clear of the table's overrides, of 0 or 1, takes them from the rows too: a clear of the cells' then gives none.
            foreach (int options in new[] { 0, 1 })
            {
                DxfDatabase db = RowLooks();
                Table(db).InsertRows(0, 9.0, 1);
                Table(db).ClearTableStyleOverrides(options);
                Table(db).ClearTableStyleOverrides(2);
                DxfTable read = Reread(db);
                Assert.Null(read.Alignment(3, 0));
                Assert.Null(read.TextHeight(3, 0));
                Assert.Null(read.TextStyle(3, 0));
            }
        }

        [Fact]
        public void ARowPutInTakingTheTitlesLook_KeepsItsKindsLooks_WhenItsCellsOverridesAreTakenAway_TheTitleShown()
        {
            // D33e n07: AutoCAD 2021, a row put in at 3 taking the title's look (InsertRowsAndInherit(3, 0, 1)), the title and the
            // header shown, kept through RemoveAllOverrides(3, 0) the title's looks as the cell's own, 170 5 and 140 6.0, as it
            // keeps them through a clear (n06). The library took them away: it read the kinds of the rows only where the table
            // left its title or header out.
            DxfDatabase db = Kept();
            Table(db).InsertRowsAndInherit(3, 0, 1);
            Table(db).RemoveAllOverrides(3, 0);
            DxfTable read = Reread(db);
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(3, 0));
            Assert.Equal(6.0, read.TextHeight(3, 0));
            Assert.False(read.IsTitleSuppressed);

            // The kinds are read anew once the TABLECONTENT changes: the table's cells cleared one by one before the row goes in
            // and after.
            DxfDatabase again = Kept();
            DxfTable table = Table(again);
            table.SetAlignment(2, 0, DxfCellAlignment.TopLeft);
            table.RemoveAllOverrides(2, 0);
            table.InsertRowsAndInherit(3, 0, 1);
            table.RemoveAllOverrides(3, 0);
            Assert.Equal(DxfCellAlignment.MiddleCenter, Reread(again).Alignment(3, 0));
            Assert.Equal(6.0, Reread(again).TextHeight(3, 0));

            // And a row of data put in its place takes no title's looks, as one put in a table that never held such a row: the
            // kinds read before are not kept.
            table.DeleteRows(3, 1);
            table.InsertRows(3, 9.0, 1);
            table.SetAlignment(3, 0, DxfCellAlignment.BottomRight);
            table.RemoveAllOverrides(3, 0);
            DxfDatabase plain = Kept();
            Table(plain).InsertRows(3, 9.0, 1);
            Table(plain).SetAlignment(3, 0, DxfCellAlignment.BottomRight);
            Table(plain).RemoveAllOverrides(3, 0);
            Assert.NotEqual(DxfCellAlignment.MiddleCenter, Reread(again).Alignment(3, 0));
            Assert.Equal(Reread(plain).Alignment(3, 0), Reread(again).Alignment(3, 0));
            Assert.Equal(Reread(plain).TextHeight(3, 0), Reread(again).TextHeight(3, 0));
        }

        [Fact]
        public void ARowOfTheTitlesStyle_IsFound_ThoughTheTableContentWritesItsMarkWithAnEscape()
        {
            // The TABLECONTENT is looked through for the styles of its rows before it is read whole: a TABLEROW_BEGIN written
            // with an escape, as a text of the file may be, is the one AutoCAD reads, and the row put in keeps the title's looks.
            DxfDatabase db = Kept();
            Table(db).InsertRowsAndInherit(3, 0, 1);
            string text = db.ToText();
            int at = -1;

            for (int k = 0; k < 4; k++)
            {
                at = text.IndexOf("\nTABLEROW_BEGIN\n", at + 1, StringComparison.Ordinal);
                Assert.True(at >= 0);
            }

            Assert.StartsWith("\nTABLEROW_BEGIN\n 90\n1\n", text.Substring(at), StringComparison.Ordinal);
            DxfDatabase escaped = DxfDatabase.Parse(text.Substring(0, at) + "\n\\U+0054ABLEROW_BEGIN\n" + text.Substring(at + "\nTABLEROW_BEGIN\n".Length));
            Table(escaped).RemoveAllOverrides(3, 0);
            DxfTable read = Reread(escaped);
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(3, 0));
            Assert.Equal(6.0, read.TextHeight(3, 0));
        }
    }
}
