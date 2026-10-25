using System;
using System.Linq;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Entities in no file, copied or made in code, held light: one file holds every copy of a drawing and every entity made in
    /// code, and keeps none of them, so that each holds its own records alone. The explode of the INSERT of 1 000 000 lines of
    /// rotnest100 held 440 MB, where a file for each part ran out of a heap of 4 GB.
    /// </summary>
    public class DxfDetachedCopyTests
    {
        [Fact]
        public void TheCopiesOfADrawing_AreHeldByOneFile_AndEntitiesMadeInCodeByAnother()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity line = ByHandle<DxfLine>(db, "3E").Clone();
            DxfEntity circle = ByHandle<DxfCircle>(db, "3F").GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 0, 0)));
            DxfEntity again = line.Clone();
            var made = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));
            var other = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 2);

            Assert.Same(line.Item.File, circle.Item.File);
            Assert.Same(line.Item.File, again.Item.File);
            Assert.Same(made.Item.File, other.Item.File);
            Assert.NotSame(line.Item.File, made.Item.File);
            Assert.Equal(db.File, line.Item.File.Origin);
            Assert.Null(line.Item.Home);
            Assert.Null(made.Item.Home);
        }

        [Fact]
        public void ACopyHeld_IsCollectedOnceNothingReachesIt_WhileTheOthersOfItsDrawingLive()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity kept = ByHandle<DxfLine>(db, "3E").Clone();
            WeakReference dropped = CopyOf(db);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(dropped.IsAlive);
            Assert.Equal("LINE", kept.Item.Type);
        }

        [Fact]
        public void CopiesAndEntitiesMadeInCode_ReadWriteAndAppendAsBefore()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine original = ByHandle<DxfLine>(db, "3E");
            var copy = (DxfLine)original.Clone();
            var made = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "steel", ColorIndex = 1 };

            copy.EndPoint = new GeoPoint3(1, 2, 3);
            Assert.Equal(Bits(original.StartPoint), Bits(copy.StartPoint));
            Assert.Equal(Bits(new GeoPoint3(1, 2, 3)), Bits(copy.EndPoint));
            Assert.Equal(Bits(new GeoPoint3(100.125, 50.5, 3.0)), Bits(original.EndPoint));
            Assert.Equal(original.Layer, copy.Layer);

            db.ModelSpace.AppendEntity(copy);
            db.ModelSpace.AppendEntity(made);

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(Bits(new GeoPoint3(1, 2, 3)), Bits(ByHandle<DxfLine>(again, copy.Handle).EndPoint));
            Assert.Equal(1, ByHandle<DxfLine>(again, made.Handle).ColorIndex);
            Assert.Equal(2, again.ModelSpace.Count - DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018).ModelSpace.Count);
        }

        [Fact]
        public void ACopyOfAnInsert_PlacesItsBlockAsTheDrawingHasItNow_AfterTheBlockIsEdited()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockReference insert = ByHandle<DxfBlockReference>(db, "65");
            int before = insert.GetGeometry().Count;

            db.BlockTable["BOLT"].AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(7, 7, 0)));
            db.BlockTable["BOLT"].OfType<DxfCircle>().Single().Radius = 0.75;
            DxfEntity copy = insert.Clone();

            Assert.Equal(before + 1, insert.GetGeometry().Count);
            Assert.Equal(string.Join(" ; ", insert.GetGeometry()), string.Join(" ; ", copy.GetGeometry()));
            Assert.Equal(insert.GeometricExtents, copy.GeometricExtents);
        }

        [Fact]
        public void ACopyOfAnInsert_KeepsToItsBlockRenamed_AndIsAppendedByTheBlocksNameNow()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockReference insert = ByHandle<DxfBlockReference>(db, "65");
            var copy = (DxfBlockReference)insert.Clone();
            int parts = copy.Explode().Count;
            string geometry = string.Join(" ; ", copy.GetGeometry());

            db.BlockTable["BOLT"].Name = "BOLT_M20";

            Assert.Equal("BOLT_M20", copy.Name);
            Assert.Same(db.BlockTable["BOLT_M20"], copy.BlockTableRecord);
            Assert.Equal(parts, copy.Explode().Count);
            Assert.Equal(geometry, string.Join(" ; ", copy.GetGeometry()));

            // Another drawing with no block of that name is given a copy of it under that name; the drawing it came from places
            // it by that name.
            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            other.ModelSpace.AppendEntity(copy.Clone());
            Assert.Equal(db.BlockTable["BOLT_M20"].Count, other.BlockTable["BOLT_M20"].Count);
            db.ModelSpace.AppendEntity(copy);
            Assert.Equal("BOLT_M20", ByHandle<DxfBlockReference>(DxfDatabase.Parse(db.ToText()), copy.Handle).Name);
        }

        [Fact]
        public void ACopyOfAnInsertWhoseBlockIsErased_PlacesNothing_AndIsRefusedWhereNoBlockHasItsName()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_PART" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            var insert = new DxfBlockReference(new GeoPoint3(5, 5, 0), block);
            db.ModelSpace.AppendEntity(insert);
            var copy = (DxfBlockReference)insert.Clone();
            Assert.Single(copy.GetGeometry());

            insert.Erase();
            block.Erase();

            Assert.Empty(copy.GetGeometry());
            Assert.Null(copy.BlockTableRecord);
            Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000).ModelSpace.AppendEntity(copy));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CopyOf(DxfDatabase db) => new WeakReference(ByHandle<DxfCircle>(db, "3F").Clone());
    }
}
