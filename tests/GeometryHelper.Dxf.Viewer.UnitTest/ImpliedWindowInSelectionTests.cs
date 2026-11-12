using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The implied window inside a command's "Select objects:", as AutoCAD 2021 asks it there: a click on nothing asks
    /// "Specify opposite corner or [Fence/WPolygon/CPolygon]:" after "Select objects:", a point ends the box, F, WP and CP turn
    /// it into a fence or a polygon from the corner, and the command asks "Select objects:" again. The plan has the line A1 from
    /// (0, 0) to (100, 0) and A2 from (0, 0) to (0, 50).
    /// </summary>
    public sealed class ImpliedWindowInSelectionTests
    {
        private const string Opposite = "Specify opposite corner or [Fence/WPolygon/CPolygon]:";

        [Fact]
        public void AtSelectObjects_AClickOnNothingAsksTheOppositeCorner_AndTheBoxGathersForTheCommand()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE");

            Assert.True(session.StartWindow(P(-10, -10)));
            Assert.True(session.IsWindowOpen);
            Assert.True(session.IsWindowing);
            Assert.Equal(Opposite, session.PromptText);
            Assert.Equal(CadSelectionBandKind.Box, session.Prompt.Band.Kind);
            Assert.Equal(P(-10, -10), session.Prompt.Band.Points[0]);

            Run(session, "110,60");

            Assert.Equal("Select objects:", session.PromptText);
            Assert.False(session.IsWindowing);
            Assert.Contains("Select objects: " + Opposite + " 110,60", session.History);
            Assert.Equal(new[] { "A1", "A2" }, session.Gathering.OrderBy(h => h).ToArray());

            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Null(document.FindEntity("A1"));
            Assert.Null(document.FindEntity("A2"));
            Assert.NotNull(document.FindEntity("A3"));
        }

        [Fact]
        public void RightToLeftTheBoxIsACrossing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE");

            session.StartWindow(P(50, 10));
            Run(session, "-10,-10");

            Assert.Equal(new[] { "A1", "A2" }, session.Gathering.OrderBy(h => h).ToArray());
        }

        [Fact]
        public void FenceFromTheCorner_GathersWhatItCrosses_AndReturnsToSelectObjects()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE");

            session.StartWindow(P(50, -10));
            Run(session, "F");

            Assert.Equal("Specify next fence point or [Undo]:", session.PromptText);
            Assert.False(session.IsWindowOpen);
            Assert.True(session.IsWindowing);
            Assert.Equal(CadSelectionBandKind.Fence, session.Prompt.Band.Kind);
            Assert.Equal(P(50, -10), session.Prompt.Band.Points[0]);
            Assert.Contains("Select objects: " + Opposite + " F", session.History);

            Run(session, P(50, 10), Enter);

            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(new[] { "A1" }, session.Gathering.ToArray());
        }

        [Fact]
        public void CPolygonFromTheCorner_GathersWhatItTouches()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE");

            session.StartWindow(P(90, -10));
            Run(session, "CP", P(110, -10), P(110, 10), Enter);

            Assert.Equal(new[] { "A1" }, session.Gathering.ToArray());
        }

        [Fact]
        public void EnterIsRefused_AndEscCancelsTheCommand()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE");

            session.StartWindow(P(-10, -10));
            Run(session, Enter);

            Assert.Equal("Invalid window specification.", session.History.Last());
            Assert.Equal(Opposite, session.PromptText);

            Run(session, Esc);

            Assert.False(session.IsRunning);
            Assert.Equal("Select objects: " + Opposite + " *Cancel*", session.History.Last());
            Assert.NotNull(document.FindEntity("A1"));
        }

        [Fact]
        public void EndWindow_LeavesTheBoxToTheCanvas_WhichAnswersSelectObjectsOnOneLine()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE");

            session.StartWindow(P(-10, -10));

            Assert.True(session.EndWindow());
            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal("Select objects: " + Opposite, session.History.Last());

            // The canvas then gives the box it shows: the line is not written twice.
            int lines = session.History.Count;
            Assert.True(session.FeedSelection(new[] { "A1", "A2" }, new DisplayBox(-10, -10, 110, 60), false));

            Assert.Equal(new[] { "A1", "A2" }, session.Gathering.OrderBy(h => h).ToArray());
            Assert.Equal(lines + 1, session.History.Count);
            Assert.Equal("2 found", session.History.Last());
        }

        [Fact]
        public void AtRemoveObjects_TheWindowTakesOut_AndAsksRemoveObjectsAgain()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "ERASE", new[] { "A1", "A2", "A3" }, "R");

            Assert.Equal("Remove objects:", session.PromptText);

            session.StartWindow(P(-10, -10));
            Run(session, "110,60");

            Assert.Contains("Remove objects: " + Opposite + " 110,60", session.History);
            Assert.Equal("Remove objects:", session.PromptText);
            Assert.Equal(new[] { "A3" }, session.Gathering.ToArray());
        }

        [Fact]
        public void NoWindowStartsInsideAnOptionOfSelectObjects()
        {
            CadCommandSession session = Session(Plan());
            Run(session, "ERASE", "W");

            Assert.False(session.StartWindow(P(0, 0)));
            Assert.Equal("Specify first corner:", session.PromptText);
        }
    }
}
