using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// INT, APP, EXT, PAR and GCE typed before a point, as AutoCAD 2021's console takes them (probed through its standard input
    /// at 0.131 to the pixel, its aperture 1.31): INT and APP take the crossing of two objects under the point, or over one ask
    /// " and" for a second; EXT and PAR, which need the cursor to rest on an object, and GCE away from a closed polyline say
    /// "No found for specified point."
    /// </summary>
    public class TypedIntersectionTests
    {
        /// <summary>
        /// Two lines crossing, C1 (0, 0)-(100, 0) and C2 (50, -50)-(50, 50), a line C3 (0, 20)-(100, 20) parallel to C1, a circle C4
        /// of 20 about (200, 0), a line C5 (150, 0)-(250, 0) over its middle and a closed rectangle C6 from (300, 0) to (340, 30).
        /// </summary>
        private static CadDocument Drawing() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    string.Empty,
                    string.Empty,
                    TestDrawings.Line(0, 0, 100, 0, handle: "C1"),
                    TestDrawings.Line(50, -50, 50, 50, handle: "C2"),
                    TestDrawings.Line(0, 20, 100, 20, handle: "C3"),
                    TestDrawings.Circle(200, 0, 20, handle: "C4"),
                    TestDrawings.Line(150, 0, 250, 0, handle: "C5"),
                    TestDrawings.Polyline("0", "C6", true, 300, 0, 0, 340, 0, 0, 340, 30, 0, 300, 30, 0)
                ),
                "crossings.dxf"
            );

        /// <summary>A LINE from (10, 10) at its next point, the override set: what is typed there is snapped.</summary>
        private static CadCommandSession Snapping(CadSnapModes mode)
        {
            CadCommandSession session = Run(Session(Drawing()), "LINE", "10,10");
            session.SnapAperture = 1.3;
            session.SnapOverride = mode;
            return session;
        }

        private static void At(CadCommandSession session, double x, double y)
        {
            Assert.Equal(x, session.LastPoint.X, 6);
            Assert.Equal(y, session.LastPoint.Y, 6);
        }

        [Fact]
        public void IntOverTwoObjects_GivesTheirCrossing()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);

            Run(session, "50,1");

            At(session, 50, 0);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void IntOverOneObject_Asks_And_ForASecond_AndGivesTheirCrossing()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);

            Run(session, "10,0.5");

            Assert.Equal(" and", session.PromptText);

            Run(session, "50,30");

            Assert.Contains(" and 50,30", session.History);
            At(session, 50, 0);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void IntWithTheSecondObjectGivenByAClick_GivesTheirCrossing()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);
            Run(session, "10,0.5");

            session.Feed(new GeoPoint2(50, 30));

            At(session, 50, 0);
        }

        [Fact]
        public void IntOverALineAndACircle_GivesTheCrossingNearestTheSecondPoint()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);

            // The line C5 under the first point, the circle under the second: they cross at (180, 0) and (220, 0).
            Run(session, "190,0.3", "219,6");

            At(session, 220, 0);
        }

        [Theory]
        [InlineData("75,40")]
        [InlineData("20,0.5")]
        [InlineData("10,20")]
        public void IntAnd_WithNoCrossingToTheSecondPoint_SaysNoIntersectionFound_AndAsksThePromptAgain(string second)
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);
            Run(session, "10,0.5");
            int before = session.History.Count;

            Run(session, second);

            Assert.Equal(
                new[]
                {
                    " and " + second,
                    "No Intersection found for specified point.",
                    "Point or option keyword required.",
                },
                session.History.Skip(before)
            );
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            At(session, 10, 10);
        }

        [Fact]
        public void IntAnd_AnsweredWithText_IsRefusedAsThePromptRefusesIt()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);
            Run(session, "10,0.5");
            int before = session.History.Count;

            Run(session, "abc");

            Assert.Equal(new[] { " and abc", "Point or option keyword required." }, session.History.Skip(before));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void IntAnd_AnsweredWithEnter_IsEnterToThePrompt()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);
            Run(session, "10,0.5", Enter);

            Assert.False(session.IsRunning);
        }

        [Fact]
        public void IntOverNoObject_SaysNoIntersectionFound()
        {
            CadCommandSession session = Snapping(CadSnapModes.Intersection);
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: 60,5",
                    "No Intersection found for specified point.",
                    "Point or option keyword required.",
                },
                session.History.Skip(before)
            );
        }

        [Fact]
        public void IntAtSelectObjects_OverOneObject_AsksAnd_ThenPicksTheCrossing()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Intersection;

            Run(session, "10,0.5");
            Assert.Equal(" and", session.PromptText);
            Run(session, "50,30");

            // The crossing (50, 0) lies on both lines: the pick there takes the nearer one, 1 found, and the prompt is back.
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal("1 found", session.History.Last());
        }

        [Fact]
        public void IntAtACorner_OverOneObject_AsksAnd_ThenTakesTheCrossingAsTheCorner()
        {
            CadCommandSession session = Run(Session(Drawing()), "ERASE", "W");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Intersection;

            Run(session, "10,0.5", "50,30");

            Assert.Equal(50.0, session.Prompt.Band.Points[0].X, 6);
            Assert.Equal(0.0, session.Prompt.Band.Points[0].Y, 6);
            Assert.StartsWith("Specify opposite corner", session.PromptText);
        }

        [Fact]
        public void AppOverTwoObjects_GivesTheirCrossing_AndOverOneAsksAnd()
        {
            CadCommandSession session = Snapping(CadSnapModes.ApparentIntersection);
            Run(session, "50,1");
            At(session, 50, 0);

            session = Snapping(CadSnapModes.ApparentIntersection);
            Run(session, "10,0.5");
            Assert.Equal(" and", session.PromptText);
        }

        [Fact]
        public void AppAnd_OverTwoParallelLines_SaysNoFoundForSpecifiedPoint_WithNoName()
        {
            CadCommandSession session = Snapping(CadSnapModes.ApparentIntersection);
            Run(session, "10,0.5");
            int before = session.History.Count;

            Run(session, "10,20");

            Assert.Equal(
                new[] { " and 10,20", "No found for specified point.", "Point or option keyword required." },
                session.History.Skip(before)
            );
        }

        [Theory]
        [InlineData(CadSnapModes.Extension, "99,0.5")]
        [InlineData(CadSnapModes.Parallel, "60,0")]
        [InlineData(CadSnapModes.GeometricCenter, "50,1")]
        public void ExtParAndGce_TypedWhereNoRestingCursorGivesThem_SayNoFoundForSpecifiedPoint(
            CadSnapModes mode,
            string typed
        )
        {
            CadCommandSession session = Snapping(mode);
            int before = session.History.Count;

            Run(session, typed);

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: " + typed,
                    "No found for specified point.",
                    "Point or option keyword required.",
                },
                session.History.Skip(before)
            );
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            At(session, 10, 10);
        }

        [Fact]
        public void GceTypedOnAClosedPolyline_GivesItsCentroid()
        {
            CadCommandSession session = Snapping(CadSnapModes.GeometricCenter);

            Run(session, "320,0.5");

            At(session, 320, 15);
        }
    }
}
