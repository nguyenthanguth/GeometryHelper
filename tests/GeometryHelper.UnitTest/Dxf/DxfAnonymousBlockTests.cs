using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Anonymous blocks added as AutoCAD's BlockTable.Add adds a block named *D or *U: named by the table, *D1 the first the
    /// drawing has not, marked anonymous (flag 1 of the BLOCK's 70) and written as ezdxf writes the *D1 of a dimension; and the
    /// picture of a dimension made in code, read back as its DimBlock after saving.
    /// </summary>
    public class DxfAnonymousBlockTests
    {
        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void ADimensionMadeInCode_WithAnAnonymousBlockOfALineAndAText_ReadsBackWithItsBlock(string version)
        {
            var db = new DxfDatabase(version);
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);

            Assert.Equal("*D1", picture.Name);
            Assert.True(picture.IsAnonymous);
            Assert.False(picture.IsDynamicBlock);
            Assert.False(picture.IsLayout);
            Assert.Same(picture, db.BlockTable["*D1"]);

            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(100, 20, 0)));
            picture.AppendEntity(new DxfMText { Location = new GeoPoint3(50, 22, 0), TextHeight = 2.5, Contents = "100" });
            var dimension = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 20, 0), string.Empty, null) { DimBlock = picture };
            db.ModelSpace.AppendEntity(dimension);

            DxfDatabase read = Opened(Saved(db));
            DxfRotatedDimension again = Assert.Single(read.ModelSpace.OfType<DxfRotatedDimension>());
            DxfBlockTableRecord block = again.DimBlock;
            Assert.Equal("*D1", block.Name);
            Assert.True(block.IsAnonymous);
            Assert.False(block.IsDynamicBlock);
            Assert.Equal(Bits(100.0), Bits(again.Measurement));
            List<DxfEntity> drawn = block.ToList();
            Assert.Equal(new[] { typeof(DxfLine), typeof(DxfMText) }, drawn.Select(e => e.GetType()));
            var line = (DxfLine)drawn[0];
            Assert.Equal(Bits(new GeoPoint3(0, 20, 0)) + ";" + Bits(new GeoPoint3(100, 20, 0)), Bits(line.StartPoint) + ";" + Bits(line.EndPoint));
            Assert.Equal("100", ((DxfMText)drawn[1]).Contents);

            // The dimension names its block; the BLOCK is *D1 of flag 1, as ezdxf and AutoCAD write a dimension's picture.
            string text = read.ToText();
            Assert.Contains("  2\n*D1\n", Records(text).Single(r => HandleOf(r) == again.Handle), StringComparison.Ordinal);
            Assert.Contains("100\nAcDbBlockBegin\n  2\n*D1\n 70\n1\n", text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AnAnonymousBlockAdded_IsWrittenAsEzdxfWritesTheD1OfADimension(string version)
        {
            // ezdxf 1.4.5's *D1: BLOCK_RECORD 4E, its BLOCK 4F.
            string fixture = version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018;
            DxfDatabase db = new DxfDatabase(version == "R2000" ? "AC1015" : "AC1032");
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);

            string text = db.ToText();
            string block = Records(text).Single(r => r.StartsWith("  0\nBLOCK\n", StringComparison.Ordinal) && r.Contains("\n*D1\n"));
            string record = Records(text).Single(r => HandleOf(r) == picture.Handle);
            Assert.Equal(Groups(Records(fixture).Single(r => HandleOf(r) == "4E")), Groups(record));
            Assert.Equal(Groups(Records(fixture).Single(r => HandleOf(r) == "4F")), Groups(block));
        }

        [Theory]
        [InlineData("R12", "*D10")]
        [InlineData("R2000", "*D12")]
        [InlineData("R2018", "*D12")]
        public void AnAnonymousBlock_TakesTheFirstNumberTheDrawingHasNot(string version, string expected)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R12" ? DxfDimensionDrawings.R12 : version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018);
            var picture = new DxfBlockTableRecord { Name = "*D" };
            var other = new DxfBlockTableRecord { Name = "*d" };
            var u = new DxfBlockTableRecord { Name = "*U" };

            db.BlockTable.Add(picture);
            db.BlockTable.Add(other);
            db.BlockTable.Add(u);

            Assert.Equal(expected, picture.Name);
            Assert.Equal("*D" + (int.Parse(expected.Substring(2), System.Globalization.CultureInfo.InvariantCulture) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), other.Name);
            Assert.True(other.IsAnonymous);
            Assert.Equal("*U1", u.Name);
            Assert.True(u.IsAnonymous);
            List<string> anonymous = DxfDatabase.Parse(db.ToText()).BlockTable.Where(b => b.IsAnonymous).Select(b => b.Name).ToList();
            Assert.Contains(expected, anonymous);
            Assert.Contains(other.Name, anonymous);
            Assert.Contains("*U1", anonymous);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("*X")]
        [InlineData("*D1")]
        [InlineData("*U7")]
        [InlineData("*DD")]
        [InlineData(" *D")]
        public void ANameOfAStarOtherThanStarDOrStarU_IsRefused_AsBefore(string name)
        {
            var db = new DxfDatabase("AC1032");
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => db.BlockTable.Add(new DxfBlockTableRecord { Name = name }));
            Assert.Throws<ArgumentException>(() => db.LayerTable.Add(new DxfLayerTableRecord { Name = "*D" }));
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AnAnonymousBlockTheFileRefuses_KeepsTheNameItWasGiven()
        {
            // A drawing of R2000 has no units of a block, which came with R2007.
            var db = new DxfDatabase("AC1015");
            var picture = new DxfBlockTableRecord { Name = "*D", Units = DxfUnits.Millimeters };

            Assert.Throws<ArgumentException>(() => db.BlockTable.Add(picture));
            Assert.Equal("*D", picture.Name);
            Assert.False(db.BlockTable.Has("*D1"));

            picture.Units = DxfUnits.Undefined;
            db.BlockTable.Add(picture);
            Assert.Equal("*D1", picture.Name);
            Assert.True(picture.IsAnonymous);
        }

        /// <summary>The groups of a record but its handle (5) and owner (330), each code and value, numbers read as numbers.</summary>
        private static List<string> Groups(string record)
        {
            string[] lines = record.Split('\n');
            var groups = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                string code = lines[i].Trim();

                if (code == "5" || code == "330")
                {
                    continue;
                }

                string value = lines[i + 1];
                groups.Add(code + "=" + (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number) && code != "2" && code != "3" ? Bits(number) : value));
            }

            return groups;
        }
    }
}
