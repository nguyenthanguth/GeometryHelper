using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The OLE2FRAME as a <see cref="DxfOle2Frame"/>: read from the head of its binary as AutoCAD 2021 reads it (where it stands,
    /// its size, its turn and how it is shown), what its compound file says it is (static, embedded, linked; its name, its link,
    /// its picture), its shape, box and lines, and copied with its binary as it is. The OLE objects are built in code by
    /// <see cref="DxfOle2FrameDrawings"/>, apart from the library's reader.
    /// </summary>
    public class DxfOle2FrameTests
    {
        private static readonly string[] Handles =
        {
            "D10",
            "D11",
            "D12",
            "D13",
            "D14",
            "D15",
            "D16",
            "D17",
            "D18",
            "D19",
            "D1A",
        };

        private static DxfOle2Frame Of(DxfDatabase db, string handle) => ByHandle<DxfOle2Frame>(db, handle);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static void Same(GeoPoint3 expected, GeoPoint3 actual) => Assert.Equal(Bits(expected), Bits(actual));

        /// <summary>The groups of a record from its subclass marker AcDbOle2Frame on, its extended data among them.</summary>
        private static string Body(string text, string handle)
        {
            string record = Records(text).First(r => HandleOf(r) == handle);
            return record.Substring(record.IndexOf("100\nAcDbOle2Frame\n", StringComparison.Ordinal));
        }

        [Fact]
        public void AnOle2FrameReadFromAFile_IsOfItsOwnClass_AsAutoCadNamesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);

            foreach (string handle in Handles)
            {
                Assert.IsType<DxfOle2Frame>(ByHandle<DxfEntity>(db, handle));
            }

            Assert.DoesNotContain(Everything(db), e => e is DxfUnknownEntity unknown && unknown.DxfName == "OLE2FRAME");
        }

        [Fact]
        public void EveryFrameRead_IsSavedByteForByte_WhateverIsAskedOfIt()
        {
            SettingWhatItHasChangesNothing<DxfOle2Frame>(DxfOle2FrameDrawings.R2018, frame =>
            {
                _ = frame.Location;
                _ = frame.Position3d;
                _ = frame.WcsWidth + frame.WcsHeight + frame.Rotation + frame.ScaleWidth + frame.ScaleHeight;
                _ = frame.LockAspect;
                _ = frame.OutputQuality + frame.AutoOutputQuality;
                _ = frame.Type;
                _ = frame.UserType + frame.LinkName + frame.LinkPath;
                _ = frame.IsLinked;
                _ = frame.GetCompoundDocument();
                _ = frame.GetPictureData(out _);
                _ = frame.GeometricExtents;
                _ = frame.GetGeometry();
                _ = frame.Explode();
                frame.Layer = frame.Layer;
            });
        }

        [Fact]
        public void ThePictureOf3By2PixelsPasted_StandsWhereItsHeadPutsIt_AsAutoCad2021ReadsIt()
        {
            DxfOle2Frame frame = Of(DxfDatabase.Parse(DxfOle2FrameDrawings.R2018), "D10");

            // Location is the lower left corner, as AutoCAD 2021 gives it, not the upper left of group 10.
            Same(P(10, 20, 0), frame.Location);
            DxfRectangle3d corners = frame.Position3d;
            Same(P(10, 40, 0), corners.UpperLeft);
            Same(P(40, 40, 0), corners.UpperRight);
            Same(P(10, 20, 0), corners.LowerLeft);
            Same(P(40, 20, 0), corners.LowerRight);
            Assert.Equal(30.0, frame.WcsWidth);
            Assert.Equal(20.0, frame.WcsHeight);
            Assert.Equal(0.0, frame.Rotation);
            Assert.Equal(100.0, frame.ScaleWidth);
            Assert.Equal(100.0, frame.ScaleHeight);
            Assert.True(frame.LockAspect);
            Assert.Equal(2, frame.OutputQuality);
            Assert.Equal(1, frame.AutoOutputQuality);
            Assert.Equal(DxfOle2FrameItemType.Static, frame.Type);
            Assert.Equal("Picture (Device Independent Bitmap)", frame.UserType);
            Assert.False(frame.IsLinked);
            Assert.Equal(string.Empty, frame.LinkName);
            Assert.Equal(string.Empty, frame.LinkPath);
            Assert.Equal(DxfOle2FrameDrawings.DibCompound(), frame.GetCompoundDocument());
        }

        [Fact]
        public void ABitmapAMetafileAndAnEnhancedMetafile_GiveTheirPictures_AsFilesOfTheirKinds()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);

            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), Of(db, "D10").GetPictureData(out DxfOlePictureFormat dib));
            Assert.Equal(DxfOlePictureFormat.Dib, dib);
            Assert.Equal(78, DxfOle2FrameDrawings.Bitmap().Length);

            DxfOle2Frame metafile = Of(db, "D11");
            Assert.Equal(
                DxfOle2FrameDrawings.PlaceableMetafile(),
                metafile.GetPictureData(out DxfOlePictureFormat wmf)
            );
            Assert.Equal(DxfOlePictureFormat.Metafile, wmf);
            Assert.Equal("Picture (Metafile)", metafile.UserType);
            Assert.Equal(DxfOle2FrameItemType.Static, metafile.Type);

            DxfOle2Frame enhanced = Of(db, "D12");
            Assert.Equal(DxfOle2FrameDrawings.EnhancedMetafile(), enhanced.GetPictureData(out DxfOlePictureFormat emf));
            Assert.Equal(DxfOlePictureFormat.EnhancedMetafile, emf);
            Assert.Equal("Picture (Enhanced Metafile)", enhanced.UserType);
        }

        [Fact]
        public void AFilePacked_IsEmbedded_NamedByItsCompObj_AndGivesNoPicture()
        {
            DxfOle2Frame frame = Of(DxfDatabase.Parse(DxfOle2FrameDrawings.R2018), "D13");

            Assert.Equal(DxfOle2FrameItemType.Embedded, frame.Type);
            Assert.Equal("OLE Package", frame.UserType);
            Assert.False(frame.IsLinked);
            Assert.Equal(string.Empty, frame.LinkName);
            Assert.Null(frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.None, format);
            Same(P(300, 0, 0), frame.Location);
        }

        [Fact]
        public void ALink_GivesTheFileAndItemOfItsAbsoluteMoniker()
        {
            DxfOle2Frame frame = Of(DxfDatabase.Parse(DxfOle2FrameDrawings.R2018), "D14");

            Assert.Equal(DxfOle2FrameItemType.Link, frame.Type);
            Assert.True(frame.IsLinked);
            Assert.Equal("C:\\Drawings\\cost.xlsx!Sheet1!R1C1:R4C3", frame.LinkName);
            Assert.Equal("C:\\Drawings\\cost.xlsx", frame.LinkPath);
            Assert.Equal("Microsoft Excel Worksheet", frame.UserType);
            Assert.Null(frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.None, format);
        }

        [Fact]
        public void ADamagedCompoundFile_GivesTheKindAndNameItsRecordWrites_NoPicture_AndItsBytesAsWritten()
        {
            DxfOle2Frame frame = Of(DxfDatabase.Parse(DxfOle2FrameDrawings.R2018), "D15");
            byte[] broken = DxfOle2FrameDrawings.DibCompound();
            broken[0] = 0;

            // The head is read as it is: the frame stands where it did.
            Same(P(0, 100, 0), frame.Location);
            Assert.Equal(30.0, frame.WcsWidth);

            // Not the static bitmap the compound file was: the kind and name its record writes, 71 of 2 and 3 of Something Else.
            Assert.Equal(DxfOle2FrameItemType.Embedded, frame.Type);
            Assert.Equal("Something Else", frame.UserType);
            Assert.Null(frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.None, format);
            Assert.Equal(broken, frame.GetCompoundDocument());
        }

        [Fact]
        public void AHeadWhoseCornersAreNotThoseOf10And11_IsReadAsAutoCadReadsIt_AndABinaryOfNoCornersBy10And11()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);

            // AutoCAD 2021 took the head moved 100 along x and 50 along y, and left 10 and 11 unread.
            DxfOle2Frame moved = Of(db, "D16");
            Same(P(110, 70, 0), moved.Location);
            Same(P(110, 70, 0), moved.GeometricExtents.Min);
            Same(P(140, 90, 0), moved.GeometricExtents.Max);

            // A binary of 50 bytes holds no corners: the frame along x and y from 10 to 11, and no compound file.
            DxfOle2Frame shortOne = Of(db, "D18");
            Same(P(0, 200, 0), shortOne.Location);
            Same(P(40, 220, 0), shortOne.Position3d.UpperRight);
            Assert.Equal(40.0, shortOne.WcsWidth);
            Assert.Null(shortOne.GetCompoundDocument());
            Assert.Null(shortOne.GetPictureData(out _));
            Assert.Equal(DxfOle2FrameItemType.Embedded, shortOne.Type);
            Assert.Equal("Something Else", shortOne.UserType);
        }

        [Fact]
        public void AFrameInPaperSpace_IsReadAsOneOfModelSpace()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Assert.Single(db.PaperSpace.OfType<DxfOle2Frame>());

            Assert.Equal("D17", frame.Handle);
            Same(P(5, 5, 0), frame.Location);
            Same(P(35, 25, 0), frame.Position3d.UpperRight);
            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), frame.GetPictureData(out _));
        }

        [Fact]
        public void ATurnedFrameUnlockedOfFixedQuality_IsReadFromItsHeadAndItsExtendedData_AndOneOfNoneByAutoCadsDefaults()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame turned = Of(db, "D19");
            double cos = Math.Cos(Math.PI / 6);
            double sin = Math.Sin(Math.PI / 6);

            // The corners as the head holds them, turned 30 degrees about the lower left (0, 300); the width and height their runs
            // along x and y, as AutoCAD 2021 gives them: 30 cos 30 and 20 cos 30.
            Same(P(0, 300, 0), turned.Location);
            Same(P(-sin * 20, 300 + cos * 20, 0), turned.Position3d.UpperLeft);
            Assert.Equal(25.980762113533157, turned.WcsWidth, 12);
            Assert.Equal(17.320508075688775, turned.WcsHeight, 12);
            Assert.Equal(0.5235987755982988, turned.Rotation);
            Assert.False(turned.LockAspect);
            Assert.Equal(0, turned.AutoOutputQuality);
            Assert.Equal(0, turned.OutputQuality);
            Assert.Equal(-sin * 20, turned.GeometricExtents.Min.X, 12);
            Assert.Equal(300 + cos * 20 + sin * 30, turned.GeometricExtents.Max.Y, 12);

            DxfOle2Frame bare = Of(db, "D1A");
            Assert.Empty(bare.XData);
            Assert.True(bare.LockAspect);
            Assert.Equal(1, bare.AutoOutputQuality);
            Assert.Equal(0.0, bare.Rotation);
        }

        [Fact]
        public void TheShapeBoxAndCrossingsOfAFrame_AreThoseOfItsFourCorners_ThoughTheReaderReadsNone()
        {
            string text = DxfOle2FrameDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfOle2Frame frame = Of(db, "D10");

            var outline = (GeoPolyline3)Assert.Single(frame.GetGeometry());
            Assert.Equal(
                new[] { P(10, 40, 0), P(40, 40, 0), P(40, 20, 0), P(10, 20, 0), P(10, 40, 0) }.Select(Bits),
                outline.Vertices.Select(Bits)
            );
            Same(P(10, 20, 0), frame.GeometricExtents.Min);
            Same(P(40, 40, 0), frame.GeometricExtents.Max);

            // A line across the frame meets its left and right sides.
            var across = new DxfLine(P(0, 30, 0), P(50, 30, 0));
            IReadOnlyList<GeoPoint3> met = frame.IntersectWith(across, DxfIntersect.OnBothOperands);
            Assert.Equal(2, met.Count);
            Assert.Contains(met, p => p.IsEqualTo(P(10, 30, 0), Tolerance.Default));
            Assert.Contains(met, p => p.IsEqualTo(P(40, 30, 0), Tolerance.Default));

            // The reader is left as it was: it reads no OLE2FRAME.
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.DoesNotContain(read.Shapes, s => s.Handle == "D10");
        }

        [Fact]
        public void AFrameExploded_GivesFourLinesAlongItsEdges_AsAutoCad2021Does()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Of(db, "D10");
            frame.Layer = "pictures";

            DxfLine[] lines = frame.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(4, lines.Length);
            Same(P(10, 40, 0), lines[0].StartPoint);
            Same(P(40, 40, 0), lines[0].EndPoint);
            Same(P(10, 20, 0), lines[3].StartPoint);
            Same(P(10, 40, 0), lines[3].EndPoint);
            Assert.All(lines, l => Assert.Equal("pictures", l.Layer));
        }

        [Fact]
        public void AFrameCopiedMoved_IsACopyAtItsNewPlace_TheFrameLeftWhereItWas()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Of(db, "D10");
            byte[] before = Saved(db);

            var copy = Assert.IsType<DxfOle2Frame>(
                frame.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(5, 0, 0)))
            );
            Assert.Equal(before, Saved(db));
            Same(P(15, 20, 0), copy.Location);
            Assert.IsType<DxfOle2Frame>(frame.GetTransformedCopy(GeoTransform3.Identity));

            // In a block, the frame is placed with the rest of what its INSERT at the origin explodes into.
            var block = new DxfBlockTableRecord { Name = "PICTURE" };
            db.BlockTable.Add(block);
            block.AppendEntity(frame.Clone());
            block.AppendEntity(new DxfLine(P(0, 0, 0), P(10, 0, 0)));
            var insert = new DxfBlockReference(P(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            Assert.Equal(2, insert.Explode().Count);
        }

        [Fact]
        public void AFrameCopiedIntoAnotherDrawingOrABlock_CarriesItsBinaryAndExtendedDataAsTheyAre()
        {
            string source = DxfOle2FrameDrawings.R2018;
            DxfDatabase from = DxfDatabase.Parse(source);
            var target = new DxfDatabase("AC1024");
            DxfEntity copy = Of(from, "D10").Clone();
            Same(P(10, 20, 0), ((DxfOle2Frame)copy).Location);
            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), ((DxfOle2Frame)copy).GetPictureData(out _));
            target.ModelSpace.AppendEntity(copy);

            var block = new DxfBlockTableRecord { Name = "LINK" };
            target.BlockTable.Add(block);
            DxfEntity linked = Of(from, "D14").Clone();
            block.AppendEntity(linked);
            target.ModelSpace.AppendEntity(new DxfBlockReference(P(100, 0, 0), block));

            // Its own records again as they were, from the subclass marker on: the binary and extended data byte for byte.
            DxfOle2Frame again = Of(DxfDatabase.Parse(target.ToText()), copy.Handle);
            Assert.Equal(Body(source, "D10"), Body(target.ToText(), copy.Handle));
            Assert.Equal(Body(source, "D14"), Body(target.ToText(), linked.Handle));
            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), again.GetPictureData(out _));
            Same(P(10, 20, 0), again.Location);

            // A copy into its own drawing, alike.
            DxfEntity twin = Of(from, "D10").Clone();
            from.ModelSpace.AppendEntity(twin);
            Assert.Equal(Body(source, "D10"), Body(from.ToText(), twin.Handle));
        }

        [Fact]
        public void AFrameOfACompoundFileOfVersion4_IsRead()
        {
            var b = new StringBuilder();
            DxfOle2FrameDrawings.Frame(
                b,
                "D20",
                false,
                "Picture (Device Independent Bitmap)",
                3,
                DxfOle2FrameDrawings.Head(
                    DxfOle2FrameDrawings.Square(1, 2, 3, 4),
                    0x80,
                    DxfOle2FrameDrawings.DibCompound(12)
                ),
                true
            );
            DxfOle2Frame frame = Of(
                DxfDatabase.Parse(DxfOle2FrameDrawings.With(DxfFileEzdxfDrawings.R2018, b.ToString())),
                "D20"
            );

            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.Dib, format);
            // Its header's sector and four of 4 096 bytes: the FAT, the directory, the mini FAT and the mini stream.
            Assert.Equal(4096 * 5, frame.GetCompoundDocument().Length);
        }

        [Fact]
        public void ACompoundFileOfMoreThan109FatSectors_IsReadThroughItsDifat()
        {
            // 7.5 MB of CONTENTS, 14 649 sectors of 512 bytes: more FAT sectors than the 109 the header lists, the rest listed
            // by a DIFAT sector.
            var contents = new byte[7500000];
            new Random(11).NextBytes(contents);
            byte[] file = DxfOle2FrameDrawings.Compound(
                DxfOle2FrameDrawings.StaticDib,
                9,
                DxfOle2FrameDrawings.Stream("\u0001Ole", DxfOle2FrameDrawings.OleStream()),
                DxfOle2FrameDrawings.Stream("CONTENTS", contents)
            );
            Assert.Equal(1u, BitConverter.ToUInt32(file, 0x48));

            DxfCompoundFile read = DxfCompoundFile.Read(file, 0, file.Length, out string why);

            Assert.Null(why);
            Assert.Equal(DxfOle2FrameDrawings.StaticDib, read.RootClassId);
            Assert.Equal(contents, read.Stream("contents", out _));
            Assert.Equal(DxfOle2FrameDrawings.OleStream(), read.Stream("\u0001Ole", out _));
            Assert.Null(read.Stream("\u0001CompObj", out _));
        }

        [Fact]
        public void ABinaryDamagedAnywhere_IsNeverReadPastItsEnd_NorThrows()
        {
            byte[] dib = DxfOle2FrameDrawings.Head(
                DxfOle2FrameDrawings.Square(0, 0, 30, 20),
                0x80,
                DxfOle2FrameDrawings.DibCompound()
            );
            byte[] link = DxfOle2FrameDrawings.Head(
                DxfOle2FrameDrawings.Square(0, 0, 30, 20),
                0x80,
                DxfOle2FrameDrawings.LinkCompound()
            );
            int read = 0;

            foreach (byte[] binary in new[] { dib, link })
            {
                for (int at = 0; at < binary.Length; at++)
                {
                    foreach (
                        byte value in new byte[] { 0x00, 0xFF, (byte)(binary[at] ^ 0x01), (byte)(binary[at] ^ 0x80) }
                    )
                    {
                        var damaged = (byte[])binary.Clone();
                        damaged[at] = value;
                        read += ReadAll(damaged) ? 1 : 0;
                    }

                    read += ReadAll(binary.Take(at).ToArray()) ? 1 : 0;
                }
            }

            // Most damage leaves a readable compound file; that none throws is the point.
            Assert.True(read > 0);
        }

        /// <summary>Asks a binary all the library reads of it; whether its compound file was read.</summary>
        private static bool ReadAll(byte[] binary)
        {
            DxfOleData.TryCorners(binary, out _);
            DxfOleData.CompoundBytes(binary);
            DxfCompoundFile file = DxfOleData.Compound(binary, out _);

            if (file == null)
            {
                return false;
            }

            DxfOleData.TypeOf(file);
            DxfOleData.UserTypeOf(file);
            DxfOleData.TryLink(file, out _, out _);
            DxfOleData.Picture(file, out _, out _);
            return true;
        }
    }
}
