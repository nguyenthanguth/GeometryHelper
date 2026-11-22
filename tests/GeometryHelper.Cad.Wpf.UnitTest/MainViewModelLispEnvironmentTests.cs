using System;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// What AutoLISP's <c>setenv</c> and <c>setcfg</c> keep, in the window (C4a, D1218): the drawings of the app share one store
    /// built from Global settings, a routine's change is saved in the settings file at once, and settings applied again are
    /// read again. The two requests of <c>textscr</c> and <c>graphscr</c> for the text window are answered too.
    /// </summary>
    public sealed class MainViewModelLispEnvironmentTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string SettingsFile => Path.Combine(_folder.FullPath, "settings.json");

        private string Room(string name) => _folder.Write(name, TestFiles.Room(out _, out _));

        private MainViewModel Opened(GlobalSettings settings = null)
        {
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.ApplySettings(settings ?? new GlobalSettings());
            Assert.True(model.Open(Room("a.dxf")));
            return model;
        }

        private static string Run(MainViewModel model, string form) => model.Session.Lisp.Run(form).Output;

        [Fact]
        public void WhatTheSettingsHold_IsWhatGetenvAndGetcfgGive_InEveryDrawing()
        {
            var settings = new GlobalSettings();
            settings.SetLispEnvironment(
                new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("ZZC4A", "from settings"),
                    new System.Collections.Generic.KeyValuePair<string, string>("cfg:AppData/ZzC4a/Last", "7"),
                }
            );
            MainViewModel model = Opened(settings);
            CadCommandSession first = model.Session;
            Assert.True(model.Open(Room("b.dxf")));

            Assert.NotSame(first, model.Session);
            Assert.Equal("\"from settings\"", Run(model, "(getenv \"zzc4a\")"));
            Assert.Equal("\"7\"", Run(model, "(getcfg \"AppData/ZzC4a/Last\")"));
            Assert.Equal("\"from settings\"", first.Lisp.Run("(getenv \"ZZC4A\")").Output);
        }

        [Fact]
        public void ARoutinesSetenvInOneDrawing_IsThereInTheOtherAndSavedInTheSettingsFile()
        {
            MainViewModel model = Opened();
            CadCommandSession first = model.Session;
            Assert.True(model.Open(Room("b.dxf")));

            Assert.Equal("\"on\"", Run(model, "(setenv \"ZZC4A\" \"on\")"));
            Assert.Equal("\"1\"", Run(model, "(setcfg \"AppData/ZzC4a/Count\" \"1\")"));

            Assert.Equal("\"on\"", first.Lisp.Run("(getenv \"ZZC4A\")").Output);
            GlobalSettings saved = GlobalSettings.Load(SettingsFile);
            Assert.Equal("on", saved.LispEnvironment["ZZC4A"]);
            Assert.Equal("1", saved.LispEnvironment["cfg:AppData/ZzC4a/Count"]);
        }

        [Fact]
        public void AValueTakenAway_IsTakenOutOfTheSettingsFileToo()
        {
            MainViewModel model = Opened();

            Run(model, "(setenv \"ZZC4A\" \"on\")");
            Run(model, "(setenv \"ZZC4A\" \"\")");

            Assert.Equal("nil", Run(model, "(getenv \"ZZC4A\")"));
            Assert.DoesNotContain("ZZC4A", GlobalSettings.Load(SettingsFile).LispEnvironment.Keys);
        }

        [Fact]
        public void SettingsAppliedAgain_ReplaceWhatTheStoreHeld_WithoutWritingTheFile()
        {
            MainViewModel model = Opened();
            Run(model, "(setenv \"ZZC4A\" \"old\")");
            File.Delete(SettingsFile);

            var settings = new GlobalSettings();
            settings.SetLispEnvironment(
                new[] { new System.Collections.Generic.KeyValuePair<string, string>("ZZC4A", "new") }
            );
            model.ApplySettings(settings);

            Assert.Equal("\"new\"", Run(model, "(getenv \"ZZC4A\")"));
            Assert.False(File.Exists(SettingsFile));
        }

        [Fact]
        public void TextscrAndTextpageShowTheTextWindow_AndGraphscrPutsItAway()
        {
            MainViewModel model = Opened();
            TextWindowViewModel shown = null;
            model.ShowTextWindow = window => shown = window;
            int closed = 0;

            Assert.Equal("nil", Run(model, "(textscr)"));
            Assert.NotNull(shown);
            Assert.Same(model.History, shown.Lines);
            shown.CloseRequested += (s, e) => closed++;

            shown = null;
            Assert.Equal("nil", Run(model, "(textpage)"));
            Assert.NotNull(shown);
            Assert.Equal("nil", Run(model, "(graphscr)"));
            Assert.Equal(1, closed);
        }

        [Fact]
        public void WithNoTextWindowShown_TheScreenFunctionsGiveNilAndWriteNothing()
        {
            MainViewModel model = Opened();
            int lines = model.History.Count;

            Assert.Equal("nil", Run(model, "(textscr)"));
            Assert.Equal("nil", Run(model, "(graphscr)"));
            Assert.Equal(lines, model.History.Count);
            Assert.Empty(model.History.Skip(lines));
        }
    }
}
