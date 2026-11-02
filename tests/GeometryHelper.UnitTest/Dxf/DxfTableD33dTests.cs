using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33d runs of 2 November 2026 (headless, AutoCAD's own edits read in
    /// memory and saved): the cells a clear of the table's overrides keeps of no value, wherever rows and columns move them,
    /// an empty text set as a value of none, the value of a block cell reset, and the rows merged on load written after a
    /// change of the table's own colour, layer or linetype. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33dTests
    {
        private const string None = "93 7 90 0";

        private const string NoneRead = "93 5 90 512";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        private static DxfDatabase Drawn() => DxfDatabase.Parse(DxfDimensionDrawings.R2018);

        /// <summary>The flags (93) and kind (90) of the value of each cell of the table's record, row by row, as <c>93 7 90 0</c>; - for a cell of none.</summary>
        private static string[] Values(DxfDatabase db)
        {
            string record = DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6");
            string[] cells = record.Split(new[] { "\n171\n" }, StringSplitOptions.None);
            var values = new string[cells.Length - 1];

            for (int k = 1; k < cells.Length; k++)
            {
                Match m = Regex.Match(cells[k], "301\nCELL_VALUE\n 93\n(-?\\d+)\n 90\n(-?\\d+)\n");
                values[k - 1] = m.Success ? "93 " + m.Groups[1].Value + " 90 " + m.Groups[2].Value : "-";
            }

            return values;
        }

        /// <summary>The R2018 drawing of a table of 3 rows and 2 columns, its title Parts unmerged, as read from a file.</summary>
        private static DxfDatabase Unmerged()
        {
            DxfDatabase db = Drawn();
            Table(db).UnmergeCells(new DxfCellRange(0, 0, 0, 1));
            return DxfDatabase.Parse(db.ToText());
        }

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
                table.SetContentColor(2, column, Aci(1));
                table.SetTextHeight(2, column, 9.0);
            }

            return DxfDatabase.Parse(db.ToText());
        }

        /// <summary>The R2018 drawing whose B-12 holds a look of every kind of its own: green behind, red, 5 high, GH_TXT, bottom right, turned 90 degrees, a red line over it (D33c b-r).</summary>
        private static DxfDatabase EveryLook()
        {
            DxfDatabase db = Drawn();
            DxfTable table = Table(db);
            table.SetBackgroundColor(2, 0, Aci(3));
            table.SetContentColor(2, 0, Aci(1));
            table.SetTextHeight(2, 0, 5.0);
            table.SetTextStyle(2, 0, db.TextStyleTable["GH_TXT"]);
            table.SetAlignment(2, 0, DxfCellAlignment.BottomRight);
            table.SetTextRotation(2, 0, DxfRotationAngle.Degrees090);
            table.SetGridColor(2, 0, DxfGridLineType.HorizontalTop, Aci(1));
            return DxfDatabase.Parse(db.ToText());
        }

        #region Cells emptied in the session

        [Fact]
        public void ACellEmptiedInTheSession_KeepsItsValueOfNone_WhenTheOverridesAreCleared_WhereverItMoved()
        {
            // D33c r08, r10, r12 and D33d a01 to a13: AutoCAD 2021's ClearTableStyleOverrides of 0 or 2 kept 93 7, 90 0 in each
            // cell emptied while the drawing was open, by InsertRows, InsertColumns, SetSize, a merge (the one made on load
            // too) or a ResetValue, wherever rows or columns moved it afterwards; the library kept it for a cell ResetValue
            // emptied alone, forgot it once rows or columns moved, and wrote 93 5, 90 512.
            var cases = new List<(Action<DxfTable> Edit, int[] Cells)>
            {
                (t => t.InsertRows(2, 9.0, 1), new[] { 4, 5 }),
                (t => t.MergeCells(new DxfCellRange(1, 0, 1, 1)), new[] { 3 }),
                (t => { t.SetSize(4, 2); t.ResetValue(3, 0); t.DeleteRows(2, 1); }, new[] { 4, 5 }),
                (t => { t.ResetValue(2, 0); t.InsertColumns(0, 20.0, 1); }, new[] { 1, 2, 3, 6, 7 }),
                (t => { t.MergeCells(new DxfCellRange(1, 0, 1, 1)); t.UnmergeCells(new DxfCellRange(1, 0, 1, 1)); }, new[] { 3 }),
            };

            foreach (int options in new[] { 0, 2 })
            {
                foreach (var (edit, cells) in cases)
                {
                    DxfDatabase db = Drawn();
                    edit(Table(db));
                    Table(db).ClearTableStyleOverrides(options);
                    string[] values = Values(db);

                    foreach (int k in cells)
                    {
                        Assert.Equal(None, values[k]);
                    }
                }
            }

            // a01: the title's second cell, emptied by the merge AutoCAD makes on load.
            DxfDatabase loaded = Unmerged();
            Table(loaded).ClearTableStyleOverrides(0);
            Assert.Equal(None, Values(loaded)[1]);

            // A cell read of no value from the file is written of none of no kind, as AutoCAD 2021 wrote it (D33b c14, D33d a06).
            DxfDatabase grown = Drawn();
            Table(grown).SetSize(4, 2);
            grown = DxfDatabase.Parse(grown.ToText());
            Table(grown).ClearTableStyleOverrides(0);
            Assert.Equal(NoneRead, Values(grown)[6]);
            Assert.Equal(NoneRead, Values(grown)[7]);

            // c01, c02: one reset, or given an empty text, while the drawing is open is emptied in the session all the same.
            foreach (Action<DxfTable> edit in new Action<DxfTable>[] { t => t.ResetValue(3, 0), t => t.SetTextString(3, 0, string.Empty) })
            {
                DxfDatabase read = Drawn();
                Table(read).SetSize(4, 2);
                read = DxfDatabase.Parse(read.ToText());
                string text = read.ToText();
                edit(Table(read));
                Assert.Equal(text, read.ToText());
                Table(read).ClearTableStyleOverrides(0);
                Assert.Equal(None, Values(read)[6]);
                Assert.Equal(NoneRead, Values(read)[7]);
            }
        }

        [Fact]
        public void ACellResetBeforeARowWentIn_LosesTheTablesLooks_WhenTheTablesOverridesAreCleared()
        {
            // D33d a07: AutoCAD 2021's ClearTableStyleOverrides(1) took from B-12, reset and then moved down by a row put in
            // above it, the data's looks it kept of the table's (7, 64, 140), and left its alignment; the library forgot the
            // reset once the row went in, and kept them.
            DxfDatabase db = RowLooks();
            DxfTable table = Table(db);
            table.ResetValue(2, 0);
            table.InsertRows(2, 9.0, 1);
            table.ClearTableStyleOverrides(1);
            DxfTable read = Reread(db);
            Assert.Equal(DxfCellProperties.Alignment, read.GetOverrides(3, 0, 0));
            Assert.Null(read.TextHeight(3, 0));
            Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(3, 0));
            Assert.Equal(None, Values(db)[6]);

            // c03, c04: and so did B-12 given an empty text, and the block cell M12 reset, in place.
            for (int column = 0; column < 2; column++)
            {
                DxfDatabase looks = RowLooks();

                if (column == 0)
                {
                    Table(looks).SetTextString(2, 0, string.Empty);
                }
                else
                {
                    Table(looks).ResetValue(2, 1);
                }

                Table(looks).ClearTableStyleOverrides(1);
                DxfTable cleared = Reread(looks);
                Assert.Equal(DxfCellProperties.Alignment, cleared.GetOverrides(2, column, 0));
                Assert.Equal(DxfCellAlignment.TopLeft, cleared.Alignment(2, column));
            }
        }

        #endregion

        #region An empty text

        [Fact]
        public void AnEmptyTextSet_IsAValueOfNone_AsResetValueLeavesIt()
        {
            // D33c r09, D33d s01 to s10: AutoCAD 2021's SetTextString of an empty text wrote a value of none (93 7, 90 0), the
            // flag of its kind of value taken out (91 0), whatever the cell held, a text, a number or the empty text of the
            // title's hidden cell, and took the content's looks away as ResetValue does; a clear of the overrides kept it of
            // none. The library wrote an empty text of the kind 4, its flag kept.
            DxfDatabase db = Drawn();
            Table(db).SetTextString(2, 0, string.Empty);
            Assert.Equal(None, Values(db)[4]);
            Assert.Equal(DxfCellProperties.Invalid, Reread(db).GetOverrides(2, 0, 0));
            Table(db).ClearTableStyleOverrides(0);
            Assert.Equal(None, Values(db)[4]);

            DxfDatabase hidden = Drawn();
            Table(hidden).SetTextString(0, 1, string.Empty);
            Assert.Equal(None, Values(hidden)[1]);

            DxfDatabase number = Drawn();
            Table(number).SetValue(2, 0, 1.5);
            number = DxfDatabase.Parse(number.ToText());
            Table(number).SetTextString(2, 0, string.Empty);
            Assert.Equal(None, Values(number)[4]);
            Assert.Equal(DxfDataType.Unknown, Reread(number).DataType(2, 0));

            // s02: the looks of the content go with it, its alignment, background and lines kept, its turn made nought.
            DxfDatabase looks = EveryLook();
            Table(looks).SetTextString(2, 0, string.Empty);
            DxfTable read = Reread(looks);
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.BackgroundColor, read.GetOverrides(2, 0, 0));
            Assert.Null(read.TextHeight(2, 0));
            Assert.Equal(DxfRotationAngle.Degrees000, read.TextRotation(2, 0));
            Assert.Equal(Aci(1), read.GetGridColor(2, 0, DxfGridLineType.HorizontalTop));
            Assert.Equal(string.Empty, read.TextString(2, 0));
        }

        #endregion

        #region A block cell reset

        [Fact]
        public void ResetValue_OfABlockCell_MakesItATextCellOfNoValue()
        {
            // D33c r04, D33d b01 to b05: AutoCAD 2021's ResetValue of the block cell M12 made it a text cell of no value (91 0,
            // 93 7, 90 0), its block and its looks of the content gone but those the table gives its row of its own, and kept
            // it so through a clear of the overrides; the library refused it.
            DxfDatabase db = Drawn();
            Table(db).ResetValue(2, 1);
            DxfTable read = Reread(db);
            Assert.Equal(DxfTableCellType.TextCell, read.CellType(2, 1));
            Assert.Equal(DxfDataType.Unknown, read.DataType(2, 1));
            Assert.Equal(string.Empty, read.TextString(2, 1));
            Assert.Equal(DxfCellProperties.Invalid, read.GetOverrides(2, 1, 0));
            Assert.Equal(None, Values(db)[5]);
            Table(db).ClearTableStyleOverrides(0);
            Assert.Equal(None, Values(db)[5]);

            DxfDatabase set = Drawn();
            Table(set).ResetValue(2, 1);
            Table(set).SetValue(2, 1, "Z");
            Assert.Equal("Z", Reread(set).TextString(2, 1));

            DxfDatabase looks = RowLooks();
            Table(looks).ResetValue(2, 1);
            DxfTable kept = Reread(looks);
            Assert.Equal(DxfTableCellType.TextCell, kept.CellType(2, 1));
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.ContentColor | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight, kept.GetOverrides(2, 1, 0));
            Assert.Equal(9.0, kept.TextHeight(2, 1));
        }

        #endregion

        #region The table's own look

        [Fact]
        public void AChangeOfTheTablesOwnLook_WritesTheRowsMergedOnLoad()
        {
            // D33c e02 and e05: AutoCAD 2021 wrote the title it merged on load merged after the table's colour was changed, as it
            // writes it after any edit; the library merged it at an edit of the table's cells or structure alone, and wrote it
            // unmerged after a change of the look the table has as an entity.
            foreach (Action<DxfTable> edit in new Action<DxfTable>[]
            {
                t => t.ColorIndex = 1,
                t => t.Color = Aci(3),
                t => t.TrueColor = 0xFF8000,
                t => t.Layer = "GH_TABLES",
                t => t.Linetype = "ByBlock",
                t => t.LinetypeScale = 2.0,
                t => t.LineWeight = DxfLineWeight.LineWeight050,
                t => t.Visible = false,
                t => t.Transparency = DxfTransparency.ByBlock,
            })
            {
                DxfDatabase db = Unmerged();
                edit(Table(db));
                DxfTable read = Reread(db);
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 1));
                Assert.Equal("Parts", read.TextString(0, 0));
            }

            // The look the table has already changes nothing, the title left unmerged as read.
            DxfDatabase same = Unmerged();
            string text = same.ToText();
            Table(same).ColorIndex = Table(same).ColorIndex;
            Table(same).Layer = Table(same).Layer;
            Assert.Equal(text, same.ToText());
        }

        #endregion
    }
}
