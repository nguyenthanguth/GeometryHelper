using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Point Style dialog of PTYPE (DDPTYPE): twenty tiles drawn as the canvas draws points, the size relative to the
    /// screen or in drawing units, OK writing PDMODE and PDSIZE as one undo step, Cancel nothing; driven with no window.
    /// </summary>
    public class PointStyleDialogTests
    {
        private static CadDocument Points(int pdmode, double pdsize)
        {
            var database = new DxfDatabase("AC1027") { Pdmode = pdmode, Pdsize = pdsize };
            database.ModelSpace.AppendEntity(new DxfDBPoint(new GeoPoint3(10, 20, 0)));
            return CadDocument.From(database, "points.dxf");
        }

        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        [Fact]
        public void TheTwentyTilesAreDrawnAsTheCanvasDrawsPoints_AndATileClickedSetsPdmode()
        {
            var dialog = new PointStyleViewModel(Points(0, 0.0));

            Assert.Equal(
                new[] { 0, 1, 2, 3, 4, 32, 33, 34, 35, 36, 64, 65, 66, 67, 68, 96, 97, 98, 99, 100 },
                dialog.Tiles.Select(t => t.Mode).ToArray()
            );
            Assert.True(dialog.Tiles[0].IsSelected);
            Assert.Equal("PDMODE 34, plus, circle around", dialog.Tiles[7].Label);

            // The plus and the cross as the display builder draws them, 16 across about (16, 16), y down.
            Assert.Equal("M8,16 L24,16 M16,24 L16,8", dialog.Tiles[2].Symbol);
            Assert.Equal("M8,24 L24,8 M8,8 L24,24", dialog.Tiles[3].Symbol);
            Assert.Equal("M16,16 L16,8", dialog.Tiles[4].Symbol);
            Assert.Equal("M24,16 A8,8 0 1 0 8,16 A8,8 0 1 0 24,16", dialog.Tiles[5].Symbol);
            Assert.Null(dialog.Tiles[1].Symbol);
            Assert.True(dialog.Tiles[0].HasDot);
            Assert.False(dialog.Tiles[1].HasDot);
            Assert.True(dialog.Tiles[15].HasDot);
            // 97: no shape, the circle and the four sides of the square, each a figure of its own.
            Assert.Equal(6, dialog.Tiles[16].Symbol.Split('M').Length);

            dialog.Tiles[7].IsSelected = true;

            Assert.Equal(34, dialog.Mode);
            Assert.True(dialog.Tiles[7].IsSelected);
            Assert.False(dialog.Tiles[0].IsSelected);
            Assert.Equal(1, dialog.Tiles.Count(t => t.IsSelected));
            Assert.Equal("PDMODE 34 · PDSIZE 0.0000", dialog.Summary);
        }

        [Fact]
        public void TheSizeRelativeToTheScreenIsWrittenBelowNought_AndInAbsoluteUnitsAboveIt()
        {
            var dialog = new PointStyleViewModel(Points(0, 0.0));

            Assert.True(dialog.IsRelative);
            Assert.Equal("5.0000", dialog.PointSize);
            Assert.Equal("% of the screen height", dialog.UnitText);
            Assert.Equal(0.0, dialog.Pdsize);

            dialog.PointSize = "10";
            Assert.Equal(-10.0, dialog.Pdsize);

            dialog.IsAbsolute = true;
            Assert.False(dialog.IsRelative);
            Assert.Equal("drawing units", dialog.UnitText);
            Assert.Equal(10.0, dialog.Pdsize);
            Assert.Equal("PDMODE 0 · PDSIZE 10.0000", dialog.Summary);

            dialog.PointSize = "0";
            Assert.False(dialog.IsSizeValid);
            Assert.True(dialog.Apply().Refused);
            dialog.PointSize = "ten";
            Assert.False(dialog.IsSizeValid);

            var absolute = new PointStyleViewModel(Points(3, 2.5));
            Assert.True(absolute.IsAbsolute);
            Assert.Equal("2.5000", absolute.PointSize);
            Assert.True(absolute.Tiles[3].IsSelected);

            var share = new PointStyleViewModel(Points(3, -12.0));
            Assert.True(share.IsRelative);
            Assert.Equal("12.0000", share.PointSize);
            Assert.Equal(-12.0, share.Pdsize);
        }

        [Fact]
        public void OkWritesPdmodeAndPdsizeAsOneUndoStep_AndThePointsAreDrawnAgain()
        {
            CadDocument document = Points(0, 0.0);
            Assert.Single(document.GetDisplay(document.ModelSpace).Items);
            var dialog = new PointStyleViewModel(document);

            dialog.Tiles.Single(t => t.Mode == 3).IsSelected = true;
            dialog.IsAbsolute = true;
            dialog.PointSize = "4";
            Assert.True(dialog.Apply().Succeeded);

            Assert.Equal(3, document.Database.Pdmode);
            Assert.Equal(4.0, document.Database.Pdsize);
            Assert.Equal("PTYPE", document.NextUndo.Name);
            Assert.Equal(2, document.GetDisplay(document.ModelSpace).Items.Count());

            Assert.True(document.Undo());
            Assert.Equal(0, document.Database.Pdmode);
            Assert.Equal(0.0, document.Database.Pdsize);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void PtypeShowsTheDialogThroughItsRequest_OkWritesBothAsOneStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                int shown = 0;
                model.ShowPointStyle = dialog =>
                {
                    shown++;
                    dialog.Tiles.Single(t => t.Mode == 35).IsSelected = true;
                    dialog.IsAbsolute = true;
                    dialog.PointSize = "2.5";
                    return true;
                };
                int revision = model.Revision;

                model.Execute("PTYPE");

                Assert.Equal(1, shown);
                Assert.Equal(35, model.Document.Database.Pdmode);
                Assert.Equal(2.5, model.Document.Database.Pdsize);
                Assert.Equal("PTYPE", model.Document.NextUndo.Name);
                Assert.True(model.Revision > revision);
                Assert.DoesNotContain(model.History, h =>
                    h.IndexOf("Enter new value for PDMODE", StringComparison.Ordinal) >= 0
                );

                model.Execute("DDPTYPE");
                Assert.Equal(2, shown);

                Assert.True(model.Document.Undo());
                Assert.Equal(0, model.Document.Database.Pdmode);
                Assert.Equal(0.0, model.Document.Database.Pdsize);
            }
        }

        [Fact]
        public void CancelOfPointStyle_WritesNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                bool undoBefore = model.Document.CanUndo;
                bool shown = false;
                model.ShowPointStyle = dialog =>
                {
                    shown = true;
                    dialog.Tiles.Single(t => t.Mode == 99).IsSelected = true;
                    dialog.PointSize = "20";
                    return false;
                };

                model.Execute("PTYPE");

                Assert.True(shown);

                Assert.Equal(0, model.Document.Database.Pdmode);
                Assert.Equal(0.0, model.Document.Database.Pdsize);
                Assert.Equal(undoBefore, model.Document.CanUndo);
                Assert.False(model.Document.IsModified);
            }
        }
    }
}
