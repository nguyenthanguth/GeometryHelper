using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The progress meter of the status bar, as AutoCAD's: OPEN and SAVE show theirs, and a command (through
    /// <see cref="CadCommandContext.Progress"/>), a plugin or the host starts its own on <see cref="Progress"/>.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadProgress _progress;
        private Dispatcher _progressDispatcher;
        private bool _isProgressShown;
        private string _progressLabel = string.Empty;
        private double _progressPercent;
        private bool _isProgressIndeterminate;
        private long _repainted;

        /// <summary>
        /// Gets the progress meter of the status bar, which every drawing's command line shares
        /// (<see cref="CadCommandSession.Progress"/>): a meter started on it is shown at once, in the place of any shown before,
        /// until it stops. It may be started and moved on any thread; the status bar follows on the window's.
        /// </summary>
        public CadProgress Progress
        {
            get
            {
                if (_progress == null)
                {
                    _progress = new CadProgress();
                    _progressDispatcher = Dispatcher.CurrentDispatcher;
                    _progress.Changed += OnProgressChanged;
                }

                return _progress;
            }
        }

        /// <summary>
        /// Gets or sets whether the status bar is repainted while a meter moves on the window's thread, as SAVE moves it, so
        /// that it is seen to move: ten times a second at most, the mouse and keys held back meanwhile. False until set, as a
        /// test or a screenshot needs; the app's window sets it.
        /// </summary>
        public bool RepaintsProgress { get; set; }

        /// <summary>Gets whether the status bar shows a meter.</summary>
        public bool IsProgressShown
        {
            get => _isProgressShown;
            private set => Set(ref _isProgressShown, value);
        }

        /// <summary>Gets what the meter shown says, as "Saving plan.dwg"; empty with none.</summary>
        public string ProgressLabel
        {
            get => _progressLabel;
            private set => Set(ref _progressLabel, value);
        }

        /// <summary>Gets the whole percent the meter shown has got to, 0 to 100; 0 with none or while it is indeterminate.</summary>
        public double ProgressPercent
        {
            get => _progressPercent;
            private set => Set(ref _progressPercent, value);
        }

        /// <summary>Gets whether the meter shown is indeterminate: a sweep with no percent.</summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            private set => Set(ref _isProgressIndeterminate, value);
        }

        /// <summary>Gets the percent the status bar writes beside the bar, as "40 %"; empty while indeterminate or with none.</summary>
        public string ProgressPercentText =>
            IsProgressShown && !IsProgressIndeterminate
                ? ((int)ProgressPercent).ToString(CultureInfo.InvariantCulture) + " %"
                : string.Empty;

        /// <summary>What is shown changed: followed at once on the window's thread, else posted to it.</summary>
        private void OnProgressChanged(object sender, EventArgs e)
        {
            Dispatcher dispatcher = _progressDispatcher;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(ShowProgress));
                return;
            }

            ShowProgress();
            Repaint();
        }

        private void ShowProgress()
        {
            CadProgressMeter meter = _progress.Shown;
            IsProgressShown = meter != null;
            ProgressLabel = meter?.Label ?? string.Empty;
            int percent = meter?.Percent ?? -1;
            IsProgressIndeterminate = meter != null && percent < 0;
            ProgressPercent = Math.Max(0, percent);
            Raise(nameof(ProgressPercentText));
        }

        /// <summary>
        /// Lets the window draw the status bar while work on its thread moves the meter, as AutoCAD's meter does during SAVE:
        /// what waits to be laid out and drawn is, the mouse and keys held back so that no command starts in the middle of the
        /// work. Not while a drawing opens, whose ending would then be shown in the middle of it.
        /// </summary>
        private void Repaint()
        {
            long now = Stopwatch.GetTimestamp();

            if (!RepaintsProgress || IsOpening || now - _repainted < Stopwatch.Frequency / 10)
            {
                return;
            }

            _repainted = now;
            InputManager.Current.PreProcessInput += HoldBack;

            try
            {
                _progressDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            }
            finally
            {
                InputManager.Current.PreProcessInput -= HoldBack;
            }
        }

        private static void HoldBack(object sender, PreProcessInputEventArgs e) => e.Cancel();
    }
}
