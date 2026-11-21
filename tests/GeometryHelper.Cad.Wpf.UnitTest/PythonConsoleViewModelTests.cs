using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Python;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonConsoleKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The PYTHON palette's view model with no engine and no window (D720): the host is a fake that keeps what it was asked and
    /// raises what the real host raises. Entries and their echo, history, the four kinds of line and the cap of 5 000, a console
    /// for each drawing, and the states of the input, the toolbar and the status line.
    /// </summary>
    public sealed class PythonConsoleViewModelTests
    {
        private const string NoPython =
            "No Python 3.11 to 3.15 (64-bit) was found. Install one from python.org, or set its python.exe in Global settings > Python.";

        private readonly FakePythonConsoleHost _host = new FakePythonConsoleHost();
        private readonly List<string> _commands = new List<string>();
        private readonly CadCommandSession _session = Drawing("plan.dxf");
        private readonly PythonConsoleViewModel _console;
        private int _settingsOpened;

        public PythonConsoleViewModelTests()
        {
            _console = new PythonConsoleViewModel(_host, _commands.Add, () => _settingsOpened++);
            _console.Follow(_session.Document, _session);
        }

        /// <summary>Types text in the input and presses Enter.</summary>
        private void Enter(string text)
        {
            _console.InputText = text;
            _console.Submit();
        }

        /// <summary>The lines of the transcript shown, each as <c>Kind|mark|text</c>.</summary>
        private string[] Shown() => _console.Lines.Select(l => l.Kind + "|" + l.Mark + "|" + l.Text).ToArray();

        /// <summary>What the host does for a palette entry once the owner has run the post: the run named Python begins, prints and ends.</summary>
        private void RunEntry(CadPythonOutcome outcome, params string[] printed)
        {
            _host.Start("Python", _session.Document);

            foreach (string line in printed)
            {
                _host.Print(line);
            }

            _host.Finish("Python", _session.Document, outcome);
        }

        // ---- Entries

        [Fact]
        public void ACompleteEntry_IsEchoedAfterItsMark_SentToTheHost_AndTheInputIsCleared()
        {
            Enter("x = 2 + 3");

            Assert.Equal(new[] { "Echo|>>> |x = 2 + 3" }, Shown());
            Assert.Equal("", _console.InputText);
            (CadCommandSession session, string text) = Assert.Single(_host.Entered);
            Assert.Same(_session, session);
            Assert.Equal("x = 2 + 3", text);
        }

        [Fact]
        public void TheValueOfAnEntry_ComesAfterItsEcho_AsAnOutputLine()
        {
            // D730: x = 2 + 3, then x, shows 5.
            Enter("x = 2 + 3");
            RunEntry(CadPythonOutcome.Completed);
            Enter("x");
            RunEntry(CadPythonOutcome.Completed, "5");

            Assert.Equal(new[] { "Echo|>>> |x = 2 + 3", "Echo|>>> |x", "Output||5" }, Shown());
        }

        [Fact]
        public void AnIncompleteEntry_KeepsItsText_AndOpensAContinuationLine_WithNothingEchoedYet()
        {
            _host.OnEnter = text => CadPythonEntry.Incomplete;

            Enter("def area(r):");

            Assert.Equal("def area(r):\n", _console.InputText);
            Assert.Equal(new[] { ">>>", "..." }, _console.InputMarks.Split('\n'));
            Assert.Empty(_console.Lines);
        }

        [Fact]
        public void ABlockOfTwoLines_IsEchoedWithAMarkOnEachLine_AndSentWholeOnceItIsComplete()
        {
            // Enter at "def area(r):" is incomplete; the user types the body and Enter twice: the text is then complete, as
            // codeop sees a block only after its blank line.
            _host.OnEnter = text =>
                text.EndsWith("\n", StringComparison.Ordinal) ? CadPythonEntry.Ran : CadPythonEntry.Incomplete;
            Enter("def area(r):");
            _console.InputText = _console.InputText + "    return r * r";
            _console.Submit();
            Assert.Equal("def area(r):\n    return r * r\n", _console.InputText);

            _console.Submit();

            Assert.Equal("def area(r):\n    return r * r\n", _host.Entered.Last().Text);
            Assert.Equal(new[] { "Echo|>>> |def area(r):", "Echo|... |    return r * r" }, Shown().Take(2).ToArray());
            Assert.Equal("", _console.InputText);
        }

        [Fact]
        public void ATextOfTwoLines_ShowsAMarkForEachLine_AndIsSentWhole()
        {
            // Shift+Enter puts "\n" into the box by hand; Enter then sends both lines.
            _console.InputText = "a = 1\nb = a + 1";

            Assert.Equal(new[] { ">>>", "..." }, _console.InputMarks.Split('\n'));

            _console.Submit();

            Assert.Equal("a = 1\nb = a + 1", Assert.Single(_host.Entered).Text);
        }

        [Fact]
        public void AnEntryThatIsASyntaxError_ShowsItsLinesAsErrors_AfterTheEcho_AndTheInputIsCleared()
        {
            // The host reports a syntax error while Enter is still running, before it returns Ran.
            _host.OnEnter = text =>
            {
                _host.PrintError("  File \"<string>\", line 1");
                _host.PrintError("SyntaxError: invalid syntax");
                return CadPythonEntry.Ran;
            };

            Enter("1 +");

            Assert.Equal(
                new[] { "Echo|>>> |1 +", "Error||  File \"<string>\", line 1", "Error||SyntaxError: invalid syntax" },
                Shown()
            );
            Assert.Equal("", _console.InputText);
        }

        [Fact]
        public void AnEntryTheHostRefuses_KeepsItsText_AndTheRefusalIsAHostLine_NotAnError()
        {
            _host.OnEnter = text =>
            {
                _host.PrintError("A command is running: finish it or press Esc first.");
                return CadPythonEntry.Refused;
            };

            Enter("x = 1");

            Assert.Contains("Host||A command is running: finish it or press Esc first.", Shown());
            Assert.DoesNotContain(_console.Lines, l => l.Kind == PythonLineKind.Error);
            Assert.Equal("x = 1", _console.InputText);
        }

        [Fact]
        public void AnEmptyInput_IsNotAnEntry_AndNothingIsSent()
        {
            Enter("");
            Enter("   ");

            Assert.Empty(_host.Entered);
            Assert.Empty(_console.Lines);
        }

        // ---- History

        [Fact]
        public void UpAndDown_WalkTheEntriesOfTheDrawing_AndComeBackToTheTextBeingTyped()
        {
            Enter("a = 1");
            Enter("b = 2");
            Enter("c = 3");
            _console.InputText = "dra";

            Assert.True(_console.HistoryUp());
            Assert.Equal("c = 3", _console.InputText);
            Assert.True(_console.HistoryUp());
            Assert.Equal("b = 2", _console.InputText);
            Assert.True(_console.HistoryUp());
            Assert.Equal("a = 1", _console.InputText);
            Assert.False(_console.HistoryUp());
            Assert.Equal("a = 1", _console.InputText);

            Assert.True(_console.HistoryDown());
            Assert.Equal("b = 2", _console.InputText);
            Assert.True(_console.HistoryDown());
            Assert.Equal("c = 3", _console.InputText);
            Assert.True(_console.HistoryDown());
            Assert.Equal("dra", _console.InputText);
            Assert.False(_console.HistoryDown());
        }

        [Fact]
        public void WithNoEntryYet_UpDoesNothing()
        {
            _console.InputText = "dra";

            Assert.False(_console.HistoryUp());
            Assert.Equal("dra", _console.InputText);
        }

        [Fact]
        public void AfterANewEntry_UpStartsAtTheNewestAgain()
        {
            Enter("a = 1");
            Enter("b = 2");
            _console.HistoryUp();
            _console.HistoryUp();

            Enter("c = 3");

            Assert.True(_console.HistoryUp());
            Assert.Equal("c = 3", _console.InputText);
        }

        [Fact]
        public void Of205Entries_TheLast200AreKept_Entry6IsTheOldestUpReaches()
        {
            for (int i = 1; i <= 205; i++)
            {
                Enter("e" + i);
            }

            Assert.True(_console.HistoryUp());
            Assert.Equal("e205", _console.InputText);

            for (int i = 0; i < 199; i++)
            {
                Assert.True(_console.HistoryUp());
            }

            Assert.Equal("e6", _console.InputText);
            Assert.False(_console.HistoryUp());
        }

        [Fact]
        public void APyRunTypedOnTheCommandLine_IsAnnouncedWithItsFileName()
        {
            // The palette did not start this run, so only the host's path can name the file (D737).
            _host.Start("PYRUN", _session.Document, @"D:\Scripts\circles.py");

            Assert.Equal(new[] { "Host||PYRUN circles.py" }, Shown());
        }

        // ---- The four kinds of line

        [Fact]
        public void ALineIsEchoOutputErrorOrHost_AsItCameFromTheEntryTheScriptThePythonErrorOrTheHost()
        {
            _host.Start("PYRUN", _session.Document);
            _host.Print("circles drawn: 10");
            _host.PrintError("Traceback (most recent call last):");
            _host.Finish("PYRUN", _session.Document, CadPythonOutcome.Failed, "ZeroDivisionError: division by zero");
            Enter("x");

            Assert.Equal(
                new[]
                {
                    "Host||PYRUN",
                    "Output||circles drawn: 10",
                    "Error||Traceback (most recent call last):",
                    "Echo|>>> |x",
                },
                Shown()
            );
        }

        [Fact]
        public void TheStartOfAFileDroppedOnTheTranscript_IsAHostLineNamingTheFile_AndTheRunIsPosted()
        {
            Assert.True(_console.DropFile(@"C:\scripts\circles.py"));
            _host.Start("PYRUN", _session.Document);

            Assert.Equal(new[] { "Host||PYRUN circles.py" }, Shown());
            (CadCommandSession session, string path) = Assert.Single(_host.Files);
            Assert.Same(_session, session);
            Assert.Equal(@"C:\scripts\circles.py", path);
        }

        [Fact]
        public void AFileOfCapitalLetters_IsAPyFileToo()
        {
            Assert.True(_console.DropFile(@"C:\SCRIPTS\CIRCLES.PY"));

            Assert.Equal(@"C:\SCRIPTS\CIRCLES.PY", Assert.Single(_host.Files).Path);
        }

        [Fact]
        public void ACommandPyloadDefined_StartsWithAHostLineOfItsName()
        {
            _host.Start("HELLO", _session.Document);
            _host.Print("hello");
            _host.Finish("HELLO", _session.Document);

            Assert.Equal(new[] { "Host||HELLO", "Output||hello" }, Shown());
        }

        [Fact]
        public void ACompletedRun_WritesNothingOfItsOwn_AndAPaletteEntryWritesNoHostLine()
        {
            Enter("pass");
            RunEntry(CadPythonOutcome.Completed);

            Assert.Equal(new[] { "Echo|>>> |pass" }, Shown());
        }

        [Fact]
        public void ARefusalOfAFileRun_IsAHostLine_NotAnError()
        {
            // D705: the host writes the refusal as an error line, then ends the refused run with Refused and the same text.
            const string refusal = "A Python script is running in other.dxf: wait for it or press Esc.";
            _host.PrintError(refusal);
            _host.Finish("PYRUN", _session.Document, CadPythonOutcome.Refused, refusal);

            Assert.Equal(new[] { "Host||" + refusal }, Shown());
        }

        [Fact]
        public void ARunRefusedWhileAnotherGoesOn_DoesNotEndTheOneGoingOn()
        {
            // D705 refuses a second run: the refused one ends at once with Refused, the first is still computing.
            _host.Start("PYRUN", _session.Document);

            _host.PrintError("A Python script is running in plan.dxf: wait for it or press Esc.");
            _host.Finish(
                "PYRUN",
                _session.Document,
                CadPythonOutcome.Refused,
                "A Python script is running in plan.dxf: wait for it or press Esc."
            );

            Assert.True(_console.IsRunning);
            Assert.True(_console.CanStop);
            Assert.False(_console.IsInputEnabled);
        }

        [Fact]
        public void ResetConsole_WhileARunGoesOn_DoesNothing()
        {
            _host.Start("PYRUN", _session.Document);

            _console.ResetConsole();

            Assert.Empty(_host.Reset);
        }

        [Fact]
        public void TheResultLineOfPyload_IsAHostLine()
        {
            _host.Start("PYLOAD", _session.Document);
            _host.Print("tools.py loaded: HELLO, NUMBER.");
            _host.Finish("PYLOAD", _session.Document);

            Assert.Equal(new[] { "Host||PYLOAD", "Host||tools.py loaded: HELLO, NUMBER." }, Shown());
        }

        // ---- The 5 000 lines

        private void Print(int lines)
        {
            _host.Start("PYRUN", _session.Document);

            for (int i = 1; i <= lines; i++)
            {
                _host.Print("line " + i);
            }

            _host.Finish("PYRUN", _session.Document);
        }

        [Fact]
        public void Exactly5000Lines_AreAllKept()
        {
            Print(4999); // and the host line of the run start: 5 000 lines.

            Assert.Equal(5000, _console.Lines.Count);
            Assert.Equal("PYRUN", _console.Lines[0].Text);
            Assert.Equal("line 4999", _console.Lines[4999].Text);
        }

        [Fact]
        public void The5001stLine_DropsTheOldest()
        {
            Print(5000); // the host line of the run start and 5 000 lines: 5 001.

            Assert.Equal(5000, _console.Lines.Count);
            Assert.Equal("line 1", _console.Lines[0].Text);
            Assert.Equal("line 5000", _console.Lines[4999].Text);
        }

        [Fact]
        public void After5003Lines_TheLast5000AreKept_InOrder()
        {
            Print(5003);

            Assert.Equal(5000, _console.Lines.Count);
            Assert.Equal("line 4", _console.Lines[0].Text);
            Assert.Equal("line 5003", _console.Lines[4999].Text);
        }

        // ---- Clear and Reset console

        [Fact]
        public void Clear_EmptiesTheTranscriptOfTheDrawingShown_AndKeepsItsEntries()
        {
            Enter("a = 1");

            _console.Clear();

            Assert.Empty(_console.Lines);
            Assert.True(_console.HistoryUp());
            Assert.Equal("a = 1", _console.InputText);
        }

        [Fact]
        public void ResetConsole_AsksTheHostToResetThatDrawing_SaysSoInAHostLine_AndKeepsTheEntries()
        {
            Enter("a = 1");

            _console.ResetConsole();

            Assert.Equal(new[] { _session.Document }, _host.Reset);
            Assert.Equal(PythonLineKind.Host, _console.Lines.Last().Kind);
            Assert.True(_console.HistoryUp());
        }

        // ---- A console for each drawing

        [Fact]
        public void EachDrawingHasItsOwnTranscriptAndItsOwnHistory()
        {
            CadCommandSession other = Drawing("other.dxf");
            Enter("a = 1");

            _console.Follow(other.Document, other);
            Assert.Empty(_console.Lines);
            Assert.False(_console.HistoryUp());
            _console.InputText = "b = 2";
            _console.Submit();
            Assert.Equal(new[] { "Echo|>>> |b = 2" }, Shown());
            Assert.Equal(other.Document.Title, _console.DocumentName);

            _console.Follow(_session.Document, _session);
            Assert.Equal(new[] { "Echo|>>> |a = 1" }, Shown());
            Assert.Equal(_session.Document.Title, _console.DocumentName);
            Assert.True(_console.HistoryUp());
            Assert.Equal("a = 1", _console.InputText);
        }

        [Fact]
        public void AnEntryIsSentWithTheSessionOfTheDrawingShown()
        {
            CadCommandSession other = Drawing("other.dxf");
            _console.Follow(other.Document, other);

            Enter("a = 1");

            Assert.Same(other, Assert.Single(_host.Entered).Session);
        }

        [Fact]
        public void TheLinesOfARunInAnotherDrawing_GoToThatDrawingsTranscript_NotTheOneShown()
        {
            CadCommandSession other = Drawing("other.dxf");
            _console.Follow(other.Document, other);
            _console.Follow(_session.Document, _session);

            _host.Start("PYRUN", other.Document);
            _host.Print("from other");
            _host.Finish("PYRUN", other.Document);

            Assert.Empty(_console.Lines);
            _console.Follow(other.Document, other);
            Assert.Contains("Output||from other", Shown());
        }

        [Fact]
        public void WithNoDrawingOpen_TheInputAndTheButtonsAreOff_AndTheTranscriptIsEmpty()
        {
            Enter("a = 1");

            _console.Follow(null, null);

            Assert.Empty(_console.Lines);
            Assert.Equal("", _console.DocumentName);
            Assert.False(_console.IsInputEnabled);
            Assert.False(_console.CanRunFile);
            Assert.False(_console.CanReset);
            Assert.False(_console.DropFile(@"C:\scripts\circles.py"));
        }

        [Fact]
        public void ClosingADrawing_DropsItsConsole_ResetsItsGlobals_AndItsTranscriptAndEntriesAreGone()
        {
            CadCommandSession other = Drawing("other.dxf");
            Enter("a = 1");
            _console.Follow(other.Document, other);

            _console.Forget(_session.Document);

            Assert.Equal(new[] { _session.Document }, _host.Reset);
            _console.Follow(_session.Document, _session);
            Assert.Empty(_console.Lines);
            Assert.False(_console.HistoryUp());
        }

        // ---- The states

        [Fact]
        public void WithPythonAndNothingRunning_TheInputAndTheButtonsAreOn_AndStopIsOff()
        {
            Assert.True(_console.IsUsable);
            Assert.True(_console.IsInputEnabled);
            Assert.True(_console.CanRunFile);
            Assert.True(_console.CanReset);
            Assert.False(_console.CanStop);
            Assert.Equal(PythonStatusKind.Ready, _console.StatusKind);
            Assert.False(_console.ShowsSettingsLink);
            Assert.Equal("Enter runs · Shift+Enter new line · Up/Down history", _console.StatusRight);
        }

        [Fact]
        public void TheStatusLine_SaysTheEngineStartsAtTheFirstRun_UntilItHasStarted()
        {
            Assert.Equal("Python 3.14.4 (venv314) · starts at the first run", _console.StatusLeft);

            _host.IsStarted = true;

            Assert.Equal("Python 3.14.4 (venv314)", _console.StatusLeft);
        }

        [Fact]
        public void TheProgramShownInBrackets_IsTheVenvFolder_OrTheFolderHoldingPythonExe()
        {
            Assert.Equal("venv314", PythonConsoleViewModel.ProgramLabel(@"D:\env\venv314\Scripts\python.exe"));
            Assert.Equal("Python314", PythonConsoleViewModel.ProgramLabel(@"C:\Python314\python.exe"));
            Assert.Equal("", PythonConsoleViewModel.ProgramLabel(null));
        }

        [Fact]
        public void WhileAFileRuns_TheInputAndTheButtonsAreGreyed_StopIsLive_AndTheStatusSaysWhatRuns()
        {
            Assert.True(_console.DropFile(@"C:\scripts\circles.py"));
            _host.Start("PYRUN", _session.Document);

            Assert.True(_console.IsRunning);
            Assert.False(_console.IsInputEnabled);
            Assert.False(_console.CanRunFile);
            Assert.False(_console.CanReset);
            Assert.True(_console.CanStop);
            Assert.Equal("circles.py is running: Esc stops it", _console.InputPlaceholder);
            Assert.Equal(PythonStatusKind.Busy, _console.StatusKind);
            Assert.Equal("Running circles.py in " + _session.Document.Title, _console.StatusLeft);
            Assert.Equal("Esc stops it", _console.StatusRight);
        }

        [Fact]
        public void AnEntryTypedWhileARunGoesOn_IsNotSentToTheHost()
        {
            _host.Start("PYRUN", _session.Document);

            Enter("x = 1");

            Assert.Empty(_host.Entered);
        }

        [Fact]
        public void WhileARunGoesOnInAnotherDrawing_TheInputHereIsGreyedToo_AndTheStatusNamesThatDrawing()
        {
            // One run at a time in the process (D705): another drawing's console cannot start one.
            CadCommandSession other = Drawing("other.dxf");
            _console.Follow(other.Document, other);
            _console.Follow(_session.Document, _session);

            _host.Start("PYRUN", other.Document);

            Assert.False(_console.IsInputEnabled);
            Assert.True(_console.CanStop);
            Assert.EndsWith(" in " + other.Document.Title, _console.StatusLeft, StringComparison.Ordinal);
        }

        [Fact]
        public void WhenTheRunEnds_TheInputAndTheButtonsComeBack()
        {
            _host.Start("PYRUN", _session.Document);
            _host.Finish("PYRUN", _session.Document);

            Assert.False(_console.IsRunning);
            Assert.True(_console.IsInputEnabled);
            Assert.True(_console.CanRunFile);
            Assert.True(_console.CanReset);
            Assert.False(_console.CanStop);
            Assert.Equal(PythonStatusKind.Ready, _console.StatusKind);
        }

        [Fact]
        public void AFailedRun_SaysTheDrawingIsAsBefore_UntilTheNextRunStarts()
        {
            Assert.True(_console.DropFile(@"C:\scripts\circles.py"));
            _host.Start("PYRUN", _session.Document);
            _host.Finish("PYRUN", _session.Document, CadPythonOutcome.Failed, "ZeroDivisionError: division by zero");

            Assert.Equal("circles.py failed: the drawing is as before", _console.StatusRight);

            Enter("x");
            RunEntry(CadPythonOutcome.Completed);

            Assert.Equal("Enter runs · Shift+Enter new line · Up/Down history", _console.StatusRight);
        }

        [Fact]
        public void AtAPrompt_TheInputIsGreyedWithItsReason_StopIsLive_AndTheStatusNamesThePrompt()
        {
            Assert.True(_console.DropFile(@"C:\scripts\circles.py"));
            _host.Start("PYRUN", _session.Document);
            var changed = new List<string>();
            _console.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            // The script asks: the host starts its carrier on the session, which waits at "Specify base point:".
            Assert.True(_session.Start(Question()));

            Assert.True(_console.IsAtPrompt);
            Assert.False(_console.IsInputEnabled);
            Assert.True(_console.CanStop);
            Assert.Equal("Answer on the command line, or press Esc", _console.InputPlaceholder);
            Assert.Equal("circles.py waits at a prompt: Specify base point", _console.StatusLeft);
            Assert.Equal("Esc or Stop cancels", _console.StatusRight);
            Assert.Equal(PythonStatusKind.Busy, _console.StatusKind);
            Assert.Contains(nameof(PythonConsoleViewModel.StatusLeft), changed);
            Assert.Contains(nameof(PythonConsoleViewModel.IsAtPrompt), changed);
        }

        [Fact]
        public void StopAtAPrompt_PressesEscOnTheSession_SoTheQuestionIsCancelled()
        {
            _host.Start("PYRUN", _session.Document);
            Assert.True(_session.Start(Question()));

            _console.Stop();

            Assert.False(_session.IsRunning);
            Assert.Contains(_session.History, line => line.EndsWith("*Cancel*", StringComparison.Ordinal));
        }

        [Fact]
        public void StopWhilePythonComputes_CallsTheHostsStopOnce_AndPressesNoEscOnTheSession()
        {
            // The session is at the Command prompt while a script computes: no question is waiting there.
            _host.Start("PYRUN", _session.Document);

            _console.Stop();

            Assert.Equal(1, _host.Stops);
            Assert.DoesNotContain(_session.History, line => line.EndsWith("*Cancel*", StringComparison.Ordinal));
        }

        [Fact]
        public void StopWithNoRunGoingOn_DoesNothing()
        {
            _console.Stop();

            Assert.Equal(0, _host.Stops);
            Assert.Empty(_session.History);
        }

        [Fact]
        public void TheStopButton_IsTheStopCommand()
        {
            _host.Start("PYRUN", _session.Document);

            Assert.True(_console.StopCommand.CanExecute(null));
            _console.StopCommand.Execute(null);

            Assert.Equal(1, _host.Stops);
        }

        [Fact]
        public void TheRunFileAndLoadButtons_StartPyrunAndPyloadOnTheDrawingShown()
        {
            _console.RunFileCommand.Execute(null);
            _console.LoadCommand.Execute(null);

            Assert.Equal(new[] { "PYRUN", "PYLOAD" }, _commands);
        }

        [Fact]
        public void AFileThatIsNotAPyFile_OrADropWhileARunGoesOn_IsNotRun()
        {
            Assert.False(_console.DropFile(@"C:\scripts\notes.txt"));
            Assert.False(_console.DropFile(@"C:\scripts\noextension"));
            _host.Start("PYRUN", _session.Document);
            Assert.False(_console.DropFile(@"C:\scripts\circles.py"));

            Assert.Empty(_host.Files);
        }

        [Fact]
        public void TheCloseButton_HidesThePalette_AndPythonShowsItAgain()
        {
            _console.IsShown = true;

            _console.CloseCommand.Execute(null);

            Assert.False(_console.IsShown);
        }

        // ---- No Python

        [Fact]
        public void WithNoPython_ThePaletteShowsTheProblemWordForWord_AndOnlyClearWorks()
        {
            _host.Problem = NoPython;
            _host.Executable = null;
            _host.Version = null;
            Enter("a = 1");

            Assert.False(_console.IsUsable);
            Assert.Equal(NoPython, _console.Problem);
            Assert.Equal("Python is not available", _console.BannerTitle);
            Assert.False(_console.IsInputEnabled);
            Assert.False(_console.CanRunFile);
            Assert.False(_console.CanReset);
            Assert.False(_console.CanStop);
            Assert.Equal("No Python", _console.InputPlaceholder);
            Assert.Equal(PythonStatusKind.Problem, _console.StatusKind);
            Assert.Equal("No Python found", _console.StatusLeft);
            Assert.True(_console.ShowsSettingsLink);
            Assert.False(_console.DropFile(@"C:\scripts\circles.py"));
            Assert.Empty(_host.Entered);
            Assert.Empty(_host.Files);
        }

        [Fact]
        public void WithNoPython_TheSettingsLinkAndTheBannerButton_OpenGlobalSettingsOnce()
        {
            _host.Problem = NoPython;

            _console.SettingsCommand.Execute(null);

            Assert.Equal(1, _settingsOpened);
        }

        [Fact]
        public void WithNoPython_ClearStillEmptiesTheTranscript()
        {
            Enter("a = 1");
            _host.Problem = NoPython;

            _console.Clear();

            Assert.Empty(_console.Lines);
        }

        [Theory]
        [InlineData(@"The Python set in Global settings (D:\nowhere\python.exe) was not found.")]
        [InlineData(@"D:\py310\python.exe is Python 3.10: GeometryHelper CAD needs 3.11 to 3.15.")]
        [InlineData(@"D:\py32\python.exe is a 32-bit Python: GeometryHelper CAD needs a 64-bit one.")]
        public void EachProblemTextOfTheEnvironment_IsTheBannersTextWordForWord(string problem)
        {
            _host.Problem = problem;

            Assert.Equal(problem, _console.Problem);
            Assert.False(_console.IsUsable);
        }

        [Fact]
        public void AnEntryThatMakesPythonFailToStart_TurnsThePaletteToItsNoPythonState_WithTheNewProblem()
        {
            // The engine starts at the first entry (D713): the host cannot start it, refuses the entry and from then on
            // reports "Python could not start: ...".
            const string failed =
                "Python could not start: No module named 'geometryhelper'. Fix Global settings > Python and restart GeometryHelper CAD.";
            _host.OnEnter = text =>
            {
                _host.Problem = failed;
                _host.PrintError(failed);
                return CadPythonEntry.Refused;
            };
            var changed = new List<string>();
            _console.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            Enter("a = 1");

            Assert.False(_console.IsUsable);
            Assert.Equal(failed, _console.Problem);
            Assert.False(_console.IsInputEnabled);
            Assert.Contains(nameof(PythonConsoleViewModel.IsUsable), changed);
            Assert.Contains(nameof(PythonConsoleViewModel.Problem), changed);
        }

        // ---- What the window is told

        [Fact]
        public void FollowingAnotherDrawing_TellsTheWindowThatTheLinesAndTheNameChanged()
        {
            CadCommandSession other = Drawing("other.dxf");
            var changed = new List<string>();
            _console.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            _console.Follow(other.Document, other);

            Assert.Contains(nameof(PythonConsoleViewModel.Lines), changed);
            Assert.Contains(nameof(PythonConsoleViewModel.DocumentName), changed);
            Assert.Contains(nameof(PythonConsoleViewModel.InputText), changed);
        }
    }
}
