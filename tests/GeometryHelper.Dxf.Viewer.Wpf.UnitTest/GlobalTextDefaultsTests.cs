using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The text height and width factor of Global settings given to the command line of each drawing.</summary>
    public sealed class GlobalTextDefaultsTests
    {
        [Fact]
        public void GlobalSettingsAppliedGiveTheCommandLineTheWidthFactorAndTheHeightOfTheDimensionText()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
            Assert.False(model.Session.Remembered.ContainsKey("text.widthFactor"));

            model.ApplySettings(new GlobalSettings { TextHeight = 3.5, WidthFactor = 0.6 });

            Assert.Equal(0.6, model.Session.Remembered["text.widthFactor"]);
            Assert.Equal(3.5, model.Session.Remembered["dim.text.height"]);
            Assert.Equal(3.5, model.Session.Remembered["text.height"]);
        }
    }
}
