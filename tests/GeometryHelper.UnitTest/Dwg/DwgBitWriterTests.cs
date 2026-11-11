using System;
using System.Text;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The bit codes as the DWG writer writes them: each value written at every one of the eight bit offsets reads back
    /// through <see cref="DwgBitReader"/> to the same value, in the bits the specification gives, the shortest form first.
    /// </summary>
    public class DwgBitWriterTests
    {
        /// <summary>Writes with <paramref name="write"/> after <paramref name="offset"/> set bits, reads it back after them with <paramref name="read"/>, and checks the reader ends where the writer did.</summary>
        private static T RoundTrip<T>(int offset, Action<DwgBitWriter> write, Func<DwgBitReader, T> read)
        {
            var writer = new DwgBitWriter();

            for (int i = 0; i < offset; i++)
            {
                writer.Bit(true);
            }

            write(writer);
            long end = writer.Position;
            var reader = new DwgBitReader(writer.ToArray());
            reader.Position = offset;
            T value = read(reader);
            Assert.Equal(end, reader.Position);
            return value;
        }

        private static byte[] Bytes(Action<DwgBitWriter> write)
        {
            var writer = new DwgBitWriter();
            write(writer);
            return writer.ToArray();
        }

        [Fact]
        public void TheBitCodes_ReadBackToTheSameValue_AtEveryBitOffset()
        {
            for (int offset = 0; offset < 8; offset++)
            {
                foreach (int value in new[] { 0, 1, 255, 256, 257, 0x7FFF, -1, -32768 })
                {
                    Assert.Equal((short)value, (short)RoundTrip(offset, w => w.BitShort(value), r => r.BitShort()));
                }

                foreach (int value in new[] { 0, 1, 255, 256, -1, int.MaxValue, int.MinValue })
                {
                    Assert.Equal(value, RoundTrip(offset, w => w.BitLong(value), r => r.BitLong()));
                }

                foreach (long value in new[] { 0L, 1L, 0x100L, 0x1234567890L, 0x00FFFFFFFFFFFFFFL })
                {
                    Assert.Equal(value, RoundTrip(offset, w => w.BitLongLong(value), r => r.BitLongLong()));
                }

                foreach (double value in new[] { 0.0, 1.0, -1.0, 0.1, 1E300, double.Epsilon, -0.0 })
                {
                    double back = RoundTrip(offset, w => w.BitDouble(value), r => r.BitDouble());
                    Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(back));
                }

                foreach (int value in new[] { 0, 1, 0x7F, 0xFF })
                {
                    Assert.Equal(value, RoundTrip(offset, w => w.RawChar(value), r => r.RawChar()));
                }

                Assert.Equal(-12345, RoundTrip(offset, w => w.RawShort(-12345), r => r.RawShort()));
                Assert.Equal(0x12345678, RoundTrip(offset, w => w.RawLong(0x12345678), r => r.RawLong()));
                Assert.Equal(-2.75, RoundTrip(offset, w => w.RawDouble(-2.75), r => r.RawDouble()));

                foreach (int value in new[] { 0, 2, 6, 7 })
                {
                    Assert.Equal(value, RoundTrip(offset, w => w.Bits3(value), r => r.Bits3()));
                }
            }
        }

        [Fact]
        public void ModularCharsAndShorts_ReadBack_SignedAndNot()
        {
            foreach (
                long value in new[]
                {
                    0L,
                    1L,
                    63L,
                    64L,
                    127L,
                    128L,
                    4610L,
                    112823273L,
                    -1L,
                    -63L,
                    -64L,
                    -1000L,
                    -112823273L,
                }
            )
            {
                Assert.Equal(value, RoundTrip(3, w => w.ModularChar(value), r => r.ModularChar()));
            }

            foreach (long value in new[] { 0L, 127L, 128L, 0x3FFFL, 0xFFFFFFFFL })
            {
                Assert.Equal(value, RoundTrip(5, w => w.ModularChar(value, false), r => r.ModularChar(false)));
            }

            foreach (long value in new[] { 0L, 1L, 0x7FFFL, 0x8000L, 4650033L, 0x3FFFFFFFL })
            {
                Assert.Equal(value, RoundTrip(1, w => w.ModularShort(value), r => r.ModularShort()));
            }
        }

        [Fact]
        public void TheExamplesOfTheSpecification_AreWrittenInTheirBytes()
        {
            // The modular chars of the specification: 4610 is 0x82 0x24, 112823273 is 0xE9 0x97 0xE6 0x35, -1 is 0x41.
            Assert.Equal(new byte[] { 0x82, 0x24 }, Bytes(w => w.ModularChar(4610)));
            Assert.Equal(new byte[] { 0xE9, 0x97, 0xE6, 0x35 }, Bytes(w => w.ModularChar(112823273)));
            Assert.Equal(new byte[] { 0x41 }, Bytes(w => w.ModularChar(-1)));

            // The modular short 4650033 is 0x31 0xF4 0x8D 0x00.
            Assert.Equal(new byte[] { 0x31, 0xF4, 0x8D, 0x00 }, Bytes(w => w.ModularShort(4650033)));

            // A bit short of 0 is 10, of 256 is 11, of 15 is 01 00001111.
            Assert.Equal(new byte[] { 0b1000_0000 }, Bytes(w => w.BitShort(0)));
            Assert.Equal(new byte[] { 0b1100_0000 }, Bytes(w => w.BitShort(256)));
            Assert.Equal(new byte[] { 0b0100_0011, 0b1100_0000 }, Bytes(w => w.BitShort(15)));
        }

        [Fact]
        public void AHandle_TakesAsFewBytesAsHoldIt_HighByteFirst()
        {
            Assert.Equal(new byte[] { 0x50 }, Bytes(w => w.Handle(5, 0)));
            Assert.Equal(new byte[] { 0x31, 0x1F }, Bytes(w => w.Handle(3, 0x1F)));
            Assert.Equal(new byte[] { 0x42, 0x01, 0x02 }, Bytes(w => w.Handle(4, 0x102)));

            foreach (ulong value in new ulong[] { 0, 1, 0xFF, 0x100, 0x123456789AUL, ulong.MaxValue })
            {
                Assert.Equal(value, RoundTrip(6, w => w.Handle(2, value), r => r.Handle(0, out _)));
            }
        }

        [Fact]
        public void AHandleRelativeToItsObject_ReadsBackToTheSameHandle()
        {
            const ulong own = 0x2A0;

            foreach (ulong target in new ulong[] { 0, own + 1, own - 1, own + 0x55, own - 0x1FF, 1 })
            {
                Assert.Equal(target, RoundTrip(2, w => w.RelativeHandle(own, target), r => r.Handle(own)));
            }
        }

        [Fact]
        public void ABitDoubleWithADefault_TakesTheShortestFormThatHoldsIt()
        {
            const double given = 10.0;

            foreach (double value in new[] { 10.0, 10.000000001, 10.5, -3.25, 0.0, 12345.678 })
            {
                Assert.Equal(
                    value,
                    RoundTrip(4, w => w.BitDoubleDefault(value, given), r => r.BitDoubleDefault(given))
                );
            }

            Assert.Equal(new byte[] { 0x00 }, Bytes(w => w.BitDoubleDefault(given, given)));

            // 10.5 differs from 10 in its sixth and seventh bytes, so it takes the full form: 11 and eight bytes.
            Assert.Equal(1 + 8, Bytes(w => w.BitDoubleDefault(10.5, given)).Length);
        }

        [Fact]
        public void Texts_ReadBack_InTheCodePageAndInUtf16()
        {
            byte[] plain = Encoding.ASCII.GetBytes("Standard");
            Assert.Equal(plain, RoundTrip(3, w => w.Text(plain), r => r.Text()));
            Assert.Empty(RoundTrip(3, w => w.Text(new byte[0]), r => r.Text()));

            const string unicode = "Tiếng Việt 日本語";
            Assert.Equal(
                unicode,
                Encoding.UTF8.GetString(RoundTrip(7, w => w.TextUnicode(unicode), r => r.TextUnicode()))
            );
            Assert.Equal(
                unicode,
                Encoding.UTF8.GetString(RoundTrip(1, w => w.TextUnicodeRaw(unicode), r => r.TextUnicodeRaw()))
            );
            Assert.Empty(RoundTrip(0, w => w.TextUnicode(string.Empty), r => r.TextUnicode()));
        }

        [Fact]
        public void TheObjectType_ThicknessAndExtrusion_ReadBack()
        {
            foreach (int type in new[] { 0x13, 0xFF, 0x1F0, 0x1F2, 0x2EF, 0x2F0, 0x1F3 + 500 })
            {
                Assert.Equal(type, RoundTrip(5, w => w.ObjectType(type), r => r.ObjectType()));
            }

            Assert.Equal(0.0, RoundTrip(2, w => w.BitThickness(0.0), r => r.BitThickness()));
            Assert.Equal(2.5, RoundTrip(2, w => w.BitThickness(2.5), r => r.BitThickness()));
            Assert.Equal(
                new[] { 0.0, 0.0, 1.0 },
                RoundTrip(2, w => w.BitExtrusion(new[] { 0.0, 0.0, 1.0 }), r => r.BitExtrusion())
            );
            Assert.Equal(
                new[] { 0.6, 0.0, 0.8 },
                RoundTrip(2, w => w.BitExtrusion(new[] { 0.6, 0.0, 0.8 }), r => r.BitExtrusion())
            );
            Assert.Single(Bytes(w => w.BitExtrusion(new[] { 0.0, 0.0, 1.0 })));
        }

        [Fact]
        public void AWriterAppendedToAnother_KeepsItsBits()
        {
            var tail = new DwgBitWriter();
            tail.BitShort(300);
            tail.Bit(true);
            tail.Handle(5, 0x1A2);

            var writer = new DwgBitWriter();
            writer.Bits2(1);
            writer.Bit(true);
            writer.Append(tail);
            Assert.Equal(3 + tail.Position, writer.Position);

            var reader = new DwgBitReader(writer.ToArray());
            Assert.Equal(1, reader.Bits2());
            Assert.True(reader.Bit());
            Assert.Equal(300, reader.BitShort());
            Assert.True(reader.Bit());
            Assert.Equal(0x1A2UL, reader.Handle(0, out int code));
            Assert.Equal(5, code);
        }
    }
}
