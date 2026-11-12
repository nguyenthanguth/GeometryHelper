using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The progress meter of the status bar, as AutoCAD's: OPEN, SAVE and SAVEAS start one, and a command, a plugin or the
    /// host starts its own with <see cref="Start"/> for any long work. The meter last started and not yet stopped is the one
    /// shown: one started while another runs, as a plugin's inside SAVE, is shown in its place, and the one under it comes
    /// back when it stops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Meters may be started, moved and stopped on any thread. <see cref="Changed"/> is raised on the thread that made the
    /// change, and only when what is shown changes: a meter started or stopped, or the meter shown moved by a whole percent,
    /// so that a drawing of 100 000 lines moves it at most 100 times however often it is told.
    /// </para>
    /// <code>
    /// using (CadProgressMeter meter = context.Progress.Start("Checking blocks"))
    /// {
    ///     meter.SetLimit(blocks.Count);
    ///     foreach (var block in blocks) { Check(block); meter.MeterProgress(); }
    /// }                                    // stopped, even where Check throws
    /// </code>
    /// </remarks>
    public sealed class CadProgress
    {
        private readonly object _gate = new object();
        private readonly List<CadProgressMeter> _running = new List<CadProgressMeter>();

        /// <summary>Raised, on the thread that made the change, when what the status bar shows of the meters changes.</summary>
        public event EventHandler Changed;

        /// <summary>Gets whether a meter is running.</summary>
        public bool IsBusy
        {
            get
            {
                lock (_gate)
                {
                    return _running.Count > 0;
                }
            }
        }

        /// <summary>Gets the meter shown: the one last started and not yet stopped; null with none running.</summary>
        public CadProgressMeter Shown
        {
            get
            {
                lock (_gate)
                {
                    return _running.Count > 0 ? _running[_running.Count - 1] : null;
                }
            }
        }

        /// <summary>Gets how many meters are running, the one shown and those under it.</summary>
        public int Depth
        {
            get
            {
                lock (_gate)
                {
                    return _running.Count;
                }
            }
        }

        /// <summary>
        /// Starts a meter, shown at once in the place of the one shown before: indeterminate until it is given a limit
        /// (<see cref="CadProgressMeter.SetLimit"/>) or a share (<see cref="CadProgressMeter.Report"/>). Stop it, or dispose of
        /// it, when the work ends; a <c>using</c> block stops it when the work throws.
        /// </summary>
        /// <param name="label">What the status bar says, as "Saving plan.dwg"; null is none.</param>
        /// <returns>The meter, running.</returns>
        public CadProgressMeter Start(string label)
        {
            var meter = new CadProgressMeter(this, label ?? string.Empty);

            lock (_gate)
            {
                _running.Add(meter);
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return meter;
        }

        /// <summary>A meter moved by a whole percent: told on where it is the one shown.</summary>
        internal void Moved(CadProgressMeter meter)
        {
            lock (_gate)
            {
                if (_running.Count == 0 || _running[_running.Count - 1] != meter)
                {
                    return;
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>A meter stopped: taken off, told on where it was the one shown.</summary>
        internal void Stopped(CadProgressMeter meter)
        {
            lock (_gate)
            {
                int at = _running.LastIndexOf(meter);

                if (at < 0)
                {
                    return;
                }

                _running.RemoveAt(at);

                if (at != _running.Count)
                {
                    return;
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
