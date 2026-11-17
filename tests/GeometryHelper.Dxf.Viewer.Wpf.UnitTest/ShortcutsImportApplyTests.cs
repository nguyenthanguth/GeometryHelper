using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Global settings, Shortcuts, Import acad.pgp as the maintainer found it on 2026-11-17: an acad.pgp whose alias is the name
    /// of a command of the presenter (DXFOUT, which AutoCAD 2021 lists as an alias of SAVEAS) left a conflict on the page, so
    /// that Apply and OK stayed greyed after an import that had changed things.
    /// </summary>
    public sealed class ShortcutsImportApplyTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void AnAliasThatIsTheNameOfACommand_IsLeftOutAndSaid_SoApplyAndOkStayOn()
        {
            string path = _folder.WriteText("acad.pgp", "ZZ, *ZOOM\nDXFOUT, *SAVEAS\nL, *PLINE\n");
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            Assert.True(dialog.ApplyCommand.CanExecute(null));

            Assert.True(dialog.ImportPgp(path));

            Assert.Equal(
                "acad.pgp: 1 alias added, 1 changed, 0 unchanged; 1 alias left out, the name of a command. Apply or OK takes them.",
                dialog.Notice
            );
            Assert.False(dialog.HasErrors);
            Assert.True(dialog.ApplyCommand.CanExecute(null));
            Assert.True(dialog.OkCommand.CanExecute(null));
            Assert.DoesNotContain("DXFOUT", dialog.Shortcuts.Single(r => r.Command == "SAVEAS").AliasList);

            Assert.True(dialog.Apply());
            Assert.Equal("ZOOM", model.Settings.Aliases["ZZ"]);
            Assert.Equal("PLINE", model.Settings.Aliases["L"]);
            Assert.False(model.Settings.Aliases.ContainsKey("DXFOUT"));
        }

        [Fact]
        public void TheCommandsOwnNameInThePgpIsNotLeftOut_ItIsTheSameCommand()
        {
            string path = _folder.WriteText("acad.pgp", "SAVEAS, *SAVEAS\nZZ, *ZOOM\n");
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings("Shortcuts");

            dialog.ImportPgp(path);

            Assert.DoesNotContain("left out", dialog.Notice);
            Assert.True(dialog.ApplyCommand.CanExecute(null));
        }
    }
}
