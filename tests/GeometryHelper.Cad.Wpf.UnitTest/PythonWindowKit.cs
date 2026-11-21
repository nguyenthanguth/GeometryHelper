using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>What the tests of the PYTHON palette's window parts share: a thread with a dispatcher, as the window's, and a way to let it work.</summary>
    internal static class PythonWindowKit
    {
        /// <summary>The longest any wait of these tests lasts, in seconds: far above the under one second the slowest of them takes, so only a hang reaches it.</summary>
        public const int GiveUpSeconds = 30;

        /// <summary>Runs a test on an STA thread, as the window's, and ends its dispatcher after it; what the test threw is thrown again here.</summary>
        public static void OnWindowThread(Action test)
        {
            ExceptionDispatchInfo failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    // The pack:// scheme the theme's dictionaries are read by, registered before a control is made.
                    _ = PackUriHelper.UriSchemePack;
                    test();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            failure?.Throw();
        }

        /// <summary>The element in a window of messages, never shown, which gives it a keyboard to hold focus; laid out at the size given.</summary>
        public static HwndSource Host(FrameworkElement root, double width = 1600, double height = 960)
        {
            var source = new HwndSource(
                new HwndSourceParameters("PythonPaletteTests")
                {
                    ParentWindow = new IntPtr(-3),
                    WindowStyle = 0,
                    Width = (int)width,
                    Height = (int)height,
                }
            )
            {
                RootVisual = root,
            };
            Layout(root, width, height);
            return source;
        }

        /// <summary>Measures and arranges an element at a size, and lets its layout settle.</summary>
        public static void Layout(FrameworkElement element, double width = 1600, double height = 960)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
        }

        /// <summary>Every element below a parent in the visual tree, parents before children.</summary>
        public static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        /// <summary>
        /// Lets the dispatcher of this thread work until <paramref name="until"/> is true, and fails after
        /// <see cref="GiveUpSeconds"/> with <paramref name="what"/>. The check runs at ApplicationIdle, under the Background
        /// priority the owner posts at, so that work posted at either runs before the check sees it.
        /// </summary>
        public static void Pump(Func<bool> until, string what)
        {
            var frame = new DispatcherFrame();
            DateTime end = DateTime.UtcNow.AddSeconds(GiveUpSeconds);
            var check = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(5),
            };
            check.Tick += (s, e) =>
            {
                if (until() || DateTime.UtcNow > end)
                {
                    frame.Continue = false;
                }
            };
            check.Start();
            Dispatcher.PushFrame(frame);
            check.Stop();
            Assert.True(until(), what + " did not happen within " + GiveUpSeconds + " seconds.");
        }

        /// <summary>
        /// Ends the dispatcher of this thread after <see cref="GiveUpSeconds"/> unless disposed first, so that a wait that
        /// never ends fails its test instead of hanging the run; <see cref="TimedOut"/> says the watchdog ended it.
        /// </summary>
        public sealed class Watchdog : IDisposable
        {
            private readonly Timer _timer;

            public Watchdog()
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                _timer = new Timer(
                    _ =>
                    {
                        TimedOut = true;
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    },
                    null,
                    GiveUpSeconds * 1000,
                    Timeout.Infinite
                );
            }

            /// <summary>Gets whether the watchdog ended the dispatcher.</summary>
            public volatile bool TimedOut;

            public void Dispose() => _timer.Dispose();
        }
    }
}
