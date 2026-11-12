using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
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

        /// <summary>
        /// With DYN on, AutoCAD 2021's angle field shows the angle override locked: &lt;30 at LINE's next point shows 30 with the
        /// cursor straight up, counted as the drawing counts angles, and a length typed then runs along it.
        /// </summary>
        [Fact]
        public void AnAngleOverrideTyped_ShowsInTheAngleFieldLocked_UntilThePointIsGiven()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                model.Execute("LINE");
                model.Execute("1000,1000");
                model.Session.Cursor = new GeoPoint2(1000, 1500);
                model.SetCursor(1000, 1500);
                Assert.True(model.IsDynamicInputPolar);
                Assert.Equal("90.0", model.DynamicInputSecond);
                Assert.False(model.IsDynamicInputSecondLocked);

                model.Execute("<30");

                Assert.Equal("30.0", model.DynamicInputSecond);
                Assert.True(model.IsDynamicInputSecondLocked);
                Assert.False(model.IsDynamicInputFirstLocked);

                model.CommandText = "100";
                model.SubmitCommand();

                DxfLine line = model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfLine>().Last();
                Assert.Equal(1000.0 + 100.0 * Math.Cos(Math.PI / 6.0), line.EndPoint.X, 9);
                Assert.Equal(1050.0, line.EndPoint.Y, 9);
                Assert.Null(model.Session.AngleOverride);
                Assert.False(model.IsDynamicInputSecondLocked);
                model.Escape();
            }
        }
    }
}
