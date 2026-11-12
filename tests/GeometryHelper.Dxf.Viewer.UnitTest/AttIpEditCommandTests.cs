using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// ATTIPEDIT (ATI): the value of an attribute edited in place, in the box over it on the canvas, as AutoCAD 2021's: the
    /// attribute clicked, its words edited where it stands, the value written as one undo step.
    /// </summary>
    public class AttIpEditCommandTests
    {
        /// <summary>
        /// A title block TB, a frame 100 by 30 with attribute definitions TITLE and DRAWN, inserted as B1 at (20, 20) with the
        /// values BEAM B-12 (B2, at (25, 38), 5 high) and NGUYEN (B3, at (25, 26), 3.5 high).
        /// </summary>
        private static CadDocument TitleBlock()
        {
            string block = TestDrawings.Block(
                "TB",
                TestDrawings.Line(0, 0, 100, 0),
                TestDrawings.Line(100, 0, 100, 30),
                TestDrawings.Line(100, 30, 0, 30),
                TestDrawings.Line(0, 30, 0, 0),
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    5.0,
                    "20",
                    18.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "TITLE?",
                    "3",
                    "Title",
                    "2",
                    "TITLE",
                    "70",
                    0
                ),
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    5.0,
                    "20",
                    6.0,
                    "30",
                    0.0,
                    "40",
                    3.5,
                    "1",
                    "NAME?",
                    "3",
                    "Drawn",
                    "2",
                    "DRAWN",
                    "70",
                    0
                )
            );
            string insert =
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nTB\n10\n20\n20\n20\n30\n0\n41\n1\n42\n1\n43\n1\n"
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    1,
                    "B2",
                    "10",
                    25.0,
                    "20",
                    38.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "BEAM B-12",
                    "2",
                    "TITLE",
                    "70",
                    0
                )
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    256,
                    "B3",
                    "10",
                    25.0,
                    "20",
                    26.0,
                    "30",
                    0.0,
                    "40",
                    3.5,
                    "1",
                    "NGUYEN",
                    "2",
                    "DRAWN",
                    "70",
                    0
                )
                + "0\nSEQEND\n5\nB5\n8\n0\n";
            return CadDocument.From(TestDrawings.Drawing(null, block, insert), "titleblock.dxf");
        }

        private static string Value(CadDocument document, string attribute) =>
            Entity<DxfBlockReference>(document, "B1").AttributeCollection.First(a => a.Handle == attribute).TextString;

        [Fact]
        public void TheAttributeClicked_IsEditedWhereItStands_AndTheValueTheEditorEndsWithIsWritten_InOneStep()
        {
            CadDocument document = TitleBlock();
            CadCommandSession session = Run(Session(document), "ATTIPEDIT");
            Assert.Equal("Select an attribute:", session.PromptText);

            Run(session, P(30, 39));

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.NotNull(edit);
            Assert.Equal(CadInPlaceKind.Text, edit.Kind);
            Assert.Equal("B2", edit.Handle);
            Assert.Equal("BEAM B-12", edit.Value);
            Assert.Equal(5.0, edit.Height);
            Near(P(25, 38), edit.Location);

            Run(session, "BEAM B-14");

            Assert.Equal("BEAM B-14", Value(document, "B2"));
            Assert.Equal("NGUYEN", Value(document, "B3"));
            Assert.False(session.IsRunning);
            Assert.Equal("ATTIPEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("BEAM B-12", Value(document, "B2"));
            AssertReopens(document);
        }

        [Fact]
        public void AnotherAttributeClicked_IsTheOneEdited()
        {
            CadDocument document = TitleBlock();

            CadCommandSession session = Run(Session(document), "ATI", P(30, 27));

            Assert.Equal("B3", session.Prompt.InPlace.Handle);
            Run(session, "TRAN");
            Assert.Equal("TRAN", Value(document, "B3"));
            Assert.Equal("BEAM B-12", Value(document, "B2"));
        }

        [Fact]
        public void ABlockPickedByHandle_GivesItsFirstAttribute()
        {
            CadDocument document = TitleBlock();

            CadCommandSession session = Run(Session(document), "ATI", new[] { "B1" });

            Assert.Equal("B2", session.Prompt.InPlace.Handle);
        }

        [Fact]
        public void AnEditCancelled_OrLeftTheSame_ChangesNothing()
        {
            CadDocument document = TitleBlock();

            Run(Session(document), "ATI", P(30, 39), Enter);
            Assert.Equal("BEAM B-12", Value(document, "B2"));
            Assert.False(document.CanUndo);

            Run(Session(document), "ATI", P(30, 39), "BEAM B-12");
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AClickOnTheBlocksLines_OrAnObjectThatIsNoBlock_IsNoAttribute()
        {
            CadDocument document = TitleBlock();

            CadCommandSession onLine = Run(Session(document), "ATI", P(70, 20));
            Assert.Contains("Selected object is not an attribute.", onLine.History);
            Assert.False(onLine.IsRunning);
            Assert.False(document.CanUndo);

            CadDocument plan = Plan();
            CadCommandSession text = Run(Session(plan), "ATI", new[] { "A6" });
            Assert.Contains("Selected object is not an attribute.", text.History);
            Assert.False(plan.CanUndo);
        }
    }
}
