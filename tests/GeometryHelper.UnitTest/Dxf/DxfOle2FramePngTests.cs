using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A static picture made of a PNG: the PNG read into a .bmp of 32 bits a pixel, its alpha laid over white. The PNGs are
    /// written here by an encoder of the tests' own, every colour type, bit depth and the interlaced kind among them.
    /// </summary>
    public class DxfOle2FramePngTests
    {
        /// <summary>A PNG of the samples given, row by row as PNG lays them (filter 0 or the one given each row), interlaced where asked.</summary>
        private static byte[] Png(int width, int height, int colour, int depth, Func<int, int, int[]> samples, byte[] palette = null, byte[] transparency = null, bool interlaced = false, int perMetre = 0)
        {
            var raw = new MemoryStream();

            void Image(int[] xs, int[] ys)
            {
                int channels = colour == 0 ? 1 : colour == 2 ? 3 : colour == 3 ? 1 : colour == 4 ? 2 : 4;
                int filter = 0;

                foreach (int y in ys)
                {
                    var bits = new List<int>();
                    foreach (int x in xs)
                    {
                        bits.AddRange(samples(x, y));
                    }

                    var line = new List<byte>();

                    if (depth >= 8)
                    {
                        foreach (int s in bits)
                        {
                            if (depth == 16)
                            {
                                line.Add((byte)(s >> 8));
                            }

                            line.Add((byte)s);
                        }
                    }
                    else
                    {
                        int perByte = 8 / depth;
                        for (int i = 0; i < bits.Count; i += perByte)
                        {
                            int b = 0;
                            for (int k = 0; k < perByte; k++)
                            {
                                b |= (i + k < bits.Count ? bits[i + k] : 0) << (8 - depth * (k + 1));
                            }

                            line.Add((byte)b);
                        }
                    }

                    // Filter 1 (sub) on odd rows, so that the reader's unfiltering is tried.
                    int bpp = Math.Max(1, channels * depth / 8);
                    byte[] row = line.ToArray();
                    filter = y % 2;

                    if (filter == 1)
                    {
                        for (int i = row.Length - 1; i >= bpp; i--)
                        {
                            row[i] = (byte)(row[i] - row[i - bpp]);
                        }
                    }

                    raw.WriteByte((byte)filter);
                    raw.Write(row, 0, row.Length);
                }
            }

            if (!interlaced)
            {
                Image(Enumerable.Range(0, width).ToArray(), Enumerable.Range(0, height).ToArray());
            }
            else
            {
                int[] x0 = { 0, 4, 0, 2, 0, 1, 0 }, y0 = { 0, 0, 4, 0, 2, 0, 1 }, dx = { 8, 8, 4, 4, 2, 2, 1 }, dy = { 8, 8, 8, 4, 4, 2, 2 };
                for (int p = 0; p < 7; p++)
                {
                    int[] xs = Enumerable.Range(0, width).Where(x => x >= x0[p] && (x - x0[p]) % dx[p] == 0).ToArray();
                    int[] ys = Enumerable.Range(0, height).Where(y => y >= y0[p] && (y - y0[p]) % dy[p] == 0).ToArray();
                    if (xs.Length > 0 && ys.Length > 0)
                    {
                        Image(xs, ys);
                    }
                }
            }

            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            Chunk(png, "IHDR", Be(width).Concat(Be(height)).Concat(new[] { (byte)depth, (byte)colour, (byte)0, (byte)0, (byte)(interlaced ? 1 : 0) }).ToArray());

            if (perMetre > 0)
            {
                Chunk(png, "pHYs", Be(perMetre).Concat(Be(perMetre)).Concat(new byte[] { 1 }).ToArray());
            }

            if (palette != null)
            {
                Chunk(png, "PLTE", palette);
            }

            if (transparency != null)
            {
                Chunk(png, "tRNS", transparency);
            }

            Chunk(png, "IDAT", Zlib(raw.ToArray()));
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        private static byte[] Be(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

        private static void Chunk(Stream s, string type, byte[] data)
        {
            byte[] name = Encoding.ASCII.GetBytes(type);
            s.Write(Be(data.Length), 0, 4);
            s.Write(name, 0, 4);
            s.Write(data, 0, data.Length);
            s.Write(Be((int)Crc(name.Concat(data).ToArray())), 0, 4);
        }

        private static uint Crc(byte[] bytes)
        {
            uint c = 0xFFFFFFFF;
            foreach (byte b in bytes)
            {
                c ^= b;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }
            }

            return c ^ 0xFFFFFFFF;
        }

        private static byte[] Zlib(byte[] raw)
        {
            var out1 = new MemoryStream();
            out1.WriteByte(0x78);
            out1.WriteByte(0x9C);
            using (var deflate = new DeflateStream(out1, CompressionLevel.Optimal, true))
            {
                deflate.Write(raw, 0, raw.Length);
            }

            uint a = 1, b = 0;
            foreach (byte x in raw)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }

            out1.Write(Be((int)((b << 16) | a)), 0, 4);
            return out1.ToArray();
        }

        /// <summary>The blue, green and red of a pixel of the .bmp a frame holds, its rows from the bottom.</summary>
        private static int[] Bgr(byte[] bmp, int x, int y)
        {
            int width = BitConverter.ToInt32(bmp, 18);
            int height = BitConverter.ToInt32(bmp, 22);
            int at = 54 + ((height - 1 - y) * width + x) * 4;
            return new int[] { bmp[at], bmp[at + 1], bmp[at + 2] };
        }

        private static byte[] PictureOf(byte[] png)
        {
            byte[] bmp = new DxfOle2Frame(png).GetPictureData(out DxfOlePictureFormat format);
            Assert.Equal(DxfOlePictureFormat.Dib, format);
            Assert.Equal((byte)32, bmp[28]);
            return bmp;
        }

        [Fact]
        public void APngOfTrueColourAndAlpha_IsLaidOverWhite_ItsSizeFromItsPhys()
        {
            // 3 by 2: red opaque, green half, blue clear; a grey row below. 7 874 pixels a metre is 200 dots an inch.
            int[] Rgba(int x, int y) => y == 0 ? (x == 0 ? new[] { 255, 0, 0, 255 } : x == 1 ? new[] { 0, 255, 0, 128 } : new[] { 0, 0, 255, 0 }) : new[] { 100, 100, 100, 255 };
            byte[] png = Png(3, 2, 6, 8, Rgba, perMetre: 7874);
            var frame = new DxfOle2Frame(png);
            byte[] bmp = frame.GetPictureData(out _);

            Assert.Equal(new[] { 0, 0, 255 }, Bgr(bmp, 0, 0));
            Assert.Equal(new[] { 127, 255, 127 }, Bgr(bmp, 1, 0));
            Assert.Equal(new[] { 255, 255, 255 }, Bgr(bmp, 2, 0));
            Assert.Equal(new[] { 100, 100, 100 }, Bgr(bmp, 1, 1));
            Assert.Equal(3 * 1000.0 / 7874, frame.WcsWidth, 15);
            Assert.Equal(7874, BitConverter.ToInt32(bmp, 38));
        }

        [Fact]
        public void APngOfEveryKind_IsReadToTheSamePixels()
        {
            // A 10 by 9 picture of greys and colours, written as each kind PNG has, interlaced and not.
            int Grey(int x, int y) => (x * 25 + y * 3) % 256;

            foreach (bool interlaced in new[] { false, true })
            {
                byte[] grey8 = PictureOf(Png(10, 9, 0, 8, (x, y) => new[] { Grey(x, y) }, interlaced: interlaced));
                byte[] grey16 = PictureOf(Png(10, 9, 0, 16, (x, y) => new[] { Grey(x, y) * 257 }, interlaced: interlaced));
                byte[] rgb16 = PictureOf(Png(10, 9, 2, 16, (x, y) => new[] { Grey(x, y) * 257, Grey(x, y) * 257, Grey(x, y) * 257 }, interlaced: interlaced));
                byte[] greyAlpha = PictureOf(Png(10, 9, 4, 8, (x, y) => new[] { Grey(x, y), 255 }, interlaced: interlaced));
                Assert.Equal(grey8, grey16);
                Assert.Equal(grey8, rgb16);
                Assert.Equal(grey8, greyAlpha);
                Assert.Equal(new[] { Grey(7, 5), Grey(7, 5), Grey(7, 5) }, Bgr(grey8, 7, 5));

                // One bit a pixel: black and white in a checker; two bits: four greys.
                byte[] bits1 = PictureOf(Png(10, 9, 0, 1, (x, y) => new[] { (x + y) % 2 }, interlaced: interlaced));
                Assert.Equal(new[] { 255, 255, 255 }, Bgr(bits1, 1, 0));
                Assert.Equal(new[] { 0, 0, 0 }, Bgr(bits1, 1, 1));
                byte[] bits2 = PictureOf(Png(10, 9, 0, 2, (x, y) => new[] { x % 4 }, interlaced: interlaced));
                Assert.Equal(new[] { 170, 170, 170 }, Bgr(bits2, 2, 3));

                // A palette of four colours, the last clear by its tRNS.
                byte[] palette = { 255, 0, 0, 0, 255, 0, 0, 0, 255, 9, 9, 9 };
                byte[] indexed = PictureOf(Png(10, 9, 3, 4, (x, y) => new[] { x % 4 }, palette, new byte[] { 255, 255, 255, 0 }, interlaced));
                Assert.Equal(new[] { 0, 0, 255 }, Bgr(indexed, 0, 2));
                Assert.Equal(new[] { 255, 0, 0 }, Bgr(indexed, 2, 2));
                Assert.Equal(new[] { 255, 255, 255 }, Bgr(indexed, 3, 2));

                // True colour keyed clear by its tRNS.
                byte[] keyed = PictureOf(Png(10, 9, 2, 8, (x, y) => x == 0 ? new[] { 10, 20, 30 } : new[] { 1, 2, 3 }, transparency: new byte[] { 0, 10, 0, 20, 0, 30 }, interlaced: interlaced));
                Assert.Equal(new[] { 255, 255, 255 }, Bgr(keyed, 0, 4));
                Assert.Equal(new[] { 3, 2, 1 }, Bgr(keyed, 1, 4));
            }
        }

        [Fact]
        public void APngDamagedOrOfAKindPngHasNot_IsRefusedWithTheReason()
        {
            byte[] png = Png(4, 4, 2, 8, (x, y) => new[] { x, y, 0 });

            byte[] cut = png.Take(png.Length - 30).ToArray();
            Assert.Throws<ArgumentException>(() => new DxfOle2Frame(cut));

            byte[] badDepth = (byte[])png.Clone();
            badDepth[24] = 3;
            Assert.Contains("colour type", Assert.Throws<ArgumentException>(() => new DxfOle2Frame(badDepth)).Message, StringComparison.Ordinal);

            byte[] noPalette = Png(4, 4, 3, 8, (x, y) => new[] { 0 }, new byte[] { 1, 2, 3 });
            noPalette = noPalette.Take(33).Concat(noPalette.Skip(33 + 12 + 3)).ToArray();
            Assert.Contains("palette", Assert.Throws<ArgumentException>(() => new DxfOle2Frame(noPalette)).Message, StringComparison.Ordinal);

            byte[] jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F' };
            Assert.Throws<ArgumentException>(() => new DxfOle2Frame(jpeg.Concat(new byte[60]).ToArray()));
        }

        [Fact]
        public void TheSamePng_GivesTheSameBytes()
        {
            byte[] png = Png(5, 5, 6, 8, (x, y) => new[] { x * 40, y * 40, 7, 200 });
            Assert.Equal(new DxfOle2Frame(png).GetCompoundDocument(), new DxfOle2Frame(png).GetCompoundDocument());
        }
    }
}
