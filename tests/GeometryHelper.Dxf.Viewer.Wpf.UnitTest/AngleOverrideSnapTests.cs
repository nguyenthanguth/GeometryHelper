using System;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// &lt;30 typed at a point prompt holds the window's cursor on 30 degrees from the base point until the point is given, as
    /// AutoCAD 2021's angle override holds it: the window hands the session's override to its snap.
    /// </summary>
    public class AngleOverrideSnapTests
    {
        [Fact]
        public void AnAngleOverrideTyped_HoldsTheSnapOnItUntilThePointIsGiven()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));

                model.Execute("LINE");
                model.Execute("0,0");
                Assert.Null(model.Snap.AngleOverride);
                model.Execute("<30");
                Assert.Equal(Math.PI / 6.0, model.Snap.AngleOverride.Value, 12);

                model.Execute("100,0");
                Assert.Null(model.Snap.AngleOverride);
                model.Execute("");
            }
        }
    }
}
