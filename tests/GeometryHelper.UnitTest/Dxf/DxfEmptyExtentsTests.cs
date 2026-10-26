using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The extents of the header grown by what is appended or moved, each axis on its own: those the header leaves empty,
    /// the least corner above the most as the 1E+20 and -1E+20 ezdxf writes for a drawing it has not measured, become the box
    /// of what is written, as AutoCAD grows them from empty; an axis is never shrunk; an INSERT grows them by what it places.
    /// </summary>
    public class DxfEmptyExtentsTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9, 1E-9);

        [Fact]
        public void ALineAppended_ToADrawingWhoseExtentsAreEmpty_MakesThemItsBox_BeforeAndAfterSaving()
        {
            // The line from (5000, -4000, 0) to (5100, -3900, 10); then the line from (0, 0, 0) to (1, 1, 0) grows the box.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            Assert.Equal(1E20, db.Extmin.X);

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(5000, -4000, 0), new GeoPoint3(5100, -3900, 10)));

            Assert.True(db.Extmin.IsEqualTo(new GeoPoint3(5000, -4000, 0), Tol), db.Extmin.ToString());
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(5100, -3900, 10), Tol), db.Extmax.ToString());

            DxfDatabase again = Opened(Saved(db));
            Assert.Equal(Bits(db.Extmin), Bits(again.Extmin));
            Assert.Equal(Bits(db.Extmax), Bits(again.Extmax));

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)));
            Assert.True(db.Extmin.IsEqualTo(new GeoPoint3(0, -4000, 0), Tol), db.Extmin.ToString());
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(5100, 1, 10), Tol), db.Extmax.ToString());
        }

        [Fact]
        public void AMultileaderVertexMoved_InADrawingWhoseExtentsAreEmpty_MakesThemTheMultileadersBox()
        {
            // B4's first line of leader 0 starts at (780, -10, 0), moved to (5000, -4000, 0); its other line still starts at
            // (780, 10, 0), and leader 1 runs from (850, -10, 0): the lines of its leaders fill the box from (780, -4000, 0) to
            // (5000, 10, 0), its words not counted.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader multileader = ByHandle<DxfMLeader>(db, "B4");
            int line = multileader.GetLeaderLineIndexes(multileader.GetLeaderIndexes()[0])[0];

            multileader.SetFirstVertex(line, new GeoPoint3(5000, -4000, 0));

            GeoPoint3 least = db.Extmin;
            GeoPoint3 most = db.Extmax;
            Assert.True(least.IsEqualTo(new GeoPoint3(780, -4000, 0), Tol), least.ToString());
            Assert.True(most.IsEqualTo(new GeoPoint3(5000, 10, 0), Tol), most.ToString());

            DxfDatabase again = Opened(Saved(db));
            Assert.Equal(Bits(least), Bits(again.Extmin));
            Assert.Equal(Bits(most), Bits(again.Extmax));
        }

        [Fact]
        public void ALineAppendedToPaperSpace_MakesItsEmptyExtentsItsBox_AndLeavesThoseOfModelSpaceEmpty()
        {
            // The line from (10, 20, 0) to (400, 280, 0) in paper space.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            string[] model = db.GetHeader("$EXTMIN").Concat(db.GetHeader("$EXTMAX")).Select(g => g.ToString()).ToArray();

            db.PaperSpace.AppendEntity(new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(400, 280, 0)));

            Assert.True(db.Pextmin.IsEqualTo(new GeoPoint3(10, 20, 0), Tol), db.Pextmin.ToString());
            Assert.True(db.Pextmax.IsEqualTo(new GeoPoint3(400, 280, 0), Tol), db.Pextmax.ToString());
            Assert.Equal(model, db.GetHeader("$EXTMIN").Concat(db.GetHeader("$EXTMAX")).Select(g => g.ToString()));
        }

        [Fact]
        public void ExtentsEmptyAlongOneAxisOnly_AreGrownAxisByAxis()
        {
            // $EXTMIN (0, 0, 0) and $EXTMAX (100, -50, 0), empty along y alone. The circle of radius 5 about (200, 10) grows x
            // to 205 and makes y its own 5 to 15, the least of each axis kept where it is lower: (0, 0, 0) to (205, 15, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            db.Extmin = new GeoPoint3(0, 0, 0);
            db.Extmax = new GeoPoint3(100, -50, 0);

            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(200, 10, 0), GeoVector3.ZAxis, 5));

            Assert.True(db.Extmin.IsEqualTo(new GeoPoint3(0, 0, 0), Tol), db.Extmin.ToString());
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(205, 15, 0), Tol), db.Extmax.ToString());
        }

        [Fact]
        public void ExtentsAHairOffAlongOneAxis_AreNotShrunkAlongTheOthers()
        {
            // $EXTMIN (0, 0, 1E-9) and $EXTMAX (1000, 1000, 0), z the wrong way round by a hair: the line from (10, 10, 0) to
            // (20, 20, 0) leaves x and y from 0 to 1000 and makes z 0.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            db.Extmin = new GeoPoint3(0, 0, 1E-9);
            db.Extmax = new GeoPoint3(1000, 1000, 0);

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(20, 20, 0)));

            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(db.Extmin));
            Assert.Equal(Bits(new GeoPoint3(1000, 1000, 0)), Bits(db.Extmax));
        }

        [Fact]
        public void ExtentsSetEmptyAgain_AreGrownByWhatIsAppendedAfter_NotByWhatCameBefore()
        {
            // The line from (5000, -4000, 0) to (5100, -3900, 10) appended, the extents set empty, then the line from (0, 0, 0)
            // to (1, 1, 0) appended: its box alone.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(5000, -4000, 0), new GeoPoint3(5100, -3900, 10)));
            db.Extmin = new GeoPoint3(1E20, 1E20, 1E20);
            db.Extmax = new GeoPoint3(-1E20, -1E20, -1E20);

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)));

            Assert.True(db.Extmin.IsEqualTo(new GeoPoint3(0, 0, 0), Tol), db.Extmin.ToString());
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(1, 1, 0), Tol), db.Extmax.ToString());
        }

        [Fact]
        public void AnInsertAppendedAndMoved_GrowsTheExtentsByWhatItPlaces()
        {
            // Extents from (0, 0, 0) to (10, 10, 0); block W of the line from (0, 0, 0) to (10, 0, 0). Its INSERT at the origin
            // lies within them; moved to (5100, 0, 0) it grows them to x 5110.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            db.Extmin = new GeoPoint3(0, 0, 0);
            db.Extmax = new GeoPoint3(10, 10, 0);
            DxfBlockTableRecord w = Block(db, "W");
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), w);

            db.ModelSpace.AppendEntity(insert);
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(10, 10, 0), Tol), db.Extmax.ToString());

            insert.Position = new GeoPoint3(5100, 0, 0);
            Assert.True(db.Extmin.IsEqualTo(new GeoPoint3(0, 0, 0), Tol), db.Extmin.ToString());
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(5110, 10, 0), Tol), db.Extmax.ToString());

            DxfDatabase again = Opened(Saved(db));
            Assert.Equal(Bits(db.Extmax), Bits(again.Extmax));
        }

        [Fact]
        public void AnArrayInsertAppended_GrowsTheExtentsByEachOfItsCells()
        {
            // Block W of the line from (0, 0, 0) to (10, 0, 0), placed in 3 columns 100 apart and 2 rows 50 apart from
            // (-20, 0, 0): the cells reach from (-20, 0, 0) to (190, 50, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfBlockTableRecord w = Block(db, "W");

            db.ModelSpace.AppendEntity(new DxfMInsertBlock(new GeoPoint3(-20, 0, 0), w, 3, 2, 100.0, 50.0));

            Assert.True(db.Extmin.IsEqualTo(new GeoPoint3(-20, 0, 0), Tol), db.Extmin.ToString());
            Assert.True(db.Extmax.IsEqualTo(new GeoPoint3(190, 50, 0), Tol), db.Extmax.ToString());
        }

        [Fact]
        public void AnInsertCopiedByWblockCloneObjects_IntoADrawingWhoseExtentsAreEmpty_MakesThemWhatItPlaces()
        {
            // Block W of the line from (0, 0, 0) to (10, 0, 0), inserted at (5000, -4000, 0) and copied into ezdxf's drawing of
            // R2018, whose extents are empty: they become (5000, -4000, 0) to (5010, -4000, 0).
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var insert = new DxfBlockReference(new GeoPoint3(5000, -4000, 0), Block(source, "W"));
            source.ModelSpace.AppendEntity(insert);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            source.WblockCloneObjects(new DxfEntity[] { insert }, target.ModelSpace);

            Assert.True(target.Extmin.IsEqualTo(new GeoPoint3(5000, -4000, 0), Tol), target.Extmin.ToString());
            Assert.True(target.Extmax.IsEqualTo(new GeoPoint3(5010, -4000, 0), Tol), target.Extmax.ToString());
        }

        /// <summary>A block of a drawing holding the line from (0, 0, 0) to (10, 0, 0).</summary>
        private static DxfBlockTableRecord Block(DxfDatabase db, string name)
        {
            var block = new DxfBlockTableRecord { Name = name };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            return block;
        }
    }
}
