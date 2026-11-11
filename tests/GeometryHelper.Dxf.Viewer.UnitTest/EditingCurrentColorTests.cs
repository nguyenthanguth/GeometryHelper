using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>CECOLOR, the colour of new objects ($CECOLOR), asked in AutoCAD's words and taken by the objects drawn after it.</summary>
    public class EditingCurrentColorTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        [Fact]
        public void CecolorRedMakesTheNextLineRed_AsOneUndoStep_AndEnterKeepsIt()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "CECOLOR");
            Assert.Equal("Enter new object color [Truecolor/COlorbook] <BYLAYER>:", session.PromptText);
            Run(session, "red");

            Assert.False(session.IsRunning);
            Assert.Equal(1, document.Database.Cecolor.ColorIndex);

            Run(session, "LINE", P(0, 0), P(100, 0), Enter);
            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.True(line.Color.IsByAci);
            Assert.Equal(1, line.Color.ColorIndex);

            // Enter keeps the colour, offered as the default.
            Run(session, "CECOLOR");
            Assert.Equal("Enter new object color [Truecolor/COlorbook] <red>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(1, document.Database.Cecolor.ColorIndex);

            // An index, then the line taken back and the colour with it.
            Run(session, "CECOLOR", "30");
            Assert.Equal(30, document.Database.Cecolor.ColorIndex);

            Assert.True(document.Undo());
            Assert.Equal(1, document.Database.Cecolor.ColorIndex);
            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.True(document.Database.Cecolor.IsByLayer);
        }

        [Fact]
        public void CecolorRefusesATrueColourAndTheColorbook_TheHeaderHoldingAnIndexAlone()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "CECOLOR", "10,20,30");
            Assert.False(session.IsRunning);
            Assert.True(document.Database.Cecolor.IsByLayer);
            Assert.Equal(
                "The drawing keeps the colour of new objects as an index: ByLayer, ByBlock or 1 to 255.",
                session.History.Last()
            );

            Run(session, "CECOLOR", "T");
            Assert.StartsWith("The drawing keeps the colour of new objects as an index", session.History.Last());

            Run(session, "CECOLOR", "CO");
            Assert.Equal("Color books are not available here.", session.History.Last());

            Run(session, "CECOLOR", "purple");
            Assert.StartsWith("A colour is ByLayer, ByBlock", session.History.Last());
            Assert.True(document.Database.Cecolor.IsByLayer);
            Assert.False(document.IsModified);
        }
    }
}
