using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The number text of the DXF a DWG is turned into, and of the DXF a drawing is saved as, the same on .NET Framework 4.8 as on
    /// .NET 8 and .NET 10. Framework prints the 16 and 17 digits of a double from digits it rounded once already, sends a number
    /// half way between two texts up where .NET Core 3.0 and later send it to the even digit, parses a text a unit off in the
    /// last bit one time in a thousand, and leaves the sign off a negative nought: 54 of 186 DWGs gave another DXF text there.
    /// The expected texts are those .NET 8 gave (and gives); the reference in the tests of the whole range is worked out in
    /// integers of any size, which no runtime's own formatting is used for.
    /// </summary>
    public class DxfRuntime1Tests
    {
        private static double FromBits(long bits) => BitConverter.Int64BitsToDouble(bits);

        private static string Written(double value)
        {
            var writer = new DwgDxfWriter();
            writer.Write(10, value);
            string text = Encoding.ASCII.GetString(writer.ToArray());
            Assert.StartsWith("10\r\n", text, StringComparison.Ordinal);
            return text.Substring(4, text.Length - 6);
        }

        [Fact]
        public void ANumberHalfWayBetweenTwoTextsOf17Digits_GoesToTheEvenDigit()
        {
            // 6.9212722778320312|5 is a double exactly (a float's): .NET Core sends it to the even 2, .NET Framework sent it up.
            (long Bits, string Text)[] cases =
            {
                (0x401BAF6200000000, "6.9212722778320312"),
                (unchecked((long)0xC07228D6A0000000), "-290.55239868164062"),
                (0x408F79AD20000000, "1007.2095336914062"),
                (unchecked((long)0xC15800683E280000), "-6291872.9711914062"),
                (unchecked((long)0xC05373E240000000), "-77.810684204101562"),
                (0x4053467E80000000, "77.101470947265625"),
                (0x4300000000000001, "562949953421312.12"),
            };

            foreach ((long bits, string text) in cases)
            {
                Assert.Equal(text, DwgDxfWriter.Number(FromBits(bits)));
                Assert.Equal(text, Written(FromBits(bits)));
            }
        }

        [Fact]
        public void The15DigitsThatReadBack_AreWritten_WhereTheFrameworkParseSaidTheyDidNot()
        {
            (long Bits, string Text)[] cases =
            {
                (0x40A6DD7686402081, "2926.731493"),
                (unchecked((long)0xC035178D4FDF3B80), "-21.0920000000001"),
                (0x403BD78D4FDF3B80, "27.8420000000001"),
                (0x3F9F464C0C4D2AE9, "0.0305415995128823"),
                (unchecked((long)0xBFEE0898ABED6E9B), "-0.938549361987913"),
            };

            foreach ((long bits, string text) in cases)
            {
                Assert.Equal(text, DwgDxfWriter.Number(FromBits(bits)));
                Assert.Equal(text, Written(FromBits(bits)));
            }
        }

        [Fact]
        public void The17DigitsAreWritten_WhereTheFrameworkParseSaidTheFifteenReadBack()
        {
            // Framework wrote -0.0688976377952756, which is the double next to this one.
            (long Bits, string Text)[] cases =
            {
                (unchecked((long)0xBFB1A3468D1A346A), "-0.068897637795275607"),
                (0x40577F02A5998E4E, "93.984536552388107"),
            };

            foreach ((long bits, string text) in cases)
            {
                Assert.Equal(text, DwgDxfWriter.Number(FromBits(bits)));
                Assert.Equal(text, Written(FromBits(bits)));
            }
        }

        [Fact]
        public void ANegativeNought_IsWrittenWithItsSign_AsNetCoreWritesIt()
        {
            double nought = FromBits(unchecked((long)0x8000000000000000));

            Assert.Equal("-0.0", DwgDxfWriter.Number(nought));
            Assert.Equal("-0.0", Written(nought));
            Assert.Equal("0.0", Written(0.0));
            Assert.Equal("-0", Numbers.Exact(nought));
        }

        [Fact]
        public void TheNumbersOutOfTheWay_AreWrittenAsNetCoreWritesThem()
        {
            (long Bits, string Text)[] cases =
            {
                (0x3CA1A62633145C07, "1.2246467991473532E-16"),
                (0x3C91A62633145C07, "6.123233995736766E-17"),
                (1, "4.94065645841247E-324"),
                (0x3EE4F8B588E368F1, "1E-05"),
                (0x3EEF75104D551D69, "1.5E-05"),
                (0x3F1A36E2EB1C432D, "0.0001"),
                (0x42D6BCC41E900000, "100000000000000.0"),
                (0x42D6BCC41E900020, "100000000000000.5"),
                (0x430C6BF526340000, "1E+15"),
                (0x7E41EB2D66005835, "1.5E+300"),
                (unchecked((long)0xBE25798EE2308C3A), "-2.5E-09"),
                (0x4341C37937E08000, "1E+16"),
                (0x43E56A95319D63E1, "1.2345678901234567E+19"),
            };

            foreach ((long bits, string text) in cases)
            {
                Assert.Equal(text, DwgDxfWriter.Number(FromBits(bits)));
                Assert.Equal(text, Written(FromBits(bits)));
            }
        }

        [Fact]
        public void ThePrintingOfTheDrawingsNumbers_IsTheSameAsNetCoreMakesIt()
        {
            // The shortest of the 15, 16 and 17 digits that read back, as the drawings are saved with.
            (long Bits, string Text)[] cases =
            {
                (0x401BAF6200000000, "6.921272277832031"),
                (unchecked((long)0xC318BE610EFA4AE9), "-1741180878361274.2"),
                (0x416241E27600912B, "9572115.687569221"),
                (0x4120BECC9CD9D046, "548710.3063492856"),
                (0x42E06353E00E27D4, "144150359994686.62"),
                (0x40850F02D91F5707, "673.8763906906541"),
                (unchecked((long)0xC05373E240000000), "-77.81068420410156"),
                (unchecked((long)0xBFB1A3468D1A346A), "-0.06889763779527561"),
                (0x4300000000000001, "562949953421312.1"),
                (1, "5E-324"),
            };

            foreach ((long bits, string text) in cases)
            {
                Assert.Equal(text, Numbers.Exact(FromBits(bits)));
            }
        }

        [Fact]
        public void ADrawingSaved_HasTheNumbersAsNetCorePrintsThem()
        {
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().ToString());
            db.ModelSpace.AppendEntity(
                new DxfLine(
                    new GeoPoint3(FromBits(0x401BAF6200000000), FromBits(unchecked((long)0xC318BE610EFA4AE9)), 0.0),
                    new GeoPoint3(FromBits(0x416241E27600912B), FromBits(0x4120BECC9CD9D046), 0.0)
                )
            );

            string text;

            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                text = Encoding.UTF8.GetString(stream.ToArray());
            }

            foreach (
                string number in new[]
                {
                    "6.921272277832031",
                    "-1741180878361274.2",
                    "9572115.687569221",
                    "548710.3063492856",
                }
            )
            {
                Assert.Contains("\n" + number + "\n", text.Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void ADecimalOf6To15Digits_IsReadByTheDwgWriterAsTheDoubleNearestIt()
        {
            // .NET Framework's parse reads -0.999778 as the double above this one, and 498.222296 as the one above.
            Assert.Equal(0xBFEFFE2E6EA85447UL, (ulong)BitConverter.DoubleToInt64Bits(DwgRecord.Double("-0.999778")));
            Assert.Equal(0x407F238E86402081UL, (ulong)BitConverter.DoubleToInt64Bits(DwgRecord.Double(" 498.222296 ")));
            Assert.Equal(12.5, DwgRecord.Double("1.25E1"));
            Assert.Equal(0.0, DwgRecord.Double("broken"));
        }

        [Fact]
        public void TheNumbersTheDxfOfADwgHolds_ReadBackAsTheDoublesTheyWereWrittenFrom()
        {
            _state = 0xD1B54A32D192ED03UL;

            for (int kind = 0; kind < 11; kind++)
            {
                for (int i = 0; i < 5_000; i++)
                {
                    double value = Value(kind);

                    if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) >= 1.797693134862315E+308)
                    {
                        continue;
                    }

                    double back = DwgRecord.Double(Written(value));

                    Assert.True(
                        BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(back),
                        "kind "
                            + kind
                            + " "
                            + Written(value)
                            + " reads as "
                            + BitConverter.DoubleToInt64Bits(back).ToString("X16", CultureInfo.InvariantCulture)
                    );
                }
            }
        }

        #region The reference, in integers of any size

        /// <summary>The first <paramref name="digits"/> digits of the size of a number, correctly rounded, a half to the even digit.</summary>
        private static BigInteger Digits(double value, int digits, out int ten)
        {
            long bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
            int biased = (int)(bits >> 52);
            BigInteger mantissa = (bits & 0xFFFFFFFFFFFFFL) | (biased == 0 ? 0L : 1L << 52);
            int two = biased == 0 ? -1074 : biased - 1075;

            ten = (int)Math.Floor(Math.Log10(Math.Abs(value)));

            while (true)
            {
                int scale = digits - 1 - ten;
                BigInteger top = two >= 0 ? mantissa << two : mantissa;
                BigInteger bottom = two >= 0 ? BigInteger.One : BigInteger.One << -two;

                if (scale >= 0)
                {
                    top *= BigInteger.Pow(10, scale);
                }
                else
                {
                    bottom *= BigInteger.Pow(10, -scale);
                }

                BigInteger quotient = BigInteger.DivRem(top, bottom, out BigInteger remainder);
                BigInteger low = BigInteger.Pow(10, digits - 1);

                if (quotient < low)
                {
                    ten--;
                    continue;
                }

                if (quotient >= low * 10)
                {
                    ten++;
                    continue;
                }

                int half = (remainder * 2).CompareTo(bottom);

                if (half > 0 || (half == 0 && !quotient.IsEven))
                {
                    quotient += 1;
                }

                if (quotient >= low * 10)
                {
                    quotient /= 10;
                    ten++;
                }

                return quotient;
            }
        }

        /// <summary>Whether digits times ten to the power of <paramref name="power"/> are read as the number: nearer to it than to its neighbours, a tie going to the even one.</summary>
        private static bool ReadsBack(BigInteger digits, int power, double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
            int biased = (int)(bits >> 52);
            BigInteger mantissa = (bits & 0xFFFFFFFFFFFFFL) | (biased == 0 ? 0L : 1L << 52);
            int two = biased == 0 ? -1074 : biased - 1075;
            bool halvedBelow = (bits & 0xFFFFFFFFFFFFFL) == 0 && biased > 1;

            // Everything times 10 to the up and 2 to the left, which make every number whole.
            int up = power < 0 ? -power : 0;
            int left = two < 2 ? 2 - two : 0;
            BigInteger unit = BigInteger.Pow(10, up);
            BigInteger text = (digits * BigInteger.Pow(10, power + up)) << left;
            BigInteger number = (mantissa << (two + left)) * unit;
            BigInteger gap = text - number;
            BigInteger above = unit << (two - 1 + left);
            BigInteger below = halvedBelow ? unit << (two - 2 + left) : above;
            int order = gap.Sign >= 0 ? gap.CompareTo(above) : BigInteger.Negate(gap).CompareTo(below);

            return order < 0 || (order == 0 && mantissa.IsEven);
        }

        /// <summary>The text .NET Core 3.0 and later give <c>ToString("G" + digits)</c>.</summary>
        private static string General(double value, int digits)
        {
            BigInteger number = Digits(value, digits, out int ten);
            string all = number.ToString(CultureInfo.InvariantCulture).TrimEnd('0');

            if (all.Length == 0)
            {
                all = "0";
            }

            int point = ten + 1;
            var text = new StringBuilder(value < 0 ? "-" : "");

            if (point > digits || point < -3)
            {
                text.Append(all[0]);

                if (all.Length > 1)
                {
                    text.Append('.').Append(all, 1, all.Length - 1);
                }

                text.Append('E')
                    .Append(point - 1 < 0 ? '-' : '+')
                    .Append(Math.Abs(point - 1).ToString("00", CultureInfo.InvariantCulture));
            }
            else if (point <= 0)
            {
                text.Append("0.").Append('0', -point).Append(all);
            }
            else if (all.Length <= point)
            {
                text.Append(all).Append('0', point - all.Length);
            }
            else
            {
                text.Append(all, 0, point).Append('.').Append(all, point, all.Length - point);
            }

            return text.ToString();
        }

        private static bool Fits(double value, int digits)
        {
            BigInteger number = Digits(value, digits, out int ten);
            return ReadsBack(number, ten - digits + 1, value);
        }

        /// <summary>The text of <see cref="DwgDxfWriter.Number"/>: 15 digits where they read back, else 17.</summary>
        internal static string ReferenceNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "0.0";
            }

            if (Math.Abs(value) >= 1.797693134862315E+308)
            {
                return value < 0 ? "-1.797693134862314E+99" : "1.797693134862314E+99";
            }

            if (value == Math.Floor(value) && Math.Abs(value) < 1E15)
            {
                return (BitConverter.DoubleToInt64Bits(value) < 0 ? "-" : "")
                    + BigInteger.Abs(new BigInteger(value))
                    + ".0";
            }

            return General(value, Fits(value, 15) ? 15 : 17);
        }

        /// <summary>The text of <see cref="Numbers.Exact"/>: the shortest of 15, 16 and 17 digits that read back.</summary>
        private static string Exact(double value)
        {
            if (value == 0.0)
            {
                return BitConverter.DoubleToInt64Bits(value) < 0 ? "-0" : "0";
            }

            if (Math.Abs(value) < 2.2250738585072014E-308)
            {
                for (int digits = 1; digits < 15; digits++)
                {
                    if (Fits(value, digits))
                    {
                        return General(value, digits);
                    }
                }
            }

            if (Math.Abs(value) < 1E15 && value == Math.Floor(value))
            {
                return General(value, 15);
            }

            foreach (int digits in new[] { 15, 16 })
            {
                if (Fits(value, digits))
                {
                    return General(value, digits);
                }
            }

            return General(value, 17);
        }

        private static ulong _state;

        private static ulong Next()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        private static double Decimal(ulong digits, int places)
        {
            double divisor = 1.0;

            for (int i = 0; i < places; i++)
            {
                divisor *= 10.0;
            }

            return digits / divisor;
        }

        /// <summary>A number of one of eleven kinds, made in arithmetic that is exact on every runtime (no parse, no sine).</summary>
        private static double Value(int kind)
        {
            ulong random = Next();
            double value;

            switch (kind)
            {
                case 0:
                    value = FromBits((long)(((ulong)(1003 + (int)(Next() % 71)) << 52) | (random & 0xFFFFFFFFFFFFFUL)));
                    break;
                case 1:
                    value = Decimal(1 + (Next() % 999999999999999UL), (int)(random % 9));
                    break;
                case 2:
                    value = Decimal(Next() >> 11, (int)(random % 17));
                    break;
                case 3:
                    value = BitConverter.ToSingle(
                        BitConverter.GetBytes((uint)(((113 + (int)(Next() % 40)) << 23) | (int)(random & 0x7FFFFF))),
                        0
                    );
                    break;
                case 4:
                    value = (Next() >> (int)(11 + (Next() % 40))) / (double)(1UL << (int)(random % 45));
                    break;
                case 5:
                    value = (1 + (Next() % 100000000UL)) / 1000.0 * ((1 + (random % 100000UL)) / 100.0);
                    break;
                case 6:
                    value = FromBits(
                        BitConverter.DoubleToInt64Bits(Decimal(1, (int)(random % 12)) * (1UL << (int)(Next() % 40)))
                            + (long)(Next() % 7)
                            - 3
                    );
                    break;
                case 7:
                    value = FromBits((long)(((ulong)(1 + (int)(Next() % 2046)) << 52) | (random & 0xFFFFFFFFFFFFFUL)));
                    break;
                case 8:
                    value = FromBits(
                        BitConverter.DoubleToInt64Bits((random & 1) == 0 ? 1E-4 : 1E14)
                            + (long)(Next() % 400000000000000UL)
                            - 200000000000000L
                    );
                    break;
                case 9:
                    value = FromBits((long)(((ulong)(1063 + (int)(Next() % 20)) << 52) | (random & 0xFFFFFFFFFFFFFUL)));
                    break;
                default:
                    value = Decimal(1 + (Next() % 999999999999999UL), 6 + (int)(random % 18));
                    break;
            }

            return (Next() & 1) == 0 ? -value : value;
        }

        [Fact]
        public void TwoHundredThousandNumbersOfElevenKinds_AreTheTextTheReferenceGives_ByEveryWriter()
        {
            _state = 0x9E3779B97F4A7C15UL;

            for (int kind = 0; kind < 11; kind++)
            {
                for (int i = 0; i < 20_000; i++)
                {
                    double value = Value(kind);

                    if (double.IsNaN(value) || double.IsInfinity(value) || value == 0.0)
                    {
                        continue;
                    }

                    string bits = BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture);

                    Assert.True(
                        ReferenceNumber(value) == Written(value),
                        "kind " + kind + " " + bits + " written as " + Written(value) + " for " + ReferenceNumber(value)
                    );
                    Assert.True(
                        ReferenceNumber(value) == DwgDxfWriter.Number(value),
                        "kind "
                            + kind
                            + " "
                            + bits
                            + " Number "
                            + DwgDxfWriter.Number(value)
                            + " for "
                            + ReferenceNumber(value)
                    );
                    Assert.True(
                        Exact(value) == Numbers.Exact(value),
                        "kind " + kind + " " + bits + " Exact " + Numbers.Exact(value) + " for " + Exact(value)
                    );
                }
            }
        }

        [Fact]
        public void TheDigitsOfEveryLengthFromOneTo17_AreThoseNetCoreGivesToString()
        {
            _state = 0x2545F4914F6CDD1DUL;

            for (int kind = 0; kind < 11; kind++)
            {
                for (int i = 0; i < 2_000; i++)
                {
                    double value = Value(kind);

                    if (double.IsNaN(value) || double.IsInfinity(value) || value == 0.0)
                    {
                        continue;
                    }

                    for (int digits = 1; digits <= 17; digits++)
                    {
                        string want = General(value, digits);
                        string got = ExactDigits.General(value, digits);

                        Assert.True(
                            want == got,
                            "kind "
                                + kind
                                + " "
                                + BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture)
                                + " at "
                                + digits
                                + " digits is "
                                + got
                                + " for "
                                + want
                        );
                    }
                }
            }
        }

        [Fact]
        public void TheEdgesOfTheRange_AreTheTextTheReferenceGives()
        {
            var values = new List<double>();

            foreach (
                double bound in new[]
                {
                    1E-4,
                    1E-3,
                    1E-5,
                    1E-6,
                    1E-11,
                    1E-12,
                    1E13,
                    1E14,
                    1E15,
                    0.5,
                    0.25,
                    0.125,
                    1.5,
                    2.5,
                    1.0 / 3.0,
                    2.0 / 3.0,
                }
            )
            {
                for (int ulps = -4; ulps <= 4; ulps++)
                {
                    long bits = BitConverter.DoubleToInt64Bits(bound) + ulps;
                    values.Add(FromBits(bits));
                    values.Add(-FromBits(bits));
                }
            }

            values.AddRange(
                new[]
                {
                    double.Epsilon,
                    -double.Epsilon,
                    2.2250738585072014E-308,
                    1.7976931348623157E+308 / 3.0,
                    Math.PI,
                    Math.E * 1000.0,
                }
            );

            foreach (double value in values)
            {
                Assert.Equal(ReferenceNumber(value), DwgDxfWriter.Number(value));
                Assert.Equal(ReferenceNumber(value), Written(value));
                Assert.Equal(Exact(value), Numbers.Exact(value));
            }
        }

        #endregion
    }
}
