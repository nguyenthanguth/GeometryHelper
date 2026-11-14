using System;
using System.Globalization;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A date or a time typed in a table's cell, as AutoCAD 2021 read it (D41: the console's <c>SetValue</c> of a text with
    /// <c>ConvertTextToValue</c> in a side database, in en-US, and the DXF it wrote): which words are a date, the kind 8 with
    /// its 16 bytes, the format <c>%x</c>, <c>%X</c> or <c>%c</c> and the words shown and opened.
    /// </summary>
    public class DxfTableDateTests
    {
        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

        /// <summary>A table of AutoCAD's with its cell (2, 0) emptied to no kind of value, so that a text typed takes the kind it reads as.</summary>
        private static DxfTable Fresh(DxfDatabase db)
        {
            DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
            table.ResetValue(2, 0);
            return table;
        }

        private static void In(CultureInfo culture, Action act)
        {
            CultureInfo was = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;

            try
            {
                act();
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }

        [Theory]
        [InlineData("1/1/2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("01/01/2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1/1/20", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1/1/49", "2049-01-01T00:00:00", "%x", "1/1/2049", "1/1/2049")]
        [InlineData("1/1/50", "1950-01-01T00:00:00", "%x", "1/1/1950", "1/1/1950")]
        [InlineData("1/1/99", "1999-01-01T00:00:00", "%x", "1/1/1999", "1/1/1999")]
        [InlineData("1/1/00", "2000-01-01T00:00:00", "%x", "1/1/2000", "1/1/2000")]
        [InlineData(" 1/1/2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1/1/ 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1 /1/2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1/1/2020 12:30", "2020-01-01T12:30:00", "%c", "1/1/2020 12:30:00 PM", "1/1/2020 12:30:00 PM")]
        [InlineData("1/1/2020  12:30", "2020-01-01T12:30:00", "%c", "1/1/2020 12:30:00 PM", "1/1/2020 12:30:00 PM")]
        [InlineData("1/1/2020 12:30:45", "2020-01-01T12:30:45", "%c", "1/1/2020 12:30:45 PM", "1/1/2020 12:30:45 PM")]
        [InlineData("1/1/2020 12:30 PM", "2020-01-01T12:30:00", "%c", "1/1/2020 12:30:00 PM", "1/1/2020 12:30:00 PM")]
        [InlineData("1/1/2020 12:30PM", "2020-01-01T12:30:00", "%c", "1/1/2020 12:30:00 PM", "1/1/2020 12:30:00 PM")]
        [InlineData("1/1/2020 12 PM", "2020-01-01T12:00:00", "%c", "1/1/2020 12:00:00 PM", "1/1/2020 12:00:00 PM")]
        [InlineData("1/1/2020 23:59:59", "2020-01-01T23:59:59", "%c", "1/1/2020 11:59:59 PM", "1/1/2020 11:59:59 PM")]
        [InlineData("1/1/2020 0:00:01", "2020-01-01T00:00:01", "%c", "1/1/2020 12:00:01 AM", "1/1/2020 12:00:01 AM")]
        [InlineData("1/1/2020 00:00:00", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("12:30", "1899-12-30T12:30:00", "%X", "12:30:00 PM", "12:30:00 PM")]
        [InlineData("12:30:45", "1899-12-30T12:30:45", "%X", "12:30:45 PM", "12:30:45 PM")]
        [InlineData("12:30 PM", "1899-12-30T12:30:00", "%X", "12:30:00 PM", "12:30:00 PM")]
        [InlineData("12:30PM", "1899-12-30T12:30:00", "%X", "12:30:00 PM", "12:30:00 PM")]
        [InlineData("12 PM", "1899-12-30T12:00:00", "%X", "12:00:00 PM", "12:00:00 PM")]
        [InlineData("12PM", "1899-12-30T12:00:00", "%X", "12:00:00 PM", "12:00:00 PM")]
        [InlineData("1 AM", "1899-12-30T01:00:00", "%X", "1:00:00 AM", "1:00:00 AM")]
        [InlineData("0:00", "1899-12-30T00:00:00", "%x", "12/30/1899", "12/30/1899")]
        [InlineData("00:00", "1899-12-30T00:00:00", "%x", "12/30/1899", "12/30/1899")]
        [InlineData("00:00:00", "1899-12-30T00:00:00", "%x", "12/30/1899", "12/30/1899")]
        [InlineData("23:59:59", "1899-12-30T23:59:59", "%X", "11:59:59 PM", "11:59:59 PM")]
        [InlineData("1:5", "1899-12-30T01:05:00", "%X", "1:05:00 AM", "1:05:00 AM")]
        [InlineData("1:05", "1899-12-30T01:05:00", "%X", "1:05:00 AM", "1:05:00 AM")]
        [InlineData("1:2:3", "1899-12-30T01:02:03", "%X", "1:02:03 AM", "1:02:03 AM")]
        [InlineData("2/29/2000", "2000-02-29T00:00:00", "%x", "2/29/2000", "2/29/2000")]
        [InlineData("12/31/9999", "9999-12-31T00:00:00", "%x", "12/31/9999", "12/31/9999")]
        [InlineData("1/1/100", "0100-01-01T00:00:00", "%x", "1/1/0100", "1/1/0100")]
        [InlineData("1/1/1", "2001-01-01T00:00:00", "%x", "1/1/2001", "1/1/2001")]
        [InlineData("1-2-3", "2003-01-02T00:00:00", "%x", "1/2/2003", "1/2/2003")]
        [InlineData("3-4-2020", "2020-03-04T00:00:00", "%x", "3/4/2020", "3/4/2020")]
        [InlineData("3-4-20", "2020-03-04T00:00:00", "%x", "3/4/2020", "3/4/2020")]
        [InlineData("2020-01-01", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("2020-1-1", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("2020-01-01 12:30", "2020-01-01T12:30:00", "%c", "1/1/2020 12:30:00 PM", "1/1/2020 12:30:00 PM")]
        [InlineData("2020/01/01", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("2020/1/1", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("2020-01", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("Jan 1 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("Jan 1, 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("Jan 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("January 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1 Jan 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("1-Jan-2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("Jan-1-2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("January 1, 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData(
            "January 1 2020 12:30",
            "2020-01-01T12:30:00",
            "%c",
            "1/1/2020 12:30:00 PM",
            "1/1/2020 12:30:00 PM"
        )]
        [InlineData("Sep 1 2020", "2020-09-01T00:00:00", "%x", "9/1/2020", "9/1/2020")]
        [InlineData("1 PM Jan 1 2020", "2020-01-01T13:00:00", "%c", "1/1/2020 1:00:00 PM", "1/1/2020 1:00:00 PM")]
        [InlineData("5 Jan 2020 12:30 AM", "2020-01-05T00:30:00", "%c", "1/5/2020 12:30:00 AM", "1/5/2020 12:30:00 AM")]
        [InlineData("Jan 5, 2020 5:00 PM", "2020-01-05T17:00:00", "%c", "1/5/2020 5:00:00 PM", "1/5/2020 5:00:00 PM")]
        [InlineData("12/30/1899", "1899-12-30T00:00:00", "%x", "12/30/1899", "12/30/1899")]
        [InlineData("12/29/1899", "1899-12-29T00:00:00", "%x", "12/29/1899", "12/29/1899")]
        [InlineData("12/31/1899", "1899-12-31T00:00:00", "%x", "12/31/1899", "12/31/1899")]
        [InlineData("1/1/1800", "1800-01-01T00:00:00", "%x", "1/1/1800", "1/1/1800")]
        [InlineData("1/1/1000", "1000-01-01T00:00:00", "%x", "1/1/1000", "1/1/1000")]
        [InlineData("1/1/1752", "1752-01-01T00:00:00", "%x", "1/1/1752", "1/1/1752")]
        [InlineData("9/2/1752", "1752-09-02T00:00:00", "%x", "9/2/1752", "9/2/1752")]
        [InlineData("9/14/1752", "1752-09-14T00:00:00", "%x", "9/14/1752", "9/14/1752")]
        [InlineData("1 / 1 / 2020", "2020-01-01T00:00:00", "%x", "1/1/2020", "1/1/2020")]
        [InlineData("12:30 1/1/2020", "2020-01-01T12:30:00", "%c", "1/1/2020 12:30:00 PM", "1/1/2020 12:30:00 PM")]
        [InlineData("12:30 AM", "1899-12-30T00:30:00", "%X", "12:30:00 AM", "12:30:00 AM")]
        [InlineData("12:30 am", "1899-12-30T00:30:00", "%X", "12:30:00 AM", "12:30:00 AM")]
        [InlineData("12:00 AM", "1899-12-30T00:00:00", "%x", "12/30/1899", "12/30/1899")]
        [InlineData("12:00 PM", "1899-12-30T12:00:00", "%X", "12:00:00 PM", "12:00:00 PM")]
        [InlineData("00:30 AM", "1899-12-30T00:30:00", "%X", "12:30:00 AM", "12:30:00 AM")]
        [InlineData("13:30 PM", "1899-12-30T13:30:00", "%X", "1:30:00 PM", "1:30:00 PM")]
        [InlineData("0:30 PM", "1899-12-30T12:30:00", "%X", "12:30:00 PM", "12:30:00 PM")]
        public void ADateOrATimeTyped_IsADateOfItsFormat_ShownAndOpenedAsAutoCadShowsIt(
            string text,
            string iso,
            string format,
            string shown,
            string editing
        )
        {
            In(En, () =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Fresh(db);

                Assert.True(table.SetValueFromText(2, 0, text));
                Assert.Equal(DxfDataType.Date, table.DataType(2, 0));
                Assert.Equal(format, table.Format(2, 0));
                Assert.Equal(shown, table.TextString(2, 0));
                Assert.Equal(editing, table.TextStringForEditing(2, 0));
                Assert.True(DxfTableDate.TryRead(text, En, out DateTime read));
                Assert.Equal(iso, read.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                Assert.Equal(shown, DxfAutoCadChecksD16Tests.Table(DxfDatabase.Parse(db.ToText())).TextString(2, 0));
            });
        }

        [Theory]
        [InlineData("1/1/2020", "E4070100030001000000000000000000", "%x")]
        [InlineData("12:30", "6B070C0006001E000C001E0000000000", "%X")]
        [InlineData("12:30:45", "6B070C0006001E000C001E002D000000", "%X")]
        [InlineData("1:5", "6B070C0006001E000100050000000000", "%X")]
        [InlineData("0:00", "6B070C0006001E000000000000000000", "%x")]
        [InlineData("1/1/2020 12:30", "E4070100030001000C001E0000000000", "%c")]
        [InlineData("7/4/2020 11:59:59 PM", "E40707000600040017003B003B000000", "%c")]
        [InlineData("1/1/1800", "08070100030001000000000000000000", "%x")]
        [InlineData("1/1/9999", "0F270100050001000000000000000000", "%x")]
        public void ADateTyped_IsWrittenAsAutoCadWroteIt_TheKind8_ItsSixteenBytes_AndTheFlags4(
            string text,
            string bytes,
            string format
        )
        {
            In(En, () =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Fresh(db);
                DxfCellAlignment? alignment = table.Alignment(2, 0);
                Assert.True(table.SetValueFromText(2, 0, text));

                // D41: 91 262144 on the cell, 93 4, 90 8, 92 16 and the 310 of the date, no 140 and no alignment of its own.
                Assert.Contains(
                    "301\nCELL_VALUE\n 93\n4\n 90\n8\n 92\n16\n310\n" + bytes + "\n 94\n0\n300\n" + format + "\n302\n",
                    DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6"),
                    StringComparison.Ordinal
                );
                Assert.Equal(alignment, table.Alignment(2, 0));
            });
        }

        [Theory]
        [InlineData("2/30/2020")]
        [InlineData("2/29/2019")]
        [InlineData("13/13/2020")]
        [InlineData("1/32/2020")]
        [InlineData("24:00")]
        [InlineData("1:60")]
        [InlineData("1/1/2020 24:00")]
        [InlineData("12:30:45.5")]
        [InlineData("1/1/2020 12:30:45.5")]
        [InlineData("12:30 P.M.")]
        [InlineData("3.4.2020")]
        [InlineData("2020-01-01T12:30:00")]
        [InlineData("2020-01-01T00:00:00Z")]
        [InlineData("Sat Jan 4 2020")]
        [InlineData("Saturday, January 4, 2020")]
        [InlineData("Monday")]
        [InlineData("Jan")]
        [InlineData("now")]
        [InlineData("12:30Z")]
        [InlineData("12:30 UTC")]
        [InlineData("1/1/2020a")]
        [InlineData("1/1/ABC")]
        [InlineData("1/1/10000")]
        [InlineData("2/29/1900")]
        [InlineData("+1/1/2020")]
        [InlineData("(1/1/2020)")]
        public void WordsThatAreNoDateOrTime_StayWords_AsAutoCadKeepsThem(string text)
        {
            In(En, () =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Fresh(db);

                Assert.False(DxfTableDate.TryRead(text, En, out DateTime _));
                Assert.False(table.SetValueFromText(2, 0, text));
            });
        }

        [Fact]
        public void ADateOverACellOfADate_KeepsItsFormat_AndShowsTheDateInIt()
        {
            In(En, () =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Fresh(db);
                Assert.True(table.SetValueFromText(2, 0, "1/1/2020"));
                table.SetFormat(2, 0, "%d/%m/%Y");

                // D41: the format stays, the flags stay; 3/7/2020 in %d/%m/%Y is 07/03/2020, and it opens as the date alone.
                Assert.True(table.SetValueFromText(2, 0, "3/7/2020"));
                Assert.Equal("%d/%m/%Y", table.Format(2, 0));
                Assert.Equal("07/03/2020", table.TextString(2, 0));
                Assert.Equal("3/7/2020", table.TextStringForEditing(2, 0));

                // A time with it opens as both, and the format %c shows both, the format %X the time alone.
                Assert.True(table.SetValueFromText(2, 0, "3/7/2020 15:04:05"));
                Assert.Equal("07/03/2020", table.TextString(2, 0));
                Assert.Equal("3/7/2020 3:04:05 PM", table.TextStringForEditing(2, 0));
                table.SetFormat(2, 0, "%c");
                Assert.Equal("3/7/2020 3:04:05 PM", table.TextString(2, 0));
                table.SetFormat(2, 0, "%X");
                Assert.Equal("3:04:05 PM", table.TextString(2, 0));

                // A time over a date is on the day of the times, 12/30/1899, which the date format shows and the editor does not.
                Assert.True(table.SetValueFromText(2, 0, "12:30"));
                Assert.Equal("12:30:00 PM", table.TextString(2, 0));
                Assert.Equal("12:30:00 PM", table.TextStringForEditing(2, 0));
                table.SetFormat(2, 0, "%x");
                Assert.Equal("12/30/1899", table.TextString(2, 0));
            });
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("5")]
        [InlineData("1.5")]
        [InlineData("1,5")]
        [InlineData("2/30/2020")]
        public void AnythingButADateOverACellOfADate_IsRefused_AsAutoCadThrowsEInvalidInput(string text)
        {
            In(En, () =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Fresh(db);
                Assert.True(table.SetValueFromText(2, 0, "1/1/2020"));

                Assert.True(table.RefusesText(2, 0, text));
                Assert.False(table.SetValueFromText(2, 0, text));
                Assert.Equal("1/1/2020", table.TextString(2, 0));
                Assert.False(table.RefusesText(2, 0, "3/4/2020"));
            });
        }

        [Fact]
        public void ADateTyped_ReadsInTheOrderOfTheCulture_AndAPointCanBeItsMark()
        {
            // The order of the culture: 3/4/2020 is the 4th of March in en-US and the 3rd of April in en-GB; in de-DE a point
            // is the mark, and 3.4.2020 the 3rd of April, shown 03.04.2020.
            In(CultureInfo.GetCultureInfo("en-GB"), () =>
            {
                Assert.True(DxfTableDate.TryRead("3/4/2020", CultureInfo.GetCultureInfo("en-GB"), out DateTime d));
                Assert.Equal(new DateTime(2020, 4, 3), d);
            });
            CultureInfo german = CultureInfo.GetCultureInfo("de-DE");
            In(german, () =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Fresh(db);
                Assert.True(table.SetValueFromText(2, 0, "3.4.2020"));
                Assert.Equal(DxfDataType.Date, table.DataType(2, 0));
                Assert.Equal("03.04.2020", table.TextString(2, 0));
                Assert.Equal("03.04.2020", table.TextStringForEditing(2, 0));
            });
            Assert.False(DxfTableDate.TryRead("3.4.2020", En, out DateTime _));
        }

        [Theory]
        [InlineData("%A, %B %d, %Y", "Saturday, March 07, 2020")]
        [InlineData("%a %b %e", "Sat Mar  7")]
        [InlineData("%d/%m/%Y", "07/03/2020")]
        [InlineData("%Y-%m-%d", "2020-03-07")]
        [InlineData("%H:%M", "15:04")]
        [InlineData("%I:%M %p", "03:04 PM")]
        [InlineData("%D", "03/07/20")]
        [InlineData("%T", "15:04:05")]
        [InlineData("%R", "15:04")]
        [InlineData("%x %X", "3/7/2020 3:04:05 PM")]
        [InlineData("%%", "%")]
        [InlineData("M/d/yyyy", "3/7/2020")]
        [InlineData("abc", "abc")]
        public void ADateInAFormatOfLetters_IsShownAsAutoCadsStrftimeShowsIt(string format, string shown)
        {
            // D41: 3/7/2020 3:04:05 PM in each format, SetDataFormat then GetTextString.
            Assert.Equal(shown, DxfTableDate.Text(new DateTime(2020, 3, 7, 15, 4, 5), format, En));
        }
    }
}
