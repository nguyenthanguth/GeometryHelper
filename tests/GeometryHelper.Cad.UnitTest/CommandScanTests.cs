using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The gaps a scan of 119 commands, typed as AutoLISP's <c>(command ...)</c> types them, found between the presenter and
    /// AutoCAD 2021's console: 3DPOLY, ZOOM and -VIEW with no window, MLEADER's first prompt,
    /// -ATTEDIT's and PLAN's defaults, DTEXT, TRIM and EXTEND in Standard mode, and VPORTS with no window. Each prompt is
    /// compared word for word with the console's transcript.
    /// </summary>
    public class CommandScanTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        #region 3DPOLY

        [Fact]
        public void ThreeDPoly_AsksTheConsolesPrompts_AndMakesOne3DPolylineOfThreeVertices()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "_.3DPOLY");
            Assert.DoesNotContain("Unknown command", string.Join("\n", session.History));
            Assert.Equal("Specify start point of polyline:", session.PromptText);

            Run(session, P(0, 0));
            Assert.Equal("Specify endpoint of line or [Undo]:", session.PromptText);
            Run(session, P(10, 0));
            Assert.Equal("Specify endpoint of line or [Undo]:", session.PromptText);
            Run(session, P(10, 10));
            Assert.Equal("Specify endpoint of line or [Close/Undo]:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            DxfPolyline3d polyline = Assert.Single(All<DxfPolyline3d>(document.ModelSpace));
            Assert.False(polyline.Closed);
            Assert.Equal(DxfPoly3dType.SimplePoly, polyline.PolyType);
            Assert.Equal(new[] { 0.0, 10.0, 10.0 }, polyline.Select(v => v.Position.X).ToArray());
            Assert.Equal(new[] { 0.0, 0.0, 10.0 }, polyline.Select(v => v.Position.Y).ToArray());
            Assert.Equal(20.0, polyline.Length, 9);
        }

        [Fact]
        public void ThreeDPoly_TypedFromAutoLisp_AsTheScanTypedIt_MakesTheSamePolyline()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Feed("(command \"_.3DPOLY\" \"0,0,0\" \"10,0,0\" \"10,10,0\" \"\")");

            Assert.DoesNotContain("Unknown command", string.Join("\n", session.History));
            DxfPolyline3d polyline = Assert.Single(All<DxfPolyline3d>(document.ModelSpace));
            Assert.Equal(3, polyline.Count());
        }

        [Fact]
        public void ThreeDPoly_Close_JoinsTheLastVertexToTheFirst_AndUndoTakesTheLastVertexBack()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "3DPOLY", P(0, 0), P(10, 0), P(10, 10), P(0, 10));
            Assert.Equal("Specify endpoint of line or [Close/Undo]:", session.PromptText);
            Run(session, "U");
            Assert.Equal(3, Assert.Single(All<DxfPolyline3d>(document.ModelSpace)).Count());
            Run(session, "C");

            Assert.False(session.IsRunning);
            DxfPolyline3d polyline = Assert.Single(All<DxfPolyline3d>(document.ModelSpace));
            Assert.True(polyline.Closed);
            Assert.Equal(3, polyline.Count());
            Assert.Equal(10.0 + 10.0 + System.Math.Sqrt(200.0), polyline.Length, 9);
        }

        [Fact]
        public void ThreeDPoly_UndoOfTheFirstSegment_TakesThePolylineAway()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "3DPOLY", P(0, 0), P(10, 0), "U");

            Assert.Empty(All<DxfPolyline3d>(document.ModelSpace));
            Assert.Equal("Specify endpoint of line or [Undo]:", session.PromptText);
            Run(session, "U");
            Assert.Equal("All segments already undone.", session.History.Last(l => l.StartsWith("All")));
            Run(session, Esc);
        }

        #endregion

        #region ZOOM, PAN and -VIEW with no window

        [Fact]
        public void Zoom_WithNoWindow_AsksTheConsolesPrompt_AndZoomsAWindow()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "ZOOM");

            Assert.DoesNotContain("There is no view to zoom.", session.History);
            Assert.Equal(
                "Specify corner of window, enter a scale factor (nX or nXP), or\n"
                    + "[All/Center/Dynamic/Extents/Previous/Scale/Window/Object] <real time>:",
                session.PromptText
            );

            Run(session, P(0, 0), P(100, 50));
            Assert.False(session.IsRunning);
            Assert.Null(session.View);

            // The window is what the session now shows, which -VIEW Save keeps: 100 wide, 100 x 534 / 942 high.
            Run(session, "-VIEW", "S", "ZW");
            DxfViewTableRecord saved = document.Database.ViewTable["ZW"];
            Assert.Equal(100.0, saved.Width, 6);
            Assert.Equal(100.0 * 534.0 / 942.0, saved.Height, 6);
            Assert.Equal(50.0, saved.CenterPoint.X, 6);
            Assert.Equal(25.0, saved.CenterPoint.Y, 6);
        }

        [Fact]
        public void ViewSave_WithNoWindow_SavesTheConsolesStartView_OfAnyDrawing_WhetherOrNotItHasAnActiveViewportRecord()
        {
            // Not the record: a drawing with no *Active viewport record gets the same view as one that has it.
            string text = new DxfDatabase("AC1027").ToText();
            int start = text.IndexOf("  0\nVPORT\n", System.StringComparison.Ordinal);
            int end = text.IndexOf("  0\n", start + 4, System.StringComparison.Ordinal);
            Assert.True(start > 0 && end > start);
            text = text.Remove(start, end - start);
            Assert.DoesNotContain("*Active", text);
            CadDocument without = CadDocument.From(DxfDatabase.Parse(text), "without.dxf");

            foreach (CadDocument document in new[] { Blank(), without })
            {
                CadCommandSession session = Start(document, "_.-VIEW");
                Assert.Equal(
                    "Enter an option [?/Delete/Orthographic/Restore/Save/sEttings/Window]:",
                    session.PromptText
                );
                Run(session, "_S");
                Assert.Equal("Enter view name to save:", session.PromptText);
                Run(session, "V1");

                Assert.DoesNotContain("There is no view to save.", session.History);
                DxfViewTableRecord view = document.Database.ViewTable["V1"];
                Assert.Equal(10.0, view.Height, 6);
                Assert.Equal(10.0 * 942.0 / 534.0, view.Width, 6);
                Assert.Equal(0.0, view.CenterPoint.X, 6);
                Assert.Equal(0.0, view.CenterPoint.Y, 6);
            }
        }

        [Fact]
        public void Pan_WithNoWindow_AsksItsPoints_AndMovesTheSessionsView()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "PAN");

            Assert.DoesNotContain("There is no view to pan.", session.History);
            Assert.Equal("Specify base point or displacement:", session.PromptText);
            Run(session, P(0, 0), P(10, 0));

            // The drawing moved 10 to the right on the screen: the view's centre is 10 to the left of where it was.
            Run(session, "-VIEW", "S", "PV");
            Assert.Equal(-10.0, document.Database.ViewTable["PV"].CenterPoint.X, 6);
        }

        #endregion

        #region The defaults and the prompts

        [Fact]
        public void MLeader_FirstPrompt_OffersTheOrderAndTheOptions_WithOptionsTheDefault()
        {
            CadCommandSession session = Start(Blank(), "MLEADER");

            Assert.Equal(
                "Specify leader arrowhead location or [leader Landing first/Content first/Options] <Options>:",
                session.PromptText
            );
            Run(session, "C");
            Assert.False(session.IsRunning);
            Assert.Contains(session.History, line => line.Contains("Content first"));
        }

        [Fact]
        public void MLeader_AnArrowheadPointStillStartsTheLeader()
        {
            CadCommandSession session = Start(Blank(), "MLEADER", P(0, 0));

            Assert.True(session.IsRunning);
            Assert.StartsWith("Specify leader landing location", session.PromptText, System.StringComparison.Ordinal);
            Run(session, Esc);
        }

        [Fact]
        public void AttEdit_OffersTheAnswerAsY_AndEnterTakesYes()
        {
            CadCommandSession session = Start(Blank(), "-ATTEDIT");

            Assert.Equal("Edit attributes one at a time? [Yes/No] <Y>:", session.PromptText);
            Run(session, Enter);
            Assert.StartsWith("Enter block name specification", session.PromptText, System.StringComparison.Ordinal);
            Run(session, Esc);
        }

        [Fact]
        public void Plan_OffersCurrentAsTheDefault_AndEnterTakesTheCurrentUcs()
        {
            CadCommandSession session = Start(Blank(), "PLAN");

            Assert.Equal("Enter an option [Current ucs/Ucs/World] <Current>:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void DText_IsACommandOfItsOwn_AndDoesNotEchoTheTextItRuns()
        {
            CadCommandSession session = Start(Blank(), "DTEXT");

            Assert.Equal("Command: DTEXT", session.History[0]);
            Assert.StartsWith("Current text style:", session.History[1], System.StringComparison.Ordinal);
            Assert.Equal("Specify start point of text or [Justify/Style]:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify height <2.5000>:", session.PromptText);
            Run(session, Esc);
        }

        [Fact]
        public void TrimAndExtend_InStandardMode_AskTheEdgesWithTheModeOffered()
        {
            CadCommandSession session = Start(Blank(), "TRIMEXTENDMODE", "0", "TRIM");

            Assert.Contains("Current settings: Projection=UCS, Edge=None, Mode=Standard", session.History);
            Assert.Contains("Select cutting edges ...", session.History);
            Assert.Equal("Select objects or [mOde] <select all>:", session.PromptText);
            Run(session, Esc);

            Run(session, "EXTEND");
            Assert.Contains("Select boundary edges ...", session.History);
            Assert.Equal("Select objects or [mOde] <select all>:", session.PromptText);

            // mOde asks the mode; Quick goes on in Quick mode, which takes every object as an edge.
            Run(session, "O");
            Assert.Equal("Enter an extend mode option [Quick/Standard] <Standard>:", session.PromptText);
            Run(session, "Q");
            Assert.Contains("Boundary edges", session.PromptText);
            Run(session, Esc);
        }

        [Fact]
        public void TrimInStandardMode_ModeStandard_AsksTheEdgesAgain()
        {
            CadCommandSession session = Start(Blank(), "TRIMEXTENDMODE", "0", "TRIM", "O", "S");

            Assert.Equal("Select objects or [mOde] <select all>:", session.PromptText);
            Run(session, Esc);
        }

        [Fact]
        public void Vports_WithNoWindow_AsksTheConsolesOptions()
        {
            CadCommandSession session = Start(Blank(), "VPORTS");

            Assert.Equal(
                "Enter an option [Save/Restore/Delete/Join/SIngle/?/2/3/4/Toggle/MOde] <3>:",
                session.PromptText
            );
            Run(session, Esc);
        }

        #endregion
    }
}
