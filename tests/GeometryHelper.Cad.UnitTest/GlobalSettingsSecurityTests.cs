using System;
using System.IO;
using System.Text;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The four security settings of <see cref="GlobalSettings"/> (decision D911 of the L6 spec): SECURELOAD, TRUSTEDPATHS,
    /// LEGACYCODESEARCH and the files answered Always Load, kept in the JSON object <c>security</c>. A value of the file
    /// that is not valid falls back to its default, each on its own, as every other setting does.
    /// </summary>
    public sealed class GlobalSettingsSecurityTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-settings-security-" + Guid.NewGuid().ToString("N")
        );

        public GlobalSettingsSecurityTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        // A hash as the settings file holds one: 64 hexadecimal digits.
        private static readonly string HashA = new string('a', 64);
        private static readonly string HashB = new string('b', 64);

        // A settings file holding the text given as its JSON.
        private GlobalSettings Read(string json)
        {
            string path = Path.Combine(_folder, "settings.json");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return GlobalSettings.Load(path);
        }

        [Fact]
        public void TheSecuritySettings_AreAutoCadsInitialValues_ByDefault()
        {
            // AutoCAD 2021 (research M1): SECURELOAD 1, LEGACYCODESEARCH 0; nothing trusted beyond the app, nothing always loaded
            var settings = new GlobalSettings();

            Assert.Equal(1, settings.SecureLoad);
            Assert.Equal(string.Empty, settings.TrustedPaths);
            Assert.False(settings.LegacyCodeSearch);
            Assert.Empty(settings.AlwaysLoadedFiles);
        }

        [Fact]
        public void TheSecuritySettings_KeepTheirValuesThroughTheFile()
        {
            string path = Path.Combine(_folder, "settings.json");
            string trusted = @"C:\Lisp;D:\Tools\Routines\...";
            string[] always = { HashA + @"|C:\Lisp\a.lsp", HashB + @"|D:\Tools\b.py" };

            new GlobalSettings
            {
                SecureLoad = 2,
                TrustedPaths = trusted,
                LegacyCodeSearch = true,
                AlwaysLoadedFiles = always,
            }.Save(path);

            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(2, read.SecureLoad);
            Assert.Equal(trusted, read.TrustedPaths);
            Assert.True(read.LegacyCodeSearch);
            Assert.Equal(always, read.AlwaysLoadedFiles);
        }

        [Fact]
        public void TheFileHoldsThemInAnObjectCalledSecurity_UnderTheKeysOfTheSpec()
        {
            string json = new GlobalSettings
            {
                SecureLoad = 0,
                TrustedPaths = @"C:\x",
                LegacyCodeSearch = true,
                AlwaysLoadedFiles = new[] { HashA + @"|C:\a.lsp" },
            }.ToJson();

            Assert.Contains("\"security\"", json);
            Assert.Contains("\"secureLoad\"", json);
            Assert.Contains("\"trustedPaths\"", json);
            Assert.Contains("\"legacyCodeSearch\"", json);
            Assert.Contains("\"alwaysLoaded\"", json);
        }

        [Fact]
        public void AHandWrittenSecurityObject_IsRead_KeyByKey()
        {
            GlobalSettings read = Read(
                @"{ ""security"": { ""secureLoad"": 2, ""trustedPaths"": ""C:\\a;C:\\b\\..."", ""legacyCodeSearch"": true,"
                    + @" ""alwaysLoaded"": [ """
                    + HashA
                    + @"|C:\\x.lsp"", """
                    + HashB
                    + @"|C:\\y.lsp"" ] } }"
            );

            Assert.Equal(2, read.SecureLoad);
            Assert.Equal(@"C:\a;C:\b\...", read.TrustedPaths);
            Assert.True(read.LegacyCodeSearch);
            Assert.Equal(new[] { HashA + @"|C:\x.lsp", HashB + @"|C:\y.lsp" }, read.AlwaysLoadedFiles);
        }

        [Fact]
        public void AFileWithNoSecurityObject_GivesTheDefaults()
        {
            // a settings file written before this version: nothing to read, nothing lost
            GlobalSettings read = Read(@"{ ""support"": { ""appLoadFolder"": ""C:\\Lisp"" } }");

            Assert.Equal(1, read.SecureLoad);
            Assert.Equal(string.Empty, read.TrustedPaths);
            Assert.False(read.LegacyCodeSearch);
            Assert.Empty(read.AlwaysLoadedFiles);
            Assert.Equal(@"C:\Lisp", read.AppLoadFolder);
        }

        [Theory]
        [InlineData("\"two\"")] // not a number
        [InlineData("7")] // out of 0..2
        [InlineData("-1")]
        [InlineData("1.5")] // not whole
        public void ASecureLoadThatIsNotValid_FallsBackToItsDefault_AndTheOthersAreStillRead(string value)
        {
            GlobalSettings read = Read(
                @"{ ""security"": { ""secureLoad"": "
                    + value
                    + @", ""trustedPaths"": ""C:\\a"", ""legacyCodeSearch"": true } }"
            );

            Assert.Equal(1, read.SecureLoad);
            Assert.Equal(@"C:\a", read.TrustedPaths);
            Assert.True(read.LegacyCodeSearch);
        }

        [Fact]
        public void ALegacyCodeSearchThatIsNotABoolean_FallsBackToItsDefault_AndAnUnknownKeyIsLeftAlone()
        {
            GlobalSettings read = Read(
                @"{ ""security"": { ""legacyCodeSearch"": ""yes"", ""secureLoad"": 0, ""somethingNew"": 5 } }"
            );

            Assert.False(read.LegacyCodeSearch);
            Assert.Equal(0, read.SecureLoad);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(-1)]
        public void ASecureLoadOutsideZeroToTwo_Throws_AndLeavesTheValue(int value)
        {
            var settings = new GlobalSettings { SecureLoad = 2 };

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SecureLoad = value);
            Assert.Equal(2, settings.SecureLoad);
        }

        [Fact]
        public void NullTrustedPaths_IsEmpty_AndNullAlwaysLoadedFiles_IsAnEmptyList()
        {
            var settings = new GlobalSettings
            {
                TrustedPaths = @"C:\a",
                AlwaysLoadedFiles = new[] { HashA + @"|C:\a.lsp" },
            };

            settings.TrustedPaths = null;
            settings.AlwaysLoadedFiles = null;

            Assert.Equal(string.Empty, settings.TrustedPaths);
            Assert.Empty(settings.AlwaysLoadedFiles);
        }

        [Fact]
        public void ACopyOfTheSettings_HasSecuritySettingsOfItsOwn()
        {
            var settings = new GlobalSettings
            {
                SecureLoad = 2,
                TrustedPaths = @"C:\a",
                LegacyCodeSearch = true,
                AlwaysLoadedFiles = new[] { HashA + @"|C:\a.lsp" },
            };
            GlobalSettings copy = settings.Clone();

            Assert.Equal(2, copy.SecureLoad);
            Assert.Equal(@"C:\a", copy.TrustedPaths);
            Assert.True(copy.LegacyCodeSearch);
            Assert.Equal(new[] { HashA + @"|C:\a.lsp" }, copy.AlwaysLoadedFiles);

            copy.SecureLoad = 0;
            copy.TrustedPaths = @"C:\b";
            copy.LegacyCodeSearch = false;
            copy.AlwaysLoadedFiles = new[] { HashB + @"|C:\b.lsp" };

            Assert.Equal(2, settings.SecureLoad);
            Assert.Equal(@"C:\a", settings.TrustedPaths);
            Assert.True(settings.LegacyCodeSearch);
            Assert.Equal(new[] { HashA + @"|C:\a.lsp" }, settings.AlwaysLoadedFiles);
        }
    }
}
