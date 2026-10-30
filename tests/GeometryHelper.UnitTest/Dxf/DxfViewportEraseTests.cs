using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The viewports of a layout erased as AutoCAD erases them: the viewport 1 that shows the sheet itself refused, and a layout
    /// whose last active viewport was erased no longer pointing at it.
    /// </summary>
    public class DxfViewportEraseTests
    {
        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheViewportThatShowsTheSheetItself_IsNotErased_AndTheDrawingIsLeftAsItWas(string version)
        {
            string text = version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport sheet = ByHandle<DxfViewport>(db, "3F");
            Assert.Equal(1, sheet.Number);

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => sheet.Erase());

            Assert.Contains("viewport 1", refused.Message, StringComparison.Ordinal);
            Assert.False(sheet.IsErased);
            Assert.Equal(text, db.ToText());
            Assert.Equal(new[] { 1, 3, -1 }, db.Layouts[2].GetViewports().Select(v => v.Number));
        }

        [Fact]
        public void AViewportErased_ThatWasItsLayoutsLastActive_IsNoLongerPointedAtByTheLayout()
        {
            // The layout Sheet A made to name its viewport 40 as the one last active (331): erased, the layout names its
            // viewport 1 (3F) instead, as AutoCAD 2021 does.
            string text = DxfObjectsEzdxfDrawings.R2018.Replace("330\n3B\n331\n3F\n", "330\n3B\n331\n40\n");
            Assert.NotEqual(DxfObjectsEzdxfDrawings.R2018, text);
            DxfDatabase db = DxfDatabase.Parse(text);

            ByHandle<DxfViewport>(db, "40").Erase();

            string layout = Records(db.ToText()).Single(r => HandleOf(r) == "3E");
            Assert.DoesNotContain("\n331\n40\n", layout, StringComparison.Ordinal);
            Assert.Contains("\n330\n3B\n331\n3F\n", layout, StringComparison.Ordinal);
            Assert.Equal("Sheet A", DxfDatabase.Parse(db.ToText()).Layouts[2].LayoutName);
        }

        [Fact]
        public void AViewportErased_ThatNoLayoutNamesLastActive_LeavesTheLayoutsAsTheyWere()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            ByHandle<DxfViewport>(db, "41").Erase();

            string layout = Records(db.ToText()).Single(r => HandleOf(r) == "3E");
            Assert.Equal(Records(text).Single(r => HandleOf(r) == "3E"), layout);
        }
    }
}
