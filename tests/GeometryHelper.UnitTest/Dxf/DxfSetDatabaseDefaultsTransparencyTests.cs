using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The transparency <see cref="DxfEntity.SetDatabaseDefaults(DxfDatabase)"/> gives an entity, as AutoCAD's
    /// <c>setDatabaseDefaults</c> gives the transparency of CETRANSPARENCY: in a drawing of R2004 (AC1018) or later, where an
    /// entity may have one; an R2000 drawing has none to give, and the entity is left as it was.
    /// </summary>
    public class DxfSetDatabaseDefaultsTransparencyTests
    {
        // The alpha 100 of 255 is 61 per cent clear and 30 is 88, both under the 90 per cent AutoCAD allows a new entity.
        [Theory]
        [InlineData("AC1018", 100)]
        [InlineData("AC1027", 100)]
        [InlineData("AC1032", 30)]
        public void SetDatabaseDefaults_GivesTheEntityTheTransparencyOfCetransparency_InAFileOfR2004OrLater(
            string version,
            byte alpha
        )
        {
            var db = new DxfDatabase(version);
            db.Cetransparency = new DxfTransparency(alpha);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            db.ModelSpace.AppendEntity(line);
            Assert.Equal(DxfTransparency.ByLayer, line.Transparency);

            line.SetDatabaseDefaults(db);

            Assert.Equal(new DxfTransparency(alpha), line.Transparency);

            // ByBlock is given as it is, and ByLayer takes the group out again.
            db.Cetransparency = DxfTransparency.ByBlock;
            line.SetDatabaseDefaults(db);
            Assert.Equal(DxfTransparency.ByBlock, line.Transparency);

            db.Cetransparency = DxfTransparency.ByLayer;
            line.SetDatabaseDefaults(db);
            Assert.Equal(DxfTransparency.ByLayer, line.Transparency);
        }

        // An R2000 drawing (AC1015) holds a CETRANSPARENCY in its variables, but an entity of it cannot carry a transparency:
        // the entity stays by layer, and the call does not throw as setting Transparency on it does.
        [Fact]
        public void SetDatabaseDefaults_LeavesTransparencyAloneInAnR2000File()
        {
            var db = new DxfDatabase("AC1015");
            db.Cetransparency = new DxfTransparency(100);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            db.ModelSpace.AppendEntity(line);
            db.Clayer = "0";

            line.SetDatabaseDefaults(db);

            Assert.Equal(DxfTransparency.ByLayer, line.Transparency);
            Assert.Equal("0", line.Layer);
        }
    }
}
