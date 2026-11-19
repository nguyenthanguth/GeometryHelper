using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The DRAWINGS list names what a file is, DWG or DXF, in its line, its folder's header and the chip; and an extension
    /// names a file of its own by its path (<see cref="CadWorkspaceExtension.DescribeFile(string)"/>) before it is opened.
    /// </summary>
    public sealed class FileKindTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ADwgFileListed_IsCalledDwg_AndTheFolderAndChipSayBoth()
        {
            _folder.Write("a.dwg", TestFiles.Room(out _, out _));
            _folder.Write("b.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();

            model.ShowFolder(_folder.FullPath);

            Assert.EndsWith(" · DWG/DXF", model.FileRows.Single(r => r.IsHeader).Label);
            Assert.StartsWith("DWG · ", model.FileRows.Single(r => r.Label == "a.dwg").Detail);
            Assert.StartsWith("DXF · ", model.FileRows.Single(r => r.Label == "b.dxf").Detail);
            Assert.Equal("DWG/DXF 2", model.DxfChipText);
        }

        [Fact]
        public void AFolderOfDwgFilesOnly_IsHeadedDwg()
        {
            _folder.Write("a.dwg", TestFiles.Room(out _, out _));
            var model = new MainViewModel();

            model.ShowFolder(_folder.FullPath);

            Assert.EndsWith(" · DWG", model.FileRows.Single(r => r.IsHeader).Label);
        }

        [Fact]
        public void AnExtensionThatKnowsAFileByItsPath_NamesItBeforeItIsOpened()
        {
            string a = _folder.Write("a.dwg", TestFiles.Room(out _, out _));
            _folder.Write("b.dwg", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            model.AddExtension(new ByPath(Path.GetFullPath(a)));

            model.ShowFolder(_folder.FullPath);

            FileRowViewModel row = model.FileRows.Single(r => r.Path == Path.GetFullPath(a));
            Assert.False(row.IsOpen);
            Assert.Equal("REBAR DRAWING SLAB", row.Label);
            Assert.Equal("S1 · SLAB", row.Detail);
            Assert.Equal("A1", row.Right);
            Assert.Contains(model.FileRows, r => r.IsHeader && r.Label == "Rebar drawings");
            Assert.Equal("Rebar drawings 1", model.OwnChipText);
            Assert.Equal("DWG/DXF 1", model.DxfChipText);
        }

        private sealed class ByPath : CadWorkspaceExtension
        {
            private readonly string _path;

            public ByPath(string path)
            {
                _path = path;
            }

            public override string FileKindName => "Rebar drawings";

            public override CadFileInfo DescribeFile(string path) =>
                string.Equals(path, _path, StringComparison.OrdinalIgnoreCase)
                    ? new CadFileInfo("Rebar drawings", "REBAR DRAWING SLAB", "S1 · SLAB", "A1", CadFileState.UpToDate)
                    : null;
        }
    }
}
