using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The grips of a table and their drags, as AutoCAD 2021's: the whole table's move, column and stretch grips, a cell's edge
    /// grips and its autofill grip; each drag one undo step, no column narrower than AutoCAD's minimum nor a row lower than its
    /// text needs.
    /// </summary>
    public class TableDragTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>A schedule of five columns 63.5 wide, a title 11 high, a header and four rows of data 9 high, at the origin.</summary>
        private static string Schedule(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "TABLE" && e.Payload is CadInsertTableRequest request)
                {
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };
            Run(session, "TABLE", P(0, 0), Esc);
            return Assert.Single(All<DxfTable>(document.ModelSpace)).Handle;
        }

        private static double[] Widths(DxfTable table) =>
            Enumerable.Range(0, table.NumColumns).Select(table.ColumnWidth).ToArray();

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        [Fact]
        public void AWholeTable_HasAMoveGrip_AColumnGripAtEachInnerLine_ThreeStretchGrips_AndTheBreakingGrip()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(document.FindEntity(handle));

            Assert.Equal(
                new[]
                {
                    CadGripKind.Insertion,
                    CadGripKind.TableColumn,
                    CadGripKind.TableColumn,
                    CadGripKind.TableColumn,
                    CadGripKind.TableColumn,
                    CadGripKind.TableWidth,
                    CadGripKind.TableHeight,
                    CadGripKind.TableSize,
                    CadGripKind.TableBreak,
                },
                grips.Select(g => g.Kind).ToArray()
            );
            Near(P(0, 0), grips[0].Point);
            Near(P(127, 0), grips[2].Point);
            Near(P(317.5, 0), grips[5].Point);
            Near(P(0, -56), grips[6].Point);
            Near(P(317.5, -56), grips[7].Point);
            Near(P(158.75, -56), grips[8].Point);
            Assert.True(CadGrips.Drag(document, handle, 8, P(158.75, -90)).Refused);
        }

        [Fact]
        public void AColumnGrip_DraggedEightRight_GrowsTheLeftColumn_TheRightGivesItBack_AndWithCtrlTheTableGrows()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // The grip between C and D, at 190.5 on the top edge.
            CadTableDrag drag = CadTableDrag.FromGrip(document, handle, 3);
            drag.Update(P(198.5, -3), false);
            Assert.Equal(new[] { 63.5, 63.5, 71.5, 55.5, 63.5 }, drag.Widths.ToArray());
            Assert.Equal("C width = 71.5000", drag.Tip);
            Near(P(198.5, 0), drag.Line[0]);
            Near(P(198.5, -56), drag.Line[1]);

            drag.Update(P(198.5, -3), true);
            Assert.Equal(new[] { 63.5, 63.5, 71.5, 63.5, 63.5 }, drag.Widths.ToArray());

            drag.Update(P(198.5, -3), false);
            Assert.True(drag.Commit().Succeeded);
            Assert.Equal("Column Width", document.NextUndo.Name);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(new[] { 63.5, 63.5, 71.5, 55.5, 63.5 }, Widths(table));
            Assert.Equal(317.5, table.Width, 9);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(Enumerable.Repeat(63.5, 5).ToArray(), Widths(Entity<DxfTable>(document, handle)));
        }

        [Fact]
        public void ALineDraggedPastAColumnsMinimum_StopsThere_AsAutoCadsMinimumColumnWidth()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            DxfTable table = Entity<DxfTable>(document, handle);

            // Two margins of 1.5 and the title's text of 6, as AutoCAD 2021 gave 0.37 for margins of 0.06 and a title of 0.25.
            Assert.Equal(9.0, CadTableEditor.MinimumColumnWidth(table, 2), 9);

            CadTableDrag drag = CadTableDrag.FromGrip(document, handle, 3);
            drag.Update(P(-500, -3), false);
            Assert.Equal(new[] { 63.5, 63.5, 9.0, 118.0, 63.5 }, drag.Widths.ToArray());
            drag.Update(P(900, -3), false);
            Assert.Equal(new[] { 63.5, 63.5, 118.0, 9.0, 63.5 }, drag.Widths.ToArray());

            // A width typed or set below it is held there, as AutoCAD 2021's SetColumnWidth holds it.
            Assert.True(CadTableEditor.SetColumnWidths(document, handle, 1, new[] { 2.0, 40.0 }).Succeeded);
            Assert.Equal(new[] { 63.5, 9.0, 40.0, 63.5, 63.5 }, Widths(Entity<DxfTable>(document, handle)));
            Assert.Equal("Column Width", document.NextUndo.Name);
            Assert.True(CadTableEditor.SetColumnWidths(document, handle, 4, new[] { 0.0 }).Refused);
            Assert.True(CadTableEditor.SetColumnWidths(document, handle, 4, new[] { 1.0, 2.0 }).Refused);
        }

        [Fact]
        public void ACellsEdgeGrips_ChangeTheColumnLeftOfTheLine_OrTheRowAboveIt_AndTheFirstEdgeMovesTheTable()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // The right edge of C4: C alone grows, the table with it.
            CadTableDrag right = CadTableDrag.Column(document, handle, 3, true);
            right.Update(P(200.5, -40), false);
            Assert.Equal(new[] { 63.5, 63.5, 73.5, 63.5, 63.5 }, right.Widths.ToArray());

            // The left edge of A4, the table's own: A shrinks by 10 and the table moves 10 along with its edge.
            CadTableDrag left = CadTableDrag.Column(document, handle, 0, true);
            left.Update(P(10, -40), false);
            Assert.Equal(new[] { 53.5, 63.5, 63.5, 63.5, 63.5 }, left.Widths.ToArray());
            Near(P(10, 0), left.Line[0]);
            Assert.True(left.Commit().Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(10.0, table.Position.X, 9);
            Assert.Equal(53.5, table.ColumnWidth(0), 9);

            // The bottom edge of row 4: row 4 grows by 6; dragged up past its text, held at 9.
            CadTableDrag row = CadTableDrag.Row(document, handle, 4);
            row.Update(P(100, -44), false);
            Assert.Equal(15.0, row.Heights[3], 9);
            Assert.Equal("Row 4 height = 15.0000", row.Tip);
            row.Update(P(100, 100), false);
            Assert.Equal(9.0, row.Heights[3], 9);
            row.Update(P(100, -44), false);
            Assert.True(row.Commit().Succeeded);
            Assert.Equal("Row Height", document.NextUndo.Name);
            Assert.Equal(15.0, Entity<DxfTable>(document, handle).RowHeight(3), 9);

            // A width typed into the field of dynamic input.
            CadTableDrag typed = CadTableDrag.Column(document, handle, 3, true);
            Assert.True(typed.TypeSize(38));
            Assert.Equal(38.0, typed.Widths[2], 9);
            Assert.False(CadTableDrag.Size(document, handle, true, false).TypeSize(38));
        }

        [Fact]
        public void TheStretchGrips_ScaleEveryColumnOrRow_InProportion_RowsHeldAtTheirText()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            DxfTable table = Entity<DxfTable>(document, handle);
            int width = CadGrips.Of(table).ToList().FindIndex(g => g.Kind == CadGripKind.TableWidth);
            int both = CadGrips.Of(table).ToList().FindIndex(g => g.Kind == CadGripKind.TableSize);

            CadTableDrag stretch = CadTableDrag.FromGrip(document, handle, width);
            stretch.Update(P(635, 0), false);
            Assert.Equal(Enumerable.Repeat(127.0, 5).ToArray(), stretch.Widths.ToArray());
            Near(P(635, -56), stretch.Outline[2]);

            CadTableDrag size = CadTableDrag.FromGrip(document, handle, both);
            size.Update(P(158.75, -28), false);
            Assert.Equal(new[] { 11.0, 9.0, 9.0, 9.0, 9.0, 9.0 }, size.Heights.ToArray());
            Assert.Equal(Enumerable.Repeat(31.75, 5).ToArray(), size.Widths.ToArray());
            size.Update(P(317.5, -112), false);
            Assert.True(size.Commit().Succeeded);
            Assert.Equal("Table Size", document.NextUndo.Name);
            Assert.Equal(112.0, Entity<DxfTable>(document, handle).Height, 9);

            // The same through the grip edit's drag, as STRETCH of the grip makes it.
            Assert.True(CadGrips.Drag(document, handle, width, P(381, 0)).Succeeded);
            Assert.Equal(
                Enumerable.Repeat(76.2, 5).ToArray(),
                Widths(Entity<DxfTable>(document, handle)).Select(w => Math.Round(w, 9)).ToArray()
            );
        }

        [Fact]
        public void TheAutofillGrip_OfAOne_DraggedDownThreeCells_PreviewsAndFills2_3_4_InOneUndoStep()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Entity<DxfTable>(document, handle).SetValue(2, 0, 1.0);

            CadTableDrag drag = CadTableDrag.Autofill(document, handle, new DxfCellRange(2, 0, 2, 0));
            Assert.Equal(CadTableDrag.FillSeries, drag.FillOptions);
            drag.Update(P(30, -52), false);
            Assert.Equal(new DxfCellRange(3, 0, 5, 0), drag.FillRange);
            Assert.Equal(new[] { 2.0, 3.0, 4.0 }, drag.Values.Select(v => Number(v.Text)).ToArray());
            Assert.Equal(4.0, Number(drag.Tip));
            Near(P(31.75, -51.5), drag.Values[2].Center);

            // A drag that has moved more cells across than down fills along the row.
            drag.Update(P(250, -30), false);
            Assert.Equal(new DxfCellRange(2, 1, 2, 3), drag.FillRange);

            // Copy Cells, chosen from the menu of the drag, copies the 1.
            drag.Update(P(30, -52), false);
            drag.FillOptions = DxfTableFillOptions.CopyContent | DxfTableFillOptions.CopyFormat;
            Assert.Equal(new[] { 1.0, 1.0, 1.0 }, drag.Values.Select(v => Number(v.Text)).ToArray());
            drag.FillOptions = CadTableDrag.FillSeries;

            Assert.True(drag.Commit().Succeeded);
            Assert.Equal("Autofill", document.NextUndo.Name);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(
                new[] { 2.0, 3.0, 4.0 },
                new[] { 3, 4, 5 }.Select(r => Number(table.TextString(r, 0))).ToArray()
            );
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Equal(string.Empty, Entity<DxfTable>(document, handle).TextString(5, 0));
        }

        [Fact]
        public void TheAutofillGrip_DraggedUp_CountsBack_AndStopsShortOfAMergedBlock()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Entity<DxfTable>(document, handle).SetValue(5, 0, 1.0);

            // Up from A6 into A3: 0, -1 and -2 above it, as AutoCAD 2021 filled 1 up with Reverse.
            CadTableDrag drag = CadTableDrag.Autofill(document, handle, new DxfCellRange(5, 0, 5, 0));
            drag.Update(P(30, -25), false);
            Assert.Equal(new DxfCellRange(2, 0, 4, 0), drag.FillRange);
            Assert.Equal(new[] { -2.0, -1.0, 0.0 }, drag.Values.Select(v => Number(v.Text)).ToArray());

            // Up into the title, the header fills and the title merged A1:E1 is left as it is.
            drag.Update(P(30, -2), false);
            Assert.Equal(new DxfCellRange(1, 0, 4, 0), drag.FillRange);

            // A block merged in A4:B4 stops a drag from A6 short of it.
            Assert.True(CadTableEditor.Merge(document, handle, new DxfCellRange(3, 0, 3, 1)).Succeeded);
            drag = CadTableDrag.Autofill(document, handle, new DxfCellRange(5, 0, 5, 0));
            drag.Update(P(30, -2), false);
            Assert.Equal(new DxfCellRange(4, 0, 4, 0), drag.FillRange);

            // Not past the source's edge, nothing to fill.
            drag.Update(P(30, -54), false);
            Assert.Null(drag.FillRange);
            Assert.Empty(drag.Commit().Records);
        }

        [Fact]
        public void TheCornersOfARangeOfSeveralRows_AreARectangle_TheTitleMergedTakenWhole()
        {
            CadDocument document = Metric();
            DxfTable table = Entity<DxfTable>(document, Schedule(document));

            GeoPoint2[] range = CadTableEditor.Corners(table, new DxfCellRange(2, 1, 3, 2));
            Near(P(63.5, -20), range[0]);
            Near(P(190.5, -20), range[1]);
            Near(P(190.5, -38), range[2]);
            Near(P(63.5, -38), range[3]);

            GeoPoint2[] whole = CadTableEditor.Corners(table, new DxfCellRange(0, 0, 5, 4));
            Near(P(317.5, 0), whole[1]);
            Near(P(0, -56), whole[3]);
        }

        [Fact]
        public void TheCellNearAPoint_IsHeldAtTheTablesEdge_OffTheTable()
        {
            CadDocument document = Metric();
            DxfTable table = Entity<DxfTable>(document, Schedule(document));

            Assert.True(CadTableEditor.CellNear(table, P(100, -25), out int row, out int column));
            Assert.Equal((2, 1), (row, column));
            Assert.True(CadTableEditor.CellNear(table, P(-50, 40), out row, out column));
            Assert.Equal((0, 0), (row, column));
            Assert.True(CadTableEditor.CellNear(table, P(900, -900), out row, out column));
            Assert.Equal((5, 4), (row, column));
        }

        [Fact]
        public void ATableWhoseRowsFlowUp_FindsItsCells_AndItsLinesAndAutofillGoUp()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // Mirrored in the line y = 0, the schedule's rows flow up from its point at the origin: the title from 0 to 11, the
            // header to 20, the rows of data 9 high above it.
            Entity<DxfTable>(document, handle).TransformBy(GeoTransform3.Scaling(1.0, -1.0, 1.0));
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(DxfFlowDirection.BottomToTop, table.FlowDirection);

            Assert.True(CadTableEditor.CellNear(table, P(100, 25), out int row, out int column));
            Assert.Equal((2, 1), (row, column));
            Assert.True(CadTableEditor.CellNear(table, P(-50, -40), out row, out column));
            Assert.Equal((0, 0), (row, column));
            Assert.True(CadTableEditor.CellNear(table, P(900, 900), out row, out column));
            Assert.Equal((5, 4), (row, column));

            // The line over row 4, at 38, dragged up to 44: row 4 grows by 6.
            CadTableDrag line = CadTableDrag.Row(document, handle, 4);
            line.Update(P(100, 44), false);
            Assert.Equal(15.0, line.Heights[3], 9);
            Near(P(0, 44), line.Line[0]);
            Near(P(317.5, 44), line.Line[1]);

            // The title's own edge, at the bottom, dragged down 10: the title grows and the table moves down with its edge.
            CadTableDrag first = CadTableDrag.Row(document, handle, 0);
            first.Update(P(100, -10), false);
            Assert.Equal(21.0, first.Heights[0], 9);
            Assert.True(first.Commit().Succeeded);
            table = Entity<DxfTable>(document, handle);
            Assert.Equal(-10.0, table.Position.Y, 9);
            Assert.Equal(21.0, table.RowHeight(0), 9);

            // A 1 in A3, from 20 to 29, filled up into A6: 2, 3 and 4.
            table.SetValue(2, 0, 1.0);
            CadTableDrag fill = CadTableDrag.Autofill(document, handle, new DxfCellRange(2, 0, 2, 0));
            fill.Update(P(30, 52), false);
            Assert.Equal(new DxfCellRange(3, 0, 5, 0), fill.FillRange);
            Assert.Equal(new[] { 2.0, 3.0, 4.0 }, fill.Values.Select(v => Number(v.Text)).ToArray());
            Near(P(31.75, 51.5), fill.Values[2].Center);
        }

        [Fact]
        public void AWholeNumberTypedInACell_IsKeptAsANumber_SoThatTheAutofillCountsOn()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // As AutoCAD 2021 reads a text it is given to parse (ConvertTextToValue): 1 a whole number, 007 the number 7.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, " 007 ").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "-3").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(DxfDataType.Long, table.DataType(2, 0));
            Assert.Equal("1", table.TextString(2, 0));
            Assert.Equal("7", table.TextString(2, 1));
            Assert.Equal(DxfDataType.Long, table.DataType(2, 2));

            CadTableDrag drag = CadTableDrag.Autofill(document, handle, new DxfCellRange(2, 0, 2, 0));
            drag.Update(P(30, -52), false);
            Assert.Equal(new[] { 2.0, 3.0, 4.0 }, drag.Values.Select(v => Number(v.Text)).ToArray());

            // Words stay words, and so does a number in a cell made to hold text.
            Assert.True(CadTableEditor.SetText(document, handle, 3, 0, "b1").Succeeded);
            Assert.Equal(DxfDataType.String, Entity<DxfTable>(document, handle).DataType(3, 0));
            Entity<DxfTable>(document, handle).SetDataType(3, 1, DxfDataType.String, DxfUnitType.Unitless);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 1, "12").Succeeded);
            Assert.Equal(DxfDataType.String, Entity<DxfTable>(document, handle).DataType(3, 1));
            Assert.Equal("12", Entity<DxfTable>(document, handle).TextString(3, 1));
            AssertReopens(document);
        }

        [Fact]
        public void RemoveOverrides_TakesTheCellsLooksBack_InOneUndoStep()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(
                CadTableEditor
                    .SetAlignment(document, handle, new DxfCellRange(2, 1, 3, 2), DxfCellAlignment.MiddleLeft)
                    .Succeeded
            );
            Assert.Equal(DxfCellAlignment.MiddleLeft, Entity<DxfTable>(document, handle).Alignment(3, 2));

            Assert.True(CadTableEditor.RemoveOverrides(document, handle, new DxfCellRange(2, 1, 3, 2)).Succeeded);
            Assert.Null(Entity<DxfTable>(document, handle).Alignment(3, 2));
            Assert.Equal("TABLEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(DxfCellAlignment.MiddleLeft, Entity<DxfTable>(document, handle).Alignment(3, 2));
        }
    }
}
