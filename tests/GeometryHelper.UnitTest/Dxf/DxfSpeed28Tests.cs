using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed19 in the core: the objects of a DWG decoded on several threads, the numbers that are not whole
    /// written as bytes in whole-number arithmetic on every runtime, and the handles written kept in a table made once.
    /// Each is held to what it was: the DXF a DWG is made into is the same bytes whatever the threads, and a number is the text
    /// the older code gave it under .NET Core, which is now the text on .NET Framework too.
    /// </summary>
    public class DxfSpeed28Tests
    {
        /// <summary>
        /// A number as DXF text as .NET Core made it before speed18 (15 digits tried back through a parse, else 17), worked out
        /// in integers so that it is the same under .NET Framework, whose own formatting and parse are a little off.
        /// </summary>
        private static string FormerNumber(double value) => DxfRuntime1Tests.ReferenceNumber(value);

        private static readonly double[] Tens =
        {
            1E0,
            1E1,
            1E2,
            1E3,
            1E4,
            1E5,
            1E6,
            1E7,
            1E8,
            1E9,
            1E10,
            1E11,
            1E12,
            1E13,
            1E14,
            1E15,
            1E16,
            1E17,
            1E18,
        };

        private static double Neighbour(double value, int ulps) =>
            BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + ulps);

        /// <summary>Doubles of the kinds a drawing holds, from the ones typed in to the ones worked out.</summary>
        private static double Value(Random random, int kind)
        {
            switch (kind)
            {
                case 0:
                    // The nearest double of a decimal of 1 to 15 digits: a coordinate as it was typed.
                    return (long)(random.NextDouble() * Tens[random.Next(1, 16)] + 1) / Tens[random.Next(0, 19)];
                case 1:
                    return Math.Round(random.NextDouble() * Tens[random.Next(0, 9)], random.Next(1, 7));
                case 2:
                    return Math.Exp((random.NextDouble() * 23.0) - 9.3);
                case 3:
                    return random.Next(1, 360) * Math.PI / 180.0;
                case 4:
                    return Neighbour(random.Next(1, 1 << 20) / Tens[random.Next(0, 7)], random.Next(-2, 3));
                case 5:
                    // Dyadic fractions, whose texts are exact and often half way between two of 15 or 17 digits.
                    return random.Next(1, 1 << 20) / Math.Pow(2.0, random.Next(0, 40));
                case 6:
                    return Neighbour(Math.Pow(10.0, random.Next(-5, 15)), random.Next(-4, 5));
                case 7:
                    return BitConverter.Int64BitsToDouble(
                        ((long)random.Next(1009, 1070) << 52) | (((long)random.Next() << 21) ^ random.Next())
                    );
                default:
                    return Math.Sqrt(Math.Round(random.NextDouble() * 1000.0, 3) + Math.Round(random.NextDouble(), 3));
            }
        }

        private static string Written(IEnumerable<double> values, out string expected)
        {
            var writer = new DwgDxfWriter(1 << 20);
            var text = new StringBuilder();

            foreach (double value in values)
            {
                writer.Write(10, value);
                text.Append("10\r\n").Append(FormerNumber(value)).Append("\r\n");
            }

            expected = text.ToString();
            return Encoding.ASCII.GetString(writer.ToArray());
        }

        [Fact]
        public void TwoMillionNumbersOfNineKinds_AreWrittenAsTheOlderCodeWroteThem()
        {
            // The older code on .NET Framework gave 17 digits to about one in a thousand of the nearest doubles of a decimal,
            // the parse of its own 15 digits not being exact; the text of .NET Core comes out here under every runtime.
            for (int kind = 0; kind < 9; kind++)
            {
                var random = new Random(2800 + kind);
                var values = new List<double>();

                for (int i = 0; i < 220_000; i++)
                {
                    double value = Value(random, kind);

                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        continue;
                    }

                    values.Add(random.Next(0, 2) == 0 ? value : -value);
                }

                string got = Written(values, out string expected);

                if (expected != got)
                {
                    string[] want = expected.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] have = got.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    int at = 0;

                    while (want[at] == have[at])
                    {
                        at++;
                    }

                    Assert.Fail(
                        "kind "
                            + kind
                            + ": the number "
                            + BitConverter.DoubleToInt64Bits(values[at / 2]).ToString("X16")
                            + " is "
                            + have[at]
                            + " where it was "
                            + want[at]
                    );
                }
            }
        }

        [Fact]
        public void DoublesThatDotNetFrameworkReadsOneOff_AreWrittenAsNetCoreWroteThem()
        {
            // Each is the nearest double of a decimal of 6 to 15 digits whose 15 digits are within a thousandth of half the
            // distance to its neighbour either way, where .NET Framework's parse is a unit off in the last of 64 bits. Where
            // an exact parse reads the text of the 15 digits as the number, they are written, and where not, 17.
            long[] bits =
            {
                unchecked((long)0xBFEFFE2E6EA85447),
                0x407F238E86402081,
                0x418D59925BE391D5,
                unchecked((long)0xC0896BA82949A565),
                0x4023478811B1D92B,
                0x3FCAF6E82949A565,
                unchecked((long)0xC124F7502ADC6E2B),
                0x40696ED805E5F30E,
                0x414F932827D2E947,
                unchecked((long)0xC1057031D157ABB9),
                0x404BFB8811B1D92B,
                0x408D2C92B89464F9,
                0x3FEF5B2B3461309C,
                0x407917682949A565,
                0x4051158811B1D92B,
            };

            string got = Written(bits.Select(BitConverter.Int64BitsToDouble), out string expected);

            Assert.Equal(expected, got);
        }

        [Fact]
        public void NumbersAtTheBoundsAndAtHalfWay_AreWrittenAsTheOlderCodeWroteThem()
        {
            var values = new List<double>();

            // The bounds of the bytes' own arithmetic, 0.0001 and 1E14, and a few ulps either side of each.
            foreach (double bound in new[] { 1E-4, 1E14, 1E-3, 1E13, 0.5, 0.25, 0.125, 1.5, 2.5, 0.1, 0.01 })
            {
                for (int ulps = -4; ulps <= 4; ulps++)
                {
                    values.Add(Neighbour(bound, ulps));
                    values.Add(-Neighbour(bound, ulps));
                }
            }

            // Numbers whose text of 15 digits ends in a 5 and no more, half way between two, and numbers that round up to a
            // power of ten at 15 digits.
            values.AddRange(
                new[]
                {
                    0.000244140625,
                    9.5367431640625E-07 * 4096.0,
                    1.52587890625E-05 * 64.0,
                    99999999999999.984,
                    99999999999999.5,
                    9999999999999.99,
                    0.99999999999999989,
                    0.099999999999999992,
                    0.0099999999999999985,
                    1234567.123456789,
                    0.1 + 0.2,
                    1.0 / 3.0,
                    2.0 / 3.0,
                    28.64788975654116,
                    57.29577951308232,
                    Math.PI,
                    Math.E * 1000.0,
                }
            );

            string got = Written(values, out string expected);

            Assert.Equal(expected, got);
        }

        [Fact]
        public void TwentyThousandNumbersThatAreNotWhole_AreWrittenWithNoStringMadeForEach()
        {
            // A string for each (the 15 digits, their parse, then the 17 where those were short, a 24-byte string at the least
            // each) took some 150 bytes a number under .NET Framework; the digits are now made in whole numbers and put in the array.
            var random = new Random(2828);
            var values = new double[20_000];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = random.Next(1, 100_000_000) / 1000.0 + 0.0005;
            }

            var writer = new DwgDxfWriter(values.Length * 24);

            foreach (double value in values.Take(100))
            {
                writer.Write(10, value);
            }

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            foreach (double value in values)
            {
                writer.Write(10, value);
            }

            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / values.Length;

            Assert.True(used < 40, "allocated " + used + " bytes a number");
        }

        #region The objects decoded on threads

        /// <summary>A DWG of that many entities of seven kinds on three layers, as a drawing written by the library.</summary>
        private static byte[] OneDwg(int count)
        {
            var writer = new DxfWriter();

            for (int i = 0; i < count; i++)
            {
                double x = i % 100 * 30.0 + 0.125;
                double y = i / 100 * 30.0 + 0.0625;
                string layer = "L" + (i % 3).ToString(CultureInfo.InvariantCulture);

                switch (i % 7)
                {
                    case 0:
                        writer.Add(new GeoLine2(new GeoPoint2(x, y), new GeoPoint2(x + 20.3, y + 12.7)), layer);
                        break;
                    case 1:
                        writer.Add(new GeoCircle2(new GeoPoint2(x, y), 5.5), layer);
                        break;
                    case 2:
                        writer.Add(new GeoArc2(new GeoPoint2(x, y), 6.25, 0.2, 3.5), layer);
                        break;
                    case 3:
                        writer.Add(
                            new GeoPolygon2(
                                new GeoPoint2(x, y),
                                new GeoPoint2(x + 20.0, y),
                                new GeoPoint2(x + 20.0, y + 20.0),
                                new GeoPoint2(x, y + 20.0)
                            ),
                            layer
                        );
                        break;
                    case 4:
                        writer.Add(
                            new GeoPolyline2(
                                new GeoPoint2(x, y),
                                new GeoPoint2(x + 20.0, y + 5.0),
                                new GeoPoint2(x + 30.0, y)
                            ),
                            layer
                        );
                        break;
                    case 5:
                        writer.Add(
                            new DxfText(
                                new GeoPoint3(x, y, 0.0),
                                new GeoVector3(0.0, 0.0, 1.0),
                                2.5,
                                0.0,
                                "T" + i.ToString(CultureInfo.InvariantCulture)
                            ),
                            layer: layer
                        );
                        break;
                    default:
                        writer.Add(new GeoPoint2(x, y), layer);
                        break;
                }
            }

            return DwgWriter.ToDwg(DxfDatabase.Parse(writer.ToString()).ToText(), DwgWriter.DefaultVersion);
        }

        private static byte[] WithThreads(int threads, Func<byte[]> make)
        {
            int before = DwgTranscoder.DecodeThreads;
            DwgTranscoder.DecodeThreads = threads;

            try
            {
                return make();
            }
            finally
            {
                DwgTranscoder.DecodeThreads = before;
            }
        }

        [Fact]
        public void ADwgOfSixThousandObjects_DecodedOnOneTwoFourAndEightThreads_IsTheSameDxfByteForByte()
        {
            byte[] file = OneDwg(6_000);

            byte[] one = WithThreads(1, () => DwgReader.ToDxf(file));

            Assert.True(one.Length > 6_000 * 80);

            foreach (int threads in new[] { 2, 4, 8, 0 })
            {
                Assert.Equal(one, WithThreads(threads, () => DwgReader.ToDxf(file)));
            }
        }

        [Fact]
        public void ADwgOfAHundredObjects_DecodedOnEightThreadsAsked_IsTheSameDxfByteForByte()
        {
            byte[] file = OneDwg(100);

            Assert.Equal(WithThreads(1, () => DwgReader.ToDxf(file)), WithThreads(8, () => DwgReader.ToDxf(file)));
        }

        [Fact]
        public void BrokenObjectsAmongThousands_AreLeftOutAndCountedAsOneThreadDoes_TheFirstTold_BeingTheFirstOfTheMap()
        {
            byte[] file = OneDwg(6_000);
            DwgContainer container = DwgContainer.Read(file);

            // A byte of some objects changed: their check sum is wrong, and each is refused as one broken object.
            var random = new Random(2829);
            int changed = 0;

            for (int i = 100; i < container.ObjectMap.Count; i += 151 + random.Next(0, 60))
            {
                container.Objects[container.ObjectMap[i].Value + 4] ^= 0x55;
                changed++;
            }

            Assert.True(changed > 30);

            var written = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (message.StartsWith("DwgReader: ", StringComparison.Ordinal) && message.Contains("broken objects"))
                {
                    lock (written)
                    {
                        written.Add(message);
                    }
                }
            };

            try
            {
                byte[] one = WithThreads(1, () => DwgTranscoder.ToDxf(container));
                string[] sequential = written.ToArray();
                written.Clear();
                byte[] many = WithThreads(8, () => DwgTranscoder.ToDxf(container));

                Assert.Equal(one, many);
                Assert.Single(sequential);
                Assert.Contains(changed.ToString(CultureInfo.InvariantCulture) + " broken objects", sequential[0]);
                Assert.Equal(sequential, written.ToArray());
            }
            finally
            {
                GeometryHelperLog.Writer = null;
                GeometryHelperLog.Enable = enabled;
            }
        }

        [Fact]
        public void ATokenStoppedWhileObjectsAreDecodedOnEightThreads_EndsTheReadingWithAnOperationCanceledException()
        {
            byte[] file = OneDwg(12_000);
            var source = new CancellationTokenSource();

            // Stopped from the share told: the reading of the objects is the share from 0.05 to 0.6.
            var steps = ProgressSteps.Of(
                new SynchronousProgress(share =>
                {
                    if (share > 0.2)
                    {
                        source.Cancel();
                    }
                })
            );

            DwgTranscoder.DecodeThreads = 8;

            try
            {
                Assert.Throws<OperationCanceledException>(() => DwgReader.ToDxf(file, null, steps, source.Token));
            }
            finally
            {
                DwgTranscoder.DecodeThreads = 0;
            }
        }

        private sealed class SynchronousProgress : IProgress<double>
        {
            private readonly Action<double> _told;

            internal SynchronousProgress(Action<double> told) => _told = told;

            public void Report(double value) => _told(value);
        }

        #endregion

        #region The handles written

        [Fact]
        public void TheSetOfHandlesWritten_HoldsWhatAHashSetHolds_OfEveryKindOfHandle()
        {
            var random = new Random(2830);

            // Made for a few, given a hundred thousand, so that it doubles through each size on the way.
            var set = new DwgHandleSet(10);
            var reference = new HashSet<ulong>();

            for (int i = 0; i < 100_000; i++)
            {
                ulong handle;

                switch (random.Next(0, 5))
                {
                    case 0:
                        handle = (ulong)random.Next(0, 20_000);
                        break;
                    case 1:
                        handle = (ulong)i + 0x1000;
                        break;
                    case 2:
                        handle = ((ulong)random.Next() << 32) | (uint)random.Next();
                        break;
                    case 3:
                        handle = (ulong)random.Next(0, 64) << 40;
                        break;
                    default:
                        handle = ulong.MaxValue - (ulong)random.Next(0, 100);
                        break;
                }

                Assert.Equal(reference.Add(handle), set.Add(handle));
                Assert.Equal(reference.Count, set.Count);
                Assert.Equal(reference.Contains(handle + 1), set.Contains(handle + 1));
            }

            foreach (ulong handle in reference)
            {
                Assert.True(set.Contains(handle));
            }

            Assert.Contains(0UL, reference);
            Assert.True(set.Contains(0UL));
            Assert.False(set.Contains(0xFFFFFFFFFFUL << 8));
        }

        [Fact]
        public void TheSetOfHandlesWritten_MadeForThreeHundredThousand_IsMadeOnceAndNotGrownByAdding()
        {
            // A HashSet grown by adding makes an array of each size on the way, 20 MB of arrays for 300 000 handles, the larger
            // of them large objects; the table made for the count makes one of 8 MB.
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var set = new DwgHandleSet(300_000);

            for (ulong handle = 1; handle <= 300_000; handle++)
            {
                set.Add(handle * 3);
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(300_000, set.Count);
            Assert.True(used < 10_000_000, "allocated " + used + " bytes");
        }

        #endregion
    }
}
