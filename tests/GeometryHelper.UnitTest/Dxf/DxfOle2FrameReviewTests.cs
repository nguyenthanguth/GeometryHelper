using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The review of <see cref="DxfOle2Frame"/>: the sizes of a link's monikers counting their own 4 bytes, a user type left
    /// empty, a frame of no corners as accoreconsole writes one, the defaults of a record that leaves a group out, a chain of
    /// sectors that comes back on itself, the Unicode name MS-OLEDS says to ignore, and a turn stored as NaN.
    /// </summary>
    public class DxfOle2FrameReviewTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static void Same(GeoPoint3 expected, GeoPoint3 actual) => Assert.Equal(Bits(expected), Bits(actual));

        /// <summary>The frame of a handle in the drawing of R2018 with the records given appended to its model space.</summary>
        private static DxfOle2Frame Read(string handle, Action<StringBuilder> records)
        {
            var b = new StringBuilder();
            records(b);
            return ByHandle<DxfOle2Frame>(
                DxfDatabase.Parse(DxfOle2FrameDrawings.With(DxfFileEzdxfDrawings.R2018, b.ToString())),
                handle
            );
        }

        /// <summary>Where the directory entry of a stream of a compound file of version 3 begins.</summary>
        private static int EntryOf(byte[] file, string name)
        {
            int at = ((int)BitConverter.ToUInt32(file, 0x30) + 1) * 512;

            for (int i = 0; i < 4; i++, at += 128)
            {
                if (Encoding.Unicode.GetString(file, at, 2 * name.Length) == name)
                {
                    return at;
                }
            }

            throw new InvalidOperationException("No stream " + name + ".");
        }

        [Fact]
        public void ALinkWhoseRelativeMonikerIsNotRead_GivesItsAbsoluteOne_EachSizeCountingItsOwn4Bytes()
        {
            // The relative moniker begins with an anti moniker, which the library does not read: its size passes it.
            DxfOle2Frame frame = Read("D20", b =>
                DxfOle2FrameDrawings.Frame(
                    b,
                    "D20",
                    false,
                    "Microsoft Excel Worksheet",
                    1,
                    DxfOle2FrameDrawings.Head(
                        DxfOle2FrameDrawings.Square(0, 0, 60, 40),
                        0x80,
                        DxfOle2FrameDrawings.LinkCompound(true)
                    ),
                    true
                )
            );

            Assert.True(frame.IsLinked);
            Assert.Equal("C:\\Drawings\\cost.xlsx!Sheet1!R1C1:R4C3", frame.LinkName);
            Assert.Equal("C:\\Drawings\\cost.xlsx", frame.LinkPath);
        }

        [Fact]
        public void AFilePackedOfAnEmptyUserType_IsNamedByItsGroup3()
        {
            DxfOle2Frame frame = Read("D20", b =>
                DxfOle2FrameDrawings.Frame(
                    b,
                    "D20",
                    false,
                    "OLE Package",
                    2,
                    DxfOle2FrameDrawings.Head(
                        DxfOle2FrameDrawings.Square(0, 0, 40, 30),
                        0x80,
                        DxfOle2FrameDrawings.PackageCompound(string.Empty)
                    ),
                    true
                )
            );

            Assert.Equal(DxfOle2FrameItemType.Embedded, frame.Type);
            Assert.Equal("OLE Package", frame.UserType);
        }

        [Fact]
        public void AFrameOfNoCorners_StandsAtTheOriginWithNoExtents_ExplodesIntoNothing_AndIsPassedOverByUpdateExt()
        {
            var made = new DxfDatabase("AC1024");
            made.ModelSpace.AppendEntity(new DxfLine(P(100, 100, 0), P(200, 150, 0)));
            string owner = made.ModelSpace.Handle;

            // As accoreconsole writes a frame it could not read: 70, 73 and the extended data alone.
            string bare =
                "  0\nOLE2FRAME\n  5\nF00\n330\n"
                + owner
                + "\n100\nAcDbEntity\n  8\n0\n100\nAcDbOle2Frame\n 70\n2\n 73\n2\n"
                + "1001\nACAD\n1000\nOLEBEGIN\n1070\n70\n1070\n1\n1070\n71\n1070\n1\n1070\n40\n1040\n0.0\n1000\nOLEEND\n";
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.With(made.ToText(), bare));
            DxfOle2Frame frame = ByHandle<DxfOle2Frame>(db, "F00");

            Same(P(0, 0, 0), frame.Location);
            Assert.Equal(0.0, frame.WcsWidth);
            Assert.Equal(0.0, frame.ScaleWidth);
            Assert.Equal(0.0, frame.ScaleHeight);
            Assert.Throws<InvalidOperationException>(() => frame.GeometricExtents);
            Assert.Empty(frame.GetGeometry());
            Assert.Empty(frame.Explode());
            Assert.Null(frame.GetCompoundDocument());
            Assert.Equal(string.Empty, frame.UserType);

            // The extents are the line's, not pulled to the origin.
            db.UpdateExt(true);
            Same(P(100, 100, 0), db.Extmin);
            Same(P(200, 150, 0), db.Extmax);
        }

        [Fact]
        public void AFrameWithNoGroup73_IsPlottedAt1_AndOneOfNoWidthIsScaled0()
        {
            DxfOle2Frame frame = Read("D20", b =>
                DxfOle2FrameDrawings.Frame(
                    b,
                    "D20",
                    false,
                    "Picture (Device Independent Bitmap)",
                    3,
                    DxfOle2FrameDrawings.Head(
                        DxfOle2FrameDrawings.Square(5, 5, 0, 20),
                        0x80,
                        DxfOle2FrameDrawings.DibCompound()
                    ),
                    true,
                    null,
                    -1
                )
            );

            Assert.Equal(1, frame.OutputQuality);
            Assert.Equal(0.0, frame.WcsWidth);
            Assert.Equal(0.0, frame.ScaleWidth);
            Assert.Equal(100.0, frame.ScaleHeight);
        }

        [Fact]
        public void AChainThatComesBackToItsSector_IsRefused_InTheMiniStreamAndOutOfIt()
        {
            // CONTENTS of 78 bytes in the mini stream, its first mini sector pointing back at itself.
            byte[] small = DxfOle2FrameDrawings.DibCompound();
            uint first = BitConverter.ToUInt32(small, EntryOf(small, "CONTENTS") + 116);
            int miniFat = ((int)BitConverter.ToUInt32(small, 0x3C) + 1) * 512;
            BitConverter.GetBytes(first).CopyTo(small, miniFat + 4 * (int)first);

            DxfCompoundFile read = DxfCompoundFile.Read(small, 0, small.Length, out _);
            Assert.Null(read.Stream("CONTENTS", out string why));
            Assert.Contains("comes back", why);

            // CONTENTS of 5 000 bytes in sectors of its own, its first sector pointing back at itself.
            var contents = new byte[5000];
            new Random(3).NextBytes(contents);
            byte[] big = DxfOle2FrameDrawings.Compound(
                DxfOle2FrameDrawings.StaticDib,
                9,
                DxfOle2FrameDrawings.Stream("CONTENTS", contents)
            );
            uint start = BitConverter.ToUInt32(big, EntryOf(big, "CONTENTS") + 116);
            Assert.Equal(contents, DxfCompoundFile.Read(big, 0, big.Length, out _).Stream("CONTENTS", out _));
            BitConverter.GetBytes(start).CopyTo(big, 512 + 4 * (int)start);

            Assert.Null(DxfCompoundFile.Read(big, 0, big.Length, out _).Stream("CONTENTS", out why));
            Assert.Contains("comes back", why);
        }

        [Fact]
        public void ACompObjOfAProgIdOfLength0_HasItsUnicodeNameIgnored_AsMsOledsSays()
        {
            foreach (
                var (progId, expected) in new[]
                {
                    ("Thing.1", "Unicode Thing"),
                    ((string)null, "Ansi Thing"),
                    (new string('x', 40), "Ansi Thing"),
                }
            )
            {
                byte[] file = DxfOle2FrameDrawings.Compound(
                    DxfOle2FrameDrawings.Package,
                    9,
                    DxfOle2FrameDrawings.Stream(
                        "\u0001CompObj",
                        DxfOle2FrameDrawings.CompObj("Ansi Thing", 0, progId, "Unicode Thing")
                    )
                );

                Assert.Equal(expected, DxfOleData.UserTypeOf(DxfCompoundFile.Read(file, 0, file.Length, out _)));
            }
        }

        [Fact]
        public void ARotationAnotherWriterStoredAsNaN_IsGivenAsNaN()
        {
            DxfOle2Frame frame = Read("D20", b =>
                DxfOle2FrameDrawings.Frame(
                    b,
                    "D20",
                    false,
                    "Picture (Device Independent Bitmap)",
                    3,
                    DxfOle2FrameDrawings.Head(
                        DxfOle2FrameDrawings.Square(0, 0, 30, 20),
                        0x80,
                        DxfOle2FrameDrawings.DibCompound()
                    ),
                    true,
                    null,
                    2,
                    "1",
                    "1",
                    "NaN",
                    "1"
                )
            );

            Assert.True(double.IsNaN(frame.Rotation));
            Assert.True(frame.LockAspect);
        }
    }
}
