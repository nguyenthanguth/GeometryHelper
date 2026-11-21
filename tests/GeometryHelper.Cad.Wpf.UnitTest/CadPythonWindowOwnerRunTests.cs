using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using GeometryHelper.Cad.Python;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The window's owner under a real <see cref="CadPythonHost"/> (D703, D704, D707): scripts run on the thread of the window's
    /// dispatcher, a question waits in a frame of it and is answered by a callback posted to it, and Esc from another thread
    /// stops a script that computes. Python starts once per process, so the one host lives in <see cref="PythonWindowFixture"/>
    /// and no other test class of this assembly may start Python. The Python is the <c>python.exe</c> of the variable
    /// <c>GH_PYTHON</c>, else the one the finder of D713 gives; with none the fixture fails every test with the finder's text.
    /// </summary>
    [Collection(PythonWindowCollection.Name)]
    public sealed class CadPythonWindowOwnerRunTests : IDisposable
    {
        private readonly PythonWindowFixture _window;
        private readonly TestFolder _folder = new TestFolder();

        public CadPythonWindowOwnerRunTests(PythonWindowFixture window) => _window = window;

        public void Dispose() => _folder.Dispose();

        private string Script(string name, params string[] lines)
        {
            string path = Path.Combine(_folder.FullPath, name);
            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return path;
        }

        private static CadCommandSession Drawing() =>
            new CadCommandSession(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));

        [Fact]
        public void AFileRunByPyrun_BeginsOnlyAfterRunFileHasReturned_AndItsLineArrivesOnTheWindowsThread()
        {
            string path = Script("hello.py", "print('hello', 3 + 4)");

            _window.OnWindow(() =>
            {
                CadPythonHost host = _window.Host;
                int window = Thread.CurrentThread.ManagedThreadId;
                var lines = new List<string>();
                var threads = new List<int>();
                var finished = new List<CadPythonRunEventArgs>();
                EventHandler<CadPythonOutputEventArgs> output = (s, e) =>
                {
                    lines.Add(e.Text);
                    threads.Add(Thread.CurrentThread.ManagedThreadId);
                };
                EventHandler<CadPythonRunEventArgs> end = (s, e) => finished.Add(e);
                host.Output += output;
                host.RunFinished += end;

                try
                {
                    host.RunFile(Drawing(), path);

                    // RunFile has returned and the script has not run: it is posted, not called (D704).
                    Assert.Empty(lines);
                    Assert.Empty(finished);

                    Pump(() => finished.Count == 1, "the end of the run");

                    Assert.Equal(new[] { "hello 7" }, lines);
                    Assert.Equal(new[] { window }, threads);
                    Assert.Equal(CadPythonOutcome.Completed, finished[0].Outcome);
                }
                finally
                {
                    host.Output -= output;
                    host.RunFinished -= end;
                }
            });
        }

        [Fact]
        public void AScriptThatAsksForAString_IsAnsweredByACallbackPostedWhileItWaits_AndGoesOn()
        {
            string path = Script(
                "ask.py",
                "import geometryhelper.cad as cad",
                "name = cad.get_string('Name')",
                "print('got', name)"
            );

            _window.OnWindow(() =>
            {
                CadPythonHost host = _window.Host;
                CadCommandSession session = Drawing();
                var lines = new List<string>();
                var prompts = new List<string>();
                var finished = new List<CadPythonRunEventArgs>();
                EventHandler<CadPythonOutputEventArgs> output = (s, e) => lines.Add(e.Text);
                EventHandler<CadPythonRunEventArgs> end = (s, e) => finished.Add(e);
                host.Output += output;
                host.RunFinished += end;

                try
                {
                    host.RunFile(session, path);

                    // The user types after the script has asked: the answer is posted behind the run, so it runs inside the
                    // run's own wait, as a keystroke handled by the window does.
                    // (An exception of a posted callback would end the window's thread: what it saw is checked after.)
                    bool taken = false;
                    _window.Owner.Post(() =>
                    {
                        prompts.Add(session.PromptText);
                        taken = session.Feed("abc");
                    });

                    Pump(() => finished.Count == 1, "the end of the run");

                    Assert.True(taken);
                    Assert.Single(prompts);
                    Assert.StartsWith("Name", prompts[0], StringComparison.Ordinal);
                    Assert.Equal(new[] { "got abc" }, lines);
                    Assert.Equal(CadPythonOutcome.Completed, finished[0].Outcome);
                }
                finally
                {
                    host.Output -= output;
                    host.RunFinished -= end;
                }
            });
        }

        [Fact]
        public void EscPostedWhileAScriptWaits_EndsTheRunAsCancelled_AndTheLinesAfterTheQuestionNeverRun()
        {
            string path = Script(
                "esc.py",
                "import geometryhelper.cad as cad",
                "print('before')",
                "cad.get_string('Name')",
                "print('after')"
            );

            _window.OnWindow(() =>
            {
                CadPythonHost host = _window.Host;
                CadCommandSession session = Drawing();
                var lines = new List<string>();
                var finished = new List<CadPythonRunEventArgs>();
                EventHandler<CadPythonOutputEventArgs> output = (s, e) => lines.Add(e.Text);
                EventHandler<CadPythonRunEventArgs> end = (s, e) => finished.Add(e);
                host.Output += output;
                host.RunFinished += end;

                try
                {
                    host.RunFile(session, path);
                    _window.Owner.Post(session.Escape);

                    Pump(() => finished.Count == 1, "the end of the run");

                    Assert.Equal(CadPythonOutcome.Cancelled, finished[0].Outcome);
                    Assert.DoesNotContain("after", lines);
                    Assert.Equal("before", lines[0]);
                    Assert.Contains(session.History, line => line.EndsWith("*Cancel*", StringComparison.Ordinal));
                    Assert.False(session.IsRunning);
                }
                finally
                {
                    host.Output -= output;
                    host.RunFinished -= end;
                }
            });
        }

        [Fact]
        public void StopFromAnotherThread_EndsAScriptThatComputesOnTheWindowsThread_WithKeyboardInterrupt()
        {
            string marker = Path.Combine(_folder.FullPath, "looping");
            string path = Script("loop.py", "open(r'" + marker + "', 'w').close()", "while True:", "    pass");

            _window.OnWindow(() =>
            {
                CadPythonHost host = _window.Host;
                var errors = new List<string>();
                var finished = new List<CadPythonRunEventArgs>();
                EventHandler<CadPythonOutputEventArgs> output = (s, e) =>
                {
                    if (e.IsError)
                    {
                        errors.Add(e.Text);
                    }
                };
                EventHandler<CadPythonRunEventArgs> end = (s, e) => finished.Add(e);
                host.Output += output;
                host.RunFinished += end;

                // What the Esc watcher of the window does: press from a thread of its own, once the script is looping, and again
                // each 50 ms until the run has ended, since a press before the script computes does nothing.
                var presser = new Thread(() =>
                {
                    DateTime give = DateTime.UtcNow.AddSeconds(GiveUpSeconds);

                    while (!File.Exists(marker) && DateTime.UtcNow < give)
                    {
                        Thread.Sleep(5);
                    }

                    while (finished.Count == 0 && DateTime.UtcNow < give)
                    {
                        Thread.Sleep(50);
                        host.Stop();
                    }
                });

                try
                {
                    host.RunFile(Drawing(), path);
                    presser.Start();

                    Pump(() => finished.Count == 1, "the end of the run");
                    presser.Join();

                    Assert.Equal(CadPythonOutcome.Cancelled, finished[0].Outcome);
                    Assert.Contains(errors, line => line.Contains("KeyboardInterrupt"));
                }
                finally
                {
                    host.Output -= output;
                    host.RunFinished -= end;
                }
            });
        }

        [Fact]
        public void TwoFilesPosted_RunOneAfterTheOther_InTheOrderPosted()
        {
            string first = Script("one.py", "print('one')");
            string second = Script("two.py", "print('two')");

            _window.OnWindow(() =>
            {
                CadPythonHost host = _window.Host;
                CadCommandSession session = Drawing();
                var lines = new List<string>();
                var finished = new List<CadPythonRunEventArgs>();
                EventHandler<CadPythonOutputEventArgs> output = (s, e) => lines.Add(e.Text);
                EventHandler<CadPythonRunEventArgs> end = (s, e) => finished.Add(e);
                host.Output += output;
                host.RunFinished += end;

                try
                {
                    host.RunFile(session, first);
                    host.RunFile(session, second);

                    Pump(() => finished.Count == 2, "the end of both runs");

                    Assert.Equal(new[] { "one", "two" }, lines);
                    Assert.Equal(
                        new[] { CadPythonOutcome.Completed, CadPythonOutcome.Completed },
                        finished.Select(f => f.Outcome.Value)
                    );
                }
                finally
                {
                    host.Output -= output;
                    host.RunFinished -= end;
                }
            });
        }
    }

    /// <summary>
    /// The one Python of the process, started on the thread of a dispatcher of its own that lives as long as the collection: the
    /// host, its owner (<see cref="CadPythonWindowOwner"/>) and the thread are made together, and the host is disposed on that
    /// thread before the dispatcher ends, as the window's host is at exit (D719).
    /// </summary>
    public sealed class PythonWindowFixture : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim();
        private Exception _failure;

        public PythonWindowFixture()
        {
            _thread = new Thread(() =>
            {
                try
                {
                    CadPythonEnvironment environment = CadPythonEnvironment.Find(
                        Environment.GetEnvironmentVariable("GH_PYTHON") ?? string.Empty
                    );

                    if (!environment.IsUsable)
                    {
                        throw new InvalidOperationException(environment.Problem);
                    }

                    Dispatcher = Dispatcher.CurrentDispatcher;
                    Owner = new CadPythonWindowOwner(Dispatcher);
                    Host = new CadPythonHost(environment, Owner);
                }
                catch (Exception ex)
                {
                    _failure = ex;
                }
                finally
                {
                    _ready.Set();
                }

                if (_failure == null)
                {
                    Dispatcher.Run();
                }
            });
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _ready.Wait();

            if (_failure != null)
            {
                throw new InvalidOperationException(_failure.Message, _failure);
            }
        }

        public Dispatcher Dispatcher { get; private set; }

        internal CadPythonWindowOwner Owner { get; private set; }

        public CadPythonHost Host { get; private set; }

        /// <summary>Runs a test body on the window's thread, and throws here what it threw there.</summary>
        public void OnWindow(Action body)
        {
            Exception failure = null;
            Dispatcher.Invoke(() =>
            {
                try
                {
                    body();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        public void Dispose()
        {
            if (_failure != null)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                Assert.False(Host.IsRunning);
                Host.Dispose();
            });
            Dispatcher.InvokeShutdown();
            _thread.Join();
        }
    }

    /// <summary>The collection of the tests that run Python: it starts once per process.</summary>
    [CollectionDefinition(Name)]
    public sealed class PythonWindowCollection : ICollectionFixture<PythonWindowFixture>
    {
        public const string Name = "Python on the window thread";
    }
}
