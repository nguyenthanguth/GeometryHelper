using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Dynamic blocks in use, the fifth round of follow-ups (AutoCAD 2021): the grip of a point parameter, the end grip of a
    /// rotation parameter and the corners of an XY parameter other than its end corner, dragged in a block that keeps a history
    /// of its grip edits, leave one GRIPLOC of the grip where a typed value would have left records of properties.
    /// </summary>
    public class DynamicBlockFollowUp5Tests
    {
        /// <summary>
        /// A drawing with the block GH_MIXED at (1 000, 500): a pointer of a polar parameter from (0, 0) to (100, 0), a dot a
        /// point parameter Anchor at (0, 200) moves, a bar a rotation parameter Angle about (0, 300), 60 long, turns, and a plate
        /// of an XY parameter Wide and High from (0, 400) to (100, 450) whose right and top edges the end point stretches.
        /// </summary>
        private static CadDocument Mixed(out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_MIXED", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var tip = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(90, 5, 0));
            block.AppendEntity(line);
            block.AppendEntity(tip);
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

            var bar = new DxfLine(new GeoPoint3(0, 300, 0), new GeoPoint3(60, 300, 0));
            block.AppendEntity(bar);
            DxfBlockRotationParameter turn = block.AddRotationParameter("Angle", null, new GeoPoint3(0, 300, 0), 60, 0);
            block.AddRotateAction(turn, new DxfEntity[] { bar });

            var right = new DxfLine(new GeoPoint3(100, 400, 0), new GeoPoint3(100, 450, 0));
            var top = new DxfLine(new GeoPoint3(0, 450, 0), new GeoPoint3(100, 450, 0));
            block.AppendEntity(right);
            block.AppendEntity(top);
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Wide",
                null,
                "High",
                null,
                new GeoPoint3(0, 400, 0),
                new GeoPoint3(100, 450, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 440), new GeoPoint2(110, 460), new DxfEntity[] { right, top });

            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(DxfDatabase.Parse(db.ToText()), "mixed.dxf");
        }

        private static DxfBlockReference Insert(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        private static double ValueOf(CadDocument document, string handle, string name) =>
            (double)
                Insert(document, handle)
                    .DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name)
                    .Value;

        /// <summary>The names of the records of the INSERT's history of grip edits (300 after each 1071); null where it keeps none.</summary>
        private static List<string> History(CadDocument document, string handle)
        {
            DxfBlockReference insert = Insert(document, handle);
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");

            return cache.Contains("ACAD_ENHANCEDBLOCKHISTORY")
                ? ((DxfXrecord)cache.GetAt("ACAD_ENHANCEDBLOCKHISTORY"))
                    .Data.Where(g => g.TypeCode == 300)
                    .Select(g => g.Value.Trim())
                    .ToList()
                : null;
        }

        /// <summary>The index of the first custom grip that stands at a point, in the coordinates of the file.</summary>
        private static int GripAt(CadDocument document, string handle, double x, double y)
        {
            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));

            for (int i = 0; i < grips.Count; i++)
            {
                if (
                    grips[i].Custom != null
                    && System.Math.Abs(grips[i].Point.X - x) < 1E-6
                    && System.Math.Abs(grips[i].Point.Y - y) < 1E-6
                )
                {
                    return i;
                }
            }

            return -1;
        }

        [Fact]
        public void ThePointGripDraggedBy20And10_MovesTheDot_AndKeepsOneGripLoc()
        {
            CadDocument document = Mixed(out string handle);
            int grip = GripAt(document, handle, 1000, 700);
            Assert.True(grip >= 0);

            Assert.True(document.GripDrag(handle, grip, P(1020, 710)).Succeeded);

            Assert.Equal(20.0, ValueOf(document, handle, "Anchor X"), 9);
            Assert.Equal(210.0, ValueOf(document, handle, "Anchor Y"), 9);
            Assert.Equal(new[] { "GRIPLOC" }, History(document, handle));
        }

        [Fact]
        public void TheRotationGripTakenAQuarterTurn_TurnsTheBar_AndKeepsOneGripLoc()
        {
            CadDocument document = Mixed(out string handle);
            int grip = GripAt(document, handle, 1060, 800);
            Assert.True(grip >= 0);

            Assert.True(document.GripDrag(handle, grip, P(1000, 860)).Succeeded);

            Assert.Equal(System.Math.PI / 2, ValueOf(document, handle, "Angle"), 9);
            Assert.Equal(new[] { "GRIPLOC" }, History(document, handle));
        }

        [Fact]
        public void ACornerOfTheXYParameterDraggedBy10And5_TakesOneDistanceAndMovesTheBasePoint_AndKeepsOneGripLoc()
        {
            CadDocument document = Mixed(out string handle);
            int grip = GripAt(document, handle, 1000, 950);
            Assert.True(grip >= 0);

            Assert.True(document.GripDrag(handle, grip, P(1010, 955)).Succeeded);

            Assert.Equal(90.0, ValueOf(document, handle, "Wide"), 9);
            Assert.Equal(55.0, ValueOf(document, handle, "High"), 9);
            Assert.Equal(new[] { "GRIPLOC" }, History(document, handle));
        }
    }
}
