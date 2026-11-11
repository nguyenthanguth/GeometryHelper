using System;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The bit codes of a DWG file, each read from bytes worked out by hand; the modular chars and the modular short are
    /// the examples of the Open Design Specification for .dwg files.
    /// </summary>
    public class DwgBitReaderTests
    {
        private static DwgBitReader Of(params byte[] bytes) => new DwgBitReader(bytes);

        [Fact]
        public void Bits_AreReadFromTheHighBitOfEachByteDown()
        {
            DwgBitReader reader = Of(0b1010_0000, 0b1100_0000);

            Assert.True(reader.Bit());
            Assert.False(reader.Bit());
            Assert.Equal(2, reader.Bits2());
            Assert.Equal(4, reader.Position);
            Assert.Equal(0x0Cu, reader.Bits(8));
            Assert.Equal(4, reader.Remaining);
        }

        [Fact]
        public void ARawChar_OffTheByteBoundary_TakesItsBitsFromTwoBytes()
        {
            DwgBitReader reader = Of(0b0001_0010, 0b0011_0100);
            reader.Position = 4;

            Assert.Equal(0x23, reader.RawChar());
        }

        [Fact]
        public void RawNumbers_AreLowByteFirst()
        {
            Assert.Equal(0x1234, Of(0x34, 0x12).RawShort());
            Assert.Equal(-2, Of(0xFE, 0xFF).RawShort());
            Assert.Equal(0x12345678, Of(0x78, 0x56, 0x34, 0x12).RawLong());
            Assert.Equal(-1, Of(0xFF, 0xFF, 0xFF, 0xFF).RawLong());
            Assert.Equal(2.5, Of(BitConverter.GetBytes(2.5)).RawDouble());
        }

        [Fact]
        public void ABitShort_IsARawShort_OneByte_Zero_Or256_ByItsFirstTwoBits()
        {
            // 00, then 0x0201 low byte first: 00 00000001 00000010.
            Assert.Equal(0x0201, Of(0b0000_0000, 0b0100_0000, 0b1000_0000).BitShort());

            // 01, then the byte 0x0F: 01 00001111.
            Assert.Equal(15, Of(0b0100_0011, 0b1100_0000).BitShort());
            Assert.Equal(0, Of(0b1000_0000).BitShort());
            Assert.Equal(256, Of(0b1100_0000).BitShort());
        }

        [Fact]
        public void ABitLong_IsARawLong_OneByte_OrZero_ByItsFirstTwoBits()
        {
            // 00, then 0x00010000 low byte first.
            Assert.Equal(0x10000, Of(0b0000_0000, 0b0000_0000, 0b0000_0000, 0b0100_0000, 0b0000_0000).BitLong());

            // 01, then the byte 0xFF.
            Assert.Equal(255, Of(0b0111_1111, 0b1100_0000).BitLong());
            Assert.Equal(0, Of(0b1000_0000).BitLong());
            Assert.Throws<FormatException>(() => Of(0b1100_0000).BitLong());
        }

        [Fact]
        public void ABitDouble_IsARawDouble_One_OrZero_ByItsFirstTwoBits()
        {
            // 00, then the eight bytes of 2.0 (00 00 00 00 00 00 00 40), moved two bits along.
            Assert.Equal(2.0, Of(0, 0, 0, 0, 0, 0, 0, 0b0001_0000, 0).BitDouble());
            Assert.Equal(1.0, Of(0b0100_0000).BitDouble());
            Assert.Equal(0.0, Of(0b1000_0000).BitDouble());
            Assert.Throws<FormatException>(() => Of(0b1100_0000).BitDouble());
        }

        [Fact]
        public void ABitDoubleWithADefault_PatchesTheBytesOfTheDefault()
        {
            double value = BitConverter.Int64BitsToDouble(0x4059_0000_0000_0000);

            // 00: the default as it is.
            Assert.Equal(value, Of(0b0000_0000).BitDoubleDefault(value));

            // 01, then four bytes 78 56 34 12 that become the low four of the eight.
            Assert.Equal(
                0x4059_0000_1234_5678,
                BitConverter.DoubleToInt64Bits(Shifted(0b01, 0x78, 0x56, 0x34, 0x12).BitDoubleDefault(value))
            );

            // 10, then six bytes: BC 9A become the fifth and sixth, then 78 56 34 12 the low four.
            Assert.Equal(
                0x4059_9ABC_1234_5678,
                BitConverter.DoubleToInt64Bits(
                    Shifted(0b10, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12).BitDoubleDefault(value)
                )
            );

            // 11: a whole raw double.
            Assert.Equal(-7.25, Shifted(0b11, BitConverter.GetBytes(-7.25)).BitDoubleDefault(value));
        }

        /// <summary>A reader over two bits and then whole bytes, which so lie two bits off the byte boundary.</summary>
        private static DwgBitReader Shifted(int twoBits, params byte[] bytes)
        {
            var data = new byte[bytes.Length + 1];
            data[0] = (byte)(twoBits << 6);

            for (int i = 0; i < bytes.Length; i++)
            {
                data[i] |= (byte)(bytes[i] >> 2);
                data[i + 1] |= (byte)(bytes[i] << 6);
            }

            return new DwgBitReader(data);
        }

        [Fact]
        public void AModularChar_OfTheSpecification_Reads4610()
        {
            Assert.Equal(4610, Of(0b1000_0010, 0b0010_0100).ModularChar());
        }

        [Fact]
        public void AModularChar_OfTheSpecification_Reads112823273()
        {
            Assert.Equal(112823273, Of(0b1110_1001, 0b1001_0111, 0b1110_0110, 0b0011_0101).ModularChar());
        }

        [Fact]
        public void AModularChar_OfTheSpecification_ReadsMinus1413()
        {
            Assert.Equal(-1413, Of(0b1000_0101, 0b0100_1011).ModularChar());
        }

        [Fact]
        public void AnUnsignedModularChar_KeepsBit0x40AsPartOfTheNumber()
        {
            Assert.Equal(5 + (0x4B << 7), Of(0b1000_0101, 0b0100_1011).ModularChar(false));
        }

        [Fact]
        public void AModularChar_ThatNeverEnds_IsRefused()
        {
            var bytes = new byte[12];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = 0xFF;
            }

            Assert.Throws<FormatException>(() => Of(bytes).ModularChar());
        }

        [Fact]
        public void AModularShort_OfTheSpecification_Reads4650033()
        {
            Assert.Equal(4650033, Of(0b0011_0001, 0b1111_0100, 0b1000_1101, 0b0000_0000).ModularShort());
        }

        [Fact]
        public void AHandle_IsItsCode_ItsCountOfBytes_AndTheBytesHighFirst()
        {
            Assert.Equal(0x1FUL, Of(0x41, 0x1F).Handle(0x100, out int code));
            Assert.Equal(4, code);
            Assert.Equal(0x1234UL, Of(0x52, 0x12, 0x34).Handle());
            Assert.Equal(0UL, Of(0x40).Handle());
        }

        [Fact]
        public void ARelativeHandle_CountsFromTheHandleOfTheObject()
        {
            Assert.Equal(0x101UL, Of(0x60).Handle(0x100));
            Assert.Equal(0xFFUL, Of(0x80).Handle(0x100));
            Assert.Equal(0x105UL, Of(0xA1, 0x05).Handle(0x100));
            Assert.Equal(0xFBUL, Of(0xC1, 0x05).Handle(0x100));
        }

        [Fact]
        public void AHandle_OfMoreThanEightBytes_IsRefused()
        {
            Assert.Throws<FormatException>(() => Of(0x49, 1, 2, 3, 4, 5, 6, 7, 8, 9).Handle());
        }

        [Fact]
        public void AText_IsCutAtTheZeroItsCountTakesIn()
        {
            // 01 and the count 3, then 'A', 'b' and the zero, two bits off the byte boundary.
            Assert.Equal(new byte[] { (byte)'A', (byte)'b' }, Shifted(0b01, 3, (byte)'A', (byte)'b', 0).Text());
            Assert.Empty(Of(0b1000_0000).Text());
        }

        [Fact]
        public void AThickness_AndAnExtrusion_AreOneBitWhereTheyAreTheUsualOnes()
        {
            Assert.Equal(0.0, Of(0b1000_0000).BitThickness());

            // 0, then the bit double 01 (1.0).
            Assert.Equal(1.0, Of(0b0010_0000).BitThickness());
            Assert.Equal(new[] { 0.0, 0.0, 1.0 }, Of(0b1000_0000).BitExtrusion());

            // 0, then three bit doubles: 01 (1.0), 10 (0.0), 10 (0.0).
            Assert.Equal(new[] { 1.0, 0.0, 0.0 }, Of(0b0011_0100).BitExtrusion());
        }

        [Fact]
        public void ReadingPastTheEnd_IsRefused_WhateverIsRead()
        {
            Assert.Throws<FormatException>(() => Of().Bit());
            Assert.Throws<FormatException>(() => Of(1).RawShort());
            Assert.Throws<FormatException>(() => Of(0, 0, 0).BitDouble());
            Assert.Throws<FormatException>(() => Of(0x42, 0x01).Handle());
            Assert.Throws<FormatException>(() => Of(0x80).ModularChar());
            Assert.Throws<FormatException>(() => Of(1, 2).Bytes(3));
            Assert.Throws<FormatException>(() => Of(1, 2).Bytes(-1));
        }

        [Fact]
        public void AReaderOverAPartOfTheBytes_EndsWhereThePartEnds()
        {
            var reader = new DwgBitReader(new byte[] { 0xFF, 0xFF, 0xFF }, 4, 12);

            Assert.Equal(0xFF, reader.RawChar());
            Assert.Equal(0, reader.Remaining);
            Assert.Throws<FormatException>(() => reader.Bit());
            Assert.Throws<FormatException>(() => reader.Position = 13);
        }
    }
}
