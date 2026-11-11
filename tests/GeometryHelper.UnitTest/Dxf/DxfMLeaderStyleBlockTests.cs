using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The block of a multileader style of block content set, as AutoCAD's <c>BlockId</c>, <c>BlockColor</c>,
    /// <c>BlockScale</c> and <c>BlockConnectionType</c> set it, written in its groups 343, 94, 47/49/140 and 177.
    /// </summary>
    public class DxfMLeaderStyleBlockTests
    {
        [Fact]
        public void TheBlockOfAStyle_ItsColourScaleAndConnection_AreWrittenAndReadBack()
        {
            var db = new DxfDatabase("AC1027");
            db.NamedObjectsDictionary.SetAt("ACAD_MLEADERSTYLE", new DxfDBDictionary());
            var style = new DxfMLeaderStyle();
            db.MLeaderStyleDictionary.SetAt("TAGS", style);
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            db.BlockTable.Add(tag);
            tag.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 5));

            style.ContentType = DxfContentType.BlockContent;
            style.Block = tag;
            style.BlockColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            style.BlockScale = new GeoVector3(2, 2, 2);
            style.BlockConnectionType = DxfBlockConnectionType.ConnectBase;

            var read = (DxfMLeaderStyle)DxfDatabase.Parse(db.ToText()).MLeaderStyleDictionary["TAGS"];
            Assert.Equal(DxfContentType.BlockContent, read.ContentType);
            Assert.Equal("TAG", read.Block.Name);
            Assert.Equal(1, read.BlockColor.ColorIndex);
            Assert.Equal(new GeoVector3(2, 2, 2), read.BlockScale);
            Assert.Equal(DxfBlockConnectionType.ConnectBase, read.BlockConnectionType);

            Assert.Throws<ArgumentOutOfRangeException>(() => style.BlockScale = new GeoVector3(0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => style.BlockConnectionType = (DxfBlockConnectionType)2);
            var other = new DxfDatabase("AC1027");
            var foreign = new DxfBlockTableRecord { Name = "TAG" };
            other.BlockTable.Add(foreign);
            Assert.Throws<ArgumentException>(() => style.Block = foreign);

            style.Block = null;
            Assert.Null(style.Block);
        }
    }
}
