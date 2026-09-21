using System;
using System.IO;
using System.Linq;
using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class GlobalCacheTests
    {
        private static string SampleModel => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "wall-extruded-solid.ifc");

        private static int CachedEntriesFor(string fullPath) =>
            IfcStoreCache.GetGlobalCacheInfo().Count(e => string.Equals(e.Key, fullPath, StringComparison.OrdinalIgnoreCase));

        [Theory]
        [InlineData("model.ifc")]           // relative to the current folder
        [InlineData(@"Models\model.ifc")]   // relative to the current folder
        [InlineData(@"\Models\model.ifc")]  // relative to the current drive
        [InlineData(@"C:model.ifc")]        // relative to the current folder of drive C:
        public void GetOrCreate_RejectsPathsThatAreNotFull(string path)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => IfcStoreCache.GetOrCreate(path));
            Assert.Contains("full path", ex.Message);
        }

        [Fact]
        public void ClearGlobalCache_RejectsPathsThatAreNotFull()
        {
            Assert.Throws<ArgumentException>(() => IfcStoreCache.ClearGlobalCache("model.ifc"));
        }

        [Fact]
        public void SpellingsOfOneFullPath_ShareOneCachedModel()
        {
            string fullPath = Path.GetFullPath(SampleModel);
            string folder = Path.GetDirectoryName(fullPath);
            string name = Path.GetFileName(fullPath);

            try
            {
                IfcStoreCache first = IfcStoreCache.GetOrCreate(fullPath);

                Assert.Same(first, IfcStoreCache.GetOrCreate(fullPath.Replace('\\', '/')));
                Assert.Same(first, IfcStoreCache.GetOrCreate(Path.Combine(folder, "..", Path.GetFileName(folder), name)));
                Assert.Same(first, IfcStoreCache.GetOrCreate(Path.Combine(folder, ".", name)));
                Assert.Same(first, IfcStoreCache.GetOrCreate(fullPath.ToUpperInvariant()));
                Assert.Equal(1, CachedEntriesFor(fullPath));

                // Released through yet another spelling of the same file.
                IfcStoreCache.ClearGlobalCache(fullPath.Replace('\\', '/'));
                Assert.Equal(0, CachedEntriesFor(fullPath));
            }
            finally
            {
                IfcStoreCache.ClearGlobalCache(fullPath);
            }
        }
    }
}
