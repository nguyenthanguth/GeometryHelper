using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>TEXTEDIT and MTEDIT, the text edited in place, each text one undo step, as AutoCAD 2021's.</summary>
    public class TextEditTests
    {
        [Fact]
        public void TextEditOnAText_AsksForItsWordsInPlace_AndAsksForTheNextUntilEnter()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ED", P(1, 201));

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.NotNull(edit);
            Assert.Equal(CadInPlaceKind.Text, edit.Kind);
            Assert.Equal("A6", edit.Handle);
            Assert.Equal("NOTE", edit.Value);
            Assert.Equal(5.0, edit.Height);
            Near(P(0, 200), edit.Location);

            Run(session, "NOTE 2");
            Assert.Equal("NOTE 2", Entity<DxfDBText>(document, "A6").TextString);
            Assert.Equal("Select an annotation object or [Undo/Mode]:", session.PromptText);
            Assert.Equal("TEXTEDIT", document.NextUndo.Name);

            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void EachTextEdited_IsAnUndoStepOfItsOwn_AndUndoTakesTheLastBack()
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, TestDrawings.Text(0, 0, 5, "ONE", handle: "B1"), TestDrawings.Text(0, 100, 5, "TWO", handle: "B2")), "two.dxf");
            CadCommandSession session = Session(document);

            Run(session, "TEXTEDIT", new[] { "B1" }, "UNO", new[] { "B2" }, "DOS");
            Assert.Equal("DOS", Entity<DxfDBText>(document, "B2").TextString);

            Run(session, "U");
            Assert.Equal("TWO", Entity<DxfDBText>(document, "B2").TextString);
            Assert.Equal("UNO", Entity<DxfDBText>(document, "B1").TextString);

            Run(session, Enter);
            Assert.True(document.Undo());
            Assert.Equal("ONE", Entity<DxfDBText>(document, "B1").TextString);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AnEditCancelled_ChangesNothing_AndModeSingleEndsAfterOneText()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "TEXTEDIT", "M", "S", new[] { "A6" }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("NOTE", Entity<DxfDBText>(document, "A6").TextString);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void MTextEdit_WritesTheWordsTheEditorEndedWith_AndTheWidthItsFrameWasDraggedTo_InOneStep()
        {
            CadDocument document = Blank();
            Run(Session(document), "MTEXT", P(0, 100), P(60, 80), "PLATE");
            string handle = Assert.Single(All<DxfMText>(document.ModelSpace)).Handle;
            CadCommandSession session = Run(Session(document), "MTEDIT", new[] { handle });

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.Equal(CadInPlaceKind.MText, edit.Kind);
            Assert.Equal("PLATE", edit.Value);
            Assert.Equal(60.0, edit.Width);
            edit.NewWidth = 80.0;
            Run(session, @"{\C1;PLATE}");

            DxfMText mtext = Entity<DxfMText>(document, handle);
            Assert.Equal(@"{\C1;PLATE}", mtext.Contents);
            Assert.Equal(80.0, mtext.Width);
            Assert.Equal("MTEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("PLATE", Entity<DxfMText>(document, handle).Contents);
            Assert.Equal(60.0, Entity<DxfMText>(document, handle).Width);
        }

        [Fact]
        public void TheMTextCommand_AsksForItsWordsInTheEditorOverTheBox()
        {
            CadCommandSession session = Run(Session(Blank()), "MTEXT", P(10, 100), P(70, 80));

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.Equal(CadInPlaceKind.MText, edit.Kind);
            Assert.Null(edit.Handle);
            Assert.Equal(string.Empty, edit.Value);
            Near(P(10, 100), edit.Location);
            Assert.Equal(60.0, edit.Width);
        }

        [Fact]
        public void TextEditOnADimension_EditsItsTextOverride_TheMeasurementShownAsAngleBrackets()
        {
            CadDocument document = Blank();
            Run(Session(document), "DLI", P(0, 0), P(300, 0), P(150, -30));
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            CadCommandSession session = Run(Session(document), "TEXTEDIT", new[] { handle });

            Assert.Equal("<>", session.Prompt.InPlace.Value);
            Run(session, "<> TYP.");
            Assert.Equal("<> TYP.", Entity<DxfRotatedDimension>(document, handle).DimensionText);
        }
    }
}
