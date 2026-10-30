using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to plugin run v12 of 31 October 2026: a link taken away leaves no cell of its
    /// range with the flag 4 of a linked cell in its state, as AutoCAD read a cell left with it as linked (73n), while a cell of
    /// the flag 4 outside every range is not linked (73m). Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD20Tests
    {
        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static readonly string Begin = G(1, "LINKEDTABLEDATACELL_BEGIN");

        #region Point 73n: the cells of a range a link is taken away from

        /// <summary>
        /// The table F6 of AutoCAD's linked by the library over (1, 0) to (2, 1), as 73-h, the cells (row, column) of its
        /// TABLECONTENT B03 then given the states (90) named, the other groups kept: 73-k with (2, 1) of the state 4.
        /// </summary>
        private static DxfDatabase LinkedWithStates(params (int Row, int Column, string State)[] states)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable table = Table(db);
            table.RemoveDataLink();
            table.SetDataLink(new DxfCellRange(1, 0, 2, 1), (DxfDataLink)db.DataLinkDictionary["GH_PARTS"]);

            string text = db.ToText();
            string content = DxfAutoCadChecksD16Tests.Record(text, "B03");
            string[] cells = content.Split(new[] { Begin }, StringSplitOptions.None);

            foreach (var (row, column, state) in states)
            {
                int k = 1 + row * 2 + column;
                int end = cells[k].IndexOf('\n', cells[k].IndexOf('\n') + 1) + 1;
                Assert.StartsWith(" 90\n", cells[k], StringComparison.Ordinal);
                cells[k] = G(90, state) + cells[k].Substring(end);
            }

            return DxfDatabase.Parse(text.Replace(content, string.Join(Begin, cells)));
        }

        /// <summary>The state (90) of each cell of the TABLECONTENT B03, row by row.</summary>
        private static List<string> States(DxfDatabase db)
        {
            string[] cells = DxfAutoCadChecksD16Tests.Record(db.ToText(), "B03").Split(new[] { Begin }, StringSplitOptions.None);
            return cells.Skip(1).Select(c => c.Split('\n')[1].Trim()).ToList();
        }

        private static void NoCellLinked(DxfTable table)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    Assert.False(table.IsLinked(row, column));
                    Assert.Null(table.GetDataLinkRange(row, column));
                }
            }
        }

        [Fact]
        public void EveryLinkTakenAway_ClearsTheFlag4OfEveryCellOfItsRange_AsAutoCadReadACellLeftWithItAsLinked()
        {
            // Plugin v12, 73n: after-d18's 73-k, (2, 1) of the state 4, given RemoveDataLink() by the library, kept 90 4 at (2, 1);
            // AutoCAD 2021 read (2, 1) as linked with no range, and saved it with 90 4. No cell of the range keeps the flag now.
            DxfDatabase db = LinkedWithStates((2, 1, "4"));
            Assert.Equal(new[] { "0", "0", "4", "0", "0", "4" }, States(db));

            Table(db).RemoveDataLink();

            Assert.Equal(new[] { "0", "0", "0", "0", "0", "0" }, States(db));
            NoCellLinked(Table(db));
            NoCellLinked(Table(DxfDatabase.Parse(db.ToText())));
        }

        [Fact]
        public void TheLinkOfACellTakenAway_ClearsTheFlag4OfEveryCellOfItsRange()
        {
            // RemoveDataLink(row, column) takes the range away as RemoveDataLink() does, by a cell of it that is not its top left.
            DxfDatabase db = LinkedWithStates((2, 1, "4"));

            Table(db).RemoveDataLink(2, 1);

            Assert.Equal(new[] { "0", "0", "0", "0", "0", "0" }, States(db));
            NoCellLinked(Table(DxfDatabase.Parse(db.ToText())));
        }

        [Fact]
        public void ACellOfTheFlag4OutsideEveryRange_IsNotLinked_AndKeepsItsStateWhenALinkIsTakenAway()
        {
            // Plugin v12, 73m: (1, 1), inside the range (1, 0) to (2, 1), and (0, 1), outside every range, of the state 4: AutoCAD
            // 2021 read (1, 0) and (1, 1) as linked, (0, 1) as not. The link taken away clears (1, 1) and leaves (0, 1) as it is,
            // being no cell of the range let go of.
            DxfDatabase db = LinkedWithStates((1, 1, "4"), (0, 1, "4"));
            DxfTable table = Table(db);

            Assert.True(table.IsLinked(1, 0));
            Assert.True(table.IsLinked(1, 1));
            Assert.False(table.IsLinked(0, 1));
            Assert.Null(table.GetDataLinkRange(0, 1));

            table.RemoveDataLink();

            Assert.Equal(new[] { "0", "4", "0", "0", "0", "0" }, States(db));
            NoCellLinked(Table(DxfDatabase.Parse(db.ToText())));
        }

        #endregion
    }
}
