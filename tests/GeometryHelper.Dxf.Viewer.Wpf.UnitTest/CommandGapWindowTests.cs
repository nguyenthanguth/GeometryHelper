using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The commands of the AutoCAD gap list as the window runs them: LAYOUT Rename and Set move the tabs, Ctrl+A selects all.</summary>
    public class CommandGapWindowTests
    {
        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        [Fact]
        public void LayoutRenameRenamesTheTab_AndLayoutSetMakesItTheActiveOne()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);

                foreach (string typed in new[] { "LAYOUT", "R", "Layout1", "Sheet A" })
                {
                    model.Execute(typed);
                }

                Assert.Equal(new[] { "Model", "Sheet A" }, model.SpaceTabs.Select(t => t.Name));
                Assert.True(model.SpaceTabs[0].IsActive);

                foreach (string typed in new[] { "LAYOUT", "S", "Sheet A" })
                {
                    model.Execute(typed);
                }

                Assert.Equal("Sheet A", model.Space.Name);
                Assert.True(model.SpaceTabs[1].IsActive);
                Assert.False(model.SpaceTabs[0].IsActive);
            }
        }

        [Fact]
        public void CtrlASelectsEveryObjectOfTheSpaceShown()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                int shown = model.Document.ModelSpace.Block.Count(e => !string.IsNullOrEmpty(e.Handle) && !model.Document.Database.LayerTable[e.Layer].IsFrozen);
                Assert.True(shown > 0);

                Assert.True(model.RunKey("Ctrl+A"));

                Assert.Equal(shown, model.Document.Selection.Handles.Count);
            }
        }
    }
}
