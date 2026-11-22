using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The first MLEADER of a drawing with no multileader style, committed with its words on the Annotate tab, whose style
    /// boxes were pushed null by their own reset without end (a stack overflow that killed the app), and the other boxes of the
    /// ribbon that must answer that push with nothing.
    /// </summary>
    public sealed class MLeaderCommitCrashTests
    {
        /// <summary>Opens the room, which has no multileader style, in a view model.</summary>
        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            DxfDatabase db = TestFiles.Room(out _, out _);
            db.Clayer = "0";
            Assert.True(model.Open(folder.Write("room.dxf", db)));
            return model;
        }

        /// <summary>The model after one MLEADER, which has made the style Standard.</summary>
        private static MainViewModel OpenWithOneMLeader(TestFolder folder)
        {
            MainViewModel model = Open(folder);
            model.Execute("MLEADER");
            model.Execute("100,100");
            model.Execute("500,500");
            model.Execute("NOTE");
            Assert.Equal("Standard", model.CurrentMLeaderStyle);
            return model;
        }

        /// <summary>
        /// Plays what a ComboBox does when its list is replaced: it clears its selection, which pushes a value into the model.
        /// The play stops at 40 deep, so that a loop ends in a count and not in a stack overflow.
        /// </summary>
        private static int RaisedWhileTheBoxIsReset(MainViewModel model, string list, Action<MainViewModel> push)
        {
            int raised = 0;
            int depth = 0;
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != list)
                {
                    return;
                }

                raised++;
                depth++;

                if (depth < 40)
                {
                    push(model);
                }

                depth--;
            };
            push(model);
            return raised;
        }

        [Fact]
        public void TheMultileaderStyleBox_PushedNullByItsOwnReset_RaisesNothingAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = OpenWithOneMLeader(folder);

                int raised = RaisedWhileTheBoxIsReset(model, nameof(MainViewModel.MLeaderStyleItems), m =>
                    m.CurrentMLeaderStyle = null
                );

                Assert.Equal(0, raised);
            }
        }

        [Fact]
        public void TheMultileaderStyleBox_PushedTheStyleItAlreadyShows_RaisesNothingAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = OpenWithOneMLeader(folder);

                int raised = RaisedWhileTheBoxIsReset(model, nameof(MainViewModel.MLeaderStyleItems), m =>
                    m.CurrentMLeaderStyle = "Standard"
                );

                Assert.Equal(0, raised);
            }
        }

        [Fact]
        public void TheMultileaderStyleBox_AskedForTheManager_StillRaisesTheListOnce()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = OpenWithOneMLeader(folder);
                int raised = 0;
                var asked = new List<string>();
                model.StyleDialogRequested += (s, name) => asked.Add(name);
                model.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.MLeaderStyleItems))
                    {
                        raised++;
                    }
                };

                model.CurrentMLeaderStyle = MainViewModel.ManageMLeaderStyles;

                Assert.Equal(new[] { "MLEADERSTYLE" }, asked);
                Assert.Equal(1, raised);
            }
        }

        [Fact]
        public void EveryOtherBoxOfTheRibbon_PushedNullByItsOwnReset_AnswersWithNoChange()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var raised = new List<string>();
                model.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

                model.CurrentLayer = null;
                model.CurrentTextStyle = null;
                model.CurrentDimStyle = null;
                model.CurrentColor = null;
                model.CurrentLinetype = null;
                model.CurrentImageFrame = null;

                Assert.Empty(raised);
            }
        }

        [Fact]
        public void TheFirstMLeaderOfADrawingOnTheAnnotateTab_CommittedWithEnter_LeavesTheWindowStanding()
        {
            Exception failure = null;
            int deepest = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;

                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Open(folder);
                        var workspace = new CadWorkspace { DataContext = model };
                        var source = new HwndSource(
                            new HwndSourceParameters("MLeaderCommitCrashTests")
                            {
                                ParentWindow = new IntPtr(-3),
                                WindowStyle = 0,
                                Width = 1600,
                                Height = 960,
                            }
                        )
                        {
                            RootVisual = workspace,
                        };

                        try
                        {
                            model.PropertyChanged += (s, e) => deepest = Math.Max(deepest, new StackTrace().FrameCount);
                            model.SelectedTab = model.AnnotateTab;
                            Flush(workspace);
                            model.StartCommand("MLEADER");
                            model.Execute("1000,1000");
                            model.Execute("1500,1400");
                            Flush(workspace);

                            foreach (char letter in "NOTE")
                            {
                                model.CommandText += letter;
                                Flush(workspace);
                            }

                            var box = (UIElement)workspace.FindName("CommandBox");
                            Keyboard.Focus(box);
                            box.RaiseEvent(
                                new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
                                {
                                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                                }
                            );
                            Flush(workspace);

                            Assert.Single(model.Document.ModelSpace.Block.Cast<DxfEntity>().OfType<DxfMLeader>());
                            Assert.Equal("Standard", model.CurrentMLeaderStyle);
                        }
                        finally
                        {
                            source.Dispose();
                            workspace.DrawingCanvas.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The run did not finish in 120 s.");

            Assert.Null(failure);
            Assert.InRange(deepest, 1, 600);
        }

        private static void Flush(CadWorkspace workspace)
        {
            workspace.Measure(new Size(1600, 960));
            workspace.Arrange(new Rect(0, 0, 1600, 960));
            workspace.UpdateLayout();
            workspace.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
    }
}
