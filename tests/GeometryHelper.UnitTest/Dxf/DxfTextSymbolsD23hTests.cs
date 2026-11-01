using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The symbols of AutoCAD 2021's text editor (D23h): each constant the letter its Symbol menu enters and its help lists,
    /// and each written by the library into an MTEXT and a TEXT of R2004 and of R2018 read back as that letter, as AutoCAD
    /// 2021 read back the library's files headless (210 readings of 21 symbols in MTEXT, TEXT, ATTRIB and dimension text,
    /// every one the letter) and wrote its own the same bytes.
    /// </summary>
    public class DxfTextSymbolsD23hTests
    {
        private static readonly string[] Symbols =
        {
            DxfTextSymbols.Degree, DxfTextSymbols.PlusMinus, DxfTextSymbols.Diameter, DxfTextSymbols.AlmostEqual,
            DxfTextSymbols.Angle, DxfTextSymbols.BoundaryLine, DxfTextSymbols.CenterLine, DxfTextSymbols.Delta,
            DxfTextSymbols.ElectricalPhase, DxfTextSymbols.FlowLine, DxfTextSymbols.Identity, DxfTextSymbols.InitialLength,
            DxfTextSymbols.MonumentLine, DxfTextSymbols.NotEqual, DxfTextSymbols.Ohm, DxfTextSymbols.Omega,
            DxfTextSymbols.PropertyLine, DxfTextSymbols.Subscript2, DxfTextSymbols.Squared, DxfTextSymbols.Cubed,
            DxfTextSymbols.NonBreakingSpace,
        };

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        /// <summary>The bytes of a new drawing of a version holding an MTEXT and a TEXT of the words, as the library saves it.</summary>
        private static byte[] Saved(string version, string words)
        {
            var db = new DxfDatabase(version);
            db.ModelSpace.AppendEntity(new DxfMText { Contents = words, Location = P(0, 0), TextHeight = 2.5 });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = words, Position = P(0, 10), Height = 2.5 });

            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>The lines of a file, read as Latin-1 so that each byte is one character, that are the given words.</summary>
        private static int CountLines(byte[] file, string latin1)
            => Encoding.GetEncoding("ISO-8859-1").GetString(file).Split('\n').Count(line => line.TrimEnd('\r') == latin1);

        [Theory]
        [InlineData(DxfTextSymbols.Degree, 0x00B0)]
        [InlineData(DxfTextSymbols.PlusMinus, 0x00B1)]
        [InlineData(DxfTextSymbols.Diameter, 0x2205)]
        [InlineData(DxfTextSymbols.AlmostEqual, 0x2248)]
        [InlineData(DxfTextSymbols.Angle, 0x2220)]
        [InlineData(DxfTextSymbols.BoundaryLine, 0xE100)]
        [InlineData(DxfTextSymbols.CenterLine, 0x2104)]
        [InlineData(DxfTextSymbols.Delta, 0x0394)]
        [InlineData(DxfTextSymbols.ElectricalPhase, 0x0278)]
        [InlineData(DxfTextSymbols.FlowLine, 0xE101)]
        [InlineData(DxfTextSymbols.Identity, 0x2261)]
        [InlineData(DxfTextSymbols.InitialLength, 0xE200)]
        [InlineData(DxfTextSymbols.MonumentLine, 0xE102)]
        [InlineData(DxfTextSymbols.NotEqual, 0x2260)]
        [InlineData(DxfTextSymbols.Ohm, 0x2126)]
        [InlineData(DxfTextSymbols.Omega, 0x03A9)]
        [InlineData(DxfTextSymbols.PropertyLine, 0x214A)]
        [InlineData(DxfTextSymbols.Subscript2, 0x2082)]
        [InlineData(DxfTextSymbols.Squared, 0x00B2)]
        [InlineData(DxfTextSymbols.Cubed, 0x00B3)]
        [InlineData(DxfTextSymbols.NonBreakingSpace, 0x00A0)]
        public void EachSymbol_IsTheOneLetterAutoCadsSymbolMenuEnters(string symbol, int code)
        {
            Assert.Equal(((char)code).ToString(), symbol);
        }

        [Fact]
        public void TheControlCodes_AreThoseAutoCadsSymbolMenuEntersForTheDegreePlusMinusAndDiameterSigns()
        {
            Assert.Equal("%%d", DxfTextSymbols.DegreeCode);
            Assert.Equal("%%p", DxfTextSymbols.PlusMinusCode);
            Assert.Equal("%%c", DxfTextSymbols.DiameterCode);
        }

        [Fact]
        public void TheDiameter_IsNeitherTheLatinLetterOWithStroke_NorTheDiameterSignOfTechnicalSymbols()
        {
            Assert.NotEqual("Ø", DxfTextSymbols.Diameter);
            Assert.NotEqual("⌀", DxfTextSymbols.Diameter);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1032")]
        public void EverySymbol_WrittenIntoAnMTextAndATextOfR2004OrR2018_ReadsBackAsTheSameLetter(string version)
        {
            foreach (string symbol in Symbols)
            {
                string words = "x" + symbol + "y";
                byte[] file = Saved(version, words);
                DxfDatabase db = DxfDatabase.Open(new MemoryStream(file));
                DxfMText mtext = db.ModelSpace.OfType<DxfMText>().Single();

                Assert.Equal(words, mtext.Contents);
                Assert.Equal(words, mtext.Text);
                Assert.Equal(words, db.ModelSpace.OfType<DxfDBText>().Single().TextString);
                Assert.All(DxfReader.Read(new MemoryStream(file)).Shapes.Select(s => s.Shape).OfType<DxfText>(), text => Assert.Equal(words, text.Value));
                Assert.Equal(2, DxfReader.Read(new MemoryStream(file)).Shapes.Count(s => s.Shape is DxfText));
            }
        }

        [Fact]
        public void TheSymbolsWrittenIntoADrawingOfR2004_AreTheBytesAutoCadWrites_ItsCodePagesLettersAsTheyAreAndTheRestAsEscapes()
        {
            // AutoCAD 2021's own DxfOut to R2004 of an MTEXT and a TEXT of these words, in the page ANSI_1252.
            string all = "x" + string.Concat(Symbols) + "y";
            string written = "x°±\\U+2205\\U+2248\\U+2220\\U+E100\\U+2104\\U+0394\\U+0278\\U+E101\\U+2261\\U+E200\\U+E102"
                + "\\U+2260\\U+2126\\U+03A9\\U+214A\\U+2082²³ y";

            Assert.Equal(2, CountLines(Saved("AC1018", all), written));
        }

        [Fact]
        public void TheDiameterBeforeTwenty_IsWrittenAsItsEscapeInR2004_AndAsItsLetterInR2018_AndReadsBackAsTheDiameterTwenty()
        {
            string words = DxfTextSymbols.Diameter + "20";

            Assert.Equal(2, CountLines(Saved("AC1018", words), "\\U+220520"));
            Assert.Equal(2, CountLines(Saved("AC1032", words), "â\u0088\u008520"));
            Assert.Equal("∅20", DxfDatabase.Open(new MemoryStream(Saved("AC1018", words))).ModelSpace.OfType<DxfMText>().Single().Contents);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1032")]
        public void TheDegreeAndPlusMinusCodes_AreKeptInTheWords_AndReadAsTheirSignsInAnMTextsText(string version)
        {
            // AutoCAD 2021 read the library's files so: TextString and Contents as written, MText.Text the signs.
            string words = "45" + DxfTextSymbols.DegreeCode + " " + DxfTextSymbols.PlusMinusCode + "0.5";
            DxfDatabase db = DxfDatabase.Open(new MemoryStream(Saved(version, words)));
            DxfMText mtext = db.ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal("45%%d %%p0.5", mtext.Contents);
            Assert.Equal("45%%d %%p0.5", db.ModelSpace.OfType<DxfDBText>().Single().TextString);
            Assert.Equal("45" + DxfTextSymbols.Degree + " " + DxfTextSymbols.PlusMinus + "0.5", mtext.Text);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1032")]
        public void TheDiameterCode_IsKeptInTheWordsOfAnMTextAndATextAsWritten(string version)
        {
            string words = DxfTextSymbols.DiameterCode + "20";
            DxfDatabase db = DxfDatabase.Open(new MemoryStream(Saved(version, words)));

            Assert.Equal("%%c20", db.ModelSpace.OfType<DxfMText>().Single().Contents);
            Assert.Equal("%%c20", db.ModelSpace.OfType<DxfDBText>().Single().TextString);
        }
    }
}
