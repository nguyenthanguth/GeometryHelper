using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A SUN of a DXF file, as AutoCAD's <c>Sun</c>: the light of the sun a viewport is lit by, owned by the viewport, which
    /// names it (group 361 of the VIEWPORT); whether it is on, its colour and intensity, the day and time it shines at and the
    /// shadows it casts, read and set as the groups of AcDbSun of the DXF reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>viewport.SetSun(new DxfSun { DateTime = new DateTime(2026, 6, 21, 15, 0, 0), Intensity = 1.5 })</c> lights a viewport
    /// by the sun of a summer afternoon, half as bright again as AutoCAD's: a SUN at the end of the OBJECTS section, owned by
    /// the viewport, the day written as its Julian day 2461213 (91) and the time as 54 000 seconds past midnight (92). A sun
    /// made in code is ezdxf's new sun: on, white, of intensity 1, casting shadows traced along the rays, on 21 September 2014
    /// at noon; that AutoCAD's own new sun is the same is not confirmed.
    /// </para>
    /// <para>
    /// A SUN came with R2007 (AC1021). Where the sun stands in the sky, AutoCAD's <c>Altitude</c>, <c>Azimuth</c> and
    /// <c>SunDirection</c>, AutoCAD works out from the drawing's geographic location and the day, and the sky
    /// (<c>SkyParameters</c>) is its renderer's: a SUN keeps neither, and neither is here.
    /// </para>
    /// </remarks>
    public sealed class DxfSun : DxfDBObject
    {
        // The groups of AcDbSun in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 90, 290, 63, 421, 40, 291, 91, 92, 292, 70, 71, 280 });

        // The Julian day of 1 January of the year 1, the first day a DateTime holds.
        private const long FirstJulianDay = 1721426;

        // The Julian day and the time of ezdxf's new sun: 21 September 2014, at noon.
        private const int DefaultDay = 2456922;
        private const int DefaultSeconds = 43200;

        // The groups a sun made in code is written with after its marker, as ezdxf writes a new one.
        private readonly List<KeyValuePair<int, string>> _made = new List<KeyValuePair<int, string>>
        {
            Pair(90, "1"), Pair(290, "1"), Pair(63, "7"), Pair(421, "16777215"), Pair(40, "1.0"), Pair(291, "1"),
            Pair(91, "2456922"), Pair(92, "43200"), Pair(292, "0"), Pair(70, "0"), Pair(71, "256"), Pair(280, "1"),
        };

        /// <summary>
        /// Initializes a sun made in code, as AutoCAD's <c>Sun()</c>, with ezdxf's defaults (AutoCAD's own not confirmed): on,
        /// white, of intensity 1, casting shadows traced along the rays, on 21 September 2014 at noon; given to a viewport by
        /// <see cref="DxfViewport.SetSun"/>.
        /// </summary>
        public DxfSun()
        {
        }

        internal DxfSun(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets whether the sun is on (group 290), as AutoCAD's <c>IsOn</c>; on where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public bool IsOn
        {
            get => Whole(290, 1) != 0;
            set => SetFlag(290, value, IsOn);
        }

        /// <summary>
        /// Gets or sets the colour of the sun's light (groups 63 and 421), as AutoCAD's <c>SunColor</c>: its true colour where it
        /// has one, else its colour of the index; the index 7, white, where the record gives neither.
        /// </summary>
        /// <value>Set, a colour of the index, written as its 63 with the 421 taken out, or a true colour, written as its 421 with the index nearest it as its 63.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is not of the index or a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public DxfColor SunColor
        {
            get
            {
                int index = Whole(63, 7);

                if (int.TryParse(Value(421)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rgb))
                {
                    return DxfColor.OfRgb(rgb & 0xFFFFFF, null, Value(63) == null ? (int?)null : index);
                }

                return DxfColor.OfIndex(index);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Equals(SunColor))
                {
                    return;
                }

                if (value.ColorMethod != DxfColorMethod.ByAci && value.ColorMethod != DxfColorMethod.ByColor)
                {
                    throw new ArgumentException("The light of a sun is of a colour of the index or a true colour.", nameof(value));
                }

                bool rgb = value.ColorMethod == DxfColorMethod.ByColor;
                Set(Pair(63, value.ColorIndex.ToString(CultureInfo.InvariantCulture)), Pair(421, rgb ? value.ColorValue.ToString(CultureInfo.InvariantCulture) : null));
            }
        }

        /// <summary>Gets or sets how bright the sun is (group 40), as AutoCAD's <c>Intensity</c>: 1 for AutoCAD's sun, 0 for none at all; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the intensity set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public double Intensity
        {
            get => Real(40, 1.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "The intensity of a sun is a finite number of nought or more.");

                if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(Intensity))
                {
                    Set(Pair(40, Numbers.Exact(value + 0.0)));
                }
            }
        }

        /// <summary>
        /// Gets or sets the day and the time of day the sun shines at (groups 91 and 92), as AutoCAD's <c>DateTime</c>: the
        /// Julian day and the seconds past midnight, the time at the drawing's place; 21 September 2014 at noon where the record
        /// gives neither.
        /// </summary>
        /// <value>Set, the day as its Julian day, 2461213 for 21 June 2026, and the time to the whole second, 15:00:00.75 written as 54 000; its kind (local or universal) is not kept. A day or a time the record gives unreadably is written, even where the date set is the one read in its place.</value>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public DateTime DateTime
        {
            get
            {
                long day = Whole(91, DefaultDay) - FirstJulianDay;
                long seconds = Whole(92, DefaultSeconds);

                if (day < 0 || day > DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay)
                {
                    day = DefaultDay - FirstJulianDay;
                }

                if (seconds < 0 || seconds >= 86400)
                {
                    seconds = DefaultSeconds;
                }

                return new DateTime(day * TimeSpan.TicksPerDay + seconds * TimeSpan.TicksPerSecond);
            }

            set
            {
                string day = (value.Ticks / TimeSpan.TicksPerDay + FirstJulianDay).ToString(CultureInfo.InvariantCulture);
                string seconds = (value.TimeOfDay.Ticks / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture);

                // Judged by the groups as written, so that a day or a time the record gives unreadably is written even where the
                // date set is the one shown in its place.
                if (string.Equals(Value(91)?.Trim(), day, StringComparison.Ordinal) && string.Equals(Value(92)?.Trim(), seconds, StringComparison.Ordinal))
                {
                    return;
                }

                Set(Pair(91, day), Pair(92, seconds));
            }
        }

        /// <summary>Gets or sets whether the time is that of daylight saving (group 292), as AutoCAD's <c>IsDaylightSavingsOn</c>; false where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public bool IsDaylightSavingsOn
        {
            get => Whole(292, 0) != 0;
            set => SetFlag(292, value, IsDaylightSavingsOn);
        }

        /// <summary>Gets or sets whether the sun casts shadows (group 291), as AutoCAD's <c>ShadowsOn</c> of its shadow parameters; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public bool ShadowsOn
        {
            get => Whole(291, 1) != 0;
            set => SetFlag(291, value, ShadowsOn);
        }

        /// <summary>Gets or sets how the shadows are made (group 70), as AutoCAD's <c>ShadowType</c> of its shadow parameters; traced along the rays where the record gives none.</summary>
        /// <value>Set, traced along the rays or drawn from maps, the two the DXF reference gives.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not <see cref="DxfShadowType.RayTraced"/> or <see cref="DxfShadowType.Maps"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public DxfShadowType ShadowType
        {
            get => (DxfShadowType)Whole(70, 0);

            set
            {
                if (value == ShadowType)
                {
                    return;
                }

                if (value != DxfShadowType.RayTraced && value != DxfShadowType.Maps)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The shadows of a sun are traced along the rays (0) or drawn from maps (1), as the DXF reference gives them.");
                }

                Set(Pair(70, ((int)value).ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>Gets or sets the size of a map of shadows (group 71), as AutoCAD's <c>ShadowMapSize</c> of its shadow parameters: 64 to 4 096, a power of two; 256 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not 64, 128, 256, 512, 1 024, 2 048 or 4 096.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public short ShadowMapSize
        {
            get => (short)Whole(71, 256);

            set
            {
                if (value == ShadowMapSize)
                {
                    return;
                }

                if (value < 64 || value > 4096 || (value & (value - 1)) != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A map of shadows is 64, 128, 256, 512, 1024, 2048 or 4096, as AutoCAD's shadow parameters take it.");
                }

                Set(Pair(71, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>Gets or sets how soft the edges of mapped shadows are (group 280), as AutoCAD's <c>ShadowMapSoftness</c> of its shadow parameters: 1 to 10; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the softness set is not from 1 to 10.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the sun is erased.</exception>
        public byte ShadowMapSoftness
        {
            get => (byte)Math.Max(0, Math.Min(255, Whole(280, 1)));

            set
            {
                if (value == ShadowMapSoftness)
                {
                    return;
                }

                if (value < 1 || value > 10)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The softness of mapped shadows runs from 1 to 10, as AutoCAD's shadow parameters take it.");
                }

                Set(Pair(280, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>Gives the sun as a line for debugging: its type, its handle and whether it is on.</summary>
        /// <returns>The line, such as <c>SUN 2F, on</c>.</returns>
        public override string ToString() => "SUN " + (Handle ?? string.Empty) + (IsOn ? ", on" : ", off");

        /// <summary>The groups a sun made in code is written with, after its subclass marker, in AutoCAD's order.</summary>
        internal IReadOnlyList<KeyValuePair<int, string>> MadeGroups => _made.AsReadOnly();

        private static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

        /// <summary>The value of the first group of a code of AcDbSun, as made in code or as the file writes it; null where it has none.</summary>
        private string Value(int code)
        {
            if (Item != null)
            {
                return Item.File.ValueOf(Item, code);
            }

            foreach (KeyValuePair<int, string> group in _made)
            {
                if (group.Key == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>A whole number of AcDbSun; the fallback where it has none, or not a whole number.</summary>
        private int Whole(int code, int fallback)
            => int.TryParse(Value(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>A number of AcDbSun; the fallback where it has none, or not a finite number.</summary>
        private double Real(int code, double fallback)
            => Numbers.TryRead(Value(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>Sets a flag of AcDbSun, written 1 or 0; nothing where it is as it was.</summary>
        private void SetFlag(int code, bool value, bool now)
        {
            if (value != now)
            {
                Set(Pair(code, value ? "1" : "0"));
            }
        }

        /// <summary>
        /// Sets groups of AcDbSun, each in its place, one it has not put where AutoCAD's order puts it, null taking one out: those
        /// a sun made in code is to be written with, or the record's.
        /// </summary>
        private void Set(params KeyValuePair<int, string>[] values)
        {
            if (Item != null)
            {
                InFile("be changed").SetValues(Item, values, Order, false);
                return;
            }

            foreach (KeyValuePair<int, string> value in values)
            {
                int at = _made.FindIndex(g => g.Key == value.Key);

                if (value.Value == null)
                {
                    if (at >= 0)
                    {
                        _made.RemoveAt(at);
                    }
                }
                else if (at >= 0)
                {
                    _made[at] = value;
                }
                else
                {
                    int rank = Rank(value.Key);
                    int put = _made.FindIndex(g => Rank(g.Key) > rank);
                    _made.Insert(put < 0 ? _made.Count : put, value);
                }
            }
        }

        /// <summary>The place of a code in AutoCAD's order of the groups of AcDbSun.</summary>
        private static int Rank(int code)
        {
            for (int i = 1; i < Order.Count; i++)
            {
                if (Order[i] == code)
                {
                    return i;
                }
            }

            return Order.Count;
        }
    }
}
