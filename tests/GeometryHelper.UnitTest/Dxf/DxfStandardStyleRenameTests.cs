using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The Standard text style and the Standard dimension style renamed, as AutoCAD 2021's -RENAME renamed them in its console
    /// (the styles table lists only the new name afterwards).
    /// </summary>
    public class DxfStandardStyleRenameTests
    {
        [Fact]
        public void TheStandardTextStyle_IsRenamed()
        {
            var db = new DxfDatabase("AC1027");

            db.TextStyleTable["Standard"].Name = "XX";

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.TextStyleTable.Has("Standard"));
            Assert.True(read.TextStyleTable.Has("XX"));
        }

        [Fact]
        public void TheStandardDimensionStyle_IsRenamed()
        {
            var db = new DxfDatabase("AC1027");

            db.DimStyleTable["Standard"].Name = "YY";

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.DimStyleTable.Has("Standard"));
            Assert.True(read.DimStyleTable.Has("YY"));
        }
    }
}
