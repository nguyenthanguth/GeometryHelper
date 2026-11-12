using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// MTEXT's Columns option and the MTEXT editor: the editor opens with the columns the option gave, and the MTEXT is
    /// written with the columns the editor ends with, as AutoCAD 2021's editor takes them over from the command.
    /// </summary>
    public class MTextColumnsOptionTests
    {
        private static MainViewModel StaticColumnsGiven(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(new DxfDatabase("AC1027"), "notes.dxf");
            model.OpenDocument(document, "notes.dxf");
            model.StartCommand("MTEXT");
            model.Execute("0,100");
            model.Execute("C");
            model.Execute("S");

            // The defaults: 80 heights in all, 2 columns, a gutter of 5 heights and 10 heights high.
            model.Execute("");
            model.Execute("");
            model.Execute("");
            model.Execute("");
            Assert.True(model.IsMTextEditorShown);
            return model;
        }

        [Fact]
        public void TheColumnsOptionsColumns_AreTheEditorsWhenItOpens_AndAreWrittenWhereTheEditorKeepsThem()
        {
            TextEditorViewModel editor = StaticColumnsGiven(out CadDocument document).TextEditor;
            double h = editor.Editor.Height;
            CadMTextFrame frame = editor.Editor.Frame;

            // 80 heights less a gutter of 5, shared by two columns.
            Assert.Equal(DxfColumnType.StaticColumns, frame.ColumnType);
            Assert.Equal(2, frame.ColumnCount);
            Assert.Equal(37.5 * h, frame.ColumnWidth, 9);
            Assert.Equal(5.0 * h, frame.Gutter, 9);
            Assert.Equal(80.0 * h, frame.Width, 9);
            Assert.Equal(10.0 * h, frame.Height, 9);

            editor.Editor.Type("NOTES");
            editor.CloseCommand.Execute(null);

            DxfMText mtext = Assert.IsType<DxfMText>(Assert.Single(document.ModelSpace.Block.Cast<DxfEntity>()));
            Assert.Equal(DxfColumnType.StaticColumns, mtext.ColumnType);
            Assert.Equal(2, mtext.ColumnCount);
            Assert.Equal(37.5 * h, mtext.ColumnWidth, 9);
            Assert.Equal(5.0 * h, mtext.ColumnGutterWidth, 9);
            Assert.Equal(10.0 * h, mtext.Height, 9);
        }

        [Fact]
        public void NoColumnsPickedInTheEditor_WinsOverTheColumnsOption()
        {
            TextEditorViewModel editor = StaticColumnsGiven(out CadDocument document).TextEditor;
            double h = editor.Editor.Height;
            editor.SetColumns(DxfColumnType.NoColumns);
            editor.Editor.Type("NOTES");
            editor.CloseCommand.Execute(null);

            DxfMText mtext = Assert.IsType<DxfMText>(Assert.Single(document.ModelSpace.Block.Cast<DxfEntity>()));
            Assert.Equal(DxfColumnType.NoColumns, mtext.ColumnType);
            Assert.Equal(80.0 * h, mtext.Width, 9);
        }
    }
}
