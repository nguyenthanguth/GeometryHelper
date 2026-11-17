using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The presenter's own decoders of the raster types the platform does not read: TGA, PCX, TIFF (plain, packed, with the
    /// predictor, in strips, in tiles, chunky and planar, in either byte order, bilevel to 16 bits) and the Windows
    /// bitmaps with no file header (DIB) or run-length coded (RLE). Each picture is made in the test by a small writer of
    /// the format, so that a decoder is checked pixel by pixel against what was written.
    /// </summary>
    public class RasterFormatTests
    {
        /// <summary>A picture of 13 by 9 pixels, each of its colours different from its neighbours', as red, green, blue and alpha.</summary>
        internal static byte[] Pattern(int width = 13, int height = 9, bool alpha = false)
        {
            var rgba = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int at = (y * width + x) * 4;
                    rgba[at] = (byte)(x * 37 + y * 11);
                    rgba[at + 1] = (byte)(x * 5 + y * 53);
                    rgba[at + 2] = (byte)(x * 91 + y * 17);
                    rgba[at + 3] = alpha ? (byte)(255 - x * 7 - y) : (byte)255;
                }
            }

            return rgba;
        }

        private static CadRaster Read(byte[] file, string name)
        {
            Assert.True(CadRasterReader.TryRead(file, name, true, out CadRaster raster), name + " is read");
            return raster;
        }

        private static void AssertSame(byte[] expected, CadRaster raster, int width, int height)
        {
            Assert.Equal(width, raster.Width);
            Assert.Equal(height, raster.Height);
            Assert.Equal(1, raster.Reduction);
            Assert.Equal(expected, raster.Rgba);
        }

        // ------------------------------------------------------------------ TGA

        internal static byte[] Tga(
            int type,
            int bits,
            int width,
            int height,
            byte[] rgba,
            bool topOrigin,
            bool rle,
            int[] palette = null,
            byte[] indexes = null,
            bool alpha = false
        )
        {
            var file = new List<byte>();
            int perPixel = bits / 8;
            file.AddRange(
                new byte[] { 0, (byte)(palette != null ? 1 : 0), (byte)type, 0, 0, (byte)(palette?.Length ?? 0), 0, 24 }
            );
            file.AddRange(
                new byte[] { 0, 0, 0, 0, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8) }
            );
            file.Add((byte)bits);
            file.Add((byte)((topOrigin ? 0x20 : 0) | (alpha ? 8 : 0)));

            if (palette != null)
            {
                foreach (int colour in palette)
                {
                    file.AddRange(new[] { (byte)colour, (byte)(colour >> 8), (byte)(colour >> 16) });
                }
            }

            for (int row = 0; row < height; row++)
            {
                int y = topOrigin ? row : height - 1 - row;
                var pixels = new List<byte[]>();

                for (int x = 0; x < width; x++)
                {
                    int at = (y * width + x) * 4;

                    if (indexes != null)
                    {
                        pixels.Add(new[] { indexes[y * width + x] });
                    }
                    else if (perPixel == 1)
                    {
                        pixels.Add(new[] { rgba[at] });
                    }
                    else if (perPixel == 3)
                    {
                        pixels.Add(new[] { rgba[at + 2], rgba[at + 1], rgba[at] });
                    }
                    else
                    {
                        pixels.Add(new[] { rgba[at + 2], rgba[at + 1], rgba[at], rgba[at + 3] });
                    }
                }

                if (!rle)
                {
                    foreach (byte[] p in pixels)
                    {
                        file.AddRange(p);
                    }

                    continue;
                }

                int i = 0;

                while (i < pixels.Count)
                {
                    int run = 1;

                    while (i + run < pixels.Count && run < 128 && pixels[i + run].SequenceEqual(pixels[i]))
                    {
                        run++;
                    }

                    if (run >= 2)
                    {
                        file.Add((byte)(0x80 | (run - 1)));
                        file.AddRange(pixels[i]);
                        i += run;
                        continue;
                    }

                    int raw = 1;

                    while (
                        i + raw < pixels.Count
                        && raw < 128
                        && (i + raw + 1 >= pixels.Count || !pixels[i + raw].SequenceEqual(pixels[i + raw + 1]))
                    )
                    {
                        raw++;
                    }

                    file.Add((byte)(raw - 1));

                    for (int k = 0; k < raw; k++)
                    {
                        file.AddRange(pixels[i + k]);
                    }

                    i += raw;
                }
            }

            return file.ToArray();
        }

        [Fact]
        public void ATrueColourTgaFromTheBottom_IsReadPixelForPixel()
        {
            byte[] expected = Pattern();
            AssertSame(expected, Read(Tga(2, 24, 13, 9, expected, false, false), "a.tga"), 13, 9);
        }

        [Fact]
        public void ATrueColourTgaFromTheTop_RunLengthCoded_IsReadPixelForPixel()
        {
            byte[] expected = Pattern();

            // A run of equal pixels in the middle of each row makes the repeat packets as well as the raw ones.
            for (int y = 0; y < 9; y++)
            {
                for (int x = 4; x < 9; x++)
                {
                    Array.Copy(expected, (y * 13 + 4) * 4, expected, (y * 13 + x) * 4, 4);
                }
            }

            AssertSame(expected, Read(Tga(10, 24, 13, 9, expected, true, true), "a.tga"), 13, 9);
        }

        [Fact]
        public void AThirtyTwoBitTgaWithEightBitsOfAlpha_KeepsItsAlpha()
        {
            byte[] expected = Pattern(alpha: true);
            AssertSame(expected, Read(Tga(2, 32, 13, 9, expected, false, false, alpha: true), "a.tga"), 13, 9);
        }

        [Fact]
        public void AColourMappedTga_AndAGreyOne_AreReadThroughTheirMapAndAsLevels()
        {
            int[] palette = { 0xFF0000, 0x00FF00, 0x0000FF, 0xFFFF00 };
            var indexes = new byte[13 * 9];
            var expected = new byte[13 * 9 * 4];

            for (int i = 0; i < indexes.Length; i++)
            {
                indexes[i] = (byte)(i * 7 % 4);
                int c = palette[indexes[i]];
                expected[i * 4] = (byte)(c >> 16);
                expected[i * 4 + 1] = (byte)(c >> 8);
                expected[i * 4 + 2] = (byte)c;
                expected[i * 4 + 3] = 255;
            }

            AssertSame(expected, Read(Tga(1, 8, 13, 9, null, false, false, palette, indexes), "a.tga"), 13, 9);
            AssertSame(expected, Read(Tga(9, 8, 13, 9, null, true, true, palette, indexes), "a.tga"), 13, 9);

            byte[] grey = Pattern();

            for (int i = 0; i < grey.Length; i += 4)
            {
                grey[i + 1] = grey[i];
                grey[i + 2] = grey[i];
            }

            AssertSame(grey, Read(Tga(3, 8, 13, 9, grey, false, false), "a.tga"), 13, 9);
        }

        [Fact]
        public void ATgaHasNoSignature_SoItIsFoundByItsNameOrByAHeaderThatMakesSense()
        {
            byte[] file = Tga(2, 24, 13, 9, Pattern(), false, false);
            Assert.True(CadRasterReader.TryRead(file, "scan.tga", false, out CadRaster named));
            Assert.Equal(13, named.Width);
            Assert.True(CadRasterReader.TryRead(file, "scan.bin", false, out CadRaster unnamed));
            Assert.Equal(9, unnamed.Height);
            Assert.False(CadRasterReader.TryRead(new byte[64], "scan.bin", false, out _));
        }

        // ------------------------------------------------------------------ PCX

        private static byte[] PcxLine(IEnumerable<byte> bytes)
        {
            var coded = new List<byte>();
            byte[] all = bytes.ToArray();
            int i = 0;

            while (i < all.Length)
            {
                int run = 1;

                while (i + run < all.Length && run < 63 && all[i + run] == all[i])
                {
                    run++;
                }

                if (run > 1 || all[i] >= 0xC0)
                {
                    coded.Add((byte)(0xC0 | run));
                }

                coded.Add(all[i]);
                i += run;
            }

            return coded.ToArray();
        }

        internal static byte[] Pcx(
            int bits,
            int planes,
            int width,
            int height,
            Func<int, int, int, int> sample,
            int[] headerPalette = null,
            int[] palette256 = null,
            int dpi = 0
        )
        {
            int perLine = ((width * bits + 7) / 8 + 1) & ~1;
            var file = new List<byte>(new byte[128]);
            file[0] = 0x0A;
            file[1] = 5;
            file[2] = 1;
            file[3] = (byte)bits;
            file[8] = (byte)(width - 1);
            file[9] = (byte)((width - 1) >> 8);
            file[10] = (byte)(height - 1);
            file[11] = (byte)((height - 1) >> 8);
            file[12] = (byte)dpi;
            file[13] = (byte)(dpi >> 8);
            file[14] = (byte)dpi;
            file[15] = (byte)(dpi >> 8);
            file[65] = (byte)planes;
            file[66] = (byte)perLine;
            file[67] = (byte)(perLine >> 8);

            if (headerPalette != null)
            {
                for (int i = 0; i < headerPalette.Length; i++)
                {
                    file[16 + 3 * i] = (byte)(headerPalette[i] >> 16);
                    file[17 + 3 * i] = (byte)(headerPalette[i] >> 8);
                    file[18 + 3 * i] = (byte)headerPalette[i];
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int plane = 0; plane < planes; plane++)
                {
                    var line = new byte[perLine];

                    for (int x = 0; x < width; x++)
                    {
                        int value = sample(x, y, plane);
                        int bit = x * bits;
                        line[bit >> 3] |= (byte)(value << (8 - bits - (bit & 7)));
                    }

                    file.AddRange(PcxLine(line));
                }
            }

            if (palette256 != null)
            {
                file.Add(0x0C);

                foreach (int colour in palette256)
                {
                    file.AddRange(new[] { (byte)(colour >> 16), (byte)(colour >> 8), (byte)colour });
                }
            }

            return file.ToArray();
        }

        [Fact]
        public void APcxOf256Colours_ReadsItsPaletteFromTheEndOfTheFile()
        {
            var palette = new int[256];

            for (int i = 0; i < 256; i++)
            {
                palette[i] = (i * 3 << 16) % 0x1000000 | ((255 - i) << 8) | (i ^ 0x5A);
            }

            byte[] file = Pcx(8, 1, 13, 9, (x, y, p) => (x * 29 + y * 61) & 255, null, palette);
            CadRaster raster = Read(file, "a.pcx");
            Assert.Equal(13, raster.Width);
            Assert.Equal(9, raster.Height);

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 13; x++)
                {
                    int c = palette[(x * 29 + y * 61) & 255];
                    int at = (y * 13 + x) * 4;
                    Assert.Equal(
                        new[] { (byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)255 },
                        raster.Rgba.Skip(at).Take(4)
                    );
                }
            }

            Assert.False(raster.IsBitonal);
        }

        [Fact]
        public void APcxOfThreePlanesOf8Bits_IsTrueColour_AndItsResolutionIsItsDotsAnInch()
        {
            byte[] expected = Pattern();
            byte[] file = Pcx(8, 3, 13, 9, (x, y, p) => expected[(y * 13 + x) * 4 + p], dpi: 300);
            CadRaster raster = Read(file, "a.pcx");
            AssertSame(expected, raster, 13, 9);
            Assert.Equal(25.4 / 300, raster.MillimetresX, 9);
            Assert.Equal(25.4 / 300, raster.MillimetresY, 9);
        }

        [Fact]
        public void APcxOneBitInOnePlane_IsBitonal_ZeroBlackAndOneWhite()
        {
            byte[] file = Pcx(1, 1, 13, 9, (x, y, p) => (x + y) % 3 == 0 ? 1 : 0);
            CadRaster raster = Read(file, "a.pcx");
            Assert.True(raster.IsBitonal);

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 13; x++)
                {
                    byte level = (x + y) % 3 == 0 ? (byte)255 : (byte)0;
                    Assert.Equal(level, raster.Rgba[(y * 13 + x) * 4]);
                }
            }
        }

        [Fact]
        public void APcxOfFourPlanesOfOneBit_TakesItsSixteenColoursFromTheHeader()
        {
            var palette = new int[16];

            for (int i = 0; i < 16; i++)
            {
                palette[i] = (i * 16 << 16) | ((15 - i) * 16 << 8) | (i * 8);
            }

            byte[] file = Pcx(1, 4, 13, 9, (x, y, p) => ((x * 3 + y * 5) % 16 >> p) & 1, palette);
            CadRaster raster = Read(file, "a.pcx");
            Assert.False(raster.IsBitonal);

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 13; x++)
                {
                    int c = palette[(x * 3 + y * 5) % 16];
                    int at = (y * 13 + x) * 4;
                    Assert.Equal(new[] { (byte)(c >> 16), (byte)(c >> 8), (byte)c }, raster.Rgba.Skip(at).Take(3));
                }
            }
        }

        // ------------------------------------------------------------------ DIB, BMP and RLE

        private static void Le(List<byte> bytes, long value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                bytes.Add((byte)(value >> (8 * i)));
            }
        }

        internal static byte[] Dib(
            int bits,
            int width,
            int height,
            Func<int, int, int> pixel,
            int[] palette = null,
            bool topDown = false,
            bool fileHeader = false,
            int compression = 0,
            byte[] pixelBytes = null,
            uint[] masks = null,
            int perMetre = 0
        )
        {
            var body = new List<byte>();
            int count = palette?.Length ?? 0;
            int headerSize = 40;
            Le(body, headerSize, 4);
            Le(body, width, 4);
            Le(body, topDown ? -height : height, 4);
            Le(body, 1, 2);
            Le(body, bits, 2);
            Le(body, compression, 4);
            Le(body, pixelBytes?.Length ?? 0, 4);
            Le(body, perMetre, 4);
            Le(body, perMetre, 4);
            Le(body, count, 4);
            Le(body, 0, 4);

            if (masks != null)
            {
                foreach (uint m in masks)
                {
                    Le(body, m, 4);
                }
            }

            if (palette != null)
            {
                foreach (int c in palette)
                {
                    body.AddRange(new[] { (byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)0 });
                }
            }

            int offset = (fileHeader ? 14 : 0) + body.Count;

            if (pixelBytes != null)
            {
                body.AddRange(pixelBytes);
            }
            else
            {
                int stride = (width * bits + 31) / 32 * 4;

                for (int row = 0; row < height; row++)
                {
                    int y = topDown ? row : height - 1 - row;
                    var line = new byte[stride];

                    for (int x = 0; x < width; x++)
                    {
                        int v = pixel(x, y);

                        if (bits <= 8)
                        {
                            int bit = x * bits;
                            line[bit >> 3] |= (byte)(v << (8 - bits - (bit & 7)));
                        }
                        else if (bits == 16)
                        {
                            line[2 * x] = (byte)v;
                            line[2 * x + 1] = (byte)(v >> 8);
                        }
                        else if (bits == 24)
                        {
                            line[3 * x] = (byte)v;
                            line[3 * x + 1] = (byte)(v >> 8);
                            line[3 * x + 2] = (byte)(v >> 16);
                        }
                        else
                        {
                            line[4 * x] = (byte)v;
                            line[4 * x + 1] = (byte)(v >> 8);
                            line[4 * x + 2] = (byte)(v >> 16);
                            line[4 * x + 3] = (byte)(v >> 24);
                        }
                    }

                    body.AddRange(line);
                }
            }

            if (!fileHeader)
            {
                return body.ToArray();
            }

            var file = new List<byte> { (byte)'B', (byte)'M' };
            Le(file, 14 + body.Count, 4);
            Le(file, 0, 4);
            Le(file, offset, 4);
            file.AddRange(body);
            return file.ToArray();
        }

        private static int Rgb(byte[] rgba, int x, int y, int width) =>
            (rgba[(y * width + x) * 4] << 16) | (rgba[(y * width + x) * 4 + 1] << 8) | rgba[(y * width + x) * 4 + 2];

        [Fact]
        public void ADibOf24Bits_WithNoFileHeader_IsReadFromItsNameOrItsInfoHeader()
        {
            byte[] expected = Pattern();
            byte[] file = Dib(24, 13, 9, (x, y) => Rgb(expected, x, y, 13), perMetre: 3780);
            CadRaster raster = Read(file, "a.dib");
            AssertSame(expected, raster, 13, 9);
            Assert.Equal(1000.0 / 3780, raster.MillimetresX, 9);
            AssertSame(expected, Read(file, "a.bin"), 13, 9);
        }

        [Fact]
        public void ABmpFromTheTop_OfEightBitsWithAPalette_IsReadThroughItsPalette()
        {
            var palette = new int[256];

            for (int i = 0; i < 256; i++)
            {
                palette[i] = (i << 16) | ((i * 5 & 255) << 8) | (255 - i);
            }

            byte[] file = Dib(8, 13, 9, (x, y) => (x * 17 + y * 29) & 255, palette, topDown: true, fileHeader: true);
            CadRaster raster = Read(file, "a.bmp");

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 13; x++)
                {
                    Assert.Equal(palette[(x * 17 + y * 29) & 255], Rgb(raster.Rgba, x, y, 13));
                }
            }
        }

        [Fact]
        public void ABitonalBmp_OfOneBit_IsBitonal_AndOneOfFourBitsIsNot()
        {
            int[] black = { 0x000000, 0xFFFFFF };
            byte[] one = Dib(1, 13, 9, (x, y) => (x * y) & 1, black, fileHeader: true);
            CadRaster raster = Read(one, "a.bmp");
            Assert.True(raster.IsBitonal);
            Assert.Equal(0xFFFFFF, Rgb(raster.Rgba, 1, 1, 13));
            Assert.Equal(0x000000, Rgb(raster.Rgba, 2, 1, 13));

            int[] sixteen = Enumerable.Range(0, 16).Select(i => (i * 16 << 16) | (i * 3)).ToArray();
            byte[] four = Dib(4, 13, 9, (x, y) => (x + y) % 16, sixteen, fileHeader: true);
            CadRaster fourBits = Read(four, "a.bmp");
            Assert.False(fourBits.IsBitonal);
            Assert.Equal(sixteen[(5 + 4) % 16], Rgb(fourBits.Rgba, 5, 4, 13));
        }

        [Fact]
        public void ABmpOf16BitsWithFiveSixFiveMasks_AndOneOf32BitsWithAlpha_AreReadByTheirMasks()
        {
            // 565: red 11111, green 000000, blue 11111 is magenta.
            byte[] sixteen = Dib(
                16,
                13,
                9,
                (x, y) => x < 6 ? 0xF81F : 0x07E0,
                compression: 3,
                masks: new uint[] { 0xF800, 0x07E0, 0x001F },
                fileHeader: true
            );
            CadRaster raster = Read(sixteen, "a.bmp");
            Assert.Equal(0xFF00FF, Rgb(raster.Rgba, 0, 0, 13));
            Assert.Equal(0x00FF00, Rgb(raster.Rgba, 12, 8, 13));

            byte[] thirtyTwo = Dib(
                32,
                13,
                9,
                (x, y) => unchecked((int)0x80102030),
                compression: 6,
                masks: new uint[] { 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000 }
            );
            CadRaster withAlpha = Read(thirtyTwo, "a.dib");
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x80 }, withAlpha.Rgba.Take(4));
        }

        [Fact]
        public void AnRle8Bitmap_WithRunsAbsoluteRunsAndAJump_IsUnpackedFromTheBottom()
        {
            int[] palette = Enumerable.Range(0, 256).Select(i => (i << 16) | (i << 8) | i).ToArray();
            var data = new List<byte>();

            // The bottom row: a run of 6 of index 9, then an absolute run of 3 (7, 8, 6, padded), then a run of 4 of 200; end of line.
            data.AddRange(new byte[] { 6, 9, 0, 3, 7, 8, 6, 0, 4, 200, 0, 0 });

            // The next row: a jump of 5 across, 0 up, a run of 8 of index 50; end of line.
            data.AddRange(new byte[] { 0, 2, 5, 0, 8, 50, 0, 0 });

            // The rest of the rows are left; end of bitmap.
            data.AddRange(new byte[] { 0, 1 });
            byte[] file = Dib(8, 13, 3, null, palette, compression: 1, pixelBytes: data.ToArray(), fileHeader: true);
            CadRaster raster = Read(file, "a.rle");
            int y = 2;

            for (int x = 0; x < 6; x++)
            {
                Assert.Equal(palette[9], Rgb(raster.Rgba, x, y, 13));
            }

            Assert.Equal(palette[7], Rgb(raster.Rgba, 6, y, 13));
            Assert.Equal(palette[8], Rgb(raster.Rgba, 7, y, 13));
            Assert.Equal(palette[6], Rgb(raster.Rgba, 8, y, 13));
            Assert.Equal(palette[200], Rgb(raster.Rgba, 9, y, 13));
            Assert.Equal(palette[200], Rgb(raster.Rgba, 12, y, 13));
            Assert.Equal(palette[0], Rgb(raster.Rgba, 4, 1, 13));
            Assert.Equal(palette[50], Rgb(raster.Rgba, 5, 1, 13));
            Assert.Equal(palette[50], Rgb(raster.Rgba, 12, 1, 13));
            Assert.Equal(palette[0], Rgb(raster.Rgba, 5, 0, 13));
        }

        [Fact]
        public void AnRle4Bitmap_IsUnpackedTwoPixelsToTheByte()
        {
            int[] palette = Enumerable.Range(0, 16).Select(i => (i * 16 << 16) | (i * 16 << 8) | (i * 16)).ToArray();

            // A run of 5 pixels alternating 3 and 12, then an absolute run of 4 (1, 2, 3, 4), then the end of the bitmap.
            byte[] data = { 5, 0x3C, 0, 4, 0x12, 0x34, 0, 1 };
            byte[] file = Dib(4, 9, 1, null, palette, compression: 2, pixelBytes: data, fileHeader: true);
            CadRaster raster = Read(file, "a.bmp");
            int[] expected = { 3, 12, 3, 12, 3, 1, 2, 3, 4 };

            for (int x = 0; x < 9; x++)
            {
                Assert.Equal(palette[expected[x]], Rgb(raster.Rgba, x, 0, 9));
            }
        }

        // ------------------------------------------------------------------ TIFF

        /// <summary>What a TIFF made by <see cref="Tiff"/> is given: how its samples are stored.</summary>
        internal sealed class TiffOptions
        {
            public int Photometric = 2;
            public int Samples = 3;
            public int Bits = 8;
            public int Compression = 1;
            public int Predictor = 1;
            public bool BigEndian;
            public bool Planar;
            public int RowsPerStrip = 4;
            public int TileSize;
            public int[] Palette;
            public int Extra;
            public int ResolutionUnit = 2;
            public double Resolution;
        }

        private static byte[] PackBitsOf(byte[] data)
        {
            var packed = new List<byte>();
            int i = 0;

            while (i < data.Length)
            {
                int run = 1;

                while (i + run < data.Length && run < 128 && data[i + run] == data[i])
                {
                    run++;
                }

                if (run >= 2)
                {
                    packed.Add((byte)(1 - run));
                    packed.Add(data[i]);
                    i += run;
                    continue;
                }

                int raw = 1;

                while (
                    i + raw < data.Length
                    && raw < 128
                    && (i + raw + 1 >= data.Length || data[i + raw] != data[i + raw + 1])
                )
                {
                    raw++;
                }

                packed.Add((byte)(raw - 1));
                packed.AddRange(data.Skip(i).Take(raw));
                i += raw;
            }

            return packed.ToArray();
        }

        private static byte[] ZlibOf(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9C);

                using (var deflate = new DeflateStream(output, CompressionMode.Compress, true))
                {
                    deflate.Write(data, 0, data.Length);
                }

                uint a = 1;
                uint b = 0;

                foreach (byte value in data)
                {
                    a = (a + value) % 65521;
                    b = (b + a) % 65521;
                }

                uint adler = (b << 16) | a;
                output.Write(new[] { (byte)(adler >> 24), (byte)(adler >> 16), (byte)(adler >> 8), (byte)adler }, 0, 4);
                return output.ToArray();
            }
        }

        /// <summary>
        /// A TIFF of <paramref name="rows"/>, each row the samples of one line as the options store them (chunky, bits packed
        /// from the top), cut into strips or tiles, the horizontal predictor applied where asked, each strip packed as asked.
        /// </summary>
        internal static byte[] Tiff(int width, byte[][] rows, TiffOptions o)
        {
            int height = rows.Length;
            var chunks = new List<byte[]>();
            int tileWidth = o.TileSize;
            int bytesPerPixel = o.Samples * o.Bits / 8;
            var planes = new List<byte[][]>();

            if (o.Planar)
            {
                for (int p = 0; p < o.Samples; p++)
                {
                    byte[][] plane = rows.Select(r =>
                        {
                            int per = o.Bits / 8;
                            var line = new byte[width * per];

                            for (int x = 0; x < width; x++)
                            {
                                Array.Copy(r, (x * o.Samples + p) * per, line, x * per, per);
                            }

                            return line;
                        })
                        .ToArray();
                    planes.Add(plane);
                }
            }
            else
            {
                planes.Add(rows);
            }

            int samplesInChunk = o.Planar ? 1 : o.Samples;

            foreach (byte[][] plane in planes)
            {
                if (o.TileSize > 0)
                {
                    int across = (width + tileWidth - 1) / tileWidth;
                    int down = (height + tileWidth - 1) / tileWidth;

                    for (int ty = 0; ty < down; ty++)
                    {
                        for (int tx = 0; tx < across; tx++)
                        {
                            var tile = new List<byte[]>();

                            for (int r = 0; r < tileWidth; r++)
                            {
                                var line = new byte[tileWidth * samplesInChunk * o.Bits / 8];
                                int y = ty * tileWidth + r;

                                if (y < height)
                                {
                                    int from = tx * tileWidth * samplesInChunk * o.Bits / 8;
                                    int count = Math.Min(line.Length, plane[y].Length - from);

                                    if (count > 0)
                                    {
                                        Array.Copy(plane[y], from, line, 0, count);
                                    }
                                }

                                tile.Add(line);
                            }

                            chunks.Add(Pack(tile.ToArray(), o, samplesInChunk));
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < height; y += o.RowsPerStrip)
                    {
                        chunks.Add(Pack(plane.Skip(y).Take(o.RowsPerStrip).ToArray(), o, samplesInChunk));
                    }
                }
            }

            var file = new List<byte> { o.BigEndian ? (byte)'M' : (byte)'I', o.BigEndian ? (byte)'M' : (byte)'I' };
            Action<List<byte>, long, int> put = (list, value, count) =>
            {
                for (int i = 0; i < count; i++)
                {
                    int shift = o.BigEndian ? 8 * (count - 1 - i) : 8 * i;
                    list.Add((byte)(value >> shift));
                }
            };
            put(file, 42, 2);
            put(file, 0, 4);
            var offsets = new List<long>();

            foreach (byte[] chunk in chunks)
            {
                offsets.Add(file.Count);
                file.AddRange(chunk);

                if (file.Count % 2 == 1)
                {
                    file.Add(0);
                }
            }

            var entries = new SortedDictionary<int, Tuple<int, long[]>>();
            Action<int, int, long[]> add = (tag, type, values) => entries[tag] = Tuple.Create(type, values);
            add(256, 4, new long[] { width });
            add(257, 4, new long[] { height });
            add(258, 3, Enumerable.Repeat((long)o.Bits, o.Samples).ToArray());
            add(259, 3, new long[] { o.Compression });
            add(262, 3, new long[] { o.Photometric });

            if (o.TileSize > 0)
            {
                add(322, 3, new long[] { tileWidth });
                add(323, 3, new long[] { tileWidth });
                add(324, 4, offsets.ToArray());
                add(325, 4, chunks.Select(c => (long)c.Length).ToArray());
            }
            else
            {
                add(273, 4, offsets.ToArray());
                add(278, 4, new long[] { o.RowsPerStrip });
                add(279, 4, chunks.Select(c => (long)c.Length).ToArray());
            }

            add(277, 3, new long[] { o.Samples });
            add(284, 3, new long[] { o.Planar ? 2 : 1 });

            if (o.Predictor != 1)
            {
                add(317, 3, new long[] { o.Predictor });
            }

            if (o.Palette != null)
            {
                var map = new long[o.Palette.Length * 3];

                for (int i = 0; i < o.Palette.Length; i++)
                {
                    map[i] = ((o.Palette[i] >> 16) & 255) * 257;
                    map[o.Palette.Length + i] = ((o.Palette[i] >> 8) & 255) * 257;
                    map[2 * o.Palette.Length + i] = (o.Palette[i] & 255) * 257;
                }

                add(320, 3, map);
            }

            if (o.Extra != 0)
            {
                add(338, 3, new long[] { o.Extra });
            }

            if (o.Resolution > 0)
            {
                add(282, 5, new[] { (long)(o.Resolution * 100), 100 });
                add(283, 5, new[] { (long)(o.Resolution * 100), 100 });
                add(296, 3, new long[] { o.ResolutionUnit });
            }

            // The values that do not fit in the 4 bytes of an entry come after the directory.
            int directoryAt = file.Count;
            int directorySize = 2 + 12 * entries.Count + 4;
            int extraAt = directoryAt + directorySize;
            var extra = new List<byte>();
            var directory = new List<byte>();
            put(directory, entries.Count, 2);

            foreach (KeyValuePair<int, Tuple<int, long[]>> entry in entries)
            {
                int type = entry.Value.Item1;
                long[] values = entry.Value.Item2;
                int size =
                    type == 3 ? 2
                    : type == 4 ? 4
                    : 8;
                int count = type == 5 ? values.Length / 2 : values.Length;
                put(directory, entry.Key, 2);
                put(directory, type, 2);
                put(directory, count, 4);
                var data = new List<byte>();

                foreach (long v in values)
                {
                    put(data, v, type == 5 ? 4 : size);
                }

                if (data.Count <= 4)
                {
                    directory.AddRange(data);
                    directory.AddRange(new byte[4 - data.Count]);
                }
                else
                {
                    put(directory, extraAt + extra.Count, 4);
                    extra.AddRange(data);
                }
            }

            put(directory, 0, 4);
            file.AddRange(directory);
            file.AddRange(extra);
            byte[] bytes = file.ToArray();
            int ifd = directoryAt;

            if (o.BigEndian)
            {
                bytes[4] = (byte)(ifd >> 24);
                bytes[5] = (byte)(ifd >> 16);
                bytes[6] = (byte)(ifd >> 8);
                bytes[7] = (byte)ifd;
            }
            else
            {
                bytes[4] = (byte)ifd;
                bytes[5] = (byte)(ifd >> 8);
                bytes[6] = (byte)(ifd >> 16);
                bytes[7] = (byte)(ifd >> 24);
            }

            return bytes;
        }

        private static byte[] Pack(byte[][] rows, TiffOptions o, int samples)
        {
            if (o.Predictor == 2)
            {
                rows = rows.Select(r =>
                    {
                        var copy = (byte[])r.Clone();

                        for (int i = r.Length - 1; i >= samples; i--)
                        {
                            copy[i] = (byte)(r[i] - r[i - samples]);
                        }

                        return copy;
                    })
                    .ToArray();
            }

            byte[] data = rows.SelectMany(r => r).ToArray();
            return o.Compression == 32773 ? PackBitsOf(data)
                : o.Compression == 8 ? ZlibOf(data)
                : data;
        }

        internal static byte[][] RgbRows(byte[] rgba, int width, int height) =>
            Enumerable
                .Range(0, height)
                .Select(y =>
                    Enumerable
                        .Range(0, width)
                        .SelectMany(x =>
                            new[]
                            {
                                rgba[(y * width + x) * 4],
                                rgba[(y * width + x) * 4 + 1],
                                rgba[(y * width + x) * 4 + 2],
                            }
                        )
                        .ToArray()
                )
                .ToArray();

        [Theory]
        [InlineData(1, false, false, 1)]
        [InlineData(32773, false, false, 1)]
        [InlineData(8, false, false, 1)]
        [InlineData(1, true, false, 1)]
        [InlineData(8, true, false, 2)]
        [InlineData(32773, false, true, 1)]
        [InlineData(1, true, true, 1)]
        public void ATrueColourTiff_IsReadPixelForPixel_InEitherByteOrder_StoredPlainPackedOrDeflated(
            int compression,
            bool bigEndian,
            bool planar,
            int predictor
        )
        {
            byte[] expected = Pattern();
            var options = new TiffOptions
            {
                Compression = compression,
                BigEndian = bigEndian,
                Planar = planar,
                Predictor = predictor,
            };
            byte[] file = Tiff(13, RgbRows(expected, 13, 9), options);
            AssertSame(expected, Read(file, "a.tif"), 13, 9);
        }

        [Fact]
        public void ATiffInTiles_OfSixteenPixels_IsReadOverTheEdgesOfItsTiles()
        {
            byte[] expected = Pattern(37, 21);
            var options = new TiffOptions { TileSize = 16, Compression = 32773 };
            byte[] file = Tiff(37, RgbRows(expected, 37, 21), options);
            AssertSame(expected, Read(file, "a.tiff"), 37, 21);

            options.Planar = true;
            options.Compression = 8;
            AssertSame(expected, Read(Tiff(37, RgbRows(expected, 37, 21), options), "a.tiff"), 37, 21);
        }

        [Fact]
        public void ABilevelTiff_OfOneBit_IsBitonal_WhiteIsZeroAndBlackIsZeroBothRead()
        {
            const int width = 21;
            const int height = 8;
            byte[][] rows = Enumerable
                .Range(0, height)
                .Select(y =>
                {
                    var row = new byte[(width + 7) / 8];

                    for (int x = 0; x < width; x++)
                    {
                        if ((x * 3 + y) % 5 < 2)
                        {
                            row[x >> 3] |= (byte)(0x80 >> (x & 7));
                        }
                    }

                    return row;
                })
                .ToArray();

            foreach (int photometric in new[] { 0, 1 })
            {
                var options = new TiffOptions
                {
                    Photometric = photometric,
                    Samples = 1,
                    Bits = 1,
                    Compression = 32773,
                    Resolution = 300,
                };
                CadRaster raster = Read(Tiff(width, rows, options), "scan.tif");
                Assert.True(raster.IsBitonal);
                Assert.Equal(25.4 / 300, raster.MillimetresX, 6);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool bit = (rows[y][x >> 3] & (0x80 >> (x & 7))) != 0;
                        byte level = (byte)(bit == (photometric == 1) ? 255 : 0);
                        Assert.Equal(level, raster.Rgba[(y * width + x) * 4]);
                    }
                }
            }
        }

        [Fact]
        public void ATiffOfSixteenColours_AndOneOfGreyLevels_AreReadThroughTheirPaletteAndAsLevels()
        {
            var palette = Enumerable
                .Range(0, 16)
                .Select(i => (i * 16 << 16) | ((15 - i) * 16 << 8) | (i * 5))
                .ToArray();
            byte[][] indexRows = Enumerable
                .Range(0, 6)
                .Select(y =>
                    Enumerable
                        .Range(0, 5)
                        .Select(i => (byte)(((y + i * 2) % 16 << 4) | ((y + i * 2 + 1) % 16)))
                        .ToArray()
                )
                .ToArray();
            var options = new TiffOptions
            {
                Photometric = 3,
                Samples = 1,
                Bits = 4,
                Palette = palette,
            };
            CadRaster raster = Read(Tiff(10, indexRows, options), "a.tif");

            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    int index = (indexRows[y][x / 2] >> (x % 2 == 0 ? 4 : 0)) & 15;
                    Assert.Equal(palette[index], Rgb(raster.Rgba, x, y, 10));
                }
            }

            byte[][] greyRows = Enumerable
                .Range(0, 4)
                .Select(y => Enumerable.Range(0, 7).Select(x => (byte)(x * 30 + y)).ToArray())
                .ToArray();
            var grey = new TiffOptions
            {
                Photometric = 1,
                Samples = 1,
                Bits = 8,
            };
            CadRaster levels = Read(Tiff(7, greyRows, grey), "a.tif");
            Assert.Equal(greyRows[0][0], levels.Rgba[0]);
            Assert.Equal(greyRows[3][6], levels.Rgba[(3 * 7 + 6) * 4]);
            grey.Photometric = 0;
            CadRaster inverted = Read(Tiff(7, greyRows, grey), "a.tif");
            Assert.Equal(255 - greyRows[3][6], inverted.Rgba[(3 * 7 + 6) * 4]);
        }

        [Fact]
        public void ATiffOfSixteenBitsASample_KeepsTheHighByte_AndOneWithAlphaKeepsItsAlpha()
        {
            // 16 bits: red 0x1234, green 0x5678, blue 0x9ABC, one pixel, in either byte order.
            foreach (bool big in new[] { false, true })
            {
                byte[] sample = big
                    ? new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC }
                    : new byte[] { 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A };
                var options = new TiffOptions
                {
                    Bits = 16,
                    BigEndian = big,
                    RowsPerStrip = 1,
                };
                CadRaster raster = Read(Tiff(1, new[] { sample }, options), "a.tif");
                Assert.Equal(new byte[] { 0x12, 0x56, 0x9A, 0xFF }, raster.Rgba);
            }

            byte[] withAlpha = Pattern(5, 4, alpha: true);
            byte[][] rows = Enumerable
                .Range(0, 4)
                .Select(y => Enumerable.Range(0, 5).SelectMany(x => withAlpha.Skip((y * 5 + x) * 4).Take(4)).ToArray())
                .ToArray();
            var rgba = new TiffOptions { Samples = 4, Extra = 2 };
            AssertSame(withAlpha, Read(Tiff(5, rows, rgba), "a.tif"), 5, 4);
        }

        [Fact]
        public void ATiffIsReadByItsSignature_NotItsName_AndItsDirectoryMayLieBehindItsStrips()
        {
            byte[] expected = Pattern();
            byte[] file = Tiff(13, RgbRows(expected, 13, 9), new TiffOptions());
            Assert.True(CadRasterReader.TryRead(file, "scan.dat", true, out CadRaster raster));
            Assert.Equal(expected, raster.Rgba);
            Assert.True(CadRasterReader.TryRead(file, "scan.tif", false, out CadRaster head));
            Assert.Equal(13, head.Width);
            Assert.Equal(9, head.Height);
            Assert.Null(head.Rgba);
        }

        [Fact]
        public void ATiffCodedAsJpeg_OrNotATiffAtAll_IsRefused_NotGuessed()
        {
            // Compression 7 is JPEG, which the decoders do not read.
            byte[] jpeg = Tiff(13, RgbRows(Pattern(), 13, 9), new TiffOptions { Compression = 7 });
            Assert.False(CadRasterReader.TryRead(jpeg, "a.tif", true, out _));

            // Its size is still known, so that it is attached as AutoCAD attaches it and drawn as its frame alone.
            Assert.True(CadRasterReader.TryRead(jpeg, "a.tif", false, out CadRaster head));
            Assert.Equal(13, head.Width);
            Assert.Equal(9, head.Height);
            Assert.False(CadRasterReader.TryRead(new byte[100], "a.tif", true, out _));
            Assert.False(
                CadRasterReader.TryRead(new byte[] { 0x49, 0x49, 42, 0, 8, 0, 0, 0, 0, 0, 0, 0 }, "a.tif", true, out _)
            );
        }

        // ------------------------------------------------------------------ Big pictures

        [Fact]
        public void APictureOfMoreThanSixteenMillionPixels_IsKeptAsTheMeanOfSquares_SoAThinLineIsNotLost()
        {
            // 4 100 by 4 100 pixels of white with one black column: 16.81 million pixels, kept as 2 by 2 squares.
            const int size = 4100;
            var raster = new CadRaster { Width = size, Height = size };
            var sink = new CadRasterSink(raster);
            var row = new byte[size * 4];

            for (int x = 0; x < size; x++)
            {
                bool black = x == 1001;
                row[4 * x] = row[4 * x + 1] = row[4 * x + 2] = black ? (byte)0 : (byte)255;
                row[4 * x + 3] = 255;
            }

            for (int y = 0; y < size; y++)
            {
                sink.Add(row, 0);
            }

            sink.Finish();
            Assert.Equal(2, raster.Reduction);
            Assert.Equal(2050, raster.KeptWidth);
            Assert.Equal(2050, raster.KeptHeight);

            // The black column is half of the pixels of its square: the grey halfway between.
            Assert.Equal(128, raster.Rgba[500 * 4]);
            Assert.Equal(255, raster.Rgba[499 * 4]);
            Assert.Equal(255, raster.Rgba[(raster.KeptWidth * 2049 + 2049) * 4]);
        }
    }
}
