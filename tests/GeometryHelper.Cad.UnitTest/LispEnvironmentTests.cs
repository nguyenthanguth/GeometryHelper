using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>getenv</c>, <c>setenv</c>, <c>getcfg</c> and <c>setcfg</c> against AutoCAD 2021's accoreconsole (probes g1, g2 and g14 of
    /// the C4a measurements), the store behind them, and the Global settings file that keeps it. AutoCAD keeps the values in
    /// the registry; the library keeps them in memory and the window in the settings.
    /// </summary>
    [Collection("LispSerial")]
    public class LispEnvironmentTests
    {
        private static string Out(LispInterpreter lisp, string expression) => lisp.Run(expression).Output;

        #region setenv and getenv

        [Fact]
        public void ASetName_IsGivenBackWhateverTheCaseOfTheName()
        {
            // AutoCAD 2021 (g1): (setenv "ZZMyVar" "hello") "hello", then getenv of "ZZMyVar" and "zzmyvar" "hello"; setting
            // "zzmyvar" again replaces it, and "ZZMYVAR" reads the new one.
            var lisp = new LispInterpreter();

            Assert.Equal("\"hello\"", Out(lisp, "(setenv \"ZZMyVar\" \"hello\")"));
            Assert.Equal("\"hello\"", Out(lisp, "(getenv \"ZZMyVar\")"));
            Assert.Equal("\"hello\"", Out(lisp, "(getenv \"zzmyvar\")"));
            Assert.Equal("\"x y\"", Out(lisp, "(setenv \"zzmyvar\" \"x y\")"));
            Assert.Equal("\"x y\"", Out(lisp, "(getenv \"ZZMYVAR\")"));
        }

        [Fact]
        public void ANameNeverSet_IsNil()
        {
            // AutoCAD 2021 (g1): (getenv "ZZNOPE") nil
            Assert.Equal("nil", Out(new LispInterpreter(), "(getenv \"ZZNOPE_C4A\")"));
        }

        [Fact]
        public void TheEmptyText_TakesTheNameAwayAndGivesTheEmptyText()
        {
            // AutoCAD 2021 (g1): (setenv "ZZMyVar" "") "", then (getenv "ZZMyVar") nil
            var lisp = new LispInterpreter();
            Out(lisp, "(setenv \"ZZMyVar\" \"x\")");

            Assert.Equal("\"\"", Out(lisp, "(setenv \"ZZMyVar\" \"\")"));
            Assert.Equal("nil", Out(lisp, "(getenv \"ZZMyVar\")"));
        }

        [Fact]
        public void AnUnsetName_FallsBackToTheWindowsEnvironment_AndASetOneShadowsIt()
        {
            // AutoCAD 2021 (g2, g14): (getenv "COMPUTERNAME") is the machine's name; set to "zzfake" it is "zzfake"; set to ""
            // it is the machine's name again; the case of the name does not matter ("username").
            var lisp = new LispInterpreter();
            string machine = Environment.GetEnvironmentVariable("COMPUTERNAME");
            Assert.False(string.IsNullOrEmpty(machine));

            Assert.Equal("\"" + machine + "\"", Out(lisp, "(getenv \"computername\")"));
            Out(lisp, "(setenv \"COMPUTERNAME\" \"zzfake\")");
            Assert.Equal("\"zzfake\"", Out(lisp, "(getenv \"COMPUTERNAME\")"));
            Out(lisp, "(setenv \"COMPUTERNAME\" \"\")");
            Assert.Equal("\"" + machine + "\"", Out(lisp, "(getenv \"COMPUTERNAME\")"));
        }

        [Theory]
        [InlineData("(setenv \"ZZMyVar\" nil)", "; error: bad argument type: stringp nil")] // AutoCAD 2021 (g1)
        [InlineData("(setenv \"ZZMyVar\" 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g1)
        [InlineData("(setenv 5 \"a\")", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g1)
        [InlineData("(setenv \"ZZMyVar\")", "; error: too few arguments")] // AutoCAD 2021 (g1)
        [InlineData("(getenv)", "; error: too few arguments")] // AutoCAD 2021 (g1)
        [InlineData("(getenv 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g1)
        [InlineData("(getenv nil)", "; error: bad argument type: stringp nil")] // AutoCAD 2021 (g1)
        [InlineData("(getenv \"ZZMyVar\" \"b\")", "; error: too many arguments")] // AutoCAD 2021 (g1)
        [InlineData("(getcfg)", "; error: too few arguments")] // AutoCAD 2021 (g1)
        [InlineData("(getcfg 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g1)
        [InlineData("(getcfg \"AppData/ZZ/Key\" \"a\")", "; error: too many arguments")] // AutoCAD 2021 (g1)
        [InlineData("(setcfg \"AppData/ZZ/Key\" 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g1)
        [InlineData("(setcfg \"AppData/ZZ/Key\" nil)", "; error: bad argument type: stringp nil")] // AutoCAD 2021 (g1)
        [InlineData("(setcfg \"AppData/ZZ/Key\")", "; error: too few arguments")] // AutoCAD 2021 (g1)
        public void WrongArguments_GiveAutoCadsErrors(string expression, string output) =>
            Assert.Equal(output, Out(new LispInterpreter(), expression));

        #endregion

        #region setcfg and getcfg

        [Fact]
        public void ASetPath_IsGivenBack_ThePathsCaseDoesNotMatter_AndTheEmptyTextIsKept()
        {
            // AutoCAD 2021 (g1): (setcfg "AppData/ZZ/Key" "val") "val", getcfg of that and of "appdata/zz/key" "val"; set to ""
            // it gives "" and getcfg then gives "", not nil; "abc\ndef" is kept as it is.
            var lisp = new LispInterpreter();

            Assert.Equal("nil", Out(lisp, "(getcfg \"AppData/ZZ/Key\")"));
            Assert.Equal("\"val\"", Out(lisp, "(setcfg \"AppData/ZZ/Key\" \"val\")"));
            Assert.Equal("\"val\"", Out(lisp, "(getcfg \"AppData/ZZ/Key\")"));
            Assert.Equal("\"val\"", Out(lisp, "(getcfg \"appdata/zz/key\")"));
            Assert.Equal("\"\"", Out(lisp, "(setcfg \"AppData/ZZ/Key\" \"\")"));
            Assert.Equal("\"\"", Out(lisp, "(getcfg \"AppData/ZZ/Key\")"));
            Assert.Equal("\"abc\\ndef\"", Out(lisp, "(setcfg \"AppData/ZZ/Key\" \"abc\\ndef\")"));
            Assert.Equal("\"abc\\ndef\"", Out(lisp, "(getcfg \"AppData/ZZ/Key\")"));
        }

        [Fact]
        public void AFolderThatHoldsAValue_GivesTheEmptyTextForANameItDoesNot_AndOneThatHoldsNothingGivesNil()
        {
            // AutoCAD 2021 (g1): after (setcfg "AppData/ZZ/Key" "val"): (getcfg "AppData/ZZ") "" and (getcfg "AppData/ZZ/Key3") "";
            // (getcfg "AppData/ZZ/Deep/Key4") nil until (setcfg "AppData/ZZ/Deep/Key4" "d") makes the folder.
            var lisp = new LispInterpreter();
            Out(lisp, "(setcfg \"AppData/ZZ/Key\" \"val\")");

            Assert.Equal("\"\"", Out(lisp, "(getcfg \"AppData/ZZ\")"));
            Assert.Equal("\"\"", Out(lisp, "(getcfg \"AppData/ZZ/Key3\")"));
            Assert.Equal("nil", Out(lisp, "(getcfg \"AppData/ZZ/Deep/Key4\")"));
            Assert.Equal("\"d\"", Out(lisp, "(setcfg \"AppData/ZZ/Deep/Key4\" \"d\")"));
            Assert.Equal("\"d\"", Out(lisp, "(getcfg \"AppData/ZZ/Deep/Key4\")"));
            Assert.Equal("nil", Out(lisp, "(getcfg \"AppData\")"));
        }

        [Theory]
        [InlineData("(setcfg \"ZZNope\" \"x\")")] // AutoCAD 2021 (g1): nil, and (getcfg "ZZNope") stays nil
        [InlineData("(setcfg \"AppData\" \"a\")")] // AutoCAD 2021 (g1): nil
        [InlineData("(setcfg \"AppData/ZZ/\" \"a\")")] // AutoCAD 2021 (g1): (getcfg "AppData/ZZ/") is nil
        public void APathWithNoFolderAndName_IsNotKept(string expression)
        {
            var lisp = new LispInterpreter();

            Assert.Equal("nil", Out(lisp, expression));
            Assert.Equal("nil", Out(lisp, "(getcfg \"ZZNope\")"));
            Assert.Equal("nil", Out(lisp, "(getcfg \"AppData/ZZ/\")"));
        }

        [Fact]
        public void TheConfigurationAndTheEnvironmentAreApart()
        {
            var lisp = new LispInterpreter();
            Out(lisp, "(setcfg \"AppData/ZZ/Key\" \"cfg\")");
            Out(lisp, "(setenv \"AppData/ZZ/Key\" \"env\")");

            Assert.Equal("\"cfg\"", Out(lisp, "(getcfg \"AppData/ZZ/Key\")"));
            Assert.Equal("\"env\"", Out(lisp, "(getenv \"AppData/ZZ/Key\")"));
        }

        #endregion

        #region The store and the session

        [Fact]
        public void TheStore_SaysWhenARoutineChangedIt_ButNotWhenItIsLoaded()
        {
            var store = new LispEnvironment();
            int changes = 0;
            store.Changed += (s, e) => changes++;

            store.Load(
                new[]
                {
                    new KeyValuePair<string, string>("A", "1"),
                    new KeyValuePair<string, string>("cfg:AppData/X/Y", ""),
                }
            );
            Assert.Equal(0, changes);
            Assert.Equal("1", store.GetEnvironment("a"));
            Assert.Equal(string.Empty, store.GetConfig("AppData/X/Y"));

            store.SetEnvironment("B", "2");
            Assert.True(store.SetConfig("AppData/X/Z", "3"));
            Assert.False(store.SetConfig("NoFolder", "4"));
            Assert.Equal(2, changes);
            Assert.Equal(
                new[] { "A=1", "B=2", "cfg:AppData/X/Y=", "cfg:AppData/X/Z=3" },
                store.Entries.Select(e => e.Key + "=" + e.Value).ToArray()
            );
        }

        [Fact]
        public void TwoSessionsGivenTheSameStore_ShareWhatARoutineSets()
        {
            var store = new LispEnvironment();
            CadCommandSession one = Session(Blank());
            CadCommandSession two = Session(Blank());
            one.LispEnvironment = store;
            two.LispEnvironment = store;

            one.Lisp.Run("(setenv \"ZZSHARED\" \"yes\")");

            Assert.Equal("\"yes\"", two.Lisp.Run("(getenv \"ZZSHARED\")").Output);
            Assert.Equal("yes", store.GetEnvironment("ZZSHARED"));
        }

        #endregion

        #region The Global settings file

        [Fact]
        public void TheSettingsFile_KeepsWhatRoutinesSet_EmptyConfigurationValuesToo()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-c4a-env-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                var settings = new GlobalSettings();
                settings.SetLispEnvironment(
                    new[]
                    {
                        new KeyValuePair<string, string>("MyTool", "C:\\x y"),
                        new KeyValuePair<string, string>("cfg:AppData/MyTool/Last", ""),
                        new KeyValuePair<string, string>("cfg:AppData/MyTool/Name", "a \"b\""),
                    }
                );
                settings.Save(path);

                GlobalSettings loaded = GlobalSettings.Load(path);

                Assert.Equal(
                    new[] { "cfg:AppData/MyTool/Last=", "cfg:AppData/MyTool/Name=a \"b\"", "MyTool=C:\\x y" },
                    loaded.LispEnvironment.Select(p => p.Key + "=" + p.Value).ToArray()
                );
                Assert.Equal(
                    loaded.LispEnvironment.Select(p => p.Key + "=" + p.Value).ToArray(),
                    loaded.Clone().LispEnvironment.Select(p => p.Key + "=" + p.Value).ToArray()
                );
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ABlankNameOrANullValue_IsLeftOutOfTheSettings()
        {
            var settings = new GlobalSettings();
            settings.SetLispEnvironment(
                new[]
                {
                    new KeyValuePair<string, string>(" ", "x"),
                    new KeyValuePair<string, string>("A", null),
                    new KeyValuePair<string, string>("B", "b"),
                }
            );

            Assert.Equal("B", Assert.Single(settings.LispEnvironment).Key);
            Assert.Throws<ArgumentNullException>(() => settings.SetLispEnvironment(null));
        }

        #endregion
    }
}
