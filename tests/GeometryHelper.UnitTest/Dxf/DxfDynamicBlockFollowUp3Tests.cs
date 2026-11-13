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
    /// Dynamic blocks, the third round of follow-ups (plugin GhDy3a, AutoCAD 2021 side databases, grips through
    /// <c>MoveGripPointsAt</c>): the Origin property of each parameter that has a base point, the history of grip edits
    /// (ACAD_ENHANCEDBLOCKHISTORY) with its GRIPLOC records, the base point of a polar parameter moved by its base grip, and
    /// the distances a base grip leaves taken to the parameter's value set. Each number below is one AutoCAD 2021 gave on a
    /// block built as these are.
    /// </summary>
    public class DxfDynamicBlockFollowUp3Tests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

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

        /// <summary>A plate 100 by 50 at (1 000, 500) of an XY parameter from (0, 0) to (100, 50), its bottom left corner held by a stretch of the base point.</summary>
        private static DxfDatabase Plate(DxfBlockParameterValueSet xs = null, DxfBlockParameterValueSet ys = null)
        {
            DxfDatabase db = Drawing("GH_PLATE", out DxfBlockTableRecord block);
            DxfLine right = Line(block, 100, 0, 100, 50);
            DxfLine top = Line(block, 0, 50, 100, 50);
            DxfLine bottom = Line(block, 0, 0, 100, 0);
            DxfLine left = Line(block, 0, 0, 0, 50);
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Width",
                null,
                "Height",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 50, 0),
                xs,
                ys
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new DxfEntity[] { right, top });
            block.AddStretchAction(
                xy,
                new GeoPoint2(-10, -10),
                new GeoPoint2(10, 10),
                new DxfEntity[] { left, bottom }
            );
            Put(db, block);
            return FollowingTheBase(db, "BLOCKSTRETCHACTION", 1);
        }

        /// <summary>A panel 100 by 50 at (1 000, 500) of a linear parameter Width from (0, 0) to (100, 0), its left edge held by a stretch of the base point.</summary>
        private static DxfDatabase Panel(DxfBlockParameterValueSet set = null)
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
                set
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, top, bottom });
            block.AddStretchAction(width, new GeoPoint2(-10, -10), new GeoPoint2(10, 60), new[] { left, top, bottom });
            Put(db, block);
            return FollowingTheBase(db, "BLOCKSTRETCHACTION", 1);
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

        /// <summary>A bar turned by a rotation parameter and a spot moved by a point parameter: the parameters that list no base grip.</summary>
        private static DxfDatabase Others()
        {
            DxfDatabase db = Drawing("GH_OTHERS", out DxfBlockTableRecord block);
            DxfLine bar = Line(block, 0, 0, 100, 0);
            DxfBlockRotationParameter turn = block.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 60, 0);
            block.AddRotateAction(turn, new DxfEntity[] { bar });
            DxfCircle dot = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
            block.AppendEntity(dot);
            DxfBlockPointParameter spot = block.AddPointParameter("Anchor", null, new GeoPoint3(0, 0, 0));
            block.AddMoveAction(spot, new DxfEntity[] { dot });
            Put(db, block);
            return Reopened(db);
        }

        /// <summary>A flip parameter alone in its block, as AutoCAD 2021 flipped one.</summary>
        private static DxfDatabase Flipped()
        {
            DxfDatabase db = Drawing("GH_FLIP", out DxfBlockTableRecord block);
            DxfLine edge = Line(block, 100, 0, 100, 50);
            DxfBlockFlipParameter flip = block.AddFlipParameter(
                "Flip",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0)
            );
            block.AddFlipAction(flip, new DxfEntity[] { edge });
            Put(db, block);
            return Reopened(db);
        }

        // ---- reading back ------------------------------------------------------------------------------------------------

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfDatabase db, string name) =>
            TheInsert(db).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name);

        private static double ValueOf(DxfDatabase db, string name) => Assert.IsType<double>(Property(db, name).Value);

        private static GeoPoint3 Origin(DxfDatabase db, int nth) =>
            Assert.IsType<GeoPoint3>(
                TheInsert(db)
                    .DynamicBlockReferencePropertyCollection.Where(p => p.PropertyName == "Origin")
                    .ElementAt(nth)
                    .Value
            );

        private static bool Has(DxfBlockReference insert, double x1, double y1, double x2, double y2) =>
            insert
                .BlockTableRecord.OfType<DxfLine>()
                .Any(l =>
                    l.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact)
                    && l.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact)
                );

        private static List<DxfTypedValue> History(DxfDatabase db)
        {
            var representation = (DxfDBDictionary)TheInsert(db).ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");
            return cache.Contains("ACAD_ENHANCEDBLOCKHISTORY")
                ? ((DxfXrecord)cache.GetAt("ACAD_ENHANCEDBLOCKHISTORY")).Data.ToList()
                : null;
        }

        /// <summary>The ids of the base grip and the end grip of the first polar parameter of the block's graph.</summary>
        private static string[] PolarGrips(DxfDatabase db)
        {
            DxfDBObject graph = TheInsert(db).DynamicBlockTableRecord.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            DxfItem node = graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .First(n => n.Type == "BLOCKPOLARPARAMETER");
            return node.Groups.Where(g => g.TypeCode == 91).Select(g => g.Value.Trim()).Take(2).ToArray();
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static double Real(DxfTypedValue group) =>
            double.Parse(group.Value, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>The base point and the end point the INSERT saved of the first polar parameter, and its fourth point (the label).</summary>
        private static GeoPoint3[] SavedPolar(DxfDatabase db)
        {
            DxfDBObject graph = TheInsert(db).DynamicBlockTableRecord.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            string id = graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .First(n => n.Type == "BLOCKPOLARPARAMETER")
                .Groups.First(g => g.TypeCode == 90)
                .Value.Trim();
            var representation = (DxfDBDictionary)TheInsert(db).ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)
                ((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            List<DxfTypedValue> groups = ((DxfXrecord)data.GetAt(id)).Data.ToList();
            var points = new List<GeoPoint3>();

            for (int i = 0; i + 2 < groups.Count; i++)
            {
                if (groups[i].TypeCode == 10)
                {
                    points.Add(new GeoPoint3(Real(groups[i]), Real(groups[i + 1]), Real(groups[i + 2])));
                }
            }

            return points.ToArray();
        }

        private static void AssertGripLoc(DxfTypedValue[] entry, string grip, double x, double y)
        {
            Assert.Equal(new short[] { 1071, 300, 11, 21, 31 }, entry.Select(g => g.TypeCode));
            Assert.Equal(grip, entry[0].Value.Trim());
            Assert.Equal("GRIPLOC", entry[1].Value.Trim());
            Assert.Equal(x, Real(entry[2]), 9);
            Assert.Equal(y, Real(entry[3]), 9);
            Assert.Equal(0.0, Real(entry[4]), 9);
        }

        // ---- 1. the Origin property ---------------------------------------------------------------------------------------

        [Fact]
        public void AnXYParameter_GivesAnOriginAfterItsDistances_OfItsBasePoint_NotShown()
        {
            DxfDatabase db = Plate();

            List<DxfDynamicBlockReferenceProperty> properties = TheInsert(db)
                .DynamicBlockReferencePropertyCollection.ToList();

            Assert.Equal(new[] { "Width", "Height", "Origin" }, properties.Select(p => p.PropertyName));
            DxfDynamicBlockReferenceProperty origin = properties[2];
            Assert.Equal(12, origin.PropertyTypeCode);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.NoUnits, origin.UnitsType);
            Assert.False(origin.Show);
            Assert.False(origin.ReadOnly);
            Assert.True(origin.VisibleInCurrentVisibilityState);
            Assert.Empty(origin.GetAllowedValues());
            Assert.Equal(new GeoPoint3(0, 0, 0), Assert.IsType<GeoPoint3>(origin.Value));
        }

        [Fact]
        public void EachLinearAndPolarParameter_GivesItsOwnOrigin_InTheBlocksCoordinates()
        {
            DxfDatabase db = Pointer(width: true);

            Assert.Equal(
                new[] { "Length", "Angle", "Origin", "Width", "Origin" },
                TheInsert(db).DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName)
            );
            Assert.Equal(new GeoPoint3(0, 0, 0), Origin(db, 0));
            Assert.Equal(new GeoPoint3(0, 100, 0), Origin(db, 1));
        }

        [Fact]
        public void ARotationParametersOrigin_IsReadOnly_AndAFlipAndAPointParameterGiveNone()
        {
            DxfDatabase db = Others();

            List<DxfDynamicBlockReferenceProperty> properties = TheInsert(db)
                .DynamicBlockReferencePropertyCollection.ToList();

            Assert.Equal(new[] { "Angle", "Origin", "Anchor X", "Anchor Y" }, properties.Select(p => p.PropertyName));
            Assert.True(properties[1].ReadOnly);
            Assert.Equal(12, properties[1].PropertyTypeCode);
            Assert.Equal(
                new[] { "Flip" },
                TheInsert(Flipped()).DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName)
            );
        }

        [Fact]
        public void TheOrigin_IsTheBasePointAGripMovedItTo_NotScaledByTheInsert()
        {
            // AutoCAD 2021: a plate's base grip dragged by (20, 10) reads the Origin (20, 10, 0); on an INSERT turned a quarter
            // turn at the scale 2 a panel's base grip dragged by 60 along the world's y reads (30, 0, 0), the block's own units.
            DxfDatabase plate = Plate();
            Property(plate, "Width").MoveBasePoint(new GeoVector3(20, 10, 0));
            Assert.Equal(new GeoPoint3(20, 10, 0), Origin(plate, 0));

            DxfDatabase panel = Turned(Panel());
            Property(panel, "Width").MoveBasePoint(new GeoVector3(0, 60, 0));
            GeoPoint3 origin = Origin(panel, 0);
            Assert.True(origin.IsEqualTo(new GeoPoint3(30, 0, 0), Exact));
        }

        [Fact]
        public void TheOrigin_IsRead_NotSet()
        {
            DxfDatabase db = Plate();
            DxfDynamicBlockReferenceProperty origin = TheInsert(db)
                .DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Origin");

            Assert.Throws<NotSupportedException>(() => origin.Value = new GeoPoint3(20, 10, 0));
        }

        // ---- 2. the history of grip edits ---------------------------------------------------------------------------------

        [Fact]
        public void APolarEndGripDragged_WritesAGripLocWithTheEndPointsMoveFromTheBlocksOwn()
        {
            // AutoCAD 2021: the pointer's end grip dragged to (1 000, 550) reads Length 50 and Angle 90 degrees, and the history
            // 1070 3, 1071 2 (the end grip's node), 300 GRIPLOC, 11 (-100, 50, 0): where the end point is, less where the block
            // has it. The two lines turn about the base point and the tip goes to (0, 50).
            DxfDatabase db = Pointer();

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));

            Assert.Equal(50.0, ValueOf(db, "Length"), 9);
            Assert.Equal(Math.PI / 2, ValueOf(db, "Angle"), 9);
            Assert.True(Has(TheInsert(db), 0, 0, 0, 50));
            List<DxfTypedValue> history = History(db);
            Assert.Equal(1070, history[0].TypeCode);
            Assert.Equal("3", history[0].Value.Trim());
            AssertGripLoc(history.Skip(1).ToArray(), PolarGrips(db)[1], -100, 50);
        }

        [Fact]
        public void APolarEndGripDragged_TakesTheDistanceAndTheAngleToTheirValueSets()
        {
            // AutoCAD 2021, distances 50 to 300 by 10 and angles every 15 degrees: the end grip at 48.3 and 39.9 degrees from the
            // base point reads Length 50 and Angle 45 degrees, the end point (35.355, 35.355), GRIPLOC (-64.645, 35.355, 0).
            DxfDatabase db = Pointer(
                DxfBlockParameterValueSet.Increments(50, 300, 10),
                DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12)
            );

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1037, 531, 0));

            Assert.Equal(50.0, ValueOf(db, "Length"), 9);
            Assert.Equal(Math.PI / 4, ValueOf(db, "Angle"), 9);
            double half = 50.0 / Math.Sqrt(2.0);
            AssertGripLoc(History(db).Skip(1).ToArray(), PolarGrips(db)[1], half - 100, half);
        }

        [Fact]
        public void TwoDragsOfTheEndGripInARow_KeepOneGripLoc()
        {
            DxfDatabase db = Pointer();

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1010, 500, 0));

            List<DxfTypedValue> history = History(db);
            Assert.Equal(1 + 5, history.Count);
            AssertGripLoc(history.Skip(1).ToArray(), PolarGrips(db)[1], -90, 0);
        }

        [Fact]
        public void AValueSetInABlockOfAPolarParameter_StartsTheHistory_AsAutoCadStartedIt()
        {
            // AutoCAD 2021 gave a pointer set to Length 70 the history 3, 1 (the parameter), Length, 70.0; one set to Angle 0.5
            // after it added 1, Angle, 0.5; a plate, a panel, a bar, a spot or a flip set kept none.
            DxfDatabase db = Pointer();

            Property(db, "Length").Value = 70.0;
            Property(db, "Angle").Value = 0.5;

            List<DxfTypedValue> history = History(db);
            Assert.Equal(new short[] { 1070, 1071, 300, 40, 1071, 300, 40 }, history.Select(g => g.TypeCode));
            Assert.Equal("3", history[0].Value.Trim());
            Assert.Equal(new[] { "Length", "Angle" }, new[] { history[2].Value.Trim(), history[5].Value.Trim() });
            Assert.Equal(70.0, Real(history[3]), 9);
            Assert.Equal(0.5, Real(history[6]), 9);

            DxfDatabase plain = Panel();
            Property(plain, "Width").Value = 150.0;
            Assert.Null(History(plain));
        }

        [Fact]
        public void TheSamePropertySetTwiceInARow_KeepsTheLastValue_AndAnotherBetweenKeepsBoth()
        {
            // AutoCAD 2021: Width 140 and then 120 left Width 120 alone, even in two sessions; Width, Length, Width kept three.
            DxfDatabase db = Pointer(width: true);

            Property(db, "Width").Value = 140.0;
            Property(db, "Width").Value = 120.0;
            Assert.Equal(new short[] { 1070, 1071, 300, 40 }, History(db).Select(g => g.TypeCode));
            Assert.Equal(120.0, Real(History(db)[3]), 9);

            Property(db, "Length").Value = 70.0;
            Property(db, "Width").Value = 100.0;
            Assert.Equal(
                new[] { "Width", "Length", "Width" },
                History(db).Where(g => g.TypeCode == 300).Select(g => g.Value.Trim())
            );
        }

        [Fact]
        public void AGripLoc_IsHowFarTheGripWentInThatDrag_NotFromTheBlocksOwn()
        {
            // AutoCAD 2021: the end grip to (0, 50), Width set, then the end grip to (35, 0) wrote GRIPLOC (-100, 50, 0) of the
            // end grip, Width 140 and GRIPLOC (35, -50, 0) of the end grip again: 35 less 0 along x, 0 less 50 along y.
            DxfDatabase db = Pointer(width: true);

            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            Property(db, "Width").Value = 140.0;
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1035, 500, 0));

            List<DxfTypedValue> history = History(db);
            Assert.Equal(1 + 5 + 3 + 5, history.Count);
            string[] grips = PolarGrips(db);
            AssertGripLoc(history.Skip(1).Take(5).ToArray(), grips[1], -100, 50);
            AssertGripLoc(history.Skip(9).ToArray(), grips[1], 35, -50);
        }

        [Fact]
        public void AGripDragAfterAValueSet_AddsItsGripLocAfterIt()
        {
            DxfDatabase db = Pointer(width: true);

            Property(db, "Width").Value = 140.0;
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));

            Assert.Equal(
                new[] { "Width", "GRIPLOC" },
                History(db).Where(g => g.TypeCode == 300).Select(g => g.Value.Trim())
            );
        }

        // ---- 3. the base point of a polar parameter ------------------------------------------------------------------------

        [Fact]
        public void APolarBaseGrip_ListsAlwaysAndMovesTheBasePoint_TheEndPointStaying()
        {
            // AutoCAD 2021: the pointer lists two grips, the end (1 100, 500) and the base (1 000, 500); the base grip dragged by
            // (30, 10) reads Origin (30, 10, 0), Length 81.39 and Angle 6.0978 (354.3 degrees), moves no line (the end stays), and
            // writes GRIPLOC (30, 10, 0) of the base grip's node; the cache keeps the base (30, 10), the end (100, 0) and the
            // label a tenth of a unit along x of the base.
            DxfDatabase db = Pointer();
            DxfBlockReference first = TheInsert(db);
            Assert.Equal(2, first.GetDynamicGrips().Count);
            Assert.Equal(new GeoPoint3(1000, 500, 0), first.GetDynamicGrips()[1].Position);

            Property(db, "Length").MoveBasePoint(new GeoVector3(30, 10, 0));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(Math.Sqrt(70 * 70 + 10 * 10), ValueOf(db, "Length"), 9);
            Assert.Equal(Math.Atan2(-10, 70) + 2 * Math.PI, ValueOf(db, "Angle"), 9);
            Assert.True(Origin(db, 0).IsEqualTo(new GeoPoint3(30, 10, 0), Exact));
            Assert.True(Has(insert, 0, 0, 100, 0));
            Assert.True(Has(insert, 100, 0, 90, 5));
            GeoPoint3[] saved = SavedPolar(db);
            Assert.True(saved[0].IsEqualTo(new GeoPoint3(30, 10, 0), Exact));
            Assert.True(saved[1].IsEqualTo(new GeoPoint3(100, 0, 0), Exact));
            Assert.True(saved[3].IsEqualTo(new GeoPoint3(40, 10, 0), Exact));
            AssertGripLoc(History(db).Skip(1).ToArray(), PolarGrips(db)[0], 30, 10);
        }

        [Fact]
        public void APolarBaseGripDragged_TakesTheDistanceAndTheAngleToTheirValueSets_TheEndStaying()
        {
            // AutoCAD 2021, distances 50 to 300 by 10 and angles every 15 degrees: the base grip by (20, 15) reads Length 80 and
            // Angle 345 degrees, the base point at the end less 80 at -15 degrees: (22.7259, 20.7055). By (33, 0) it reads Length
            // 70 with the base at 30, and by (60, 0) Length 50 with the base at 50.
            DxfDatabase db = Pointer(
                DxfBlockParameterValueSet.Increments(50, 300, 10),
                DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12)
            );

            Property(db, "Length").MoveBasePoint(new GeoVector3(20, 15, 0));

            Assert.Equal(80.0, ValueOf(db, "Length"), 9);
            Assert.Equal(2 * Math.PI - Math.PI / 12, ValueOf(db, "Angle"), 9);
            GeoPoint3 origin = Origin(db, 0);
            Assert.Equal(100 - 80 * Math.Cos(Math.PI / 12), origin.X, 9);
            Assert.Equal(80 * Math.Sin(Math.PI / 12), origin.Y, 9);

            DxfDatabase longer = Pointer(DxfBlockParameterValueSet.Increments(50, 300, 10), null);
            Property(longer, "Length").MoveBasePoint(new GeoVector3(33, 0, 0));
            Assert.Equal(70.0, ValueOf(longer, "Length"), 9);
            Assert.Equal(new GeoPoint3(30, 0, 0), Origin(longer, 0));

            DxfDatabase shortest = Pointer(DxfBlockParameterValueSet.Increments(50, 300, 10), null);
            Property(shortest, "Length").MoveBasePoint(new GeoVector3(60, 0, 0));
            Assert.Equal(50.0, ValueOf(shortest, "Length"), 9);
            Assert.Equal(new GeoPoint3(50, 0, 0), Origin(shortest, 0));
        }

        [Fact]
        public void ThePolarEndGripDraggedAfterTheBaseMoved_TurnsTheLinesAboutTheNewBasePoint()
        {
            // AutoCAD 2021, the base grip by (20, 0) and then the end grip to 50 at 45 degrees from the new base point
            // (value sets of 10 and 15 degrees): the line from (0, 0) to (100, 0) runs from (5.8579, -14.1421) to (55.3553,
            // 35.3553), the tip from there to (44.7487, 31.8198); GRIPLOC of the base (20, 0), then of the end (-44.6447, 35.3553).
            DxfDatabase db = Pointer(
                DxfBlockParameterValueSet.Increments(50, 300, 10),
                DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12)
            );
            Property(db, "Length").MoveBasePoint(new GeoVector3(20, 0, 0));

            double half = 50.0 / Math.Sqrt(2.0);
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000 + 20 + half, 500 + half, 0));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(50.0, ValueOf(db, "Length"), 9);
            Assert.Equal(Math.PI / 4, ValueOf(db, "Angle"), 9);
            Assert.Equal(new GeoPoint3(20, 0, 0), Origin(db, 0));
            Assert.True(Has(insert, 20 - 20 / Math.Sqrt(2.0), -20 / Math.Sqrt(2.0), 20 + half, half));
            Assert.True(Has(insert, 20 + half, half, 20 + half - 15.0 / Math.Sqrt(2.0), half - 5.0 / Math.Sqrt(2.0)));
            List<DxfTypedValue> history = History(db);
            Assert.Equal(1 + 5 + 5, history.Count);
            AssertGripLoc(history.Skip(1).Take(5).ToArray(), PolarGrips(db)[0], 20, 0);
            AssertGripLoc(history.Skip(6).ToArray(), PolarGrips(db)[1], half - 100 + 20 - 0, half);
        }

        [Fact]
        public void ThePolarBaseGripDraggedAfterTheEndGrip_IsRefused_AndNothingIsWritten()
        {
            // AutoCAD 2021 worked the end grip to (0, 50), the base grip by 30 and the end grip to (35, 0) out one grip edit
            // after the other and left the first line at (45.43, 25.72); the library works from the values, so it keeps to the
            // base point moved while the end point stands where the block has it.
            DxfDatabase db = Pointer();
            Property(db, "Length").MoveEndPoint(new GeoPoint3(1000, 550, 0));
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => Property(db, "Length").MoveBasePoint(new GeoVector3(30, 0, 0)));

            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ARotationAndAPointParameter_ListNoBaseGrip()
        {
            // AutoCAD 2021 listed the end of the rotation and the point's grip, and no base grip: the grip at the
            // insertion point of the bar moved the INSERT, so there is no base point of those a grip moves.
            DxfDatabase db = Others();

            Assert.Equal(
                new[] { DxfDynamicBlockGripType.Rotation, DxfDynamicBlockGripType.Point },
                TheInsert(db).GetDynamicGrips().Select(g => g.GripType)
            );
        }

        // ---- 4. the distances a base grip leaves --------------------------------------------------------------------------

        [Fact]
        public void ALinearBaseGripDragged_TakesTheDistanceToTheValueSet_AndMovesTheBasePointBySo()
        {
            // AutoCAD 2021, Width 50 to 300 by 10 of a panel 100: the base grip by 33 reads Width 70 and the base point 30, by
            // 60 reads Width 50 (the minimum) and the base point 50; a Width set after it is taken as ever (83 to 80).
            DxfDatabase by33 = Panel(DxfBlockParameterValueSet.Increments(50, 300, 10));
            Property(by33, "Width").MoveBasePoint(new GeoVector3(33, 0, 0));
            Assert.Equal(70.0, ValueOf(by33, "Width"), 9);
            Assert.Equal(new GeoPoint3(30, 0, 0), Origin(by33, 0));
            Assert.True(Has(TheInsert(by33), 30, 0, 30, 50));

            DxfDatabase by60 = Panel(DxfBlockParameterValueSet.Increments(50, 300, 10));
            Property(by60, "Width").MoveBasePoint(new GeoVector3(60, 0, 0));
            Assert.Equal(50.0, ValueOf(by60, "Width"), 9);
            Assert.Equal(new GeoPoint3(50, 0, 0), Origin(by60, 0));

            Property(by33, "Width").Value = 83.0;
            Assert.Equal(80.0, ValueOf(by33, "Width"), 9);
            Assert.Equal(new GeoPoint3(30, 0, 0), Origin(by33, 0));
        }

        [Fact]
        public void AnXYBaseGripDragged_TakesEachDistanceToItsValueSet()
        {
            // AutoCAD 2021, Width 50 to 300 by 10 and Height 20 to 100 by 5: the base grip by (33, 13) reads Width 70 and Height
            // 35 with the base point (30, 15); by (60, 50) Width 50 and Height 20 with (50, 30).
            DxfDatabase db = Plate(
                DxfBlockParameterValueSet.Increments(50, 300, 10),
                DxfBlockParameterValueSet.Increments(20, 100, 5)
            );
            Property(db, "Width").MoveBasePoint(new GeoVector3(33, 13, 0));
            Assert.Equal(70.0, ValueOf(db, "Width"), 9);
            Assert.Equal(35.0, ValueOf(db, "Height"), 9);
            Assert.Equal(new GeoPoint3(30, 15, 0), Origin(db, 0));

            DxfDatabase low = Plate(
                DxfBlockParameterValueSet.Increments(50, 300, 10),
                DxfBlockParameterValueSet.Increments(20, 100, 5)
            );
            Property(low, "Width").MoveBasePoint(new GeoVector3(60, 50, 0));
            Assert.Equal(50.0, ValueOf(low, "Width"), 9);
            Assert.Equal(20.0, ValueOf(low, "Height"), 9);
            Assert.Equal(new GeoPoint3(50, 30, 0), Origin(low, 0));
        }

        [Fact]
        public void ABaseMoveOfAParameterOfNoValueSet_StaysAsItWas()
        {
            DxfDatabase db = Plate();

            Property(db, "Width").MoveBasePoint(new GeoVector3(33, 13, 0));

            Assert.Equal(67.0, ValueOf(db, "Width"), 9);
            Assert.Equal(37.0, ValueOf(db, "Height"), 9);
            Assert.Equal(new GeoPoint3(33, 13, 0), Origin(db, 0));
        }
    }
}
