using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The window's editor over a table cell, as AutoCAD 2021's in-place editor: each key typed lays the row out again in a
    /// preview, the cell selected and the table's frame follow it, and the drawing is written once, when the editor closes.
    /// </summary>
    public sealed class TableLiveWindowTests
    {
        private static MainViewModel Opened(out CadDocument document, out string handle)
        {
            var model = new MainViewModel();
            document = CadDocument.From(CadTemplate.CreateMetric(), "tables.dxf");
            model.OpenDocument(document);
            model.ShowInsertTable = dialog =>
            {
                dialog.Columns = "3";
                dialog.ColumnWidth = "40";
                dialog.DataRows = "2";
                dialog.Ok();
                return true;
            };
            model.Execute("TABLE");
            model.Execute("0,0");
            model.Escape();
            handle = document.ModelSpace.Block.OfType<DxfTable>().Single().Handle;
            return model;
        }

        [Fact]
        public void EachKeyTyped_LaysTheRowOutAgain_TheWindowIsToldWhenItMoves_AndEscWritesNothing()
        {
            MainViewModel model = Opened(out CadDocument document, out string handle);
            string undo = document.NextUndo.Name;
            int told = 0;
            model.TableLiveChanged += (sender, e) => told++;

            // B3, 9 high under the title (11) and the header (9), opened for typing.
            Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
            Assert.True(model.TableCellKey("F2", false));
            CadTableLive live = model.TextEditor.Edit.TableLive;
            Assert.NotNull(live);
            Assert.False(live.ChangesRows);

            // A key that leaves the row as it was tells no one.
            model.TextEditor.Editor.Type("one");
            Assert.False(live.ChangesRows);
            Assert.Equal(0, told);

            model.TextEditor.Editor.Type("\ntwo\nthree");
            Assert.True(live.ChangesRows);
            Assert.True(live.RowHeights[2] - 9.0 > 1.0);
            Assert.True(told > 0);
            Assert.NotEmpty(live.Items);

            // Lines taken out again: the row shrinks back to the 9 it was, and the table is the one in the drawing again.
            int grown = told;
            model.TextEditor.Editor.SelectAll();
            model.TextEditor.Editor.Type("one");
            Assert.False(live.ChangesRows);
            Assert.True(told > grown);
            Assert.Empty(live.Items);

            model.TextEditor.Editor.Type("\ntwo\nthree");
            model.Escape();
            Assert.False(model.IsCommandRunning);
            DxfTable table = (DxfTable)document.FindEntity(handle);
            Assert.Equal(9.0, table.RowHeight(2), 9);
            Assert.Equal(string.Empty, table.TextString(2, 1));
            Assert.Equal(undo, document.NextUndo.Name);
        }

        [Fact]
        public void TheEditorClosed_WritesTheWordsOnce_TheRowsAsThePreviewHadThem_OneUndoStep()
        {
            MainViewModel model = Opened(out CadDocument document, out string handle);
            string undo = document.NextUndo.Name;
            Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
            Assert.True(model.TableCellKey("F2", false));
            CadTableLive live = model.TextEditor.Edit.TableLive;
            model.TextEditor.Editor.Type("one\ntwo\nthree");
            double[] seen = live.RowHeights.ToArray();
            Assert.True(live.ChangesRows);

            model.CloseTextEditor(true);
            Assert.False(model.IsCommandRunning);
            DxfTable table = (DxfTable)document.FindEntity(handle);
            Assert.Equal("one\\Ptwo\\Pthree", table.TextString(2, 1));

            for (int r = 0; r < seen.Length; r++)
            {
                Assert.Equal(seen[r], table.RowHeight(r), 9);
            }

            Assert.Equal("TABLEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(undo, document.NextUndo.Name);
            Assert.Equal(9.0, ((DxfTable)document.FindEntity(handle)).RowHeight(2), 9);
        }
    }
}
