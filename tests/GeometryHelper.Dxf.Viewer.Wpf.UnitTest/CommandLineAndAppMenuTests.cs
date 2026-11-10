using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The command line's AutoComplete, AutoCorrect, Recent Commands and right-click menu, the application menu of the G button,
    /// Recent Documents and the start screen of each app (designs CmdAutoComplete, AppMenu, S1StartDxf).
    /// </summary>
    public sealed class CommandLineAndAppMenuTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        // ---- AutoComplete

        [Fact]
        public void LaTyped_ListsLayerByItsAliasFirst_ThenTheCommandsThatBeginWithIt_ThenTheVariables_ThenTheMidStringNamesBelowARule()
        {
            MainViewModel model = Opened();

            model.CommandText = "LA";
            model.ShowSuggestions();

            IReadOnlyList<CommandSuggestionViewModel> rows = model.Suggestions;
            Assert.True(model.IsSuggestionListOpen);
            Assert.Equal("LAYER", rows[0].Name);
            Assert.Equal("alias LA", rows[0].Note);
            Assert.True(rows[0].IsSelected);
            Assert.Same(rows[0], model.SelectedSuggestion);
            Assert.Equal(("", "LA", "YER"), (rows[0].Before, rows[0].Hit, rows[0].After));

            // The names that begin with LA: the commands, then the variables, each by name while none was used.
            int rule = rows.ToList().FindIndex(r => r.StartsMidString);
            Assert.True(rule > 1);
            List<CommandSuggestionViewModel> prefix = rows.Take(rule).Skip(1).ToList();
            Assert.All(prefix, r => Assert.StartsWith("LA", r.Name, StringComparison.Ordinal));
            Assert.Equal(prefix.OrderBy(r => r.IsVariable).ThenBy(r => r.Name, StringComparer.Ordinal).Select(r => r.Name), prefix.Select(r => r.Name));
            Assert.Contains(prefix, r => r.Name == "LAYOUT");

            // Below the rule the names with LA inside, CLAYER among them as a variable with {x}.
            List<CommandSuggestionViewModel> inside = rows.Skip(rule).ToList();
            Assert.All(inside, r => Assert.True(r.Name.IndexOf("LA", StringComparison.Ordinal) > 0, r.Name));
            CommandSuggestionViewModel clayer = Assert.Single(inside, r => r.Name == "CLAYER");
            Assert.True(clayer.IsVariable);
            Assert.Equal("system variable", clayer.Note);
            Assert.Null(clayer.IconKey);
            Assert.Equal(("C", "LA", "YER"), (clayer.Before, clayer.Hit, clayer.After));
            Assert.Single(rows, r => r.StartsMidString);
        }

        [Fact]
        public void ACommandUsedMore_ComesFirstAmongThoseThatBeginWithTheLetters_AndTheCountIsKeptInTheSettings()
        {
            MainViewModel model = Opened();
            GlobalSettings settings = model.Settings.Clone();

            for (int i = 0; i < 3; i++)
            {
                settings.CountCommandUse("LAYOUT");
            }

            settings.CountCommandUse("LAYMCH");
            model.ApplySettings(settings);
            model.CommandText = "LA";
            model.ShowSuggestions();

            // The alias's command stays first; then LAYOUT (3 uses), LAYMCH (1), then the others by name.
            Assert.Equal(new[] { "LAYER", "LAYOUT", "LAYMCH" }, model.Suggestions.Take(3).Select(r => r.Name));

            int changed = 0;
            model.SettingsChanged += (s, e) => changed++;
            model.Execute("LAYOUT");
            model.Escape();

            Assert.Equal(4, model.Settings.CommandUses["LAYOUT"]);
            Assert.True(changed >= 1);
        }

        [Fact]
        public void RouTyped_OffersRoundAsASynonymOfFillet_AndRunningItStartsFillet()
        {
            MainViewModel model = Opened();

            model.CommandText = "ROU";
            model.ShowSuggestions();

            CommandSuggestionViewModel round = model.Suggestions[0];
            Assert.Equal("ROUND", round.Name);
            Assert.Equal(" (FILLET)", round.Synonym);
            Assert.Equal("FILLET", round.Command);
            Assert.Equal("GhIconFillet", round.IconKey);

            Assert.True(model.RunSuggestion());

            Assert.False(model.IsSuggestionListOpen);
            Assert.Equal(string.Empty, model.CommandText);
            Assert.True(model.IsCommandRunning);
            Assert.Contains("Command: FILLET", model.History);
            Assert.Equal("FILLET", model.RecentCommands[0]);
        }

        [Fact]
        public void AMisspellingCorrectedThreeTimes_IsOfferedItsCommandFirst_AndTwiceIsNotEnough()
        {
            MainViewModel model = Opened();

            for (int i = 0; i < 2; i++)
            {
                model.Execute("LIEN");
                model.Execute("LINE");
                model.Escape();
            }

            Assert.Null(model.Settings.CorrectionOf("LIEN"));
            model.CommandText = "LIEN";
            model.ShowSuggestions();
            Assert.False(model.IsSuggestionListOpen);

            model.CommandText = string.Empty;
            model.Execute("lien");
            model.Execute("L");
            model.Escape();

            Assert.Equal("LINE", model.Settings.CorrectionOf("LIEN"));
            model.CommandText = "LIEN";
            model.ShowSuggestions();
            CommandSuggestionViewModel first = model.Suggestions[0];
            Assert.Equal("LINE", first.Name);
            Assert.Equal("AutoCorrect", first.Note);
            Assert.True(first.IsSelected);
        }

        [Fact]
        public void AutoCorrectOff_NeitherLearnsNorOffers()
        {
            MainViewModel model = Opened();
            GlobalSettings settings = model.Settings.Clone();
            settings.AutoCorrect = false;
            model.ApplySettings(settings);

            for (int i = 0; i < 3; i++)
            {
                model.Execute("LIEN");
                model.Execute("LINE");
                model.Escape();
            }

            Assert.Empty(model.Settings.AutoCorrections);
        }

        [Fact]
        public void TheInputSettings_TakeOutTheVariables_TheMidStringNames_OrTheWholeList()
        {
            MainViewModel model = Opened();
            GlobalSettings settings = model.Settings.Clone();
            settings.SearchSystemVariables = false;
            model.ApplySettings(settings);
            model.CommandText = "LA";
            model.ShowSuggestions();
            Assert.DoesNotContain(model.Suggestions, r => r.IsVariable);
            Assert.Contains(model.Suggestions, r => r.StartsMidString);

            settings.MidStringSearch = false;
            model.ApplySettings(settings);
            model.ShowSuggestions();
            Assert.DoesNotContain(model.Suggestions, r => r.StartsMidString);
            Assert.All(model.Suggestions, r => Assert.StartsWith("LA", r.Name, StringComparison.Ordinal));

            settings.AutoComplete = false;
            model.ApplySettings(settings);
            model.ShowSuggestions();
            Assert.False(model.IsSuggestionListOpen);
            Assert.Empty(model.Suggestions);
        }

        [Fact]
        public void NoListOpens_WithNoDrawing_WithACommandRunning_OrForWordsParted()
        {
            var model = new MainViewModel();
            model.CommandText = "LA";
            model.ShowSuggestions();
            Assert.False(model.IsSuggestionListOpen);

            model = Opened();
            model.Execute("LINE");
            model.CommandText = "LA";
            model.ShowSuggestions();
            Assert.False(model.IsSuggestionListOpen);

            model.Escape();
            model.CommandText = "ZOOM E";
            model.ShowSuggestions();
            Assert.False(model.IsSuggestionListOpen);
        }

        [Fact]
        public void EscWithACommandNameTyped_ClearsWhatIsTyped_AndClosesTheList_AsAutoCadDoes()
        {
            MainViewModel model = Opened();
            model.CommandText = "REC";
            model.ShowSuggestions();
            Assert.True(model.IsSuggestionListOpen);

            model.Escape();

            Assert.Equal(string.Empty, model.CommandText);
            Assert.False(model.IsSuggestionListOpen);

            // Typed with no list open, Esc clears it all the same.
            model.CommandText = "REC";
            model.Escape();
            Assert.Equal(string.Empty, model.CommandText);
        }

        [Fact]
        public void TheArrowsMoveTheRowChosen_TheListFollowsWhatIsTyped_AndEmptyingItClosesIt()
        {
            MainViewModel model = Opened();
            model.CommandText = "LA";
            model.ShowSuggestions();

            model.MoveSuggestion(1);
            Assert.Same(model.Suggestions[1], model.SelectedSuggestion);
            Assert.Single(model.Suggestions, r => r.IsSelected);
            model.MoveSuggestion(-1);
            model.MoveSuggestion(-1);
            Assert.Same(model.Suggestions[0], model.SelectedSuggestion);

            // Typed on, the list open follows at once; no delay waits again.
            model.CommandText = "LAYOU";
            Assert.Equal("LAYOUT", model.Suggestions[0].Name);

            model.CommandText = string.Empty;
            Assert.False(model.IsSuggestionListOpen);

            model.CommandText = "LA";
            model.ShowSuggestions();
            model.CloseSuggestions();
            Assert.False(model.RunSuggestion());
            Assert.Equal("LA", model.CommandText);
        }

        [Fact]
        public void TheRowChosen_RunsWithEnter_AsAClickRunsAnother()
        {
            MainViewModel model = Opened();
            model.CommandText = "LAYO";
            model.ShowSuggestions();

            Assert.True(model.RunSuggestion(model.Suggestions.Single(r => r.Name == "LAYOUT")));

            Assert.Contains("Command: LAYOUT", model.History);
            Assert.True(model.IsCommandRunning);
            Assert.Equal(1, model.Settings.CommandUses["LAYOUT"]);
        }

        // ---- Recent Commands and the right-click menu

        [Fact]
        public void RecentCommands_KeepTheLastTwentyNewestFirst_EachOnce()
        {
            MainViewModel model = Opened();
            string[] names = { "LINE", "CIRCLE", "MOVE", "COPY", "ROTATE", "SCALE", "MIRROR", "TRIM", "EXTEND", "FILLET", "CHAMFER", "OFFSET", "ARC", "PLINE", "STRETCH", "BREAK", "JOIN", "ERASE", "EXPLODE", "LAYER", "ZOOM" };

            foreach (string name in names.Concat(new[] { "c" }))
            {
                model.Execute(name);
                model.Escape();
            }

            // C is CIRCLE's alias: CIRCLE comes back to the top; LINE, the oldest of 21, has gone.
            Assert.Equal(MainViewModel.RecentCommandsKept, model.RecentCommands.Count);
            Assert.Equal("CIRCLE", model.RecentCommands[0]);
            Assert.Equal("ZOOM", model.RecentCommands[1]);
            Assert.DoesNotContain("LINE", model.RecentCommands);
            Assert.Single(model.RecentCommands, c => c == "CIRCLE");

            CadMenuItemViewModel recent = model.CommandLineMenu()[0];
            Assert.Equal("Recent Commands", recent.Header);
            Assert.Equal(model.RecentCommands, recent.Items.Select(i => i.Header));
            recent.Items[1].Command.Execute(null);
            Assert.Equal("Command: ZOOM", model.History.Last(l => l.StartsWith("Command:", StringComparison.Ordinal)));
        }

        [Fact]
        public void TheCommandLineMenu_HasTheDesignsItems_RecentCommandsGreyedBeforeAny_AndSearchContentGreyed()
        {
            MainViewModel model = Opened();

            IReadOnlyList<CadMenuItemViewModel> menu = model.CommandLineMenu();

            Assert.Equal(new[] { "Recent Commands", "Input Settings", "", "Copy", "Copy History", "Paste", "Paste To CmdLine", "", "Options…" }, menu.Select(i => i.Header));
            Assert.False(menu[0].IsEnabled);
            IReadOnlyList<CadMenuItemViewModel> input = menu[1].Items;
            Assert.Equal(new[] { "AutoComplete", "AutoCorrect", "Search System Variables", "Search Content", "Mid-string Search", "", "Delay Time…" }, input.Select(i => i.Header));
            Assert.All(input.Where(i => i.Header.Length > 0 && i.Header != "Search Content" && i.Header != "Delay Time…"), i => Assert.True(i.IsChecked, i.Header));
            Assert.False(input[3].IsEnabled);
            Assert.False(input[3].IsChecked);
            Assert.True(input[6].IsEnabled);

            // A setting switched off is kept: the window saves the settings when told.
            int changed = 0;
            model.SettingsChanged += (s, e) => changed++;
            input[4].Command.Execute(null);
            Assert.False(model.Settings.MidStringSearch);
            Assert.Equal(1, changed);
            Assert.False(model.CommandLineMenu()[1].Items[4].IsChecked);

            string asked = null;
            model.SettingsRequested += (s, category) => asked = category ?? "first";
            menu[8].Command.Execute(null);
            Assert.Equal("first", asked);
        }

        [Fact]
        public void DelayTime_IsAskedOnTheCommandLine_AndRefusesAValueOutOfRange()
        {
            MainViewModel model = Opened();

            model.CommandLineMenu()[1].Items[6].Command.Execute(null);

            Assert.Equal("Enter the delay time in milliseconds <300>:", model.Prompt);
            model.Execute("50");
            Assert.Contains("Requires an integer between 100 and 10000.", model.History);
            Assert.Equal("Enter the delay time in milliseconds <300>:", model.Prompt);
            model.Execute("500");
            Assert.False(model.IsCommandRunning);
            Assert.Equal(500, model.Settings.AutoCompleteDelay);
        }

        [Fact]
        public void CopyCopyHistoryPasteAndPasteToCmdLine_GoThroughTheClipboardTheWindowGives()
        {
            MainViewModel model = Opened();
            string clipboard = "ZOOM\nE";
            model.ReadClipboardText = () => clipboard;
            model.WriteClipboardText = text => clipboard = text;

            IReadOnlyList<CadMenuItemViewModel> menu = model.CommandLineMenu("LIN");
            menu[3].Command.Execute(null);
            Assert.Equal("LIN", clipboard);

            Assert.False(model.CommandLineMenu()[3].IsEnabled);
            model.CommandLineMenu()[4].Command.Execute(null);
            Assert.Equal(string.Join(Environment.NewLine, model.History), clipboard);

            clipboard = "CIRCLE\r\nsecond line";
            model.CommandText = "X";
            model.CommandLineMenu()[5].Command.Execute(null);
            Assert.Equal("XCIRCLE", model.CommandText);

            model.CommandText = string.Empty;
            clipboard = "LINE\n0,0\n100,0\n";
            model.CommandLineMenu()[6].Command.Execute(null);
            Assert.Contains("Command: LINE", model.History);
            Assert.True(model.IsCommandRunning);

            clipboard = string.Empty;
            Assert.False(model.CommandLineMenu()[5].IsEnabled);
            Assert.False(model.CommandLineMenu()[6].IsEnabled);
        }

        // ---- The settings file

        [Fact]
        public void TheInputSettingsUsesCorrectionsAndRecentFiles_ReadBackFromTheFile()
        {
            var settings = new GlobalSettings
            {
                AutoComplete = false,
                AutoCorrect = true,
                SearchSystemVariables = false,
                MidStringSearch = false,
                AutoCompleteDelay = 750,
            };
            settings.CountCommandUse("LINE");
            settings.CountCommandUse("line");
            settings.CountCommandUse("-PLOT");

            for (int i = 0; i < 3; i++)
            {
                settings.NoteCorrection("LIEN", "LINE");
            }

            settings.NoteCorrection("CRICLE", "CIRCLE");
            var start = new DateTime(2026, 11, 10, 9, 14, 0, DateTimeKind.Utc);
            settings.NoteFileOpened(@"D:\Plans\a.dxf", start);
            settings.NoteFileOpened(@"D:\Plans\b.dxf", start.AddMinutes(1));
            settings.NoteFileOpened(@"D:\Plans\c.dxf", start.AddMinutes(2));
            settings.SetFilePinned(@"D:\Plans\a.dxf", true);
            string path = Path.Combine(_folder.FullPath, "settings.json");

            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.False(read.AutoComplete);
            Assert.False(read.SearchSystemVariables);
            Assert.False(read.MidStringSearch);
            Assert.Equal(750, read.AutoCompleteDelay);
            Assert.Equal(2, read.CommandUses["LINE"]);
            Assert.Equal(1, read.CommandUses["-PLOT"]);
            Assert.Equal("LINE", read.CorrectionOf("lien"));
            Assert.Null(read.CorrectionOf("CRICLE"));
            Assert.Equal(new[] { 3, 1 }, read.AutoCorrections.Select(c => c.Count));
            Assert.Equal(new[] { @"D:\Plans\a.dxf", @"D:\Plans\c.dxf", @"D:\Plans\b.dxf" }, read.RecentFiles.Select(f => f.Path));
            Assert.Equal(new[] { true, false, false }, read.RecentFiles.Select(f => f.IsPinned));
            Assert.Equal(start.AddMinutes(2), read.RecentFiles[1].Opened);
            Assert.Equal(DateTimeKind.Utc, read.RecentFiles[1].Opened.Kind);
            Assert.Equal(settings.ToJson(), read.ToJson());
            Assert.DoesNotContain("\r", File.ReadAllText(path));
        }

        [Fact]
        public void TheDelayOutOfRange_IsRefused_AndABadEntryOfTheFileIsLeftOut()
        {
            var settings = new GlobalSettings();
            Assert.Equal(300, settings.AutoCompleteDelay);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.AutoCompleteDelay = 99);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.AutoCompleteDelay = 10001);

            string path = Path.Combine(_folder.FullPath, "bad.json");
            File.WriteAllText(path, "{ \"input\": { \"delayMilliseconds\": 5 }, \"commandUses\": { \"LINE\": \"many\", \"MOVE\": 2 }, \"recentFiles\": [ { \"path\": \"\", \"opened\": \"2026-11-10T09:14:00Z\" }, { \"path\": \"D:\\\\x.dxf\", \"opened\": \"2026-11-10T09:14:00Z\", \"pinned\": true } ] }");
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(300, read.AutoCompleteDelay);
            Assert.False(read.CommandUses.ContainsKey("LINE"));
            Assert.Equal(2, read.CommandUses["MOVE"]);
            Assert.Equal(@"D:\x.dxf", Assert.Single(read.RecentFiles).Path);
        }

        [Fact]
        public void RecentFiles_PinnedFirstInPinOrder_ThenNewestFirst_NineKept_AndAnUnpinnedFileGoesBackByItsDate()
        {
            var settings = new GlobalSettings();
            var start = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < 12; i++)
            {
                settings.NoteFileOpened(@"D:\Plans\f" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".dxf", start.AddHours(i));
            }

            Assert.Equal(GlobalSettings.RecentFilesShown, settings.RecentFiles.Count);
            Assert.Equal(@"D:\Plans\f11.dxf", settings.RecentFiles[0].Path);
            Assert.Equal(@"D:\Plans\f03.dxf", settings.RecentFiles.Last().Path);

            Assert.True(settings.SetFilePinned(@"D:\Plans\f05.dxf", true));
            Assert.True(settings.SetFilePinned(@"d:\plans\F09.DXF", true));
            Assert.False(settings.SetFilePinned(@"D:\Plans\f09.dxf", true));
            Assert.False(settings.SetFilePinned(@"D:\Plans\gone.dxf", true));
            Assert.Equal(new[] { @"D:\Plans\f05.dxf", @"D:\Plans\f09.dxf", @"D:\Plans\f11.dxf", @"D:\Plans\f10.dxf" }, settings.RecentFiles.Take(4).Select(f => f.Path));

            // Opened again, a pinned file keeps its place; one not pinned goes to the top of the others.
            settings.NoteFileOpened(@"D:\Plans\f05.dxf", start.AddDays(1));
            settings.NoteFileOpened(@"D:\Plans\f04.dxf", start.AddDays(1));
            Assert.Equal(new[] { @"D:\Plans\f05.dxf", @"D:\Plans\f09.dxf", @"D:\Plans\f04.dxf", @"D:\Plans\f11.dxf" }, settings.RecentFiles.Take(4).Select(f => f.Path));

            Assert.True(settings.SetFilePinned(@"D:\Plans\f09.dxf", false));
            Assert.Equal(new[] { @"D:\Plans\f05.dxf", @"D:\Plans\f04.dxf", @"D:\Plans\f11.dxf", @"D:\Plans\f10.dxf", @"D:\Plans\f09.dxf" }, settings.RecentFiles.Take(5).Select(f => f.Path));
        }

        [Fact]
        public void ResetOfGlobalSettings_KeepsWhatTheSettingsRemember()
        {
            var model = new MainViewModel();
            GlobalSettings settings = model.Settings.Clone();
            settings.CountCommandUse("LINE");
            settings.NoteFileOpened(@"D:\Plans\a.dxf", DateTime.UtcNow);
            settings.AutoComplete = false;
            model.ApplySettings(settings);
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings();

            dialog.Reset();
            Assert.True(dialog.Apply());

            Assert.True(model.Settings.AutoComplete);
            Assert.Equal(1, model.Settings.CommandUses["LINE"]);
            Assert.Single(model.Settings.RecentFiles);
        }

        // ---- Recent Documents

        [Fact]
        public void OpeningAFile_PutsItFirstInRecentDocuments_AndAPinIsShared()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            int changed = 0;
            model.SettingsChanged += (s, e) => changed++;

            Assert.True(model.Open(room));
            Assert.True(model.Open(other));

            Assert.Equal(new[] { "other.dxf", "room.dxf" }, model.RecentDocuments.Select(d => d.Name));
            Assert.Equal(room, model.Settings.RecentFiles[1].Path);
            Assert.True(changed >= 2);
            Assert.False(model.HasPinnedDocuments);

            model.PinRecent(model.RecentDocuments[1], true);
            Assert.Equal(new[] { "room.dxf", "other.dxf" }, model.RecentDocuments.Select(d => d.Name));
            Assert.Equal(new[] { "room.dxf" }, model.PinnedDocuments.Select(d => d.Name));
            Assert.Equal(new[] { "other.dxf" }, model.UnpinnedDocuments.Select(d => d.Name));
            Assert.True(model.RecentDocuments[1].IsFirstAfterPinned);
            Assert.Equal("Unpin", model.RecentDocuments[0].PinText);

            // The menu of the G button lists the same files.
            Assert.Equal(model.RecentDocuments.Select(d => d.Path), model.CreateAppMenu().RecentDocuments.Select(d => d.Path));

            // Its pin button unpins it.
            model.RecentDocuments[0].PinCommand.Execute(null);
            Assert.False(model.HasPinnedDocuments);
        }

        [Fact]
        public void OpenedText_SaysTodayYesterdayOrTheDate()
        {
            var model = new MainViewModel { Now = () => new DateTime(2026, 11, 10, 12, 0, 0) };
            GlobalSettings settings = model.Settings.Clone();
            settings.NoteFileOpened(@"D:\Plans\a.dxf", new DateTime(2026, 11, 10, 9, 14, 0, DateTimeKind.Local).ToUniversalTime());
            settings.NoteFileOpened(@"D:\Plans\b.dxf", new DateTime(2026, 11, 9, 16, 40, 0, DateTimeKind.Local).ToUniversalTime());
            settings.NoteFileOpened(@"D:\Plans\c.dxf", new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Local).ToUniversalTime());
            model.ApplySettings(settings);

            Assert.Equal(new[] { "today 09:14", "yesterday 16:40", "2026-10-30" }, model.RecentDocuments.Select(d => d.OpenedText));
        }

        [Fact]
        public void AFileWithAPreview_GivesItsVersionAndPicture_ReadFromItsTopAndEnd()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string text = File.ReadAllText(room);
            int end = text.LastIndexOf("EOF", StringComparison.Ordinal);
            int zero = text.LastIndexOf("  0", end, StringComparison.Ordinal);
            byte[] bitmap = Bitmap(8, 6);
            var section = new StringBuilder("  0\nSECTION\n  2\nTHUMBNAILIMAGE\n 90\n" + bitmap.Length + "\n");
            string hex = BitConverter.ToString(bitmap).Replace("-", string.Empty);

            for (int i = 0; i < hex.Length; i += 254)
            {
                section.Append("310\n").Append(hex.Substring(i, Math.Min(254, hex.Length - i))).Append('\n');
            }

            section.Append("  0\nENDSEC\n");
            File.WriteAllText(room, text.Substring(0, zero) + section + text.Substring(zero));
            string plain = _folder.Write("plain.dxf", TestFiles.Room(out _, out _));

            OnSta(() =>
            {
                var model = new MainViewModel();
                GlobalSettings settings = model.Settings.Clone();
                settings.NoteFileOpened(room, DateTime.UtcNow);
                settings.NoteFileOpened(plain, DateTime.UtcNow.AddMinutes(-1));
                settings.NoteFileOpened(Path.Combine(_folder.FullPath, "gone.dxf"), DateTime.UtcNow.AddMinutes(-2));
                model.ApplySettings(settings);
                RecentDocumentViewModel withPicture = model.RecentDocuments[0];
                RecentDocumentViewModel without = model.RecentDocuments[1];
                RecentDocumentViewModel gone = model.RecentDocuments[2];
                int raised = 0;
                withPicture.PropertyChanged += (s, e) => raised += e.PropertyName == nameof(RecentDocumentViewModel.Thumbnail) ? 1 : 0;

                // Read on a worker thread, the window not held while a slow file is read; the picture raised once there.
                System.Threading.Tasks.Task.WaitAll(withPicture.WhenRead(), without.WhenRead(), gone.WhenRead());
                Assert.Equal(1, raised);
                Assert.Equal("AutoCAD 2013 DXF (AC1027)", withPicture.VersionText);
                Assert.True(withPicture.HasThumbnail);
                Assert.Equal(8, ((System.Windows.Media.Imaging.BitmapSource)withPicture.Thumbnail).PixelWidth);
                Assert.Equal(6, ((System.Windows.Media.Imaging.BitmapSource)withPicture.Thumbnail).PixelHeight);
                Assert.EndsWith(" KB", withPicture.SizeText, StringComparison.Ordinal);
                Assert.False(without.HasThumbnail);
                Assert.Equal("AutoCAD 2013 DXF (AC1027)", without.VersionText);
                Assert.False(gone.HasThumbnail);
                Assert.Equal("DXF", gone.VersionText);
                Assert.Equal("not found", gone.ModifiedText);
                Assert.Equal(string.Empty, gone.SizeText);
            });
        }

        [Fact]
        public void OpeningARecentFile_ShowsIt()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            GlobalSettings settings = model.Settings.Clone();
            settings.NoteFileOpened(room, DateTime.UtcNow.AddDays(-3));
            model.ApplySettings(settings);

            model.RecentDocuments[0].OpenCommand.Execute(null);

            Assert.True(model.HasDocument);
            Assert.Equal("room.dxf", model.Title);
            Assert.Equal("today", model.RecentDocuments[0].OpenedText.Substring(0, 5));
        }

        [Fact]
        public void ARecentFileNoLongerOnTheDisk_IsTakenOffTheList_WhenChosen()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            GlobalSettings settings = model.Settings.Clone();
            settings.NoteFileOpened(room, DateTime.UtcNow);
            settings.NoteFileOpened(Path.Combine(_folder.FullPath, "gone.dxf"), DateTime.UtcNow.AddMinutes(-1));
            model.ApplySettings(settings);

            Assert.False(model.OpenRecent(model.RecentDocuments[1]));

            Assert.False(model.HasDocument);
            Assert.Contains("Cannot find " + Path.Combine(_folder.FullPath, "gone.dxf") + ": it is taken off the recent files.", model.History);
            Assert.Equal(new[] { room }, model.RecentDocuments.Select(d => d.Path));
            Assert.Equal(new[] { room }, model.Settings.RecentFiles.Select(f => f.Path));
        }

        [Fact]
        public void RecentCommands_AreKeptInTheSettingsFile_ForTheNextSession()
        {
            MainViewModel model = Opened();
            model.Execute("LINE");
            model.Escape();
            model.Execute("CIRCLE");
            model.Escape();
            string path = Path.Combine(_folder.FullPath, "settings.json");
            model.Settings.Save(path);

            var next = new MainViewModel();
            next.ApplySettings(GlobalSettings.Load(path));

            Assert.Equal(new[] { "CIRCLE", "LINE" }, next.RecentCommands.Take(2));
            Assert.Equal(model.RecentCommands, next.RecentCommands);
        }

        // ---- The start screen of each app

        [Fact]
        public void WithNoHostForModelDrawings_TheStartScreenIsGeometryHelperDxfs_AndCtrlNMakesANewDrawing()
        {
            var model = new MainViewModel();

            Assert.False(model.CanMakeFromModel);
            Assert.Equal("Open and edit any 2D drawing. No file is open.", model.StartScreenText);
            Assert.Equal(new[] { MainViewModel.NewDrawingWelcomeText }, model.RecentHistory);
            Assert.Equal("Welcome. Ctrl+O opens a DXF, Ctrl+N starts a new drawing.", MainViewModel.NewDrawingWelcomeText);

            model.NewByShortcut();

            Assert.True(model.HasDocument);
            Assert.Equal("Drawing1.dxf", model.Title);
        }

        [Fact]
        public void WithAHostForModelDrawings_TheStartScreenOffersThem_AndCtrlNAsksTheHost()
        {
            var model = new MainViewModel();
            var raised = new List<string>();
            model.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
            int asked = 0;
            EventHandler handler = (s, e) => asked++;

            model.NewFromModelRequested += handler;

            Assert.True(model.CanMakeFromModel);
            Assert.Contains(nameof(MainViewModel.CanMakeFromModel), raised);
            Assert.Equal("Open and edit any 2D drawing, or make drawings from a model. No file is open.", model.StartScreenText);
            Assert.Equal(new[] { MainViewModel.WelcomeText }, model.RecentHistory);
            model.NewByShortcut();
            Assert.Equal(1, asked);
            Assert.False(model.HasDocument);
            Assert.Contains(model.CreateAppMenu().Items[0].Items, i => i.Name == "Drawing from a model");

            model.NewFromModelRequested -= handler;
            Assert.False(model.CanMakeFromModel);
            Assert.DoesNotContain(model.CreateAppMenu().Items[0].Items, i => i.Name == "Drawing from a model");
        }

        // ---- The application menu

        [Fact]
        public void TheAppMenu_HasTheDesignsItems_GreysWhatNeedsADrawing_AndShowsAnItemsListOnHover()
        {
            AppMenuViewModel start = new MainViewModel().CreateAppMenu("GeometryHelper DXF");

            Assert.Equal(new[] { "New", "Open", "Save", "Save As", "", "Export", "Print", "", "Close" }, start.Items.Select(i => i.Name));
            Assert.Equal("Exit GeometryHelper DXF", start.ExitText);
            Assert.False(start.Items.Single(i => i.Key == "save").IsEnabled);
            Assert.False(start.Items.Single(i => i.Key == "export").IsEnabled);
            Assert.True(start.Items.Single(i => i.Key == "new").IsEnabled);
            Assert.True(start.IsRecentShown);

            AppMenuViewModel menu = Opened().CreateAppMenu();
            AppMenuItemViewModel export = menu.Items.Single(i => i.Key == "export");
            Assert.Equal(new[] { "PDF", "PNG", "JPG", "BMP", "Block or objects to a DXF", "Other Formats" }, export.Items.Select(i => i.Name));
            Assert.Equal(new[] { true, true, true, true, true, false }, export.Items.Select(i => i.IsEnabled));
            Assert.Equal(new[] { "Plot", "Batch Plot", "Plot Preview", "Page Setup", "Manage Plot Styles" }, menu.Items.Single(i => i.Key == "print").Items.Select(i => i.Name));
            Assert.Equal(new[] { true, false, true, true, false }, menu.Items.Single(i => i.Key == "print").Items.Select(i => i.IsEnabled));
            Assert.Equal(new[] { "Drawing", "Drawing Template", "Other Formats" }, menu.Items.Single(i => i.Key == "saveas").Items.Select(i => i.Name));
            Assert.Equal(new[] { "Current Drawing", "All Drawings" }, menu.Items.Single(i => i.Key == "close").Items.Select(i => i.Name));

            menu.ShownItem = export;
            Assert.False(menu.IsRecentShown);
            Assert.True(export.IsShown);
            Assert.Equal("Export a copy of the drawing to other file formats", menu.ShownTitle);
            menu.ShownItem = menu.Items.Single(i => i.Key == "save");
            Assert.True(menu.IsRecentShown);
            Assert.False(export.IsShown);
        }

        [Fact]
        public void ExportPdf_RunsPlot_PngRunsPngout_AndTheMenuClosesOnAChoice()
        {
            MainViewModel model = Opened();
            PlotDialogViewModel shown = null;
            model.ShowPlot = dialog =>
            {
                shown = dialog;
                return false;
            };
            AppMenuViewModel menu = model.CreateAppMenu();
            int closed = 0;
            menu.Chosen += (s, e) => closed++;
            AppMenuItemViewModel export = menu.Items.Single(i => i.Key == "export");

            menu.Choose(export.Items[0]);

            Assert.NotNull(shown);
            Assert.Equal(1, closed);
            Assert.Contains("Command: PLOT", model.History);

            string asked = null;
            model.AskImageFile = (folder, name) =>
            {
                asked = name;
                return null;
            };
            menu.Choose(export.Items[1]);
            Assert.Contains("Command: PNGOUT", model.History);
            Assert.EndsWith(".png", asked, StringComparison.Ordinal);
            Assert.Equal(2, closed);

            // A click on Export itself runs its first entry, PDF.
            shown = null;
            menu.Choose(export);
            Assert.NotNull(shown);

            // An entry not built does nothing.
            menu.Choose(export.Items[5]);
            Assert.Equal(3, closed);
        }

        [Fact]
        public void TheAppMenusSearch_ListsTheCommandsThatHoldTheLetters_AndRunsOne()
        {
            MainViewModel model = Opened();
            AppMenuViewModel menu = model.CreateAppMenu();

            menu.SearchText = "plot";

            Assert.True(menu.IsSearching);
            Assert.False(menu.IsRecentShown);
            Assert.Equal("PLOT", menu.SearchResults[0].Name);
            Assert.Contains(menu.SearchResults, r => r.Name == "-PLOT");
            int closed = 0;
            menu.Chosen += (s, e) => closed++;
            menu.RunSearchResult(menu.SearchResults[0]);
            Assert.Equal(1, closed);
            Assert.Contains("Command: PLOT", model.History);

            menu.SearchText = string.Empty;
            Assert.True(menu.IsRecentShown);
            Assert.Empty(menu.SearchResults);
        }

        [Fact]
        public void CloseAllDrawings_ClosesEveryDrawingOpen()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            model.Open(room);
            model.Open(other);

            model.CreateAppMenu().Items.Single(i => i.Key == "close").Items[1].Command.Execute(null);

            Assert.False(model.HasDocument);
        }

        private MainViewModel Opened()
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            Assert.True(model.Open(room));
            return model;
        }

        /// <summary>A bitmap of Windows of 24 bits as a THUMBNAILIMAGE keeps it: the header of 40 bytes and the rows, no file header.</summary>
        private static byte[] Bitmap(int width, int height)
        {
            int row = (width * 3 + 3) & ~3;
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(40));
            bytes.AddRange(BitConverter.GetBytes(width));
            bytes.AddRange(BitConverter.GetBytes(height));
            bytes.AddRange(BitConverter.GetBytes((short)1));
            bytes.AddRange(BitConverter.GetBytes((short)24));
            bytes.AddRange(BitConverter.GetBytes(0));
            bytes.AddRange(BitConverter.GetBytes(row * height));
            bytes.AddRange(new byte[16]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < row; x++)
                {
                    bytes.Add((byte)(x < width * 3 ? 0xFF : 0));
                }
            }

            return bytes.ToArray();
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(failure.ToString());
            }
        }
    }
}
