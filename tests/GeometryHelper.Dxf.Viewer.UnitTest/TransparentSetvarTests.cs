using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// SETVAR run inside another command as AutoCAD 2021 runs 'SETVAR, and the Recent Input the prompts keep: points and
    /// numbers typed written in the drawing's units, the selection sets answered at Select objects named as AutoCAD names
    /// them, "Selection set (2 items)", and chosen again.
    /// </summary>
    public class TransparentSetvarTests
    {
        [Fact]
        public void SetvarTypedAtLinesNextPoint_SetsTheVariable_AndTheLineGoesOn()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            int lines = All<DxfLine>(document.ModelSpace).Count();

            Run(session, "LINE", "0,300", "'SETVAR");

            Assert.True(session.IsTransparent);
            Assert.Equal("SETVAR", session.Current.Name);
            Assert.StartsWith(">>Enter variable name or [?]", session.PromptText);

            Run(session, "LTSCALE", "2");

            Assert.Equal(2.0, document.Database.Ltscale, 9);
            Assert.False(session.IsTransparent);
            Assert.Equal("LINE", session.Current.Name);
            Assert.Equal("Resuming LINE command.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, "100,300", Enter);

            Assert.Equal(lines + 1, All<DxfLine>(document.ModelSpace).Count());
        }

        [Fact]
        public void PointsTypedInDecimalUnits_AreKeptToTheDrawingsPlaces_AsTheDrawingWritesThem()
        {
            CadDocument document = Plan();
            document.Database.Luprec = 2;
            CadCommandSession session = Session(document);

            Run(session, "LINE", "0,300", "100.456,300", "@0,50");

            Assert.Equal(new[] { "@0.00,50.00", "100.46,300.00", "0.00,300.00" }, session.RecentInput);
        }

        [Fact]
        public void SelectionSetsAnswered_AreOfferedAgainAtSelectObjects_NewestFirst_NamedByTheirCount()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            string[] handles = All<DxfLine>(document.ModelSpace).Select(l => l.Handle).ToArray();

            Run(session, "MOVE");
            Assert.Empty(session.RecentInput);
            Run(session, (object)new[] { handles[0] }, Enter, Esc);
            Run(session, "MOVE", (object)new[] { handles[0], handles[1] }, Esc);
            Run(session, "COPY");

            Assert.Equal(new[] { "Selection set (2 items)", "Selection set (1 items)" }, session.RecentInput);
            Assert.True(session.FeedRecentInput(1));
            Run(session, Enter);

            Assert.StartsWith("Specify base point", session.PromptText);
        }
    }
}
