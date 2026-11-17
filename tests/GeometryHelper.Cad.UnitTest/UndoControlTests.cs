using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// UNDO's Auto, Control (All, None, One, Combine, Layer), BEgin and End, and the words U, UNDO and REDO write, as AutoCAD
    /// 2021's console has them (probed through its standard input, UNDOCTL read after each): a group made by BEgin and End is
    /// "GROUP LINE LINE" to U and REDO and one step of them; U inside a group takes one step of it; Control None turns U off
    /// ("U command disabled.  Use UNDO command to turn it on") and its UNDO asks the control option alone; Control One keeps
    /// the last command alone and its UNDO asks "Enter an option [Control] &lt;1&gt;:"; the names of the steps UNDO 2 takes
    /// stand on one line.
    /// </summary>
    public class UndoControlTests
    {
        private const string Settings = "Current settings: Auto = On, Control = All, Combine = Yes, Layer = Yes";

        /// <summary>Erases the three lines and the circle one command each: ERASE is the name of each step.</summary>
        private static CadCommandSession Erase(CadCommandSession session, params string[] handles)
        {
            foreach (string handle in handles)
            {
                Run(session, "E", new[] { handle }, Enter);
            }

            return session;
        }

        [Fact]
        public void TheCommandsBetweenBeginAndEnd_AreOneGroup_UAndRedoTakeWhole()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Erase(session, "A1");

            Run(session, "UNDO", "BE");
            Erase(session, "A2", "A3");
            Run(session, "UNDO", "E");

            Run(session, "U");

            Assert.Equal("GROUP ERASE ERASE", session.History.Last());
            Assert.NotNull(document.FindEntity("A2"));
            Assert.NotNull(document.FindEntity("A3"));
            Assert.Null(document.FindEntity("A1"));

            Run(session, "REDO");

            Assert.Equal("Everything has been redone", session.History.Last());
            Assert.Equal("GROUP ERASE ERASE", session.History[session.History.Count - 2]);
            Assert.Null(document.FindEntity("A2"));
            Assert.Null(document.FindEntity("A3"));
        }

        [Fact]
        public void UInsideAnOpenGroup_TakesOneStepOfIt_AndTheGroupStaysOpen()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "UNDO", "BE");
            Erase(session, "A1", "A2");
            Run(session, "U");

            Assert.Equal("ERASE", session.History.Last());
            Assert.NotNull(document.FindEntity("A2"));
            Assert.Null(document.FindEntity("A1"));

            Run(session, "UNDO", "E", "U");

            Assert.Equal("GROUP ERASE", session.History.Last());
            Assert.NotNull(document.FindEntity("A1"));
        }

        [Fact]
        public void BeginInsideAnOpenGroup_ClosesItAndBeginsAnother()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "UNDO", "BE");
            Erase(session, "A1");
            Run(session, "UNDO", "BE");
            Erase(session, "A2");
            Run(session, "UNDO", "E", "U");

            Assert.Equal("GROUP ERASE", session.History.Last());
            Assert.NotNull(document.FindEntity("A2"));
            Assert.Null(document.FindEntity("A1"));

            Run(session, "U");

            Assert.Equal("GROUP ERASE", session.History.Last());
            Assert.NotNull(document.FindEntity("A1"));
        }

        [Fact]
        public void EndWithNoGroupOpen_SaysNothing_AndASingleStepKeepsItsName()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Erase(session, "A1");
            int before = session.History.Count;

            Run(session, "UNDO", "E");

            // Only the settings and the answer are written: no message for an End that closes nothing.
            Assert.Equal(
                new[]
                {
                    "Command: UNDO",
                    Settings,
                    "Enter the number of operations to undo or [Auto/Control/BEgin/End/Mark/Back] <1>: E",
                },
                session.History.Skip(before)
            );

            Run(session, "U");

            Assert.Equal("ERASE", session.History.Last());
        }

        [Fact]
        public void UndoOfSeveralOperations_WritesTheirNamesOnOneLine_AndSaysEverythingUndoneWhenItRanOut()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Erase(session, "A1", "A2", "A3");

            Run(session, "UNDO", "2");
            Assert.Equal("ERASE ERASE", session.History.Last());
            Assert.NotNull(document.FindEntity("A2"));
            Assert.NotNull(document.FindEntity("A3"));
            Assert.Null(document.FindEntity("A1"));

            Run(session, "UNDO", "5");

            Assert.Equal(
                new[] { "ERASE", "Everything has been undone" },
                session.History.Skip(session.History.Count - 2)
            );
        }

        [Fact]
        public void ControlNone_TurnsUOff_AndItsUndoAsksTheControlOptionAlone()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Erase(session, "A1");

            Run(session, "UNDO", "C");
            Assert.Equal("Enter an UNDO control option [All/None/One/Combine/Layer] <All>:", session.PromptText);
            Run(session, "N", "U");

            Assert.Equal("U command disabled.  Use UNDO command to turn it on", session.History.Last());
            Assert.Null(document.FindEntity("A1"));

            Run(session, "UNDO");

            Assert.Equal(
                "Current settings: Auto = Off, Control = None, Combine = Yes, Layer = Yes",
                session.History.Last()
            );
            Assert.Equal("Enter an UNDO control option [All/None/One/Combine/Layer] <All>:", session.PromptText);

            Run(session, "BE");

            Assert.Equal("Invalid option keyword.", session.History.Last());
            Assert.Equal("Enter an UNDO control option [All/None/One/Combine/Layer] <All>:", session.PromptText);
        }

        [Fact]
        public void ControlAll_AfterNone_TurnsAutoOnAgain_ButWhatWasDoneWhileNoneIsGone()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "UNDO", "C", "N");
            Erase(session, "A1");
            Run(session, "UNDO", Enter);

            Assert.False(session.IsRunning);

            Run(session, "UNDO");
            Assert.Equal(Settings, session.History.Last());
            Run(session, Esc, "U");

            Assert.Equal("Everything has been undone", session.History.Last());
            Assert.Null(document.FindEntity("A1"));
        }

        [Fact]
        public void ControlOne_KeepsTheLastCommandAlone_AndItsUndoAsksForOne()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Erase(session, "A1");

            Run(session, "UNDO", "C", "O");
            Erase(session, "A2", "A3");
            Run(session, "U");

            Assert.Equal(
                new[] { "ERASE", "Everything has been undone" },
                session.History.Skip(session.History.Count - 2)
            );
            Assert.NotNull(document.FindEntity("A3"));
            Assert.Null(document.FindEntity("A2"));

            Run(session, "U");

            Assert.Equal("Everything has been undone", session.History.Last());
            Assert.Null(document.FindEntity("A2"));

            Run(session, "UNDO");

            Assert.Equal(
                "Current settings: Auto = Off, Control = One, Combine = Yes, Layer = Yes",
                session.History.Last()
            );
            Assert.Equal("Enter an option [Control] <1>:", session.PromptText);
        }

        [Theory]
        [InlineData("2")]
        [InlineData("0")]
        [InlineData("abc")]
        [InlineData("BE")]
        public void ControlOne_Undo_RefusesWhatIsNotOne(string typed)
        {
            CadCommandSession session = Run(Session(Plan()), "UNDO", "C", "O", "UNDO");

            Run(session, typed);

            Assert.Equal("Requires an integer value 1, or option keyword.", session.History.Last());
            Assert.Equal("Enter an option [Control] <1>:", session.PromptText);

            Run(session, "1");

            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ControlCombineAndLayer_AskYesOrNo_EnterIsYes_AndTheSettingsShowThem()
        {
            CadCommandSession session = Run(Session(Plan()), "UNDO", "C", "C");
            Assert.Equal("Combine zoom and pan operations? [Yes/No] <Yes>:", session.PromptText);

            Run(session, "O");
            Assert.Equal("Yes or No, please.", session.History.Last());
            Assert.Equal("Combine zoom and pan operations? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N");

            Run(session, "UNDO", "C", "L");
            Assert.Equal("Combine layer dialog operations? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N", "UNDO");
            Assert.Equal(
                "Current settings: Auto = On, Control = All, Combine = No, Layer = No",
                session.History.Last()
            );

            Run(session, Esc, "UNDO", "C", "C", Enter, "UNDO", "C", "L", Enter, "UNDO");
            Assert.Equal(Settings, session.History.Last());
        }

        [Fact]
        public void Auto_AsksOnOrOff_TheCurrentOneIsTheDefault_AndTheSettingsShowIt()
        {
            CadCommandSession session = Run(Session(Plan()), "UNDO", "A");
            Assert.Equal("Enter UNDO Auto mode [ON/OFF] <On>:", session.PromptText);

            Run(session, "OFF", "UNDO");
            Assert.Equal(
                "Current settings: Auto = Off, Control = All, Combine = Yes, Layer = Yes",
                session.History.Last()
            );

            Run(session, "A");
            Assert.Equal("Enter UNDO Auto mode [ON/OFF] <Off>:", session.PromptText);
            Run(session, "ON", "UNDO");
            Assert.Equal(Settings, session.History.Last());
        }

        [Fact]
        public void RedoWithNothingToRedo_SaysSoAsAutoCadDoes()
        {
            CadCommandSession session = Run(Session(Plan()), "REDO");

            Assert.Equal("Nothing to redo", session.History.Last());
        }

        [Fact]
        public void ControlCombineNo_MakesEachRealTimeZoomAStepOfItsOwn()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            string handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            session.View = new CadView(800, 600);
            document.EnterViewport(handle);

            Run(session, "UNDO", "C", "C", "N");
            Assert.True(document.SetViewportView(handle, P(1000, 500), 800, "ZOOM", true).Succeeded);
            Assert.True(document.SetViewportView(handle, P(900, 500), 800, "PAN", true).Succeeded);

            Assert.Equal("PAN", document.NextUndo.Name);
            Assert.Single(document.NextUndo.Records);
            Assert.True(document.Undo());
            Assert.Equal("ZOOM", document.NextUndo.Name);
        }
    }
}
