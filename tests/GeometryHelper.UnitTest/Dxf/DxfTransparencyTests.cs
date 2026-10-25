using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfTransparency"/> as AutoCAD's <c>Transparency</c>, and the transparency of an entity (group 440) and of a
    /// layer (extended data of AcCmTransparency), written as AutoCAD writes them; ezdxf 1.4.5 read every value set back in the
    /// probe, its recover and audit finding nothing.
    /// </summary>
    public class DxfTransparencyTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void ATransparency_IsAutoCADs_ByLayerByBlockOrAnAlpha_AndItsValueAsAFileWritesIt()
        {
            var half = new DxfTransparency(127);

            Assert.True(half.IsByAlpha);
            Assert.Equal(DxfTransparencyMethod.ByAlpha, half.Method);
            Assert.Equal(127, half.Alpha);
            Assert.Equal(0x0200007F, half.Value);
            Assert.Equal(DxfTransparency.ByLayer, default(DxfTransparency));
            Assert.True(new DxfTransparency((byte)0).IsClear);
            Assert.True(new DxfTransparency(DxfTransparencyMethod.ByAlpha).IsSolid);
            Assert.Equal(0x01000000, DxfTransparency.ByBlock.Value);
            Assert.Equal(DxfTransparency.ByBlock, DxfTransparency.OfValue(0x01000000));
            Assert.Equal(half, DxfTransparency.OfValue(33554559));
            Assert.True(DxfTransparency.OfValue(0x05000000).IsInvalid);
            Assert.NotEqual(half, new DxfTransparency(128));
            Assert.Equal("ByAlpha 127", half.ToString());
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfTransparency((DxfTransparencyMethod)7));
        }

        [Fact]
        public void AnEntitysTransparency_IsItsGroup440_WrittenAfterItsLook_AndByLayerTakesItOut()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfCircle circle = ByHandle<DxfCircle>(db, "3F");

            Assert.Equal(DxfTransparency.ByLayer, line.Transparency);
            line.Transparency = new DxfTransparency(127);
            circle.Transparency = DxfTransparency.ByBlock;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "3E", "3F");
            string record = Records(saved).Single(r => HandleOf(r) == "3E");
            Assert.Contains("  8\nsteel\n440\n33554559\n100\nAcDbLine\n", record, StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new DxfTransparency(127), ByHandle<DxfLine>(again, "3E").Transparency);
            Assert.Equal(DxfTransparency.ByBlock, ByHandle<DxfCircle>(again, "3F").Transparency);

            ByHandle<DxfLine>(again, "3E").Transparency = DxfTransparency.ByLayer;
            ByHandle<DxfCircle>(again, "3F").Transparency = DxfTransparency.ByLayer;
            Assert.Equal(text, again.ToText());
        }

        [Fact]
        public void ATransparencyGivenTheOneItHas_ChangesNothing_InEveryDrawing()
        {
            foreach (string text in Drawings)
            {
                SettingWhatItHasChangesNothing<DxfEntity>(text, e => e.Transparency = e.Transparency);
            }
        }

        [Fact]
        public void ADrawingOlderThanR2004_HasNoTransparency_AndRefusesOne()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLine line = db.ModelSpace.OfType<DxfLine>().First();

                Assert.Throws<InvalidOperationException>(() => line.Transparency = new DxfTransparency(10));
                Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { Transparency = new DxfTransparency(10) }));
                Assert.Equal(text, db.ToText());
            }

            DxfDatabase r2018 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var made = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { Transparency = new DxfTransparency(10) };
            r2018.ModelSpace.AppendEntity(made);
            Assert.Equal(new DxfTransparency(10), ByHandle<DxfLine>(DxfDatabase.Parse(r2018.ToText()), made.Handle).Transparency);
            Assert.Throws<ArgumentException>(() => made.Transparency = new DxfTransparency(DxfTransparencyMethod.ErrorValue));
        }

        [Fact]
        public void ALayersTransparency_IsItsExtendedDataOfAcCmTransparency_SolidWhereItHasNone()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            Assert.True(steel.Transparency.IsSolid);
            steel.Transparency = new DxfTransparency(64);

            string saved = db.ToText();
            string appid = HandleOf(Records(saved).Single(r => r.StartsWith("  0\nAPPID\n", StringComparison.Ordinal) && r.Contains("\nAcCmTransparency\n")));
            OnlyTheseChanged(text, saved, steel.Handle, "3", appid);
            Assert.EndsWith("1001\nAcCmTransparency\n1071\n33554496\n", Records(saved).Single(r => HandleOf(r) == steel.Handle), StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new DxfTransparency(64), again.LayerTable["steel"].Transparency);

            again.LayerTable["steel"].Transparency = new DxfTransparency(255);
            Assert.Empty(again.LayerTable["steel"].XData);
            Assert.Throws<ArgumentException>(() => steel.Transparency = DxfTransparency.ByLayer);
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000).LayerTable["steel"].Transparency = new DxfTransparency(64));

            var made = new DxfLayerTableRecord { Name = "GH_Glass", Transparency = new DxfTransparency(100) };
            Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000).LayerTable.Add(new DxfLayerTableRecord { Name = "GH_Glass", Transparency = new DxfTransparency(100) }));
            db.LayerTable.Add(made);
            Assert.Equal(new DxfTransparency(100), DxfDatabase.Parse(db.ToText()).LayerTable["GH_Glass"].Transparency);
        }
    }
}
