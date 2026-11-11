using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The container of R2004 and later as the DWG writer makes it: the compression of a page read back byte for byte, and
    /// AutoCAD's own sections of a drawing put into a container of the writer's, which the reader takes apart into the same
    /// DXF as AutoCAD's file.
    /// </summary>
    public class DwgContainerWriterTests
    {
        private static byte[] RoundTrip(byte[] data)
        {
            byte[] packed = DwgLz77.Compress(data, 0, data.Length);
            var back = new byte[data.Length];
            Assert.Equal(data.Length, DwgLz77.Decompress(packed, 0, packed.Length, back));
            return back;
        }

        [Fact]
        public void APageCompressed_DecompressesToItsBytes_WhateverTheyAre()
        {
            var random = new Random(5);

            foreach (int length in new[] { 0, 1, 2, 3, 4, 17, 18, 19, 255, 300, 5000, 0x7400 })
            {
                var noise = new byte[length];
                random.NextBytes(noise);
                Assert.Equal(noise, RoundTrip(noise));

                // Runs of a few values, long and short, near and far: every kind of match the codes hold.
                var runs = new byte[length];

                for (int i = 0; i < length; i++)
                {
                    runs[i] = (byte)(random.Next(100) < 70 ? (i / 37) % 3 : random.Next(4));
                }

                Assert.Equal(runs, RoundTrip(runs));
            }
        }

        [Fact]
        public void APageOfZeros_ShrinksToAFewBytes()
        {
            var zeros = new byte[0x7400];

            byte[] packed = DwgLz77.Compress(zeros, 0, zeros.Length);

            Assert.True(packed.Length < 160, packed.Length + " bytes");
            Assert.Equal(zeros, RoundTrip(zeros));
        }

        [Fact]
        public void AutoCADsSectionsInTheWritersContainer_ReadToTheSameDxf()
        {
            foreach (string name in new[] { "curves_AC1032", "blocks_AC1018", "props_AC1024", "blocks_AC1027" })
            {
                byte[] file = DwgAnswerKey.Dwg(name);
                List<KeyValuePair<string, byte[]>> sections = DwgContainer.SectionsOf(file, out Dictionary<string, int[]> layout);
                var writer = new DwgContainerWriter(Encoding.ASCII.GetString(file, 0, 6), file[0x0B], file[0x13] | (file[0x14] << 8));

                foreach (KeyValuePair<string, byte[]> section in sections)
                {
                    int[] place = layout[section.Key];
                    writer.Add(section.Key, section.Value, place[0], place[1], place[2] == 2);
                }

                byte[] made = writer.ToArray();

                Assert.Equal(DwgReader.ToDxf(file), DwgReader.ToDxf(made));
                Assert.Equal(made, writer.ToArray());
            }
        }
    }
}
