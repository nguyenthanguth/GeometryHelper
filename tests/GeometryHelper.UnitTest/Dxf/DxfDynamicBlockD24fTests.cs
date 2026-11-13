using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A base point a grip moved (D24f): AutoCAD's XY sample keeps an INSERT whose XY parameter's base point a grip moved from
    /// (0, 5) to (0, 104.30), its end point at (100.59, 0), and drew its array of rows from there, at y 99.30 down to 19.30;
    /// the library sets such an INSERT from the base point it keeps, as AutoCAD 2021 sets one read back from its file (plugin
    /// GhDy2a).
    /// </summary>
    public class DxfDynamicBlockD24fTests
    {
        private const double Close = 1E-9;

        // The distances of the INSERT 2AE of AutoCAD's XY sample, as AutoCAD saved them.
        private const double SampleX = 100.5926984975917;
        private const double SampleY = 104.303888075734;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>A new drawing of R2010 with an empty block of the name and an INSERT of it at the origin.</summary>
        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1024");
            block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return db;
        }

        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        /// <summary>The block of AutoCAD's XY sample: a circle at the origin, the XY parameter from (0, 5) to (5, 0), an array of columns of 25 and rows of 20.</summary>
        private static DxfDatabase Grid(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_GRID", out block);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockXYParameter xy = block.AddXYParameter(
                "X Distance1",
                null,
                "Y Distance1",
                null,
                new GeoPoint3(0, 5, 0),
                new GeoPoint3(5, 0, 0)
            );
            block.AddArrayAction(xy, new[] { dot }, 25, 20);
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) =>
            Assert.IsType<double>(Property(insert, name).Value);

        private static List<T> Shown<T>(DxfBlockReference insert) => insert.BlockTableRecord.OfType<T>().ToList();

        /// <summary>The id of the first node of the type in the graph of a block.</summary>
        private static string IdOf(DxfBlockTableRecord block, string type)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            DxfItem node = graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .First(n => n.Type == type);
            return node.Groups.First(g => g.TypeCode == 90).Value.Trim();
        }

        /// <summary>
        /// Writes the base point and the end point an INSERT keeps of a parameter of two points in its cache, as a grip edit in
        /// AutoCAD leaves them; the other groups of the record kept.
        /// </summary>
        private static void Grip(DxfBlockReference insert, string id, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)
                ((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            DxfItem record = data.GetAt(id).Item;
            DxfFile file = record.File;

            // The record's groups as the file holds them: its head (1071) is of codes Data does not take.
            var groups = file.TailOf(record, 0)
                .Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value))
                .ToList();
            int at = groups.FindIndex(g => g.Key == 10);
            var points = new[] { basePoint, endPoint };

            for (int i = 0; i < 2; i++)
            {
                groups[at + 3 * i] = new KeyValuePair<int, string>(
                    10,
                    points[i].X.ToString("R", CultureInfo.InvariantCulture)
                );
                groups[at + 3 * i + 1] = new KeyValuePair<int, string>(
                    20,
                    points[i].Y.ToString("R", CultureInfo.InvariantCulture)
                );
                groups[at + 3 * i + 2] = new KeyValuePair<int, string>(
                    30,
                    points[i].Z.ToString("R", CultureInfo.InvariantCulture)
                );
            }

            file.ReplaceTail(record, 0, groups);
        }

        [Fact]
        public void AnInsertWhoseXYBasePointAGripMoved_IsSetFromWhereItKeepsTheBasePoint_AsAutoCadSetsIt()
        {
            // AutoCAD's XY sample (ACadSharp's, saved by AutoCAD 2021) keeps its INSERT 2AE with the XY parameter's base point
            // at (0, 104.30) and its end point at (100.59, 0), the block's own being (0, 5) and (5, 0): a grip moved both. The
            // library works it out from the base point the INSERT keeps, as AutoCAD 2021 did an INSERT read back from its file
            // (plugin GhDy2a: a base point moved by (10, 20) and X Distance1 set to 105 gave four columns of 25 and five rows
            // of 20 from the block's own circle, a grip's move of the first copy not being kept): X Distance1 set to 60 gives
            // two columns, the Y distance staying.
            DxfDatabase db = Grid(out DxfBlockTableRecord block);
            Property(TheInsert(db), "X Distance1").Value = SampleX;
            Property(TheInsert(db), "Y Distance1").Value = SampleY;
            Grip(
                TheInsert(db),
                IdOf(block, "BLOCKXYPARAMETER"),
                new GeoPoint3(0, SampleY, 0),
                new GeoPoint3(SampleX, 0, 0)
            );
            DxfDatabase reopened = Reopened(db);
            DxfBlockReference insert = TheInsert(reopened);

            // Read as AutoCAD read it (plugin v16): the distances between the two points it saved.
            Assert.Equal(SampleX, ValueOf(insert, "X Distance1"), Close);
            Assert.Equal(SampleY, ValueOf(insert, "Y Distance1"), Close);

            Property(insert, "X Distance1").Value = 60.0;

            DxfBlockReference back = TheInsert(Reopened(reopened));
            Assert.Equal(60.0, ValueOf(back, "X Distance1"), Close);
            Assert.Equal(SampleY, ValueOf(back, "Y Distance1"), Close);
            List<GeoPoint3> dots = Shown<DxfCircle>(back).Select(c => c.Center).ToList();
            Assert.Equal(10, dots.Count);
            Assert.Contains(dots, d => d.IsEqualTo(new GeoPoint3(0, 0, 0), Exact));
            Assert.Contains(dots, d => d.IsEqualTo(new GeoPoint3(25, -80, 0), Exact));
        }

        [Fact]
        public void AnInsertWhoseLinearBasePointAGripMoved_IsSetFromWhereItKeepsTheBasePoint()
        {
            // A linear parameter's base grip moves its base point as an XY parameter's does: Width from (50, 0) to (200, 0) reads
            // 150, and set to 250 it takes the end point to (300, 0) and the right edge, which the end point stretches, to x 300,
            // where the block's own base point at the origin would put it at 250 (AutoCAD 2021 set Width 200 with the end point
            // at (220, 0) from the base point at (20, 0) on an INSERT read back from its file, plugin GhDy2a).
            DxfDatabase db = Drawing("GH_PANEL", out DxfBlockTableRecord block);
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)));
            DxfLine bottom = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, bottom });
            Property(TheInsert(db), "Width").Value = 200.0;
            Grip(TheInsert(db), IdOf(block, "BLOCKLINEARPARAMETER"), new GeoPoint3(50, 0, 0), new GeoPoint3(200, 0, 0));
            DxfDatabase reopened = Reopened(db);
            DxfBlockReference insert = TheInsert(reopened);

            Assert.Equal(150.0, ValueOf(insert, "Width"), Close);

            Property(insert, "Width").Value = 250.0;

            DxfBlockReference back = TheInsert(Reopened(reopened));
            Assert.Equal(250.0, ValueOf(back, "Width"), Close);
            Assert.Contains(Shown<DxfLine>(back), l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(300, 0, 0), Exact)
                && l.EndPoint.IsEqualTo(new GeoPoint3(300, 50, 0), Exact)
            );
        }

        [Fact]
        public void AnInsertWhoseBasePointIsTheBlocksOwn_WithinThePointTolerance_IsSetAsBefore()
        {
            // The library keeps the block's own base point in each cache it writes, and AutoCAD kept it in every INSERT it set in
            // plugins v17 to v21b: such an INSERT is set again. A base point saved a ten-thousandth off the block's own, within
            // the point tolerance, is the block's own.
            DxfDatabase db = Grid(out DxfBlockTableRecord block);
            Property(TheInsert(db), "X Distance1").Value = SampleX;
            Property(TheInsert(db), "Y Distance1").Value = SampleY;
            Grip(
                TheInsert(db),
                IdOf(block, "BLOCKXYPARAMETER"),
                new GeoPoint3(1E-4, 5 - 1E-4, 0),
                new GeoPoint3(SampleX, 5 - SampleY, 0)
            );
            DxfDatabase reopened = Reopened(db);

            Property(TheInsert(reopened), "Y Distance1").Value = 45.0;

            DxfBlockReference back = TheInsert(Reopened(reopened));
            List<GeoPoint3> dots = Shown<DxfCircle>(back).Select(c => c.Center).ToList();
            Assert.Equal(8, dots.Count);
            Assert.Contains(dots, d => d.IsEqualTo(new GeoPoint3(75, -20, 0), Exact));
            Assert.Equal(45.0, ValueOf(back, "Y Distance1"), Close);
        }
    }
}
