using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLINE, MLEDIT and MLSTYLE of the presenter, in the words of AutoCAD 2021 (read from its accoreconsole: the prompts, the
    /// messages and the records MLEDIT leaves), and the variables CMLJUST, CMLSCALE and CMLSTYLE.
    /// </summary>
    public class MlineCommandTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static GeoPoint3 P3(double x, double y) => new GeoPoint3(x, y, 0);

        private static List<DxfUnknownEntity> Mlines(CadDocument document) =>
            All<DxfUnknownEntity>(document.ModelSpace).Where(m => m.DxfName == "MLINE").ToList();

        /// <summary>A drawing of two multilines of the style STANDARD at scale 1 (offsets 0 and -1, as AutoCAD writes them at the top), given as lists of points.</summary>
        private static CadDocument Drawing(params GeoPoint2[][] lines)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "CMLSCALE", "1");

            foreach (GeoPoint2[] points in lines)
            {
                Run(session, "MLINE");
                Run(session, points.Cast<object>().ToArray());
                Run(session, Enter);
            }

            return document;
        }

        /// <summary>The builder of the multiline at an index of the drawing's multilines.</summary>
        private static DxfMlineBuilder Read(CadDocument document, int index) =>
            new DxfMlineBuilder(Mlines(document)[index]);

        [Fact]
        public void Mline_AsksItsSettingsThenTheStartPoint_AsAutoCADDoes()
        {
            CadCommandSession session = Start(Blank(), "MLINE");

            Assert.Contains("Current settings: Justification = Top, Scale = 20.00, Style = STANDARD", session.History);
            Assert.Equal("Specify start point or [Justification/Scale/STyle]:", session.PromptText);
        }

        [Fact]
        public void Mline_AsksNextPointWithNoOptionAtFirst_UndoFromTheSecondPoint_AndCloseFromTheThird()
        {
            CadCommandSession session = Start(Blank(), "ML", P(0, 0));
            Assert.Equal("Specify next point:", session.PromptText);

            Run(session, P(100, 0));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, P(100, 100));
            Assert.Equal("Specify next point or [Close/Undo]:", session.PromptText);
        }

        [Fact]
        public void Mline_ThroughThreePoints_MakesOneMlineOfStandardAtTheCurrentScaleAndJustification()
        {
            CadDocument document = Blank();
            Start(document, "MLINE", P(0, 0), P(100, 0), P(100, 100), Enter);

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            var builder = new DxfMlineBuilder(mline);
            Assert.Equal(new[] { P3(0, 0), P3(100, 0), P3(100, 100) }, builder.Points);
            Assert.Equal(20.0, builder.Scale);
            Assert.Equal(DxfMlineJustification.Top, builder.Justification);
            Assert.False(builder.IsClosed);
            Assert.Equal("STANDARD", builder.Style.Name);

            // 20 wide at the top: the second element 20 to the right of the first, which stands on the points.
            DxfLine[] lines = mline.Explode().Cast<DxfLine>().ToArray();
            Assert.Equal(4, lines.Length);
            Near(P3(0, 0), lines[0].StartPoint);
            Near(P3(0, -20), lines[1].StartPoint);
            Near(P3(120, -20), lines[1].EndPoint);
        }

        [Fact]
        public void Mline_Close_MakesAClosedMlineOfTheThreePoints()
        {
            CadDocument document = Blank();
            Start(document, "MLINE", P(0, 0), P(100, 0), P(100, 100), "C");

            var builder = new DxfMlineBuilder(Assert.Single(Mlines(document)));
            Assert.True(builder.IsClosed);
            Assert.Equal(3, builder.Points.Count);
        }

        [Fact]
        public void Mline_UndoTakesBackTheLastPoint_AndEnterWithOnePointMakesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLINE", P(0, 0), P(100, 0), P(100, 100));
            Run(session, "U");
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, Enter);

            Assert.Equal(2, new DxfMlineBuilder(Assert.Single(Mlines(document))).Points.Count);

            CadDocument none = Blank();
            Start(none, "MLINE", P(0, 0), Enter);
            Assert.Empty(Mlines(none));
        }

        [Fact]
        public void Mline_IsOneUndoStep_AndTheDrawingReopens()
        {
            CadDocument document = Blank();
            Start(document, "MLINE", P(0, 0), P(100, 0), P(100, 100), Enter);
            Assert.Single(Mlines(document));
            AssertReopens(document);

            document.Undo();

            Assert.Empty(Mlines(document));
        }

        [Fact]
        public void MlineJustification_AsksTopZeroBottom_AndComesBackToTheStart_TheNewSettingInTheLine()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLINE", "J");
            Assert.Equal("Enter justification type [Top/Zero/Bottom] <top>:", session.PromptText);

            Run(session, "Z");

            Assert.Equal("Specify start point or [Justification/Scale/STyle]:", session.PromptText);
            Assert.Equal(
                "Current settings: Justification = Zero, Scale = 20.00, Style = STANDARD",
                session.History.Last(h => h.StartsWith("Current"))
            );
            Assert.Equal(1, document.Database.Cmljust);

            Run(session, "J");
            Assert.Equal("Enter justification type [Top/Zero/Bottom] <zero>:", session.PromptText);
            Run(session, "B", P(0, 0), P(100, 0), Enter);

            var builder = new DxfMlineBuilder(Assert.Single(Mlines(document)));
            Assert.Equal(DxfMlineJustification.Bottom, builder.Justification);
            Assert.Equal(2, document.Database.Cmljust);
        }

        [Fact]
        public void MlineScale_AsksTheScale_AcceptsNoughtAndNegatives_AndShowsTwoPlaces()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLINE", "S");
            Assert.Equal("Enter mline scale <20.00>:", session.PromptText);

            Run(session, "2.5");
            Assert.Equal(
                "Current settings: Justification = Top, Scale = 2.50, Style = STANDARD",
                session.History.Last(h => h.StartsWith("Current"))
            );
            Assert.Equal(2.5, document.Database.Cmlscale);

            Run(session, "S", "-3");
            Assert.Equal(-3.0, document.Database.Cmlscale);
            Run(session, "S", "0");
            Assert.Equal(0.0, document.Database.Cmlscale);
            Assert.Equal(
                "Current settings: Justification = Top, Scale = 0.00, Style = STANDARD",
                session.History.Last(h => h.StartsWith("Current"))
            );
        }

        [Fact]
        public void MlineStyle_ListsTheLoadedStylesOnAQuestionMark_TakesANameAndIgnoresOneItHasNot()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLINE", "ST");
            Assert.Equal("Enter mline style name or [?]:", session.PromptText);

            Run(session, "?");

            Assert.Contains("Loaded mline styles:", session.History);
            Assert.Contains("---------------- ------------------", session.History);
            Assert.Contains("STANDARD", session.History);
            Assert.Equal("Enter mline style name or [?]:", session.PromptText);

            Run(session, "NOSUCH");
            Assert.Equal("Enter mline style name or [?]:", session.PromptText);

            document.Database.AddMlineStyle("WALL", new DxfMlineStyleSettings());
            Run(session, "wall");

            Assert.Equal("Specify start point or [Justification/Scale/STyle]:", session.PromptText);
            Assert.Equal(
                "Current settings: Justification = Top, Scale = 20.00, Style = WALL",
                session.History.Last(h => h.StartsWith("Current"))
            );
            Assert.Equal("WALL", ((DxfMlineStyle)document.Database.CmlstyleID).Name);
        }

        [Fact]
        public void TheVariablesCmljustCmlscaleAndCmlstyle_AreSetByTheirNames()
        {
            CadDocument document = Blank();
            Run(Session(document), "MLSTYLE", Enter);
            document.Database.AddMlineStyle("WALL", new DxfMlineStyleSettings());
            CadCommandSession session = Start(document, "CMLJUST");

            Assert.Equal("Enter new value for CMLJUST <0>:", session.PromptText);
            Run(session, "2", "CMLSCALE");
            Assert.Equal("Enter new value for CMLSCALE <20.0000>:", session.PromptText);
            Run(session, "4", "CMLSTYLE");
            Assert.Equal("Enter new value for CMLSTYLE <\"STANDARD\">:", session.PromptText);
            Run(session, "WALL");

            Assert.Equal(2, document.Database.Cmljust);
            Assert.Equal(4.0, document.Database.Cmlscale);
            Assert.Equal("WALL", ((DxfMlineStyle)document.Database.CmlstyleID).Name);
        }

        [Fact]
        public void MlinePreview_IsTheMlineThePointsAndTheCursorMake()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLINE", "S", "1", P(0, 0), P(100, 0));

            List<string> shapes = PreviewTests.PreviewMakes(session, P(100, 100), Enter);

            Assert.Equal(4, shapes.Count);
        }

        [Fact]
        public void AnMlineIsDrawnByItsElementsAndPickedByThem()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) });

            DisplayList2 display = document.GetDisplay(document.ModelSpace);

            Assert.Equal(2, display.Items.Count);
            Assert.All(display.Items, i => Assert.Equal(Mlines(document)[0].Handle, i.Handle));
            Assert.Equal(
                new[] { Mlines(document)[0].Handle },
                document.GetPickIndex(document.ModelSpace).Pick(50, -1, 0.5)
            );
        }

        [Fact]
        public void AnMline_CanBeMovedCopiedAndScaledByTheEditCommands()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0), P(100, 100) });
            string handle = Mlines(document)[0].Handle;

            CadCommandSession session = Start(document, "MOVE", new[] { handle }, Enter, P(0, 0), P(10, 5));
            Assert.Equal(new[] { P3(10, 5), P3(110, 5), P3(110, 105) }, Read(document, 0).Points);

            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(0, 200), Enter);
            Assert.Equal(2, Mlines(document).Count);
            Assert.Equal(P3(10, 205), Read(document, 1).Points[0]);

            Run(session, "SCALE", new[] { handle }, Enter, P(10, 5), "2");
            Assert.Equal(new[] { P3(10, 5), P3(210, 5), P3(210, 205) }, Read(document, 0).Points);
            Assert.Equal(2.0, Read(document, 0).Scale);
        }

        #region MLEDIT

        [Fact]
        public void Mledit_AsksTheOptionsAsAutoCADsConsoleDoes_AndThenTheFirstMline()
        {
            CadCommandSession session = Start(Drawing(), "MLEDIT");
            Assert.Equal("Enter mline editing option [CC/OC/MC/CT/OT/MT/CJ/AV/DV/CS/CA/WA]:", session.PromptText);

            Run(session, "cc");
            Assert.Equal("Select first mline:", session.PromptText);
        }

        [Fact]
        public void ClosedCross_BreaksEveryElementOfTheFirstWhereTheSecondCrosses_AndLeavesTheSecondWhole()
        {
            // AutoCAD 2021: (0, 0)-(100, 0) crossed by (50, -50)-(50, 50), both at scale 1 at the top: the first's two elements
            // broken from 50 to 51, which its first vertex holds as [offset, 0, 50, 51].
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -50), P(50, 50) });
            string second = Text(Mlines(document)[1]);

            CadCommandSession session = Start(document, "MLEDIT", "CC", P(20, 0));
            Assert.Equal("Select second mline:", session.PromptText);
            Run(session, P(50, -30));
            Assert.Equal("Select first mline or [Undo]:", session.PromptText);

            DxfMlineBuilder first = Read(document, 0);
            Assert.Equal(new[] { 0.0, 50.0, 51.0 }, Tail(first, 0, 0));
            Assert.Equal(new[] { 0.0, 50.0, 51.0 }, Tail(first, 0, 1));
            Assert.Equal(second, Text(Mlines(document)[1]));
        }

        [Fact]
        public void OpenCross_AlsoBreaksTheOuterElementsOfTheSecondAcrossTheFirst()
        {
            // AutoCAD 2021: the second's elements broken from 49 to 50 along it, the width of the first.
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -50), P(50, 50) });

            Start(document, "MLEDIT", "OC", P(20, 0), P(50, -30));

            DxfMlineBuilder second = Read(document, 1);
            Assert.Equal(new[] { 0.0, 49.0, 50.0 }, Tail(second, 0, 0));
            Assert.Equal(new[] { 0.0, 49.0, 50.0 }, Tail(second, 0, 1));
        }

        [Fact]
        public void TwoMlinesThatDoNotMeet_AreToldSo_AndThePromptComesBackWithUndo()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(0, 20), P(100, 20) });

            CadCommandSession session = Start(document, "MLEDIT", "CC", P(20, 0), P(20, 20));

            Assert.Equal("These multilines do not intersect.", session.History.Last());
            Assert.Equal("Select first mline or [Undo]:", session.PromptText);
        }

        [Fact]
        public void APickOnNothingButAMline_IsToldToPickMlinesOnly()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) });
            Run(Session(document), "LINE", P(0, 50), P(100, 50), Enter);

            CadCommandSession session = Start(document, "MLEDIT", "CC", P(20, 50));

            Assert.Equal("Please pick mlines only.", session.History.Last());
            Assert.Equal("Select first mline:", session.PromptText);
        }

        [Fact]
        public void ClosedTee_TrimsOrExtendsTheFirstToTheSecondsNearEdge_AndLeavesTheEndCapsOff()
        {
            // AutoCAD 2021: a stub (50, -50)-(50, -10) to the mline (0, 0)-(100, 0), picked at (50, -30): its end moves to
            // (50, 0), its elements run from 0 to 49 (the near edge, one below the line), and its flags are 9.
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -50), P(50, -10) });

            Start(document, "MLEDIT", "CT", P(50, -30), P(20, 0));

            DxfMlineBuilder stub = Read(document, 1);
            Assert.Equal(new[] { P3(50, -50), P3(50, 0) }, stub.Points);
            Assert.True(stub.SuppressEndCaps);
            Assert.Equal(new[] { 0.0, 49.0 }, Tail(stub, 0, 0));
            Assert.Equal(new[] { 0.0, 49.0 }, Tail(stub, 0, 1));
        }

        [Fact]
        public void ClosedTee_OfAStubThatPassesTheLine_TrimsItBackToTheLine()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -50), P(50, 30) });

            Start(document, "MLEDIT", "CT", P(50, -30), P(20, 0));

            DxfMlineBuilder stub = Read(document, 1);
            Assert.Equal(new[] { P3(50, -50), P3(50, 0) }, stub.Points);
            Assert.Equal(new[] { 0.0, 49.0 }, Tail(stub, 0, 0));
        }

        [Fact]
        public void TeeFromTheOtherEnd_StartsTheFirstAtTheLine_AndItsElementsStartAtTheNearEdge()
        {
            // AutoCAD 2021: the stub drawn (50, -10) to (50, -50), picked at (50, -30): its start moves to (50, 0), caps flag 5, and
            // its elements start 1 along it, which its first vertex holds as [offset, 1].
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -10), P(50, -50) });

            Start(document, "MLEDIT", "CT", P(50, -30), P(20, 0));

            DxfMlineBuilder stub = Read(document, 1);
            Assert.Equal(new[] { P3(50, 0), P3(50, -50) }, stub.Points);
            Assert.True(stub.SuppressStartCaps);
            Assert.Equal(new[] { 1.0 }, Tail(stub, 0, 0));
        }

        [Fact]
        public void OpenTee_AlsoOpensTheSecondsNearElementAcrossTheFirstsWidth()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -50), P(50, -10) });

            Start(document, "MLEDIT", "OT", P(50, -30), P(20, 0));

            DxfMlineBuilder line = Read(document, 0);
            Assert.Equal(new[] { 0.0 }, Tail(line, 0, 0));
            Assert.Equal(new[] { 0.0, 50.0, 51.0 }, Tail(line, 0, 1));
        }

        [Fact]
        public void CornerJoint_MovesBothEndsToWhereTheLinesMeet_AndMitresTheElementsAcrossTheCorner()
        {
            // AutoCAD 2021: (0, 0)-(80, 0) and (100, -50)-(100, 50), picked at (20, 0) and (100, -30): the first runs on to
            // (100, 0) with its elements 1, 0.5 and 0 past it, the second ends at (100, 0) with its elements 49, 49.5 and
            // the whole of it.
            CadDocument document = Drawing(new[] { P(0, 0), P(80, 0) }, new[] { P(100, -50), P(100, 50) });

            Start(document, "MLEDIT", "CJ", P(20, 0), P(100, -30));

            DxfMlineBuilder first = Read(document, 0);
            DxfMlineBuilder second = Read(document, 1);
            Assert.Equal(new[] { P3(0, 0), P3(100, 0) }, first.Points);
            Assert.Equal(new[] { P3(100, -50), P3(100, 0) }, second.Points);
            Assert.Equal(new[] { 0.0, 1.0 }, Tail(first, 1, 0));
            Assert.Empty(Tail(first, 1, 1));
            Assert.Equal(new[] { 0.0, 49.0 }, Tail(second, 0, 0));
            Assert.Equal(new[] { 0.0 }, Tail(second, 0, 1));
            Assert.True(first.SuppressEndCaps);
            Assert.True(second.SuppressEndCaps);
        }

        [Fact]
        public void AddVertex_PutsAVertexOnTheLineAtThePick_AndDeleteVertexTakesTheNearestOneOut()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0), P(100, 100) });

            Start(document, "MLEDIT", "AV", P(50, 0.3), Enter);

            Assert.Equal(new[] { P3(0, 0), P3(50, 0), P3(100, 0), P3(100, 100) }, Read(document, 0).Points);

            Start(document, "MLEDIT", "DV", P(100, 0), Enter);

            Assert.Equal(new[] { P3(0, 0), P3(50, 0), P3(100, 100) }, Read(document, 0).Points);
        }

        [Fact]
        public void CutSingle_BreaksOnlyThePickedElementBetweenThePicks_AndCutAllBreaksEvery_AndWeldClosesThem()
        {
            // AutoCAD 2021: CS at (30, y) and (60, y) on the second of three elements: [offset, 0, 30, 60] on it alone.
            CadDocument document = Blank();
            Run(Session(document), "MLSTYLE", Enter);
            DxfMlineStyleSettings three = new DxfMlineStyleSettings();
            three.Elements.Add(new DxfMlineStyleElement(0.0, DxfColor.ByLayer, "BYLAYER"));
            document.Database.AddMlineStyle("THREE", three);
            Start(document, "CMLSTYLE", "THREE", "CMLSCALE", "1", "MLINE", P(0, 0), P(100, 0), Enter);

            Start(document, "MLEDIT", "CS", P(30, -0.5), P(60, -0.5), Enter);

            DxfMlineBuilder cut = Read(document, 0);
            Assert.Equal(new[] { 0.0 }, Tail(cut, 0, 0));
            Assert.Equal(new[] { 0.0, 30.0, 60.0 }, Tail(cut, 0, 1));
            Assert.Equal(new[] { 0.0 }, Tail(cut, 0, 2));

            Start(document, "MLEDIT", "CA", P(10, 0), P(20, 0), Enter);
            Assert.Equal(new[] { 0.0, 10.0, 20.0 }, Tail(Read(document, 0), 0, 0));

            Start(document, "MLEDIT", "WA", P(5, 0), P(80, 0), Enter);
            Assert.Equal(new[] { 0.0 }, Tail(Read(document, 0), 0, 0));
            Assert.Equal(new[] { 0.0 }, Tail(Read(document, 0), 0, 1));
        }

        [Fact]
        public void Undo_TakesBackTheLastEditOfTheCommand()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0), P(100, 100) });
            CadCommandSession session = Start(document, "MLEDIT", "AV", P(50, 0));
            Assert.Equal(4, Read(document, 0).Points.Count);
            Assert.Equal("Select mline or [Undo]:", session.PromptText);

            Run(session, "U", Enter);

            Assert.Equal(3, Read(document, 0).Points.Count);
        }

        [Fact]
        public void TheEditsOfMledit_AreOneUndoStep_AndTheDrawingReopens()
        {
            CadDocument document = Drawing(new[] { P(0, 0), P(100, 0) }, new[] { P(50, -50), P(50, 50) });
            string before = document.Database.ToText();

            Start(document, "MLEDIT", "CC", P(20, 0), P(50, -30), Enter);
            AssertReopens(document);
            document.Undo();

            Assert.Equal(before, document.Database.ToText());
        }

        #endregion

        #region MLSTYLE

        [Fact]
        public void Mlstyle_NewAsksAName_ThenItsOptions_AndAddsTheStyleInCapitals()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLSTYLE");
            Assert.Equal("Enter an option [?/Current/New/Modify/Rename/Delete/Load/Save] <exit>:", session.PromptText);
            Assert.Contains("Current multiline style: STANDARD", session.History);

            Run(session, "N");
            Assert.Equal("Enter name of new multiline style:", session.PromptText);
            Run(session, "wall");
            Assert.Equal(
                "Enter style option [Description/Elements/Caps/Fill/Joints/Angles/Done] <Done>:",
                session.PromptText
            );

            Run(session, "D", "outer wall");
            Run(session, "E", "A", "0.25", "3", "BYLAYER", Enter);
            Run(session, "C", "SL", "EO", Enter);
            Run(session, "F", "C", "5");
            Run(session, "J", "Y");
            Run(session, "A", "60", "120");
            Run(session, Enter);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            var style = (DxfMlineStyle)document.Database.MLStyleDictionary.GetAt("wall");
            Assert.Equal("WALL", style.Name);
            Assert.Equal("outer wall", style.Description);
            Assert.True(style.Filled);
            Assert.Equal(5, style.FillColor.ColorIndex);
            Assert.True(style.ShowMiters);
            Assert.True(style.StartSquareCap);
            Assert.True(style.EndRoundCap);
            Assert.Equal(Math.PI / 3.0, style.StartAngle, 9);
            Assert.Equal(2.0 * Math.PI / 3.0, style.EndAngle, 9);
            Assert.Equal(new[] { 0.5, 0.25, -0.5 }.Length, style.Elements.Count);
            Assert.Equal(0.25, style.Elements[1].Offset);
            Assert.Equal(3, style.Elements[1].Color.ColorIndex);
        }

        [Fact]
        public void Mlstyle_NewRefusesANameAutoCADDoesNotTake_AndOneTheDrawingHas()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLSTYLE", "N", "a/b");

            Assert.Contains(session.History, h => h.StartsWith("A name cannot have"));
            Assert.Equal("Enter an option [?/Current/New/Modify/Rename/Delete/Load/Save] <exit>:", session.PromptText);

            Run(session, "N", "standard");
            Assert.Contains("Multiline style standard already exists.", session.History);
        }

        [Fact]
        public void Mlstyle_ElementsAreDeletedAndChanged_ButTheLastIsKept()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MLSTYLE", "N", "wall", "E", "Delete", "1");
            Assert.Equal("Enter element option [Add/Delete/Change/Done] <Done>:", session.PromptText);

            Run(session, "Delete", "1");
            Assert.Equal(
                "A multiline style has at least one element.",
                session.History.Last(h => h.StartsWith("A multiline"))
            );

            Run(session, "Change", "1", "2", "red", Enter, Enter, Enter, Enter);

            var style = (DxfMlineStyle)document.Database.MLStyleDictionary.GetAt("wall");
            DxfMlineStyleElement element = Assert.Single(style.Elements);
            Assert.Equal(2.0, element.Offset);
            Assert.Equal(1, element.Color.ColorIndex);
            Assert.Equal("BYLAYER", element.Linetype);
        }

        [Fact]
        public void Mlstyle_AStyleOfTheDrawingWithMlinesOnItCanOnlyHaveItsDescriptionChanged()
        {
            CadDocument document = Blank();
            Start(document, "MLINE", P(0, 0), P(100, 0), Enter);

            CadCommandSession session = Start(document, "MLSTYLE", "M", "STANDARD");

            Assert.Contains(session.History, h => h.Contains("only its description can"));
            Assert.Equal("Enter style option [Description/Done] <Done>:", session.PromptText);
        }

        [Fact]
        public void Mlstyle_CurrentMakesAStyleCurrent_AndModifyRewritesAnUnusedOne()
        {
            CadDocument document = Blank();
            document.Database.AddMlineStyle("WALL", new DxfMlineStyleSettings());
            CadCommandSession session = Start(document, "MLSTYLE", "C", "wall");

            Assert.Contains("Current multiline style: WALL", session.History.Last(h => h.StartsWith("Current")));

            Run(session, "M", "WALL", "D", "thick", "F", "C", "2", Enter, Enter);

            var style = (DxfMlineStyle)document.Database.MLStyleDictionary.GetAt("WALL");
            Assert.Equal("thick", style.Description);
            Assert.True(style.Filled);
        }

        #endregion

        /// <summary>All the groups of an entity as one line, to see that a record did not change.</summary>
        private static string Text(DxfUnknownEntity mline) =>
            string.Join(
                "|",
                mline.Groups.Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + ":" + g.Value.Trim())
            );

        /// <summary>The parameters an element holds at a vertex after its first (its offset): the starts and stops of its stretches.</summary>
        private static double[] Tail(DxfMlineBuilder builder, int vertex, int element) =>
            Tail(builder.Build(), vertex, element);

        private static double[] Tail(DxfUnknownEntity mline, int vertex, int element)
        {
            int at = -1;
            int each = -1;
            var found = new List<double>();

            foreach (DxfTypedValue group in mline.Groups)
            {
                if (group.TypeCode == 11)
                {
                    at++;
                    each = -1;
                }
                else if (group.TypeCode == 74)
                {
                    each++;
                }
                else if (group.TypeCode == 41 && at == vertex && each == element)
                {
                    found.Add(double.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
                }
            }

            return found.Skip(1).ToArray();
        }
    }
}
