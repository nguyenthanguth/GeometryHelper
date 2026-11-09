using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The options of "Select objects:", as AutoCAD 2021 offers them: Window, Crossing, BOX, Fence, WPolygon, CPolygon, Last,
    /// Previous, Group, Remove, Add, Undo, Multiple, SIngle and AUto typed, and Shift to take out what is picked. Each runs
    /// ERASE over <see cref="Plan"/>, whose objects A1 to A8 it describes.
    /// </summary>
    public class SelectObjectsOptionsTests
    {
        private readonly ITestOutputHelper _output;

        public SelectObjectsOptionsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static bool Erased(CadDocument document, string handle) => document.FindEntity(handle) == null;

        [Fact]
        public void Fence_TakesWhatItsSegmentsCross_UntilEnter()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ERASE", "F", P(50, -10));
            Assert.Equal("Specify next fence point or [Undo]:", session.PromptText);
            Assert.Equal(CadSelectionBandKind.Fence, session.Prompt.Band.Kind);

            // Across A1 at x = 50, then on to (50, 60) along nothing, then across A2 by (-10, 40).
            Run(session, P(50, 10), P(-10, 40), Enter);
            Assert.Equal("2 found", session.History.Last());
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, Enter);

            Assert.True(Erased(document, "A1"));
            Assert.True(Erased(document, "A2"));
            Assert.False(Erased(document, "A3"));
            Assert.Contains("Select objects: F", session.History);
            Assert.Contains("Specify first fence point:", session.History);
        }

        [Fact]
        public void FenceUndo_TakesBackItsLastPoint()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ERASE", "F", "50,-10", "50,10", "-10,40", "U", Enter, Enter);

            Assert.True(Erased(document, "A1"));
            Assert.False(Erased(document, "A2"));
        }

        [Fact]
        public void WPolygonAndCPolygon_TakeWhatLiesInsideAndWhatTheyTouch()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ERASE", "WP");
            Assert.Equal("First polygon point:", session.PromptText);
            Run(session, P(-10, -10), P(110, -10), P(110, 60), P(-10, 60), Enter);
            Assert.Equal("2 found", session.History.Last());

            // A crossing polygon about a point of the circle's edge touches it.
            Run(session, "CP", P(205, -5), P(215, -5), P(215, 5), Enter);
            Assert.Equal("1 found, 3 total", session.History.Last());
            Run(session, Enter);

            Assert.True(Erased(document, "A1") && Erased(document, "A2") && Erased(document, "A3"));
            Assert.False(Erased(document, "A5"));
        }

        [Fact]
        public void WindowAndCrossingTyped_TakeTheirOwnWayWhicheverWayTheBoxIsDrawn_AndBoxByItsDirection()
        {
            CadDocument document = Plan();

            // Window right to left still takes only what lies inside: the INSERT, not A1 reaching out of it.
            CadCommandSession session = Run(Session(document), "ERASE", "W");
            Assert.Equal("Specify first corner:", session.PromptText);
            Run(session, P(520, 20));
            Assert.Equal("Specify opposite corner:", session.PromptText);
            Assert.Equal(CadSelectionBandKind.Window, session.Prompt.Band.Kind);
            Run(session, P(490, -5));
            Assert.Equal("1 found", session.History.Last());

            // Crossing left to right still takes what it touches: A1 through the box.
            Run(session, "C", P(95, -5), P(105, 5));
            Assert.Equal("1 found, 2 total", session.History.Last());

            // BOX right to left is a crossing: A4's arc through it.
            Run(session, "BOX", P(325, 5), P(315, -5));
            Assert.Equal("1 found, 3 total", session.History.Last());
            Run(session, Enter);

            Assert.True(Erased(document, "A7") && Erased(document, "A1") && Erased(document, "A4"));
        }

        [Fact]
        public void RemoveAndAdd_TakeOutAndPutBack_AsAutoCadCountsThem()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ERASE", new[] { "A1", "A2", "A3" }, "R");
            Assert.Equal("Remove objects:", session.PromptText);

            Run(session, (object)new[] { "A3" });
            Assert.Equal("1 found, 1 removed, 2 total", session.History.Last());

            Run(session, "A");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A4" }, Enter);

            Assert.True(Erased(document, "A1") && Erased(document, "A2") && Erased(document, "A4"));
            Assert.False(Erased(document, "A3"));
        }

        [Fact]
        public void ShiftHeld_TakesOutWhatAPickOrABoxTakes()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ERASE", new[] { "A1", "A2", "A3" });

            session.Feed(P(50, 0), true);
            Assert.Equal("1 found, 1 removed, 2 total", session.History.Last());
            session.FeedSelection(new[] { "A2" }, true);
            Assert.Equal("1 found, 1 removed, 1 total", session.History.Last());
            Run(session, Enter);

            Assert.True(Erased(document, "A3"));
            Assert.False(Erased(document, "A1") || Erased(document, "A2"));
        }

        [Fact]
        public void Undo_TakesBackTheLastChangeOfTheSelection()
        {
            CadDocument document = Plan();
            Run(Session(document), "ERASE", new[] { "A1" }, new[] { "A2", "A3" }, "U", Enter);

            Assert.True(Erased(document, "A1"));
            Assert.False(Erased(document, "A2") || Erased(document, "A3"));
        }

        [Fact]
        public void Previous_IsTheSelectionTheCommandBeforeTook()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "MOVE", new[] { "A2", "A3" }, Enter, P(0, 0), P(0, 5));
            Run(session, "ERASE", "P");

            Assert.Equal("2 found", session.History.Last());
            Assert.Equal(new[] { "A2", "A3" }, session.Gathering);
        }

        [Fact]
        public void Last_IsTheObjectMadeLast_ThatTheSpaceShows()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "LINE", P(0, 300), P(50, 300), Enter);
            string made = document.ModelSpace.Block.Last().Handle;

            Run(session, "ERASE", "L", Enter);
            Assert.True(Erased(document, made));

            // A8 was made after A7, but with its layer off it is not shown.
            Run(session, "ERASE", "L");
            Assert.Equal(new[] { "A8" }, session.Gathering);
            Run(session, Esc);
            document.Database.LayerTable["Locked"].IsOff = true;
            document.Regen();
            Run(session, "ERASE", "L");
            Assert.Equal(new[] { "A7" }, session.Gathering);
        }

        [Fact]
        public void Single_EndsTheSelectionWithTheFirstObjectsTaken()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "ERASE", "SI", new[] { "A3" });

            Assert.False(session.IsRunning);
            Assert.True(Erased(document, "A3"));
        }

        [Fact]
        public void Multiple_FindsTheObjectsAtThePointsAtEnter_TwoAtOnePointTwice()
        {
            CadDocument document = Plan();

            // A1 and A2 meet at the origin: two picks there take both.
            CadCommandSession session = Run(Session(document), "ERASE", "M", P(0, 0), P(0, 0));
            Assert.Empty(session.Gathering);
            Run(session, Enter);
            Assert.Equal("2 found", session.History.Last());
            Run(session, Enter);

            Assert.True(Erased(document, "A1") && Erased(document, "A2"));
        }

        [Fact]
        public void Group_TakesTheGroupNamed_AndSaysWhereThereIsNone()
        {
            var db = new DxfDatabase("AC1027");
            var a = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5);
            var b = new DxfCircle(new GeoPoint3(20, 0, 0), new GeoVector3(0, 0, 1), 5);
            var c = new DxfCircle(new GeoPoint3(40, 0, 0), new GeoVector3(0, 0, 1), 5);
            db.ModelSpace.AppendEntity(a);
            db.ModelSpace.AppendEntity(b);
            db.ModelSpace.AppendEntity(c);
            CadDocument document = CadDocument.From(db, "group.dxf");
            CadCommandSession session = Run(Session(document), "-GROUP", Enter, "PAIR", "Pair", new[] { a.Handle, b.Handle }, Enter);
            Assert.Equal("Group \"PAIR\" has been created.", session.History.Last());

            Run(session, "ERASE", "G", "NONE");
            Assert.Equal("Group \"NONE\" has not been defined.", session.History.Last());
            Assert.Equal("Select objects:", session.PromptText);

            Run(session, "G", "pair");
            Assert.Equal("2 found", session.History.Last());
            Run(session, Enter);
            Assert.True(Erased(document, a.Handle) && Erased(document, b.Handle));
            Assert.False(Erased(document, c.Handle));
        }

        [Fact]
        public void AnAnswerOfNoOption_IsAnInvalidSelection_WithTheOptionsListed()
        {
            CadCommandSession session = Run(Session(Plan()), "ERASE", "XYZ");

            Assert.Equal(
                new[] { "Select objects: XYZ", "*Invalid selection*", "Expects a point or", "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon/Group/Add/Remove/Multiple/Previous/Undo/AUto/SIngle" },
                session.History.Skip(session.History.Count - 4));
            Assert.Equal("Select objects:", session.PromptText);
        }

        [Fact]
        public void AFenceAndAPolygonOverAHundredThousandObjects_AreTimed()
        {
            var db = new DxfDatabase("AC1027");

            for (int i = 0; i < 100000; i++)
            {
                double x = (i % 1000) * 10.0;
                double y = (i / 1000) * 10.0;
                db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(x, y, 0), new GeoPoint3(x + 5, y + 5, 0)));
            }

            CadDocument document = CadDocument.From(db, "big.dxf");
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            var fence = new List<GeoPoint2> { P(-1, 2.5), P(10001, 2.5), P(10001, 502.5), P(-1, 502.5) };
            var ring = new List<GeoPoint2> { P(-1, -1), P(10001, -1), P(10001, 1001), P(-1, 1001) };

            index.Fence(fence);
            var watch = Stopwatch.StartNew();
            int fenced = index.Fence(fence).Count;
            long fenceMs = watch.ElapsedMilliseconds;
            watch.Restart();
            int inside = index.WindowPolygon(ring).Count;
            long windowMs = watch.ElapsedMilliseconds;
            watch.Restart();
            int touched = index.CrossingPolygon(ring).Count;
            long crossingMs = watch.ElapsedMilliseconds;
            _output.WriteLine("Fence: " + fenceMs + " ms (" + fenced + "), WPolygon: " + windowMs + " ms (" + inside + "), CPolygon: " + crossingMs + " ms (" + touched + ")");

            // The fence crosses row 0 along y = 2.5 and row 50 along y = 502.5, its sides passing by the ends of the rows.
            Assert.Equal(2000, fenced);
            Assert.Equal(100000, inside);
            Assert.Equal(100000, touched);
            // Alone each takes under 100 ms; the bound is wide for a run beside the other suites, against a sweep gone quadratic.
            Assert.True(fenceMs < 1000 && windowMs < 1000 && crossingMs < 1000, "Too slow: " + fenceMs + ", " + windowMs + ", " + crossingMs + " ms.");
        }
    }
}
