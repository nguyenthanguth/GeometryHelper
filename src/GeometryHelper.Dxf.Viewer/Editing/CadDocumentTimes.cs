using System;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The clocks of one drawing that TIME shows and TDINDWG and TDUSRTIMER read: when it was opened, the time the user's timer
    /// has run, and since when it has had changes not saved. The drawing's own times (<c>$TDCREATE</c>, <c>$TDUPDATE</c>,
    /// <c>$TDINDWG</c>) are kept in its header; what the header holds is the editing time before this session, and the session
    /// adds the time since the drawing was opened. A test sets <see cref="Clock"/> and <see cref="OpenedAt"/> to fixed times.
    /// </summary>
    internal sealed class CadDocumentTimes
    {
        private readonly CadDocument _document;
        private DateTime? _dirtySince;
        private bool _running;
        private TimeSpan _timerBase;
        private DateTime _timerSince;
        private DateTime _openedAt;

        internal CadDocumentTimes(CadDocument document)
        {
            _document = document;
            OpenedAt = Clock();
            _running = document.Database.Usrtimer;
            _timerBase = document.Database.Tdusrtimer;
            _timerSince = OpenedAt;
            document.Changed += (s, e) => NoteChange();
        }

        /// <summary>Gets or sets the clock: the computer's time until a test sets another.</summary>
        internal Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        /// <summary>Gets or sets when the drawing was opened, which the editing time of this session counts from.</summary>
        internal DateTime OpenedAt
        {
            get => _openedAt;
            set
            {
                // The user's timer, not yet run, counts from the same moment.
                _timerSince = value;
                _openedAt = value;
            }
        }

        /// <summary>Gets whether the user's timer runs.</summary>
        internal bool TimerRunning => _running;

        /// <summary>Gets when the drawing was made: the header's <c>$TDCREATE</c>, or when it was opened where the header gives none.</summary>
        internal DateTime Created
        {
            get
            {
                DateTime made = _document.Database.Tdcreate;
                return made == default(DateTime) ? OpenedAt : made;
            }
        }

        /// <summary>Gets when the drawing was last saved: the header's <c>$TDUPDATE</c>, or when it was opened where the header gives none.</summary>
        internal DateTime Updated
        {
            get
            {
                DateTime saved = _document.Database.Tdupdate;
                return saved == default(DateTime) ? OpenedAt : saved;
            }
        }

        /// <summary>Gets how long the drawing has been edited in all: the header's <c>$TDINDWG</c> and the time since it was opened.</summary>
        internal TimeSpan Editing => _document.Database.Tdindwg + (Clock() - OpenedAt);

        /// <summary>Gets the time the user's timer has run: what it had, and the time since it was last started while it runs.</summary>
        internal TimeSpan Timer => _running ? _timerBase + (Clock() - _timerSince) : _timerBase;

        /// <summary>Starts or stops the user's timer, keeping the time it has run.</summary>
        /// <param name="on">Whether it runs from now.</param>
        internal void SetTimer(bool on)
        {
            if (on == _running)
            {
                return;
            }

            DateTime now = Clock();

            if (_running)
            {
                _timerBase += now - _timerSince;
            }

            _running = on;
            _timerSince = now;
        }

        /// <summary>Sets the user's timer back to nought; it goes on running where it ran.</summary>
        internal void ResetTimer()
        {
            _timerBase = TimeSpan.Zero;
            _timerSince = Clock();
        }

        /// <summary>
        /// Gets how long the autosave has to wait, <paramref name="minutes"/> after the first change not saved; null where the
        /// drawing has no change, as AutoCAD's "&lt;no modifications yet&gt;". Never below nought.
        /// </summary>
        /// <param name="minutes">The interval of SAVETIME in minutes.</param>
        /// <returns>The time left, or null.</returns>
        internal TimeSpan? NextAutosave(int minutes)
        {
            if (!_document.IsModified)
            {
                _dirtySince = null;
                return null;
            }

            DateTime since = _dirtySince ?? OpenedAt;
            TimeSpan left = TimeSpan.FromMinutes(minutes) - (Clock() - since);
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>A time as AutoCAD's TIME writes it: Tuesday, November 17, 2026  8:34:00:000 AM.</summary>
        /// <param name="time">The time.</param>
        /// <returns>The words.</returns>
        internal static string Show(DateTime time) =>
            time.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture)
            + "  "
            + time.ToString("h:mm:ss", CultureInfo.InvariantCulture)
            + ":"
            + time.Millisecond.ToString("000", CultureInfo.InvariantCulture)
            + (time.Hour < 12 ? " AM" : " PM");

        /// <summary>A time spent as AutoCAD's TIME writes it: 0 days 00:09:59:999.</summary>
        /// <param name="span">The time.</param>
        /// <returns>The words.</returns>
        internal static string Show(TimeSpan span)
        {
            TimeSpan time = span < TimeSpan.Zero ? TimeSpan.Zero : span;
            return ((long)time.TotalDays).ToString(CultureInfo.InvariantCulture)
                + " days "
                + time.Hours.ToString("00", CultureInfo.InvariantCulture)
                + ":"
                + time.Minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":"
                + time.Seconds.ToString("00", CultureInfo.InvariantCulture)
                + ":"
                + time.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
        }

        /// <summary>A time as a Julian date to eight places, as the TD variables show it: 2461362.36004630.</summary>
        /// <param name="time">The time.</param>
        /// <returns>The number.</returns>
        internal static string Julian(DateTime time) =>
            (time.ToOADate() + 2415018.5).ToString("0.00000000", CultureInfo.InvariantCulture);

        private void NoteChange()
        {
            if (!_document.IsModified)
            {
                _dirtySince = null;
            }
            else if (_dirtySince == null)
            {
                _dirtySince = Clock();
            }
        }
    }
}
