using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A point typed after an object snap override, as AutoCAD 2021's console takes it (probed through its standard input over a
    /// line from (0, 0) to (100, 0) at 0.131 to the pixel, its aperture 1.31): the objects within the aperture of the point are
    /// looked at and the snap point of any of them nearest the point is the answer, however far from the point it lies, or "No
    /// Endpoint found for specified point." is said, then the words of the prompt, and the prompt asked again.
    /// </summary>
    public class TypedSnapTests
    {
        private static CadCommandSession Snapping(CadSnapModes mode, params object[] before)
        {
            CadCommandSession session = Run(Session(Plan()), before);
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
        public void EndTypedBeforeAPointNearALine_GivesTheEndNearestThePoint_WhereverItLies()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "LINE");

            Run(session, "60,1");

            At(session, 100, 0);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.Null(session.SnapOverride);
        }

        [Fact]
        public void EndTypedBeforeAPointNearNoObject_SaysNoEndpointFound_AndAsksTheFirstPointAgain()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "LINE");
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[] { "Specify first point: 60,5", "No Endpoint found for specified point.", "Invalid point." },
                session.History.Skip(before)
            );
            Assert.Equal("Specify first point:", session.PromptText);
            Assert.Null(session.SnapOverride);

            // The override is spent: the next point is what is typed.
            Run(session, "60,5");
            At(session, 60, 5);
        }

        [Fact]
        public void EndTypedAtAPromptWithOptions_SaysPointOrOptionKeywordRequired()
        {
            CadCommandSession session = Run(Session(Plan()), "LINE", "10,10");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Endpoint;
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: 60,5",
                    "No Endpoint found for specified point.",
                    "Point or option keyword required.",
                },
                session.History.Skip(before)
            );
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void AnObjectJustOutsideTheAperture_HasNoEndpointFound()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "LINE");

            Run(session, "60,1.4");

            Assert.Contains("No Endpoint found for specified point.", session.History);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void EndTypedAtSelectObjects_PicksTheObjectAtTheEnd()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "ERASE");

            Run(session, "60,1");

            Assert.Equal("1 found", session.History.Last());
            Assert.Equal(new[] { "A1" }, session.Gathering);
            Assert.Equal("Select objects:", session.PromptText);
        }

        [Fact]
        public void EndTypedAtSelectObjects_WithNoObjectNear_IsAnInvalidSelection_NotTheStartOfAWindow()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "ERASE");
            int before = session.History.Count;

            Run(session, "60,5");

            string[] said = session.History.Skip(before).ToArray();
            Assert.Equal(
                new[]
                {
                    "Select objects: 60,5",
                    "No Endpoint found for specified point.",
                    "*Invalid selection*",
                    "Expects a point or",
                },
                said.Take(4)
            );
            Assert.StartsWith("Window/Last/Crossing/BOX/ALL", said[4]);
            Assert.Equal("Select objects:", session.PromptText);
            Assert.False(session.IsWindowOpen);

            // Spent: the next point on nothing starts the window as it does with no override.
            Run(session, "60,5");
            Assert.True(session.IsWindowOpen);
        }

        [Fact]
        public void EndTypedAtAPromptForOneObject_WithNoObjectNear_SaysSoAndInvalidSelection()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint, "BREAK");
            int before = session.History.Count;

            Run(session, "60,5");

            Assert.Equal(
                new[]
                {
                    "Select object: 60,5",
                    "No Endpoint found for specified point.",
                    "*Invalid selection*",
                    "Expects a point or",
                    "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon",
                },
                session.History.Skip(before)
            );
            Assert.Equal("Select object:", session.PromptText);
        }

        [Fact]
        public void MidTypedBeforeAPointNearALine_GivesTheMiddleOfIt()
        {
            CadCommandSession session = Snapping(CadSnapModes.Midpoint, "LINE");

            Run(session, "20,1");

            At(session, 50, 0);
        }

        [Fact]
        public void CenTypedBeforeAPointNearACircle_GivesItsCentre_AndSaysNoCenterFoundOnALine()
        {
            CadCommandSession session = Snapping(CadSnapModes.Center, "LINE");
            Run(session, "209.9,1");
            At(session, 200, 0);

            session = Snapping(CadSnapModes.Center, "LINE");
            Run(session, "60,1");
            Assert.Contains("No Center found for specified point.", session.History);
        }

        [Fact]
        public void PerTypedBeforeAPointNearALine_GivesTheFootFromTheLastPoint()
        {
            CadCommandSession session = Run(Session(Plan()), "LINE", "50,20");
            session.SnapAperture = 1.3;
            session.SnapOverride = CadSnapModes.Perpendicular;

            Run(session, "30,1");

            At(session, 50, 0);
        }

        [Fact]
        public void NearestTypedWhereNothingIsNear_SaysNoFoundForSpecifiedPoint_WithNoNameInIt()
        {
            CadCommandSession session = Snapping(CadSnapModes.Nearest, "LINE");

            Run(session, "60,5");

            Assert.Contains("No found for specified point.", session.History);
        }

        [Fact]
        public void ASnapNotTaken_LeavesThePointAsTyped()
        {
            CadCommandSession session = Snapping(CadSnapModes.Endpoint | CadSnapModes.Midpoint, "LINE");

            Run(session, "60,5");

            At(session, 60, 5);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void NoneTyped_LeavesThePointAsTyped()
        {
            CadCommandSession session = Snapping(CadSnapModes.None, "LINE");

            Run(session, "60,1");

            At(session, 60, 1);
        }
    }
}
