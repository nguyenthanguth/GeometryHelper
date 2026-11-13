using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Every command the Shortcuts page of Global settings lists says in a few words what it does, so that a drafter looking
    /// for a key to give finds the command by its purpose as well as by its name.
    /// </summary>
    public class ShortcutDescriptionTests
    {
        [Fact]
        public void EveryCommandOfTheShortcutsPage_HasADescription()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            dialog.ShortcutSearch = string.Empty;

            string[] missing = dialog
                .Shortcuts.Where(r => string.IsNullOrWhiteSpace(r.Description))
                .Select(r => r.Command)
                .ToArray();

            Assert.True(missing.Length == 0, "No description: " + string.Join(" ", missing));
        }

        [Fact]
        public void TheDescriptions_AreShortAndStartWithACapital()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");

            Assert.All(dialog.Shortcuts.Where(r => r.Description.Length > 0), r =>
            {
                Assert.True(r.Description.Length <= 60, r.Command + ": " + r.Description);
                Assert.True(char.IsUpper(r.Description[0]), r.Command + ": " + r.Description);
            });
        }
    }
}
