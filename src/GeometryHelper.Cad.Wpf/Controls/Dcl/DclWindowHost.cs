using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GeometryHelper.Cad.Dcl;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What shows the DCL dialogs of a window's routines (D329, D330, D334): <see cref="Open"/> makes a
    /// <see cref="DclDialogWindow"/> over the main window (or over the dialog it is a child of) and shows it without
    /// waiting, with the window under it switched off; <see cref="Next"/> shows the tiles' values again and pushes a dispatcher
    /// frame until the user does something; <see cref="Close"/> takes the window away and gives the window under it its input
    /// back. Every call comes on the UI thread, one at a time, as the interpreter's owner thread makes them.
    /// </summary>
    internal sealed class DclWindowHost : IDclHost
    {
        private readonly Func<Window> _main;
        private DclMetrics _metrics;
        private int _opened;
        private readonly Dictionary<DclDialog, Shown> _open = new Dictionary<DclDialog, Shown>();

        /// <summary>Makes a host.</summary>
        /// <param name="main">Gives the window the dialogs open over; it may give null, and a window not shown is not used.</param>
        internal DclWindowHost(Func<Window> main)
        {
            _main = main;
        }

        /// <summary>
        /// Gets the metrics the windows draw with (<see cref="DclDialogWindow.CreateMetrics"/>), so that <c>dimx_tile</c> of OK
        /// gives the 49 the window draws.
        /// </summary>
        public DclMetrics Metrics => _metrics ??= DclDialogWindow.CreateMetrics();

        /// <summary>Gets the windows of the dialogs open now, the oldest first.</summary>
        internal IEnumerable<DclDialogWindow> Windows =>
            _open.Values.OrderBy(shown => shown.Sequence).Select(shown => shown.Window);

        /// <summary>Gives the window of an open dialog; null where none is.</summary>
        /// <param name="dialog">The dialog.</param>
        /// <returns>Its window.</returns>
        internal DclDialogWindow WindowOf(DclDialog dialog) =>
            _open.TryGetValue(dialog, out Shown shown) ? shown.Window : null;

        /// <inheritdoc />
        public void Open(DclDialog dialog)
        {
            Window below =
                dialog.Parent != null && _open.TryGetValue(dialog.Parent, out Shown parent)
                    ? parent.Window
                    : _main?.Invoke();

            if (below != null && !below.IsVisible)
            {
                below = null;
            }

            var window = new DclDialogWindow(dialog);
            var shown = new Shown(window, below, below != null && below.IsEnabled) { Sequence = ++_opened };
            window.Raised += shown.Add;
            _open[dialog] = shown;

            if (below != null)
            {
                window.Owner = below;
            }

            Place(window, dialog, below);
            window.Refresh();

            // The window under the dialog takes no input while it is shown.
            if (below != null)
            {
                below.IsEnabled = false;
            }

            window.Show();
            window.Activate();
            window.Refresh();
        }

        /// <inheritdoc />
        public DclEvent Next(DclDialog dialog)
        {
            if (!_open.TryGetValue(dialog, out Shown shown))
            {
                return DclEvent.Escape();
            }

            shown.Window.Refresh();

            if (shown.Events.Count == 0 && !Dispatcher.CurrentDispatcher.HasShutdownStarted)
            {
                shown.Frame = new DispatcherFrame();
                Dispatcher.PushFrame(shown.Frame);
                shown.Frame = null;
            }

            if (shown.Events.Count == 0)
            {
                return DclEvent.Escape();
            }

            DclEvent next = shown.Events[0];
            shown.Events.RemoveAt(0);
            shown.Window.RememberPosition();
            return next;
        }

        /// <inheritdoc />
        public void Close(DclDialog dialog)
        {
            if (!_open.TryGetValue(dialog, out Shown shown))
            {
                return;
            }

            _open.Remove(dialog);
            shown.Window.Raised -= shown.Add;
            shown.Window.Release();

            if (shown.Below != null)
            {
                shown.Below.IsEnabled = shown.BelowWasEnabled;

                if (shown.Below.IsVisible)
                {
                    shown.Below.Activate();
                }
            }
        }

        // The dialog where it was asked for, or centred over the window below it, or over the screen.
        private static void Place(DclDialogWindow window, DclDialog dialog, Window below)
        {
            window.Chrome.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size size = window.Chrome.DesiredSize;
            DpiScale dpi = VisualTreeHelper.GetDpi(below ?? (Visual)window);
            Rect around =
                below != null && below.WindowState != WindowState.Maximized
                    ? new Rect(below.Left, below.Top, below.ActualWidth, below.ActualHeight)
                    : new Rect(
                        SystemParameters.WorkArea.Left,
                        SystemParameters.WorkArea.Top,
                        SystemParameters.WorkArea.Width,
                        SystemParameters.WorkArea.Height
                    );
            double left = around.Left + (around.Width - size.Width) / 2;
            double top = around.Top + (around.Height - size.Height) / 2;

            if (dialog.ScreenX.HasValue)
            {
                left = dialog.ScreenX.Value / dpi.DpiScaleX;
            }

            if (dialog.ScreenY.HasValue)
            {
                top = dialog.ScreenY.Value / dpi.DpiScaleY;
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
        }

        // One dialog being shown: its window, the one under it, what the user did that Next has not given yet.
        private sealed class Shown
        {
            internal Shown(DclDialogWindow window, Window below, bool belowWasEnabled)
            {
                Window = window;
                Below = below;
                BelowWasEnabled = belowWasEnabled;
            }

            internal DclDialogWindow Window { get; }

            internal Window Below { get; }

            internal bool BelowWasEnabled { get; }

            internal int Sequence { get; set; }

            internal List<DclEvent> Events { get; } = new List<DclEvent>();

            internal DispatcherFrame Frame { get; set; }

            // A slider dragged reports every step: only the last of a run of them for one slider is kept.
            internal void Add(DclEvent dclEvent)
            {
                int last = Events.Count - 1;

                if (
                    last >= 0
                    && dclEvent.Kind == DclEventKind.Tile
                    && dclEvent.Reason == 3
                    && Events[last].Kind == DclEventKind.Tile
                    && Events[last].Reason == 3
                    && string.Equals(Events[last].Key, dclEvent.Key, StringComparison.Ordinal)
                )
                {
                    Events[last] = dclEvent;
                }
                else
                {
                    Events.Add(dclEvent);
                }

                if (Frame != null)
                {
                    Frame.Continue = false;
                }
            }
        }
    }
}
