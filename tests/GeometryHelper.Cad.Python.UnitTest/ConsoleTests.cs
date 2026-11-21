using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The palette's console (D714): each drawing has its own globals, an entry is complete, incomplete or wrong as
    /// <c>python -i</c> reads it, a value is echoed, and an entry is refused while a command or another run is going on.
    /// </summary>
    public sealed class ConsoleTests : PythonHostTestBase
    {
        public ConsoleTests(PythonHostFixture fixture)
            : base(fixture) { }

        [Fact]
        public void AnAssignmentThenItsName_EchoesTheValue()
        {
            // The acceptance of D730: `x = 2 + 3`, then `x` echoes 5.
            CadCommandSession session = NewSession();

            CadPythonEntry first = Enter(session, "x = 2 + 3");
            CadPythonEntry second = Enter(session, "x");

            Assert.Equal(CadPythonEntry.Ran, first);
            Assert.Equal(CadPythonEntry.Ran, second);
            Assert.Equal(new[] { "5" }, Printed);
            Assert.Empty(Errors);
        }

        [Theory]
        [InlineData("def f():")]
        [InlineData("x = (1,")]
        [InlineData("if True:\n    x = 1\nelse:")]
        public void AnEntryThatIsNotFinished_IsIncomplete_AndNothingRuns(string text)
        {
            CadCommandSession session = NewSession();

            CadPythonEntry entry = Enter(session, text);

            Assert.Equal(CadPythonEntry.Incomplete, entry);
            Assert.Empty(Printed);
            Assert.Empty(Errors);
        }

        [Fact]
        public void ADefinitionOfSeveralLines_IsRunWhenComplete_AndCanBeCalled()
        {
            CadCommandSession session = NewSession();

            CadPythonEntry defined = Enter(session, "def f():\n    return 7\n");
            Enter(session, "f()");

            Assert.Equal(CadPythonEntry.Ran, defined);
            Assert.Equal(new[] { "7" }, Printed);
        }

        [Fact]
        public void ASyntaxError_IsWrittenAsAnError_AndTheConsoleGoesOn()
        {
            CadCommandSession session = NewSession();

            CadPythonEntry entry = Enter(session, "1/");
            Enter(session, "1 + 1");

            Assert.Equal(CadPythonEntry.Ran, entry);
            Assert.Contains(Errors, line => line.Contains("SyntaxError"));
            Assert.Equal(new[] { "2" }, Printed);
        }

        [Fact]
        public void ARuntimeError_IsOneLineOnTheCommandLineWithNoFilePart_AndATracebackInThePalette()
        {
            CadCommandSession session = NewSession();

            Enter(session, "1 / 0");

            Assert.Contains("Python error: ZeroDivisionError: division by zero", session.History);
            Assert.Contains("ZeroDivisionError: division by zero", Errors);
            Assert.DoesNotContain(Errors, line => line.Contains("_app.py") || line.Contains("<string>"));
        }

        [Fact]
        public void TheConsoleStartsWithItsOwnNameAndNothingImported()
        {
            CadCommandSession session = NewSession();

            Enter(session, "__name__");
            Enter(session, "'cad' in dir()");

            Assert.Equal(new[] { "'__console__'", "False" }, Printed);
        }

        [Fact]
        public void EachDrawingHasItsOwnGlobals()
        {
            // v is set in the first drawing's console; the second drawing's console has no v (research M7: names are apart,
            // sys.modules is shared).
            CadCommandSession first = NewSession("first.dxf");
            CadCommandSession second = NewSession("second.dxf");

            Enter(first, "v = 1");
            Enter(second, "v");
            Enter(first, "v");

            Assert.Contains(Errors, line => line.Contains("NameError"));
            Assert.Equal(new[] { "1" }, Printed);
        }

        [Fact]
        public void ResetConsole_DropsTheGlobalsOfThatDrawingOnly()
        {
            CadCommandSession first = NewSession("first.dxf");
            CadCommandSession second = NewSession("second.dxf");
            Enter(first, "v = 1");
            Enter(second, "v = 2");

            Host.ResetConsole(first.Document);
            Enter(first, "v");
            Enter(second, "v");

            Assert.Contains(Errors, line => line.Contains("NameError"));
            Assert.Equal(new[] { "2" }, Printed);
        }

        [Fact]
        public void AnEntryThatDraws_IsOneStepNamedPython()
        {
            CadCommandSession session = NewSession();

            Enter(session, "import geometryhelper.cad as cad");
            Enter(session, "cad.command('CIRCLE', (0, 0), 5)");

            CadDocument document = session.Document;
            Assert.Equal(1, document.EntityCount);
            Assert.Equal("Python", document.NextUndo.Name);
        }

        [Fact]
        public void AnEntryThatAsks_GetsItsAnswerFromTheUser()
        {
            CadCommandSession session = NewSession();
            Enter(session, "import geometryhelper.cad as cad");

            Enter(session, "cad.get_point('Where').X", Typed("12,3"));

            Assert.Equal(new[] { "12.0" }, Printed);
        }

        [Fact]
        public void AnEntryWhileACommandRuns_IsRefused_AndTheCommandKeepsAsking()
        {
            CadCommandSession session = NewSession();
            session.Feed("CIRCLE");

            CadPythonEntry entry = Enter(session, "1 + 1");

            Assert.Equal(CadPythonEntry.Refused, entry);
            Assert.Contains(Output, line => line.Text == "A command is running: finish it or press Esc first.");
            Assert.DoesNotContain("2", Printed);
            Assert.Equal("CIRCLE", session.Current.Name);
            session.Escape();
        }

        [Fact]
        public void AnEntryWhileARunGoesOnInAnotherDrawing_IsRefusedWithTheRunsText()
        {
            // The first drawing's script waits at a question; an entry typed in the second drawing's palette is refused at
            // once, naming the drawing that is running (D714, D705 step 1).
            CadCommandSession first = NewSession("first.dxf");
            CadCommandSession second = NewSession("second.dxf");
            CadPythonEntry? entry = null;

            RunLines(first, new[] { UsesCad, "cad.get_point('Where')" }, s =>
            {
                entry = Host.Enter(second, "2 + 2");
                s.Feed("1,1");
            });

            Assert.Equal(CadPythonEntry.Refused, entry);
            Assert.Contains(Output, line =>
                line.Text == "A Python script is running in first.dxf: wait for it or press Esc."
            );
        }
    }
}
