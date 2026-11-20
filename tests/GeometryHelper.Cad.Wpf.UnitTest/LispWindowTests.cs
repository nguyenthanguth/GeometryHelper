using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// AutoLISP in the window: a line typed with its spaces goes to the session whole, Space types a space in it, AutoComplete
    /// offers the <c>c:</c> commands, APPLOAD picks an AutoLISP file or a plugin, and a drawing opened runs the startup
    /// files of the support folders. Driven with no window.
    /// </summary>
    public sealed class LispWindowTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private MainViewModel Opened(Action<GlobalSettings> settings = null)
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();

            if (settings != null)
            {
                GlobalSettings changed = model.Settings.Clone();
                settings(changed);
                model.ApplySettings(changed);
            }

            Assert.True(model.Open(room));
            return model;
        }

        /// <summary>A Load/Unload Applications that loads one file, as a drafter does by typing its path and pressing Load, and closes.</summary>
        private static Func<AppLoadViewModel, bool> Loading(string path) =>
            dialog =>
            {
                dialog.FileName = path;
                dialog.Load();
                return true;
            };

        [Fact]
        public void AFormWithSpaces_GoesToTheSessionWholeAndItsValueEndsTheHistory()
        {
            MainViewModel model = Opened();

            model.Execute("(setq a 1)");

            Assert.Equal("1", model.History.Last());
            Assert.Equal("Command: (setq a 1)", model.History[model.History.Count - 2]);
        }

        [Fact]
        public void AFormWithAWordThatIsAnObjectSnapName_IsNotSplitIntoWords()
        {
            MainViewModel model = Opened();

            model.Execute("(setq a (strcat \"mid\" \" \" \"end\"))");
            model.Execute("!a");

            Assert.Equal("\"mid end\"", model.History.Last());
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void APromptThatAsksForText_ForALispLineToo_TakesSpaces()
        {
            MainViewModel model = Opened();

            model.CommandText = "(setq";
            Assert.True(model.PromptTakesSpaces);

            model.CommandText = "!a";
            Assert.True(model.PromptTakesSpaces);

            model.CommandText = "LINE";
            Assert.False(model.PromptTakesSpaces);

            model.CommandText = string.Empty;
            Assert.False(model.PromptTakesSpaces);
        }

        [Fact]
        public void AFormLeftOpen_KeepsTakingSpacesUntilItIsClosed()
        {
            MainViewModel model = Opened();

            model.Execute("(setq a");
            model.CommandText = "1";
            Assert.True(model.PromptTakesSpaces);

            model.Execute("1)");
            model.CommandText = "LINE";
            Assert.False(model.PromptTakesSpaces);
        }

        [Fact]
        public void GetStringWithoutCr_SpaceEndsTheText_AndWithCrItIsTyped()
        {
            // AutoCAD 2021 (q3): (getstring "Str: ") ends at a space; (getstring T "Str: ") takes "hello world".
            MainViewModel model = Opened();

            model.Execute("(getstring \"Str: \")");
            model.CommandText = "hel";
            Assert.False(model.PromptTakesSpaces);
            model.CommandText = string.Empty;
            model.Execute("hello");
            Assert.Equal("\"hello\"", model.History.Last());

            model.Execute("(getstring T \"Str: \")");
            model.CommandText = "hel";
            Assert.True(model.PromptTakesSpaces);
            model.CommandText = string.Empty;
            model.Execute("hello world");
            Assert.Equal("\"hello world\"", model.History.Last());
        }

        [Fact]
        public void AGetPointWaiting_TakesATypedFormAsTextAndSpaceIsEnterAtThePointPrompt()
        {
            MainViewModel model = Opened();

            model.Execute("(getpoint)");
            model.CommandText = "(setq";
            Assert.False(model.PromptTakesSpaces);
            model.CommandText = "3,4";
            Assert.False(model.PromptTakesSpaces);
            model.CommandText = string.Empty;
            model.Execute("3,4");

            Assert.Equal("(3.0 4.0 0.0)", model.History.Last());
            Assert.False(model.Session.IsLispWaiting);
        }

        [Fact]
        public void AlertOfAForm_IsShownByTheWindowWithItsText()
        {
            MainViewModel model = Opened();
            var shown = new System.Collections.Generic.List<string>();
            model.ShowAlert = shown.Add;

            CadUiRequestEventArgs request = model.Session.Ask("ALERT", null, "Xong\nlan hai");

            Assert.True(request.Handled);
            Assert.Equal(new[] { "Xong\nlan hai" }, shown);
        }

        [Fact]
        public void AlertWithNoWindowToShowIt_IsLeftUnhandledForTheSessionToWrite()
        {
            MainViewModel model = Opened();

            CadUiRequestEventArgs request = model.Session.Ask("ALERT", null, "Xong");

            Assert.False(request.Handled);
        }

        [Fact]
        public void ClosingADocumentWhoseRoutineWaits_LeavesNoEvaluationWaiting()
        {
            MainViewModel model = Opened();
            CadCommandSession session = model.Session;
            model.Execute("(getpoint \"P: \")");
            Assert.True(session.IsLispWaiting);

            Assert.True(model.CloseDocument());

            Assert.False(session.IsLispWaiting);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ClosingADocumentWhoseRoutineWaitsInsideACommand_LeavesNothingRunning()
        {
            // The form typed at the prompt of LINE suspends it; one Esc ends the wait and LINE asks again, so closing goes on
            // until the session is idle.
            MainViewModel model = Opened();
            CadCommandSession session = model.Session;
            model.StartCommand("LINE");
            model.Execute("(getpoint \"P: \")");
            Assert.True(session.IsLispWaiting);

            Assert.True(model.CloseDocument());

            Assert.False(session.IsLispWaiting);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ACommandDefinedWithDefun_IsOfferedByAutoCompleteFromItsFirstLetters()
        {
            MainViewModel model = Opened();
            model.Execute("(defun c:ghzigzag () (princ \"zig\") (princ))");

            model.CommandText = "GHZI";
            model.ShowSuggestions();

            Assert.True(model.IsSuggestionListOpen);
            Assert.Equal("GHZIGZAG", model.Suggestions[0].Name);
        }

        [Fact]
        public void ACommandNamedLikeABuiltInOne_IsListedOnce()
        {
            MainViewModel model = Opened();
            model.Execute("(defun c:circle () (princ))");

            model.CommandText = "CIRCLE";
            model.ShowSuggestions();

            Assert.Single(model.Suggestions, r => r.Name == "CIRCLE");
        }

        [Fact]
        public void AppLoad_OfAnLspFile_LoadsItAndDefinesItsCommand()
        {
            MainViewModel model = Opened();
            string path = _folder.WriteText(
                "ghload.lsp",
                "(defun c:ghload () (princ \"loaded\") (princ))\n(princ \"hello\")"
            );
            model.ShowAppLoad = Loading(path);

            model.StartCommand("APPLOAD");

            Assert.Equal("ghload.lsp successfully loaded.", model.History.Last());
            Assert.Contains("hello", model.History);
            Assert.Equal(new[] { "GHLOAD" }, model.Session.LispCommandNames);

            model.Execute("GHLOAD");
            Assert.Equal("loaded", model.History.Last());
        }

        [Fact]
        public void AppLoad_OfATextFile_SaysItIsNeitherAnAutoLispFileNorAPlugin()
        {
            MainViewModel model = Opened();
            string path = _folder.WriteText("notes.txt", "hello");
            model.ShowAppLoad = Loading(path);

            model.StartCommand("APPLOAD");

            Assert.Equal("notes.txt is not an AutoLISP file or a plugin.", model.History.Last());
            Assert.Empty(model.Session.LispCommandNames);
        }

        [Fact]
        public void AppLoad_Cancelled_WritesNothing()
        {
            MainViewModel model = Opened();
            model.ShowAppLoad = dialog => false;
            int before = model.History.Count;

            model.StartCommand("APPLOAD");

            Assert.Equal(before + 1, model.History.Count);
            Assert.Equal("Command: APPLOAD", model.History.Last());
        }

        [Fact]
        public void AppLoad_OfADllWithNoPluginManager_SaysSo()
        {
            MainViewModel model = Opened();
            string path = _folder.WriteText("tool.dll", "x");
            model.ShowAppLoad = Loading(path);

            model.StartCommand("APPLOAD");

            Assert.Equal("No plugin manager is in use: applications cannot be loaded.", model.History.Last());
        }

        [Fact]
        public void LoadApplication_IsAvailableOnceTheWindowCanAskForAFile()
        {
            MainViewModel model = Opened();
            var ribbon = (ManageRibbonViewModel)model.ManageTab.TabContent;
            Assert.False(ribbon.CanLoadApplication);

            model.ShowAppLoad = dialog => false;

            Assert.True(ribbon.CanLoadApplication);
        }

        [Fact]
        public void ADrawingOpened_RunsTheAcaddocFileOfItsSupportFolderAndTheStartupSuite()
        {
            string support = Path.Combine(_folder.FullPath, "support");
            Directory.CreateDirectory(support);
            File.WriteAllText(
                Path.Combine(support, "acaddoc.lsp"),
                "(defun c:ghdoc () (princ \"doc\") (princ))\n(princ \"[acaddoc]\")",
                new UTF8Encoding(false)
            );
            string suite = _folder.WriteText("suite.lsp", "(princ \"[suite]\")");

            MainViewModel model = Opened(s =>
            {
                s.SupportSearchFolders = new[] { support };
                s.LispStartupSuite = new[] { suite };
            });

            Assert.Equal(
                new[] { "[acaddoc]", "[suite]" },
                model.History.Where(l => l.StartsWith("[", StringComparison.Ordinal))
            );
            Assert.Equal(new[] { "GHDOC" }, model.Session.LispCommandNames);
        }
    }
}
