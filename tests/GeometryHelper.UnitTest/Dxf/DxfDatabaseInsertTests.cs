using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Another drawing inserted into this one, as AutoCAD's Database.Insert: its model space copied and moved into this one's,
    /// or made a block, and the records of its tables and its named blocks merged by name, this drawing's own kept; ezdxf 1.4.5
    /// read the probe's inserted drawings back, its recover and audit finding nothing.
    /// </summary>
    public class DxfDatabaseInsertTests
    {
        private static DxfDatabase NewDrawing() => DxfDatabase.Parse(new DxfWriter().ToString());

        /// <summary>A drawing of a line on steel (red), a circle on rebar, an INSERT of BOLT, and the unused layer SPARE.</summary>
        private static DxfDatabase Source()
        {
            DxfDatabase source = NewDrawing();
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "steel", ColorIndex = 1 });
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "rebar", ColorIndex = 3 });
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "SPARE", ColorIndex = 4 });
            var bolt = new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(1, 2, 0) };
            source.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfCircle(new GeoPoint3(1, 2, 0), new GeoVector3(0, 0, 1), 3) { Layer = "steel" });
            source.BlockTable.Add(new DxfBlockTableRecord { Name = "UNUSED" });
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "steel" });
            source.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(20, 0, 0), new GeoVector3(0, 0, 1), 5) { Layer = "rebar" });
            source.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(40, 0, 0), bolt));
            source.Insbase = new GeoPoint3(20, 0, 0);
            return source;
        }

        [Fact]
        public void ADrawingInserted_IsCopiedIntoModelSpaceMoved_ItsTablesMergedByName()
        {
            DxfDatabase source = Source();
            string sourceText = source.ToText();
            DxfDatabase db = NewDrawing();
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "rebar", ColorIndex = 5 });

            db.Insert(GeoTransform3.Translation(new GeoVector3(100, 0, 0)), source, true);

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            DxfLine line = read.ModelSpace.OfType<DxfLine>().Single();
            Assert.Equal(new GeoPoint3(100, 0, 0), line.StartPoint);
            Assert.Equal(new GeoPoint3(110, 0, 0), line.EndPoint);
            Assert.Equal("steel", line.Layer);
            Assert.Equal(new GeoPoint3(120, 0, 0), read.ModelSpace.OfType<DxfCircle>().Single().Center);
            DxfBlockReference insert = read.ModelSpace.OfType<DxfBlockReference>().Single();
            Assert.Equal(new GeoPoint3(140, 0, 0), insert.Position);
            Assert.Equal("BOLT", insert.Name);

            // Every record of the other drawing's tables comes where this one has none of its name; this one's own stays.
            Assert.Equal(1, read.LayerTable["steel"].ColorIndex);
            Assert.Equal(5, read.LayerTable["rebar"].ColorIndex);
            Assert.Equal(4, read.LayerTable["SPARE"].ColorIndex);
            Assert.True(read.BlockTable.Has("UNUSED"));
            Assert.Equal(new GeoPoint3(1, 2, 0), read.BlockTable["BOLT"].Origin);
            Assert.Single(read.BlockTable["BOLT"]);

            // The extents hold what was moved, not where it was first written: BOLT's circle of radius 3 about its origin, put at
            // (140, 0, 0), reaches x 143.
            Assert.Equal(new GeoPoint3(100, -5, 0), read.Extmin);
            Assert.Equal(new GeoPoint3(143, 5, 0), read.Extmax);
            Assert.Equal(sourceText, source.ToText());
        }

        [Fact]
        public void ADrawingInsertedAsItIs_KeepsItsCoordinates()
        {
            DxfDatabase db = NewDrawing();

            db.Insert(GeoTransform3.Identity, Source(), false);

            Assert.Equal(new GeoPoint3(10, 0, 0), db.ModelSpace.OfType<DxfLine>().Single().EndPoint);
            Assert.Equal(3, db.ModelSpace.Count);
        }

        [Fact]
        public void ADrawingMadeABlock_IsDrawnAboutItsBasePoint_AndModelSpaceIsLeftAsItIs()
        {
            DxfDatabase source = Source();
            DxfDatabase db = NewDrawing();
            int before = db.ModelSpace.Count;

            DxfBlockTableRecord plate = db.Insert("PLATE", source, true);

            Assert.Equal("PLATE", plate.Name);
            Assert.Equal(new GeoPoint3(20, 0, 0), plate.Origin);
            Assert.Equal(before, db.ModelSpace.Count);
            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "LINE", "CIRCLE", "INSERT" }, read.BlockTable["PLATE"].Select(e => e.Item.Type));
            Assert.Equal(new GeoPoint3(10, 0, 0), read.BlockTable["PLATE"].OfType<DxfLine>().Single().EndPoint);
            Assert.True(read.BlockTable.Has("BOLT"));
            Assert.Equal(3, read.LayerTable["rebar"].ColorIndex);

            // An INSERT of PLATE at the origin puts its base point (20, 0, 0) there: the line from (-20, 0, 0), the circle about
            // the origin, and BOLT's circle of radius 3 about (20, 0, 0).
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), plate));
            GeoAabb3 placed = db.ModelSpace.OfType<DxfBlockReference>().Single().GeometricExtents;
            Assert.True(placed.Min.IsEqualTo(new GeoPoint3(-20, -5, 0), Tolerance.Default), placed.ToString());
            Assert.True(placed.Max.IsEqualTo(new GeoPoint3(23, 5, 0), Tolerance.Default), placed.ToString());
        }

        [Fact]
        public void ADrawingThisOneCannotHold_IsRefusedWhole_AndNothingIsWritten()
        {
            DxfDatabase source = Source();
            source.ModelSpace.AppendEntity(new DxfSpline(3, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0), new GeoPoint3(2, 0, 0), new GeoPoint3(3, 1, 0) },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0 }, new double[0], 0.0, 0.0));
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            string text = r12.ToText();

            Assert.Throws<ArgumentException>(() => r12.Insert(GeoTransform3.Identity, source, true));
            Assert.Throws<ArgumentException>(() => r12.Insert("PLATE", source, true));
            Assert.Equal(text, r12.ToText());

            // A transformation an INSERT would be sheared by is refused before anything is written too.
            DxfDatabase db = NewDrawing();
            string empty = db.ToText();
            var shear = new GeoTransform3(new double[,] { { 1, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            Assert.Throws<ArgumentException>(() => db.Insert(shear, Source(), true));
            Assert.Equal(empty, db.ToText());
        }

        [Fact]
        public void ADrawingMadeABlockOfTheNameOfABlockItHas_IsRefused_AndNothingIsWritten()
        {
            // OUTER, of the other drawing, inserts INNER; ARROWX is a block of its own.
            DxfDatabase source = NewDrawing();
            var inner = new DxfBlockTableRecord { Name = "INNER" };
            source.BlockTable.Add(inner);
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            source.BlockTable.Add(new DxfBlockTableRecord { Name = "ARROWX" });
            var outer = new DxfBlockTableRecord { Name = "OUTER" };
            source.BlockTable.Add(outer);
            outer.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), inner));
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "steel" });
            source.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(50, 0, 0), outer) { Layer = "steel" });
            DxfDatabase db = NewDrawing();
            string text = db.ToText();

            Assert.Throws<ArgumentException>(() => db.Insert("OUTER", source, true));
            Assert.Throws<ArgumentException>(() => db.Insert("outer", source, true));

            Assert.Equal(text, db.ToText());
            Assert.Equal("PLATE", db.Insert("PLATE", source, true).Name);
        }

        [Fact]
        public void ADrawingWithABlockThatInsertsItself_IsRefusedWhole_AndNothingIsWritten()
        {
            // X holds an INSERT of X; Y and Z insert each other.
            const string Head = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n";
            const string Line = "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n0\n 31\n0\n";
            const string Tail = "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nINSERT\n  8\n0\n  2\n{0}\n 10\n0\n 20\n0\n 30\n0\n  0\nENDSEC\n  0\nEOF\n";
            string Block(string name, string inserted) => "  0\nBLOCK\n  8\n0\n  2\n" + name + "\n 70\n0\n 10\n0\n 20\n0\n 30\n0\n" + Line
                + "  0\nINSERT\n  8\n0\n  2\n" + inserted + "\n 10\n1\n 20\n0\n 30\n0\n 50\n30\n  0\nENDBLK\n  8\n0\n";

            foreach (string source in new[] { Head + Block("X", "X") + string.Format(Tail, "X"), Head + Block("Y", "Z") + Block("Z", "Y") + string.Format(Tail, "Y") })
            {
                foreach (DxfDatabase db in new[] { NewDrawing(), DxfDatabase.Parse(DxfFileEzdxfDrawings.R12) })
                {
                    string text = db.ToText();

                    Assert.Throws<ArgumentException>(() => db.Insert(GeoTransform3.Identity, DxfDatabase.Parse(source), true));
                    Assert.Throws<ArgumentException>(() => db.Insert(GeoTransform3.Translation(new GeoVector3(5, 0, 0)), DxfDatabase.Parse(source), true));
                    Assert.Throws<ArgumentException>(() => db.Insert("PLATE", DxfDatabase.Parse(source), true));
                    Assert.Equal(text, db.ToText());
                }
            }
        }

        [Fact]
        public void ADrawing_IsNotInsertedIntoItself_NorAsABlockOfANameTaken()
        {
            DxfDatabase db = Source();

            Assert.Throws<ArgumentException>(() => db.Insert(GeoTransform3.Identity, db, true));
            Assert.Throws<ArgumentException>(() => NewDrawing().Insert("*U1", db, true));
            Assert.Throws<ArgumentException>(() => db.Insert("bolt", Source(), true));
            Assert.Throws<ArgumentNullException>(() => db.Insert((string)null, Source(), true));
            Assert.Throws<ArgumentNullException>(() => db.Insert((GeoTransform3)null, Source(), true));
            Assert.Throws<ArgumentNullException>(() => db.Insert(GeoTransform3.Identity, null, true));
        }
    }
}
