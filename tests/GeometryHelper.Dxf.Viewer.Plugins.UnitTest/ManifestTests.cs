using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using GeometryHelper.Dxf.Viewer.Plugins;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Plugins.UnitTest
{
    public sealed class ManifestTests : IDisposable
    {
        private readonly PluginFiles _files = new PluginFiles();

        public void Dispose() => _files.Dispose();

        [Fact]
        public void AManifestGivesWhatThePluginSaysOfItself()
        {
            CadPluginManifest manifest = CadPluginManifest.Parse(PluginFiles.Manifest());

            Assert.Equal("number-circles", manifest.Id);
            Assert.Equal("Number circles", manifest.Name);
            Assert.Equal("1.2.0", manifest.Version);
            Assert.Equal("Nguyen Thang", manifest.Author);
            Assert.Equal("command", manifest.Kind);
            Assert.Equal(new Version(1, 0), manifest.MinimumApiVersion);
            Assert.Equal(new[] { "NUMCIRCLES", "FAILNOW" }, manifest.Commands);
            Assert.Equal("icon.png", manifest.Icon);
            Assert.Null(manifest.Refusal());
        }

        [Fact]
        public void AManifestWithANameOnlyIsACommandPluginOfApiOneWhoseIdIsItsName()
        {
            CadPluginManifest manifest = CadPluginManifest.Parse("{ \"name\": \"Tools\", \"extra\": 3 }");

            Assert.Equal("Tools", manifest.Id);
            Assert.Equal("command", manifest.Kind);
            Assert.Equal(new Version(1, 0), manifest.MinimumApiVersion);
            Assert.Empty(manifest.Commands);
            Assert.Null(manifest.Icon);
            Assert.Equal(string.Empty, manifest.Version);
        }

        [Theory]
        [InlineData("{ \"id\": \"x\" }", "no name")]
        [InlineData("not json", "not JSON")]
        [InlineData("{ \"name\": \"X\", \"minimumApiVersion\": \"one\" }", "not a version")]
        public void AManifestThatCannotBeReadIsRefusedSayingWhy(string json, string why)
        {
            FormatException refused = Assert.Throws<FormatException>(() => CadPluginManifest.Parse(json));

            Assert.Contains(why, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void APluginThatNeedsANewerApiOrIsOfAnotherKindIsRefusedSayingWhy()
        {
            Assert.Equal(
                "The plugin Number circles needs version 2.0 of the plugin API, and this presenter has 1.0: it needs a newer presenter.",
                CadPluginManifest.Parse(PluginFiles.Manifest(api: "2.0")).Refusal());
            Assert.Equal(
                "The plugin Number circles is of the kind \"drawing\", and this presenter loads command plugins only.",
                CadPluginManifest.Parse(PluginFiles.Manifest(kind: "drawing")).Refusal());
        }

        [Fact]
        public void TheManifestOfAPackageIsReadFromTheZipWithoutUnpackingIt()
        {
            string package = _files.Package(PluginFiles.Manifest());

            CadPluginManifest manifest = CadPluginManifest.Read(package);

            Assert.Equal("Number circles", manifest.Name);
            Assert.Equal(package, Assert.Single(Directory.GetFileSystemEntries(_files.Root)));
        }

        [Fact]
        public void APackageIsUnpackedWithItsFolders()
        {
            string package = _files.Package(PluginFiles.Manifest(), "sample.ghplugin", new KeyValuePair<string, string>("data/readme.txt", "Numbers circles."));
            string folder = Path.Combine(_files.Root, "unpacked");

            CadPluginManifest manifest = CadPluginPackage.Unpack(package, folder);

            Assert.Equal("number-circles", manifest.Id);
            Assert.True(File.Exists(Path.Combine(folder, CadPluginManifest.FileName)));
            Assert.True(File.Exists(Path.Combine(folder, PluginFiles.SampleName + ".dll")));
            Assert.Equal("Numbers circles.", File.ReadAllText(Path.Combine(folder, "data", "readme.txt")));
        }

        [Fact]
        public void AnEntryThatWouldLandOutsideTheFolderIsRefusedAndNothingIsWritten()
        {
            string package = _files.Package(PluginFiles.Manifest(), "evil.ghplugin", new KeyValuePair<string, string>("../evil.txt", "out"));
            string folder = Path.Combine(_files.Root, "unpacked");

            FormatException refused = Assert.Throws<FormatException>(() => CadPluginPackage.Unpack(package, folder));

            Assert.Contains("../evil.txt", refused.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(folder));
            Assert.False(File.Exists(Path.Combine(_files.Root, "evil.txt")));
        }

        [Fact]
        public void APackageWithNoManifestAtItsTopIsRefused()
        {
            string package = Path.Combine(_files.Root, "bare.ghplugin");

            using (ZipArchive zip = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(PluginFiles.BuiltSample, PluginFiles.SampleName + ".dll");
            }

            Assert.Throws<FileNotFoundException>(() => CadPluginManifest.Read(package));
        }
    }
}
