using System;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The times the header keeps, as AutoCAD's <c>Database</c> gives them: when the drawing was made and last saved, how long
    /// it has been edited, and the time zone of its place. A date is written as AutoCAD writes it, a Julian day and the part
    /// of the day gone since midnight: <c>2451545.75</c> is 1 January 2000 at 18:00.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        // The Julian day of 1 January of the year 1, DateTime's first day.
        private const double FirstJulianDay = 1721426.0;

        /// <summary>
        /// Gets when the drawing was made, in the time of its place (<c>$TDCREATE</c>), as AutoCAD's <c>Tdcreate</c>, to the
        /// millisecond, its <c>Kind</c> <see cref="DateTimeKind.Unspecified"/>; <c>default(DateTime)</c> where the header gives
        /// none or a time DateTime cannot hold: one before 1 January of the year 1 (the Julian day 1721426), or one rounded past
        /// the end of 31 December 9999, whose noon is 5373484.5.
        /// </summary>
        public DateTime Tdcreate => Julian("$TDCREATE", DateTimeKind.Unspecified);

        /// <summary>Gets when the drawing was made, in universal time (<c>$TDUCREATE</c>), as AutoCAD's <c>Tducreate</c>, its <c>Kind</c> <see cref="DateTimeKind.Utc"/>; <c>default(DateTime)</c> where the header gives none or a time DateTime cannot hold.</summary>
        public DateTime Tducreate => Julian("$TDUCREATE", DateTimeKind.Utc);

        /// <summary>Gets when the drawing was last saved, in the time of its place (<c>$TDUPDATE</c>), as AutoCAD's <c>Tdupdate</c>; <c>default(DateTime)</c> where the header gives none or a time DateTime cannot hold.</summary>
        public DateTime Tdupdate => Julian("$TDUPDATE", DateTimeKind.Unspecified);

        /// <summary>Gets when the drawing was last saved, in universal time (<c>$TDUUPDATE</c>), as AutoCAD's <c>Tduupdate</c>, its <c>Kind</c> <see cref="DateTimeKind.Utc"/>; <c>default(DateTime)</c> where the header gives none or a time DateTime cannot hold.</summary>
        public DateTime Tduupdate => Julian("$TDUUPDATE", DateTimeKind.Utc);

        /// <summary>Gets how long the drawing has been edited in all (<c>$TDINDWG</c>, in days), as AutoCAD's <c>Tdindwg</c>, to the millisecond; nought where the header gives none.</summary>
        public TimeSpan Tdindwg => Days("$TDINDWG");

        /// <summary>Gets the time the user's timer has run (<c>$TDUSRTIMER</c>, in days), as AutoCAD's <c>Tdusrtimer</c>, to the millisecond; nought where the header gives none.</summary>
        public TimeSpan Tdusrtimer => Days("$TDUSRTIMER");

        /// <summary>Gets or sets whether the user's timer runs (<c>$USRTIMER</c> of 1), as AutoCAD's <c>Usrtimer</c>; true where the header gives none.</summary>
        public bool Usrtimer
        {
            get => Bool("$USRTIMER", 70, true);
            set => PutBool("$USRTIMER", 70, value, Usrtimer, 1009);
        }

        /// <summary>Gets or sets the time zone of the drawing's place (<c>$TIMEZONE</c>), as AutoCAD's <c>TimeZone</c>; <see cref="DxfTimeZone.Pacific"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the zone set is not one of <see cref="DxfTimeZone"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfTimeZone TimeZone
        {
            get => (DxfTimeZone)Whole("$TIMEZONE", 70, (int)DxfTimeZone.Pacific);

            set
            {
                if (value != TimeZone && !Enum.IsDefined(typeof(DxfTimeZone), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A time zone is one of DxfTimeZone.");
                }

                PutWhole("$TIMEZONE", 70, (int)value, (int)TimeZone, int.MinValue, int.MaxValue, 1021);
            }
        }

        /// <summary>
        /// Gets the hours a time zone is ahead of universal time, as AutoCAD's <c>TimeZoneOffset</c>: 5.75 for
        /// <see cref="DxfTimeZone.Kathmandu"/>, -8 for <see cref="DxfTimeZone.Pacific"/>; summer time is not counted.
        /// </summary>
        /// <param name="tz">The time zone.</param>
        /// <returns>The hours, below nought west of Greenwich.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the zone is not one of <see cref="DxfTimeZone"/>.</exception>
        public double TimeZoneOffset(DxfTimeZone tz) => Minutes(tz, nameof(tz)) / 60.0;

        /// <summary>
        /// Gets the words a time zone is shown by, as AutoCAD's <c>TimeZoneDescription</c>: its offset from universal time and
        /// its name, <c>(UTC-08:00) Pacific</c> for <see cref="DxfTimeZone.Pacific"/>; written by the library, not AutoCAD's own
        /// words.
        /// </summary>
        /// <param name="tz">The time zone.</param>
        /// <returns>The words.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the zone is not one of <see cref="DxfTimeZone"/>.</exception>
        public string TimeZoneDescription(DxfTimeZone tz)
        {
            int minutes = Minutes(tz, nameof(tz));
            int whole = Math.Abs(minutes);
            string name = tz.ToString();
            var words = new System.Text.StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                {
                    words.Append(' ');
                }

                words.Append(name[i]);
            }

            return "(UTC" + (minutes < 0 ? "-" : "+") + (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture) + ") " + words;
        }

        /// <summary>
        /// Sets the time zone to the first of AutoCAD's zones of an offset from universal time, as AutoCAD's
        /// <c>SetTimeZoneAsUtcOffset</c>: -7 gives <see cref="DxfTimeZone.Mountain"/> rather than Arizona or Mazatlan.
        /// </summary>
        /// <param name="offset">The hours ahead of universal time, as 5.5 for India.</param>
        /// <returns>The zone set.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when no zone of AutoCAD is that many hours ahead.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the zone changes in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfTimeZone SetTimeZoneAsUtcOffset(double offset)
        {
            double minutes = offset * 60.0;
            double whole = Math.Round(minutes);

            if (!(Math.Abs(minutes - whole) <= 1E-9) || Math.Abs(whole) > 24 * 60)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "No time zone of AutoCAD is " + offset.ToString(CultureInfo.InvariantCulture) + " hours from universal time.");
            }

            int at = (int)Math.Abs(whole);
            var zone = (DxfTimeZone)(Math.Sign(whole) * (at / 60 * 1000 + at % 60 * 10));

            if (!Enum.IsDefined(typeof(DxfTimeZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "No time zone of AutoCAD is " + offset.ToString(CultureInfo.InvariantCulture) + " hours from universal time.");
            }

            TimeZone = zone;
            return zone;
        }

        /// <summary>
        /// Starts the drawing's times again, as AutoCAD's <c>ResetTimes</c>: made and last saved now (<c>$TDCREATE</c> and
        /// <c>$TDUPDATE</c> in the time of the computer, <c>$TDUCREATE</c> and <c>$TDUUPDATE</c> in universal time, the last
        /// two where the file's version has them), edited for no time (<c>$TDINDWG</c>) and the user's timer at nought
        /// (<c>$TDUSRTIMER</c>).
        /// </summary>
        public void ResetTimes()
        {
            DateTime now = DateTime.Now;
            DateTime universal = now.ToUniversalTime();
            Set("$TDCREATE", 40, JulianOf(now));
            Set("$TDUPDATE", 40, JulianOf(now));

            if (File.Supports(1015))
            {
                Set("$TDUCREATE", 40, JulianOf(universal));
                Set("$TDUUPDATE", 40, JulianOf(universal));
            }

            PutReal("$TDINDWG", 40, 0.0, Real("$TDINDWG", 40, 0.0), Bound.Finite, 1009);
            PutReal("$TDUSRTIMER", 40, 0.0, Real("$TDUSRTIMER", 40, 0.0), Bound.Finite, 1009);
        }

        /// <summary>A date of the header, read as AutoCAD reads it: the whole Julian day, and the part of it since midnight to the millisecond.</summary>
        private DateTime Julian(string name, DateTimeKind kind)
        {
            if (!Numbers.TryRead(First(name, 40), out double julian) || !Guard.IsFinite(julian))
            {
                return default(DateTime);
            }

            double day = Math.Floor(julian);
            double milliseconds = Math.Round((julian - day) * 86400000.0);

            // 3 652 058 days after the first is 31 December 9999, the last day DateTime holds; a part of it rounded up to the
            // midnight after it is past its end.
            if (day < FirstJulianDay || day > FirstJulianDay + 3652058.0 || (day == FirstJulianDay + 3652058.0 && milliseconds >= 86400000.0))
            {
                return default(DateTime);
            }

            return new DateTime(1, 1, 1, 0, 0, 0, kind).AddDays(day - FirstJulianDay).AddMilliseconds(milliseconds);
        }

        /// <summary>A date as the header writes it, a Julian day and the part of it since midnight.</summary>
        private static string JulianOf(DateTime date)
            => Numbers.Exact(FirstJulianDay + (date.Date - DateTime.MinValue).Days + date.TimeOfDay.Ticks / (double)TimeSpan.TicksPerDay);

        /// <summary>A time of the header given in days, to the millisecond; nought where it gives none or more than a TimeSpan holds.</summary>
        private TimeSpan Days(string name)
        {
            double days = Real(name, 40, 0.0);
            return Math.Abs(days) < 1E7 ? TimeSpan.FromTicks((long)Math.Round(days * 86400000.0) * TimeSpan.TicksPerMillisecond) : TimeSpan.Zero;
        }

        /// <summary>The minutes a time zone is ahead of universal time: its number's thousands are hours, its tens minutes.</summary>
        private static int Minutes(DxfTimeZone tz, string parameter)
        {
            if (!Enum.IsDefined(typeof(DxfTimeZone), tz))
            {
                throw new ArgumentOutOfRangeException(parameter, tz, "A time zone is one of DxfTimeZone.");
            }

            int number = Math.Abs((int)tz);
            return Math.Sign((int)tz) * (number / 1000 * 60 + number % 1000 / 10);
        }
    }
}
