using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// An entity moved by <see cref="DxfEntity.TransformBy(GeoTransform3)"/> or copied by
    /// <see cref="DxfEntity.GetTransformedCopy"/> moves its extended data with it, as AutoCAD's does: a world position (1011)
    /// moved, a distance (1041) scaled; one with none of those keeps the bytes of its extended data.
    /// </summary>
    public class DxfEntityTransformDataTests
    {
        private static readonly GeoTransform3 MovedAndDoubled = GeoTransform3.Translation(new GeoVector3(10, 0, 0)) * GeoTransform3.Scaling(2);

        private static readonly DxfTypedValue[] World =
        {
            new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "kept"),
            new DxfTypedValue(1011, "1"), new DxfTypedValue(1021, "2"), new DxfTypedValue(1031, "3"),
            new DxfTypedValue(1041, "2.5"),
        };

        [Fact]
        public void ALineMoved_MovesItsWorldPointAndScalesItsDistance_InTheWritingOfItsRecord()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.XData = World;
            string before = db.ToText();

            line.TransformBy(MovedAndDoubled);

            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, line.XData.Select(g => g.Value));
            Assert.Equal(Bits(new GeoPoint3(10, 0, 0)), Bits(line.StartPoint));
            OnlyTheseChanged(before, db.ToText(), "3E");
            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, ByHandle<DxfLine>(DxfDatabase.Parse(db.ToText()), "3E").XData.Select(g => g.Value));
        }

        [Fact]
        public void ACopyMoved_MovesItsExtendedData_AndTheOriginalKeepsIts()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.XData = World;

            DxfEntity copy = line.GetTransformedCopy(MovedAndDoubled);

            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, copy.XData.Select(g => g.Value));
            Assert.Equal(World.Select(g => g.Value), line.XData.Select(g => g.Value));
        }

        [Fact]
        public void AnEntityWithNoWorldData_KeepsTheBytesOfItsExtendedData()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            string data = Records(text).Single(r => HandleOf(r) == "3E");
            data = data.Substring(data.IndexOf("1001\n", StringComparison.Ordinal));

            line.TransformBy(MovedAndDoubled);

            Assert.EndsWith(data, Records(db.ToText()).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);
        }

        [Fact]
        public void AnInsertAndAnMTextMoved_MoveTheirExtendedData_AndAnInsertRefusingItsTransformationWritesNothing()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockReference insert = ByHandle<DxfBlockReference>(db, "65");
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            insert.XData = World;
            mtext.XData = World;

            insert.TransformBy(MovedAndDoubled);
            mtext.TransformBy(MovedAndDoubled);

            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, insert.XData.Select(g => g.Value));
            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, mtext.XData.Select(g => g.Value));

            string before = db.ToText();
            var shear = new GeoTransform3(new double[,] { { 1, 0.5, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            Assert.Throws<ArgumentException>(() => insert.TransformBy(shear));
            Assert.Equal(before, db.ToText());
            Assert.Equal("12", insert.XData[2].Value);
            Assert.Equal(12.0, double.Parse(insert.XData[2].Value, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void TheVerticesOfAPolylineAndTheAttributesOfAnInsert_MoveTheirExtendedDataWithIt_OnceEach()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfPolyline3d polyline = db.ModelSpace.OfType<DxfPolyline3d>().First();
            DxfEntity vertex = polyline.Cast<DxfEntity>().First();
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().First(b => b.AttributeCollection.Cast<object>().Any());
            DxfEntity attribute = insert.AttributeCollection.Cast<DxfEntity>().First();
            vertex.XData = World;
            attribute.XData = World;

            polyline.TransformBy(MovedAndDoubled);
            insert.TransformBy(MovedAndDoubled);

            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, vertex.XData.Select(g => g.Value));
            Assert.Equal(new[] { "GHPROBE", "kept", "12", "4", "6", "5" }, attribute.XData.Select(g => g.Value));

            // Moved on its own, a vertex or an attribute moves its own data the same way.
            vertex.TransformBy(MovedAndDoubled);
            attribute.TransformBy(MovedAndDoubled);
            Assert.Equal(new[] { "GHPROBE", "kept", "34", "8", "12", "10" }, vertex.XData.Select(g => g.Value));
            Assert.Equal(new[] { "GHPROBE", "kept", "34", "8", "12", "10" }, attribute.XData.Select(g => g.Value));

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "GHPROBE", "kept", "34", "8", "12", "10" }, again.ModelSpace.OfType<DxfPolyline3d>().First().Cast<DxfEntity>().First().XData.Select(g => g.Value));
        }
    }
}
