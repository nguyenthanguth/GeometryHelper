using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Every number the library reads from a text goes through <see cref="Numbers"/>, and every one it writes as a text that
    /// has to read back as itself through <see cref="Numbers.Exact"/>. .NET Framework's own parser is a unit off in the last
    /// bit for about one decimal of 6 to 15 digits in a thousand (-0.999778 and 498.222296 are read as the double above), drops
    /// the sign of a negative nought and refuses a number past a double where .NET Core gives it infinity; its "R" prints 15
    /// or 16 digits where 16 or 17 are needed for about one double in 25 000 (the tests of the printed text fail only there).
    /// </summary>
    public class DxfNumbersRoutingTests
    {
        private static double FromBits(long bits) => BitConverter.Int64BitsToDouble(bits);

        private static void SameBits(double expected, double actual) =>
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

        /// <summary>The folder of the library's sources, found from where the tests run; null where the tests run elsewhere.</summary>
        private static string Sources()
        {
            for (
                DirectoryInfo folder = new DirectoryInfo(AppContext.BaseDirectory);
                folder != null;
                folder = folder.Parent
            )
            {
                string sources = Path.Combine(folder.FullName, "src", "GeometryHelper");

                if (File.Exists(Path.Combine(folder.FullName, "GeometryHelper.slnx")) && Directory.Exists(sources))
                {
                    return sources;
                }
            }

            return null;
        }

        [Fact]
        public void NoSourceOfTheLibrary_ReadsADoubleWithTheRuntimesParser()
        {
            string sources = Sources();

            if (sources == null)
            {
                return;
            }

            var found = new List<string>();
            var parser = new Regex(@"\b[Dd]ouble\s*\.\s*(Try)?Parse\s*\(", RegexOptions.CultureInvariant);

            foreach (string path in Directory.GetFiles(sources, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (parser.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        found.Add(path.Substring(sources.Length + 1) + ":" + (i + 1));
                    }
                }
            }

            Assert.Empty(found);
        }

        // The compiler's own literal is the double nearest its text; Framework's parse reads the first two as the double above.
        [Theory]
        [InlineData("-0.999778", -0.999778)]
        [InlineData("498.222296", 498.222296)]
        [InlineData("0.1", 0.1)]
        [InlineData("1e+300", 1e+300)]
        [InlineData("1.5E-7", 1.5E-7)]
        [InlineData("  12.25 ", 12.25)]
        [InlineData("1.7976931348623157E308", double.MaxValue)]
        public void Parse_ReadsTheDoubleNearestTheText_OnEitherRuntime(string text, double expected)
        {
            Assert.True(Numbers.TryRead(text, out double read));
            SameBits(expected, read);
            SameBits(expected, Numbers.Parse(text));
        }

        [Fact]
        public void Parse_KeepsTheSignOfANegativeNought()
        {
            SameBits(-0.0, Numbers.Parse("-0"));
            SameBits(-0.0, Numbers.Parse("-0.0"));
            SameBits(0.0, Numbers.Parse("+0"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("abc")]
        [InlineData("1,5")]
        [InlineData("1e")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        [InlineData("1.#INF")]
        [InlineData("-1.#IND")]
        [InlineData("1.#QNAN")]
        [InlineData("1e400")]
        [InlineData("-1e400")]
        public void Parse_RefusesWhatIsNotAFiniteNumber_OnEitherRuntime(string text)
        {
            Assert.False(Numbers.TryRead(text, out double value));
            Assert.Equal(0.0, value);
            Assert.Throws<FormatException>(() => Numbers.Parse(text));
        }

        [Theory]
        [InlineData("NaN", double.NaN)]
        [InlineData("nan", double.NaN)]
        [InlineData(" NaN ", double.NaN)]
        [InlineData("Infinity", double.PositiveInfinity)]
        [InlineData("+Infinity", double.PositiveInfinity)]
        [InlineData("-Infinity", double.NegativeInfinity)]
        [InlineData("-infinity", double.NegativeInfinity)]
        public void TryReadNotFinite_ReadsTheWordsNetWritesForANumberThatIsNotFinite(string text, double expected)
        {
            Assert.True(Numbers.TryReadNotFinite(text, out double value));
            SameBits(expected, value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("-")]
        [InlineData("Inf")]
        [InlineData("1.#INF")]
        [InlineData("--Infinity")]
        [InlineData("-NaN")]
        [InlineData("NaN1")]
        public void TryReadNotFinite_RefusesAnyOtherText(string text)
        {
            Assert.False(Numbers.TryReadNotFinite(text, out double value));
            Assert.Equal(0.0, value);
        }

        [Fact]
        public void ADwgRecordsReal_OfANumberPastADouble_IsNoughtOnEitherRuntime()
        {
            // .NET Core's parser gives infinity for 1e400 and Framework's refuses it: the same number now on both.
            Assert.Equal(0.0, DwgRecord.Double("1e400"));
            Assert.Equal(0.0, DwgRecord.Double("-1e400"));
            Assert.Equal(0.0, DwgRecord.Double("1.#INF"));
            SameBits(-0.999778, DwgRecord.Double(" -0.999778 "));
            Assert.True(double.IsNaN(DwgRecord.Double("NaN")));
            Assert.True(double.IsPositiveInfinity(DwgRecord.Double("Infinity")));
        }

        [Fact]
        public void ABlockConstraintsValueSetAs498_222296_IsTheDoubleOfThatText()
        {
            // The value goes through the 12 figures AutoCAD keeps and back: 498.222296 is read as the double above on Framework.
            SameBits(498.222296, DxfBlockConstraints.Rounded(498.222296));
            SameBits(-0.999778, DxfBlockConstraints.Rounded(-0.999778));
            Assert.Equal(612.345678901, DxfBlockConstraints.Rounded(612.3456789012345));
        }

        [Fact]
        public void ANumberInDecimals_IsOfTheDigitsThatReadBackAsIt_WhereTheFrameworksRFallsShort()
        {
            // 4532.5992882869205 is 4532.59928828692 in .NET Framework's "R", which reads back as the double below it.
            double value = FromBits(0x40B1B4996AF50933);
            Assert.Equal("4532.5992882869205", Numbers.Exact(value));
            Assert.Equal("4532.599288286921", DxfTableEditingText.Decimal(value));
            Assert.Equal("-4532.599288286921", DxfTableEditingText.Decimal(-value));
            Assert.Equal("4.532599288286921E+03", DxfTableEditingText.Scientific(value));
            Assert.Equal("4532.599288286921, 4532.599288286921, 0", DxfTableEditingText.Point(value, value, 0.0));
        }

        [Fact]
        public void AHatchsVertexOf17Digits_IsWrittenInR12AsTheNumberItIs()
        {
            double x = FromBits(0x40B1B4996AF50933);
            var database = new DxfDatabase("AC1032");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[]
                {
                    new GeoPoint2(x, 0),
                    new GeoPoint2(x + 100, 0),
                    new GeoPoint2(x + 100, 60),
                    new GeoPoint2(x, 60),
                },
                new double[4]
            );
            database.ModelSpace.AppendEntity(hatch);

            string text;

            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1009");
                text = Encoding.GetEncoding("iso-8859-1").GetString(stream.ToArray());
            }

            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var held = new List<double>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "1040")
                {
                    held.Add(Numbers.Parse(lines[i + 1]));
                }
            }

            Assert.NotEmpty(held);
            Assert.Contains(held, h => BitConverter.DoubleToInt64Bits(h) == BitConverter.DoubleToInt64Bits(x));
        }

        [Fact]
        public void AFieldsReal_IsTheShortestTextOf15To17DigitsThatReadsBackAsIt()
        {
            // -439.2756472524608 is the 16 digits Framework's parse says read back; the double it reads is the one next to it.
            double value = FromBits(unchecked((long)0xC07B74690D17E8D3));
            var database = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                TextString = "x",
                Position = new GeoPoint3(0, 0, 0),
                Height = 2.5,
            };
            database.ModelSpace.AppendEntity(text);
            var area = new DxfFieldDraft
            {
                Evaluator = "AcObjProp",
                Code = "\\AcObjProp Object(%<\\_ObjIdx 0>%).Area",
                State = 59,
                Status = 2,
                Value = new DxfFieldValue
                {
                    Type = 2,
                    Double = value,
                    Flags = 4,
                    Shown = "x",
                },
                Shown = "x",
            };
            var root = new DxfFieldDraft
            {
                Evaluator = "_text",
                Code = "%<\\_FldIdx 0>%",
                State = 9,
                Status = 2,
                Value = new DxfFieldValue { Flags = 3 },
            };
            root.Children.Add(area);
            text.SetField("TEXT", root);

            string[] lines = database.ToText().Replace("\r", string.Empty).Split('\n');
            var held = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "140")
                {
                    held.Add(lines[i + 1].Trim());
                }
            }

            Assert.Contains(Numbers.Exact(value), held);
            Assert.DoesNotContain("-439.2756472524608", held);
        }

        [Fact]
        public void ASeriesOfNumbersFilledDown_IsTheDoubleOfItsFifteenFigures_WhereTheFrameworkParseIsAUnitOff()
        {
            // 1.001025 and 1.001026 go on to 1.001027: the double nearest it, not the one above, as Framework reads its 15 figures.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
            table.SetSize(5, 2);
            table.SetValue(2, 0, 1.001025);
            table.SetValue(3, 0, 1.001026);
            DxfDatabase reread = DxfDatabase.Parse(db.ToText());
            table = DxfAutoCadChecksD16Tests.Table(reread);
            table.Fill(new DxfCellRange(4, 0, 4, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.GenerateSeries);
            string[] lines = reread.ToText().Replace("\r", string.Empty).Split('\n');
            var held = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "140")
                {
                    held.Add(lines[i + 1].Trim());
                }
            }

            // The one the fill made.
            Assert.Single(held, v => v == Numbers.Exact(1.001027));
        }

        [Fact]
        public void AFractionsStackedHeight_IsWrittenAsTheNumberItIs_WhereTheFrameworksRFallsShort()
        {
            // 0.5287137706664921 is 0.528713770666492 in Framework's "R", which reads back as the double below it.
            double stack = FromBits(0x3FE0EB39243EA33F);
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDimension dim = db.ModelSpace.OfType<DxfRotatedDimension>().First();
            dim.Dimlfac = 1.0;
            dim.Dimrnd = 0.0;
            dim.Dimzin = 0;
            dim.Dimpost = string.Empty;
            dim.Dimalt = false;
            dim.Dimlunit = 4;
            dim.Dimdec = 4;
            dim.Dimfrac = 0;
            dim.Dimtfac = stack;
            Assert.Equal("1'-3{\\H" + Numbers.Exact(stack) + "x;\\S1/2;}\"", dim.FormatMeasurement(15.5, string.Empty));
            Assert.Equal("0.5287137706664921", Numbers.Exact(stack));
        }
    }
}
