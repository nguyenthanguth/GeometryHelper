using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL and SNAPISOPAIR of the drawing's <c>*Active</c> viewport, read and applied to the
    /// snaps, set from the command line in AutoCAD 2021's words and written back into the drawing; ISOPLANE, and F5 and
    /// Ctrl+E turning to the next isometric plane.
    /// </summary>
    public class SnapBaseAngleAndIsoplaneTests
    {
        /// <summary>The room with a snap from (5, 7) turned by 30 degrees, a major line every 4 and an isometric snap on the right plane.</summary>
        private static DxfDatabase SetRoom()
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            active.SnapBase = new GeoPoint2(5, 7);
            active.SnapAngle = Math.PI / 6.0;
            active.GridMajor = 4;
            active.IsometricSnapEnabled = true;
            active.SnapPair = 2;
            active.SnapIncrements = new GeoVector2(5 * Math.Sqrt(3.0), 2.5);
            return db;
        }

        [Fact]
        public void ADrawingsSnapBaseAngleMajorAndIsoplane_AreAppliedToTheSnaps_AndADrawingWithoutThemHasTheDefaults()
        {
            using (var folder = new TestFolder())
            {
                string set = folder.Write("set.dxf", SetRoom());
                string plain = folder.Write("plain.dxf", TestFiles.Room(out _, out _));
                var model = new MainViewModel();

                model.Open(set);
                Assert.Equal(new GeoPoint2(5, 7), model.SnapBase);
                Assert.Equal(Math.PI / 6.0, model.SnapAngle, 12);
                Assert.Equal(4, model.GridMajor);
                Assert.Equal(CadIsoplane.Right, model.Isoplane);
                Assert.Equal(new GeoPoint2(5, 7), model.Snap.GridOrigin);
                Assert.Equal(Math.PI / 6.0, model.Snap.SnapAngle, 12);
                Assert.Equal(CadIsoplane.Right, model.Snap.Isoplane);

                model.Open(plain);
                Assert.Equal(GeoPoint2.Origin, model.Snap.GridOrigin);
                Assert.Equal(0.0, model.Snap.SnapAngle);
                Assert.Null(model.Snap.Isoplane);
                Assert.Equal(5, model.GridMajor);
                Assert.False(model.Document.IsModified);
            }
        }

        [Fact]
        public void SnapangTyped_IsShownInTheDrawingsAngleUnits_KeptWithinAWholeTurn_AndWrittenIntoTheDrawing()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));

                model.Execute("SNAPANG");
                Assert.Equal("Enter new value for SNAPANG <0.00>:", model.Prompt);
                model.Execute("400");
                Assert.Equal(40.0 * Math.PI / 180.0, model.Snap.SnapAngle, 12);
                model.Execute("SNAPANG");
                Assert.Equal("Enter new value for SNAPANG <40.00>:", model.Prompt);
                model.Execute("-30");
                Assert.Equal(330.0 * Math.PI / 180.0, model.SnapAngle, 12);
                Assert.Equal(330.0 * Math.PI / 180.0, model.Document.Database.ViewportTable["*Active"].SnapAngle, 12);

                model.Execute("SNAPBASE");
                Assert.Equal("Enter new value for SNAPBASE <0.0000,0.0000>:", model.Prompt);
                model.Execute("3,4");
                Assert.Equal(new GeoPoint2(3, 4), model.Snap.GridOrigin);
                Assert.Equal(new GeoPoint2(3, 4), model.Document.Database.ViewportTable["*Active"].SnapBase);
            }
        }

        [Fact]
        public void GridmajorSnapstylAndSnapisopair_RefuseWhatAutoCadRefuses_AndKeepWhatTheyTake()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));

                model.Execute("GRIDMAJOR");
                model.Execute("0");
                Assert.Equal("Requires an integer between 1 and 100.", model.History.Last());
                model.Execute("7");
                Assert.Equal(7, model.GridMajor);

                model.Execute("SNAPSTYL");
                model.Execute("2");
                Assert.Equal("Requires 0 or 1 only.", model.History.Last());
                model.Execute("1");
                Assert.Equal(CadIsoplane.Left, model.Snap.Isoplane);

                model.Execute("SNAPISOPAIR");
                model.Execute("3");
                Assert.Equal("Requires an integer between 0 and 2.", model.History.Last());
                model.Execute("1");
                Assert.Equal(CadIsoplane.Top, model.Snap.Isoplane);

                DxfViewportTableRecord active = model.Document.Database.ViewportTable["*Active"];
                Assert.Equal(7, active.GridMajor);
                Assert.True(active.IsometricSnapEnabled);
                Assert.Equal(1, active.SnapPair);

                model.Execute("SNAPSTYL");
                model.Execute("0");
                Assert.Null(model.Snap.Isoplane);
                Assert.Null(model.Isoplane);
            }
        }

        [Fact]
        public void Isoplane_SaysThePlane_TakesTheNextOnEnter_AndAPlaneNamed()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));

                model.Execute("ISOPLANE");
                Assert.Equal("Current isoplane: Left", model.History.Last());
                Assert.Equal("Enter isometric plane setting [Left/Top/Right] <Top>:", model.Prompt);
                model.Execute("");
                Assert.Equal("Current isoplane: Top", model.History.Last());

                model.Execute("ISOPLANE");
                model.Execute("L");
                Assert.Equal("Current isoplane: Left", model.History.Last());
                Assert.Equal(0, model.Document.Database.ViewportTable["*Active"].SnapPair);
            }
        }

        [Fact]
        public void F5AndCtrlE_TurnToTheNextIsoplane_WhileACommandRuns()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                model.Execute("SNAPSTYL");
                model.Execute("1");
                model.Execute("LINE");

                Assert.True(model.RunKey("F5"));
                Assert.Equal("<Isoplane Top>", model.History.Last());
                Assert.Equal(CadIsoplane.Top, model.Snap.Isoplane);
                Assert.True(model.RunKey("Ctrl+E"));
                Assert.Equal("<Isoplane Right>", model.History.Last());
                Assert.True(model.RunKey("F5"));
                Assert.Equal(CadIsoplane.Left, model.Isoplane);
                Assert.True(model.IsCommandRunning);
            }
        }

        [Fact]
        public void SnapSpacingTyped_WithAnIsometricSnap_IsTheVerticalSpacing_AsAutoCadKeepsIt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("set.dxf", SetRoom()));

                model.Execute("SNAP");
                Assert.Equal("Specify snap spacing or [ON/OFF] <5.0000>:", model.Prompt);
                model.Execute("3");

                Assert.Equal(3 * Math.Sqrt(3.0), model.Snap.GridX, 12);
                Assert.Equal(1.5, model.Snap.GridY, 12);
                GeoVector2 kept = model.Document.Database.ViewportTable["*Active"].SnapIncrements;
                Assert.Equal(1.5, kept.Y, 12);
            }
        }
    }
}
