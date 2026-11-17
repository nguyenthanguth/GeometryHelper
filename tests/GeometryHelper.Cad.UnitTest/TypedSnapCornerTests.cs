using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// An object snap override before a corner of a window or a point of a fence or a polygon, and a lone number at one, as
    /// AutoCAD 2021's console takes them (probed through its standard input over a line from (0, 0) to (100, 0) at 0.131 to
    /// the pixel, its aperture 1.31): the point typed is snapped as at any prompt for a point, and where the snap finds nothing
    /// "No Endpoint found for specified point." is said, then "Invalid window specification." at a corner (and the first
    /// corner asked again) or "Invalid response." at a fence or a polygon (and the same prompt asked again).
    /// </summary>
    public class TypedSnapCornerTests
    {
        /// <summary>A line B1 from (0, 0) to (100, 0), a short line B2 from (101, 2) to (110, 2) and B3 from (70, 5) to (80, 5).</summary>
        private static CadDocument Drawing() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    string.Empty,
                    string.Empty,
                    TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                    TestDrawings.Line(101, 2, 110, 2, handle: "B2"),
                    TestDrawings.Line(70, 5, 80, 5, handle: "B3")
                ),
                "corners.dxf"
            );

        private static CadCommandSession Snapping(CadSnapModes mode, params object[] before)
        {
            CadCommandSession session = Run(Session(Drawing()), before);
            session.SnapAperture = 1.3;
            session.SnapOverride = mode;
            return session;
        }

        [Theory]
        [InlineData("W")]
        [InlineData("C")]
        [InlineData("BOX")]
        public void EndBeforeTheFirstCornerOfAWindow_SnapsTheCorner(string option)
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "ERASE", option);

            Run(session, "60,1");

            Assert.Equal(100.0, session.Prompt.Band.Points[0].X, 6);
            Assert.Equal(0.0, session.Prompt.Band.Points[0].Y, 6);
            Assert.StartsWith("Specify opposite corner", session.PromptText);
            Assert.Null(session.SnapOverride);
        }

        [Fact]
        public void EndBeforeTheOppositeCornerOfAWindow_SnapsTheCorner()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "W", "65,6");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Endpoint;

            // The end of B1 nearest (60, 1) is (100, 0): the window from (65, 6) to it holds B3 (70, 5)-(80, 5); from (60, 1) it would not.
            Run(session, "60,1");

            Assert.Equal("1 found", session.History.Last());
            Assert.Equal(new[] { "B3" }, session.Gathering);
        }

        [Theory]
        [InlineData("W")]
        [InlineData("C")]
        [InlineData("BOX")]
        public void EndBeforeAFirstCornerWithNothingNear_IsAnInvalidWindowSpecification_AndAsksTheFirstCornerAgain(
            string option
        )
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "ERASE", option);
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[]
                {
                    "Specify first corner: 60,5",
                    "No Endpoint found for specified point.",
                    "Invalid window specification.",
                },
                session.History.Skip(before)
            );
            Assert.Equal("Specify first corner:", session.PromptText);
            Assert.Null(session.SnapOverride);

            // The override is spent: the next corner is the point typed.
            Run(session, "60,5");
            Assert.Equal(60.0, session.Prompt.Band.Points[0].X, 6);
        }

        [Fact]
        public void EndBeforeTheOppositeCornerWithNothingNear_AsksTheFirstCornerAgain()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "W", "10,10");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Endpoint;
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[]
                {
                    "Specify opposite corner: 60,5",
                    "No Endpoint found for specified point.",
                    "Invalid window specification.",
                },
                session.History.Skip(before)
            );
            Assert.Equal("Specify first corner:", session.PromptText);
        }

        [Fact]
        public void EndBeforeTheOppositeCornerOfTheImpliedWindow_NothingNear_AsksTheOppositeCornerAgain()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "150,40");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Endpoint;
            Assert.True(session.IsWindowOpen);
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[]
                {
                    "Specify opposite corner or [Fence/WPolygon/CPolygon]: 60,5",
                    "No Endpoint found for specified point.",
                    "Invalid window specification.",
                },
                session.History.Skip(before).Select(l => l.Replace("Select objects: ", string.Empty))
            );
            Assert.True(session.IsWindowOpen);
        }

        [Fact]
        public void EndBeforeTheOppositeCornerOfTheImpliedWindow_SnapsTheCorner_AndTakesWhatLiesInside()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "69,40");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Endpoint;

            // The end of B1 nearest (60, 1) is (100, 0): the window from (69, 40) to it holds B3 (70, 5)-(80, 5); from (60, 1) it would not.
            Run(session, "60,1");

            Assert.Equal("1 found", session.History.Last());
            Assert.Equal(new[] { "B3" }, session.Gathering);
        }

        [Theory]
        [InlineData("F", "Specify first fence point or pick/drag cursor:")]
        [InlineData("WP", "First polygon point or pick/drag cursor:")]
        [InlineData("CP", "First polygon point or pick/drag cursor:")]
        public void EndBeforeTheFirstPointOfAFenceOrAPolygon_SnapsIt_OrIsAnInvalidResponseAndAsksAgain(
            string option,
            string first
        )
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "ERASE", option);
            Assert.Equal(first, session.PromptText);

            Run(session, "60,1");
            Assert.Equal(100.0, session.Prompt.Band.Points[0].X, 6);
            Assert.StartsWith(
                option == "F" ? "Specify next fence point" : "Specify endpoint of line",
                session.PromptText
            );

            session = Snapping(CadSnapModes.Endpoint, "ERASE", option);
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[] { first + " 60,5", "No Endpoint found for specified point.", "Invalid response." },
                session.History.Skip(before)
            );
            Assert.Equal(first, session.PromptText);
        }

        [Fact]
        public void EndBeforeALaterPointOfAFence_NothingNear_AsksTheSamePromptAgain()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "F", "10,10");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Endpoint;
            string prompt = session.PromptText;
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[] { prompt + " 60,5", "No Endpoint found for specified point.", "Invalid response." },
                session.History.Skip(before)
            );
            Assert.Equal(prompt, session.PromptText);
        }

        [Fact]
        public void AMenuOverrideAtTheCornerOfAWindow_IsTheSameAsTypingIt()
        {
            CadCommandSession session = Snapping(CadSnapModes.Midpoint, "ERASE", "W");

            Run(session, "20,1");

            Assert.Equal(50.0, session.Prompt.Band.Points[0].X, 6);
        }

        [Theory]
        [InlineData("W")]
        [InlineData("C")]
        [InlineData("BOX")]
        public void ALoneNumberAtTheFirstCornerOfAWindow_IsThatFarFromTheLastPoint(string option)
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", option);
            session.LastPoint = new GeoPoint2(20, 5);

            Run(session, "10");

            Assert.Equal(30.0, session.Prompt.Band.Points[0].X, 6);
            Assert.Equal(5.0, session.Prompt.Band.Points[0].Y, 6);
            Assert.StartsWith("Specify opposite corner", session.PromptText);
        }

        [Fact]
        public void ALoneNumberAtTheOppositeCorner_TakesWhatTheWindowHolds()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "W", "60,0");

            // 20 along from the corner (60, 0): the window (60, 0) to (80, 0) is flat and holds nothing whole.
            Run(session, "20");

            Assert.Equal("0 found", session.History.Last());
            Assert.Equal("Select objects:", session.PromptText);
        }

        [Fact]
        public void ALoneNumberAtAFencePoint_IsThatFarFromTheLastPoint()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "F", "0,0", "50");

            Assert.Equal(50.0, session.Prompt.Band.Points[1].X, 6);
            Assert.Equal(0.0, session.Prompt.Band.Points[1].Y, 6);
        }

        [Fact]
        public void ALoneNumberAtSelectObjects_StartsTheImpliedWindowThatFarFromTheLastPoint()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE");
            session.LastPoint = new GeoPoint2(0, 40);

            Run(session, "10");

            Assert.True(session.IsWindowOpen);
            Assert.Equal(10.0, session.Prompt.Band.Points[0].X, 6);
            Assert.Equal(40.0, session.Prompt.Band.Points[0].Y, 6);
        }

        [Fact]
        public void TextThatIsNoNumberAtACorner_IsStillAnInvalidWindowSpecification()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "W");

            Run(session, "abc");

            Assert.Equal("Invalid window specification.", session.History.Last());
        }
    }
}
