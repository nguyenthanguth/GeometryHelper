using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The grip modes of AutoCAD 2021: a grip clicked asks "** STRETCH **", Enter or Space goes on to MOVE, ROTATE, SCALE and
    /// MIRROR, a mode's name typed goes to it, Copy leaves the originals, several hot grips stretch together, and the whole
    /// grip edit is one undo step.
    /// </summary>
    public class GripModeTests
    {
        private static KeyValuePair<string, int> Grip(string handle, int index) => new KeyValuePair<string, int>(handle, index);

        private static CadCommandSession GripEdit(CadDocument document, params KeyValuePair<string, int>[] hot)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Assert.True(session.StartGripEdit(hot));
            return session;
        }

        [Fact]
        public void AGripClicked_AsksStretchInAutoCadsWords_AndAPointStretchesIt_AsOneUndoStep()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });

            CadCommandSession session = GripEdit(document, Grip("A1", 1));
            Assert.True(session.IsGripEditing);
            Assert.Equal("** STRETCH **", session.History.Last());
            Assert.Equal("Specify stretch point or [Base point/Copy/Undo/eXit]:", session.PromptText);
            Near(P(100, 0), session.Prompt.BasePoint.Value);

            Run(session, P(100, 30));

            Assert.False(session.IsRunning);
            Near(new GeoPoint3(100, 30, 0), Entity<DxfLine>(document, "A1").EndPoint);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.Equal(new[] { "A1" }, document.Selection.Handles);

            Assert.True(document.Undo());
            Near(new GeoPoint3(100, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void EnterCyclesStretchMoveRotateScaleMirror_AndAModesNameTypedGoesToIt()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });
            CadCommandSession session = GripEdit(document, Grip("A1", 0));

            Run(session, Enter);
            Assert.Equal("** MOVE **", session.History.Last());
            Assert.Equal("Specify move point or [Base point/Copy/Undo/eXit]:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("** ROTATE **", session.History.Last());
            Assert.Equal("Specify rotation angle or [Base point/Copy/Undo/Reference/eXit]:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("** SCALE **", session.History.Last());
            Assert.Equal("Specify scale factor or [Base point/Copy/Undo/Reference/eXit]:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("** MIRROR **", session.History.Last());
            Assert.Equal("Specify second point or [Base point/Copy/Undo/eXit]:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("** STRETCH **", session.History.Last());

            Run(session, "MO");
            Assert.Equal("** MOVE **", session.History.Last());
            Run(session, "ro");
            Assert.Equal("** ROTATE **", session.History.Last());
            Run(session, "SCALE");
            Assert.Equal("** SCALE **", session.History.Last());
            Run(session, "MI");
            Assert.Equal("** MIRROR **", session.History.Last());
            Run(session, "ST");
            Assert.Equal("** STRETCH **", session.History.Last());

            Run(session, "X");
            Assert.False(session.IsRunning);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void MoveRotateScaleAndMirror_TakeTheObjectsSelected_FromTheBasePoint()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1", "A2" });

            // MOVE: both lines by the way from the grip at (0, 0) to (10, 20).
            Run(GripEdit(document, Grip("A1", 0)), "MO", P(10, 20));
            Near(new GeoPoint3(10, 20, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Near(new GeoPoint3(10, 70, 0), Entity<DxfLine>(document, "A2").EndPoint);

            // ROTATE: a quarter turn typed about the grip at (10, 20).
            Run(GripEdit(document, Grip("A1", 0)), "RO", "90");
            Near(new GeoPoint3(10, 120, 0), Entity<DxfLine>(document, "A1").EndPoint);
            Near(new GeoPoint3(-40, 20, 0), Entity<DxfLine>(document, "A2").EndPoint);

            // SCALE: twice from (10, 20).
            Run(GripEdit(document, Grip("A1", 0)), "SC", "2");
            Near(new GeoPoint3(10, 220, 0), Entity<DxfLine>(document, "A1").EndPoint);

            // MIRROR: across the line from (10, 20) up the Y axis, the originals mirrored in place.
            Run(GripEdit(document, Grip("A1", 0)), "MI", P(10, 100));
            Near(new GeoPoint3(110, 20, 0), Entity<DxfLine>(document, "A2").EndPoint);
            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count(l => l.Handle == "A1" || l.Handle == "A2"));

            // Each grip edit was one step.
            for (int i = 0; i < 4; i++)
            {
                Assert.True(document.Undo());
            }

            Near(new GeoPoint3(0, 50, 0), Entity<DxfLine>(document, "A2").EndPoint);
        }

        [Fact]
        public void CopyLeavesTheOriginals_AsksAgainUntilExit_UndoTakesTheLastCopyBack_AndAllIsOneStep()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A3" });
            int circles = All<DxfCircle>(document.ModelSpace).Count;

            CadCommandSession session = GripEdit(document, Grip("A3", 0));
            Run(session, "MO", "C");
            Assert.Equal("** MOVE (multiple) **", session.History.Last());
            Run(session, P(200, 50), P(200, 100), P(200, 150));
            Assert.True(session.IsRunning);
            Assert.Equal(circles + 3, All<DxfCircle>(document.ModelSpace).Count);

            Run(session, "U");
            Assert.Equal(circles + 2, All<DxfCircle>(document.ModelSpace).Count);
            Run(session, "X");

            Assert.False(session.IsRunning);
            Near(new GeoPoint3(200, 0, 0), Entity<DxfCircle>(document, "A3").Center);
            Assert.True(document.Undo());
            Assert.Equal(circles, All<DxfCircle>(document.ModelSpace).Count);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void CtrlHeldAtTheFirstPoint_TurnsCopyOn_AStretchedCopyLeavingTheLine()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A2" });
            int lines = All<DxfLine>(document.ModelSpace).Count;

            CadCommandSession session = GripEdit(document, Grip("A2", 1));
            Assert.True(session.Feed(P(30, 50), false, true));

            Assert.True(session.IsRunning);
            Assert.Equal("** STRETCH (multiple) **", session.History.Last());
            Assert.Equal(lines + 1, All<DxfLine>(document.ModelSpace).Count);
            Near(new GeoPoint3(0, 50, 0), Entity<DxfLine>(document, "A2").EndPoint);
            DxfLine copy = All<DxfLine>(document.ModelSpace).Last();
            Near(new GeoPoint3(0, 0, 0), copy.StartPoint);
            Near(new GeoPoint3(30, 50, 0), copy.EndPoint);

            // A point picked with no Ctrl afterwards copies again; Esc ends, keeping the copies.
            Run(session, P(-30, 50), Esc);
            Assert.Equal(lines + 2, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void SeveralHotGripsStretchTogether_ALinesTwoEndsMoveItWhole_ACircleKeepsItsRadius()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1", "A3" });

            Run(GripEdit(document, Grip("A1", 0), Grip("A1", 1), Grip("A3", 0), Grip("A3", 1)), P(10, 10));

            Near(new GeoPoint3(10, 10, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Near(new GeoPoint3(110, 10, 0), Entity<DxfLine>(document, "A1").EndPoint);
            Near(new GeoPoint3(210, 10, 0), Entity<DxfCircle>(document, "A3").Center);
            Assert.Equal(10.0, Entity<DxfCircle>(document, "A3").Radius, 9);

            Assert.True(document.Undo());
            Near(new GeoPoint3(0, 0, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Near(new GeoPoint3(200, 0, 0), Entity<DxfCircle>(document, "A3").Center);
        }

        [Fact]
        public void BasePointGivesAnotherBase_AndTheGripMovesByTheWayFromIt()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });
            CadCommandSession session = GripEdit(document, Grip("A1", 1));

            Run(session, "B");
            Assert.Equal("Specify base point:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Equal("Specify stretch point or [Base point/Copy/Undo/eXit]:", session.PromptText);
            Near(P(50, 0), session.Prompt.BasePoint.Value);
            Run(session, P(60, 5));

            Near(new GeoPoint3(110, 5, 0), Entity<DxfLine>(document, "A1").EndPoint);
        }

        [Fact]
        public void TheGripEditIsNotTheCommandEnterRunsAgain_AndNoneStartsWhileACommandRuns()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });
            CadCommandSession session = Session(document);
            Run(session, "ZOOM", Esc);

            Assert.True(session.StartGripEdit(new[] { Grip("A1", 0) }));
            Run(session, Esc);
            Assert.Equal("ZOOM", session.LastCommandName);

            Run(session, "LINE");
            Assert.False(session.StartGripEdit(new[] { Grip("A1", 0) }));
            Assert.Equal("LINE", session.Current.Name);
            Run(session, Esc);
            Assert.False(session.StartGripEdit(new[] { Grip("A1", 9), Grip("NONE", 0) }));
        }

        [Fact]
        public void ScaleByReference_ScalesSoThatTheReferenceLengthBecomesTheNewOne()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A3" });

            Run(GripEdit(document, Grip("A3", 0)), "SC", "R", "10", "25");

            Assert.Equal(25.0, Entity<DxfCircle>(document, "A3").Radius, 9);
            Near(new GeoPoint3(200, 0, 0), Entity<DxfCircle>(document, "A3").Center);
        }
    }
}
