using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The presenter's TIFF decoders against Windows' own encoder: a picture saved by <see cref="TiffBitmapEncoder"/> with LZW,
    /// Deflate, PackBits, or the fax codings (modified Huffman, Group 3 and Group 4) is read back by
    /// <see cref="CadRasterReader"/> as the picture that was saved, pixel for pixel. Windows writes the data, so a code
    /// table or a table of the presenter's that were wrong would not be mirrored by the test.
    /// </summary>
    public sealed class RasterTiffCodecTests
    {
        private static byte[] Save(BitmapSource picture, TiffCompressOption compression)
        {
            var encoder = new TiffBitmapEncoder { Compression = compression };
            encoder.Frames.Add(BitmapFrame.Create(picture));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>The compression tag (259) of the first directory of a TIFF written by Windows, little-endian.</summary>
        private static int CompressionOf(byte[] file)
        {
            int ifd = BitConverter.ToInt32(file, 4);
            int count = BitConverter.ToUInt16(file, ifd);

            for (int i = 0; i < count; i++)
            {
                int at = ifd + 2 + 12 * i;

                if (BitConverter.ToUInt16(file, at) == 259)
                {
                    return BitConverter.ToUInt16(file, at + 8);
                }
            }

            return 0;
        }

        private static CadRaster Read(byte[] file)
        {
            Assert.True(CadRasterReader.TryRead(file, "scan.tif", true, out CadRaster raster));
            Assert.Equal(1, raster.Reduction);
            return raster;
        }

        /// <summary>
        /// A bilevel picture of rows of black and white runs, a row made from the one above it by moving each change a few
        /// pixels either way, taking some out and putting some in, so that the vertical, pass and horizontal codes of Group 4
        /// are all met; and rows of every run length from 0 to 63 and every multiple of 64 to 2 560, so that every code of
        /// both tables is written. Black is true.
        /// </summary>
        private static bool[][] Runs(int width)
        {
            var random = new Random(20261117);
            var rows = new List<bool[]>();
            var changes = new List<int> { 10, 40, 41, 300, 301, 500, width - 20 };

            for (int y = 0; y < 90; y++)
            {
                var next = new List<int>();

                foreach (int c in changes)
                {
                    int roll = random.Next(10);
                    int moved =
                        c
                        + (
                            roll < 6 ? random.Next(-3, 4)
                            : roll < 8 ? random.Next(-40, 41)
                            : 0
                        );

                    if (roll == 9)
                    {
                        continue;
                    }

                    next.Add(Math.Max(0, Math.Min(width - 1, moved)));

                    if (roll == 8)
                    {
                        next.Add(Math.Max(0, Math.Min(width - 1, moved + random.Next(1, 90))));
                    }
                }

                next.Sort();
                changes = next;
                rows.Add(Paint(width, changes));
            }

            for (int l = 0; l <= 63; l++)
            {
                rows.Add(Paint(width, new List<int> { l, l + 64 - l / 2 }));
            }

            for (int k = 1; k <= 40; k++)
            {
                int white = 64 * k + k % 64;
                int black = 64 * k + (k * 7) % 64;

                if (white + black < width)
                {
                    rows.Add(Paint(width, new List<int> { white, white + black }));
                    rows.Add(Paint(width, new List<int> { 0, black, black + white }));
                }
            }

            rows.Add(new bool[width]);
            rows.Add(Enumerable.Repeat(true, width).ToArray());
            return rows.ToArray();
        }

        private static bool[] Paint(int width, List<int> changes)
        {
            var row = new bool[width];

            for (int i = 0; i < changes.Count; i += 2)
            {
                int end = i + 1 < changes.Count ? changes[i + 1] : width;

                for (int x = changes[i]; x < end && x < width; x++)
                {
                    row[x] = true;
                }
            }

            return row;
        }

        private static BitmapSource BlackAndWhite(bool[][] rows, int width)
        {
            int stride = (width + 7) / 8;
            var bits = new byte[stride * rows.Length];

            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // BlackWhite: a clear bit is black, a set one white.
                    if (!rows[y][x])
                    {
                        bits[y * stride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                    }
                }
            }

            return BitmapSource.Create(width, rows.Length, 96, 96, PixelFormats.BlackWhite, null, bits, stride);
        }

        [Theory]
        [InlineData(TiffCompressOption.Ccitt4)]
        [InlineData(TiffCompressOption.Ccitt3)]
        [InlineData(TiffCompressOption.Rle)]
        [InlineData(TiffCompressOption.Lzw)]
        [InlineData(TiffCompressOption.Zip)]
        [InlineData(TiffCompressOption.None)]
        public void ABilevelPictureSavedByWindows_IsReadBackBlackForBlack_WhateverItsCoding(
            TiffCompressOption compression
        )
        {
            const int width = 2700;
            bool[][] rows = Runs(width);
            byte[] file = Save(BlackAndWhite(rows, width), compression);

            // The coding Windows says it wrote: the test would prove nothing of a picture it left plain.
            int coding = CompressionOf(file);
            int expectedCoding =
                compression == TiffCompressOption.Ccitt4 ? 4
                : compression == TiffCompressOption.Ccitt3 ? 3
                : compression == TiffCompressOption.Lzw ? 5
                : compression == TiffCompressOption.None ? 1
                : 0;

            if (expectedCoding > 0)
            {
                Assert.Equal(expectedCoding, coding);
            }
            else
            {
                // Zip is Deflate, written as 8 or 32946; Rle is PackBits or the modified Huffman runs.
                Assert.Contains(coding, new[] { 8, 32946, 2, 32773 });
            }

            CadRaster raster = Read(file);
            Assert.Equal(width, raster.Width);
            Assert.Equal(rows.Length, raster.Height);
            Assert.True(raster.IsBitonal);
            int wrong = 0;

            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool black = raster.Rgba[(y * width + x) * 4] == 0;

                    if (black != rows[y][x])
                    {
                        wrong++;
                    }
                }
            }

            Assert.Equal(0, wrong);
        }

        private static byte[] Noise(int width, int height, int channels)
        {
            var random = new Random(7);
            var bytes = new byte[width * height * channels];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        // A smooth ramp with a little noise: it packs, and it makes the LZW table fill and clear again.
                        bytes[(y * width + x) * channels + c] = (byte)((x / 3 + y * 2 + c * 40 + random.Next(4)) & 255);
                    }
                }
            }

            return bytes;
        }

        [Theory]
        [InlineData(TiffCompressOption.Lzw)]
        [InlineData(TiffCompressOption.Zip)]
        [InlineData(TiffCompressOption.Rle)]
        [InlineData(TiffCompressOption.None)]
        public void ATrueColourPictureSavedByWindows_IsReadBackPixelForPixel(TiffCompressOption compression)
        {
            const int width = 260;
            const int height = 190;
            byte[] bgr = Noise(width, height, 3);
            BitmapSource picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, bgr, width * 3);
            CadRaster raster = Read(Save(picture, compression));
            Assert.False(raster.IsBitonal);

            for (int i = 0; i < width * height; i++)
            {
                Assert.Equal(bgr[i * 3 + 2], raster.Rgba[i * 4]);
                Assert.Equal(bgr[i * 3 + 1], raster.Rgba[i * 4 + 1]);
                Assert.Equal(bgr[i * 3], raster.Rgba[i * 4 + 2]);
                Assert.Equal(255, raster.Rgba[i * 4 + 3]);
            }
        }

        [Theory]
        [InlineData(TiffCompressOption.Lzw)]
        [InlineData(TiffCompressOption.Zip)]
        public void AGreyPicture_AndAPalettePicture_SavedByWindows_AreReadBackPixelForPixel(
            TiffCompressOption compression
        )
        {
            const int width = 250;
            const int height = 120;
            byte[] grey = Noise(width, height, 1);
            CadRaster levels = Read(
                Save(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, grey, width), compression)
            );

            for (int i = 0; i < width * height; i++)
            {
                Assert.Equal(grey[i], levels.Rgba[i * 4]);
                Assert.Equal(grey[i], levels.Rgba[i * 4 + 2]);
            }

            var colours = Enumerable
                .Range(0, 256)
                .Select(i => Color.FromRgb((byte)i, (byte)(255 - i), (byte)(i * 7)))
                .ToList();
            byte[] indexes = Noise(width, height, 1);
            CadRaster indexed = Read(
                Save(
                    BitmapSource.Create(
                        width,
                        height,
                        96,
                        96,
                        PixelFormats.Indexed8,
                        new BitmapPalette(colours),
                        indexes,
                        width
                    ),
                    compression
                )
            );

            for (int i = 0; i < width * height; i++)
            {
                Color c = colours[indexes[i]];
                Assert.Equal(c.R, indexed.Rgba[i * 4]);
                Assert.Equal(c.G, indexed.Rgba[i * 4 + 1]);
                Assert.Equal(c.B, indexed.Rgba[i * 4 + 2]);
            }
        }

        [Fact]
        public void ThePictureCacheReadsAGifByThePlatform_AndATiffByTheOwnDecoder_BothAtTheirSize()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gk", "cache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                byte[] bgr = Noise(40, 30, 3);
                BitmapSource picture = BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgr24, null, bgr, 120);
                var gif = new GifBitmapEncoder();
                gif.Frames.Add(BitmapFrame.Create(picture));
                string gifPath = Path.Combine(folder, "a.gif");

                using (FileStream stream = File.Create(gifPath))
                {
                    gif.Save(stream);
                }

                string tifPath = Path.Combine(folder, "a.tif");
                File.WriteAllBytes(tifPath, Save(picture, TiffCompressOption.Lzw));

                SkiaSharp.SKImage fromGif = CadImageCache.Get(gifPath, 1, true);
                SkiaSharp.SKImage fromTiff = CadImageCache.Get(tifPath, 1, true);

                Assert.NotNull(fromGif);
                Assert.Equal(40, fromGif.Width);
                Assert.Equal(30, fromGif.Height);
                Assert.NotNull(fromTiff);
                Assert.Equal(40, fromTiff.Width);
                Assert.Equal(30, fromTiff.Height);

                // A file that is no picture is not drawn, and does not stop the others.
                string junk = Path.Combine(folder, "junk.tif");
                File.WriteAllBytes(junk, new byte[200]);
                Assert.Null(CadImageCache.Get(junk, 1, true));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void TheResolutionWindowsWrites_IsReadAsMillimetresAPixel()
        {
            const int width = 40;
            byte[] grey = Noise(width, 30, 1);
            BitmapSource picture = BitmapSource.Create(width, 30, 300, 300, PixelFormats.Gray8, null, grey, width);
            CadRaster raster = Read(Save(picture, TiffCompressOption.None));
            Assert.Equal(25.4 / 300, raster.MillimetresX, 6);
            Assert.Equal(25.4 / 300, raster.MillimetresY, 6);
        }
    }
}
