using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// OSNAP and its alias OS open Drafting Settings on the Object Snap tab, as AutoCAD 2021 does, and no longer ask the
    /// status bar's "Enter mode [ON/OFF]"; F3 and the status-bar button still toggle the object snaps with no dialog.
    /// The window is never made: the tests listen to <see cref="MainViewModel.DraftingSettingsRequested"/> instead.
    /// </summary>
    public sealed class OsnapCommandTests
    {
        private static MainViewModel Open(List<string> asked)
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "room.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            model.DraftingSettingsRequested += (s, e) => asked.Add(((DraftingSettingsRequestedEventArgs)e).Tab);
            return model;
        }

        private static bool OsnapIsOn(MainViewModel model) => model.Toggles.Single(t => t.Name == "OSNAP").IsOn;

        [Theory]
        [InlineData("OSNAP")]
        [InlineData("OS")]
        [InlineData("DDOSNAP")]
        public void TypingOsnapOrItsAliases_OpensDraftingSettingsOnTheObjectSnapTab_AndAsksNoMode(string typed)
        {
            var asked = new List<string>();
            MainViewModel model = Open(asked);

            model.Execute(typed);

            Assert.Equal(new[] { DraftingSettingsViewModel.ObjectSnapTab }, asked);
            Assert.False(model.IsCommandRunning);
            Assert.DoesNotContain(model.History, l => l.StartsWith("Enter mode", System.StringComparison.Ordinal));
            Assert.True(OsnapIsOn(model));
        }

        [Fact]
        public void OsnapTypedInsideLine_OpensDraftingSettings_AndLineGoesOn()
        {
            var asked = new List<string>();
            MainViewModel model = Open(asked);
            model.StartCommand("LINE");
            string prompt = model.Prompt;

            model.Execute("'OSNAP");

            Assert.Equal(new[] { DraftingSettingsViewModel.ObjectSnapTab }, asked);
            Assert.Contains("Resuming LINE command.", model.History);
            Assert.True(model.IsCommandRunning);
            Assert.Equal(prompt, model.Prompt);
            Assert.True(OsnapIsOn(model));
        }

        [Fact]
        public void F3AndTheStatusBarToggleTheObjectSnaps_WithNoDialog()
        {
            var asked = new List<string>();
            MainViewModel model = Open(asked);

            Assert.True(model.RunKey("F3"));
            Assert.False(OsnapIsOn(model));
            Assert.True(model.RunKey("F3"));
            Assert.True(OsnapIsOn(model));

            model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
            Assert.False(model.Snap.IsOn);

            Assert.Empty(asked);
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void F3TogglesTheObjectSnaps_InsideACommandToo()
        {
            var asked = new List<string>();
            MainViewModel model = Open(asked);
            model.StartCommand("LINE");

            Assert.True(model.RunKey("F3"));

            Assert.False(OsnapIsOn(model));
            Assert.Empty(asked);
            Assert.True(model.IsCommandRunning);
        }

        [Fact]
        public void MinusOsnapStillAsksTheListOfModes_AndOrthoStillAsksOnOrOff()
        {
            var asked = new List<string>();
            MainViewModel model = Open(asked);

            model.Execute("-OSNAP");
            Assert.Equal("Enter list of object snap modes:", model.Prompt);
            model.Execute(string.Empty);

            model.Execute("ORTHO");
            Assert.Equal("Enter mode [ON/OFF] <OFF>:", model.Prompt);
            model.Execute("ON");
            Assert.True(model.Toggles.Single(t => t.Name == "ORTHO").IsOn);

            Assert.Empty(asked);
        }

        [Fact]
        public void DsettingsAndItsAliasesOpenTheTabUsedLast_WhileOsnapAlwaysOpensObjectSnap()
        {
            var asked = new List<string>();
            MainViewModel model = Open(asked);

            model.Execute("DSETTINGS");
            model.CreateDraftingSettings(DraftingSettingsViewModel.PolarTrackingTab);
            model.Execute("DS");
            model.Execute("SE");
            model.Execute("OSNAP");
            model.Execute("DS");
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.ObjectSnapTab);
            dialog.Tab = DraftingSettingsViewModel.SnapAndGridTab;
            model.Execute("DSETTINGS");

            Assert.Equal(
                new[]
                {
                    DraftingSettingsViewModel.ObjectSnapTab,
                    DraftingSettingsViewModel.PolarTrackingTab,
                    DraftingSettingsViewModel.PolarTrackingTab,
                    DraftingSettingsViewModel.ObjectSnapTab,
                    DraftingSettingsViewModel.ObjectSnapTab,
                    DraftingSettingsViewModel.SnapAndGridTab,
                },
                asked
            );
        }
    }
}
