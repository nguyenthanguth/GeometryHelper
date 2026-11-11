using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The control codes of a text as AutoCAD 2021 reads them (D23i), checked headless on its own MTEXT and TEXT: %%c is the
    /// diameter sign U+2205 in both; an MTEXT's <c>MText.Text</c> keeps %%% and %%nnn as written, and draws them so (an MTEXT
    /// "x%%%y" 46.667 wide at height 10 against 26.667 for "x%y"), where a TEXT reads them as the per cent sign and the
    /// character of that code (26.667 wide both); an MTEXT's <c>\~</c> is the no-break space U+00A0. A dimension AutoCAD wrote
    /// without its group 70 (and 50) is a rotated dimension along the X axis.
    /// </summary>
    public class DxfTextCodesD23iTests
    {
        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        private static byte[] Saved(DxfDatabase db)
        {
            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                return stream.ToArray();
            }
        }

        private static DxfDatabase Words(string words)
        {
            var db = new DxfDatabase("AC1032");
            db.ModelSpace.AppendEntity(
                new DxfMText
                {
                    Contents = words,
                    Location = P(0, 0),
                    TextHeight = 2.5,
                }
            );
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = words,
                    Position = P(0, 10),
                    Height = 2.5,
                }
            );
            return db;
        }

        private static string[] Values(DxfDatabase db, bool multiline) =>
            DxfReader
                .Read(new MemoryStream(Saved(db)))
                .Shapes.Select(s => s.Shape)
                .OfType<DxfText>()
                .Where(t => t.IsMultiline == multiline)
                .Select(t => t.Value)
                .ToArray();

        [Fact]
        public void TheDiameterCode_IsReadAsTheDiameterSignOfAutoCad_InAnMTextAndAText()
        {
            DxfDatabase db = Words("x%%cy %%C");
            string expected = "x" + DxfTextSymbols.Diameter + "y " + DxfTextSymbols.Diameter;

            Assert.Equal(expected, db.ModelSpace.OfType<DxfMText>().Single().Text);
            Assert.Equal(new[] { expected }, Values(db, true));
            Assert.Equal(new[] { expected }, Values(db, false));
        }

        [Theory]
        [InlineData("x%%%y")]
        [InlineData("x%%248y")]
        [InlineData("50%%%d %%065%%066")]
        public void APerCentSignAndACharacterByItsCode_AreKeptAsWritten_InAnMText(string written)
        {
            DxfDatabase db = Words(written);

            Assert.Equal(written, db.ModelSpace.OfType<DxfMText>().Single().Text);
            Assert.Equal(new[] { written }, Values(db, true));
        }

        [Theory]
        [InlineData("x%%%y", "x%y")]
        [InlineData("x%%248y", "xøy")]
        public void APerCentSignAndACharacterByItsCode_AreRead_InAText(string written, string expected)
        {
            Assert.Equal(new[] { expected }, Values(Words(written), false));
        }

        [Fact]
        public void TheNoBreakSpaceCodeOfAnMText_IsTheNoBreakSpace()
        {
            DxfDatabase db = Words("1\\~2");

            Assert.Equal("1\u00A02", db.ModelSpace.OfType<DxfMText>().Single().Text);
            Assert.Equal(new[] { "1\u00A02" }, Values(db, true));
        }

        [Fact]
        public void ADimensionWrittenWithoutItsKindOrAngle_IsOpenedAsARotatedDimensionAlongX()
        {
            var db = new DxfDatabase("AC1018");
            var picture = new DxfBlockTableRecord { Name = "GH_DIMBLOCK" };
            db.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(P(0, 5), P(30, 5)));
            db.ModelSpace.AppendEntity(
                new DxfRotatedDimension(0.0, P(0, 0), P(30, 0), P(15, 5), string.Empty, null) { DimBlock = picture }
            );
            string[] lines = Encoding.ASCII.GetString(Saved(db)).Replace("\r", string.Empty).Split('\n');

            // AutoCAD's DxfOut of such a dimension on a named block leaves out its 70 and its 50.
            var kept = new List<string>();
            bool inDimension = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                string code = lines[i].Trim();

                if (code == "0")
                {
                    inDimension = lines[i + 1].Trim() == "DIMENSION";
                }
                else if (inDimension && (code == "70" || code == "50"))
                {
                    continue;
                }

                kept.Add(lines[i]);
                kept.Add(lines[i + 1]);
            }

            string file = string.Join("\n", kept) + "\n";
            Assert.DoesNotContain("AcDbDimension\n 70", file);

            DxfDatabase opened = DxfDatabase.Open(new MemoryStream(Encoding.ASCII.GetBytes(file)));
            DxfRotatedDimension dimension = Assert.IsType<DxfRotatedDimension>(
                opened.ModelSpace.OfType<DxfEntity>().Single(e => e is DxfDimension || e is DxfUnknownEntity)
            );

            Assert.Equal(0.0, dimension.Rotation);
            Assert.Equal(30.0, dimension.Measurement, 9);
        }
    }
}
