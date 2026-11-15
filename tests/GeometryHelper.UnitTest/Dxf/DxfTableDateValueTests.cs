using System;
using System.Globalization;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The value of a cell that holds a date or a time, as AutoCAD 2021's console gave it (D42, <c>Table.GetValue</c> and
    /// <c>Cell.Value</c> of a cell typed with <c>SetValue</c> of a text and <c>ConvertTextToValue</c>, in en-US): a
    /// <see cref="DateTime"/>, a time on the 30th of December 1899, where the library gave the words the cell shows.
    /// </summary>
    public class DxfTableDateValueTests
    {
        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

        private static DxfTable Typed(string text, out DxfDatabase db)
        {
            CultureInfo was = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = En;

            try
            {
                db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
                table.ResetValue(2, 0);
                Assert.True(table.SetValueFromText(2, 0, text));
                return table;
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }

        [Theory]
        [InlineData("1/1/2020", "2020-01-01T00:00:00")]
        [InlineData("12:30", "1899-12-30T12:30:00")]
        [InlineData("1/1/2020 12:30", "2020-01-01T12:30:00")]
        [InlineData("12/30/1899", "1899-12-30T00:00:00")]
        [InlineData("2/29/2020 23:59", "2020-02-29T23:59:00")]
        [InlineData("6/15/2021 8:05", "2021-06-15T08:05:00")]
        [InlineData("12:30:45", "1899-12-30T12:30:45")]
        public void TheValueOfADateOrATime_IsTheDateTimeAutoCadGave(string text, string iso)
        {
            DxfTable table = Typed(text, out _);

            object value = table.Value(2, 0);

            Assert.IsType<DateTime>(value);
            Assert.Equal(iso, ((DateTime)value).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            Assert.Equal(value, table.GetValue(2, 0, 0));
        }

        [Fact]
        public void TheValueOfADate_ReadAgainFromTheSavedFile_IsTheSameDateTime()
        {
            DxfTable table = Typed("1/1/2020 12:30", out DxfDatabase db);

            DxfTable again = DxfAutoCadChecksD16Tests.Table(DxfDatabase.Parse(db.ToText()));

            Assert.Equal(new DateTime(2020, 1, 1, 12, 30, 0), again.Value(2, 0));
        }

        [Fact]
        public void TheValueOfANumberAndOfAText_AreNotDates()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
            table.SetValue(2, 0, 12);
            table.SetValue(1, 0, 1.5);

            Assert.Equal(12, table.Value(2, 0));
            Assert.Equal(1.5, table.Value(1, 0));
            Assert.IsType<string>(table.Value(0, 0));
        }
    }
}
