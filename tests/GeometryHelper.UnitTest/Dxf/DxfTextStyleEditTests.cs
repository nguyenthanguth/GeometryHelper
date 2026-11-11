using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What AutoCAD's Text Style dialog writes into a STYLE record and the library could not: the backwards and upside-down
    /// flags (group 71), a style renamed with the texts written in it, and a style deleted only where nothing is written in it.
    /// </summary>
    public class DxfTextStyleEditTests
    {
        [Fact]
        public void BackwardsAndUpsideDown_AreWrittenAsGroup71_ForAStyleOfAFileAndOneMadeInCode()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "GH_MIRROR",
                    FileName = "romans.shx",
                    FlagBits = 6,
                }
            );
            DxfTextStyleTableRecord standard = db.TextStyleTable["Standard"];
            standard.FlagBits = 2;

            DxfDatabase read = DxfDatabase.Parse(db.ToText());

            Assert.Equal(6, read.TextStyleTable["GH_MIRROR"].FlagBits);
            Assert.Equal(2, read.TextStyleTable["Standard"].FlagBits);
            Assert.Throws<ArgumentOutOfRangeException>(() => standard.FlagBits = 1);
        }

        [Fact]
        public void AStyleRenamed_TakesItsTextsAndTheCurrentStyleWithIt()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_NOTES", FileName = "romans.shx" });
            db.Textstyle = "GH_NOTES";
            var text = new DxfDBText
            {
                Position = new GeoPoint3(0, 0, 0),
                Height = 2.5,
                TextString = "BEAM B-12",
                TextStyleName = "GH_NOTES",
            };
            db.ModelSpace.AppendEntity(text);
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 10, 0),
                TextHeight = 2.5,
                Contents = "PLATE",
                TextStyleName = "GH_NOTES",
            };
            db.ModelSpace.AppendEntity(mtext);

            db.TextStyleTable["GH_NOTES"].Name = "GH_LABELS";

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.TextStyleTable.Has("GH_NOTES"));
            Assert.True(read.TextStyleTable.Has("GH_LABELS"));
            Assert.Equal("GH_LABELS", read.Textstyle);
            Assert.Equal("GH_LABELS", read.ModelSpace.OfType<DxfDBText>().Single().TextStyleName);
            Assert.Equal("GH_LABELS", read.ModelSpace.OfType<DxfMText>().Single().TextStyleName);
            Assert.Throws<ArgumentException>(() => read.TextStyleTable["GH_LABELS"].Name = "Standard");
        }

        [Fact]
        public void AStyleIsDeleted_OnlyWhereNothingIsWrittenInIt_AndItIsNeitherStandardNorCurrent()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_USED", FileName = "romans.shx" });
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SPARE", FileName = "romans.shx" });
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 0, 0),
                    Height = 2.5,
                    TextString = "A",
                    TextStyleName = "GH_USED",
                }
            );

            Assert.Throws<InvalidOperationException>(() => db.TextStyleTable["Standard"].Erase());
            Assert.Throws<InvalidOperationException>(() => db.TextStyleTable["GH_USED"].Erase());
            db.TextStyleTable["GH_SPARE"].Erase();

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.TextStyleTable.Has("GH_SPARE"));
            Assert.True(read.TextStyleTable.Has("GH_USED"));
        }

        [Fact]
        public void AStyleADimensionStylePointsAt_IsNotDeleted()
        {
            var db = new DxfDatabase("AC1027");
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_DIMS", FileName = "romans.shx" });
            db.DimStyleTable.Add(
                new DxfDimStyleTableRecord { Name = "GH_25", Dimtxsty = db.TextStyleTable["GH_DIMS"] }
            );

            Assert.Throws<InvalidOperationException>(() => db.TextStyleTable["GH_DIMS"].Erase());
        }
    }
}
