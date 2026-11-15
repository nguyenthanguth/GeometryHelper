using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A series of dates filled down a table, as AutoCAD 2021's console went on with it (D42, <c>Table.Fill</c> with
    /// <c>GenerateSeries</c> over cells typed with <c>SetValue</c> of a text and <c>ConvertTextToValue</c>, 48 sources in all, in
    /// a zone seven hours ahead of Greenwich): the dates counted in seconds as <c>mktime</c> counts them, the zone taken off
    /// the date as shown and not put back, from the last date by the step between the last two, a day for one. The expected
    /// dates are worked out here from the zone of the machine, as AutoCAD's were from its own.
    /// </summary>
    public class DxfTableDateFillTests
    {
        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

        private static TimeSpan Zone(DateTime shown) => TimeZoneInfo.Local.GetUtcOffset(shown);

        private static DateTime Day(int year, int month, int day, int hour = 0, int minute = 0) =>
            new DateTime(year, month, day, hour, minute, 0);

        /// <summary>A table of 14 rows and 2 columns whose cells (from row 2, in column 0) are typed with the words given.</summary>
        private static DxfTable Typed(int firstRow, params string[] words) => Typed(out _, firstRow, words);

        private static DxfTable Typed(out DxfDatabase db, int firstRow, params string[] words)
        {
            db = DxfSpeed12Scenarios.Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 14, 2, 8.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int i = 0; i < words.Length; i++)
            {
                Assert.True(table.SetValueFromText(firstRow + i, 0, words[i]));
            }

            return table;
        }

        private static void In(Action act)
        {
            CultureInfo was = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = En;

            try
            {
                act();
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }

        private static void Fill(
            DxfTable table,
            int from,
            int to,
            int source,
            int sources,
            DxfTableFillOptions options
        ) =>
            table.Fill(
                new DxfCellRange(from, 0, to, 0),
                new DxfCellRange(source, 0, source + sources - 1, 0),
                DxfTableFillOptions.GenerateSeries | options
            );

        [Fact]
        public void OneDate_GoesOnByADay_WithTheZoneOfTheMachineTakenOff()
        {
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020");

                Fill(table, 3, 5, 2, 1, DxfTableFillOptions.None);

                TimeSpan zone = Zone(Day(2020, 1, 1));
                Assert.Equal(Day(2020, 1, 1), table.Value(2, 0));

                for (int k = 1; k <= 3; k++)
                {
                    DateTime expected = Day(2020, 1, 1).AddDays(k) - zone;
                    Assert.Equal(expected, table.Value(2 + k, 0));
                    Assert.Equal(expected.ToString("d", En), table.TextString(2 + k, 0));
                    Assert.Equal("%x", table.Format(2 + k, 0));
                    Assert.Equal(DxfDataType.Date, table.DataType(2 + k, 0));
                }
            });
        }

        [Fact]
        public void TwoDates_GoOnByTheStepBetweenThem_ThatOfAMonthTooMakesItThirtyOneDays()
        {
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020", "2/1/2020");

                Fill(table, 4, 6, 2, 2, DxfTableFillOptions.None);

                TimeSpan zone = Zone(Day(2020, 2, 1));
                Assert.Equal(Day(2020, 2, 1).AddDays(31) - zone, table.Value(4, 0));
                Assert.Equal(Day(2020, 2, 1).AddDays(62) - zone, table.Value(5, 0));
                Assert.Equal(Day(2020, 2, 1).AddDays(93) - zone, table.Value(6, 0));
            });
        }

        [Fact]
        public void ThreeDates_GoOnByTheStepBetweenTheLastTwo_NotTheAverageOfThem()
        {
            // 1/1, 1/2 and 1/4: the numbers' 1.5 from the first to the last, but 2 days, the last step, for AutoCAD's dates.
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020", "1/2/2020", "1/4/2020");

                Fill(table, 5, 7, 2, 3, DxfTableFillOptions.None);

                TimeSpan zone = Zone(Day(2020, 1, 4));
                Assert.Equal(Day(2020, 1, 4).AddDays(2) - zone, table.Value(5, 0));
                Assert.Equal(Day(2020, 1, 4).AddDays(4) - zone, table.Value(6, 0));
                Assert.Equal(Day(2020, 1, 4).AddDays(6) - zone, table.Value(7, 0));
            });
        }

        [Fact]
        public void ADescendingPair_GoesOnDownwards()
        {
            In(() =>
            {
                DxfTable table = Typed(2, "1/5/2020", "1/3/2020");

                Fill(table, 4, 5, 2, 2, DxfTableFillOptions.None);

                TimeSpan zone = Zone(Day(2020, 1, 3));
                Assert.Equal(Day(2020, 1, 3).AddDays(-2) - zone, table.Value(4, 0));
                Assert.Equal(Day(2020, 1, 3).AddDays(-4) - zone, table.Value(5, 0));
            });
        }

        [Fact]
        public void Reverse_GoesBackFromTheFirst_ByTheStepBetweenTheFirstTwo_TheLastFilledCellNextToIt()
        {
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020", "1/2/2020", "1/4/2020");

                Fill(table, 5, 7, 2, 3, DxfTableFillOptions.Reverse);

                TimeSpan zone = Zone(Day(2020, 1, 1));
                Assert.Equal(Day(2020, 1, 1).AddDays(-1) - zone, table.Value(7, 0));
                Assert.Equal(Day(2020, 1, 1).AddDays(-2) - zone, table.Value(6, 0));
                Assert.Equal(Day(2020, 1, 1).AddDays(-3) - zone, table.Value(5, 0));
            });
        }

        [Fact]
        public void AFillAboveTheSource_GoesOnThroughTheFillRange_FromItsFirstCell()
        {
            In(() =>
            {
                DxfTable table = Typed(6, "1/10/2020", "1/11/2020");

                Fill(table, 2, 4, 6, 2, DxfTableFillOptions.None);

                TimeSpan zone = Zone(Day(2020, 1, 11));
                Assert.Equal(Day(2020, 1, 11).AddDays(1) - zone, table.Value(2, 0));
                Assert.Equal(Day(2020, 1, 11).AddDays(2) - zone, table.Value(3, 0));
                Assert.Equal(Day(2020, 1, 11).AddDays(3) - zone, table.Value(4, 0));
            });
        }

        [Fact]
        public void DatesAndTimesOfDifferentFormats_GoOnInSeconds_EachCellOfTheFormatOfTheSourceItRepeats()
        {
            // 1/1/2020 and 1/2/2020 12:30: a step of 36 hours 30 minutes, the cells in turn %x and %c.
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020", "1/2/2020 12:30");

                Fill(table, 4, 5, 2, 2, DxfTableFillOptions.None);

                TimeSpan zone = Zone(Day(2020, 1, 2, 12, 30));
                TimeSpan step = TimeSpan.FromMinutes(36 * 60 + 30);
                Assert.Equal(Day(2020, 1, 2, 12, 30) + step - zone, table.Value(4, 0));
                Assert.Equal(Day(2020, 1, 2, 12, 30) + step + step - zone, table.Value(5, 0));
                Assert.Equal("%x", table.Format(4, 0));
                Assert.Equal("%c", table.Format(5, 0));
            });
        }

        [Fact]
        public void ATimeOfTheDayAlone_ThatIsBeforeTheEpoch_GoesOnFromMinusOne_AsAutoCadDidIt()
        {
            // 12:30 is on the 30th of December 1899, which mktime refuses (-1): one source goes on by a day from there, to
            // 1/1/1970 23:59:59 in whatever zone; two, a step of nought from -1 (23:59:59 on 31 December 1969).
            In(() =>
            {
                DxfTable one = Typed(2, "12:30");
                Fill(one, 3, 5, 2, 1, DxfTableFillOptions.None);

                Assert.Equal(new DateTime(1970, 1, 1, 23, 59, 59), one.Value(3, 0));
                Assert.Equal(new DateTime(1970, 1, 2, 23, 59, 59), one.Value(4, 0));
                Assert.Equal(new DateTime(1970, 1, 3, 23, 59, 59), one.Value(5, 0));
                Assert.Equal("%X", one.Format(3, 0));

                DxfTable two = Typed(2, "12:30", "13:30");
                Fill(two, 4, 6, 2, 2, DxfTableFillOptions.None);

                Assert.Equal(new DateTime(1969, 12, 31, 23, 59, 59), two.Value(4, 0));
                Assert.Equal(new DateTime(1969, 12, 31, 23, 59, 59), two.Value(6, 0));
            });
        }

        [Fact]
        public void ADateTheSeriesCannotMake_IsADateOfNoWords_OfSixteenBytesOfNought()
        {
            // Reverse from 1/1/2020 by a step of 5 000 days: the 4th cell back is 20 000 days before it, past 12 hours before the
            // epoch, where AutoCAD 2021 made no date: its words empty, its value read as the empty text.
            In(() =>
            {
                string later = Day(2020, 1, 1).AddDays(5000).ToString("M/d/yyyy", En);
                DxfTable table = Typed(out DxfDatabase db, 2, "1/1/2020", later);

                Fill(table, 4, 13, 2, 2, DxfTableFillOptions.Reverse);

                TimeSpan zone = Zone(Day(2020, 1, 1));
                Assert.Equal(Day(2020, 1, 1).AddDays(-5000) - zone, table.Value(13, 0));
                Assert.Equal(Day(2020, 1, 1).AddDays(-15000) - zone, table.Value(11, 0));
                Assert.Equal(DxfDataType.Date, table.DataType(10, 0));
                Assert.Equal(string.Empty, table.TextString(10, 0));
                Assert.Equal(string.Empty, table.Value(10, 0));
                Assert.Contains(
                    "310\n" + new string('0', 32) + "\n",
                    DxfAutoCadChecksD16Tests.Record(db.ToText(), table.Handle),
                    StringComparison.Ordinal
                );
            });
        }

        [Fact]
        public void DatesMixedWithANumber_AreCopied_NotCarriedOn()
        {
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020");
                table.SetValue(3, 0, 5);

                Fill(table, 4, 7, 2, 2, DxfTableFillOptions.None);

                Assert.Equal(Day(2020, 1, 1), table.Value(4, 0));
                Assert.Equal(5, table.Value(5, 0));
                Assert.Equal(Day(2020, 1, 1), table.Value(6, 0));
                Assert.Equal(5, table.Value(7, 0));
            });
        }

        [Fact]
        public void CopyContent_WinsOverTheSeriesOfDates()
        {
            In(() =>
            {
                DxfTable table = Typed(2, "1/1/2020", "1/2/2020");

                Fill(table, 4, 5, 2, 2, DxfTableFillOptions.CopyContent);

                Assert.Equal(Day(2020, 1, 1), table.Value(4, 0));
                Assert.Equal(Day(2020, 1, 2), table.Value(5, 0));
            });
        }

        [Fact]
        public void TheDatesOfTheConsole_AreTheOnesGivenInAZoneSevenHoursAheadOfGreenwich()
        {
            // The dates AutoCAD 2021's console gave, written out, where the machine is in the zone it ran in (SE Asia); the others
            // are held by the tests above, worked out from the machine's own zone.
            if (Zone(Day(2020, 1, 1)) != TimeSpan.FromHours(7) || Zone(Day(2020, 7, 1)) != TimeSpan.FromHours(7))
            {
                return;
            }

            In(() =>
            {
                DxfTable one = Typed(2, "1/1/2020");
                Fill(one, 3, 5, 2, 1, DxfTableFillOptions.None);
                Assert.Equal(Day(2020, 1, 1, 17), one.Value(3, 0));
                Assert.Equal(Day(2020, 1, 2, 17), one.Value(4, 0));
                Assert.Equal(Day(2020, 1, 3, 17), one.Value(5, 0));

                DxfTable three = Typed(2, "1/1/2020", "1/2/2020", "1/4/2020");
                Fill(three, 5, 7, 2, 3, DxfTableFillOptions.None);
                Assert.Equal(Day(2020, 1, 5, 17), three.Value(5, 0));
                Assert.Equal(Day(2020, 1, 7, 17), three.Value(6, 0));
                Assert.Equal(Day(2020, 1, 9, 17), three.Value(7, 0));

                DxfTable times = Typed(2, "1/1/2020 12:30", "1/1/2020 18:00");
                Fill(times, 4, 6, 2, 2, DxfTableFillOptions.None);
                Assert.Equal(Day(2020, 1, 1, 16, 30), times.Value(4, 0));
                Assert.Equal(Day(2020, 1, 1, 22, 0), times.Value(5, 0));
                Assert.Equal(Day(2020, 1, 2, 3, 30), times.Value(6, 0));

                // The first date is before the epoch in the zone (1/1/1970 is -25 200 s, so -1): a step of 61 201 s.
                DxfTable epoch = Typed(2, "1/1/1970", "1/2/1970");
                Fill(epoch, 4, 6, 2, 2, DxfTableFillOptions.None);
                Assert.Equal(new DateTime(1970, 1, 2, 10, 0, 1), epoch.Value(4, 0));
                Assert.Equal(new DateTime(1970, 1, 3, 3, 0, 2), epoch.Value(5, 0));
                Assert.Equal(new DateTime(1970, 1, 3, 20, 0, 3), epoch.Value(6, 0));

                // Back from 1/3/1970 by a day: 61 200 s, then -25 200 s (12/31/1969 17:00), then none.
                DxfTable back = Typed(2, "1/3/1970", "1/4/1970");
                Fill(back, 4, 7, 2, 2, DxfTableFillOptions.Reverse);
                Assert.Equal(new DateTime(1970, 1, 1, 17, 0, 0), back.Value(7, 0));
                Assert.Equal(new DateTime(1969, 12, 31, 17, 0, 0), back.Value(6, 0));
                Assert.Equal(string.Empty, back.Value(5, 0));
            });
        }

        [Fact]
        public void ADateSeries_SavedAndReadAgain_IsTheSameDateTimes()
        {
            In(() =>
            {
                var db = DxfSpeed12Scenarios.Metric();
                var table = new DxfTable(new GeoPoint3(0, 0, 0), 8, 2, 8.0, 30.0);
                db.ModelSpace.AppendEntity(table);
                Assert.True(table.SetValueFromText(2, 0, "1/1/2020 12:30"));
                Fill(table, 3, 5, 2, 1, DxfTableFillOptions.None);

                DxfTable again = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfTable>().Single();

                for (int r = 2; r <= 5; r++)
                {
                    Assert.Equal(table.Value(r, 0), again.Value(r, 0));
                    Assert.IsType<DateTime>(again.Value(r, 0));
                }
            });
        }
    }
}
