using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The objects selected before a routine runs (the pick-first, or implied, selection), taken as AutoCAD 2021's accoreconsole
    /// takes them (probes of the pickfirstc1 task): <c>(ssget)</c> with no mode, <c>":S"</c>, <c>":L"</c> or a filter gives
    /// them without asking, says "N found" (not for <c>":S"</c>), makes them the Previous selection and clears the selection,
    /// while PICKFIRST is not 0; a command a routine starts with <c>command</c>, <c>vl-cmdf</c> or <c>command-s</c> and gives
    /// its answers in the same call asks "Select objects:" and takes the answer, never the implied selection. The drawing is three
    /// lines at y 0, 5 and 9 (A, B, C) on layer 0 and a layer ColLayout, and the routine is a drafter's own: <c>(defun c:C1 ()
    /// (ssget) (command "change" "p" "" "p" "la" "ColLayout" ""))</c>.
    /// </summary>
    public sealed class LispImpliedSelectionTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private const string C1 =
            "(defun c:C1 () (ssget) (command \"change\" \"p\" \"\" \"p\" \"la\" \"ColLayout\" \"\"))";

        private static CadCommandSession ThreeLines()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "ColLayout", ColorIndex = 2 });
            document.Database.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "Locked",
                    ColorIndex = 3,
                    IsLocked = true,
                }
            );
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "LINE", "0,0", "10,0", Enter);
            Run(session, "LINE", "0,5", "10,5", Enter);
            Run(session, "LINE", "0,9", "10,9", Enter);
            session.Feed("(setq a (ssname (ssget \"_X\") 2) b (ssname (ssget \"_X\") 1) c (ssname (ssget \"_X\") 0))");
            session.Feed(C1);
            return session;
        }

        private static DxfEntity[] Lines(CadCommandSession session) =>
            session.Document.Database.ModelSpace.OfType<DxfEntity>().OrderBy(l => ((DxfLine)l).StartPoint.Y).ToArray();

        private static string[] Layers(CadCommandSession session) => Lines(session).Select(l => l.Layer).ToArray();

        // Selects the lines at the indexes before a command, as a click or a window does with no command running.
        private static void Grip(CadCommandSession session, params int[] lines) =>
            session.Document.Selection.Set(lines.Select(i => Lines(session)[i].Handle).ToList());

        private static string[] After(CadCommandSession session, int before) =>
            session.History.Skip(before).Select(l => l.TrimEnd()).ToArray();

        #region the routine of the report

        [Fact]
        public void C1WithAnObjectGrippedFirst_ChangesItsLayer_WithNoQuestion()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            int before = session.History.Count;

            Run(session, "C1");

            Assert.False(session.IsRunning);
            Assert.False(session.IsLispWaiting);
            Assert.Equal(new[] { "0", "ColLayout", "0" }, Layers(session));

            // AutoCAD 2021: ssget takes the gripped line ("1 found"), and CHANGE asks "Select objects:" and takes the P
            string[] lines = After(session, before);
            Assert.Equal(
                new[] { "Command: C1", "1 found", "change", "Select objects: p", "1 found" },
                lines.Take(5).ToArray()
            );
            Assert.Equal(0, session.Document.Selection.Count);
        }

        [Fact]
        public void C1WithTwoObjectsGrippedFirst_ChangesBoth()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 0, 2);

            Run(session, "C1");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "ColLayout", "0", "ColLayout" }, Layers(session));
        }

        [Fact]
        public void C1WithNothingGripped_AsksSelectObjects_AndChangesWhatIsSelected()
        {
            CadCommandSession session = ThreeLines();

            Run(session, "C1");

            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText.TrimEnd());

            Run(session, "ALL", Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "ColLayout", "ColLayout", "ColLayout" }, Layers(session));
        }

        [Fact]
        public void C1WithPickfirstZero_AsksSelectObjects_AndLeavesTheGrippedObjectAlone()
        {
            CadCommandSession session = ThreeLines();
            session.Document.PickFirst = 0;
            Grip(session, 1);

            Run(session, "C1");

            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText.TrimEnd());
            Assert.Equal(new[] { "0", "0", "0" }, Layers(session));
        }

        #endregion

        #region ssget

        [Fact]
        public void SsgetWithObjectsGrippedFirst_GivesThemWithNoQuestion_AndSaysHowManyWereFound()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            int before = session.History.Count;

            // AutoCAD 2021: (setq s (ssget)) writes "1 found" and gives the set at once; it holds the gripped line
            Run(session, "(setq s (ssget))");

            Assert.False(session.IsRunning);
            Assert.Equal("1", Value(session, "(sslength s)"));
            Assert.Equal("T", Value(session, "(equal (ssname s 0) b)"));
            Assert.Equal("1 found", After(session, before)[1]);
        }

        [Fact]
        public void SsgetWithObjectsGrippedFirst_MakesThemThePreviousSelection_AndClearsTheSelection()
        {
            CadCommandSession session = ThreeLines();
            Value(session, "(ssget \"_X\")");
            Grip(session, 1);

            Run(session, "(setq s (ssget))");

            Assert.Equal(0, session.Document.Selection.Count);
            Assert.Equal("nil", Value(session, "(ssget \"_I\")"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"_P\"))"));
            Assert.Equal("T", Value(session, "(equal (ssname (ssget \"_P\") 0) b)"));
        }

        [Fact]
        public void SsgetAskedTwiceWithObjectsGrippedFirst_TakesThemOnce_AndAsksTheSecondTime()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            Run(session, "(setq s1 (ssget))");

            Run(session, "(setq s2 (ssget))");

            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText.TrimEnd());
        }

        [Fact]
        public void SsgetWithPickfirstZero_AsksEvenWithObjectsGripped_AndKeepsThem()
        {
            CadCommandSession session = ThreeLines();
            session.Document.PickFirst = 0;
            Grip(session, 1);

            Run(session, "(setq s (ssget))");

            Assert.True(session.IsRunning);
            Assert.Equal(1, session.Document.Selection.Count);
        }

        [Fact]
        public void SsgetWithAFilter_TakesTheGrippedObjectsThatPassIt_AndCountsOnlyThose()
        {
            CadCommandSession session = ThreeLines();
            Value(session, "(command \"_.CHPROP\" a \"\" \"LA\" \"ColLayout\" \"\")");
            Grip(session, 0, 1);
            int before = session.History.Count;

            // AutoCAD 2021: two gripped, one on ColLayout, (ssget '((8 . "ColLayout"))) writes "1 found" and gives that one
            Run(session, "(setq s (ssget '((8 . \"ColLayout\"))))");

            Assert.False(session.IsRunning);
            Assert.Equal("1 found", After(session, before)[1]);
            Assert.Equal("1", Value(session, "(sslength s)"));
            Assert.Equal("T", Value(session, "(equal (ssname s 0) a)"));
            Assert.Equal(0, session.Document.Selection.Count);
        }

        [Fact]
        public void SsgetWithAFilterNothingGrippedPasses_SaysZeroFound_ThenAsks_AndTheGripsAreGone()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            int before = session.History.Count;

            // AutoCAD 2021: (ssget '((8 . "NOPE"))) writes "0 found" and then asks "Select objects:"; Enter gives nil and no
            // object is selected before any more
            Run(session, "(setq s (ssget '((8 . \"NOPE\"))))");

            Assert.True(session.IsRunning);
            Assert.Equal("0 found", After(session, before)[1]);
            Assert.Equal("Select objects:", session.PromptText.TrimEnd());

            Run(session, Enter);

            Assert.Equal("nil", Value(session, "!s"));
            Assert.Equal("nil", Value(session, "(ssget \"_I\")"));
        }

        [Fact]
        public void SsgetOnePickMode_TakesTheGrippedObjectsAndSaysNothingFound()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            int before = session.History.Count;

            // AutoCAD 2021: (ssget "_:S") with an object gripped gives the set and writes no "found" line
            Run(session, "(setq s (ssget \"_:S\"))");

            Assert.False(session.IsRunning);
            Assert.DoesNotContain("1 found", After(session, before));
            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        [Fact]
        public void SsgetWithLockedMode_LeavesOutAGrippedObjectOnALockedLayer_AndSaysSo()
        {
            CadCommandSession session = ThreeLines();
            Value(session, "(command \"_.CHPROP\" b \"\" \"LA\" \"Locked\" \"\")");
            Grip(session, 1);
            int before = session.History.Count;

            // AutoCAD 2021: the one gripped line on a locked layer: "1 found", "1 was on a locked layer.", then "Select objects:"
            Run(session, "(setq s (ssget \"_:L\"))");

            string[] lines = After(session, before);
            Assert.Equal("1 found", lines[1]);
            Assert.Equal("1 was on a locked layer.", lines[2]);
            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText.TrimEnd());
        }

        [Fact]
        public void SsgetOfIImplied_GivesTheGrippedObjects_AndLeavesThemGripped()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);

            // AutoCAD 2021: (ssget "_I") twice gives the set twice, and no "found" line
            Assert.Equal("1", Value(session, "(sslength (ssget \"_I\"))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"_I\"))"));
            Assert.Equal(1, session.Document.Selection.Count);
        }

        [Fact]
        public void SsgetOfXWithObjectsGripped_LeavesThemForTheNextSsget()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            Value(session, "(setq x (ssget \"_X\"))");

            Run(session, "(setq s (ssget))");

            Assert.False(session.IsRunning);
            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        #endregion

        #region command, vl-cmdf and command-s

        [Theory]
        [InlineData("(command \"_.ERASE\" \"p\" \"\")")]
        [InlineData("(vl-cmdf \"_.ERASE\" \"p\" \"\")")]
        [InlineData("(command-s \"_.ERASE\" \"p\" \"\")")]
        public void ACommandGivenItsAnswersInTheSameCall_TakesThemAtSelectObjects_NotTheGrippedObjects(string form)
        {
            CadCommandSession session = ThreeLines();
            Value(session, "(ssget \"_X\")");
            Grip(session, 1);
            int before = session.History.Count;

            // AutoCAD 2021 (D1 to D5): the P goes to "Select objects:" and gives the Previous selection, three objects, not the
            // one gripped; the command shows "Select objects: p 3 found"
            Run(session, form);

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(Lines(session), l => !l.IsErased);
            Assert.Contains("3 found", After(session, before));
        }

        [Fact]
        public void MoveGivenItsAnswersInTheSameCall_MovesThePreviousSelection_NotTheGrippedObjects()
        {
            CadCommandSession session = ThreeLines();
            Value(session, "(ssget \"_X\")");
            Grip(session, 1);

            Run(session, "(command \"_.MOVE\" \"p\" \"\" \"0,0\" \"0,1\")");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { 1.0, 6.0, 10.0 }, Lines(session).Select(l => ((DxfLine)l).StartPoint.Y).ToArray());
        }

        [Fact]
        public void ACommandStartedAloneWithCommand_TakesTheGrippedObjectsAsItReturns()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);
            int before = session.History.Count;

            // AutoCAD 2021 (E4): (command "_.MOVE") writes "_.MOVE nil" and "1 found", and waits at the base point
            Run(session, "(command \"_.MOVE\")");

            Assert.True(session.IsRunning);
            Assert.Contains("1 found", After(session, before));

            Run(session, "(command \"0,0\" \"0,2\")");

            Assert.Equal(new[] { 0.0, 7.0, 9.0 }, Lines(session).Select(l => ((DxfLine)l).StartPoint.Y).ToArray());
        }

        [Fact]
        public void SsgetThenEraseOfP_ErasesTheGrippedObjectsOnly()
        {
            CadCommandSession session = ThreeLines();
            Grip(session, 1);

            Run(session, "(progn (setq s (ssget)) (command \"_.ERASE\" \"p\" \"\"))");

            Assert.False(session.IsRunning);
            Assert.Equal(
                new[] { 0.0, 9.0 },
                Lines(session).Where(l => !l.IsErased).Select(l => ((DxfLine)l).StartPoint.Y).ToArray()
            );
        }

        #endregion
    }
}
