using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The window's owner of Python runs (D704): <c>Post</c> queues work on the window's dispatcher below what the user did, and
    /// <c>WaitUntil</c> lets the window work, in a frame of the dispatcher, until the script's question is answered. Each test
    /// runs on an STA thread whose dispatcher is the owner's. No Python is started: these are the owner's rules alone; the runs
    /// through a real host are in <see cref="CadPythonWindowOwnerRunTests"/>.
    /// </summary>
    public sealed class CadPythonWindowOwnerTests
    {
        [Fact]
        public void PostedWork_DoesNotRunInline_AndRunsInTheOrderPosted_OnceTheDispatcherWorks()
        {
            OnWindowThread(() =>
            {
                var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                var ran = new List<int>();

                owner.Post(() => ran.Add(1));
                owner.Post(() => ran.Add(2));
                owner.Post(() => ran.Add(3));

                // Post has returned and nothing has run: the dispatcher has not been given the thread yet.
                Assert.Empty(ran);

                Pump(() => ran.Count == 3, "the three posted works");
                Assert.Equal(new[] { 1, 2, 3 }, ran);
            });
        }

        [Fact]
        public void PostedWork_RunsAfterWhatTheUserDidFirst_BecauseItIsPostedAtBackgroundPriority()
        {
            OnWindowThread(() =>
            {
                var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                var ran = new List<string>();

                // The script is posted first, then a click handled at Normal and an input event (Input, 5 above Background 4): both
                // run before the script, Normal before Input, so the command line shows the end of PYRUN first (D704).
                owner.Post(() => ran.Add("script"));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => ran.Add("click")), DispatcherPriority.Normal);
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => ran.Add("key")), DispatcherPriority.Input);

                Pump(() => ran.Count == 3, "the three works");
                Assert.Equal(new[] { "click", "key", "script" }, ran);
            });
        }

        [Fact]
        public void WaitUntil_Returns_WhenDoneTurnsTrueFromAPostedCallback()
        {
            OnWindowThread(() =>
            {
                using (var watchdog = new Watchdog())
                {
                    var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                    var answered = false;

                    // What the user does while the script waits: a callback posted by the owner's own Post.
                    owner.Post(() => answered = true);

                    owner.WaitUntil(() => answered);

                    Assert.False(watchdog.TimedOut);
                    Assert.True(answered);
                }
            });
        }

        [Fact]
        public void WaitUntil_KeepsWorkingTheDispatcher_UntilTheLastOfSeveralCallbacksHasRun()
        {
            OnWindowThread(() =>
            {
                using (var watchdog = new Watchdog())
                {
                    var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                    var typed = new List<char>();

                    // Three keystrokes of an answer, each its own callback: the wait leaves only when the third has run.
                    foreach (char key in "abc")
                    {
                        owner.Post(() => typed.Add(key));
                    }

                    owner.WaitUntil(() => typed.Count == 3);

                    Assert.False(watchdog.TimedOut);
                    Assert.Equal(new[] { 'a', 'b', 'c' }, typed);
                }
            });
        }

        [Fact]
        public void WaitUntil_WhenDoneIsTrueAlready_ReturnsAtOnce_WithNothingPosted()
        {
            OnWindowThread(() =>
            {
                using (var watchdog = new Watchdog())
                {
                    var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);

                    owner.WaitUntil(() => true);

                    // No callback exists to turn anything true: a wait that needs one to come would sit here until the watchdog.
                    Assert.False(watchdog.TimedOut);
                }
            });
        }

        [Fact]
        public void WaitUntil_Returns_WhenTheDispatcherShutsDownDuringTheWait()
        {
            OnWindowThread(() =>
            {
                using (var watchdog = new Watchdog())
                {
                    var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                    var shutDown = false;

                    // The window is closed while a script waits: the callback ends the dispatcher, done never turns true.
                    owner.Post(() =>
                    {
                        shutDown = true;
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                    });

                    owner.WaitUntil(() => false);

                    Assert.False(watchdog.TimedOut);
                    Assert.True(shutDown);
                }
            });
        }

        [Fact]
        public void WaitUntil_AfterTheDispatcherHasShutDown_ReturnsAtOnce_AndDoesNotThrow()
        {
            OnWindowThread(() =>
            {
                var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                Assert.True(Dispatcher.CurrentDispatcher.HasShutdownStarted);

                // A script still unwinding as the window closes asks once more: the frame of a dispatcher that has shut down
                // cannot be pushed, so the owner must not try (as DclWindowHost.Next does not).
                Assert.Null(Record.Exception(() => owner.WaitUntil(() => false)));
            });
        }

        [Fact]
        public void Post_OfWorkThatPostsMore_RunsTheLaterWorkAfterTheEarlier()
        {
            OnWindowThread(() =>
            {
                var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                var ran = new List<string>();

                // The run PYRUN posts ends a command that posts the next run: the second begins only after the first ended.
                owner.Post(() =>
                {
                    ran.Add("first begins");
                    owner.Post(() => ran.Add("second"));
                    ran.Add("first ends");
                });

                Pump(() => ran.Count == 3, "both runs");
                Assert.Equal(new[] { "first begins", "first ends", "second" }, ran);
            });
        }

        [Fact]
        public void PostedWork_RunsOnTheWindowsThread_WhoeverPostsIt()
        {
            OnWindowThread(() =>
            {
                var owner = new CadPythonWindowOwner(Dispatcher.CurrentDispatcher);
                int window = Thread.CurrentThread.ManagedThreadId;
                int ranOn = -1;

                // Another thread posts (the remote server's, a timer): the work still runs on the window's.
                var poster = new Thread(() => owner.Post(() => ranOn = Thread.CurrentThread.ManagedThreadId));
                poster.Start();
                poster.Join();

                Pump(() => ranOn != -1, "the work posted from the other thread");
                Assert.Equal(window, ranOn);
            });
        }
    }
}
