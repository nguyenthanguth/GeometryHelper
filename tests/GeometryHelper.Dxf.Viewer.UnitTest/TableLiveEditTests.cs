using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A table cell typed in: while the words are typed its row grows to hold the lines (and shrinks back as they are taken
    /// out), the rows below move down and the table is drawn so, from a preview of the words; nothing is written to the drawing
    /// until the editor closes, and what it then writes is what the same words give today, one undo step.
    /// </summary>
    public class TableLiveEditTests
    {
        private const int Places = 9;

        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>A schedule of five columns 63.5 wide, a title, a header and four rows of data, made by TABLE at the origin.</summary>
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

        private static DisplayBox Union(IReadOnlyList<DisplayItem2> items) =>
            items.Select(i => i.Bounds).Aggregate((a, b) => a.Union(b));

        [Fact]
        public void TypedLines_GrowTheRow_AndLinesTakenOut_ShrinkItBackToWhatItWas()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            var live = new CadTableLive(document, handle, 2, 1);
            double[] was = Enumerable.Range(0, 6).Select(r => TableOf(document, handle).RowHeight(r)).ToArray();
            Assert.Equal(9.0, was[2], Places);
            Assert.Equal(was, live.RowHeights);
            Assert.False(live.ChangesRows);

            live.Update("one");
            Assert.False(live.ChangesRows);

            live.Update("one\\Ptwo");
            double two = live.RowHeights[2];
            Assert.True(live.ChangesRows);
            Assert.True(two > 9.0);

            live.Update("one\\Ptwo\\Pthree\\Pfour");
            Assert.True(live.RowHeights[2] > two);
            Assert.Equal(was[0], live.RowHeights[0], Places);
            Assert.Equal(was[3], live.RowHeights[3], Places);

            live.Update("one\\Ptwo");
            Assert.Equal(two, live.RowHeights[2], Places);

            live.Update(string.Empty);
            Assert.False(live.ChangesRows);
            Assert.Equal(was, live.RowHeights);
            Assert.Empty(live.Items);
        }

        public static IEnumerable<object[]> Words()
        {
            yield return new object[] { 2, 1, "HEA300 S355 with end plates both sides, galvanised and painted" };
            yield return new object[] { 2, 1, "one\\Ptwo\\Pthree\\Pfour\\Pfive" };
            yield return new object[] { 5, 4, "a" };
            yield return new object[]
            {
                0,
                0,
                "A title long enough to run over its merged row, \\Pand then onto a second line",
            };
            yield return new object[] { 1, 2, "Section and grade of the member\\Pwith a second line" };
        }

        [Theory]
        [MemberData(nameof(Words))]
        public void TheRowsOfThePreview_AreThoseTheSameWordsGiveWhenWritten(int row, int column, string words)
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            CadDocument twin = Metric();
            string twinHandle = Schedule(twin);
            var live = new CadTableLive(document, handle, row, column);

            // The title is merged across the table: its words are typed in its top left cell, and fit its whole width.
            live.Update(words);
            Assert.True(CadTableEditor.SetText(twin, twinHandle, row, column, words).Succeeded);

            DxfTable written = TableOf(twin, twinHandle);

            for (int r = 0; r < written.NumRows; r++)
            {
                Assert.Equal(written.RowHeight(r), live.RowHeights[r], Places);
            }
        }

        [Fact]
        public void ABlockMergedAcrossRows_GrowsItsLastRow_AsWhenWritten()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.True(CadTableEditor.Merge(document, handle, new DxfCellRange(2, 0, 3, 0)).Succeeded);
            CadDocument twin = Metric();
            string twinHandle = Schedule(twin);
            Assert.True(CadTableEditor.Merge(twin, twinHandle, new DxfCellRange(2, 0, 3, 0)).Succeeded);
            string words = "one\\Ptwo\\Pthree\\Pfour\\Pfive\\Psix\\Pseven\\Peight";

            var live = new CadTableLive(document, handle, 2, 0);
            live.Update(words);
            Assert.True(CadTableEditor.SetText(twin, twinHandle, 2, 0, words).Succeeded);

            DxfTable written = TableOf(twin, twinHandle);
            Assert.True(written.RowHeight(3) > 9.0);

            for (int r = 0; r < written.NumRows; r++)
            {
                Assert.Equal(written.RowHeight(r), live.RowHeights[r], Places);
            }
        }

        [Fact]
        public void TheRowsBelow_MoveDownByTheGrowth_AndTheTableIsDrawnSo()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            CadDocument twin = Metric();
            string twinHandle = Schedule(twin);
            string words = "one\\Ptwo\\Pthree\\Pfour";
            var live = new CadTableLive(document, handle, 2, 1);

            live.Update(words);
            Assert.True(CadTableEditor.SetText(twin, twinHandle, 2, 1, words).Succeeded);

            // The row grew by what the preview says; the point a text is attached at moves with it: not at the top, by the
            // growth at the bottom, by half of it in the middle.
            double grown = live.RowHeights[2] - 9.0;
            Assert.True(grown > 1.0);
            Assert.Equal(0.0, live.Shift(2).X, Places);
            Assert.Equal(0.0, live.Shift(2).Y, Places);
            Assert.Equal(-grown / 2.0, live.Shift(5).Y, Places);
            Assert.Equal(-grown, live.Shift(8).Y, Places);

            // The table drawn from the preview reaches as far as the table written, and no further.
            DisplayBox drawn = Union(live.Items);
            DisplayBox expected = twin.GetDisplay(twin.ModelSpace).Bounds;
            Assert.Equal(expected.MinX, drawn.MinX, 6);
            Assert.Equal(expected.MaxX, drawn.MaxX, 6);
            Assert.Equal(expected.MinY, drawn.MinY, 6);
            Assert.Equal(expected.MaxY, drawn.MaxY, 6);
            Assert.True(drawn.MinY < document.GetDisplay(document.ModelSpace).Bounds.MinY - 1.0);
        }

        [Fact]
        public void TheDrawing_IsNotTouched_WhileTheWordsAreTyped()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            string undo = document.NextUndo.Name;
            DisplayBox bounds = document.GetDisplay(document.ModelSpace).Bounds;
            int changes = 0;
            document.Changed += (sender, e) => changes++;
            var live = new CadTableLive(document, handle, 2, 1);

            foreach (string words in new[] { "a", "a\\Pb", "a\\Pb\\Pc\\Pd", "a" })
            {
                live.Update(words);
            }

            live.Update("a\\Pb\\Pc\\Pd");
            DxfTable table = TableOf(document, handle);
            Assert.Equal(0, changes);
            Assert.Equal(undo, document.NextUndo.Name);
            Assert.Equal(9.0, table.RowHeight(2), Places);
            Assert.Equal(string.Empty, table.TextString(2, 1));
            Assert.Equal(bounds.MinY, document.GetDisplay(document.ModelSpace).Bounds.MinY, Places);
        }

        [Fact]
        public void TheEditorOfACell_CarriesThePreview_EscLeavesTheDrawingAsItWas_AndCloseWritesOneStep()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            string undo = document.NextUndo.Name;
            double[] was = Enumerable.Range(0, 6).Select(r => TableOf(document, handle).RowHeight(r)).ToArray();

            // Esc: the preview grew the rows, the drawing did not.
            CadCommandSession session = Run(Session(document), "TABLEDIT", P(1 * 63.5 + 30, -25));
            CadTableLive live = session.Prompt.InPlace.TableLive;
            Assert.NotNull(live);
            live.Update("one\\Ptwo\\Pthree");
            Assert.True(live.ChangesRows);
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.Equal(undo, document.NextUndo.Name);
            Assert.Equal(was, Enumerable.Range(0, 6).Select(r => TableOf(document, handle).RowHeight(r)).ToArray());

            // Closed with the words: one step, and the rows are those of the preview.
            session = Run(Session(document), "TABLEDIT", P(1 * 63.5 + 30, -25));
            live = session.Prompt.InPlace.TableLive;
            live.Update("one\\Ptwo\\Pthree");
            double[] seen = live.RowHeights.ToArray();
            Run(session, "one\\Ptwo\\Pthree");
            Run(session, Esc);
            DxfTable table = TableOf(document, handle);
            Assert.Equal("one\\Ptwo\\Pthree", table.TextString(2, 1));

            for (int r = 0; r < seen.Length; r++)
            {
                Assert.Equal(seen[r], table.RowHeight(r), Places);
            }

            Assert.Equal("TABLEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(undo, document.NextUndo.Name);
            Assert.Equal(was, Enumerable.Range(0, 6).Select(r => TableOf(document, handle).RowHeight(r)).ToArray());
        }
    }
}
