using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Security page of Global settings (D916 as D920 changed it, <c>design/SecSettingsSecurity.png</c>): the SECURELOAD and
    /// LEGACYCODESEARCH radios, the list of trusted folders with the program folder first and always trusted, Add (with the
    /// folder browser and the writable-folder warning), Remove, Move up and down, Include subfolders, and Forget for the files
    /// always loaded. Everything is applied by Apply or OK, and Cancel leaves the settings as they were. Assumed members of
    /// <see cref="GlobalSettingsViewModel"/>: <c>SecureLoad</c> (0 to 2), <c>LegacyCodeSearch</c> (bool),
    /// <c>TrustedFolders</c> (rows with <c>Entry</c> and <c>IsProgramFolder</c>, the program folder's first),
    /// <c>SelectedTrustedFolder</c>, <c>IncludeSubfolders</c> (of the selected entry), <c>AddTrustedFolderCommand</c>,
    /// <c>RemoveTrustedFolderCommand</c>, <c>MoveTrustedFolderUpCommand</c>, <c>MoveTrustedFolderDownCommand</c> (none takes a
    /// parameter: they act on the selected entry), <c>BrowseTrustedFolder</c> (<c>Func&lt;string&gt;</c>, null for none chosen),
    /// <c>ConfirmWritableTrustedFolder</c> (<c>Func&lt;string, bool&gt;</c>), <c>AlwaysLoadedCount</c> and
    /// <c>ForgetAlwaysLoadedCommand</c>.
    /// </summary>
    public sealed class GlobalSettingsSecurityTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string SettingsFile => Path.Combine(_folder.FullPath, "settings.json");

        /// <summary>A folder of this test, made, with its full path given.</summary>
        private string Dir(string name) => Directory.CreateDirectory(Path.Combine(_folder.FullPath, name)).FullName;

        /// <summary>The model of the last <see cref="Model"/> call, whose settings a test reads after Apply.</summary>
        private MainViewModel _model;

        private MainViewModel Model(GlobalSettings settings = null)
        {
            _model = new MainViewModel { SettingsPath = SettingsFile };
            _model.ApplySettings(settings ?? new GlobalSettings());
            return _model;
        }

        private static List<string> Entries(GlobalSettingsViewModel dialog) =>
            dialog.TrustedFolders.Where(row => !row.IsProgramFolder).Select(row => row.Entry).ToList();

        private static string Entry(string sha, string path) => sha + "|" + path;

        [Fact]
        public void WithDefaultSettings_SecureLoadIsOne_LegacyCodeSearchIsOff_NothingIsTrusted_NothingIsAlwaysLoaded()
        {
            GlobalSettingsViewModel dialog = Model().CreateGlobalSettings("Security");

            Assert.Equal(1, dialog.SecureLoad);
            Assert.False(dialog.LegacyCodeSearch);
            Assert.Empty(Entries(dialog));
            Assert.Equal(0, dialog.AlwaysLoadedCount);
        }

        [Fact]
        public void ThePage_ReadsTheSettings_TheEntriesAsStored_AndTheProgramFolderFirstAlwaysTrusted()
        {
            string one = Path.Combine(_folder.FullPath, "one");
            string two = Path.Combine(_folder.FullPath, "two");
            var settings = new GlobalSettings
            {
                SecureLoad = 2,
                LegacyCodeSearch = true,
                TrustedPaths = one + ";" + two + @"\...",
                AlwaysLoadedFiles = new[]
                {
                    Entry(new string('a', 64), @"C:\x\1.lsp"),
                    Entry(new string('b', 64), @"C:\x\2.lsp"),
                    Entry(new string('c', 64), @"C:\x\3.py"),
                },
            };

            GlobalSettingsViewModel dialog = Model(settings).CreateGlobalSettings("Security");

            Assert.Equal(2, dialog.SecureLoad);
            Assert.True(dialog.LegacyCodeSearch);
            Assert.Equal(new[] { one, two + @"\..." }, Entries(dialog));
            Assert.Equal(3, dialog.AlwaysLoadedCount);
            Assert.True(dialog.TrustedFolders[0].IsProgramFolder);
            Assert.Equal(CadLoadSecurity.ProgramFolder + @"\...", dialog.TrustedFolders[0].Entry);
            Assert.Single(dialog.TrustedFolders, row => row.IsProgramFolder);
        }

        [Fact]
        public void TheRadios_WriteTheSettingsAndTheGateOfTheOpenDrawings_OnApply_NotBefore()
        {
            MainViewModel model = Model();
            Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Security");

            dialog.SecureLoad = 2;
            dialog.LegacyCodeSearch = true;

            Assert.Equal(1, model.Settings.SecureLoad);
            Assert.False(model.Settings.LegacyCodeSearch);
            Assert.Equal(1, model.Session.LoadSecurity.SecureLoad);

            Assert.True(dialog.Apply());

            Assert.Equal(2, model.Settings.SecureLoad);
            Assert.True(model.Settings.LegacyCodeSearch);
            Assert.Equal(2, model.Session.LoadSecurity.SecureLoad);
            Assert.True(model.Session.LoadSecurity.LegacyCodeSearch);
        }

        [Fact]
        public void Cancel_LeavesTheSettingsAsTheyWere_AndOkSavesWhatWasChosen()
        {
            MainViewModel model = Model();
            GlobalSettingsViewModel cancelled = model.CreateGlobalSettings("Security");
            cancelled.SecureLoad = 0;
            cancelled.LegacyCodeSearch = true;

            cancelled.CancelCommand.Execute(null);

            Assert.Equal(1, model.Settings.SecureLoad);
            Assert.False(model.Settings.LegacyCodeSearch);

            GlobalSettingsViewModel kept = model.CreateGlobalSettings("Security");
            kept.SecureLoad = 0;
            kept.LegacyCodeSearch = true;

            Assert.True(kept.Ok());

            GlobalSettings read = GlobalSettings.Load(SettingsFile);
            Assert.Equal(0, read.SecureLoad);
            Assert.True(read.LegacyCodeSearch);
        }

        [Fact]
        public void AddOfAWritableFolder_AsksTheWarningWithThatFolder_AndCancelAddsNothing()
        {
            string tools = Dir("tools");
            GlobalSettingsViewModel dialog = Model().CreateGlobalSettings("Security");
            var asked = new List<string>();
            dialog.BrowseTrustedFolder = () => tools;
            dialog.ConfirmWritableTrustedFolder = folder =>
            {
                asked.Add(folder);
                return false;
            };

            dialog.AddTrustedFolderCommand.Execute(null);

            Assert.Equal(new[] { tools }, asked);
            Assert.Empty(Entries(dialog));
            Assert.True(dialog.Apply());
            Assert.Equal("", _model.Settings.TrustedPaths);
        }

        [Fact]
        public void AddOfAWritableFolder_ThenContinue_AddsItAsTheFolderAlone_SelectedAtTheEnd()
        {
            string one = Dir("one");
            string two = Dir("two");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = one })
                .CreateGlobalSettings("Security");
            dialog.BrowseTrustedFolder = () => two;
            dialog.ConfirmWritableTrustedFolder = folder => true;

            dialog.AddTrustedFolderCommand.Execute(null);

            Assert.Equal(new[] { one, two }, Entries(dialog));
            Assert.Equal(two, dialog.SelectedTrustedFolder.Entry);
            Assert.False(dialog.IncludeSubfolders);
        }

        [Fact]
        public void AddOfAFolderTheAppCannotWriteIn_AsksNoWarning_AndAddsIt()
        {
            using (var readOnly = new ReadOnlyFolder(Path.Combine(_folder.FullPath, "readonly")))
            {
                // The precondition: the probe of D915 (create a file in it) really fails here.
                Assert.Throws<UnauthorizedAccessException>(() =>
                    File.Create(Path.Combine(readOnly.FullPath, "probe.tmp"), 1, FileOptions.DeleteOnClose).Dispose()
                );
                GlobalSettingsViewModel dialog = Model().CreateGlobalSettings("Security");
                bool asked = false;
                dialog.BrowseTrustedFolder = () => readOnly.FullPath;
                dialog.ConfirmWritableTrustedFolder = folder =>
                {
                    asked = true;
                    return false;
                };

                dialog.AddTrustedFolderCommand.Execute(null);

                Assert.False(asked);
                Assert.Equal(new[] { readOnly.FullPath }, Entries(dialog));
            }
        }

        [Fact]
        public void AddWhenTheFolderBrowserChoosesNothing_AddsNothing_AndAsksNothing()
        {
            GlobalSettingsViewModel dialog = Model().CreateGlobalSettings("Security");
            bool asked = false;
            dialog.BrowseTrustedFolder = () => null;
            dialog.ConfirmWritableTrustedFolder = folder =>
            {
                asked = true;
                return true;
            };

            dialog.AddTrustedFolderCommand.Execute(null);

            Assert.False(asked);
            Assert.Empty(Entries(dialog));
        }

        [Fact]
        public void Apply_WritesTheEntriesInTheOrderShown_JoinedBySemicolons_WithoutTheProgramFolder()
        {
            string one = Dir("one");
            string two = Dir("two");
            string three = Dir("three");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = one + ";" + two })
                .CreateGlobalSettings("Security");
            dialog.BrowseTrustedFolder = () => three;
            dialog.ConfirmWritableTrustedFolder = folder => true;
            dialog.AddTrustedFolderCommand.Execute(null);

            Assert.True(dialog.Apply());

            Assert.Equal(one + ";" + two + ";" + three, _model.Settings.TrustedPaths);
        }

        [Fact]
        public void IncludeSubfolders_AddsBackslashThreeDotsToTheSelectedEntry_AndTakesItOff()
        {
            string one = Dir("one");
            string two = Dir("two");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = one + ";" + two })
                .CreateGlobalSettings("Security");
            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => r.Entry == two);
            Assert.False(dialog.IncludeSubfolders);

            dialog.IncludeSubfolders = true;

            Assert.Equal(new[] { one, two + @"\..." }, Entries(dialog));
            Assert.True(dialog.IncludeSubfolders);
            Assert.True(dialog.Apply());
            Assert.Equal(one + ";" + two + @"\...", _model.Settings.TrustedPaths);

            dialog.IncludeSubfolders = false;

            Assert.Equal(new[] { one, two }, Entries(dialog));
        }

        [Fact]
        public void ASelectedEntryEndingInBackslashThreeDots_ShowsIncludeSubfoldersTicked()
        {
            string one = Dir("one");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = one + @"\..." })
                .CreateGlobalSettings("Security");

            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => !r.IsProgramFolder);

            Assert.True(dialog.IncludeSubfolders);
        }

        [Fact]
        public void TheProgramFolderRow_CanBeSelected_ButNotRemovedNorMoved()
        {
            string one = Dir("one");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = one })
                .CreateGlobalSettings("Security");

            dialog.SelectedTrustedFolder = dialog.TrustedFolders[0];

            Assert.Same(dialog.TrustedFolders[0], dialog.SelectedTrustedFolder);
            Assert.False(dialog.RemoveTrustedFolderCommand.CanExecute(null));
            Assert.False(dialog.MoveTrustedFolderUpCommand.CanExecute(null));
            Assert.False(dialog.MoveTrustedFolderDownCommand.CanExecute(null));
        }

        [Fact]
        public void Remove_TakesOutTheSelectedEntry_AndMoveUpAndDownReorderThem_AndApplyWritesThatOrder()
        {
            string a = Dir("a");
            string b = Dir("b");
            string c = Dir("c");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = a + ";" + b + ";" + c })
                .CreateGlobalSettings("Security");

            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => r.Entry == c);
            dialog.MoveTrustedFolderUpCommand.Execute(null);
            Assert.Equal(new[] { a, c, b }, Entries(dialog));

            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => r.Entry == a);
            dialog.MoveTrustedFolderDownCommand.Execute(null);
            Assert.Equal(new[] { c, a, b }, Entries(dialog));

            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => r.Entry == a);
            dialog.RemoveTrustedFolderCommand.Execute(null);
            Assert.Equal(new[] { c, b }, Entries(dialog));
            Assert.True(dialog.Apply());
            Assert.Equal(c + ";" + b, _model.Settings.TrustedPaths);
        }

        [Fact]
        public void TheFirstEntryCannotMoveUp_AndTheLastCannotMoveDown_BecauseTheProgramFolderStaysFirst()
        {
            string a = Dir("a");
            string b = Dir("b");
            GlobalSettingsViewModel dialog = Model(new GlobalSettings { TrustedPaths = a + ";" + b })
                .CreateGlobalSettings("Security");

            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => r.Entry == a);
            Assert.False(dialog.MoveTrustedFolderUpCommand.CanExecute(null));
            Assert.True(dialog.MoveTrustedFolderDownCommand.CanExecute(null));

            dialog.SelectedTrustedFolder = dialog.TrustedFolders.Single(r => r.Entry == b);
            Assert.True(dialog.MoveTrustedFolderUpCommand.CanExecute(null));
            Assert.False(dialog.MoveTrustedFolderDownCommand.CanExecute(null));
        }

        [Fact]
        public void Forget_EmptiesTheListOfFilesAlwaysLoaded_OnApply_NotBefore()
        {
            var settings = new GlobalSettings
            {
                AlwaysLoadedFiles = new[]
                {
                    Entry(new string('a', 64), @"C:\x\1.lsp"),
                    Entry(new string('b', 64), @"C:\x\2.lsp"),
                },
            };
            MainViewModel model = Model(settings);
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Security");
            Assert.Equal(2, dialog.AlwaysLoadedCount);

            dialog.ForgetAlwaysLoadedCommand.Execute(null);

            Assert.Equal(0, dialog.AlwaysLoadedCount);
            Assert.Equal(2, model.Settings.AlwaysLoadedFiles.Count);

            Assert.True(dialog.Apply());

            Assert.Empty(model.Settings.AlwaysLoadedFiles);
        }

        [Fact]
        public void Forget_ThenCancel_KeepsTheFilesAlwaysLoaded()
        {
            string entry = Entry(new string('a', 64), @"C:\x\1.lsp");
            MainViewModel model = Model(new GlobalSettings { AlwaysLoadedFiles = new[] { entry } });
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Security");

            dialog.ForgetAlwaysLoadedCommand.Execute(null);
            dialog.CancelCommand.Execute(null);

            Assert.Equal(new[] { entry }, model.Settings.AlwaysLoadedFiles);
        }
    }
}
