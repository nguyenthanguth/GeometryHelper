using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The progress meter on the status bar, as AutoCAD's: a meter started by a command or the host shown with its label,
    /// bar and percent; SAVE and the opening of a drawing in the background shown on it; and gone once stopped. Each runs on
    /// an STA thread whose dispatcher is the window's.
    /// </summary>
    public class ProgressBarTests
    {
        [Fact]
        public void AMeterStarted_IsShownOnTheStatusBar_AndGoneOnceStopped()
        {
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                    var workspace = new CadWorkspace { DataContext = model };

                    try
                    {
                        var meter = (FrameworkElement)workspace.FindName("ProgressMeter");
                        var bar = (ProgressBar)workspace.FindName("ProgressMeterBar");
                        Layout(workspace);
                        Assert.False(model.IsProgressShown);
                        Assert.Equal(Visibility.Collapsed, meter.Visibility);

                        // Started with no limit: the label and a sweep, no percent.
                        CadProgressMeter checking = model.Progress.Start("Checking blocks");
                        Layout(workspace);
                        Assert.True(model.IsProgressShown);
                        Assert.Equal("Checking blocks", model.ProgressLabel);
                        Assert.True(model.IsProgressIndeterminate);
                        Assert.Equal(string.Empty, model.ProgressPercentText);
                        Assert.Equal(Visibility.Visible, meter.Visibility);
                        Assert.True(bar.IsIndeterminate);

                        checking.Report(0.405);
                        Layout(workspace);
                        Assert.False(bar.IsIndeterminate);
                        Assert.Equal(40.0, bar.Value);
                        Assert.Equal("40 %", model.ProgressPercentText);

                        // The session of the drawing shown has the same meter, for commands and plugins.
                        Assert.Same(model.Progress, model.Session.Progress);

                        checking.Stop();
                        Layout(workspace);
                        Assert.False(model.IsProgressShown);
                        Assert.Equal(Visibility.Collapsed, meter.Visibility);
                    }
                    finally
                    {
                        workspace.DrawingCanvas.Dispose();
                    }
                }
            });
        }

        [Fact]
        public void SavingADrawing_ShowsSavingOnTheMeter_AndStopsItAfter()
        {
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                    var shown = new List<string>();
                    model.Progress.Changed += (s, e) =>
                    {
                        CadProgressMeter meter = model.Progress.Shown;
                        shown.Add(meter == null ? "-" : meter.Label + " " + meter.Percent);
                    };

                    Assert.True(model.SaveAs(System.IO.Path.Combine(folder.FullPath, "copy.dwg")));

                    Assert.Equal("Saving copy.dwg -1", shown[0]);
                    Assert.Contains("Saving copy.dwg 100", shown);
                    Assert.Equal("-", shown[shown.Count - 1]);
                    Assert.False(model.Progress.IsBusy);
                    Assert.False(model.IsProgressShown);
                }
            });
        }

        [Fact]
        public void ADrawingOpenedInTheBackground_IsShownOnTheMeter_FromNoughtToAHundred()
        {
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                    var model = new MainViewModel();
                    var percents = new List<int>();
                    model.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MainViewModel.ProgressPercent) && model.IsProgressShown)
                        {
                            percents.Add((int)model.ProgressPercent);
                        }
                    };

                    Task<bool> opened = model.OpenAsync(room);

                    Assert.True(model.IsProgressShown);
                    Assert.Equal("Opening room.dxf", model.ProgressLabel);
                    Assert.True(Wait(opened));

                    Assert.False(model.IsProgressShown);
                    Assert.False(model.Progress.IsBusy);
                    Assert.NotEmpty(percents);
                    Assert.Equal(100, percents[percents.Count - 1]);

                    for (int i = 1; i < percents.Count; i++)
                    {
                        Assert.True(
                            percents[i] >= percents[i - 1],
                            "The meter went back: " + string.Join(", ", percents)
                        );
                    }
                }
            });
        }

        private static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(1600, 900));
            element.Arrange(new Rect(0, 0, 1600, 900));
            element.UpdateLayout();
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
