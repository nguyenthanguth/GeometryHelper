using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A leaning text (an oblique angle) given to the attribute of an INSERT scaled unlike along its two axes keeps the shape the
    /// scale makes of it, as AutoCAD 2021's <c>SetAttributeFromBlock</c> and its INSERT worked it (plugin GhAs1, accoreconsole):
    /// its height the height of the slanted upright across the new baseline, its width the new baseline over that height, its
    /// lean the new one. The library took the height of the upright before the lean, whatever the lean was, and wrote a text
    /// of the right lean and the wrong size.
    /// </summary>
    public class DxfLeaningTextUnlikeScaleTests
    {
        /// <summary>
        /// A block of two attribute definitions, T8 3 high, 0.7 wide, leaning 15 degrees, and T13 3 high, 1.3 wide, leaning -10
        /// degrees, centred and bottom aligned on (85, 5).
        /// </summary>
        private static DxfBlockTableRecord Block(DxfDatabase db)
        {
            var block = new DxfBlockTableRecord { Name = "GH_LEAN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lean = new DxfAttributeDefinition(new GeoPoint3(70, 5, 0), "VAL8", "T8", "Prompt 8", null)
            {
                Height = 3,
                WidthFactor = 0.7,
                Oblique = 15 * Math.PI / 180,
            };
            block.AppendEntity(lean);
            var centred = new DxfAttributeDefinition(new GeoPoint3(85, 5, 0), "VAL13", "T13", "Prompt 13", null)
            {
                Height = 3,
                HorizontalMode = DxfTextHorizontalMode.TextCenter,
                VerticalMode = DxfTextVerticalMode.TextBottom,
                AlignmentPoint = new GeoPoint3(85, 5, 0),
                WidthFactor = 1.3,
                Oblique = -10 * Math.PI / 180,
            };
            block.AppendEntity(centred);
            return block;
        }

        private static DxfAttributeReference Synced(DxfBlockTableRecord block, string tag, DxfBlockReference insert)
        {
            var attribute = new DxfAttributeReference();
            attribute.SetAttributeFromBlock(
                block.OfType<DxfAttributeDefinition>().Single(d => d.Tag == tag),
                insert.BlockTransform
            );
            insert.AttributeCollection.AppendAttribute(attribute);
            return attribute;
        }

        private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 4);

        private static double Turns(double angle) => Math.IEEERemainder(angle, 2 * Math.PI);

        /// <summary>
        /// An INSERT at (5 000, 500) scaled 2 along x, AutoCAD 2021 gave T8 3 high, 1.4 wide, leaning 0.49195 (the tangent of
        /// 15 degrees doubled), and T13 3 high, 2.6 wide, leaning -0.33904; the library gave 2.644, 1.588 and 2.829, 2.757.
        /// </summary>
        [Fact]
        public void ALeaningTextOfAnInsertStretchedAlongX_KeepsItsHeight_AndDoublesItsWidthAndTheTangentOfItsLean()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = Block(db);
            var insert = new DxfBlockReference(new GeoPoint3(5000, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(2, 1, 1),
            };
            db.ModelSpace.AppendEntity(insert);

            DxfAttributeReference plain = Synced(block, "T8", insert);
            DxfAttributeReference centred = Synced(block, "T13", insert);

            Near(3.0, plain.Height);
            Near(1.4, plain.WidthFactor);
            Near(0.49195, Turns(plain.Oblique));
            Near(0.0, Turns(plain.Rotation));
            Near(3.0, centred.Height);
            Near(2.6, centred.WidthFactor);
            Near(-0.33904, Turns(centred.Oblique));
        }

        /// <summary>
        /// An INSERT turned 75 degrees and scaled (2, 0.5): AutoCAD 2021 gave T8 1.5 high, 2.8 wide, leaning 0.82 turned
        /// 1.309 (75 degrees), and T13 1.5 high, 5.2 wide, leaning -0.6143; the library gave 1.023, 4.104 and 1.226, 6.363.
        /// </summary>
        [Fact]
        public void ALeaningTextOfAnInsertTurnedAndStretched_KeepsTheShapeTheScaleMakesOfIt()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = Block(db);
            var insert = new DxfBlockReference(new GeoPoint3(13000, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(2, 0.5, 1),
                Rotation = 75 * Math.PI / 180,
            };
            db.ModelSpace.AppendEntity(insert);

            DxfAttributeReference plain = Synced(block, "T8", insert);
            DxfAttributeReference centred = Synced(block, "T13", insert);

            Near(1.5, plain.Height);
            Near(2.8, plain.WidthFactor);
            Near(0.82, Turns(plain.Oblique));
            Near(1.309, Turns(plain.Rotation));
            Near(1.5, centred.Height);
            Near(5.2, centred.WidthFactor);
            Near(-0.6143, Turns(centred.Oblique));
        }

        /// <summary>
        /// An INSERT turned 40 degrees and scaled (1, 3): AutoCAD 2021 gave T8 9 high, 0.23333 wide, leaning 0.0891, and T13 9
        /// high, 0.43333 wide, leaning -0.0587; the library gave 8.964, 0.23426 and 8.984, 0.43408.
        /// </summary>
        [Fact]
        public void ALeaningTextOfAnInsertStretchedAlongY_KeepsTheShapeTheScaleMakesOfIt()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = Block(db);
            var insert = new DxfBlockReference(new GeoPoint3(6000, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(1, 3, 1),
                Rotation = 40 * Math.PI / 180,
            };
            db.ModelSpace.AppendEntity(insert);

            DxfAttributeReference plain = Synced(block, "T8", insert);
            DxfAttributeReference centred = Synced(block, "T13", insert);

            Near(9.0, plain.Height);
            Assert.Equal(0.23333, plain.WidthFactor, 4);
            Assert.Equal(0.0891, Turns(plain.Oblique), 3);
            Near(9.0, centred.Height);
            Assert.Equal(0.43333, centred.WidthFactor, 4);
            Assert.Equal(-0.0587, Turns(centred.Oblique), 3);
        }

        /// <summary>The same blocks scaled alike along both axes keep every size they had: a lean changes nothing then.</summary>
        [Fact]
        public void ALeaningTextOfAnInsertScaledAlike_KeepsItsLean_AndScalesItsHeightAlone()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = Block(db);
            var insert = new DxfBlockReference(new GeoPoint3(3000, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(2, 2, 2),
            };
            db.ModelSpace.AppendEntity(insert);

            DxfAttributeReference plain = Synced(block, "T8", insert);

            Near(6.0, plain.Height);
            Near(0.7, plain.WidthFactor);
            Near(15 * Math.PI / 180, Turns(plain.Oblique));
        }
    }
}
