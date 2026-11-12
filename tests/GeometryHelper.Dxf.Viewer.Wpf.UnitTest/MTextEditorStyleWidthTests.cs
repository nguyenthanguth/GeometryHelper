using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A width in times (<c>\W1.2x;</c>) in an MTEXT of a style of letters 0.8 wide is 0.96 wide, as AutoCAD 2021 draws it
    /// (probed: simplex at 0.8, "{\W1.2x;ABC}" a fragment of width factor 0.96, 6.629 wide at 2.5 high), in the MTEXT editor
    /// as in the drawing, and an edit keeps it so.
    /// </summary>
    public class MTextEditorStyleWidthTests
    {
        [Fact]
        public void AWidthInTimesInAStyleOfWidth08_IsEdited096Wide_AndWrittenBackSo()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "NARROW", XScale = 0.8 });
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Contents = "{\\W1.2x;ABC}",
            };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "NARROW";
            CadDocument document = CadDocument.From(database, "narrow.dxf");
            var model = new MainViewModel();
            model.OpenDocument(document, "narrow.dxf");
            Assert.Equal(new[] { 0.96 }, Widths(document));

            Assert.True(model.EditTextAt(mtext.Handle));
            CadMTextEditor editor = model.TextEditor.Editor;
            Assert.Equal(0.96, editor.Content.Paragraphs[0].Runs[0].Look.WidthFactor.Value, 9);
            editor.MoveTo(3);
            editor.Type("D");
            model.TextEditor.CloseCommand.Execute(null);

            Assert.Equal(new[] { 0.96 }, Widths(document));
            Assert.Contains("ABCD", mtext.Contents);
        }

        /// <summary>The width factors of the runs an MTEXT is drawn as, rounded to 9 places.</summary>
        private static double[] Widths(CadDocument document) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayText2>()
                .Single()
                .Parts.OfType<DisplayText2>()
                .Select(p => System.Math.Round(p.WidthFactor, 9))
                .ToArray();
    }
}
