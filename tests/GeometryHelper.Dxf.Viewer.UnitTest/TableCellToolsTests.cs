using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Cut, Copy and Paste, Match Cell, Cell Locking, Data Format, and Insert Block, Field and Formula of the cells of a table, as
    /// the Table Cell tab of AutoCAD 2021 makes them, on a schedule of five columns 63.5 wide, a title, a header and four rows of
    /// data (rows 0 to 5, columns 0 to 4) and on AutoCAD's own table drawing with its block cell of BOLT.
    /// </summary>
    public class TableCellToolsTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        private static string Schedule(CadDocument document)
        {
            CadCommandSession session = Session(document);
            int next = 0;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "TABLE" && next++ == 0)
                {
                    ((CadInsertTableRequest)e.Payload).Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };
            Run(session, "TABLE", P(0, 0), Esc);
            return Assert.Single(All<DxfTable>(document.ModelSpace)).Handle;
        }

        private static DxfTable TableOf(CadDocument document, string handle) => Entity<DxfTable>(document, handle);

        private static DxfCellRange One(int row, int column) => new DxfCellRange(row, column, row, column);

        private static GeoPoint2 Middle(DxfTable table, int row, int column)
        {
            IReadOnlyList<GeoPoint3> c = table.GetCellExtents(row, column, true);
            return new GeoPoint2((c[0].X + c[2].X) / 2.0, (c[0].Y + c[2].Y) / 2.0);
        }

        [Fact]
        public void ACellLockedInItsContent_RefusesTyping_AndEmptying_ButTakesAFormat()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "HEA300").Succeeded);

            CadEditResult locked = CadTableEditor.SetLocking(document, handle, One(2, 0), DxfCellState.ContentLocked);
            Assert.True(locked.Succeeded, locked.Message);
            Assert.Equal(DxfCellState.ContentLocked, TableOf(document, handle).GetCellState(2, 0));

            CadEditResult typed = CadTableEditor.SetText(document, handle, 2, 0, "IPE200");
            Assert.True(typed.Refused);
            Assert.Contains("A3", typed.Message);
            Assert.True(CadTableEditor.ClearContents(document, handle, One(2, 0)).Refused);
            Assert.Equal("HEA300", TableOf(document, handle).TextString(2, 0));
            Assert.True(
                CadTableEditor.SetAlignment(document, handle, One(2, 0), DxfCellAlignment.MiddleLeft).Succeeded
            );

            Assert.True(CadTableEditor.SetLocking(document, handle, One(2, 0), DxfCellState.None).Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "IPE200").Succeeded);
            Assert.Equal("IPE200", TableOf(document, handle).TextString(2, 0));
        }

        [Fact]
        public void ACellLockedInItsFormat_RefusesAlignmentFillAndBorders_ButTakesText_AndLockingIsOneUndoStep()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            Assert.True(
                CadTableEditor
                    .SetLocking(document, handle, new DxfCellRange(2, 0, 3, 1), DxfCellState.FormatLocked)
                    .Succeeded
            );

            Assert.True(CadTableEditor.SetAlignment(document, handle, One(3, 1), DxfCellAlignment.BottomRight).Refused);
            Assert.True(
                CadTableEditor
                    .SetBackground(document, handle, One(2, 0), DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3))
                    .Refused
            );
            Assert.True(
                CadTableEditor.SetBorders(document, handle, One(2, 1), DxfGridLineType.AllGridLines, false).Refused
            );
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "x").Succeeded);
            Assert.True(
                CadTableEditor.SetAlignment(document, handle, One(4, 4), DxfCellAlignment.BottomRight).Succeeded
            );

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(DxfCellState.FormatLocked, TableOf(document, handle).GetCellState(3, 1));
            Assert.True(document.Undo());
            Assert.Equal(DxfCellState.None, TableOf(document, handle).GetCellState(3, 1));
        }

        [Fact]
        public void CellsCopied_AreHeldAsTheyWere_AndPastedOverOthersWithTheirLookAndMerges()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "HEA300").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "12.5").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 0, "IPE200").Succeeded);
            Assert.True(
                CadTableEditor.SetAlignment(document, handle, One(2, 0), DxfCellAlignment.MiddleRight).Succeeded
            );
            Assert.True(
                CadTableEditor
                    .SetBackground(document, handle, One(2, 1), DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3))
                    .Succeeded
            );
            Assert.True(CadTableEditor.Merge(document, handle, new DxfCellRange(3, 0, 3, 1)).Succeeded);

            CadTableCellClip clip = CadTableEditor.Copy(TableOf(document, handle), new DxfCellRange(2, 0, 3, 1));
            Assert.Equal(2, clip.Rows);
            Assert.Equal(2, clip.Columns);
            Assert.Equal("A3:B4", clip.Name);

            // The cells copied are changed, then pasted: what was copied is what comes out.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "changed").Succeeded);
            CadEditResult pasted = CadTableEditor.Paste(document, handle, One(4, 2), clip);
            Assert.True(pasted.Succeeded, pasted.Message);

            DxfTable table = TableOf(document, handle);
            Assert.Equal("HEA300", table.TextString(4, 2));
            Assert.Equal(DxfCellAlignment.MiddleRight, table.Alignment(4, 2));
            Assert.Equal(12.5, Convert.ToDouble(table.Value(4, 3)), 9);
            Assert.Equal(3, table.BackgroundColor(4, 3).ColorIndex);
            Assert.True(table.IsMergedCell(5, 2, out DxfCellRange merged));
            Assert.Equal(new DxfCellRange(5, 2, 5, 3), merged);
            Assert.Equal("IPE200", table.TextString(5, 2));

            Assert.True(document.Undo());
            Assert.Equal(string.Empty, TableOf(document, handle).TextString(4, 2));
            Assert.False(TableOf(document, handle).IsMergedCell(5, 2, out _));
        }

        [Fact]
        public void CellsCut_LoseTheirContentButKeepTheirLook_AndPasteWhereTold_ARangeOfMultiplesTilesThem()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "A").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "B").Succeeded);
            Assert.True(
                CadTableEditor.SetAlignment(document, handle, One(2, 0), DxfCellAlignment.BottomLeft).Succeeded
            );

            CadEditResult cut = CadTableEditor.Cut(
                document,
                handle,
                new DxfCellRange(2, 0, 2, 1),
                out CadTableCellClip clip
            );
            Assert.True(cut.Succeeded, cut.Message);
            Assert.True(clip.IsCut);
            Assert.Equal(string.Empty, TableOf(document, handle).TextString(2, 0));
            Assert.Equal(DxfCellAlignment.BottomLeft, TableOf(document, handle).Alignment(2, 0));

            // A range of three rows by four columns takes the two cells side by side twice and down three times.
            Assert.True(CadTableEditor.Paste(document, handle, new DxfCellRange(3, 0, 5, 3), clip).Succeeded);
            DxfTable table = TableOf(document, handle);

            foreach (int row in new[] { 3, 4, 5 })
            {
                Assert.Equal("A", table.TextString(row, 0));
                Assert.Equal("B", table.TextString(row, 1));
                Assert.Equal("A", table.TextString(row, 2));
                Assert.Equal("B", table.TextString(row, 3));
                Assert.Equal(DxfCellAlignment.BottomLeft, table.Alignment(row, 2));
            }
        }

        [Fact]
        public void ACutOfALockedCell_AndAPasteOverOne_AreRefused()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "A").Succeeded);
            Assert.True(CadTableEditor.SetLocking(document, handle, One(2, 0), DxfCellState.ContentLocked).Succeeded);

            Assert.True(CadTableEditor.Cut(document, handle, One(2, 0), out CadTableCellClip none).Refused);
            Assert.Null(none);

            CadTableCellClip clip = CadTableEditor.Copy(TableOf(document, handle), One(2, 0));
            Assert.True(CadTableEditor.Paste(document, handle, One(2, 0), clip).Refused);
            Assert.True(
                CadTableEditor
                    .SetLocking(document, handle, One(2, 0), DxfCellState.ContentLocked | DxfCellState.FormatLocked)
                    .Succeeded
            );
            Assert.True(CadTableEditor.Paste(document, handle, One(3, 0), clip).Succeeded);
            Assert.Equal("A", TableOf(document, handle).TextString(3, 0));
        }

        [Fact]
        public void AFormulaPasted_MovesItsReferences_AsTheCellMoved()
        {
            Assert.Equal("=Sum(A1:A3)", CadTableFormula.Shift("=Sum(A1:A3)", 0, 0));
            Assert.Equal("=Sum(B3:B5)", CadTableFormula.Shift("=Sum(A1:A3)", 2, 1));
            Assert.Equal("=Sum($A1:$A3)+B$2", CadTableFormula.Shift("=Sum($A1:$A3)+B$1", 0, 0).Replace("B$1", "B$2"));
            Assert.Equal("=Sum($A3:$A5)+B$1", CadTableFormula.Shift("=Sum($A1:$A3)+B$1", 2, 0));
            Assert.Equal("=A1", CadTableFormula.Shift("=B2", -1, -1));
            Assert.Equal("=B1", CadTableFormula.Shift("=B1", -3, 0));
        }

        [Fact]
        public void MatchCell_GivesTheLookOfOneCell_ToOthers_KeepingTheirWords()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "src").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 3, "keep").Succeeded);
            Assert.True(CadTableEditor.SetAlignment(document, handle, One(2, 0), DxfCellAlignment.TopRight).Succeeded);
            Assert.True(
                CadTableEditor
                    .SetBackground(document, handle, One(2, 0), DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5))
                    .Succeeded
            );
            Assert.True(
                CadTableEditor.SetBorders(document, handle, One(2, 0), DxfGridLineType.AllGridLines, false).Succeeded
            );

            CadEditResult matched = CadTableEditor.MatchCell(
                document,
                handle,
                2,
                0,
                handle,
                new DxfCellRange(3, 2, 4, 3)
            );
            Assert.True(matched.Succeeded, matched.Message);

            DxfTable table = TableOf(document, handle);
            Assert.Equal("keep", table.TextString(3, 3));
            Assert.Equal(DxfCellAlignment.TopRight, table.Alignment(3, 3));
            Assert.Equal(5, table.BackgroundColor(4, 2).ColorIndex);
            Assert.False(table.GridVisibility(3, 3, DxfCellEdgeMasks.TopMask));
        }

        [Fact]
        public void TheMatchCellCommand_AsksForASourceAndDestinations_UntilEnter()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(
                CadTableEditor.SetAlignment(document, handle, One(2, 0), DxfCellAlignment.BottomRight).Succeeded
            );
            DxfTable table = TableOf(document, handle);
            CadCommandSession session = Session(document);

            Run(session, "MATCHCELL", Middle(table, 2, 0));
            Assert.Equal("Select destination cell:", session.PromptText);
            Run(session, Middle(table, 3, 1));
            Run(session, Middle(table, 4, 4));
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(DxfCellAlignment.BottomRight, TableOf(document, handle).Alignment(3, 1));
            Assert.Equal(DxfCellAlignment.BottomRight, TableOf(document, handle).Alignment(4, 4));
            Assert.Null(TableOf(document, handle).Alignment(3, 2));
        }

        [Fact]
        public void AFormulaInACell_ShowsWhatItComesTo_AndIsWorkedOutAgainWhenACellItReadsChanges()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "10").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 0, "20").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 4, 0, "30").Succeeded);

            CadEditResult sum = CadTableEditor.InsertFormula(document, handle, 5, 0, "=Sum(A3:A5)");
            Assert.True(sum.Succeeded, sum.Message);
            Assert.True(CadTableEditor.InsertFormula(document, handle, 5, 1, "=Average(A3:A5)").Succeeded);
            Assert.True(CadTableEditor.InsertFormula(document, handle, 5, 2, "=Count(A3:A5)").Succeeded);
            Assert.True(CadTableEditor.InsertFormula(document, handle, 5, 3, "=A5*2+A3").Succeeded);
            Assert.True(CadTableEditor.InsertFormula(document, handle, 5, 4, "=A6/B6").Succeeded);

            DxfTable table = TableOf(document, handle);
            Assert.Equal("=Sum(A3:A5)", table.GetFormula(5, 0, 0));
            Assert.Equal(60, Convert.ToDouble(table.GetValue(5, 0, 0)), 9);
            Assert.Equal(20, Convert.ToDouble(table.GetValue(5, 1, 0)), 9);
            Assert.Equal(3, Convert.ToDouble(table.GetValue(5, 2, 0)), 9);
            Assert.Equal(70, Convert.ToDouble(table.GetValue(5, 3, 0)), 9);
            Assert.Equal(3, Convert.ToDouble(table.GetValue(5, 4, 0)), 9);

            // A cell the formulas read is changed: they follow, in the same undo step, the one reading another's result too.
            Assert.True(CadTableEditor.SetText(document, handle, 3, 0, "50").Succeeded);
            table = TableOf(document, handle);
            Assert.Equal(90, Convert.ToDouble(table.GetValue(5, 0, 0)), 9);
            Assert.Equal(30, Convert.ToDouble(table.GetValue(5, 1, 0)), 9);
            Assert.Equal(90.0 / 30.0, Convert.ToDouble(table.GetValue(5, 4, 0)), 9);
            Assert.Equal("=Sum(A3:A5)", table.GetFormula(5, 0, 0));

            Assert.True(document.Undo());
            Assert.Equal(60, Convert.ToDouble(TableOf(document, handle).GetValue(5, 0, 0)), 9);
        }

        [Fact]
        public void AFormulaThatCannotBeWorkedOut_ShowsHashes_AndOneOfWordsIsRefused()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "words").Succeeded);

            Assert.True(CadTableEditor.InsertFormula(document, handle, 3, 0, "=Sum(A3:A5)").Succeeded);
            Assert.Equal("#####", TableOf(document, handle).TextString(3, 0));
            Assert.True(CadTableEditor.InsertFormula(document, handle, 3, 1, "Sum(A1)").Refused);
            Assert.True(CadTableEditor.InsertFormula(document, handle, 3, 1, "=").Refused);
        }

        [Fact]
        public void TheEditTableCellCommand_WritesASumFromTwoCornersPicked()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "4").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 1, "6").Succeeded);
            DxfTable table = TableOf(document, handle);
            CadCommandSession session = Session(document);

            Run(session, "EDITTABLECELL", Middle(table, 4, 1));
            Assert.Contains("Sum", session.PromptText);
            Run(session, "Sum", Middle(table, 2, 1));
            Assert.Equal("Select second corner of table cell range:", session.PromptText);
            Run(session, Middle(table, 3, 1));

            Assert.False(session.IsRunning);
            Assert.Equal("=Sum(B3:B4)", TableOf(document, handle).GetFormula(4, 1, 0));
            Assert.Equal(10, Convert.ToDouble(TableOf(document, handle).GetValue(4, 1, 0)), 9);
        }

        [Fact]
        public void AFieldInACell_ShowsTheWordsItComesTo_AndAFieldOfTheDrawingFollowsTheDrawing()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            CadEditResult field = CadTableEditor.InsertField(
                document,
                handle,
                2,
                2,
                "%<\\AcVar Filename \\f \"%fn2\">%"
            );
            Assert.True(field.Succeeded, field.Message);

            DxfTable table = TableOf(document, handle);
            Assert.Equal("\\AcVar Filename \\f \"%fn2\"", table.GetFieldCode(2, 2, 0));
            Assert.Equal("metric", table.TextString(2, 2));
            Assert.True(CadTableEditor.InsertField(document, handle, 2, 2, "not a field").Refused);
        }

        [Fact]
        public void DataFormat_GivesNumbersAKindAndAFormat_AMoneyOfTwoPlacesWithItsSign()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1234.5").Succeeded);

            CadEditResult money = CadTableEditor.SetDataFormat(
                document,
                handle,
                One(2, 0),
                DxfDataType.Double,
                DxfUnitType.Currency,
                "%lu2%pr2%ps[$,]%th44"
            );
            Assert.True(money.Succeeded, money.Message);

            DxfTable table = TableOf(document, handle);
            Assert.Equal(DxfUnitType.Currency, table.UnitType(2, 0));
            Assert.Equal("%lu2%pr2%ps[$,]%th44", table.Format(2, 0));
            Assert.Equal("$1,234.50", table.TextString(2, 0));

            Assert.True(
                CadTableEditor
                    .SetDataFormat(
                        document,
                        handle,
                        One(2, 0),
                        DxfDataType.Double,
                        DxfUnitType.Percentage,
                        "%lu2%pr1%ps[,%]%ct8[100]"
                    )
                    .Succeeded
            );
            Assert.Equal("123450.0%", TableOf(document, handle).TextString(2, 0));
        }

        [Fact]
        public void ABlockPutInACell_IsABlockCell_FittedOrAtAScaleAndTurn_AndALockedCellRefusesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            CadDocument document = CadDocument.From(db, "blocks.dxf");
            string handle = db.ModelSpace.OfType<DxfTable>().First().Handle;

            CadEditResult put = CadTableEditor.InsertBlock(
                document,
                handle,
                1,
                1,
                "BOLT",
                1.0,
                true,
                0.0,
                DxfCellAlignment.MiddleCenter
            );
            Assert.True(put.Succeeded, put.Message);

            DxfTable table = TableOf(document, handle);
            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(1, 1));
            Assert.Equal("BOLT", table.GetBlockTableRecord(1, 1).Name);
            Assert.True(table.IsAutoScale(1, 1));
            Assert.Equal(DxfCellAlignment.MiddleCenter, table.Alignment(1, 1));

            Assert.True(CadTableEditor.InsertBlock(document, handle, 1, 1, "BOLT", 2.0, false, 90.0, null).Succeeded);
            table = TableOf(document, handle);
            Assert.False(table.IsAutoScale(1, 1));
            Assert.Equal(2.0, table.BlockScale(1, 1), 9);
            Assert.Equal(Math.PI / 2.0, table.BlockRotation(1, 1), 9);

            Assert.True(CadTableEditor.InsertBlock(document, handle, 1, 0, "NOSUCH", 1.0, true, 0.0, null).Refused);
            Assert.True(CadTableEditor.SetLocking(document, handle, One(1, 0), DxfCellState.ContentLocked).Succeeded);
            Assert.True(CadTableEditor.InsertBlock(document, handle, 1, 0, "BOLT", 1.0, true, 0.0, null).Refused);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(DxfTableCellType.BlockCell, TableOf(document, handle).CellType(1, 1));
            Assert.True(document.Undo());
            Assert.Equal(DxfTableCellType.TextCell, TableOf(document, handle).CellType(1, 1));
        }

        [Fact]
        public void ABlockCellCopied_IsPastedAsABlockCellOfTheSameBlockAndScale()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            CadDocument document = CadDocument.From(db, "blocks.dxf");
            string handle = db.ModelSpace.OfType<DxfTable>().First().Handle;
            Assert.True(CadTableEditor.InsertBlock(document, handle, 1, 1, "BOLT", 2.5, false, 0.0, null).Succeeded);

            CadTableCellClip clip = CadTableEditor.Copy(TableOf(document, handle), One(1, 1));
            Assert.True(CadTableEditor.Paste(document, handle, One(2, 0), clip).Succeeded);

            DxfTable table = TableOf(document, handle);
            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(2, 0));
            Assert.Equal("BOLT", table.GetBlockTableRecord(2, 0).Name);
            Assert.Equal(2.5, table.BlockScale(2, 0), 9);
        }

        [Fact]
        public void ACellStyleMadeFromTheHeader_IsGivenToCells_WithItsLookAndItsId_AndTakenBackByRowAndColumn()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 1, "kept").Succeeded);
            Assert.Equal("Data", CadTableEditor.CellStyleOf(TableOf(document, handle), 3, 1));
            Assert.Equal(new[] { "Title", "Header", "Data" }, CadTableEditor.CellStyleNames(TableOf(document, handle)));

            CadEditResult made = CadTableEditor.CreateCellStyle(document, "Standard", "Heavy", "Header");
            Assert.True(made.Succeeded, made.Message);
            Assert.Equal(
                new[] { "Title", "Header", "Data", "Heavy" },
                CadTableEditor.CellStyleNames(TableOf(document, handle))
            );
            Assert.True(CadTableEditor.CreateCellStyle(document, "Standard", "heavy", "Data").Refused);
            Assert.True(CadTableEditor.CreateCellStyle(document, "Standard", "Other", "Nothing").Refused);

            CadEditResult given = CadTableEditor.SetCellStyle(document, handle, new DxfCellRange(3, 0, 3, 1), "Heavy");
            Assert.True(given.Succeeded, given.Message);

            DxfTable table = TableOf(document, handle);
            Assert.Equal("Heavy", CadTableEditor.CellStyleOf(table, 3, 1));
            Assert.Equal(101, table.GetCellStyleId(3, 0));
            Assert.Equal("kept", table.TextString(3, 1));
            Assert.Equal(DxfCellAlignment.MiddleCenter, table.Alignment(3, 1));
            Assert.Equal("Data", CadTableEditor.CellStyleOf(table, 4, 1));

            Assert.True(CadTableEditor.SetCellStyle(document, handle, One(3, 1), null).Succeeded);
            Assert.Equal("Data", CadTableEditor.CellStyleOf(TableOf(document, handle), 3, 1));
            Assert.Equal("Heavy", CadTableEditor.CellStyleOf(TableOf(document, handle), 3, 0));

            Assert.True(document.Undo());
            Assert.Equal("Heavy", CadTableEditor.CellStyleOf(TableOf(document, handle), 3, 1));
            Assert.True(document.Undo());
            Assert.Equal("Data", CadTableEditor.CellStyleOf(TableOf(document, handle), 3, 1));
            Assert.True(document.Undo());
            Assert.Equal(new[] { "Title", "Header", "Data" }, CadTableEditor.CellStyleNames(TableOf(document, handle)));
        }

        [Fact]
        public void TheBuiltInCellStyles_GiveTheLookOfTheirKindOfRow_ToAnyCell_AndAreNotDeletedOrRenamed()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            var style = CadTableEditor.StyleOf(TableOf(document, handle));

            Assert.True(CadTableEditor.SetCellStyle(document, handle, One(4, 2), "Title").Succeeded);

            DxfTable table = TableOf(document, handle);
            Assert.Equal(style.TextHeight(DxfRowType.TitleRow), table.TextHeight(4, 2).Value, 9);
            Assert.Equal(style.Alignment(DxfRowType.TitleRow), table.Alignment(4, 2));
            Assert.Equal("Title", CadTableEditor.CellStyleOf(table, 4, 2));

            Assert.True(CadTableEditor.DeleteCellStyle(document, "Standard", "Data").Refused);
            Assert.True(CadTableEditor.RenameCellStyle(document, "Standard", "Header", "Top").Refused);
            Assert.True(CadTableEditor.SetCellStyle(document, handle, One(4, 2), "Nothing").Refused);

            Assert.True(CadTableEditor.CreateCellStyle(document, "Standard", "Heavy", "Data").Succeeded);
            Assert.True(CadTableEditor.RenameCellStyle(document, "Standard", "Heavy", "Bold").Succeeded);
            Assert.True(CadTableEditor.CreateCellStyle(document, "Standard", "Light", "Data").Succeeded);
            Assert.True(CadTableEditor.RenameCellStyle(document, "Standard", "Light", "bold").Refused);
            Assert.True(CadTableEditor.DeleteCellStyle(document, "Standard", "Bold").Succeeded);
            Assert.Equal(
                new[] { "Title", "Header", "Data", "Light" },
                CadTableEditor.CellStyleNames(TableOf(document, handle))
            );
        }
    }
}
