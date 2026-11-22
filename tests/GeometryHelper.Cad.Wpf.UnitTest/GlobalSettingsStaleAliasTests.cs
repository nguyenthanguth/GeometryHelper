using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A settings file saved before Import acad.pgp left out the aliases that are command names can hold DTEXT as an alias of
    /// TEXT, though DTEXT is a command of its own and a name wins over an alias. Global settings used to open with that alias in
    /// red on the Shortcuts page, which kept Apply and OK off on every page (L9 row M3, the maintainer's own file).
    /// </summary>
    public class GlobalSettingsStaleAliasTests
    {
        private static GlobalSettingsViewModel Dialog()
        {
            var settings = new GlobalSettings();
            settings.SetShortcuts(
                settings.Aliases.Concat(new[] { new KeyValuePair<string, string>("DTEXT", "TEXT") }),
                settings.Keys
            );
            var model = new MainViewModel();
            model.ApplySettings(settings);
            return model.CreateGlobalSettings("Security");
        }

        [Fact]
        public void AnAliasThatIsAnotherCommandsName_IsLeftOutOfTheDialog_SoApplyAndOkAreNotHeld()
        {
            GlobalSettingsViewModel dialog = Dialog();

            Assert.False(dialog.HasErrors);
            Assert.True(dialog.ApplyCommand.CanExecute(null));
            Assert.True(dialog.OkCommand.CanExecute(null));
            Assert.DoesNotContain("DTEXT", dialog.Shortcuts.Single(r => r.Command == "TEXT").Aliases);
        }

        [Fact]
        public void TheOtherAliasesOfTheCommand_StayInTheDialog()
        {
            GlobalSettingsViewModel dialog = Dialog();

            Assert.Equal("DT", dialog.Shortcuts.Single(r => r.Command == "TEXT").Aliases);
        }
    }
}
