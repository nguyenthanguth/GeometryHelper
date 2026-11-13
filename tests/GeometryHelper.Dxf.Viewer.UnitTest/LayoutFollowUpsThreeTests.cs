using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The layout follow-ups of round 4, each checked against what AutoCAD 2021's console did: CTAB, the name an undo step
    /// of a layout switch carries (LAYOUT, TILEMODE or CTAB, as the command that made it), MSPACE and PSPACE as undo steps
    /// (even where they only say "Already in ..."), and -VIEW Restore switching the layout as a step of its own.
    /// </summary>
    public class LayoutFollowUpsThreeTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A drawing with a rectangle in model space and on Layout1 a viewport about (110, 60); Layout1 shown.</summary>
        private static CadDocument WithViewport(out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            session = Start(document, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            return document;
        }

        #region CTAB

        [Fact]
        public void Ctab_AsksTheLayoutShown_AndALayoutTypedInEitherCaseIsShown_OneStepNamedCtab()
        {
            CadDocument document = Blank();
            string layout = document.Spaces[1].Name;

            CadCommandSession session = Start(document, "CTAB");
            Assert.Equal("Enter new value for CTAB <\"Model\">:", session.PromptText);
            Run(session, layout.ToLowerInvariant());

            Assert.False(session.IsRunning);
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Assert.Equal("CTAB", document.NextUndo.Name);

            Run(session, "CTAB");
            Assert.Equal("Enter new value for CTAB <\"" + layout + "\">:", session.PromptText);
            Run(session, "\"model\"");
            Assert.Same(document.ModelSpace, document.CurrentSpace);

            // AutoCAD's U says the name of the command that made the step.
            Run(session, "U");
            Assert.Equal("CTAB", session.History.Last());
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Run(session, "U");
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void Ctab_EnterKeepsTheLayout_AndANameNoLayoutHasIsRefused_AsAutoCadRefusesIt_WithNoStep()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "CTAB", Enter);
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.False(document.CanUndo);

            foreach (string typed in new[] { "Nope", "Lay*", ".", "?", document.Spaces[1].Name + "x" })
            {
                Run(session, "CTAB", typed);
                Assert.Equal("Cannot set CTAB to that value.", session.History.Last());
                Assert.False(session.IsRunning);
            }

            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void SetvarCtab_ShowsTheLayoutToo_AndReadsItsNameInQuotes()
        {
            CadDocument document = Blank();
            string layout = document.Spaces[1].Name;

            CadCommandSession session = Start(document, "SETVAR", "CTAB", layout);
            Assert.Same(document.Spaces[1], document.CurrentSpace);

            Run(session, "SETVAR", "CTAB");
            Assert.Equal("Enter new value for CTAB <\"" + layout + "\">:", session.PromptText);
            Run(session, Esc);
            Assert.False(document.Spaces[1].IsModel);
        }

        [Fact]
        public void TheStepOfALayoutSwitch_IsNamedAsTheCommandThatMadeIt_LayoutTilemodeOrCtab()
        {
            CadDocument document = Blank();
            string layout = document.Spaces[1].Name;
            CadCommandSession session = Start(document, "LAYOUT", "S", layout);
            Assert.Equal("LAYOUT", document.NextUndo.Name);

            Run(session, "TILEMODE", "1");
            Assert.Equal("TILEMODE", document.NextUndo.Name);
            Run(session, "TILEMODE", "0");
            Assert.Equal("TILEMODE", document.NextUndo.Name);

            Run(session, "CTAB", "Model");
            Assert.Equal("CTAB", document.NextUndo.Name);

            Run(session, "U");
            Assert.Equal("CTAB", session.History.Last());
            Run(session, "U");
            Assert.Equal("TILEMODE", session.History.Last());
            Run(session, "U");
            Assert.Equal("TILEMODE", session.History.Last());
            Run(session, "U");
            Assert.Equal("LAYOUT", session.History.Last());
            Assert.Same(document.ModelSpace, document.CurrentSpace);
        }

        #endregion

        #region MSPACE and PSPACE

        [Fact]
        public void MspaceAndPspace_AreUndoSteps_UTakesThemBack_AsAutoCadsConsoleDoes()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            CadUndoStep drawn = document.NextUndo;

            Run(session, "MSPACE");
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Assert.Equal("MSPACE", document.NextUndo.Name);
            Run(session, "PSPACE");
            Assert.Null(document.ActiveViewport);
            Assert.Equal("PSPACE", document.NextUndo.Name);

            Run(session, "U");
            Assert.Equal("PSPACE", session.History.Last());
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Run(session, "U");
            Assert.Equal("MSPACE", session.History.Last());
            Assert.Null(document.ActiveViewport);
            Assert.Same(drawn, document.NextUndo);

            Run(session, "REDO");
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Run(session, "REDO");
            Assert.Null(document.ActiveViewport);
        }

        [Fact]
        public void MspaceWhereAlreadyInModelSpace_SaysSo_AndIsAStepToo_ButAnEmptyOne()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            CadUndoStep drawn = document.NextUndo;

            Run(session, "MSPACE", "MSPACE");
            Assert.Equal("Already in model space.", session.History.Last());
            Run(session, "PSPACE", "PSPACE");
            Assert.Equal("Already in paper space.", session.History.Last());

            // AutoCAD 2021's console undid the four in turn: the two "Already" ones changed nothing, each said its own name.
            Run(session, "U");
            Assert.Equal("PSPACE", session.History.Last());
            Assert.Null(document.ActiveViewport);
            Run(session, "U");
            Assert.Equal("PSPACE", session.History.Last());
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Run(session, "U");
            Assert.Equal("MSPACE", session.History.Last());
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Run(session, "U");
            Assert.Equal("MSPACE", session.History.Last());
            Assert.Null(document.ActiveViewport);
            Assert.Same(drawn, document.NextUndo);
        }

        [Fact]
        public void MspaceOfALayoutWithNoViewport_SaysSoAsAutoCadDoes_AndIsAStep()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "ERASE", new[] { handle }, Enter);
            CadUndoStep erased = document.NextUndo;

            Run(session, "MSPACE");

            Assert.Equal("There are no active Model space viewports.", session.History.Last());
            Assert.Equal("MSPACE", document.NextUndo.Name);
            Run(session, "U");
            Assert.Same(erased, document.NextUndo);
        }

        [Fact]
        public void MspaceAndPspaceOnTheModelTab_AreRefused_WithNoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MSPACE");
            Assert.Equal("** Command not allowed in Model Tab **", session.History.Last());
            Run(session, "PSPACE");
            Assert.Equal("** Command not allowed in Model Tab **", session.History.Last());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ALayoutSwitchAfterMspace_IsUndoneInOrder_TheViewportWorkedThroughComingBack()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "MSPACE", "CTAB", "Model");
            Assert.Same(document.ModelSpace, document.CurrentSpace);

            Run(session, "U");
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Run(session, "U");
            Assert.Null(document.ActiveViewport);
        }

        #endregion

        #region -VIEW Restore

        [Fact]
        public void ViewRestoreOfAPaperViewFromTheModelTab_ShowsTheLayoutAsPartOfTheCommandsStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.View = new CadView(400, 300);
            session.View.ZoomTo(new DisplayBox(0, 0, 400, 300), 0.0);
            document.ShowSpace(document.Spaces[1]);
            Run(session, "-VIEW", "S", "PV");
            document.ShowSpace(document.ModelSpace);

            Run(session, "-VIEW", "R", "PV");
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Assert.Equal("-VIEW", document.NextUndo.Name);

            // One U for the restore, which goes back to the Model tab; the next U undoes the switch to the layout before it.
            Run(session, "U");
            Assert.Equal("-VIEW", session.History.Last());
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Run(session, "U");
            Assert.Equal("LAYOUT", session.History.Last());
            Assert.Same(document.Spaces[1], document.CurrentSpace);

            Run(session, "REDO");
            Run(session, "REDO");
            Assert.Same(document.Spaces[1], document.CurrentSpace);
        }

        #endregion
    }
}
