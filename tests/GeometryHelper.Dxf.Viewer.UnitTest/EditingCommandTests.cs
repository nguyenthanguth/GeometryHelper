using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingCommandTests
    {
        [Fact]
        public void MoveWithALineSelectedFirst_AsksForTheBasePointInAutoCadsWords_AndMovesIt()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });
            CadCommandSession session = Session(document);

            session.Start("M");
            Assert.Equal("Specify base point or [Displacement] <Displacement>:", session.PromptText);
            session.Feed(P(0, 0));
            Assert.Equal("Specify second point or <use first point as displacement>:", session.PromptText);
            Assert.Equal(P(0, 0), session.Prompt.BasePoint.Value);
            session.Feed(P(30, 40));

            Assert.False(session.IsRunning);
            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Near(new GeoPoint3(30, 40, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Assert.Equal(0, document.Selection.Count);
            Assert.Equal(new[] { "Command: M", "MOVE", "Specify base point or [Displacement] <Displacement>:", "Specify second point or <use first point as displacement>:" }, session.History);
            Assert.Equal("MOVE", document.NextUndo.Name);
        }

        [Fact]
        public void MoveWithNothingSelected_AsksToSelectObjects_ThenTakesThePicksUntilEnter()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MOVE");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, P(50, 0.4));
            Assert.Equal("1 found", session.History.Last());
            session.FeedSelection(new[] { "A1", "A2" });
            Assert.Equal("2 found (1 duplicate), 2 total", session.History.Last());
            Assert.Equal(new[] { "A1", "A2" }, session.Gathering.ToArray());
            Run(session, Enter);
            Assert.Equal("Specify base point or [Displacement] <Displacement>:", session.PromptText);
            Run(session, P(0, 0), P(0, 10));

            Near(new GeoPoint3(0, 10, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Near(new GeoPoint3(0, 60, 0), Entity<DxfLine>(document, "A2").EndPoint);
            Assert.Single(document.NextUndo.Records, r => r.Handle == "A1");
        }

        [Fact]
        public void EscBeforeTheSecondPoint_ChangesNothing()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            document.Selection.Set(new[] { "A1" });
            CadCommandSession session = Session(document);

            Run(session, "MOVE", P(0, 0), Esc);

            Assert.Equal(CadCommandOutcome.Cancelled, session.LastOutcome);
            Assert.Equal("Specify second point or <use first point as displacement>: *Cancel*", session.History.Last());
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheDisplacementKeywordIsTypedByItsCapitalLetter_AndEnterTakesTheDefault()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });
            CadCommandSession session = Session(document);

            Run(session, "MOVE", "d");
            Assert.Equal("Specify displacement <0.0000, 0.0000, 0.0000>:", session.PromptText);
            Run(session, "10,5");
            Near(new GeoPoint3(10, 5, 0), Entity<DxfLine>(document, "A1").StartPoint);

            document.Selection.Set(new[] { "A1" });
            Run(session, "MOVE", Enter);
            Assert.Equal("Specify displacement <10.0000, 5.0000, 0.0000>:", session.PromptText);
            Run(session, Enter);
            Near(new GeoPoint3(20, 10, 0), Entity<DxfLine>(document, "A1").StartPoint);
        }

        [Fact]
        public void AKeywordIsFoundByItsCapitalLettersByItsWholeWordOrByALongerStart()
        {
            CadPrompt prompt = CadPrompt.ForPoint("Specify endpoint of arc", null, new[] { "CLose", "Line", "Second pt", "mOde" });

            Assert.Equal("CLose", prompt.MatchKeyword("cl"));
            Assert.Equal("CLose", prompt.MatchKeyword("close"));
            Assert.Equal("Line", prompt.MatchKeyword("L"));
            Assert.Equal("Line", prompt.MatchKeyword("lin"));
            Assert.Equal("Second pt", prompt.MatchKeyword("S"));
            Assert.Equal("mOde", prompt.MatchKeyword("o"));
            Assert.Null(prompt.MatchKeyword("c"));
            Assert.Null(prompt.MatchKeyword("x"));
            Assert.Equal("Specify endpoint of arc or [CLose/Line/Second pt/mOde]:", prompt.Text);
            Assert.Equal("Erase source objects? [Yes/No] <No>:", CadPrompt.ForKeyword("Erase source objects?", new[] { "Yes", "No" }, "No").Text);
        }

        [Fact]
        public void AnAliasStartsItsCommand_AndANameNoCommandHasIsSaidToBeUnknown()
        {
            CadCommandSession session = Session(Plan());

            Assert.False(session.Start("FOO"));
            Assert.Equal("Unknown command \"FOO\".  Press F1 for help.", session.History.Last());
            Assert.True(session.Start("l"));
            Assert.Equal("LINE", session.Current.Name);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void TheShortcutsAreDataThatGlobalSettingsReplace()
        {
            CadCommandSession session = Session(Plan());
            session.Registry.Shortcuts = new CadShortcuts(new Dictionary<string, string> { ["MM"] = "move" }, new Dictionary<string, string> { ["F5"] = "line" });

            Assert.Equal("MOVE", session.Registry.Find("mm").Name);
            Assert.Null(session.Registry.Find("M"));
            Assert.Equal("LINE", session.Registry.Shortcuts.Keys["f5"]);
            Assert.Equal("ZOOM", CadShortcuts.Default.Aliases["Z"]);
            Assert.Equal("OSNAP", CadShortcuts.Default.Keys["F3"]);
        }

        [Fact]
        public void EnterWithNoCommandRunning_RunsTheLastOneAgain()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(0, 0), P(10, 0), Enter, Enter);

            Assert.Equal("LINE", session.Current.Name);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void TypedPointsAreAbsolute_RelativeToTheLastPoint_OrPolar()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "1000,1000", "@10,0", "@10<90", Enter);

            List<DxfLine> lines = All<DxfLine>(document.ModelSpace).Skip(3).ToList();
            Assert.Equal(2, lines.Count);
            Near(new GeoPoint3(1010, 1000, 0), lines[0].EndPoint);
            Near(new GeoPoint3(1010, 1010, 0), lines[1].EndPoint);
        }

        [Fact]
        public void ANumberAtAPointPrompt_IsADistanceAlongTheRubberBandTowardsTheCursor()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(1000, 1000));
            session.Cursor = P(1000, 1500);
            Run(session, "25", Enter);

            Near(new GeoPoint3(1000, 1025, 0), All<DxfLine>(document.ModelSpace).Last().EndPoint);
        }

        [Fact]
        public void AnAnswerThePromptCannotTake_IsSaidAndThePromptStays()
        {
            CadCommandSession session = Session(Plan());

            Run(session, "LINE", "abc");

            Assert.Equal("Invalid point.", session.History.Last());
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void LineDrawsSegmentsAsOneUndoStep_UndoTakesTheLastOneBack_AndCloseClosesThem()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(1000, 0), P(1100, 0));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(1100, 100), P(1200, 100), "U");
            Assert.Equal("Specify next point or [Close/Undo]:", session.PromptText);
            Run(session, "C");

            List<DxfLine> made = All<DxfLine>(document.ModelSpace).Skip(3).ToList();
            Assert.Equal(3, made.Count);
            Near(new GeoPoint3(1100, 100, 0), made[2].StartPoint);
            Near(new GeoPoint3(1000, 0, 0), made[2].EndPoint);
            Assert.Equal("LINE", document.NextUndo.Name);
            AssertReopens(document);

            document.Undo();
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void EscDuringLine_KeepsTheSegmentsDrawn_AsAutoCadKeepsThem()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(1000, 0), P(1100, 0), P(1100, 100), Esc);

            Assert.Equal(5, All<DxfLine>(document.ModelSpace).Count);
            Assert.Equal(2, document.NextUndo.Records.Count);
        }

        [Fact]
        public void CopyPlacesACopyAtEachPointUntilExit_AndUndoTakesTheLastCopyBack()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A3" });
            CadCommandSession session = Session(document);

            Run(session, "CO");
            Assert.Equal("Current settings:  Copy mode = Multiple", session.History.Last());
            Run(session, P(200, 0), P(200, 50));
            Assert.Equal("Specify second point or [Exit/Undo] <Exit>:", session.PromptText);
            Run(session, P(200, 100), P(200, 150), "U", Enter);

            List<DxfCircle> circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(new[] { 0.0, 50.0, 100.0 }, circles.Select(c => c.Center.Y));
            Assert.Equal("COPY", document.NextUndo.Name);
            AssertReopens(document);
        }

        [Fact]
        public void EraseRotateScaleAndMirror_EachChangeTheDrawingInOneStepThatUndoes()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "E", new[] { "A2" }, Enter);
            Assert.Null(document.FindEntity("A2"));

            Run(session, "RO", new[] { "A1" }, Enter, P(0, 0));
            Assert.Equal("Specify rotation angle or [Copy/Reference] <0>:", session.PromptText);
            Run(session, "90");
            Near(new GeoPoint3(0, 100, 0), Entity<DxfLine>(document, "A1").EndPoint);

            Run(session, "SC", new[] { "A3" }, Enter, P(200, 0));
            Assert.Equal("Specify scale factor or [Copy/Reference] <1.0000>:", session.PromptText);
            Run(session, "2.5");
            Assert.Equal(25, Entity<DxfCircle>(document, "A3").Radius, 9);

            Run(session, "MI", new[] { "A4" }, Enter, P(400, 0), P(400, 10));
            Assert.Equal("Erase source objects? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            DxfArc mirrored = Entity<DxfArc>(document, "A4");
            Near(new GeoPoint3(500, 0, 0), mirrored.Center);
            GeoPoint3[] ends = { mirrored.StartPoint, mirrored.EndPoint };
            Assert.Contains(ends, p => p.DistanceTo(new GeoPoint3(480, 0, 0)) < 1E-9);
            Assert.Contains(ends, p => p.DistanceTo(new GeoPoint3(400 + 100, 20, 0)) < 1E-9);
            AssertReopens(document);

            int steps = 0;

            while (document.Undo())
            {
                steps++;
            }

            Assert.Equal(4, steps);
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void RotatingACopy_LeavesTheSourceWhereItWas()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "ROTATE", new[] { "A1" }, Enter, P(0, 0), "C");
            Assert.Equal("Rotating a copy of the selected objects.", session.History.Last());
            Run(session, "45");

            Near(new GeoPoint3(100, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);
            double half = 100.0 / Math.Sqrt(2.0);
            Near(new GeoPoint3(half, half, 0), All<DxfLine>(document.ModelSpace).Last().EndPoint, 1E-9);
        }

        [Fact]
        public void ExplodeBreaksAnInsertIntoTheLinesOfItsBlock_AndSaysWhatItCouldNotExplode()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "X", new[] { "A7", "A1" }, Enter);

            Assert.Equal("1 was not able to be exploded.", session.History.Last());
            Assert.Null(document.FindEntity("A7"));
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(7, lines.Count);
            Near(new GeoPoint3(510, 10, 0), lines[5].StartPoint);
            AssertReopens(document);
        }

        [Fact]
        public void MatchPropTakesTheLayerAndLookOfTheSource()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MA", P(50, 0));
            Assert.Equal("Select destination object(s) or [Settings]:", session.PromptText);
            Run(session, new[] { "A2", "A3" }, Enter);

            Assert.Equal("Steel", Entity<DxfLine>(document, "A2").Layer);
            Assert.Equal("Steel", Entity<DxfCircle>(document, "A3").Layer);
            Assert.Equal("MATCHPROP", document.NextUndo.Name);
        }

        [Fact]
        public void ObjectsOnALockedLayerAreLeftOut_AndSaidSo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "E", new[] { "A8", "A2" }, Enter);

            Assert.Contains("1 was on a locked layer.", session.History);
            Assert.NotNull(document.FindEntity("A8"));
            Assert.Null(document.FindEntity("A2"));
        }

        [Fact]
        public void LayerOnTheCommandLine_MakesLayersSetsTheCurrentOneAndSwitchesThemOffAsOneStep()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "LA");
            Assert.Equal("Enter an option [Set/New/ON/OFF/Freeze/Thaw/LOck/Unlock]:", session.PromptText);
            Run(session, "N", "Walls, Doors", "S", "Walls", "OFF", "Steel", "F", "Walls", "LO", "Doors", Enter);

            Assert.Equal("Walls", document.Database.Clayer);
            Assert.True(document.Database.LayerTable["Steel"].IsOff);
            Assert.True(document.Database.LayerTable["Doors"].IsLocked);
            Assert.False(document.Database.LayerTable["Walls"].IsFrozen);
            Assert.Contains("Cannot freeze layer \"Walls\".  It is the CURRENT layer.", session.History);
            Assert.Empty(document.GetPickIndex(document.ModelSpace).Pick(50, 0, 0.5));
            Assert.Equal("LAYER", document.NextUndo.Name);

            document.Undo();
            Assert.False(document.Database.LayerTable.Has("Walls"));
            Assert.Equal(new[] { "A1" }, document.GetPickIndex(document.ModelSpace).Pick(50, 0, 0.5));
        }

        [Fact]
        public void LayerHandledByTheWindow_AsksNothingOnTheCommandLine()
        {
            CadCommandSession session = Session(Plan());
            session.UiRequested += (sender, e) => e.Handled = e.Name == "LAYER";

            Run(session, "LAYER");

            Assert.False(session.IsRunning);
        }

        [Fact]
        public void UndoTakesBackANumberOfCommands_UOneAndRedoBringsOneBack()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            Run(session, "E", new[] { "A1" }, Enter, "E", new[] { "A2" }, Enter, "E", new[] { "A3" }, Enter);

            Run(session, "UNDO");
            Assert.Equal("Enter the number of operations to undo <1>:", session.PromptText);
            Run(session, "2");
            Assert.NotNull(document.FindEntity("A2"));
            Assert.Null(document.FindEntity("A1"));

            Run(session, "REDO");
            Assert.Null(document.FindEntity("A2"));
            Run(session, "U", "U", "U");
            Assert.Equal("Everything has been undone", session.History.Last());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ZoomExtentsAndWindow_ChangeTheView_AndPreviousGoesBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            var view = new CadView(1000, 500);
            session.View = view;

            Run(session, "Z", "E");
            Assert.Equal(255, view.CenterX, 6);
            DisplayBox extents = view.VisibleBox;

            Run(session, "Z", P(0, 0), P(100, 50));
            Assert.Equal(50, view.CenterX, 6);
            Assert.Equal(10, view.PixelsPerUnit, 6);

            Run(session, "Z", "P");
            Assert.Equal(extents.MinX, view.VisibleBox.MinX, 6);
            Assert.Equal(extents.MaxX, view.VisibleBox.MaxX, 6);
        }

        [Fact]
        public void ACommandThatThrows_HasItsEditsTakenBackAndSaysWhy()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            var registry = CadCommandRegistry.CreateDefault();
            registry.Add(new CadCommand("BREAKS", Breaks));
            var session = new CadCommandSession(document, registry);

            Run(session, "BREAKS", P(0, 0));

            Assert.Equal(CadCommandOutcome.Failed, session.LastOutcome);
            Assert.Equal("*Error* the second half failed", session.History.Last());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ZoomExtentsInALayout_TakesInItsSheet_AsTheMiddleDoubleClickDoes()
        {
            CadDocument document = Blank();
            CadSpace layout = document.Spaces[1];
            document.CurrentSpace = layout;
            CadCommandSession session = Session(document);
            var view = new CadView(1000, 500);
            session.View = view;

            Run(session, "Z", "A");

            DisplayBox paper = layout.PaperBounds;
            Assert.False(paper.IsEmpty);
            Assert.Equal((paper.MinX + paper.MaxX) / 2.0, view.CenterX, 6);
            Assert.True(view.VisibleBox.Contains(paper));
        }

        [Fact]
        public void EnterAtZoomsPrompt_LeavesTheViewAsItWas_AndSaysSo()
        {
            CadCommandSession session = Session(Plan());
            var view = new CadView(1000, 500);
            session.View = view;

            Run(session, "Z", Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("Real-time zoom is the mouse wheel's: the view is as it was.", session.History.Last());
            Assert.Equal(1, view.PixelsPerUnit, 12);
            Assert.Equal(0, view.CenterX, 12);
        }

        [Fact]
        public void ZoomScaleFactors_nXScaleTheViewAsItIs_AndNAloneThatOfTheExtents()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            var view = new CadView(1000, 500);
            session.View = view;
            Run(session, "Z", "E");
            double extents = view.PixelsPerUnit;

            Run(session, "Z", "2x");
            Assert.Equal(2 * extents, view.PixelsPerUnit, 9);
            Run(session, "Z", "2X");
            Assert.Equal(4 * extents, view.PixelsPerUnit, 9);
            Run(session, "Z", "0.5");
            Assert.Equal(0.5 * extents, view.PixelsPerUnit, 9);
            Assert.Equal(255, view.CenterX, 6);
            Run(session, "Z", "2xp");
            Assert.StartsWith("nXP scales the view of a viewport", session.History.Last(), StringComparison.Ordinal);
        }

        [Fact]
        public void RotateByReference_TurnsFromTheReferenceAngleToTheNewOne_TypedOrByPoints()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "RO", new[] { "A1" }, Enter, P(0, 0), "R");
            Assert.Equal("Specify the reference angle <0>:", session.PromptText);
            Run(session, "30");
            Assert.Equal("Specify the new angle or [Points] <0>:", session.PromptText);
            Run(session, "90");
            Near(new GeoPoint3(50, 100 * Math.Sqrt(3.0) / 2.0, 0), Entity<DxfLine>(document, "A1").EndPoint, 1E-9);

            Run(session, "RO", new[] { "A1" }, Enter, P(0, 0), "R", P(0, 0), P(50, 100 * Math.Sqrt(3.0) / 2.0), "P", P(0, 0), P(0, -10));
            Near(new GeoPoint3(0, -100, 0), Entity<DxfLine>(document, "A1").EndPoint, 1E-9);
        }

        [Fact]
        public void ScaleByReference_MakesTheReferenceLengthTheNewOne()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "SC", new[] { "A1" }, Enter, P(0, 0), "R");
            Assert.Equal("Specify reference length <1.0000>:", session.PromptText);
            Run(session, "100", "250");
            Near(new GeoPoint3(250, 0, 0), Entity<DxfLine>(document, "A1").EndPoint, 1E-9);

            Run(session, "SC", new[] { "A1" }, Enter, P(0, 0), "R", P(0, 0), P(250, 0), "P", P(0, 0), P(0, 50));
            Near(new GeoPoint3(50, 0, 0), Entity<DxfLine>(document, "A1").EndPoint, 1E-9);
        }

        private static IEnumerable<CadPrompt> Breaks(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify a point");
            c.Apply(null, s => s.Append(c.Space, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 5, 0))));
            throw new InvalidOperationException("the second half failed");
        }
    }
}
