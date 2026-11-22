using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The Plugins folder beside the program (<see cref="CadSupportKind.Plugins"/>): shipped with a README.txt as Fonts,
    /// Hatches, Linetypes and Lisps are, and on none of the search paths of fonts, hatch patterns, linetypes or AutoLISP.
    /// </summary>
    public sealed class PluginsFolderKindTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gh-plugins-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [Fact]
        public void ThePluginsFolder_IsTheFifthDefaultFolderBesideTheProgram()
        {
            var roots = new CadSupportFolders(_root);

            Assert.Equal(Path.Combine(_root, "Plugins"), roots.FolderOf(CadSupportKind.Plugins));
            Assert.Empty(roots.ExistingOf(CadSupportKind.Plugins));
            Directory.CreateDirectory(Path.Combine(_root, "Plugins"));
            Assert.Equal(new[] { Path.Combine(_root, "Plugins") }, roots.ExistingOf(CadSupportKind.Plugins));
            Assert.Null(new CadSupportFolders(null).FolderOf(CadSupportKind.Plugins));
        }

        [Fact]
        public void ThePluginsFolderShipped_IsBesideTheDll_WithItsReadme()
        {
            CadSupportFolders folders = CadSupportFolders.Default;

            Assert.Single(folders.ExistingOf(CadSupportKind.Plugins));
            Assert.True(File.Exists(Path.Combine(folders.FolderOf(CadSupportKind.Plugins), "README.txt")));
        }

        [Fact]
        public void ThePluginsFolder_IsOnNoSupportSearchPath()
        {
            Directory.CreateDirectory(Path.Combine(_root, "Plugins"));
            Directory.CreateDirectory(Path.Combine(_root, "Lisps"));
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), Path.Combine(_root, "a.dxf"));
            document.SupportRoots = new CadSupportFolders(_root);
            string plugins = Path.Combine(_root, "Plugins");

            foreach (
                CadSupportKind? kind in new CadSupportKind?[]
                {
                    CadSupportKind.Fonts,
                    CadSupportKind.Hatches,
                    CadSupportKind.Linetypes,
                    null,
                }
            )
            {
                Assert.DoesNotContain(CadSupportFiles.FoldersOf(document, kind), f => f == plugins);
            }

            var session = new CadCommandSession(document, CadCommandRegistry.CreateDefault());

            Assert.Equal(new[] { Path.Combine(_root, "Lisps") }, session.Lisp.SupportFolders);
        }
    }
}
