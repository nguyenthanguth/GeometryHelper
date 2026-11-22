using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The known applications of <see cref="GlobalSettings"/>: the AutoLISP files and plugins Load/Unload Applications lists
    /// though they are not loaded, each with the commands it last defined, kept in the settings file between runs.
    /// </summary>
    public sealed class KnownApplicationsSettingsTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-settings-known-" + Guid.NewGuid().ToString("N")
        );

        public KnownApplicationsSettingsTests()
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
            catch (UnauthorizedAccessException) { }
        }

        [Fact]
        public void ByDefault_NoApplicationIsKnown()
        {
            Assert.Empty(new GlobalSettings().KnownApplications);
        }

        [Fact]
        public void TheKnownApplications_SurviveASaveAndALoad_PathsWithBackslashesAndAColonIncluded()
        {
            string path = Path.Combine(_folder, "settings.json");
            var settings = new GlobalSettings();
            settings.SetKnownApplications(
                new[]
                {
                    new KeyValuePair<string, string>(@"C:\Lisp\b.lsp", "BB, B2"),
                    new KeyValuePair<string, string>(@"C:\Lisp\a.lsp", string.Empty),
                    new KeyValuePair<string, string>(@"D:\Plugins\X.dll", "XC"),
                }
            );

            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(
                new[] { @"C:\Lisp\a.lsp", @"C:\Lisp\b.lsp", @"D:\Plugins\X.dll" },
                read.KnownApplications.Keys.ToArray()
            );
            Assert.Equal("BB, B2", read.KnownApplications[@"C:\Lisp\b.lsp"]);
            Assert.Equal(string.Empty, read.KnownApplications[@"C:\Lisp\a.lsp"]);
            Assert.Equal("XC", read.KnownApplications[@"d:\plugins\x.dll"]);
        }

        [Fact]
        public void ABlankPathIsLeftOut_AndACopyShareNothingWithTheSettings()
        {
            var settings = new GlobalSettings();
            settings.SetKnownApplications(
                new[]
                {
                    new KeyValuePair<string, string>("  ", "X"),
                    new KeyValuePair<string, string>(@"C:\Lisp\a.lsp", "A"),
                }
            );

            GlobalSettings copy = settings.Clone();
            copy.SetKnownApplications(Array.Empty<KeyValuePair<string, string>>());

            Assert.Single(settings.KnownApplications);
            Assert.Empty(copy.KnownApplications);
            Assert.Throws<ArgumentNullException>(() => settings.SetKnownApplications(null));
        }
    }
}
