using System;
using System.Threading;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// One meter of the status bar, started by <see cref="CadProgress.Start"/>, as AutoCAD's ProgressMeter: given a limit
    /// (<see cref="SetLimit"/>) and moved a step at a time (<see cref="MeterProgress"/>), or told a share of 0 to 1 as an
    /// <see cref="IProgress{T}"/> (<see cref="Report"/>), as <see cref="CadDocument.SaveAs(string, IProgress{double})"/> tells
    /// it; indeterminate, a sweep with no percent, until either. Stopping it, or disposing of it, takes it off the status
    /// bar; a meter stopped is not moved again.
    /// </summary>
    /// <remarks>
    /// A meter may be moved on any thread. It counts in whole percent: a step or a share that leaves the percent as it was
    /// costs a few instructions and tells nothing, so a loop over a million objects can move it at each one.
    /// </remarks>
    public sealed class CadProgressMeter : IProgress<double>, IDisposable
    {
        private readonly CadProgress _owner;
        private int _limit;
        private int _position;
        private int _percent = -1;
        private int _stopped;

        internal CadProgressMeter(CadProgress owner, string label)
        {
            _owner = owner;
            Label = label;
        }

        /// <summary>Gets what the status bar says of it, as "Saving plan.dwg".</summary>
        public string Label { get; }

        /// <summary>Gets the number of steps it was given by <see cref="SetLimit"/>; 0 for none.</summary>
        public int Limit => Volatile.Read(ref _limit);

        /// <summary>Gets the steps made since the limit was set.</summary>
        public int Position => Volatile.Read(ref _position);

        /// <summary>Gets the whole percent it shows, 0 to 100; -1 while it is indeterminate.</summary>
        public int Percent => Volatile.Read(ref _percent);

        /// <summary>Gets whether it is indeterminate: given neither a limit nor a share yet.</summary>
        public bool IsIndeterminate => Percent < 0;

        /// <summary>Gets whether it runs: started and not yet stopped.</summary>
        public bool IsRunning => Volatile.Read(ref _stopped) == 0;

        /// <summary>
        /// Sets the number of steps the work takes, the meter going back to 0 %, as AutoCAD's SetLimit; 0 makes it
        /// indeterminate again.
        /// </summary>
        /// <param name="limit">The number of steps, 0 or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the limit is below 0.</exception>
        public void SetLimit(int limit)
        {
            if (limit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "The limit of a progress meter is 0 or more.");
            }

            Volatile.Write(ref _limit, limit);
            Volatile.Write(ref _position, 0);
            Show(limit > 0 ? 0 : -1);
        }

        /// <summary>
        /// Moves it one step of its limit, as AutoCAD's MeterProgress; a step past the limit leaves it at 100 %, and one with
        /// no limit set does nothing.
        /// </summary>
        public void MeterProgress()
        {
            int limit = Volatile.Read(ref _limit);

            if (limit <= 0)
            {
                return;
            }

            int at = Interlocked.Increment(ref _position);
            Show((int)(Math.Min(at, limit) * 100L / limit));
        }

        /// <summary>
        /// Shows a share of the work done, 0 to 1, as a whole percent rounded down: 0.405 shows 40 %. A share below 0 is 0,
        /// one above 1 is 1, and one that is not a number is not shown.
        /// </summary>
        /// <param name="value">The share done.</param>
        public void Report(double value)
        {
            if (double.IsNaN(value))
            {
                return;
            }

            double share =
                value > 1.0 ? 1.0
                : value > 0.0 ? value
                : 0.0;
            Show((int)Math.Floor(share * 100.0));
        }

        /// <summary>Stops it, taking it off the status bar and showing the meter under it, if any; stopping it again does nothing.</summary>
        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0)
            {
                _owner.Stopped(this);
            }
        }

        /// <summary>Stops it, as <see cref="Stop"/>.</summary>
        public void Dispose() => Stop();

        private void Show(int percent)
        {
            if (!IsRunning)
            {
                return;
            }

            if (Interlocked.Exchange(ref _percent, percent) != percent)
            {
                _owner.Moved(this);
            }
        }
    }
}
