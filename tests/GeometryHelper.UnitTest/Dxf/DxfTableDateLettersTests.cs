using System;
using System.Globalization;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// More letters of <c>strftime</c> in the format of a table's date, as AutoCAD 2021's console showed them (en-US, a drawing
    /// read from DXF, the format set again so that AutoCAD works the words out): the dates are those of the 16 bytes of the
    /// cell, whose day of the week the DXF carries. <c>%j</c>, <c>%U</c>, <c>%W</c>, <c>%V</c>, <c>%G</c> and <c>%g</c> are not
    /// written: AutoCAD gives <c>%j</c> 001 for every date, <c>%U</c> 01 for a Sunday and 00 for any other day, and
    /// <c>%W</c> 01 for a Monday and 00 for any other, as the day of the year is never worked out for a date it reads, which
    /// is plainly wrong; those show the date.
    /// </summary>
    public class DxfTableDateLettersTests
    {
        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

        [Theory]
        [InlineData("%C", "20")]
        [InlineData("%h", "Mar")]
        [InlineData("%F", "2020-03-07")]
        [InlineData("%u", "6")]
        [InlineData("%r", "3:04:05 PM")]
        [InlineData("%F %r", "2020-03-07 3:04:05 PM")]
        public void TheLettersCHFUAndR_AreShownAsAutoCadShowsThem(string format, string shown)
        {
            // 3/7/2020 3:04:05 PM, a Saturday; %r is the time of the machine, 3:04:05 PM, with no nought before the 3.
            Assert.Equal(shown, DxfTableDate.Text(new DateTime(2020, 3, 7, 15, 4, 5), format, En));
        }

        [Theory]
        [InlineData(2020, 3, 8, "7")]
        [InlineData(2020, 3, 9, "1")]
        [InlineData(2021, 2, 1, "1")]
        [InlineData(2020, 11, 29, "7")]
        public void TheLetterU_IsTheDayOfTheWeekFromOneOnAMonday_ToSevenOnASunday(
            int year,
            int month,
            int day,
            string shown
        )
        {
            Assert.Equal(shown, DxfTableDate.Text(new DateTime(year, month, day, 9, 0, 0), "%u", En));
        }

        [Fact]
        public void TheLettersZAndLowerZ_AreTheMachinesZone_AsAutoCadShowsIt()
        {
            // The machine of the check was at +07:00 and AutoCAD showed +0700 and the name of its daylight time,
            // "SE Asia Daylight Time", for every date; the zone of the machine is read, not the one the check ran in.
            var date = new DateTime(2020, 3, 7, 15, 4, 5);
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(date);
            string expected =
                (offset < TimeSpan.Zero ? "-" : "+")
                + Math.Abs(offset.Hours).ToString("00", CultureInfo.InvariantCulture)
                + Math.Abs(offset.Minutes).ToString("00", CultureInfo.InvariantCulture);
            Assert.Equal(expected, DxfTableDate.Text(date, "%z", En));
            Assert.Equal(TimeZoneInfo.Local.DaylightName, DxfTableDate.Text(date, "%Z", En));
            Assert.Equal(expected + " " + TimeZoneInfo.Local.DaylightName, DxfTableDate.Text(date, "%z %Z", En));
        }

        [Theory]
        [InlineData("%j")]
        [InlineData("%U")]
        [InlineData("%W")]
        [InlineData("%V")]
        [InlineData("%G")]
        [InlineData("%g")]
        public void TheLettersAutoCadGetsWrong_ShowTheDate(string format)
        {
            // AutoCAD's 001 for the 7th of March and its 00 for a Saturday are not the day of the year and the week: the date.
            Assert.Equal("3/7/2020", DxfTableDate.Text(new DateTime(2020, 3, 7, 15, 4, 5), format, En));
        }
    }
}
