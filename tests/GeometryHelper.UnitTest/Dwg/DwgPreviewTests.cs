using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The preview image of a DWG, the picture of its file in Explorer and in the Open dialog: kept in the section
    /// THUMBNAILIMAGE of a DXF, written into the DWG as AutoCAD 2021 lays it out, and read back from any version.
    /// </summary>
    /// <remarks>
    /// The layout is that of AutoCAD 2021's own previews (of its sample drawings of AutoCAD 2007): a header of 80 bytes
    /// (kind 1) and the bitmap without its file header (kind 2), each placed in the file. AutoCAD 2021 loaded our file and
    /// gave the same bitmap through <c>Database.ThumbnailBitmap</c> (probe GhD7).
    /// </remarks>
    public class DwgPreviewTests
    {
        /// <summary>A bitmap of Windows of 24 bits and 8 by 4 pixels, as a THUMBNAILIMAGE keeps it: 40 bytes of header and the rows.</summary>
        private static byte[] Bitmap()
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(40));
            bytes.AddRange(BitConverter.GetBytes(8));
            bytes.AddRange(BitConverter.GetBytes(4));
            bytes.AddRange(BitConverter.GetBytes((short)1));
            bytes.AddRange(BitConverter.GetBytes((short)24));
            bytes.AddRange(BitConverter.GetBytes(0));
            bytes.AddRange(BitConverter.GetBytes(96));
            bytes.AddRange(new byte[16]);

            for (int i = 0; i < 96; i++)
            {
                bytes.Add((byte)((i * 7) + 3));
            }

            return bytes.ToArray();
        }

        private static string Hex(byte[] bytes) =>
            string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

        /// <summary>The DXF of a drawing of curves with a THUMBNAILIMAGE of <paramref name="image"/> added before its end.</summary>
        private static string WithThumbnail(byte[] image)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false)).ToText();

            if (image == null)
            {
                return text;
            }

            int eof = text.LastIndexOf("EOF", StringComparison.Ordinal);
            int at = text.LastIndexOf("  0", eof, StringComparison.Ordinal);
            var section = new StringBuilder();
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            section.Append("  0").Append(newline).Append("SECTION").Append(newline);
            section.Append("  2").Append(newline).Append("THUMBNAILIMAGE").Append(newline);
            section
                .Append(" 90")
                .Append(newline)
                .Append(image.Length.ToString(CultureInfo.InvariantCulture))
                .Append(newline);

            for (int i = 0; i < image.Length; i += 127)
            {
                section.Append("310").Append(newline).Append(Hex(image.Skip(i).Take(127).ToArray())).Append(newline);
            }

            section.Append("  0").Append(newline).Append("ENDSEC").Append(newline);
            return text.Insert(at, section.ToString());
        }

        private static byte[] Written(string text) => DwgWriter.ToDwg(text, "AC1032");

        [Fact]
        public void ABitmapInTheThumbnailImageOfTheDxf_IsTheDwgsPreview_AndReadBackFromIt()
        {
            byte[] image = Bitmap();
            byte[] dwg = Written(WithThumbnail(image));

            Assert.Equal(image, DwgPreview.Image(DwgContainer.Read(dwg).Preview));
            DxfDatabase back = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(dwg), false));
            Assert.Equal(image, back.ThumbnailBitmap);
        }

        [Fact]
        public void APng_IsKeptAsItIs_AsAPreviewOfItsOwnKind()
        {
            byte[] png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
                .Concat(Bitmap())
                .ToArray();
            byte[] dwg = Written(WithThumbnail(png));

            Assert.Equal(png, DwgPreview.Image(DwgContainer.Read(dwg).Preview));
            Assert.Equal(png, DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(dwg), false)).ThumbnailBitmap);
        }

        [Fact]
        public void ADrawingWithNoThumbnail_HasNoPictureInItsPreview_AndNoThumbnailImageComesBack()
        {
            byte[] dwg = Written(WithThumbnail(null));

            Assert.Null(DwgPreview.Image(DwgContainer.Read(dwg).Preview));
            Assert.Null(DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(dwg), false)).ThumbnailBitmap);
        }

        [Fact]
        public void TheDrawingWithAThumbnail_IsTheSameAsTheOneWithoutBut_ForThePreview()
        {
            // Nothing but the preview and the places that follow it differ: the same objects, so the same reading.
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                DwgReader.ToDxf(Written(WithThumbnail(null))),
                DwgReader.ToDxf(Written(WithThumbnail(Bitmap())))
            );

            Assert.Empty(result.Differences);
        }

        [Fact]
        public void ThePreviewOfAnyOfAutoCadsDrawings_WhichHoldsNoPicture_GivesNone()
        {
            foreach (string name in new[] { "curves", "curves_AC1018", "curves_AC1021", "curves_AC1032" })
            {
                Assert.Null(DwgPreview.Image(DwgContainer.Read(DwgAnswerKey.Dwg(name)).Preview));
            }
        }

        [Fact]
        public void APreviewOfTheLayoutOfAutoCad2007_IsReadByItsPlaceInItsFile()
        {
            // As the sample drawings of AutoCAD 2007 lay one: a count of 2, the header at 0x0D47 and the bitmap at 0x0D97 of a
            // file whose preview data begins at 0x0D20, the images following the list at 39.
            byte[] image = Bitmap();
            long origin = 0x0D20;
            var block = new List<byte>(
                new byte[]
                {
                    0x1F,
                    0x25,
                    0x6D,
                    0x07,
                    0xD4,
                    0x36,
                    0x28,
                    0x28,
                    0x9D,
                    0x57,
                    0xCA,
                    0x3F,
                    0x9D,
                    0x44,
                    0x10,
                    0x2B,
                }
            );
            block.AddRange(BitConverter.GetBytes(1 + 18 + 80 + image.Length));
            block.Add(2);
            block.Add(1);
            block.AddRange(BitConverter.GetBytes((int)(origin + 39)));
            block.AddRange(BitConverter.GetBytes(80));
            block.Add(2);
            block.AddRange(BitConverter.GetBytes((int)(origin + 119)));
            block.AddRange(BitConverter.GetBytes(image.Length));
            block.AddRange(new byte[80]);
            block.AddRange(image);
            block.AddRange(
                new byte[]
                {
                    0xE0,
                    0xDA,
                    0x92,
                    0xF8,
                    0x2B,
                    0xC9,
                    0xD7,
                    0xD7,
                    0x62,
                    0xA8,
                    0x35,
                    0xC0,
                    0x62,
                    0xBB,
                    0xEF,
                    0xD4,
                }
            );

            Assert.Equal(image, DwgPreview.Image(block.ToArray()));
        }

        [Fact]
        public void ABrokenPreview_IsNoReasonToRefuseTheFile()
        {
            byte[] dwg = Written(WithThumbnail(Bitmap()));
            DwgContainer container = DwgContainer.Read(dwg);

            // The section holds its sentinel and counts; cut short by one byte, a preview gives no picture.
            byte[] cut = container.Preview.Take(container.Preview.Length - 1).ToArray();
            Assert.Null(DwgPreview.Image(cut));
            Assert.Null(DwgPreview.Image(null));
            Assert.Null(DwgPreview.Image(new byte[10]));
        }
    }
}
