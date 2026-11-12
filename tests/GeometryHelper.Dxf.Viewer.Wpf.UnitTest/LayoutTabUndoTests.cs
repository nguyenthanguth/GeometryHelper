using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A tab clicked is an undo step: U and REDO show the space before and after it, and the tabs follow.</summary>
    public class LayoutTabUndoTests
    {
        [Fact]
        public void ATabClicked_IsOneUndoStep_TheTabsAndTheSpaceFollowUndoAndRedo()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                CadSpace layout = model.Document.Spaces.Single(s => s.Name == "Layout1");

                model.SelectSpaceCommand.Execute(model.SpaceTabs[1]);
                Assert.Same(layout, model.Space);
                Assert.True(model.SpaceTabs[1].IsActive);
                Assert.Equal("LAYOUT", model.Document.NextUndo.Name);

                Assert.True(model.Undo());
                Assert.Same(model.Document.ModelSpace, model.Space);
                Assert.True(model.SpaceTabs[0].IsActive);
                Assert.False(model.SpaceTabs[1].IsActive);
                Assert.Equal("MODEL", model.SpaceLabel);

                Assert.True(model.Redo());
                Assert.Same(layout, model.Space);
                Assert.True(model.SpaceTabs[1].IsActive);
                Assert.Equal("PAPER", model.SpaceLabel);
            }
        }
    }
}
