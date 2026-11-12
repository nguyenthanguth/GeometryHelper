using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The implied window of the Command prompt, as AutoCAD 2021 asks it: "Specify opposite corner or
    /// [Fence/WPolygon/CPolygon]:", a point ending the box, F, WP and CP turning it into a fence or a polygon from the corner.
    /// The plan has the line A1 from (0, 0) to (100, 0) and A2 from (0, 0) to (0, 50).
    /// </summary>
    public sealed class ImpliedWindowTests
    {
        private const string Opposite = "Specify opposite corner or [Fence/WPolygon/CPolygon]:";

        [Fact]
        public void AClickOnNothing_AsksTheOppositeCorner_AndACornerTypedSelectsWhatTheWindowHolds()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Assert.True(session.StartWindow(P(-10, -10)));
            Assert.True(session.IsRunning);
            Assert.True(session.IsWindowOpen);
            Assert.Equal(Opposite, session.PromptText);
            Assert.Null(session.Prompt.BasePoint);

            Run(session, "110,60");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "A1", "A2" }, document.Selection.Handles.OrderBy(h => h).ToArray());
            Assert.Equal("Command: " + Opposite + " 110,60", session.History.Last());

            // Enter at the Command prompt runs no window again: no command was run.
            Assert.Null(session.LastCommandName);
        }

        [Fact]
        public void RightToLeftTheBoxIsACrossing_LeftToRightAWindow()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            session.StartWindow(P(-10, -10));
            Run(session, "50,10");

            Assert.Empty(document.Selection.Handles);

            session.StartWindow(P(50, 10));
            Run(session, "-10,-10");

            Assert.Equal(new[] { "A1", "A2" }, document.Selection.Handles.OrderBy(h => h).ToArray());
        }

        [Fact]
        public void FenceFromTheCorner_TakesWhatItCrosses_UntilEnter()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            session.StartWindow(P(50, -10));
            Run(session, "F");

            Assert.Equal("Specify next fence point or [Undo]:", session.PromptText);
            Assert.False(session.IsWindowOpen);
            Assert.True(session.IsWindowing);
            Assert.Equal(CadSelectionBandKind.Fence, session.Prompt.Band.Kind);
            Assert.Equal(P(50, -10), session.Prompt.Band.Points[0]);

            Run(session, P(50, 10), P(70, 300), "U", Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "A1" }, document.Selection.Handles.ToArray());
        }

        [Fact]
        public void WPolygonAndCPolygon_CloseThePolygonFromTheCorner()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            session.StartWindow(P(-10, -10));
            Run(session, "WP");

            Assert.Equal("Specify endpoint of line or [Undo]:", session.PromptText);

            Run(session, P(110, -10), P(110, 60), P(-10, 60), Enter);

            Assert.Equal(new[] { "A1", "A2" }, document.Selection.Handles.OrderBy(h => h).ToArray());

            document.Selection.Clear();
            session.StartWindow(P(90, -10));
            Run(session, "CP", P(110, -10), P(110, 10), Enter);

            Assert.Equal(new[] { "A1" }, document.Selection.Handles.ToArray());
        }

        [Fact]
        public void EnterIsRefused_EscCancels_AndEndWindowLeavesTheSelectionToTheCanvas()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            session.StartWindow(P(-10, -10));
            Run(session, Enter);

            Assert.Equal("Invalid window specification.", session.History.Last());
            Assert.Equal(Opposite, session.PromptText);

            Run(session, Esc);

            Assert.False(session.IsRunning);
            Assert.Equal("Command: " + Opposite + " *Cancel*", session.History.Last());
            Assert.Empty(document.Selection.Handles);

            session.StartWindow(P(-10, -10));

            Assert.True(session.EndWindow());
            Assert.False(session.IsRunning);
            Assert.Equal("Command: " + Opposite, session.History.Last());
            Assert.Empty(document.Selection.Handles);
            Assert.False(session.EndWindow());
        }

        [Fact]
        public void NoWindowStartsWhileACommandAsksAPoint()
        {
            CadCommandSession session = Session(Plan());
            Run(session, "LINE");

            Assert.False(session.StartWindow(P(0, 0)));
            Assert.Equal("Specify first point:", session.PromptText);
        }
    }
}
