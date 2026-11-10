using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The MTEXT editor's changes written in AutoCAD's inline codes, by value, and the runs the display draws of them.</summary>
    public class MTextEditorTests
    {
        private static CadMTextEditor Editor(string contents = "AB CD", string font = "Arial", bool shx = false)
        {
            var editor = new CadMTextEditor(contents, 2.5, font, shx);
            editor.Select(3, 2);
            return editor;
        }

        [Fact]
        public void BoldOnTwoLettersInArial_IsWrittenAsAutoCADWritesIt()
        {
            CadMTextEditor editor = Editor();
            editor.SetBold(true);
            Assert.Equal(@"AB {\fArial|b1|i0|c0|p34;CD}", editor.Contents);

            editor.SetItalic(true);
            Assert.Equal(@"AB {\fArial|b1|i1|c0|p34;CD}", editor.Contents);
        }

        [Fact]
        public void TheLinesUnderOverAndThrough_AreWrittenAsTheirOnAndOffCodes()
        {
            CadMTextEditor editor = Editor();
            editor.SetUnderline(true);
            Assert.Equal(@"AB \LCD\l", editor.Contents);

            editor.SetOverline(true);
            editor.SetStrikethrough(true);
            Assert.Equal(@"AB \L\O\KCD\k\o\l", editor.Contents);
        }

        [Theory]
        [InlineData("colour", @"AB {\C1;CD}")]
        [InlineData("height", @"AB {\H2x;CD}")]
        [InlineData("oblique", @"AB {\Q15;CD}")]
        [InlineData("tracking", @"AB {\T1.5000;CD}")]
        [InlineData("width", @"AB {\W0.8000;CD}")]
        [InlineData("shx", @"AB {\Fromans|c0;CD}")]
        public void EachLookOfTheFormattingPanel_IsWrittenInBracesWithItsCode(string what, string written)
        {
            CadMTextEditor editor = Editor();

            switch (what)
            {
                case "colour":
                    editor.SetColorIndex(1);
                    break;
                case "height":
                    editor.SetHeight(5.0);
                    break;
                case "oblique":
                    editor.SetOblique(15.0);
                    break;
                case "tracking":
                    editor.SetTracking(1.5);
                    break;
                case "width":
                    editor.SetWidthFactor(0.8);
                    break;
                default:
                    editor.SetFont("romans", true);
                    break;
            }

            Assert.Equal(written, editor.Contents);
        }

        [Fact]
        public void AParagraphCentredAndASymbol_AreWrittenAsPxqcAndTheSymbolsCode()
        {
            CadMTextEditor editor = new CadMTextEditor("HOLES", 2.5, "Arial");
            editor.MoveTo(5);
            editor.Type("\n");
            editor.InsertSymbol("Ø", "%%c");
            editor.Type("22 ");
            editor.InsertSymbol("≈", @"\U+2248");
            editor.SetAlignment('c');

            Assert.Equal(@"HOLES\P\pxqc;%%c22 \U+2248", editor.Contents);
            Assert.Equal("HOLES\nØ22 ≈", editor.Text);
        }

        [Fact]
        public void LettersTypedAfterBoldIsSetWithNothingSelected_AreBold()
        {
            var editor = new CadMTextEditor("AB", 2.5, "Arial");
            editor.MoveTo(2);
            editor.SetBold(true);
            editor.Type("CD");

            Assert.Equal(@"AB{\fArial|b1|i0|c0|p34;CD}", editor.Contents);
        }

        [Fact]
        public void BoldOnLettersOfAnShxStyle_DoesNothing()
        {
            CadMTextEditor editor = Editor(font: "romans", shx: true);
            Assert.False(editor.CanBold);
            editor.SetBold(true);

            Assert.Equal("AB CD", editor.Contents);
            Assert.False(editor.IsModified);
        }

        [Fact]
        public void ClearFormattingChangeCaseReplaceAllAndUndo_WorkOnTheWords()
        {
            var editor = new CadMTextEditor(@"bolt {\C1;m20} and bolt", 2.5, "Arial");
            editor.SelectAll();
            editor.ClearFormatting();
            Assert.Equal("bolt m20 and bolt", editor.Contents);

            editor.ChangeCase(true);
            Assert.Equal("BOLT M20 AND BOLT", editor.Contents);
            Assert.Equal(2, editor.ReplaceAll("bolt", "nut", false));
            Assert.Equal("nut M20 AND nut", editor.Contents);

            Assert.True(editor.Undo());
            Assert.True(editor.Undo());
            Assert.True(editor.Undo());
            Assert.Equal(@"bolt {\C1;m20} and bolt", editor.Contents);
            Assert.False(editor.IsModified);
        }

        [Theory]
        [InlineData(@"{\fArial|b1|i0|c0|p34;Bold} and {\C1;red}")]
        [InlineData(@"A\\B\{C\}\PD")]
        [InlineData(@"\pxqc;TITLE\PSUB")]
        [InlineData(@"1\S1/2; %%d \LU\l")]
        [InlineData(@"{\H2x;BIG} {\Q15;\W0.8000;lean}")]
        public void WordsAutoCADWrote_ComeBackAsTheyWere_WhenNothingIsChanged(string contents)
        {
            var editor = new CadMTextEditor(contents, 2.5, "Arial");

            Assert.Equal(contents, editor.Contents);
            Assert.False(editor.IsModified);
        }

        [Fact]
        public void ATrueColour_IsReadAsWindowsWritesOne_BlueInTheHighByte()
        {
            CadMTextContent content = CadMTextContent.Parse(@"{\c255;R}", 2.5);

            Assert.Equal(0xFF0000, content.Paragraphs[0].Runs[0].Look.TrueColor);
            Assert.Equal(@"{\c255;R}", content.Write(2.5));
        }

        private static DisplayText2 Shown(string contents, string layer = "0")
        {
            var database = new DxfDatabase("AC1027");
            var mtext = new DxfMText { Contents = contents, Location = new GeoPoint3(10, 20, 0), TextHeight = 2.5, Layer = layer };
            database.ModelSpace.AppendEntity(mtext);
            CadDocument document = CadDocument.From(database);
            return Assert.IsType<DisplayText2>(Assert.Single(document.GetDisplay(document.ModelSpace).Items));
        }

        [Fact]
        public void AnMTextOfTwoColours_IsDrawnAsTwoRuns_TheSecondAfterTheFirstInItsOwnColour()
        {
            DisplayText2 shown = Shown(@"AB{\C1;CD}");

            Assert.Equal("ABCD", shown.Text);
            Assert.Equal(2, shown.Parts.Count);
            DisplayText2 first = Assert.IsType<DisplayText2>(shown.Parts[0]);
            DisplayText2 second = Assert.IsType<DisplayText2>(shown.Parts[1]);
            Assert.Equal("AB", first.Text);
            Assert.Equal("CD", second.Text);
            Assert.Equal(0xFF0000, second.Style.Rgb);
            Assert.Equal(first.Style.Rgb, shown.Style.Rgb);

            // Attached top left: the line hangs from (10, 20), the second run on the baseline of the first and after it.
            Assert.InRange(first.Y, 16.5, 17.5);
            Assert.Equal(first.Y, second.Y, 9);
            Assert.InRange(first.X, 9.0, 10.0);
            Assert.True(second.X > first.X + 1.0);
        }

        [Fact]
        public void BoldItalicUnderlineAndAHeightRun_AreDrawnInTheirFaceHeightAndLines()
        {
            DisplayText2 shown = Shown(@"A{\fArial|b1|i1|c0|p34;B}\LC\l{\H2x;D}");

            DisplayText2[] texts = shown.Parts.OfType<DisplayText2>().ToArray();
            Assert.Equal(new[] { "A", "B", "C", "D" }, texts.Select(t => t.Text).ToArray());
            Assert.True(texts[1].IsBold);
            Assert.True(texts[1].IsItalic);
            Assert.Equal("Arial", texts[1].FontName);
            Assert.Equal(5.0, texts[3].Height, 9);

            // C is underlined 0.2 of its height below its baseline, from its start to its end, where D begins.
            DisplayPolyline2 line = Assert.Single(shown.Parts.OfType<DisplayPolyline2>());
            Assert.Equal(texts[2].X, line.Xy[0], 9);
            Assert.Equal(texts[3].X, line.Xy[2], 9);
            Assert.Equal(texts[2].Y - 0.5, line.Xy[1], 9);
        }

        [Fact]
        public void AnMTextOfOneLook_IsDrawnALineAPart_AndKeepsItsWordsWhole()
        {
            DisplayText2 shown = Shown(@"\W0.8000;PLATE\PSUB");

            DisplayText2[] lines = shown.Parts.OfType<DisplayText2>().ToArray();
            Assert.Equal(new[] { "PLATE", "SUB" }, lines.Select(t => t.Text).ToArray());
            Assert.All(lines, t => Assert.Equal(0.8, t.WidthFactor, 9));
            Assert.True(lines[1].Y < lines[0].Y - 2.5);
            Assert.Equal("PLATE\nSUB", shown.Text);
        }
    }
}
