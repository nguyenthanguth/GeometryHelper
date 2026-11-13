using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Find and Replace's Zoom to objects shows the space the objects are in as a click on its tab does: one undo step that
    /// U takes back to the space shown before, where the tab shown is the tab clicked.
    /// </summary>
    public class FindZoomLayoutTests
    {
        [Fact]
        public void ZoomToObjectsOfALayout_ShowsItAsOneUndoStep_AndUGoesBackToModel()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var main = new MainViewModel();
                        main.Open(
                            folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out _, out string viewport))
                        );
                        CadSpace layout = main.Document.Spaces.Single(s => s.Name == "Layout1");
                        Assert.True(main.Document.CurrentSpace.IsModel);

                        main.ZoomToObjects(new[] { viewport });

                        Assert.Same(layout, main.Document.CurrentSpace);
                        Assert.Same(layout, main.SpaceTabs.Single(t => t.IsActive).Space);
                        Assert.Equal("LAYOUT", main.Document.NextUndo.Name);

                        main.Document.Undo();
                        Assert.True(main.Document.CurrentSpace.IsModel);
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
