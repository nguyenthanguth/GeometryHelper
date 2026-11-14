using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A number of a format of units, <c>%lu</c> with its unit 1 to 6, <c>%pr</c> its places and <c>%zs</c> its zeros, shown
    /// as AutoCAD 2021 showed it (side database of the console, <c>SetValue</c> then <c>SetDataFormat</c>, under each LUNITS
    /// and LUPREC): the unit 6 is the drawing's, and a format of no places takes the drawing's.
    /// </summary>
    public class DxfTableUnitsFormatTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        [Theory]
        [InlineData(3, 4, 12.3456, "%lu2", "12.3456")]
        [InlineData(3, 4, -0.5, "%lu2", "-0.5000")]
        [InlineData(3, 4, 11.99999, "%lu2", "12.0000")]
        [InlineData(3, 4, 12.3456, "%lu3", "1'-0.3456\"")]
        [InlineData(3, 4, -0.5, "%lu3", "-0'-0.5000\"")]
        [InlineData(3, 4, 11.99999, "%lu3", "1'-0.0000\"")]
        [InlineData(3, 4, 12.3456, "%lu4", "1'-0 3/8\"")]
        [InlineData(3, 4, -0.5, "%lu4", "-0'-0 1/2\"")]
        [InlineData(3, 4, 11.99999, "%lu4", "1'-0\"")]
        [InlineData(3, 4, 12.3456, "%lu6", "1'-0.3456\"")]
        [InlineData(3, 4, -0.5, "%lu6", "-0'-0.5000\"")]
        [InlineData(3, 4, 11.99999, "%lu6", "1'-0.0000\"")]
        [InlineData(3, 4, 12.3456, "%lu1%pr0", "1E+01")]
        [InlineData(3, 4, -0.5, "%lu1%pr0", "-5E-01")]
        [InlineData(3, 4, 11.99999, "%lu1%pr0", "1E+01")]
        [InlineData(3, 4, 12.3456, "%lu1%pr2", "1.23E+01")]
        [InlineData(3, 4, -0.5, "%lu1%pr2", "-5.00E-01")]
        [InlineData(3, 4, 11.99999, "%lu1%pr2", "1.20E+01")]
        [InlineData(3, 4, 12.3456, "%lu3%pr0", "1'-0\"")]
        [InlineData(3, 4, -0.5, "%lu3%pr0", "-0'-1\"")]
        [InlineData(3, 4, 11.99999, "%lu3%pr0", "1'-0\"")]
        [InlineData(3, 4, 12.3456, "%lu3%pr2", "1'-0.35\"")]
        [InlineData(3, 4, -0.5, "%lu3%pr2", "-0'-0.50\"")]
        [InlineData(3, 4, 11.99999, "%lu3%pr2", "1'-0.00\"")]
        [InlineData(3, 4, 12.3456, "%lu3%pr6", "1'-0.345600\"")]
        [InlineData(3, 4, -0.5, "%lu3%pr6", "-0'-0.500000\"")]
        [InlineData(3, 4, 11.99999, "%lu3%pr6", "0'-11.999990\"")]
        [InlineData(3, 4, 12.3456, "%lu5%pr0", "12")]
        [InlineData(3, 4, -0.5, "%lu5%pr0", "-1")]
        [InlineData(3, 4, 11.99999, "%lu5%pr0", "12")]
        [InlineData(3, 4, 12.3456, "%lu5%pr2", "12 1/4")]
        [InlineData(3, 4, -0.5, "%lu5%pr2", "-1/2")]
        [InlineData(3, 4, 11.99999, "%lu5%pr2", "12")]
        [InlineData(3, 4, 12.3456, "%lu6%pr0", "1'-0\"")]
        [InlineData(3, 4, -0.5, "%lu6%pr0", "-0'-1\"")]
        [InlineData(3, 4, 11.99999, "%lu6%pr0", "1'-0\"")]
        [InlineData(3, 4, 12.3456, "%lu6%pr2", "1'-0.35\"")]
        [InlineData(3, 4, -0.5, "%lu6%pr2", "-0'-0.50\"")]
        [InlineData(3, 4, 11.99999, "%lu6%pr2", "1'-0.00\"")]
        [InlineData(3, 4, 12.3456, "%lu3%zs1", "1'-0.3456\"")]
        [InlineData(3, 4, -0.5, "%lu3%zs1", "-0.5000\"")]
        [InlineData(3, 4, 11.99999, "%lu3%zs1", "1'-0.0000\"")]
        [InlineData(3, 4, 12.3456, "%lu3%zs2", "1'-0.3456\"")]
        [InlineData(3, 4, -0.5, "%lu3%zs2", "-0'-0.5000\"")]
        [InlineData(3, 4, 11.99999, "%lu3%zs2", "1'")]
        [InlineData(3, 4, 12.3456, "%lu3%pr2%zs3", "1'-0.35\"")]
        [InlineData(3, 4, -0.5, "%lu3%pr2%zs3", "-0.50\"")]
        [InlineData(3, 4, 11.99999, "%lu3%pr2%zs3", "1'")]
        [InlineData(3, 4, 12.3456, "%lu6%zs1", "1'-0.3456\"")]
        [InlineData(3, 4, -0.5, "%lu6%zs1", "-0.5000\"")]
        [InlineData(3, 4, 11.99999, "%lu6%zs1", "1'-0.0000\"")]
        [InlineData(3, 2, 12.3456, "%lu2", "12.35")]
        [InlineData(3, 2, -0.5, "%lu2", "-0.50")]
        [InlineData(3, 2, 12.3456, "%lu3", "1'-0.35\"")]
        [InlineData(3, 2, -0.5, "%lu3", "-0'-0.50\"")]
        [InlineData(3, 2, 12.3456, "%lu4", "1'-0 1/4\"")]
        [InlineData(3, 2, -0.5, "%lu4", "-0'-0 1/2\"")]
        [InlineData(3, 2, 12.3456, "%lu6", "1'-0.35\"")]
        [InlineData(3, 2, -0.5, "%lu6", "-0'-0.50\"")]
        [InlineData(3, 2, 12.3456, "%lu6%pr0", "1'-0\"")]
        [InlineData(3, 2, -0.5, "%lu6%pr0", "-0'-1\"")]
        [InlineData(3, 2, 12.3456, "%lu3%pr2%zs3", "1'-0.35\"")]
        [InlineData(3, 2, -0.5, "%lu3%pr2%zs3", "-0.50\"")]
        [InlineData(3, 2, 12.3456, "%lu6%zs1", "1'-0.35\"")]
        [InlineData(3, 2, -0.5, "%lu6%zs1", "-0.50\"")]
        [InlineData(1, 4, 12.3456, "%lu2", "12.3456")]
        [InlineData(1, 4, -0.5, "%lu2", "-0.5000")]
        [InlineData(1, 4, 12.3456, "%lu6", "1.2346E+01")]
        [InlineData(1, 4, -0.5, "%lu6", "-5.0000E-01")]
        [InlineData(1, 4, 12.3456, "%lu6%pr0", "1E+01")]
        [InlineData(1, 4, -0.5, "%lu6%pr0", "-5E-01")]
        [InlineData(1, 4, 12.3456, "%lu6%pr2", "1.23E+01")]
        [InlineData(1, 4, -0.5, "%lu6%pr2", "-5.00E-01")]
        [InlineData(4, 2, 12.3456, "%lu4", "1'-0 1/4\"")]
        [InlineData(4, 2, 11.99999, "%lu4", "1'-0\"")]
        [InlineData(4, 2, 12.3456, "%lu6", "1'-0 1/4\"")]
        [InlineData(4, 2, 11.99999, "%lu6", "1'-0\"")]
        [InlineData(4, 2, 12.3456, "%lu6%pr0", "1'-0\"")]
        [InlineData(4, 2, 11.99999, "%lu6%pr0", "1'-0\"")]
        [InlineData(5, 2, 12.3456, "%lu6", "12 1/4")]
        [InlineData(5, 2, -0.5, "%lu6", "-1/2")]
        [InlineData(5, 2, 12.3456, "%lu5%pr2", "12 1/4")]
        [InlineData(5, 2, -0.5, "%lu5%pr2", "-1/2")]
        [InlineData(5, 2, 12.3456, "%lu6%pr0", "12")]
        [InlineData(5, 2, -0.5, "%lu6%pr0", "-1")]
        public void ANumberOfAFormatOfUnits_IsShownAsAutoCadShowsIt_UnderTheDrawingsUnitsAndPlaces(
            int lunits,
            int luprec,
            double value,
            string format,
            string shown
        )
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            db.Lunits = lunits;
            db.Luprec = luprec;
            DxfTable table = Table(db);
            table.SetValue(2, 0, value);
            table.SetFormat(2, 0, format);

            Assert.Equal(shown, table.TextString(2, 0));
            Assert.Equal(format, table.Format(2, 0));
            Assert.Equal(shown, Table(DxfDatabase.Parse(db.ToText())).TextString(2, 0));
        }
    }
}
