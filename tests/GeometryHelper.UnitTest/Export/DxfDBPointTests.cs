using System;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The POINT as a <see cref="DxfDBPoint"/>: read as ezdxf 1.4.5 reads it, set and read back to the bit, made in code and
    /// appended, erased, and moved as ezdxf moves one.
    /// </summary>
    public class DxfDBPointTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        private static readonly GeoTransform3 Turn = new GeoTransform3(new[,]
        {
            { 0.7816391739070251, -0.4829292842142122, 0.3947397981737998, 10.0 },
            { 0.5501172307043584, 0.8320301337746346, -0.07139249941787586, -5.0 },
            { -0.29395787843858057, 0.27295633888831433, 0.9160150668873173, 2.5 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        [Fact]
        public void ThePointsOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // ezdxf 1.4.5: POINT 41 of R2000 (44 of R12) at (3, 4, 5); POINT 86 of the dimension's block *D1 at (0, -10, 0).
            foreach (var (text, handle) in new[] { (DxfFileEzdxfDrawings.R12, "44"), (DxfFileEzdxfDrawings.R2000, "41"), (DxfFileEzdxfDrawings.R2018, "41") })
            {
                DxfDBPoint point = ByHandle<DxfDBPoint>(DxfDatabase.Parse(text), handle);
                Assert.Equal(Bits(new GeoPoint3(3, 4, 5)), Bits(point.Position));
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(point.Normal));
                Assert.Equal(0.0, point.Thickness);
                Assert.Equal(0.0, point.EcsRotation);
            }

            DxfDBPoint defining = ByHandle<DxfDBPoint>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "86");
            Assert.Equal(Bits(new GeoPoint3(0, -10, 0)), Bits(defining.Position));
            Assert.Equal("Defpoints", defining.Layer);
        }

        [Fact]
        public void TheShapeOfAPoint_IsTheReadersPoint_ToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });
            DxfDBPoint point = ByHandle<DxfDBPoint>(db, "41");

            var shape = (GeoPoint3)Assert.Single(point.GetGeometry());
            Assert.Equal(Bits((GeoPoint3)read.Shapes.Single(s => s.Handle == "41").Shape), Bits(shape));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfDBPoint>(text, point =>
                {
                    point.Position = point.Position;
                    point.Normal = point.Normal;
                    point.Thickness = point.Thickness;
                    point.EcsRotation = point.EcsRotation;
                    point.Layer = point.Layer;
                    point.XData = point.XData;
                    point.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void APointGivenANewPlaceNormalThicknessAndTurn_IsThatRecordWrittenAnew_ReadBackToTheBit()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBPoint point = ByHandle<DxfDBPoint>(db, "41");

            point.Position = new GeoPoint3(7.5, -8.25, 9.0);
            point.Normal = new GeoVector3(0.0, 0.6, 0.8);
            point.Thickness = 2.5;
            point.EcsRotation = Math.PI / 2;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "41");
            DxfDBPoint again = ByHandle<DxfDBPoint>(DxfDatabase.Parse(saved), "41");
            Assert.Equal(Bits(new GeoPoint3(7.5, -8.25, 9.0)), Bits(again.Position));
            Assert.Equal(Bits(new GeoVector3(0.0, 0.6, 0.8)), Bits(again.Normal));
            Assert.Equal(2.5, again.Thickness);
            Assert.Equal(Math.PI / 2, again.EcsRotation, 15);
            Assert.Contains(" 50\n90\n", Records(saved).Single(r => HandleOf(r) == "41"), StringComparison.Ordinal);
        }

        [Fact]
        public void APointMadeInCode_IsAppendedToEachVersion_AndReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var point = new DxfDBPoint(new GeoPoint3(9, 8, 7)) { ColorIndex = 2 };

                db.ModelSpace.AppendEntity(point);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, point.Handle);
                DxfDBPoint again = ByHandle<DxfDBPoint>(DxfDatabase.Parse(saved), point.Handle);
                Assert.Equal(Bits(new GeoPoint3(9, 8, 7)), Bits(again.Position));
                Assert.Equal(2, again.ColorIndex);
            }

            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(new DxfDBPoint().Position));
        }

        [Fact]
        public void APointErased_TakesItsRecordOut_AndRefusesASetter()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfDBPoint point = ByHandle<DxfDBPoint>(db, "44");

            point.Erase();

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "44");
            Assert.Throws<InvalidOperationException>(() => point.Position = new GeoPoint3(1, 1, 1));
        }

        [Fact]
        public void APointMoved_IsWhereEzdxfMovesIt()
        {
            // ezdxf 1.4.5 Point.transform on the oracle's turn: at (12.386899375733226, -0.37849026987776657, 7.290027054674102),
            // extrusion (0.3947397981737999, -0.07139249941787587, 0.9160150668873175).
            DxfDBPoint point = ByHandle<DxfDBPoint>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "41");

            point.TransformBy(Turn);

            Assert.True(point.Position.DistanceTo(new GeoPoint3(12.386899375733226, -0.37849026987776657, 7.290027054674102)) <= 1E-12);
            Assert.True(point.Normal.Subtract(new GeoVector3(0.3947397981737999, -0.07139249941787587, 0.9160150668873175)).Length <= 1E-12);
        }
    }
}
