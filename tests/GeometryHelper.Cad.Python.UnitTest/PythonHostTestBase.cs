using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// What every test of the host does the same way: a drawing and its session with the Python commands in the registry, a
    /// folder to write scripts in, the events of the host gathered, and a run played to its end on the thread of the test
    /// (the owner of the tests posts and answers, D704). A test that asked something it gave no answer for, or gave an
    /// answer nothing asked for, fails in <see cref="Run"/>.
    /// </summary>
    [Collection(PythonHostCollection.Name)]
    public abstract class PythonHostTestBase : IDisposable
    {
        private readonly EventHandler<CadPythonOutputEventArgs> _onOutput;
        private readonly EventHandler<CadPythonRunEventArgs> _onStarted;
        private readonly EventHandler<CadPythonRunEventArgs> _onFinished;
        private readonly List<CadPythonOutputEventArgs> _output = new List<CadPythonOutputEventArgs>();
        private readonly List<CadPythonRunEventArgs> _started = new List<CadPythonRunEventArgs>();
        private readonly List<CadPythonRunEventArgs> _finished = new List<CadPythonRunEventArgs>();
        private int _scripts;

        protected PythonHostTestBase(PythonHostFixture fixture)
        {
            Host = fixture.Host;
            Owner = fixture.Owner;
            Folder = Path.Combine(Path.GetTempPath(), "gh-python-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
            Owner.Reset();
            _onOutput = (sender, e) => _output.Add(e);
            _onStarted = (sender, e) => _started.Add(e);
            _onFinished = (sender, e) => _finished.Add(e);
            Host.Output += _onOutput;
            Host.RunStarted += _onStarted;
            Host.RunFinished += _onFinished;
        }

        public void Dispose()
        {
            Host.Output -= _onOutput;
            Host.RunStarted -= _onStarted;
            Host.RunFinished -= _onFinished;
            Host.LoadCheck = null;
            bool running = Host.IsRunning;

            try
            {
                Directory.Delete(Folder, true);
            }
            catch (IOException) { }

            Assert.False(running);
        }

        protected CadPythonHost Host { get; }

        internal TestPythonOwner Owner { get; }

        /// <summary>A folder of this test, removed with it.</summary>
        protected string Folder { get; }

        protected IReadOnlyList<CadPythonOutputEventArgs> Output => _output;

        protected IReadOnlyList<CadPythonRunEventArgs> Started => _started;

        protected IReadOnlyList<CadPythonRunEventArgs> Finished => _finished;

        /// <summary>The lines the scripts printed, in order: what the palette shows apart from the errors.</summary>
        protected List<string> Printed => _output.Where(line => !line.IsError).Select(line => line.Text).ToList();

        /// <summary>The lines the palette shows as errors: stderr and tracebacks.</summary>
        protected List<string> Errors => _output.Where(line => line.IsError).Select(line => line.Text).ToList();

        /// <summary>A drawing called <paramref name="name"/> with a session whose registry has PYRUN, PYLOAD and the commands loaded so far.</summary>
        /// <remarks>FILEDIA is 0, so that PYRUN and PYLOAD ask the file on the command line and no dialog opens.</remarks>
        protected CadCommandSession NewSession(string name = "blank.dxf")
        {
            var session = new CadCommandSession(Blank(name));
            session.Remembered["filedia"] = 0.0;
            Host.AddCommandsTo(session.Registry);
            return session;
        }

        /// <summary>Writes a script of this test's folder and gives its full path.</summary>
        protected string Script(string name, params string[] lines) => WriteFile(Folder, name, Lines(lines));

        /// <summary>Writes the lines as a script of a name of its own and runs it with PYRUN's own call.</summary>
        protected CadPythonRunEventArgs RunLines(
            CadCommandSession session,
            string[] lines,
            params Action<CadCommandSession>[] answers
        ) =>
            Run(
                session,
                Script("script" + (++_scripts).ToString(CultureInfo.InvariantCulture) + ".py", lines),
                answers
            );

        /// <summary>Runs a file as PYRUN does, plays the answers at its prompts, and gives the end of the run.</summary>
        protected CadPythonRunEventArgs Run(
            CadCommandSession session,
            string path,
            params Action<CadCommandSession>[] answers
        )
        {
            Owner.Session = session;
            Owner.Answer(answers);
            int before = _finished.Count;
            Host.RunFile(session, path);
            Owner.Drain();
            return Verified(before);
        }

        /// <summary>Loads a file as PYLOAD does, plays the answers, and gives the end of the run.</summary>
        protected CadPythonRunEventArgs Load(
            CadCommandSession session,
            string path,
            params Action<CadCommandSession>[] answers
        )
        {
            Owner.Session = session;
            Owner.Answer(answers);
            int before = _finished.Count;
            Host.LoadFile(session, path);
            Owner.Drain();
            return Verified(before);
        }

        /// <summary>A palette entry: the host's answer, the run it posted played to its end.</summary>
        protected CadPythonEntry Enter(
            CadCommandSession session,
            string text,
            params Action<CadCommandSession>[] answers
        )
        {
            Owner.Session = session;
            Owner.Answer(answers);
            CadPythonEntry entry = Host.Enter(session, text);
            Owner.Drain();
            Assert.Empty(Owner.Unanswered);
            Assert.Equal(0, Owner.PendingAnswers);
            return entry;
        }

        /// <summary>Plays the posted work and checks that every prompt was answered and every answer was asked for.</summary>
        private CadPythonRunEventArgs Verified(int finishedBefore)
        {
            Assert.Empty(Owner.Unanswered);
            Assert.Equal(0, Owner.PendingAnswers);
            Assert.True(_finished.Count > finishedBefore, "The run did not finish.");
            return _finished.Last();
        }

        /// <summary>Runs <paramref name="body"/> with the thread's culture set, as a user whose Windows is set to it would run it.</summary>
        protected static void InCulture(string name, Action body)
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(name);

            try
            {
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }
    }
}
