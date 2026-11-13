using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The Viewports dialog starting with what -VPORTS MOde set: Display, or the current viewport.</summary>
    public class ViewportsDialogModeTests
    {
        [Fact]
        public void ApplyTo_StartsWithTheModeVportsSet()
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), "mode.dxf");
            CadTiledViewports tiles = document.TiledViewports;

            Assert.Equal("Display", new ViewportsViewModel(tiles).ApplyTo);

            tiles.AppliesToCurrentViewport = true;
            Assert.Equal("Current Viewport", new ViewportsViewModel(tiles).ApplyTo);

            tiles.AppliesToCurrentViewport = false;
            Assert.Equal("Display", new ViewportsViewModel(tiles).ApplyTo);
        }
    }
}
