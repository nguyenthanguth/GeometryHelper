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
    /// Dynamic blocks, the fourth round of follow-ups (plugin GhDb4a, AutoCAD 2021 side databases, grips through
    /// <c>MoveGripPointsAt</c>): the grip edits of a polar parameter worked out one after the other, the Origin of a parameter
    /// set, a stretch or an array that follows a base point kept there at the next value set, and the history of grip edits
    /// (what starts it, a drag that goes nowhere, a distance kept in the block's units). Each number below is one AutoCAD 2021
    /// gave on a block built as these are.
    /// </summary>
    public class DxfDynamicBlockFollowUp4Tests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        // What AutoCAD printed, to six places.
        private static readonly Tolerance Near = new Tolerance(2E-6, 2E-6);

        // ---- building ----------------------------------------------------------------------------------------------------

        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1032");
            block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            return db;
        }

        private static DxfLine Line(DxfBlockTableRecord block, double x1, double y1, double x2, double y2)
        {
            var line = new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));
            block.AppendEntity(line);
            return line;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static void Put(DxfDatabase db, DxfBlockTableRecord block) =>
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 500, 0), block));

        /// <summary>The INSERT turned a quarter turn and scaled by 2.</summary>
        private static DxfDatabase Turned(DxfDatabase db)
        {
            DxfBlockReference insert = TheInsert(db);
            insert.ScaleFactors = new GeoVector3(2, 2, 2);
            insert.Rotation = Math.PI / 2;
            return Reopened(db);
        }

        /// <summary>Makes the nth action of a kind follow its parameter's base point (BaseXDelta, BaseYDelta), by the text.</summary>
        private static DxfDatabase FollowingTheBase(DxfDatabase db, string type, int nth)
        {
            string[] lines = db.ToText().Replace("\r\n", "\n").Split('\n');
            int seen = -1;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != type || ++seen != nth)
                {
                    continue;
                }

                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    lines[j + 1] =
                        lines[j + 1].Trim() == "EndXDelta" ? "BaseXDelta"
                        : lines[j + 1].Trim() == "EndYDelta" ? "BaseYDelta"
                        : lines[j + 1];
                }
            }

            return DxfDatabase.Parse(string.Join("\n", lines));
        }

        /// <summary>A pointer of two lines at (1 000, 500): a polar parameter from (0, 0) to (100, 0), its end held by a polar stretch; and, where asked, a linear parameter Width above it.</summary>
        private static DxfDatabase Pointer(
            DxfBlockParameterValueSet distances = null,
            DxfBlockParameterValueSet angles = null,
            bool width = false
        )
        {
            DxfDatabase db = Drawing("GH_POINTER", out DxfBlockTableRecord block);
            DxfLine line = Line(block, 0, 0, 100, 0);
            DxfLine tip = Line(block, 100, 0, 90, 5);
            DxfBlockPolarParameter polar = block.AddPolarParameter(
                "Length",
                null,
                "Angle",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                distances,
                angles
            );
            block.AddPolarStretchAction(
                polar,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 10),
                new DxfEntity[] { line, tip },
                new DxfEntity[0]
            );

            if (width)
            {
                DxfLine box = Line(block, 0, 100, 100, 100);
                DxfLine edge = Line(block, 100, 100, 100, 150);
                DxfBlockLinearParameter linear = block.AddLinearParameter(
                    "Width",
                    null,
                    new GeoPoint3(0, 100, 0),
                    new GeoPoint3(100, 100, 0),
                    20,
                    null
                );
                block.AddStretchAction(linear, new GeoPoint2(90, 90), new GeoPoint2(110, 160), new[] { box, edge });
            }

            Put(db, block);
            return Reopened(db);
        }

        /// <summary>A plate 100 by 50 at (1 000, 500) of an XY parameter from (0, 0) to (100, 50), its bottom left corner held by a stretch of the base point.</summary>
        private static DxfDatabase Plate(bool attributes = false)
        {
            DxfDatabase db = Drawing("GH_PLATE", out DxfBlockTableRecord block);
            DxfLine right = Line(block, 100, 0, 100, 50);
            DxfLine top = Line(block, 0, 50, 100, 50);
            DxfLine bottom = Line(block, 0, 0, 100, 0);
            DxfLine left = Line(block, 0, 0, 0, 50);
            var held = new List<DxfEntity> { right, top };
            var heldByBase = new List<DxfEntity> { left, bottom };
            var definitions = new List<DxfAttributeDefinition>();

            if (attributes)
            {
                var note = new DxfAttributeDefinition(new GeoPoint3(98, 48, 0), "-", "NOTE", "Note", null)
                {
                    Height = 2.5,
                    LockPositionInBlock = true,
                };
                var lower = new DxfAttributeDefinition(new GeoPoint3(2, 2, 0), "-", "LOWER", "Lower", null)
                {
                    Height = 2.5,
                    LockPositionInBlock = true,
                };
                block.AppendEntity(note);
                block.AppendEntity(lower);
                held.Add(note);
                heldByBase.Add(lower);
                definitions.Add(note);
                definitions.Add(lower);
            }

            DxfBlockXYParameter xy = block.AddXYParameter(
                "Width",
                null,
                "Height",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 50, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), held);
            block.AddStretchAction(xy, new GeoPoint2(-10, -10), new GeoPoint2(10, 10), heldByBase);
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);

            foreach (DxfAttributeDefinition definition in definitions)
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            return Reopened(FollowingTheBase(db, "BLOCKSTRETCHACTION", 1));
        }

        /// <summary>A panel 100 by 50 at (1 000, 500) of a linear parameter Width from (0, 0) to (100, 0), its left edge held by a stretch of the base point, and, where asked, a circle whose array of columns of 25 follows it.</summary>
        private static DxfDatabase Panel(bool array)
        {
            DxfDatabase db = Drawing("GH_PANEL", out DxfBlockTableRecord block);
            DxfLine right = Line(block, 100, 0, 100, 50);
            DxfLine top = Line(block, 0, 50, 100, 50);
            DxfLine bottom = Line(block, 0, 0, 100, 0);
            DxfLine left = Line(block, 0, 0, 0, 50);
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, top, bottom });
            block.AddStretchAction(width, new GeoPoint2(-10, -10), new GeoPoint2(10, 60), new[] { left, top, bottom });

            if (array)
            {
                var dot = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5);
                block.AppendEntity(dot);
                block.AddArrayAction(width, new[] { dot }, 25);
            }

            Put(db, block);
            return Reopened(FollowingTheBase(db, "BLOCKSTRETCHACTION", 1));
        }

        /// <summary>A pointer as above with, above it, a plate of an XY parameter Wide and High from (0, 100) to (100, 150), its right and top edges held by a stretch of the end point.</summary>
        private static DxfDatabase PointerWithAPlate()
        {
            DxfDatabase db = Drawing("GH_POINTERP", out DxfBlockTableRecord block);
            DxfLine line = Line(block, 0, 0, 100, 0);
            DxfLine tip = Line(block, 100, 0, 90, 5);
            DxfBlockPolarParameter polar = block.AddPolarParameter(
                "Length",
                null,
                "Angle",
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
            DxfLine right = Line(block, 100, 100, 100, 150);
            DxfLine top = Line(block, 0, 150, 100, 150);
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Wide",
                null,
                "High",
                null,
                new GeoPoint3(0, 100, 0),
                new GeoPoint3(100, 150, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 140), new GeoPoint2(110, 160), new DxfEntity[] { right, top });
            Put(db, block);
            return Reopened(db);
        }

        /// <summary>A bar 100 long and a linear parameter Width whose scale action scales a second bar.</summary>
        private static DxfDatabase Scaled()
        {
            DxfDatabase db = Drawing("GH_SCALED", out DxfBlockTableRecord block);
            Line(block, 0, 0, 100, 0);
            DxfLine second = Line(block, 0, 20, 100, 20);
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null
            );
            block.AddScaleAction(width, new DxfEntity[] { second });
            Put(db, block);
            return Reopened(db);
        }

        // ---- reading back ------------------------------------------------------------------------------------------------

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfDatabase db, string name) =>
            TheInsert(db).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name);

        private static double ValueOf(DxfDatabase db, string name) => Assert.IsType<double>(Property(db, name).Value);

        private static DxfDynamicBlockReferenceProperty OriginProperty(DxfDatabase db, int nth) =>
            TheInsert(db).DynamicBlockReferencePropertyCollection.Where(p => p.PropertyName == "Origin").ElementAt(nth);

        private static GeoPoint3 Origin(DxfDatabase db, int nth) =>
            Assert.IsType<GeoPoint3>(OriginProperty(db, nth).Value);

        private static void AssertPoint(GeoPoint3 point, double x, double y, double z) =>
            Assert.True(
                point.IsEqualTo(new GeoPoint3(x, y, z), Exact),
                point + " is not (" + x + ", " + y + ", " + z + ")"
            );

        private static void AssertHas(
            DxfBlockReference insert,
            double x1,
            double y1,
            double x2,
            double y2,
            Tolerance tolerance
        ) =>
            Assert.True(
                Has(insert, x1, y1, x2, y2, tolerance),
                "no line from ("
                    + x1
                    + ", "
                    + y1
                    + ") to ("
                    + x2
                    + ", "
                    + y2
                    + ") among "
                    + string.Join(
                        " ",
                        insert
                            .BlockTableRecord.OfType<DxfLine>()
                            .Select(l => "[" + l.StartPoint + " " + l.EndPoint + "]")
                    )
            );

        private static bool Has(
            DxfBlockReference insert,
            double x1,
            double y1,
            double x2,
            double y2,
            Tolerance tolerance
        ) =>
            insert
                .BlockTableRecord.OfType<DxfLine>()
                .Any(l =>
                    l.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), tolerance)
                    && l.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), tolerance)
                );

        private static List<DxfCircle> Circles(DxfBlockReference insert) =>
            insert.BlockTableRecord.OfType<DxfCircle>().ToList();

        private static DxfXrecord HistoryRecord(DxfDatabase db)
        {
            var representation = (DxfDBDictionary)TheInsert(db).ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");
            return cache.Contains("ACAD_ENHANCEDBLOCKHISTORY")
                ? (DxfXrecord)cache.GetAt("ACAD_ENHANCEDBLOCKHISTORY")
                : null;
        }

        private static List<DxfTypedValue> History(DxfDatabase db) => HistoryRecord(db)?.Data.ToList();

        private static double Real(DxfTypedValue group) =>
            double.Parse(group.Value, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>The ids of the nodes of the first parameter of a type, as its grips (the first two 91s) and its own (90).</summary>
        private static string[] Nodes(DxfDatabase db, string type)
        {
            string[] lines = db.ToText().Replace("\r\n", "\n").Split('\n');
            var found = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != type)
                {
                    continue;
                }

                string id = null;
                var grips = new List<string>();

                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    if (lines[j].Trim() == "90" && id == null)
                    {
                        id = lines[j + 1].Trim();
                    }
                    else if (lines[j].Trim() == "91")
                    {
                        grips.Add(lines[j + 1].Trim());
                    }
                }

                found.Add(id);
                found.AddRange(grips.Take(2));
                break;
            }

            return found.ToArray();
        }

        /// <summary>The history's records after its header, as node, name and the numbers that follow.</summary>
        private static List<KeyValuePair<string, double[]>> Records(DxfDatabase db)
        {
            var records = new List<KeyValuePair<string, double[]>>();
            List<DxfTypedValue> data = History(db);

            for (int i = 0; data != null && i < data.Count; i++)
            {
                if (data[i].TypeCode != 1071)
                {
                    continue;
                }

                var numbers = new List<double>();

                for (int j = i + 2; j < data.Count && data[j].TypeCode != 1071; j++)
                {
                    numbers.Add(Real(data[j]));
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

        // ---- 1. a polar base grip dragged after the end grip, after a distance or an angle set -------------------------------

        [Fact]
        public void TheEndGripToBlockPoint0And50_TheBaseGripBy30_TheEndGripTo35And0_TurnsTheFirstLineAboutTheBasePointAsItStoodAtEachEdit()
        {
            // AutoCAD 2021 worked the three grip edits out one after the other and left the first line from (45.434873,
            // 25.724788) to (35, 0), the tip from (35, 0) to (28.99755, 9.432422), Length 5, Angle 0 and the base point (30, 0).
            DxfDatabase db = Pointer();

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            Property(db, "Length").MoveBasePoint(new GeoVector3(30, 0, 0));
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1035, 500, 0));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(5.0, ValueOf(db, "Length"), 9);
            Assert.Equal(0.0, ValueOf(db, "Angle"), 9);
            AssertPoint(Origin(db, 0), 30, 0, 0);
            AssertHas(insert, 45.434873, 25.724788, 35, 0, Near);
            AssertHas(insert, 35, 0, 28.99755, 9.432422, Near);
            string[] grips = Nodes(db, "BLOCKPOLARPARAMETER");
            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Equal(3, records.Count);
            AssertRecord(records[0], grips[2], "GRIPLOC", -100, 50, 0);
            AssertRecord(records[1], grips[1], "GRIPLOC", 30, 0, 0);
            AssertRecord(records[2], grips[2], "GRIPLOC", 35, -50, 0);
        }

        [Fact]
        public void TheBaseGripBy30_ThenTheEndGripToBlockPoint0And50_TurnsTheLinesAboutTheNewBasePoint()
        {
            // AutoCAD 2021: the first line from (45.434873, -25.724788) to (0, 50), the tip from there to (0.857493, 38.852592).
            DxfDatabase db = Pointer();

            Property(db, "Length").MoveBasePoint(new GeoVector3(30, 0, 0));
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));

            DxfBlockReference insert = TheInsert(db);
            AssertHas(insert, 45.434873, -25.724788, 0, 50, Near);
            AssertHas(insert, 0, 50, 0.857493, 38.852592, Near);
        }

        [Fact]
        public void ALengthSet_ThenTheBaseGripBy30_ThenTheEndGrip_TurnsTheLinesAboutTheBasePointAsItStoodAtEachEdit()
        {
            // AutoCAD 2021: Length 60, the base grip by (30, 0), the end grip to block point (35, 40): the first line from
            // (26.279, -29.7683) to (35, 40), the tip from (35, 40) to (28.7983, 30.6974), Length 40.3113, Angle 1.4464.
            DxfDatabase db = Pointer();

            Property(db, "Length").Value = 60.0;
            Property(db, "Length").MoveBasePoint(new GeoVector3(30, 0, 0));
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1035, 540, 0));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(40.3113, ValueOf(db, "Length"), 4);
            Assert.Equal(1.4464, ValueOf(db, "Angle"), 4);
            AssertHas(insert, 26.279, -29.7683, 35, 40, new Tolerance(1E-3, 1E-3));
            AssertHas(insert, 35, 40, 28.7983, 30.6974, new Tolerance(1E-3, 1E-3));
        }

        [Fact]
        public void AnAngleSet_ThenTheBaseGripBy20And10Down_ThenTheEndGrip_TurnsTheLinesAboutTheBasePointAsItStoodAtEachEdit()
        {
            // AutoCAD 2021: Angle 1, the base grip by (20, -10), the end grip to block point (-40, 30): Length 72.111, Angle
            // 2.5536, the first line from (5.5129, -27.033) to (-40, 30), the tip from there to (-36.7473, 19.3033); the history
            // keeps Angle 1, the base grip's GRIPLOC (20, -10, 0) and the end grip's (-94.0302, -54.1471, 0).
            DxfDatabase db = Pointer();

            Property(db, "Angle").Value = 1.0;
            Property(db, "Length").MoveBasePoint(new GeoVector3(20, -10, 0));
            Property(db, "Length").MoveEndPoint(new GeoPoint3(960, 530, 0));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(72.111, ValueOf(db, "Length"), 3);
            Assert.Equal(2.5536, ValueOf(db, "Angle"), 4);
            AssertHas(insert, 5.5129, -27.033, -40, 30, new Tolerance(1E-3, 1E-3));
            AssertHas(insert, -40, 30, -36.7473, 19.3033, new Tolerance(1E-3, 1E-3));
            string[] nodes = Nodes(db, "BLOCKPOLARPARAMETER");
            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Equal(3, records.Count);
            AssertRecord(records[0], nodes[0], "Angle", 1);
            AssertRecord(records[1], nodes[1], "GRIPLOC", 20, -10, 0);
            Assert.Equal(nodes[2] + " GRIPLOC", records[2].Key);
            Assert.Equal(-94.0302, records[2].Value[0], 3);
            Assert.Equal(-54.1471, records[2].Value[1], 3);
        }

        [Fact]
        public void TheEditsOfAnInsertWrittenToTextAndReadBack_ContinueFromItsHistory()
        {
            // AutoCAD 2021 read the library's INSERT after the end grip and the base grip, and took the end grip to (35, 0) as
            // it did without the writing in between: the first line from (45.434873, 25.724788).
            DxfDatabase db = Pointer();
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            Property(db, "Length").MoveBasePoint(new GeoVector3(30, 0, 0));
            db = Reopened(db);

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1035, 500, 0));

            AssertHas(TheInsert(db), 45.434873, 25.724788, 35, 0, Near);
            Assert.Equal(3, Records(db).Count);
        }

        [Fact]
        public void TheEditsOfAnInsertScaledByTwo_AreWorkedInTheBlocksOwnUnits_AndTheHistoryKeepsTheDistancesSo()
        {
            // AutoCAD 2021, on an INSERT turned a quarter turn at the scale 2: Width 300 kept as 150 in the history, Length 60
            // as 30, Angle 1 as 1; the first line from (0, 0) to (16.2091, 25.2441), the tip to (6.5987, 19.5309).
            DxfDatabase db = Turned(Pointer(width: true));

            Property(db, "Width").Value = 300.0;
            Property(db, "Length").Value = 60.0;
            Property(db, "Angle").Value = 1.0;

            DxfBlockReference insert = TheInsert(db);
            AssertHas(insert, 0, 0, 16.2091, 25.2441, new Tolerance(1E-4, 1E-4));
            AssertHas(insert, 16.2091, 25.2441, 6.5987, 19.5309, new Tolerance(1E-4, 1E-4));
            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Equal(3, records.Count);
            Assert.Equal(150.0, records[0].Value[0], 9);
            Assert.Equal(30.0, records[1].Value[0], 9);
            Assert.Equal(1.0, records[2].Value[0], 9);
        }

        // ---- 2. a stretch or an array that follows a base point, at a later value set ------------------------------------------

        [Fact]
        public void APanelsBaseGripBy10_ThenWidth150_KeepsTheLeftEdgeAtTheBasePoint()
        {
            // AutoCAD 2021, in memory, after DXF, after DWG and from the library's own file alike: Width 90 and the edges from
            // x 10, then Width 150: the right edge at x 160, the left, top and bottom edges still from x 10.
            DxfDatabase db = Panel(false);
            Property(db, "Width").MoveBasePoint(new GeoVector3(10, 0, 0));

            Property(db, "Width").Value = 150.0;

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(150.0, ValueOf(db, "Width"), 9);
            AssertHas(insert, 160, 0, 160, 50, Exact);
            AssertHas(insert, 10, 50, 160, 50, Exact);
            AssertHas(insert, 10, 0, 160, 0, Exact);
            AssertHas(insert, 10, 0, 10, 50, Exact);
        }

        [Fact]
        public void APanelsBaseGripBy10_WrittenAndReadBack_ThenWidth150_KeepsTheLeftEdgeAtTheBasePoint()
        {
            DxfDatabase db = Panel(false);
            Property(db, "Width").MoveBasePoint(new GeoVector3(10, 0, 0));
            db = Reopened(db);

            Property(db, "Width").Value = 150.0;

            AssertHas(TheInsert(db), 10, 0, 10, 50, Exact);
            AssertHas(TheInsert(db), 10, 0, 160, 0, Exact);
        }

        [Fact]
        public void APlateWhoseBaseGripWentBy20And0_WrittenAndReadBack_ThenWidth150_KeepsTheBottomAndLeftEdgesAtTheBasePoint()
        {
            // AutoCAD 2021: bottom from (20, 0) to (100, 0), left from (20, 0) to (0, 50), right from (100, 0) to (170, 50),
            // top from (0, 50) to (170, 50); NOTE at (1 168, 548), LOWER at (1 022, 502).
            DxfDatabase db = Plate(true);
            Property(db, "Width").MoveBasePoint(new GeoVector3(20, 0, 0));
            db = Reopened(db);

            Property(db, "Width").Value = 150.0;

            DxfBlockReference insert = TheInsert(db);
            AssertHas(insert, 20, 0, 100, 0, Exact);
            AssertHas(insert, 20, 0, 0, 50, Exact);
            AssertHas(insert, 100, 0, 170, 50, Exact);
            AssertHas(insert, 0, 50, 170, 50, Exact);
            Assert.True(
                insert
                    .AttributeCollection.Single(a => a.Tag == "NOTE")
                    .Position.IsEqualTo(new GeoPoint3(1168, 548, 0), Exact)
            );
            Assert.True(
                insert
                    .AttributeCollection.Single(a => a.Tag == "LOWER")
                    .Position.IsEqualTo(new GeoPoint3(1022, 502, 0), Exact)
            );
        }

        [Fact]
        public void AnArrayFollowingTheBasePoint_KeepsWhereTheBasePointStands_AtEachValueSetAndDrag()
        {
            // AutoCAD 2021: the base grip by 10, Width 150, the base grip by 10 again: the first circle at x 60, 60, then 70.
            DxfDatabase db = Panel(true);
            Property(db, "Width").MoveBasePoint(new GeoVector3(10, 0, 0));
            Assert.Contains(Circles(TheInsert(db)), c => c.Center.IsEqualTo(new GeoPoint3(60, 25, 0), Exact));

            Property(db, "Width").Value = 150.0;
            List<DxfCircle> circles = Circles(TheInsert(db));
            Assert.Equal(6, circles.Count);
            Assert.Contains(circles, c => c.Center.IsEqualTo(new GeoPoint3(60, 25, 0), Exact));
            Assert.Contains(circles, c => c.Center.IsEqualTo(new GeoPoint3(185, 25, 0), Exact));

            Property(db, "Width").MoveBasePoint(new GeoVector3(10, 0, 0));
            circles = Circles(TheInsert(db));
            Assert.Equal(5, circles.Count);
            Assert.Contains(circles, c => c.Center.IsEqualTo(new GeoPoint3(70, 25, 0), Exact));
            Assert.Contains(circles, c => c.Center.IsEqualTo(new GeoPoint3(170, 25, 0), Exact));
        }

        // ---- 3. the Origin of a parameter set -------------------------------------------------------------------------------

        [Fact]
        public void ThePointersOrigin_SetToAWorldPoint_MovesTheBasePointAndTheEndPointTogether_AndTheLinesStay()
        {
            // AutoCAD 2021: the Origin set to (1 030, 520, 0) reads (30, 20, 0), the world's point taken into the block's
            // coordinates; Length 100 and Angle 0 stay and the lines stay (the stretch measures the end point from the base
            // point); the history 1070 3, then the parameter's node, Origin, the point (30, 20, 0).
            DxfDatabase db = Pointer();

            OriginProperty(db, 0).Value = new GeoPoint3(1030, 520, 0);

            DxfBlockReference insert = TheInsert(db);
            AssertPoint(Origin(db, 0), 30, 20, 0);
            Assert.Equal(100.0, ValueOf(db, "Length"), 9);
            Assert.Equal(0.0, ValueOf(db, "Angle"), 9);
            AssertHas(insert, 0, 0, 100, 0, Exact);
            AssertHas(insert, 100, 0, 90, 5, Exact);
            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Single(records);
            AssertRecord(records[0], Nodes(db, "BLOCKPOLARPARAMETER")[0], "Origin", 30, 20, 0);
        }

        [Fact]
        public void TheOrigin_SetTwice_KeepsTheLastRecordAlone_AndTheSameValueSetStillWritesOne()
        {
            // AutoCAD 2021: Origin (30, 20) and then (50, 10) left the one record (50, 10, 0); the Origin set to its own value
            // (0, 0, 0) wrote the record (0, 0, 0).
            DxfDatabase db = Pointer();
            OriginProperty(db, 0).Value = new GeoPoint3(1030, 520, 0);
            OriginProperty(db, 0).Value = new GeoPoint3(1050, 510, 0);

            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Single(records);
            AssertRecord(records[0], Nodes(db, "BLOCKPOLARPARAMETER")[0], "Origin", 50, 10, 0);

            DxfDatabase same = Pointer();
            OriginProperty(same, 0).Value = new GeoPoint3(1000, 500, 0);
            AssertRecord(Records(same).Single(), Nodes(same, "BLOCKPOLARPARAMETER")[0], "Origin", 0, 0, 0);
        }

        [Fact]
        public void AnOriginSetThenTheEndGrip_TurnsTheLinesAboutTheNewBasePoint_FromTheEndPointWhereItWent()
        {
            // AutoCAD 2021: Origin (30, 20), then the end grip to block point (80, 60): Length 64.0312, Angle 0.6747, the first
            // line from (19.0678, -14.3582) to (69.0678, 25.6418), the tip to (58.1357, 23.2992); history Origin, then GRIPLOC
            // of the end grip (-50, 40, 0) from where the end point went with the Origin, (130, 20).
            DxfDatabase db = Pointer();

            OriginProperty(db, 0).Value = new GeoPoint3(1030, 520, 0);
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1080, 560, 0));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(64.0312, ValueOf(db, "Length"), 4);
            Assert.Equal(0.6747, ValueOf(db, "Angle"), 4);
            AssertHas(insert, 19.0678, -14.3582, 69.0678, 25.6418, new Tolerance(1E-3, 1E-3));
            AssertHas(insert, 69.0678, 25.6418, 58.1357, 23.2992, new Tolerance(1E-3, 1E-3));
            string[] nodes = Nodes(db, "BLOCKPOLARPARAMETER");
            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Equal(2, records.Count);
            AssertRecord(records[0], nodes[0], "Origin", 30, 20, 0);
            AssertRecord(records[1], nodes[2], "GRIPLOC", -50, 40, 0);
        }

        [Fact]
        public void AnEndGrip_ThenAnOrigin_ThenTheEndGripAgain_WorksTheThreeOut()
        {
            // AutoCAD 2021: end grip to block point (0, 50), Origin (30, 20), end grip to the world's (1 035, 500): the first
            // line from (54.2536, 46.6789) to (59.2536, 26.6789), the tip to (61.6789, 37.593).
            DxfDatabase db = Pointer();

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            OriginProperty(db, 0).Value = new GeoPoint3(1030, 520, 0);
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1035, 500, 0));

            DxfBlockReference insert = TheInsert(db);
            AssertHas(insert, 54.2536, 46.6789, 59.2536, 26.6789, new Tolerance(1E-3, 1E-3));
            AssertHas(insert, 59.2536, 26.6789, 61.6789, 37.593, new Tolerance(1E-3, 1E-3));
            Assert.Equal(3, Records(db).Count);
        }

        [Fact]
        public void ALinearOrigin_SetBesideItsLine_MovesTheWholeParameterByTheSame_AndTheEdgesWithIt()
        {
            // AutoCAD 2021, the Width of a pointer's box set to Origin (30, 120): Width 100 stays, the end point's stretch
            // takes the box's far end to (130, 120) and the edge from there to (130, 170) (moved by (30, 20)); the history
            // keeps Origin (30, 120, 0) of the linear parameter's node.
            DxfDatabase db = Pointer(width: true);

            OriginProperty(db, 1).Value = new GeoPoint3(1030, 620, 0);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(100.0, ValueOf(db, "Width"), 9);
            AssertPoint(Origin(db, 1), 30, 120, 0);
            AssertHas(insert, 0, 100, 130, 120, Exact);
            AssertHas(insert, 130, 120, 130, 170, Exact);
            AssertRecord(Records(db).Single(), Nodes(db, "BLOCKLINEARPARAMETER")[0], "Origin", 30, 120, 0);
        }

        [Fact]
        public void APlatesOrigin_SetThenWidth150_StretchesTheEdgesFromWhereTheBasePointWent()
        {
            // AutoCAD 2021: Origin (20, 10) of a plate with a stretch of its base point, then Width 150: bottom and left
            // edges from (20, 10), the right edge from (100, 0) to (170, 60), the top from (0, 50) to (170, 60); no history.
            DxfDatabase db = Plate();

            OriginProperty(db, 0).Value = new GeoPoint3(1020, 510, 0);
            Property(db, "Width").Value = 150.0;

            DxfBlockReference insert = TheInsert(db);
            AssertPoint(Origin(db, 0), 20, 10, 0);
            Assert.Equal(150.0, ValueOf(db, "Width"), 9);
            Assert.Equal(50.0, ValueOf(db, "Height"), 9);
            AssertHas(insert, 100, 0, 170, 60, Exact);
            AssertHas(insert, 0, 50, 170, 60, Exact);
            AssertHas(insert, 20, 10, 100, 0, Exact);
            AssertHas(insert, 20, 10, 0, 50, Exact);
            Assert.Null(History(db));
        }

        [Fact]
        public void TheOrigin_OnAnInsertTurnedAndScaled_IsTheWorldsPointTakenIntoTheBlocksCoordinates()
        {
            // AutoCAD 2021: on an INSERT at (1 000, 500) turned a quarter turn at the scale 2, Origin set to (1 030, 520, 0)
            // reads (10, -15, 0).
            DxfDatabase db = Turned(Pointer());

            OriginProperty(db, 0).Value = new GeoPoint3(1030, 520, 0);

            Assert.True(Origin(db, 0).IsEqualTo(new GeoPoint3(10, -15, 0), Exact));
        }

        [Fact]
        public void TheOrigin_SetToAThingThatIsNotAPoint_OrToANumberThatIsNotFinite_IsRefused()
        {
            DxfDatabase db = Pointer();
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => OriginProperty(db, 0).Value = 30.0);
            Assert.Throws<ArgumentNullException>(() => OriginProperty(db, 0).Value = null);
            Assert.Throws<ArgumentException>(() => OriginProperty(db, 0).Value = new GeoPoint3(double.NaN, 0, 0));

            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AnAfterwardsBaseGripDrag_AddsItsOwnMove_ToTheOriginSet()
        {
            // AutoCAD 2021: Origin (30, 20), then the base grip by (10, 5): Origin (40, 25), the lines stay, the history keeps
            // Origin and then GRIPLOC (10, 5, 0) of the base grip.
            DxfDatabase db = Pointer();

            OriginProperty(db, 0).Value = new GeoPoint3(1030, 520, 0);
            Property(db, "Length").MoveBasePoint(new GeoVector3(10, 5, 0));

            AssertPoint(Origin(db, 0), 40, 25, 0);
            AssertHas(TheInsert(db), 0, 0, 100, 0, Exact);
            List<KeyValuePair<string, double[]>> records = Records(db);
            Assert.Equal(2, records.Count);
            AssertRecord(records[1], Nodes(db, "BLOCKPOLARPARAMETER")[1], "GRIPLOC", 10, 5, 0);
        }

        // ---- 4. the history of grip edits ---------------------------------------------------------------------------------

        [Fact]
        public void ABlockWithAnArrayOrAScaleAction_StartsAHistoryAtTheFirstValueSet_AsAPointerDoes()
        {
            // AutoCAD 2021 started the history (1070 3) of a panel with an array, of a grid and of a bar a scale action scales
            // at the first Width set, and of no block of stretches, moves, rotates and flips alone.
            DxfDatabase array = Panel(true);
            Property(array, "Width").Value = 150.0;
            AssertRecord(Records(array).Single(), Nodes(array, "BLOCKLINEARPARAMETER")[0], "Width", 150);

            DxfDatabase scaled = Scaled();
            Property(scaled, "Width").Value = 150.0;
            AssertRecord(Records(scaled).Single(), Nodes(scaled, "BLOCKLINEARPARAMETER")[0], "Width", 150);

            DxfDatabase stretched = Panel(false);
            Property(stretched, "Width").Value = 150.0;
            Assert.Null(History(stretched));
        }

        [Fact]
        public void TheBaseGripOfALinearParameter_DraggedInABlockOfAnArray_WritesAGripLocOfItsNode()
        {
            // AutoCAD 2021: the panel's base grip by 10 wrote 1071 (the base grip's node), GRIPLOC, (10, 0, 0); by 10 more, the one
            // record (20, 0, 0).
            DxfDatabase db = Panel(true);

            Property(db, "Width").MoveBasePoint(new GeoVector3(10, 0, 0));
            string[] nodes = Nodes(db, "BLOCKLINEARPARAMETER");
            AssertRecord(Records(db).Single(), nodes[1], "GRIPLOC", 10, 0, 0);

            Property(db, "Width").MoveBasePoint(new GeoVector3(10, 0, 0));
            AssertRecord(Records(db).Single(), nodes[1], "GRIPLOC", 20, 0, 0);
        }

        [Fact]
        public void TheEndGripOfALinearParameter_DraggedInABlockOfAnArray_WritesAGripLocOfItsNode_AndTakesTheDistance()
        {
            // AutoCAD 2021: the panel's end grip by (30, 0) read Width 130 and wrote 1071 (the end grip's node), GRIPLOC,
            // (30, 0, 0); by 10 more the one record (40, 0, 0), Width 140.
            DxfDatabase db = Panel(true);

            Property(db, "Width").MoveEndPoint(new GeoPoint3(1130, 500, 0));

            Assert.Equal(130.0, ValueOf(db, "Width"), 9);
            AssertHas(TheInsert(db), 130, 0, 130, 50, Exact);
            string[] nodes = Nodes(db, "BLOCKLINEARPARAMETER");
            AssertRecord(Records(db).Single(), nodes[2], "GRIPLOC", 30, 0, 0);

            Property(db, "Width").MoveEndPoint(new GeoPoint3(1140, 500, 0));

            Assert.Equal(140.0, ValueOf(db, "Width"), 9);
            AssertRecord(Records(db).Single(), nodes[2], "GRIPLOC", 40, 0, 0);
        }

        [Fact]
        public void TheEndCornerOfAnXYParameter_DraggedInABlockOfAPolarParameter_WritesAGripLocOfItsNode()
        {
            // AutoCAD 2021: the plate's end corner by (30, 10) read Wide 130 and High 60, stretched the right and top edges to
            // (130, 160) and wrote the one record GRIPLOC (30, 10, 0) of the end grip's node.
            DxfDatabase db = PointerWithAPlate();

            Property(db, "Wide").MoveEndPoint(new GeoPoint3(1130, 660, 0));

            Assert.Equal(130.0, ValueOf(db, "Wide"), 9);
            Assert.Equal(60.0, ValueOf(db, "High"), 9);
            AssertHas(TheInsert(db), 100, 100, 130, 160, Exact);
            AssertHas(TheInsert(db), 0, 150, 130, 160, Exact);
            AssertRecord(Records(db).Single(), Nodes(db, "BLOCKXYPARAMETER")[2], "GRIPLOC", 30, 10, 0);
        }

        [Fact]
        public void TheEndGripOfALinearParameter_DraggedBesideItsLineOrBackOverTheBasePoint_IsRefused()
        {
            DxfDatabase db = Panel(true);
            string before = db.ToText();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Property(db, "Width").MoveEndPoint(new GeoPoint3(990, 500, 0))
            );
            Assert.Throws<ArgumentException>(() => Property(Others(), "Anchor X").MoveEndPoint(new GeoPoint3(0, 0, 0)));

            Assert.Equal(before, db.ToText());
        }

        /// <summary>A bar turned by a rotation parameter and a spot moved by a point parameter: the parameters that list no end grip to drag.</summary>
        private static DxfDatabase Others()
        {
            DxfDatabase db = Drawing("GH_OTHERS", out DxfBlockTableRecord block);
            DxfLine bar = Line(block, 0, 0, 100, 0);
            DxfBlockRotationParameter turn = block.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 60, 0);
            block.AddRotateAction(turn, new DxfEntity[] { bar });
            var dot = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
            block.AppendEntity(dot);
            DxfBlockPointParameter spot = block.AddPointParameter("Anchor", null, new GeoPoint3(0, 0, 0));
            block.AddMoveAction(spot, new DxfEntity[] { dot });
            Put(db, block);
            return Reopened(db);
        }

        [Fact]
        public void TwoDragsOfAGripThatBringItBack_LeaveNoRecord_AndOneThatGoesNowhereLeavesNone()
        {
            // AutoCAD 2021: the base grip by 30 and by -30 left the history 1070 3 alone; by 0 (or 1E-12 or less) left no
            // history at all; by 2E-12 or more it kept the record.
            DxfDatabase back = Pointer();
            Property(back, "Length").MoveBasePoint(new GeoVector3(30, 0, 0));
            Property(back, "Length").MoveBasePoint(new GeoVector3(-30, 0, 0));
            Assert.Equal(new short[] { 1070 }, History(back).Select(g => g.TypeCode));

            DxfDatabase nowhere = Pointer();
            Property(nowhere, "Length").MoveBasePoint(new GeoVector3(0, 0, 0));
            Assert.Null(History(nowhere));

            DxfDatabase tiny = Pointer();
            Property(tiny, "Length").MoveBasePoint(new GeoVector3(5E-13, 5E-13, 0));
            Assert.Null(History(tiny));

            DxfDatabase kept = Pointer();
            Property(kept, "Length").MoveBasePoint(new GeoVector3(2E-12, 0, 0));
            Assert.Single(Records(kept));

            DxfDatabase endBack = Pointer();
            Property(endBack, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            Property(endBack, "Length").MoveEndPoint(new GeoPoint3(1100, 500, 0));
            Assert.Equal(new short[] { 1070 }, History(endBack).Select(g => g.TypeCode));
        }

        [Fact]
        public void AnEndGripDraggedToWhereItStands_LeavesNoHistory()
        {
            DxfDatabase db = Pointer();

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1100, 500, 0));

            Assert.Null(History(db));
        }
    }
}
