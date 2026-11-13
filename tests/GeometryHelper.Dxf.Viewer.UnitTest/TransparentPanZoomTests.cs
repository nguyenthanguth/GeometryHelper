using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// 'PAN and 'ZOOM typed inside a command, as AutoCAD 2021 runs them (probed in its console): their prompts after "&gt;&gt;",
    /// "Resuming LINE command." at their end, one nested in the other; the answers typed kept for Recent Input; and a point
    /// typed at "Select objects:" that hits nothing, which finds none and starts no window. The plan has the line A1 from
    /// (0, 0) to (100, 0) and the circle A3 of radius 10 about (200, 0), its extents centred on x = 255.
    /// </summary>
    public class TransparentPanZoomTests
    {
        private static CadCommandSession Viewed(CadDocument document, out CadView view)
        {
            CadCommandSession session = Session(document);
            view = new CadView(1000, 500);
            session.View = view;
            return session;
        }

        [Fact]
        public void ZoomTypedAtLinesNextPoint_ZoomsWithItsPromptsAfterChevrons_AndTheLineGoesOn()
        {
            CadDocument document = Plan();
            CadCommandSession session = Viewed(document, out CadView view);
            int lines = All<DxfLine>(document.ModelSpace).Count();

            Run(session, "LINE", "0,300", "'ZOOM");

            Assert.True(session.IsTransparent);
            Assert.Equal("ZOOM", session.Current.Name);
            Assert.StartsWith(">>Specify corner of window, enter a scale factor (nX or nXP) or [", session.PromptText);
            Assert.Contains("Specify next point or [Undo]: 'ZOOM", session.History);

            Run(session, "E");

            Assert.Equal(255, view.CenterX, 6);
            Assert.False(session.IsTransparent);
            Assert.Equal("LINE", session.Current.Name);
            Assert.Equal("Resuming LINE command.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.Equal(P(0, 300), session.Prompt.BasePoint);

            Run(session, "100,300", Enter);

            Assert.Equal(lines + 1, All<DxfLine>(document.ModelSpace).Count());
            Assert.Equal("LINE", session.LastCommandName);
        }

        [Fact]
        public void ZoomWindowTyped_AsksItsCornersAfterChevrons()
        {
            CadCommandSession session = Viewed(Plan(), out CadView view);

            Run(session, "LINE", "0,0", "'Z", "W");

            Assert.Equal(">>Specify first corner:", session.PromptText);

            Run(session, "0,0");

            Assert.Equal(">>Specify opposite corner:", session.PromptText);

            Run(session, "100,50");

            Assert.Equal(50, view.CenterX, 6);
            Assert.Equal(10, view.PixelsPerUnit, 6);
            Assert.Equal("Resuming LINE command.", session.History.Last());
            Assert.Equal(P(0, 0), session.LastPoint);
        }

        [Fact]
        public void PanTypedAtSelectObjects_KeepsTheObjectsGathered()
        {
            CadDocument document = Plan();
            CadCommandSession session = Viewed(document, out CadView view);
            double centre = view.CenterX;

            Run(session, "ERASE", P(50, 0), "'PAN");

            Assert.Equal(">>Specify base point or displacement:", session.PromptText);
            Assert.Equal(new[] { "A1" }, session.Gathering);

            Run(session, "0,0", "10,0");

            Assert.Equal(centre - 10, view.CenterX, 6);
            Assert.Equal("Resuming ERASE command.", session.History.Last());
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(new[] { "A1" }, session.Gathering);

            Run(session, Enter);

            Assert.Null(document.FindEntity("A1"));
        }

        [Fact]
        public void ZoomInsideATransparentPan_Nests_AndEachResumesInTurn()
        {
            CadCommandSession session = Viewed(Plan(), out CadView view);

            Run(session, "LINE", "0,0", "'PAN", "'ZOOM", "E");

            Assert.Equal(255, view.CenterX, 6);
            Assert.Equal("Resuming PAN command.", session.History.Last());
            Assert.Equal(">>Specify base point or displacement:", session.PromptText);
            Assert.True(session.IsTransparent);

            Run(session, "0,0", "0,0");

            Assert.Equal("Resuming LINE command.", session.History.Last());
            Assert.False(session.IsTransparent);
        }

        [Fact]
        public void ZoomAtZoomsOwnPrompt_IsRefusedWithZoomsWords()
        {
            CadCommandSession session = Viewed(Plan(), out _);

            Run(session, "ZOOM", "'ZOOM");

            Assert.Equal("Requires a distance, numberX, or option keyword.", session.History.Last());
            Assert.False(session.IsTransparent);
            Assert.StartsWith("Specify corner of window", session.PromptText);
        }

        [Fact]
        public void ACommandNotTransparent_IsRefused_AndItsTextGoesToThePrompt()
        {
            CadCommandSession session = Viewed(Plan(), out _);

            Run(session, "LINE", "0,0", "'CIRCLE");

            Assert.Equal(
                new[]
                {
                    "** That command may not be invoked transparently **",
                    "Specify next point or [Undo]: 'CIRCLE",
                    "Point or option keyword required.",
                },
                session.History.Skip(session.History.Count - 3)
            );
            Assert.Equal("LINE", session.Current.Name);
        }

        [Fact]
        public void EscInATransparentZoom_CancelsTheCommandItInterrupted_Too()
        {
            CadCommandSession session = Viewed(Plan(), out _);

            Run(session, "LINE", "0,0", "'ZOOM", Esc);

            Assert.False(session.IsRunning);
            Assert.False(session.IsTransparent);
            Assert.Equal(CadCommandOutcome.Cancelled, session.LastOutcome);
            Assert.Equal("LINE", session.LastCommandName);
        }

        [Fact]
        public void ZoomWithAnApostropheAtTheCommandPrompt_IsZoom()
        {
            CadCommandSession session = Viewed(Plan(), out CadView view);

            Run(session, "'ZOOM", "E");

            Assert.Equal(255, view.CenterX, 6);
            Assert.False(session.IsRunning);
            Assert.Equal("ZOOM", session.LastCommandName);
        }

        [Fact]
        public void PointsTyped_AreOfferedAgain_NewestFirst_ApartFromNumbers()
        {
            CadCommandSession session = Viewed(Plan(), out _);

            Run(session, "LINE", "0,300", "100,300", "@0,50");

            Assert.Equal(new[] { "@0.0000,50.0000", "100.0000,300.0000", "0.0000,300.0000" }, session.RecentInput);

            // A picked point and an option are not kept; a point typed again moves to the top.
            Run(session, P(500, 500), "U", "100,300");

            Assert.Equal(new[] { "100.0000,300.0000", "@0.0000,50.0000", "0.0000,300.0000" }, session.RecentInput);

            Run(session, Enter, "CIRCLE", "0,0");

            Assert.Empty(session.RecentInput);

            Run(session, "25", "CIRCLE", "50,50");

            Assert.Equal(new[] { "25.0000" }, session.RecentInput);
            Run(session, Esc);
            Assert.Empty(session.RecentInput);
        }

        [Fact]
        public void APointTypedAtSelectObjectsOnNothing_FindsNone_AndAsksAgain_StartingNoWindow()
        {
            CadCommandSession session = Viewed(Plan(), out _);

            Run(session, "ERASE");
            bool taken = session.Feed("-500,-500");

            Assert.True(taken);
            Assert.Equal(
                new[] { "Select objects: -500,-500", "0 found" },
                session.History.Skip(session.History.Count - 2)
            );
            Assert.Equal("Select objects:", session.PromptText);
            Assert.False(session.IsWindowOpen);

            Run(session, "50,0");

            Assert.Equal(new[] { "Select objects: 50,0", "1 found" }, session.History.Skip(session.History.Count - 2));
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }
    }
}
