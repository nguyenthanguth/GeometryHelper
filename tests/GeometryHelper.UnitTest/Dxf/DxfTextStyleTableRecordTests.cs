using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The text style table edited: a style added, its font, height, width and slant set, and its TrueType font kept in the
    /// extended data of ACAD as AutoCAD keeps it; ezdxf 1.4.5 read the probe's GH_ROMANS and GH_ARIAL back.
    /// </summary>
    public class DxfTextStyleTableRecordTests
    {
        [Fact]
        public void AStyleAdded_IsAStyleRecordOfTheTable_ReadBackAsItWasMade()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var romans = new DxfTextStyleTableRecord { Name = "GH_ROMANS", FileName = "romans.shx", TextSize = 2.5, XScale = 0.75, ObliquingAngle = 15 * Math.PI / 180, IsVertical = true };

                Assert.Equal("romans.shx", romans.FileName);
                Assert.Equal(2.5, romans.PriorSize);
                db.TextStyleTable.Add(romans);

                Assert.Same(romans, db.TextStyleTable["gh_romans"]);
                Assert.Throws<ArgumentException>(() => db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Gh_Romans" }));
                DxfTextStyleTableRecord read = DxfDatabase.Parse(db.ToText()).TextStyleTable["GH_ROMANS"];
                Assert.Equal("romans.shx", read.FileName);
                Assert.Equal(string.Empty, read.BigFontFileName);
                Assert.Equal(2.5, read.TextSize);
                Assert.Equal(0.75, read.XScale);
                Assert.Equal(15 * Math.PI / 180, read.ObliquingAngle, 15);
                Assert.True(read.IsVertical);
                Assert.Equal(2.5, read.PriorSize);
                Assert.Equal(new DxfFontDescriptor(string.Empty, false, false, 0, 0), read.Font);
            }
        }

        [Fact]
        public void AStyleOfTheFile_IsChangedInItsRecord_AndEverySetterGivenItsValueChangesNothing()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            foreach (DxfTextStyleTableRecord style in db.TextStyleTable)
            {
                style.FileName = style.FileName;
                style.BigFontFileName = style.BigFontFileName;
                style.TextSize = style.TextSize;
                style.XScale = style.XScale;
                style.ObliquingAngle = style.ObliquingAngle;
                style.IsVertical = style.IsVertical;
                style.Font = style.Font;
                style.Name = style.Name;
            }

            Assert.Equal(text, db.ToText());

            DxfTextStyleTableRecord standard = db.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            standard.TextSize = 3.5;
            standard.XScale = 0.8;
            standard.ObliquingAngle = -10 * Math.PI / 180;
            standard.IsVertical = true;
            standard.BigFontFileName = "bigfont.shx";
            standard.Font = new DxfFontDescriptor("Arial", true, false, 0, 34);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "29");
            Assert.EndsWith("1001\nACAD\n1000\nArial\n1071\n33554466\n", Records(saved).Single(r => HandleOf(r) == "29"), StringComparison.Ordinal);
            DxfTextStyleTableRecord read = DxfDatabase.Parse(saved).TextStyleTable["Standard"];
            Assert.Equal("arial.ttf", read.FileName);
            Assert.Equal(3.5, read.TextSize);
            Assert.Equal(0.8, read.XScale);
            Assert.Equal(-10 * Math.PI / 180, read.ObliquingAngle, 15);
            Assert.True(read.IsVertical);
            Assert.Equal("bigfont.shx", read.BigFontFileName);
            Assert.Equal(new DxfFontDescriptor("Arial", true, false, 0, 34), read.Font);
            Assert.True(read.Font.Bold);
            Assert.Equal(34, read.Font.PitchAndFamily);

            Assert.Throws<ArgumentOutOfRangeException>(() => standard.ObliquingAngle = Math.PI / 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => standard.XScale = 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => standard.TextSize = -1.0);
            Assert.Throws<InvalidOperationException>(() => standard.Name = "Other");

            standard.Font = new DxfFontDescriptor(string.Empty, false, false, 0, 0);
            Assert.Null(standard.GetXDataForApplication("ACAD"));
        }
    }
}
