using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>Reset to defaults of Global settings puts the panels back to their first widths once it is applied.</summary>
    public sealed class PanelLayoutResetTests
    {
        [Fact]
        public void ResetThenApply_PutsThePanelsBack_OnceAndOnlyThen()
        {
            var model = new MainViewModel();
            int resets = 0;
            model.PanelLayoutReset += (s, e) => resets++;
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings(null);

            Assert.True(dialog.Apply());
            Assert.Equal(0, resets);

            dialog.Reset();
            Assert.Equal(0, resets);

            Assert.True(dialog.Apply());
            Assert.Equal(1, resets);

            Assert.True(dialog.Apply());
            Assert.Equal(1, resets);
        }
    }
}
