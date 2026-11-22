using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Python;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Python.Runtime;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A script's handler of a .NET event that raises after its run has ended (P6, D926). pythonnet builds the delegate and the
    /// error reaches the window's thread as a <see cref="PythonException"/>: the crash log's handler of unhandled errors writes
    /// it as one line on the command line instead of showing a crash message. The handler itself is not driven here, as it
    /// needs a running application.
    /// </summary>
    [Collection(PythonWindowCollection.Name)]
    public sealed class CadCrashLogPythonTests : IDisposable
    {
        private readonly PythonWindowFixture _window;
        private readonly TestFolder _folder = new TestFolder();

        public CadCrashLogPythonTests(PythonWindowFixture window) => _window = window;

        public void Dispose() => _folder.Dispose();

        private string Script(string name, params string[] lines)
        {
            string path = Path.Combine(_folder.FullPath, name);
            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return path;
        }

        private void Run(CadCommandSession session, string path)
        {
            var finished = new List<CadPythonRunEventArgs>();
            EventHandler<CadPythonRunEventArgs> end = (s, e) => finished.Add(e);
            _window.Host.RunFinished += end;

            try
            {
                _window.Host.RunFile(session, path);
                Pump(() => finished.Count == 1, "the end of the run");
                Assert.True(finished[0].Outcome == CadPythonOutcome.Completed, path + ": " + finished[0].Error);
            }
            finally
            {
                _window.Host.RunFinished -= end;
            }
        }

        /// <summary>Hangs a handler that raises <c>RuntimeError(message)</c> on the drawing's Changed, edits the drawing, and gives what the dispatcher would have caught; the handler is taken off again.</summary>
        private Exception Caught(string message)
        {
            Exception caught = null;

            _window.OnWindow(() =>
            {
                CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");
                var session = new CadCommandSession(document);
                Run(
                    session,
                    Script(
                        "hang.py",
                        "import builtins",
                        "import geometryhelper.cad as cad",
                        "def handler(sender, args):",
                        "    raise RuntimeError(" + message + ")",
                        "builtins.p6_handler = handler",
                        "cad.session().Document.Changed += handler"
                    )
                );

                try
                {
                    var line = new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(100, 0, 0) };
                    document.ApplyEdit("draw", null, scope => scope.Append(document.ModelSpace, line));
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                Run(
                    session,
                    Script(
                        "off.py",
                        "import builtins",
                        "import geometryhelper.cad as cad",
                        "cad.session().Document.Changed -= builtins.p6_handler",
                        "del builtins.p6_handler"
                    )
                );
            });

            return caught;
        }

        [Fact]
        public void AHandlerThatRaisesAfterItsRun_IsWrittenAsOnePythonErrorLine_AndNotLeftToTheCrashMessage()
        {
            Exception caught = Caught("'handler boom'");
            var written = new List<string>();

            Assert.IsType<PythonException>(caught);
            bool reported = CadCrashLog.TryReportPython(caught, written.Add);

            Assert.True(reported);
            Assert.Equal(new[] { "Python error: RuntimeError: handler boom" }, written);
        }

        [Fact]
        public void AMessageOfSeveralLines_IsWrittenOnOneLine()
        {
            Exception caught = Caught("'first\\nsecond'");
            var written = new List<string>();

            CadCrashLog.TryReportPython(caught, written.Add);

            Assert.Equal(new[] { "Python error: RuntimeError: first second" }, written);
        }

        [Fact]
        public void AnExceptionThatIsNotFromPython_IsLeftToTheCrashHandler()
        {
            var written = new List<string>();

            bool reported = CadCrashLog.TryReportPython(new InvalidOperationException("not python"), written.Add);

            Assert.False(reported);
            Assert.Empty(written);
        }
    }
}
