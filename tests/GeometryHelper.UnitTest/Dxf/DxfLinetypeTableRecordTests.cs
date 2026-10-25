using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The dashes of a complex linetype, each with its shape or text, style, scale, rotation and offset, written as AutoCAD
    /// writes them; erasing a linetype nothing draws with. ezdxf 1.4.5 read the probe's GH_GAS back as the pattern
    /// 49 0.5, 74 0, 49 -0.2, 74 3, 75 0, 340 29, 46 0.1, 50 30, 44 -0.1, 45 -0.05, 9 GAS, 49 -0.25, 74 0.
    /// </summary>
    public class DxfLinetypeTableRecordTests
    {
        [Fact]
        public void AGasLineMadeInCode_IsWrittenAsAutoCADWritesIt_AndReadBack()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTextStyleTableRecord standard = db.TextStyleTable["Standard"];
                DxfLinetypeTableRecord gas = Gas(standard);

                Assert.Same(standard, gas.ShapeStyleAt(1));
                Assert.Equal("GAS", gas.TextAt(1));
                db.LinetypeTable.Add(gas);

                string saved = db.ToText();
                Assert.Contains(" 49\n0.5\n 74\n0\n 49\n-0.2\n 74\n3\n 75\n0\n340\n29\n 46\n0.1\n 50\n29.999999999999996\n 44\n-0.1\n 45\n-0.05\n  9\nGAS\n 49\n-0.25\n 74\n0\n", Records(saved).Single(r => r.Contains("\nGH_GAS\n")), StringComparison.Ordinal);
                DxfLinetypeTableRecord read = DxfDatabase.Parse(saved).LinetypeTable["GH_GAS"];
                Assert.Equal(3, read.NumDashes);
                Assert.Equal("29", read.ShapeStyleAt(1).Handle);
                Assert.Null(read.ShapeStyleAt(0));
                Assert.Equal(0, read.ShapeNumberAt(1));
                Assert.Equal(0.1, read.ShapeScaleAt(1));
                Assert.Equal(Bits(Math.PI / 6), Bits(read.ShapeRotationAt(1)));
                Assert.Equal(new GeoVector2(-0.1, -0.05), read.ShapeOffsetAt(1));
                Assert.True(read.ShapeIsUcsOrientedAt(1));
                Assert.False(read.ShapeIsUcsOrientedAt(0));
                Assert.Equal(string.Empty, read.TextAt(0));
                Assert.Equal(0.0, read.ShapeScaleAt(0));
                Assert.Equal(0.95, read.PatternLength, 12);
            }
        }

        [Fact]
        public void ADashOfALinetypeOfAFile_IsChangedInItsRecord_EveryOtherGroupKept_AndItsOwnValueChangesNothing()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            made.LinetypeTable.Add(Gas(made.TextStyleTable["Standard"]));
            string text = made.ToText();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLinetypeTableRecord gas = db.LinetypeTable["GH_GAS"];

            for (int i = 0; i < gas.NumDashes; i++)
            {
                gas.SetDashLengthAt(i, gas.DashLengthAt(i));
                gas.SetShapeStyleAt(i, gas.ShapeStyleAt(i));
                gas.SetShapeNumberAt(i, gas.ShapeNumberAt(i));
                gas.SetShapeOffsetAt(i, gas.ShapeOffsetAt(i));
                gas.SetShapeRotationAt(i, gas.ShapeRotationAt(i));
                gas.SetShapeScaleAt(i, gas.ShapeScaleAt(i));
                gas.SetTextAt(i, gas.TextAt(i));
                gas.SetShapeIsUcsOrientedAt(i, gas.ShapeIsUcsOrientedAt(i));
            }

            Assert.Equal(text, db.ToText());

            gas.SetTextAt(1, "HW");
            gas.SetShapeRotationAt(1, Math.PI / 2);
            gas.SetShapeIsUcsOrientedAt(1, false);
            gas.SetDashLengthAt(0, 0.75);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, gas.Handle);
            Assert.Contains(" 49\n0.75\n 74\n0\n 49\n-0.2\n 74\n2\n 75\n0\n340\n29\n 46\n0.1\n 50\n90\n 44\n-0.1\n 45\n-0.05\n  9\nHW\n 49\n-0.25\n 74\n0\n", Records(saved).Single(r => HandleOf(r) == gas.Handle), StringComparison.Ordinal);

            // A shape in the place of the text: the shape's number, the dash one of a shape (74 of 4), its text gone.
            gas.SetTextAt(1, string.Empty);
            gas.SetShapeNumberAt(1, 132);
            DxfLinetypeTableRecord read = DxfDatabase.Parse(db.ToText()).LinetypeTable["GH_GAS"];
            Assert.Equal(132, read.ShapeNumberAt(1));
            Assert.Equal(string.Empty, read.TextAt(1));
            Assert.Contains(" 74\n4\n 75\n132\n", Records(db.ToText()).Single(r => HandleOf(r) == gas.Handle), StringComparison.Ordinal);

            Assert.Throws<ArgumentOutOfRangeException>(() => gas.SetTextAt(3, "X"));
            Assert.Throws<ArgumentOutOfRangeException>(() => gas.SetShapeNumberAt(0, -1));
            Assert.Throws<ArgumentException>(() => gas.SetShapeStyleAt(0, new DxfTextStyleTableRecord { Name = "free" }));
            Assert.Throws<InvalidOperationException>(() => gas.NumDashes = 4);
        }

        [Fact]
        public void ADrawingOfR12_HasNoComplexLinetype_AndRefusesOne()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            var dashed = new DxfLinetypeTableRecord { Name = "GH_DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 0.5);
            dashed.SetDashLengthAt(1, -0.25);
            db.LinetypeTable.Add(dashed);

            Assert.Throws<InvalidOperationException>(() => dashed.SetTextAt(0, "X"));
            Assert.Throws<InvalidOperationException>(() => dashed.SetShapeScaleAt(0, 2.0));
            dashed.SetDashLengthAt(1, -0.5);
            Assert.Contains(" 49\n0.5\n 49\n-0.5\n", Records(db.ToText()).Single(r => r.Contains("\nGH_DASHED\n")), StringComparison.Ordinal);

            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.Throws<ArgumentException>(() => db.LinetypeTable.Add(Gas(other.TextStyleTable["Standard"])));
        }

        [Fact]
        public void ALinetypeErased_IsItsRecordTakenOut_WhereNothingDrawsWithIt()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var dashed = new DxfLinetypeTableRecord { Name = "GH_DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 0.5);
            dashed.SetDashLengthAt(1, -0.25);
            db.LinetypeTable.Add(dashed);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.Linetype = "GH_DASHED";

            Assert.Throws<InvalidOperationException>(() => dashed.Erase());
            line.Linetype = "ByLayer";
            db.LayerTable["steel"].LinetypeName = "GH_DASHED";
            Assert.Throws<InvalidOperationException>(() => dashed.Erase());
            db.LayerTable["steel"].LinetypeName = "Continuous";
            db.Celtype = "GH_DASHED";
            Assert.Throws<InvalidOperationException>(() => dashed.Erase());
            db.Celtype = "ByLayer";

            dashed.Erase();

            Assert.True(dashed.IsErased);
            Assert.False(db.LinetypeTable.Has("GH_DASHED"));
            Assert.Throws<ArgumentException>(() => line.Linetype = "GH_DASHED");
            Assert.Throws<InvalidOperationException>(() => db.LinetypeTable["Continuous"].Erase());
            Assert.Throws<InvalidOperationException>(() => db.LinetypeTable["ByLayer"].Erase());
        }

        /// <summary>AutoCAD's gas line: a dash 0.5, the text GAS in a style 0.1 high, 0.1 back and 0.05 down, turned 30 degrees from the x axis, and a gap.</summary>
        private static DxfLinetypeTableRecord Gas(DxfTextStyleTableRecord style)
        {
            var gas = new DxfLinetypeTableRecord { Name = "GH_GAS", AsciiDescription = "Gas ----GAS----", NumDashes = 3 };
            gas.SetDashLengthAt(0, 0.5);
            gas.SetDashLengthAt(1, -0.2);
            gas.SetTextAt(1, "GAS");
            gas.SetShapeStyleAt(1, style);
            gas.SetShapeScaleAt(1, 0.1);
            gas.SetShapeOffsetAt(1, new GeoVector2(-0.1, -0.05));
            gas.SetShapeRotationAt(1, Math.PI / 6);
            gas.SetShapeIsUcsOrientedAt(1, true);
            gas.SetDashLengthAt(2, -0.25);
            return gas;
        }
    }
}
