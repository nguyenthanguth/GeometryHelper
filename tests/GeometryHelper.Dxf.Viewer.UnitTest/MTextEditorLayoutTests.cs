using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The MTEXT editor lays its words out as the canvas draws them (<see cref="CadMTextEditorLayout"/>): the same line breaks,
    /// baselines and stacks, and a caret that follows the words as they wrap. Every text is 10 high in a font 10 above its
    /// baseline, as in <see cref="MTextLayoutTests"/>: an I is a line up at 1 moving the pen 4, and a space moves it 5.
    /// </summary>
    public sealed class MTextEditorLayoutTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private CadDocument Document(string contents, double width, Action<DxfMText> more, out DxfMText mtext)
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            mtext = new DxfMText { Contents = contents, Location = new GeoPoint3(0, 0, 0), TextHeight = 10.0, Width = width };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_SHX";
            more?.Invoke(mtext);
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return document;
        }

        /// <summary>The MTEXT drawn by the canvas, and the editor opened on it and laid out.</summary>
        private CadMTextEditorLayout Edited(string contents, double width, Action<DxfMText> more, out DisplayText2 drawn, out CadMTextEditor editor)
        {
            CadDocument document = Document(contents, width, more, out DxfMText mtext);
            drawn = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
            var edit = new CadInPlaceEdit(CadInPlaceKind.MText, mtext.Handle, mtext.Contents, new GeoPoint2(0, 0), mtext.TextHeight, 0.0, mtext.Width, mtext.TextStyleName, (int)mtext.Attachment);
            editor = new CadMTextEditor(edit.Value, edit.Height);
            var layout = new CadMTextEditorLayout(document, edit);
            layout.Update(editor, mtext.Width, (int)mtext.Attachment, mtext.LineSpacingFactor, 0xFFFFFF, 0xFFFFFF);
            return layout;
        }

        /// <summary>The editor draws every part the canvas draws, letter for letter where the canvas draws it.</summary>
        private static void SameParts(DisplayText2 drawn, CadMTextEditorLayout layout)
        {
            DisplayText2[] shown = drawn.Parts.OfType<DisplayText2>().ToArray();
            DisplayText2[] edited = layout.Parts.OfType<DisplayText2>().ToArray();
            Assert.Equal(shown.Select(t => t.Text).ToArray(), edited.Select(t => t.Text).ToArray());

            for (int i = 0; i < shown.Length; i++)
            {
                Assert.Equal(shown[i].Strokes.Count, edited[i].Strokes.Count);

                for (int s = 0; s < shown[i].Strokes.Count; s++)
                {
                    Assert.Equal(shown[i].Strokes[s].Length, edited[i].Strokes[s].Length);

                    for (int k = 0; k < shown[i].Strokes[s].Length; k++)
                    {
                        Assert.Equal(shown[i].Strokes[s][k], edited[i].Strokes[s][k], 9);
                    }
                }
            }

            Assert.Equal(drawn.Parts.OfType<DisplayPolyline2>().Count(), layout.Parts.OfType<DisplayPolyline2>().Count());
        }

        [Fact]
        public void WrappedWords_BreakAndStandOnTheBaselinesTheCanvasDrawsThem()
        {
            CadMTextEditorLayout layout = Edited("II II II", 20.0, null, out DisplayText2 drawn, out _);

            // "II II" on the first line, its baseline 10 below the top; "II" 50 / 3 below it. The pen starts at -1 for the ink to
            // stand on the edge; the space the line breaks at stays at the end of the first line, where "II II" moves the pen, 20.
            SameParts(drawn, layout);
            Assert.Equal(2, layout.LineCount);
            Assert.Equal(-10.0, layout.BaselineOf(0), 9);
            Assert.Equal(-10.0 - (50.0 / 3.0), layout.BaselineOf(1), 9);
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 1, 1, 1 }, Enumerable.Range(0, 9).Select(layout.LineOfPlace).ToArray());
            Assert.Equal(-1.0, layout.XOf(0), 9);
            Assert.Equal(20.0, layout.XOf(5), 9);
            Assert.Equal(-1.0, layout.XOf(6), 9);
            Assert.Equal(7.0, layout.XOf(8), 9);
            Assert.Equal(5, layout.LineEnd(0));
            Assert.Equal(6, layout.LineStart(1));
            Assert.Equal(8, layout.LineEnd(1));
        }

        [Fact]
        public void AtLeastAndExactSpacing_PutTheEditorsBaselinesWhereTheCanvasPutsThem()
        {
            foreach (DxfLineSpacingStyle style in new[] { DxfLineSpacingStyle.AtLeast, DxfLineSpacingStyle.Exactly })
            {
                CadMTextEditorLayout layout = Edited(@"I\P{\H2x;I}\PI", 0.0, t => t.LineSpacingStyle = style, out DisplayText2 drawn, out _);
                SameParts(drawn, layout);
                double[] feet = drawn.Parts.OfType<DisplayText2>().Select(t => t.Strokes[0][1]).ToArray();
                Assert.Equal(3, layout.LineCount);

                for (int n = 0; n < 3; n++)
                {
                    Assert.Equal(feet[n], layout.BaselineOf(n), 9);
                }
            }

            // At least, the line of the letter 20 high stands 10 lower than one of 10; exactly, 50 / 3 below the first.
            Assert.Equal(-10.0 - (50.0 / 3.0) - 10.0, Edited(@"I\P{\H2x;I}\PI", 0.0, null, out _, out _).BaselineOf(1), 9);
            Assert.Equal(-10.0 - (50.0 / 3.0), Edited(@"I\P{\H2x;I}\PI", 0.0, t => t.LineSpacingStyle = DxfLineSpacingStyle.Exactly, out _, out _).BaselineOf(1), 9);
        }

        [Fact]
        public void AStackedFraction_IsDrawnStackedInTheEditor_OnePlaceAsWideAsItMovesThePen()
        {
            CadMTextEditorLayout layout = Edited(@"I\SI/II;I", 0.0, null, out DisplayText2 drawn, out CadMTextEditor editor);

            // The stack is one place of the editor: from the pen after the first letter, 3, it moves the pen by its ink, 4, and a
            // seventh of the height; the last letter's place starts there.
            SameParts(drawn, layout);
            Assert.Equal(3, editor.Length);
            Assert.Equal(1, layout.LineCount);
            Assert.Equal(3.0, layout.XOf(1), 9);
            Assert.Equal(3.0 + 4.0 + (10.0 / 7.0), layout.XOf(2), 9);
            Assert.Equal(layout.XOf(2), layout.RightOf(1), 9);
        }

        [Fact]
        public void TypingPastTheWidth_TakesTheWordAndTheCaretToTheNextLine()
        {
            CadMTextEditorLayout layout = Edited("II II", 20.0, null, out _, out CadMTextEditor editor);
            editor.MoveTo(editor.Length);
            Assert.Equal(0, layout.LineOfPlace(editor.Caret));
            Assert.Equal(20.0, layout.XOf(editor.Caret), 9);

            // A third word does not fit 20: it goes down, and the caret after it with it, where it moves the pen to, 7.
            editor.Type(" II");
            layout.Update(editor, 20.0, 1, 1.0, 0xFFFFFF, 0xFFFFFF);
            Assert.Equal(2, layout.LineCount);
            Assert.Equal(1, layout.LineOfPlace(editor.Caret));
            Assert.Equal(7.0, layout.XOf(editor.Caret), 9);

            // An Enter at the end makes an empty last line for the caret to stand on, at the left edge.
            editor.Type("\n");
            layout.Update(editor, 20.0, 1, 1.0, 0xFFFFFF, 0xFFFFFF);
            Assert.Equal(3, layout.LineCount);
            Assert.Equal(2, layout.LineOfPlace(editor.Caret));
            Assert.Equal(0.0, layout.XOf(editor.Caret), 9);
        }

        [Fact]
        public void AClick_FindsThePlaceOfTheLineAndLetterUnderIt()
        {
            CadMTextEditorLayout layout = Edited("II II II", 20.0, null, out _, out _);

            // Left of the middle of the second line's first I (at 1 to 4 for its pen at -1 and 3): before it; past the line: its end.
            Assert.Equal(6, layout.PlaceAt(0.0, layout.BaselineOf(1) + 5.0));
            Assert.Equal(7, layout.PlaceAt(2.0, layout.BaselineOf(1) + 5.0));
            Assert.Equal(8, layout.PlaceAt(40.0, layout.BaselineOf(1)));
            Assert.Equal(5, layout.PlaceAt(40.0, layout.BaselineOf(0)));
        }
    }
}
