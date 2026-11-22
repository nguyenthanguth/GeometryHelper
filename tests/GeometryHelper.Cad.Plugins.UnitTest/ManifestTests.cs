using System;
using System.IO;
using GeometryHelper.Cad.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Plugins.UnitTest
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
                "The plugin Number circles needs version 2.0 of the plugin API, and this presenter has 1.1: it needs a newer presenter.",
                CadPluginManifest.Parse(PluginFiles.Manifest(api: "2.0")).Refusal()
            );
            Assert.Equal(
                "The plugin Number circles is of the kind \"drawing\", and this presenter loads command plugins only.",
                CadPluginManifest.Parse(PluginFiles.Manifest(kind: "drawing")).Refusal()
            );
        }

        [Fact]
        public void TheManifestOfAPluginJsonFileIsRead_AndAFileOfAnyOtherNameIsReadAsJsonToo()
        {
            string path = Path.Combine(_files.Root, CadPluginManifest.FileName);
            File.WriteAllText(path, PluginFiles.Manifest());

            CadPluginManifest manifest = CadPluginManifest.Read(path);

            Assert.Equal("Number circles", manifest.Name);
            Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(_files.Root)));
            Assert.Throws<FileNotFoundException>(() => CadPluginManifest.Read(Path.Combine(_files.Root, "none.json")));

            // A .ghplugin is not a package any more: it is read as the JSON it is not.
            string zip = Path.Combine(_files.Root, "sample.ghplugin");
            File.WriteAllBytes(zip, new byte[] { 0x50, 0x4b, 3, 4 });
            Assert.Throws<FormatException>(() => CadPluginManifest.Read(zip));
        }
    }
}
