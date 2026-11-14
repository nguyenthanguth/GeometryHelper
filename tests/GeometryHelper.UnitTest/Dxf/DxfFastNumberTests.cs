using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The numbers the fast read of a DXF record reads from bytes, against the number reader of <see cref="DxfReader"/> that
    /// reads the same text as a string: the same double to the bit, or the same refusal, for every text.
    /// </summary>
    /// <remarks>
    /// Each kind of text runs to <c>GH_NUMBER_COUNT</c> numbers (50 000 where it is not set; the check before a release is
    /// 20 000 000), and <c>GH_FUZZ_SEED</c> moves the seed.
    /// </remarks>
    public class DxfFastNumberTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int Count()
        {
            return
                int.TryParse(
                    Environment.GetEnvironmentVariable("GH_NUMBER_COUNT"),
                    NumberStyles.Integer,
                    Inv,
                    out int n
                )
                && n > 0
                ? n
                : 50000;
        }

        private static int Seed()
        {
            return int.TryParse(
                Environment.GetEnvironmentVariable("GH_FUZZ_SEED"),
                NumberStyles.Integer,
                Inv,
                out int n
            )
                ? n
                : 0;
        }

        /// <summary>The difference between the two readers for a text, or null where they agree.</summary>
        private static string Differs(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            bool fast = DxfFastNumber.TryReal(bytes, 0, bytes.Length, out double got);
            bool slow = Numbers.TryReadLong(text, out double expected);

            if (fast != slow)
            {
                return "'" + text + "' read " + fast + " by the fast reader, " + slow + " by the number reader";
            }

            // The reader's own entry, which takes the fast read of a string first, gives what the long way gives.
            bool entry = Numbers.TryRead(text, out double entered);

            if (
                entry != slow
                || (slow && BitConverter.DoubleToInt64Bits(entered) != BitConverter.DoubleToInt64Bits(expected))
            )
            {
                return "'"
                    + text
                    + "' read "
                    + entry
                    + " as "
                    + entered.ToString("R", Inv)
                    + " by TryRead, "
                    + slow
                    + " as "
                    + expected.ToString("R", Inv)
                    + " by TryReadLong";
            }

            return fast && BitConverter.DoubleToInt64Bits(got) != BitConverter.DoubleToInt64Bits(expected)
                ? "'"
                    + text
                    + "' read as "
                    + got.ToString("R", Inv)
                    + " ("
                    + BitConverter.DoubleToInt64Bits(got).ToString("x", Inv)
                    + ") by the fast reader, "
                    + expected.ToString("R", Inv)
                    + " ("
                    + BitConverter.DoubleToInt64Bits(expected).ToString("x", Inv)
                    + ") by the number reader"
                : null;
        }

        private static void Check(IEnumerable<string> texts)
        {
            var wrong = new List<string>();

            foreach (string text in texts)
            {
                string why = Differs(text);

                if (why != null && wrong.Count < 5)
                {
                    wrong.Add(why);
                }
            }

            Assert.Empty(wrong);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-0")]
        [InlineData("0.0")]
        [InlineData("-0.000")]
        [InlineData("0e5")]
        [InlineData("-0E-5")]
        [InlineData("1")]
        [InlineData("+1")]
        [InlineData("-12.5")]
        [InlineData(".5")]
        [InlineData("5.")]
        [InlineData("-.5e1")]
        [InlineData("007.50")]
        [InlineData("1e22")]
        [InlineData("1e23")]
        [InlineData("1E-22")]
        [InlineData("1E-23")]
        [InlineData("9007199254740992")]
        [InlineData("9007199254740993")]
        [InlineData("9007199254740995")]
        [InlineData("4503599627370497.5")]
        [InlineData("18014398509481986")]
        [InlineData("1234.5678901234567")]
        [InlineData("-1234.5678901234567")]
        [InlineData("0.30000000000000004")]
        [InlineData("0.1")]
        [InlineData("123456789012345678")]
        [InlineData("1234567890123456789")]
        [InlineData("9999999999999999999")]
        [InlineData("12345678901234567890")]
        [InlineData("1.4210854715202004E-14")]
        [InlineData("-3.5527136788005009e-15")]
        [InlineData("1.7976931348623157E308")]
        [InlineData("1.7976931348623159E308")]
        [InlineData("2.2250738585072014E-308")]
        [InlineData("2.2250738585072011E-308")]
        [InlineData("4.9406564584124654E-324")]
        [InlineData("1e-400")]
        [InlineData("1e400")]
        [InlineData("1E+3")]
        [InlineData("1E+0003")]
        [InlineData(" 12.5 ")]
        [InlineData("\t12.5\t")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("-")]
        [InlineData("+")]
        [InlineData(".")]
        [InlineData("e5")]
        [InlineData("1e")]
        [InlineData("1e+")]
        [InlineData("1.2.3")]
        [InlineData("1,5")]
        [InlineData("0x10")]
        [InlineData("abc")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("--1")]
        [InlineData("1 2")]
        [InlineData("1\v")]
        public void ATextOfEveryKind_IsReadAsTheNumberReaderReadsIt(string text)
        {
            Assert.Null(Differs(text));
        }

        [Fact]
        public void DigitsAndAPointAndAnExponentAtRandom_AreReadAsTheNumberReaderReadsThem()
        {
            var rnd = new Random(2026 + Seed());
            var texts = new List<string>();

            for (int i = 0; i < Count(); i++)
            {
                int length = rnd.Next(1, 22);
                var text = new StringBuilder();

                if (rnd.Next(4) == 0)
                {
                    text.Append(rnd.Next(2) == 0 ? '-' : '+');
                }

                int point = rnd.Next(5) == 0 ? -1 : rnd.Next(length + 1);

                for (int d = 0; d < length; d++)
                {
                    if (d == point)
                    {
                        text.Append('.');
                    }

                    text.Append((char)('0' + (d == 0 && rnd.Next(8) != 0 ? rnd.Next(1, 10) : rnd.Next(10))));
                }

                if (point == length)
                {
                    text.Append('.');
                }

                if (rnd.Next(3) == 0)
                {
                    text.Append(rnd.Next(2) == 0 ? 'e' : 'E');

                    if (rnd.Next(3) == 0)
                    {
                        text.Append(rnd.Next(2) == 0 ? '-' : '+');
                    }

                    text.Append(rnd.Next(rnd.Next(2) == 0 ? 40 : 330).ToString(Inv));
                }

                texts.Add(text.ToString());
            }

            Check(texts);
        }

        [Fact]
        public void TheShortestTextOfADoubleOfAnyBits_IsReadAsTheNumberReaderReadsIt()
        {
            var rnd = new Random(7 + Seed());
            var bits = new byte[8];
            var texts = new List<string>();

            for (int i = 0; i < Count(); i++)
            {
                rnd.NextBytes(bits);
                double value = BitConverter.ToDouble(bits, 0);

                // Of every size, and of those a drawing holds: up to a million either way, to the 17 digits AutoCAD writes.
                if (i % 2 == 0)
                {
                    value = (rnd.NextDouble() - 0.5) * Math.Pow(10, rnd.Next(-12, 9));
                }

                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    continue;
                }

                texts.Add(value.ToString("R", Inv));
                texts.Add(value.ToString("G17", Inv));
                texts.Add(value.ToString("E16", Inv));
            }

            Check(texts);
        }

        [Fact]
        public void TheTiesBetweenTwoDoubles_AreReadAsTheNumberReaderReadsThem()
        {
            // A text exactly half way between two doubles goes to the even one; one more or less goes to its own side.
            var rnd = new Random(11 + Seed());
            var texts = new List<string>();

            for (int i = 0; i < Count(); i++)
            {
                int exponent = rnd.Next(41, 64);
                ulong mantissa = (1UL << 52) | ((ulong)rnd.Next() << 21) ^ (ulong)rnd.Next(1 << 21);
                mantissa &= (1UL << 53) - 1;
                mantissa |= 1UL << 52;
                BigInteger tie;
                int fraction;

                if (exponent >= 52)
                {
                    BigInteger spacing = BigInteger.One << (exponent - 52);
                    tie = ((BigInteger)mantissa * spacing) + (spacing >> 1);
                    fraction = 0;

                    if (exponent == 52)
                    {
                        // Spacing one: the half is a fraction of one digit.
                        tie = (BigInteger)mantissa * 10 + 5;
                        fraction = 1;
                    }
                }
                else
                {
                    // Spacing 2^-n: the half is the odd multiple of 2^-(n+1); written in decimal it has n + 1 digits after the point.
                    int n = 52 - exponent;
                    BigInteger scaled = ((BigInteger)mantissa * 2) + 1;
                    tie = scaled * BigInteger.Pow(5, n + 1);
                    fraction = n + 1;
                }

                foreach (int step in new[] { -1, 0, 1 })
                {
                    BigInteger moved = tie + step;

                    if (moved < 0)
                    {
                        continue;
                    }

                    string digits = moved.ToString(Inv);

                    if (fraction > 0)
                    {
                        digits = digits.PadLeft(fraction + 1, '0');
                        digits =
                            digits.Substring(0, digits.Length - fraction)
                            + "."
                            + digits.Substring(digits.Length - fraction);
                    }

                    texts.Add(digits);
                }
            }

            Check(texts);
        }

        [Fact]
        public void DigitsOfAPowerOfTen_AreReadAsTheNumberReaderReadsThem()
        {
            var rnd = new Random(13 + Seed());
            var texts = new List<string>();

            for (int i = 0; i < Count(); i++)
            {
                // 1, 10, 100, ... and the digits one off them, and the numbers 5 and 9 times them, with exponents.
                int zeros = rnd.Next(0, 19);
                string lead = new[] { "1", "9", "5", "4", "2" }[rnd.Next(5)];
                string run = lead + new string('0', zeros);

                if (rnd.Next(3) == 0 && zeros > 0)
                {
                    run = run.Substring(0, run.Length - 1) + (rnd.Next(2) == 0 ? "1" : "9");
                }

                if (rnd.Next(3) == 0)
                {
                    run += new string('9', rnd.Next(0, 19 - Math.Min(18, zeros)));
                }

                string text = run + "e" + rnd.Next(-120, 80).ToString(Inv);
                texts.Add(text);
                texts.Add(run.Insert(rnd.Next(run.Length + 1), "."));
            }

            Check(texts);
        }

        [Theory]
        [InlineData("9007199254740993", true)]
        [InlineData("9007199254740995", true)]
        [InlineData("18014398509481986", true)]
        [InlineData("18014398509481987", true)]
        [InlineData("9007199254740993e5", true)]
        [InlineData("1.8014398509481987e16", true)]
        [InlineData("9223372036854775809", true)]
        [InlineData("90071992547409930000", false)]
        public void AnExactTieBetweenTwoDoubles_OfWholeDigitsTimesAPowerOfTen_IsSettledByTheFastReadAlone(
            string text,
            bool settled
        )
        {
            // The power of ten is held whole by the 128 bits, so the product is exact and a tie goes to the even double: no
            // second end of a range to disagree with the first, and nothing left to the long way. Of 20 digits there is no
            // such product, and the long way reads it.
            bool sure = DxfFastNumber.TryText(text, 0, text.Length, out double fast);
            bool read = Numbers.TryReadLong(text, out double expected);

            Assert.True(read);
            Assert.Equal(settled, sure);

            if (sure)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(fast));
            }
        }

        /// <summary>The letters of these code points, written as numbers so that no editor or tool turns them into other letters.</summary>
        private static string Beyond(params int[] codePoints) =>
            string.Concat(Array.ConvertAll(codePoints, char.ConvertFromUtf32));

        [Fact]
        public void ATextWithLettersBeyondAscii_IsReadByTheNumberReaderAsTheLongWayReadsIt()
        {
            // No-break and em spaces, Arabic-Indic and full-width digits, a Kelvin sign, a vulgar fraction, a NUL.
            string[] texts =
            {
                Beyond(0xA0) + "12.5",
                "12.5" + Beyond(0x2003),
                Beyond(0xA0, 0xA0) + " -3e2 " + Beyond(0xA0),
                Beyond(0x661, 0x662),
                Beyond(0xFF11, 0xFF12) + "." + Beyond(0xFF15),
                "1" + Beyond(0xA0) + "2",
                "12.5\0",
                Beyond(0xA0),
                "1e" + Beyond(0x661),
                "1" + Beyond(0x212A),
                Beyond(0xBD),
            };

            foreach (string text in texts)
            {
                bool entry = Numbers.TryRead(text, out double entered);
                bool slow = Numbers.TryReadLong(text, out double expected);

                Assert.Equal(slow, entry);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(entered));
            }
        }

        [Fact]
        public void EveryValueOfEveryGroupOfTheDrawingsOfAFolder_IsReadByTryReadAsTheLongWayReadsIt()
        {
            // GH_DXF_CORPUS names a folder of DXF files: the text of each group, a number or not, is read both ways.
            string folder = Environment.GetEnvironmentVariable("GH_DXF_CORPUS");

            if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
            {
                return;
            }

            var wrong = new List<string>();
            int texts = 0;
            int numbers = 0;

            foreach (string path in System.IO.Directory.GetFiles(folder, "*.dxf"))
            {
                DxfFile file;

                try
                {
                    file = DxfFile.Open(path);
                }
                catch (FormatException)
                {
                    continue;
                }

                var items = new List<DxfItem>(file.ModelSpace);
                items.AddRange(file.PaperSpace);

                foreach (DxfFileBlock block in file.Blocks.Values)
                {
                    items.AddRange(block.Items);
                }

                foreach (DxfItem item in items)
                {
                    foreach (DxfTypedValue group in item.Groups)
                    {
                        string text = group.Value;

                        if (text == null)
                        {
                            continue;
                        }

                        texts++;
                        bool entry = Numbers.TryRead(text, out double entered);
                        bool slow = Numbers.TryReadLong(text, out double expected);
                        numbers += slow ? 1 : 0;

                        if (
                            (
                                entry != slow
                                || BitConverter.DoubleToInt64Bits(entered) != BitConverter.DoubleToInt64Bits(expected)
                            )
                            && wrong.Count < 5
                        )
                        {
                            wrong.Add(System.IO.Path.GetFileName(path) + ": '" + text + "'");
                        }
                    }
                }
            }

            Assert.Empty(wrong);
            Assert.True(numbers > 0 || texts == 0);
        }

        [Fact]
        public void ANullText_IsNotANumber()
        {
            Assert.False(Numbers.TryRead(null, out double value));
            Assert.Equal(0.0, value);
        }
    }
}
