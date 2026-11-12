using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Bullets and lists in the MTEXT editor, written as AutoCAD 2021's TextEditor writes them (probed: paragraphs "Plate",
    /// "Beam", "Column" of an MTEXT 2.5 high given each NumberingType): a hanging indent of 3 heights with its tab stop, and
    /// each paragraph opening with its label and a tab.
    /// </summary>
    public class MTextNumberingTests
    {
        private static CadMTextEditor Listed(string contents, CadMTextNumbering numbering)
        {
            var editor = new CadMTextEditor(contents, 2.5);
            editor.SelectAll();
            editor.SetNumbering(numbering);
            return editor;
        }

        [Theory]
        [InlineData(CadMTextNumbering.Number, "1.\t", "2.\t", "3.\t")]
        [InlineData(CadMTextNumbering.LetterLower, "a.\t", "b.\t", "c.\t")]
        [InlineData(CadMTextNumbering.LetterUpper, "A.\t", "B.\t", "C.\t")]
        [InlineData(
            CadMTextNumbering.Bullet,
            "{\\fSymbol|b0|i0|c2|p18;·\t}",
            "{\\fSymbol|b0|i0|c2|p18;·\t}",
            "{\\fSymbol|b0|i0|c2|p18;·\t}"
        )]
        public void ParagraphsListed_AreWrittenAsAutoCadsTextEditorWritesThem(
            CadMTextNumbering numbering,
            string first,
            string second,
            string third
        )
        {
            CadMTextEditor editor = Listed("Plate\\PBeam\\PColumn", numbering);

            Assert.Equal(
                "\\pxi-3,l3,t3;" + first + "Plate\\P" + second + "Beam\\P" + third + "Column",
                editor.Contents
            );
        }

        [Fact]
        public void OneParagraphNumbered_IsTheFirstOfItsList()
        {
            Assert.Equal("\\pxi-3,l3,t3;1.\tPlate", Listed("Plate", CadMTextNumbering.Number).Contents);
        }

        [Fact]
        public void ALabelIsReplaced_OffTakesTheListAway_AndEachIsOneUndoStep()
        {
            CadMTextEditor editor = Listed("Plate\\PBeam", CadMTextNumbering.Number);
            editor.SelectAll();
            editor.SetNumbering(CadMTextNumbering.LetterUpper);
            Assert.Equal("\\pxi-3,l3,t3;A.\tPlate\\PB.\tBeam", editor.Contents);

            editor.SelectAll();
            editor.SetNumbering(CadMTextNumbering.Off);
            Assert.Equal("Plate\\PBeam", editor.Contents);

            Assert.True(editor.Undo());
            Assert.Equal("\\pxi-3,l3,t3;A.\tPlate\\PB.\tBeam", editor.Contents);
            Assert.True(editor.Undo());
            Assert.Equal("\\pxi-3,l3,t3;1.\tPlate\\P2.\tBeam", editor.Contents);
            Assert.True(editor.Undo());
            Assert.Equal("Plate\\PBeam", editor.Contents);
        }

        [Fact]
        public void TheCaretInAParagraphNumbered_StaysWithItsLetter()
        {
            var editor = new CadMTextEditor("Plate\\PBeam", 2.5);
            editor.MoveTo(8);
            editor.SetNumbering(CadMTextNumbering.Number);

            // The second paragraph alone: "Beam" numbered 1, the caret still before its "a".
            Assert.Equal("Plate\\P\\pxi-3,l3,t3;1.\tBeam", editor.Contents);
            Assert.Equal(11, editor.Caret);
        }
    }
}
