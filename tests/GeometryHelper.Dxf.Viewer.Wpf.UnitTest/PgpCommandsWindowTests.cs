using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The commands of acad.pgp that reach the window: COLOR shows the Select Color dialog, as AutoCAD's.</summary>
    public class PgpCommandsWindowTests
    {
        [Fact]
        public void ColorTyped_ShowsTheSelectColorDialog_AndSetsTheColourOfNewObjects()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                SelectColorViewModel shown = null;
                model.PickColor = dialog =>
                {
                    shown = dialog;
                    dialog.PickIndex(30);
                    return true;
                };

                model.Execute("COL");

                Assert.NotNull(shown);
                Assert.Equal(30, model.Document.Database.Cecolor.ColorIndex);
                Assert.False(model.Session.IsRunning);
            }
        }

        [Fact]
        public void TheCanvasMenusIsolateAndHideCommands_AreKnownCommands()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));

                foreach (string name in new[] { "ISOLATEOBJECTS", "HIDEOBJECTS", "UNISOLATEOBJECTS" })
                {
                    Assert.Equal(name, model.Session.Registry.Find(name)?.Name);
                }
            }
        }
    }
}
