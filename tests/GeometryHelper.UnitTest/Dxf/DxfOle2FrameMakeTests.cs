using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A static picture made in code from a bitmap, as AutoCAD 2021 makes one of a bitmap pasted: the .bmp as the stream
    /// CONTENTS of a compound file of the class StaticDib behind AutoCAD's head, read back by the library as any frame read.
    /// </summary>
    public class DxfOle2FrameMakeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>A .bmp of 32 bits a pixel, BI_RGB, of the size and pixels a metre given, every pixel the same.</summary>
        internal static byte[] Bitmap32(int width, int height, int perMetre)
        {
            int pixels = width * height * 4;
            var b = new List<byte> { 0x42, 0x4D };
            b.AddRange(BitConverter.GetBytes(54 + pixels));
            b.AddRange(BitConverter.GetBytes(0));
            b.AddRange(BitConverter.GetBytes(54));
            b.AddRange(BitConverter.GetBytes(40));
            b.AddRange(BitConverter.GetBytes(width));
            b.AddRange(BitConverter.GetBytes(height));
            b.AddRange(BitConverter.GetBytes((short)1));
            b.AddRange(BitConverter.GetBytes((short)32));
            b.AddRange(BitConverter.GetBytes(0));
            b.AddRange(BitConverter.GetBytes(pixels));
            b.AddRange(BitConverter.GetBytes(perMetre));
            b.AddRange(BitConverter.GetBytes(perMetre));
            b.AddRange(BitConverter.GetBytes(0));
            b.AddRange(BitConverter.GetBytes(0));
            var bytes = new byte[54 + pixels];
            b.CopyTo(bytes);

            for (int i = 54; i < bytes.Length; i += 4)
            {
                bytes[i] = 200;
                bytes[i + 1] = 120;
                bytes[i + 2] = 40;
            }

            return bytes;
        }

        [Fact]
        public void APictureMadeOfABitmap_IsAStaticBitmapAtItsNaturalSize_ReadBackAsAnyFrame()
        {
            byte[] bitmap = DxfOle2FrameDrawings.Bitmap();
            var frame = new DxfOle2Frame(bitmap);

            // 3 by 2 pixels of 3 780 a metre: 0.794 by 0.529 millimetres, its lower left corner at the origin.
            Assert.Equal(P(0, 0, 0), frame.Location);
            Assert.Equal(3 * 1000.0 / 3780, frame.WcsWidth, 15);
            Assert.Equal(2 * 1000.0 / 3780, frame.WcsHeight, 15);
            Assert.Equal(DxfOle2FrameItemType.Static, frame.Type);
            Assert.Equal("Picture (Device Independent Bitmap)", frame.UserType);
            Assert.Equal(bitmap, frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.Dib, format);

            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.ModelSpace.AppendEntity(frame);
            frame.Position3d = new DxfRectangle3d(P(10, 40, 0), P(40, 40, 0), P(10, 20, 0), P(40, 20, 0));
            DxfOle2Frame read = ByHandle<DxfOle2Frame>(Opened(Saved(db)), frame.Handle);

            Assert.Equal(P(10, 20, 0), read.Location);
            Assert.Equal(30.0, read.WcsWidth);
            Assert.Equal(bitmap, read.GetPictureData(out _));
            Assert.Equal(DxfOle2FrameItemType.Static, read.Type);
            Assert.True(read.LockAspect);
            Assert.Equal(2, read.OutputQuality);
            Assert.NotEmpty(read.GetCompoundDocument());
        }

        [Fact]
        public void APictureMade_IsTheSameBytesEachTime_AndHoldsTheHeadAutoCadWritesForAPaste()
        {
            byte[] bitmap = DxfOle2FrameDrawings.Bitmap();
            DxfDatabase a = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDatabase b = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            a.ModelSpace.AppendEntity(new DxfOle2Frame(bitmap));
            b.ModelSpace.AppendEntity(new DxfOle2Frame(bitmap));
            Assert.Equal(Saved(a), Saved(b));

            DxfOle2Frame frame = a.ModelSpace.OfType<DxfOle2Frame>().Single();
            byte[] compound = frame.GetCompoundDocument();
            Assert.Equal(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, compound.Take(8));
            Assert.Contains("1000\nOLEBEGIN\n", a.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void APictureAddedToPaperSpace_IsGivenThePaperMarks()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var frame = new DxfOle2Frame(DxfOle2FrameDrawings.Bitmap());
            db.PaperSpace.AppendEntity(frame);
            string text = TextOf(Saved(db));
            string record = Records(text).First(r => HandleOf(r) == frame.Handle);

            Assert.Contains("\n 72\n1\n", record.Replace("\r", string.Empty), StringComparison.Ordinal);
            Assert.Contains("310\n8155", record, StringComparison.Ordinal);
        }

        [Fact]
        public void APictureLargerThanTheMiniStreamAndTheHeadersFat_IsWrittenInSectorsAndThroughItsDifat()
        {
            // 1 300 by 1 400 pixels of 32 bits: 7.28 MB, past the 109 FAT sectors the header of a compound file counts.
            byte[] bitmap = Bitmap32(1300, 1400, 3780);
            var frame = new DxfOle2Frame(bitmap);

            Assert.Equal(bitmap, frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.Dib, format);

            // 1 300 pixels of 3 780 a metre are 34 391 hundredths of a millimetre, within 16 bits; 3 000 are 79 365, kept to
            // 16 bits as AutoCAD 2021 keeps them (13 829), the frame its true width.
            var wide = new DxfOle2Frame(Bitmap32(3000, 2, 3780));
            Assert.Equal(3000 * 1000.0 / 3780, wide.WcsWidth, 12);
        }

        [Fact]
        public void BytesThatAreNotABitmapAFrameTakes_AreRefusedWithTheReason()
        {
            Assert.Throws<ArgumentNullException>(() => new DxfOle2Frame(null));
            Assert.Contains("BM", Assert.Throws<ArgumentException>(() => new DxfOle2Frame(new byte[60])).Message, StringComparison.Ordinal);

            byte[] compressed = DxfOle2FrameDrawings.Bitmap();
            compressed[30] = 1;
            Assert.Contains("compressed", Assert.Throws<ArgumentException>(() => new DxfOle2Frame(compressed)).Message, StringComparison.Ordinal);

            byte[] cut = DxfOle2FrameDrawings.Bitmap().Take(70).ToArray();
            Assert.Contains("not within", Assert.Throws<ArgumentException>(() => new DxfOle2Frame(cut)).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void APictureMadeAndMoved_KeepsItsPicture()
        {
            var frame = new DxfOle2Frame(DxfOle2FrameDrawings.Bitmap());
            frame.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 50, 0)) * GeoTransform3.Scaling(10));

            Assert.True(frame.Location.IsEqualTo(P(100, 50, 0), Tol));
            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), frame.GetPictureData(out _));
        }
    }
}
