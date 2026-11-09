using System;
using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A file the window cannot read as it is opens as what could be recovered of it, unsaved, the damaged file left alone.</summary>
    public class RecoverWindowTests
    {
        [Fact]
        public void AFileWithALineThatIsNotAGroupCode_OpensAsWhatCouldBeRecovered_UnderANameOfItsOwn()
        {
            using (var folder = new TestFolder())
            {
                string text = TestFiles.Room(out _, out _).ToText();
                int line = text.IndexOf("100\nAcDbLine\n", StringComparison.Ordinal);
                string path = folder.WriteText("room.dxf", text.Insert(line, "not a code\n"));
                var model = new MainViewModel();

                Assert.True(model.Open(path));

                Assert.Equal("room.dxf (recovered)", model.Document.Title);
                Assert.False(model.Document.HasFile);
                Assert.Equal(3, model.Document.EntityCount);
                Assert.Contains(model.History, l => l.StartsWith("Cannot open room.dxf as it is: This is not a DXF that can be read: line ", StringComparison.Ordinal));
                Assert.Contains("The file could not be read as it is; opening what could be recovered: 1 repair.", model.History);
                Assert.Contains(model.History, l => l.EndsWith(" is not a group code ('not a code'); it was left out.", StringComparison.Ordinal));
                Assert.Equal(text.Insert(line, "not a code\n"), System.IO.File.ReadAllText(path));
            }
        }
    }
}
