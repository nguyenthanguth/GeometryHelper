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
    /// Dynamic blocks, the fifth round of follow-ups (AutoCAD 2021, grips through <c>MoveGripPointsAt</c>): the grip of a point
    /// parameter, the end grip of a rotation parameter and the two corners of an XY parameter other than its end write a
    /// <c>GRIPLOC</c> of their node in the history of grip edits, how far the grip went in the block's coordinates, as the
    /// grips of a linear, an XY end and a polar parameter do; AutoCAD 2021 gave (20, 10, 0), (-60, 60, 0) and (10, 5, 0).
    /// </summary>
    public class DxfDynamicBlockFollowUp5Tests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A pointer at (1 000, 500) of two lines held by a polar parameter from (0, 0) to (100, 0), which starts a history; a
        /// point parameter Anchor at (0, 200) moving a dot; a rotation parameter Angle about (0, 300), 60 long, turning a bar;
        /// and a plate of an XY parameter Wide and High from (0, 400) to (100, 450), its right and top edges held by a stretch
        /// of the end point.
        /// </summary>
        private static DxfDatabase Mixed()
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_MIXED", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine line = Line(block, 0, 0, 100, 0);
            DxfLine tip = Line(block, 100, 0, 90, 5);
            DxfBlockPolarParameter polar = block.AddPolarParameter(
                "Length",
                null,
                "Angle of the pointer",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null,
                null
            );
            block.AddPolarStretchAction(
                polar,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 10),
                new DxfEntity[] { line, tip },
                new DxfEntity[0]
            );

            var dot = new DxfCircle(new GeoPoint3(0, 200, 0), GeoVector3.ZAxis, 5);
            block.AppendEntity(dot);
            DxfBlockPointParameter spot = block.AddPointParameter("Anchor", null, new GeoPoint3(0, 200, 0));
            block.AddMoveAction(spot, new DxfEntity[] { dot });

            DxfLine bar = Line(block, 0, 300, 60, 300);
            DxfBlockRotationParameter turn = block.AddRotationParameter("Angle", null, new GeoPoint3(0, 300, 0), 60, 0);
            block.AddRotateAction(turn, new DxfEntity[] { bar });

            DxfLine right = Line(block, 100, 400, 100, 450);
            DxfLine top = Line(block, 0, 450, 100, 450);
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Wide",
                null,
                "High",
                null,
                new GeoPoint3(0, 400, 0),
                new GeoPoint3(100, 450, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 440), new GeoPoint2(110, 460), new DxfEntity[] { right, top });

            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 500, 0), block));
            return DxfDatabase.Parse(db.ToText());
        }

        private static DxfLine Line(DxfBlockTableRecord block, double x1, double y1, double x2, double y2)
        {
            var line = new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));
            block.AppendEntity(line);
            return line;
        }

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfDatabase db, string name) =>
            TheInsert(db).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name);

        private static double ValueOf(DxfDatabase db, string name) => Assert.IsType<double>(Property(db, name).Value);

        /// <summary>The ids of the grips of the first parameter of a type: its 91s, or its 93 for a parameter of one point.</summary>
        private static string[] Grips(DxfDatabase db, string type)
        {
            string[] lines = db.ToText().Replace("\r\n", "\n").Split('\n');
            var found = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != type)
                {
                    continue;
                }

                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    if (lines[j].Trim() == "91" || lines[j].Trim() == "93")
                    {
                        found.Add(lines[j + 1].Trim());
                    }
                }

                break;
            }

            return found.ToArray();
        }

        /// <summary>The history's records after its header, as node and name, and the numbers that follow.</summary>
        private static List<KeyValuePair<string, double[]>> Records(DxfDatabase db)
        {
            var representation = (DxfDBDictionary)TheInsert(db).ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");
            var records = new List<KeyValuePair<string, double[]>>();

            if (!cache.Contains("ACAD_ENHANCEDBLOCKHISTORY"))
            {
                return records;
            }

            List<DxfTypedValue> data = ((DxfXrecord)cache.GetAt("ACAD_ENHANCEDBLOCKHISTORY")).Data.ToList();

            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].TypeCode != 1071)
                {
                    continue;
                }

                var numbers = new List<double>();

                for (int j = i + 2; j < data.Count && data[j].TypeCode != 1071; j++)
                {
                    numbers.Add(double.Parse(data[j].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
                }

                records.Add(
                    new KeyValuePair<string, double[]>(
                        data[i].Value.Trim() + " " + data[i + 1].Value.Trim(),
                        numbers.ToArray()
                    )
                );
            }

            return records;
        }

        private static void AssertRecord(
            KeyValuePair<string, double[]> record,
            string node,
            string name,
            params double[] numbers
        )
        {
            Assert.Equal(node + " " + name, record.Key);
            Assert.Equal(numbers.Length, record.Value.Length);

            for (int i = 0; i < numbers.Length; i++)
            {
                Assert.Equal(numbers[i], record.Value[i], 9);
            }
        }

        private static bool Has(DxfBlockReference insert, double x1, double y1, double x2, double y2) =>
            insert
                .BlockTableRecord.OfType<DxfLine>()
                .Any(l =>
                    (
                        l.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact)
                        && l.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact)
                    )
                    || (
                        l.StartPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact)
                        && l.EndPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact)
                    )
                );

        [Fact]
        public void ThePointGripDraggedBy20And10_WritesAGripLocOfItsNode_AndMovesTheDot()
        {
            // AutoCAD 2021: the point parameter's grip by (20, 10) wrote 1071 (its node), GRIPLOC, (20, 10, 0).
            DxfDatabase db = Mixed();

            Property(db, "Anchor X").MoveEndPoint(new GeoPoint3(1020, 710, 0));

            Assert.Equal(20.0, ValueOf(db, "Anchor X"), 9);
            Assert.Equal(210.0, ValueOf(db, "Anchor Y"), 9);
            Assert.Contains(TheInsert(db).BlockTableRecord.OfType<DxfCircle>(), c =>
                c.Center.IsEqualTo(new GeoPoint3(20, 210, 0), Exact)
            );
            string[] grips = Grips(db, "BLOCKPOINTPARAMETER");
            AssertRecord(Records(db).Single(), grips.Last(), "GRIPLOC", 20, 10, 0);
        }

        [Fact]
        public void ThePointGripDraggedTwice_KeepsOneRecord_TheTwoMovesAdded()
        {
            DxfDatabase db = Mixed();

            Property(db, "Anchor Y").MoveEndPoint(new GeoPoint3(1020, 710, 0));
            Property(db, "Anchor Y").MoveEndPoint(new GeoPoint3(1015, 705, 0));

            string[] grips = Grips(db, "BLOCKPOINTPARAMETER");
            AssertRecord(Records(db).Single(), grips.Last(), "GRIPLOC", 15, 5, 0);
        }

        [Fact]
        public void TheRotationGripTurnedAQuarterTurn_WritesAGripLocOfItsEndGrip_AndTurnsTheBar()
        {
            // AutoCAD 2021: the rotation parameter's end grip taken from (60, 300) to (0, 360) wrote 1071 (the end grip's node),
            // GRIPLOC, (-60, 60, 0).
            DxfDatabase db = Mixed();

            Property(db, "Angle").MoveEndPoint(new GeoPoint3(1000, 860, 0));

            Assert.Equal(Math.PI / 2, ValueOf(db, "Angle"), 9);
            Assert.True(Has(TheInsert(db), 0, 300, 0, 360));
            string[] grips = Grips(db, "BLOCKROTATIONPARAMETER");
            AssertRecord(Records(db).Single(), grips[1], "GRIPLOC", -60, 60, 0);
        }

        [Fact]
        public void TheCornerOfBaseXAndEndY_DraggedBy10And5_WritesAGripLocOfItsNode_AndMovesTheBasePointAlongX()
        {
            // AutoCAD 2021: the corner of base x and end y by (10, 5) wrote its node's GRIPLOC (10, 5, 0) and no record of
            // High or of the base grip; High read 55 and the base point went by 10 along x, the end point staying.
            DxfDatabase db = Mixed();

            Property(db, "Wide").MoveCornerPoint(new GeoPoint3(1010, 955, 0), false);

            Assert.Equal(90.0, ValueOf(db, "Wide"), 9);
            Assert.Equal(55.0, ValueOf(db, "High"), 9);
            Assert.True(Has(TheInsert(db), 100, 400, 100, 455));
            Assert.True(Has(TheInsert(db), 0, 450, 100, 455));
            string[] grips = Grips(db, "BLOCKXYPARAMETER");
            AssertRecord(Records(db).Single(), grips[2], "GRIPLOC", 10, 5, 0);
        }

        [Fact]
        public void TheCornerOfEndXAndBaseY_DraggedBy10And5_WritesAGripLocOfItsNode_AndMovesTheBasePointAlongY()
        {
            DxfDatabase db = Mixed();

            Property(db, "Wide").MoveCornerPoint(new GeoPoint3(1110, 905, 0), true);

            Assert.Equal(110.0, ValueOf(db, "Wide"), 9);
            Assert.Equal(45.0, ValueOf(db, "High"), 9);
            Assert.True(Has(TheInsert(db), 100, 400, 110, 450));
            Assert.True(Has(TheInsert(db), 0, 450, 110, 450));
            string[] grips = Grips(db, "BLOCKXYPARAMETER");
            AssertRecord(Records(db).Single(), grips[3], "GRIPLOC", 10, 5, 0);
        }

        [Fact]
        public void ACornerDraggedBackOverItsBasePoint_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Mixed();
            string before = db.ToText();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Property(db, "Wide").MoveCornerPoint(new GeoPoint3(1010, 900, 0), false)
            );
            Assert.Throws<ArgumentException>(() =>
                Property(db, "Anchor X").MoveCornerPoint(new GeoPoint3(1010, 900, 0), false)
            );
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void TheEdgesOfAnInsertWhoseCornerWasDragged_AreTheSameAsThoseOfTheTwoValuesSetAndTheBaseGripMoved()
        {
            // The corner's drag takes the same values as setting High and moving the base point did before; only the record of
            // the history differs.
            DxfDatabase corner = Mixed();
            Property(corner, "Wide").MoveCornerPoint(new GeoPoint3(1010, 955, 0), false);

            DxfDatabase typed = Mixed();
            Property(typed, "High").Value = 55.0;
            Property(typed, "Wide").MoveBasePoint(new GeoVector3(10, 0, 0));

            Assert.Equal(ValueOf(typed, "Wide"), ValueOf(corner, "Wide"), 9);
            Assert.Equal(ValueOf(typed, "High"), ValueOf(corner, "High"), 9);
            Assert.Equal(2, Records(typed).Count);
            Assert.Single(Records(corner));
        }
    }
}
