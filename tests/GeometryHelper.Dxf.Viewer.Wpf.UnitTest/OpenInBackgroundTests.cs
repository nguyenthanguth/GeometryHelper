using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A drawing opened in the background, as the app's window opens one: read and drawn on a worker thread, shown only once
    /// drawn, a command typed meanwhile not run, and Esc stopping it. Each runs on an STA thread whose dispatcher is the
    /// window's.
    /// </summary>
    public class OpenInBackgroundTests
    {
        [Fact]
        public void AFileOpenedInTheBackground_IsShownOnlyOnceDrawn_AndACommandTypedMeanwhileIsNotRun()
        {
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                    string other = folder.Write("other.dxf", TestFiles.Room(out _, out _));
                    var model = new MainViewModel();
                    Assert.True(model.Open(other));

                    Task<bool> opened = model.OpenAsync(room);

                    // Nothing is shown until the window's thread is free to show it.
                    Assert.False(opened.IsCompleted);
                    Assert.True(model.IsOpening);
                    Assert.Equal("other.dxf", model.Title);
                    Assert.Equal("Opening room.dxf: reading the file", model.OpeningText);
                    Assert.True(model.CancelOpeningCommand.CanExecute(null));

                    // Typed or started, a command waits for no drawing half drawn: it is not run, and says so.
                    model.Execute("LINE");
                    model.StartCommand("CIRCLE");

                    Assert.Equal(
                        "LINE not run: room.dxf is still opening. Run it again once it is shown, or press Esc to stop opening it.",
                        model.History[model.History.Count - 2]
                    );
                    Assert.StartsWith("CIRCLE not run: room.dxf is still opening.", model.History.Last());
                    Assert.False(model.Session.IsRunning);

                    Assert.True(Wait(opened));

                    Assert.False(model.IsOpening);
                    Assert.Equal(string.Empty, model.OpeningText);
                    Assert.False(model.CancelOpeningCommand.CanExecute(null));
                    Assert.Equal("room.dxf", model.Title);
                    Assert.Equal(
                        "Opened room.dxf: 3 entities, " + model.Layers.Count + " layers.",
                        model.History.Last()
                    );

                    // Shown, the drawing takes commands again.
                    model.Execute("LINE");
                    Assert.True(model.Session.IsRunning);
                }
            });
        }

        [Fact]
        public void EscWhileAFileOpensInTheBackground_StopsItAndLeavesTheDrawingShown()
        {
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                    string other = folder.Write("other.dxf", TestFiles.Room(out _, out _));
                    var model = new MainViewModel();
                    Assert.True(model.Open(other));

                    Task<bool> opened = model.OpenAsync(room);
                    model.Escape();

                    Assert.False(Wait(opened));
                    Assert.False(model.IsOpening);
                    Assert.Equal("other.dxf", model.Title);
                    Assert.Equal("Opening room.dxf stopped.", model.History.Last());

                    // Opened again, it opens.
                    Assert.True(Wait(model.OpenAsync(room)));
                    Assert.Equal("room.dxf", model.Title);
                }
            });
        }

        [Fact]
        public void OpenWithOpensInBackgroundSet_StartsTheOpeningAndReturns()
        {
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                    string other = folder.Write("other.dxf", TestFiles.Room(out _, out _));
                    var model = new MainViewModel { OpensInBackground = true };

                    Assert.True(model.Open(room));
                    Assert.True(model.IsOpening);
                    Assert.Null(model.Document);

                    // One drawing opens at a time; the same one asked again joins it.
                    Assert.False(model.Open(other));
                    Assert.Equal("Cannot open other.dxf yet: room.dxf is still opening.", model.History.Last());
                    Task<bool> same = model.OpenAsync(room);

                    Assert.True(Wait(same));
                    Assert.Equal("room.dxf", model.Title);
                }
            });
        }

        /// <summary>Runs a test on an STA thread, as the window's.</summary>
        private static void OnWindowThread(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Lets the window's dispatcher run until an opening ends, a minute at most, and gives what it gave.</summary>
        private static bool Wait(Task<bool> opened)
        {
            var frame = new DispatcherFrame();
            opened.ContinueWith(_ => frame.Continue = false, TaskContinuationOptions.ExecuteSynchronously);
            var timeout = new DispatcherTimer(
                TimeSpan.FromMinutes(1),
                DispatcherPriority.Normal,
                (s, e) => frame.Continue = false,
                Dispatcher.CurrentDispatcher
            );
            Dispatcher.PushFrame(frame);
            timeout.Stop();
            Assert.True(opened.IsCompleted, "The opening did not end within a minute.");
            return opened.Result;
        }
    }
}
