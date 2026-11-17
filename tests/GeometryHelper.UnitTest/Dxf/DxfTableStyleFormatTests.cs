using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The kind of value, its unit and the format string the cells of a table style hold (groups 90, 91 and 1 of each cell
    /// style), as AutoCAD's <c>GetDataType</c>, <c>SetDataType</c>, <c>GetFormat</c> and <c>SetFormat</c> give them: the
    /// general, unitless and empty a style starts with, a currency of two places set for the data alone, and the same read back
    /// from the file written.
    /// </summary>
    public class DxfTableStyleFormatTests
    {
        private static DxfDatabase Database(out DxfTableStyle style)
        {
            var db = new DxfDatabase("AC1032");

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            style = new DxfTableStyle();
            db.TableStyleDictionary.SetAt("GH_PRICES", style);
            return db;
        }

        [Fact]
        public void AStyleMadeInCode_HoldsGeneralDataUnitlessAndNoFormat()
        {
            Database(out DxfTableStyle style);

            foreach (DxfRowType kind in new[] { DxfRowType.TitleRow, DxfRowType.HeaderRow, DxfRowType.DataRow })
            {
                Assert.Equal(DxfDataType.General, style.DataType(kind).DataType);
                Assert.Equal(DxfUnitType.Unitless, style.DataType(kind).UnitType);
                Assert.Equal(string.Empty, style.Format(kind));
            }
        }

        [Fact]
        public void ACurrencySetForTheData_IsReadBack_AndLeavesTheOtherRowsAlone_AndSurvivesTheFile()
        {
            DxfDatabase db = Database(out DxfTableStyle style);

            style.SetDataType(DxfDataType.Double, DxfUnitType.Currency, DxfRowType.DataRow);
            style.SetFormat("%lu2%pr2%ps[$,]", DxfRowType.DataRow);

            Assert.Equal(DxfDataType.Double, style.DataType(DxfRowType.DataRow).DataType);
            Assert.Equal(DxfUnitType.Currency, style.DataType(DxfRowType.DataRow).UnitType);
            Assert.Equal("%lu2%pr2%ps[$,]", style.Format(DxfRowType.DataRow));
            Assert.Equal(DxfDataType.General, style.DataType(DxfRowType.HeaderRow).DataType);
            Assert.Equal(string.Empty, style.Format(DxfRowType.TitleRow));

            var again = DxfDatabase.Parse(db.ToText());
            var read = (DxfTableStyle)again.TableStyleDictionary["GH_PRICES"];
            Assert.Equal(DxfDataType.Double, read.DataType(DxfRowType.DataRow).DataType);
            Assert.Equal(DxfUnitType.Currency, read.DataType(DxfRowType.DataRow).UnitType);
            Assert.Equal("%lu2%pr2%ps[$,]", read.Format(DxfRowType.DataRow));
        }

        [Fact]
        public void SeveralRowTypesAtOnce_TakeTheSameFormat_AndANullFormatIsRefused()
        {
            Database(out DxfTableStyle style);

            style.SetFormat("%lu2", DxfRowType.HeaderRow | DxfRowType.DataRow);

            Assert.Equal("%lu2", style.Format(DxfRowType.HeaderRow));
            Assert.Equal("%lu2", style.Format(DxfRowType.DataRow));
            Assert.Equal(string.Empty, style.Format(DxfRowType.TitleRow));
            Assert.Throws<ArgumentNullException>(() => style.SetFormat(null, DxfRowType.DataRow));
        }
    }
}
