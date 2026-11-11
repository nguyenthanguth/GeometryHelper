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
    /// An OLE2FRAME moved, scaled, sized and copied between spaces as AutoCAD 2021 does it: the corners in the head of its binary,
    /// which AutoCAD reads, written anew with groups 10 and 11; the frame kept upright, as AutoCAD 2021's ROTATE, SCALE and
    /// MIRROR leave it (the maintainer's GUI samples); the size at 100 percent kept in the extended data.
    /// </summary>
    public class DxfOle2FrameMoveTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfOle2Frame Of(DxfDatabase db, string handle) => ByHandle<DxfOle2Frame>(db, handle);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static void Same(GeoPoint3 expected, GeoPoint3 actual) => Assert.Equal(Bits(expected), Bits(actual));

        private static void Corners(DxfOle2Frame frame, GeoPoint3 upperLeft, GeoPoint3 lowerRight)
        {
            DxfRectangle3d at = frame.Position3d;
            Same(upperLeft, at.UpperLeft);
            Same(P(lowerRight.X, upperLeft.Y, upperLeft.Z), at.UpperRight);
            Same(lowerRight, at.LowerRight);
            Same(P(upperLeft.X, lowerRight.Y, lowerRight.Z), at.LowerLeft);
        }

        /// <summary>The 310 lines of one record, in their order.</summary>
        private static List<string> Lines(string text, string handle)
        {
            string record = Records(text).First(r => HandleOf(r) == handle);
            string[] rows = record.Split('\n');
            var lines = new List<string>();

            for (int i = 0; i + 1 < rows.Length; i += 2)
            {
                if (rows[i].Trim() == "310")
                {
                    lines.Add(rows[i + 1]);
                }
            }

            return lines;
        }

        /// <summary>The bytes of a record's binary, as AutoCAD reads them.</summary>
        private static byte[] Binary(string text, string handle) =>
            Lines(text, handle)
                .SelectMany(l =>
                    Enumerable.Range(0, l.Length / 2).Select(i => Convert.ToByte(l.Substring(2 * i, 2), 16))
                )
                .ToArray();

        [Fact]
        public void AFrameMoved_HasTheCornersOfItsHeadMoved_AndOnlyTheLinesTheyAreInWrittenAnew()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            string before = db.ToText();

            Of(db, "D10").TransformBy(GeoTransform3.Translation(new GeoVector3(5, 7, 0)));
            string after = TextOf(Saved(db));
            DxfOle2Frame read = Of(Opened(Saved(db)), "D10");

            // The picture 30 by 20 at (10, 20) is at (15, 27): its head, which AutoCAD reads, and 10 and 11 alike.
            Corners(read, P(15, 47, 0), P(45, 27, 0));
            Same(P(15, 27, 0), read.Location);
            Assert.Equal("15", Body(after, "D10", 10));
            Assert.Equal("27", Body(after, "D10", 21));

            // The head is the first 128 bytes: the lines of 32 bytes past them, and the extended data, are as they were.
            List<string> was = Lines(before, "D10");
            List<string> now = Lines(after, "D10");
            Assert.Equal(was.Count, now.Count);
            Assert.Equal(was.Skip(4), now.Skip(4));
            Assert.NotEqual(was[0], now[0]);
            Assert.Equal(DxfOle2FrameDrawings.Bitmap(), read.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.Dib, format);
            Assert.Equal(100.0, read.ScaleWidth);
        }

        [Fact]
        public void AFrameScaledByAHalf_IsScaledAboutTheBase_ItsSizeAt100PercentKept_AsAutoCad2021ScalesOne()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);

            Of(db, "D10").TransformBy(GeoTransform3.Scaling(P(10, 20, 0), 0.5));
            DxfOle2Frame read = Of(Opened(Saved(db)), "D10");

            // AutoCAD 2021's scale05.dxf: the corners scaled about the base, 41/42 kept, the size of the text (43) halved.
            Corners(read, P(10, 30, 0), P(25, 20, 0));
            Assert.Equal(50.0, read.ScaleWidth, 12);
            Assert.Equal(50.0, read.ScaleHeight, 12);
            Assert.Contains(
                "1070\n43\n1040\n0.0833333333333333",
                Body(TextOf(Saved(db)), "D10"),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AFrameMirroredOrTurnedHalfRound_IsTheBoxOfItsCornersSoMoved_ThePictureNotTurnedOver()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Of(db, "D10");

            // Mirrored about x = 40, as AutoCAD 2021's mirror.dxf: the frame on the other side, upright.
            frame.TransformBy(GeoTransform3.Mirror(new GeoPlane3(P(40, 0, 0), new GeoVector3(1, 0, 0))));
            Corners(frame, P(40, 40, 0), P(70, 20, 0));

            // Half round about its middle: the same box.
            frame.TransformBy(GeoTransform3.RotationAxis(P(55, 30, 0), new GeoVector3(0, 0, 1), Math.PI));
            DxfRectangle3d at = frame.Position3d;
            Assert.True(at.UpperLeft.IsEqualTo(P(40, 40, 0), Tol), at.ToString());
            Assert.True(at.LowerRight.IsEqualTo(P(70, 20, 0), Tol), at.ToString());
        }

        [Fact]
        public void AFrameTurnedSheeredOrStretched_IsRefused_AndLeftAsItWas()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Of(db, "D10");
            byte[] before = Saved(db);

            Assert.Throws<ArgumentException>(() => frame.TransformBy(GeoTransform3.RotationZ(Math.PI / 6)));
            Assert.Throws<ArgumentException>(() => frame.TransformBy(GeoTransform3.RotationZ(Math.PI / 2)));
            Assert.Throws<ArgumentException>(() => frame.TransformBy(GeoTransform3.Scaling(2, 1, 1)));
            Assert.Throws<ArgumentException>(() => frame.TransformBy(GeoTransform3.RotationX(Math.PI / 4)));
            Assert.Throws<ArgumentException>(() => frame.GetTransformedCopy(GeoTransform3.RotationZ(Math.PI / 6)));
            Assert.Equal(before, Saved(db));
        }

        [Fact]
        public void AFrameSized_KeepsItsUpperLeftCorner_AndItsAspectWhereLocked()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Of(db, "D10");

            frame.WcsWidth = 60;
            Corners(frame, P(10, 40, 0), P(70, 0, 0));

            frame.LockAspect = false;
            frame.WcsHeight = 10;
            Corners(frame, P(10, 40, 0), P(70, 30, 0));
            Assert.False(Of(Opened(Saved(db)), "D10").LockAspect);

            // A share of the size kept at 100 percent, 30 by 20: unlocked, the height stays.
            frame.ScaleWidth = 50;
            Corners(frame, P(10, 40, 0), P(25, 30, 0));
            Assert.Equal(50.0, frame.ScaleWidth, 12);
            Assert.Equal(50.0, frame.ScaleHeight, 12);

            frame.LockAspect = true;
            frame.ScaleHeight = 200;
            Corners(frame, P(10, 40, 0), P(70, 0, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() => frame.WcsWidth = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => frame.ScaleWidth = double.NaN);
        }

        [Fact]
        public void AFramePutOnARectangle_TakesItAlongXAndYOnly()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            DxfOle2Frame frame = Of(db, "D10");

            frame.Position3d = new DxfRectangle3d(P(10, 60, 0), P(70, 60, 0), P(10, 20, 0), P(70, 20, 0));
            Corners(Of(Opened(Saved(db)), "D10"), P(10, 60, 0), P(70, 20, 0));

            byte[] before = Saved(db);
            Assert.Throws<ArgumentException>(() =>
                frame.Position3d = new DxfRectangle3d(P(0, 0, 0), P(10, 5, 0), P(-5, -10, 0), P(5, -5, 0))
            );
            Assert.Throws<ArgumentException>(() =>
                frame.Position3d = new DxfRectangle3d(P(70, 60, 0), P(10, 60, 0), P(70, 20, 0), P(10, 20, 0))
            );
            Assert.Equal(before, Saved(db));
        }

        [Fact]
        public void APlotQualityAndAnAspectSet_AreWrittenWhereAutoCadKeepsThem_TheExtendedDataMadeWhereThereIsNone()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);

            Of(db, "D10").OutputQuality = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => Of(db, "D10").OutputQuality = 3);

            // D1A has no extended data: set, it is given the data AutoCAD writes for a frame, its size at 100 percent its own.
            DxfOle2Frame bare = Of(db, "D1A");
            bare.LockAspect = false;
            DxfDatabase read = Opened(Saved(db));
            Assert.Equal(0, Of(read, "D10").OutputQuality);
            Assert.False(Of(read, "D1A").LockAspect);
            Assert.Equal(100.0, Of(read, "D1A").ScaleWidth);
            Assert.Contains(
                "1000\nOLEBEGIN\n1070\n70\n1070\n1\n1070\n71\n1070\n0\n",
                Body(TextOf(Saved(db)), "D1A"),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AFrameCopiedIntoPaperSpaceOrModelSpace_IsGivenTheMarksAutoCad2021WritesForIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);

            // AutoCAD 2021's paper.dxf: 0x5581, a u32 of 0 at 102 and 72 = 1 in paper space; 0x5580, 1 and 0 in model space.
            DxfEntity toPaper = Of(db, "D10").Clone();
            db.PaperSpace.AppendEntity(toPaper);
            DxfEntity toModel = Of(db, "D17").Clone();
            db.ModelSpace.AppendEntity(toModel);
            string text = TextOf(Saved(db));

            byte[] paper = Binary(text, toPaper.Handle);
            byte[] model = Binary(text, toModel.Handle);
            Assert.Equal(new byte[] { 0x81, 0x55 }, paper.Take(2));
            Assert.Equal(0u, BitConverter.ToUInt32(paper, 102));
            Assert.Equal("1", Body(text, toPaper.Handle, 72));
            Assert.Equal(new byte[] { 0x80, 0x55 }, model.Take(2));
            Assert.Equal(1u, BitConverter.ToUInt32(model, 102));
            Assert.Equal("0", Body(text, toModel.Handle, 72));

            // The rest of the binary is the original's.
            Assert.Equal(Binary(text, "D10").Skip(106), paper.Skip(106));
        }

        [Fact]
        public void AnInsertExploded_KeepsAFrameItsPlacingKeepsUpright_AndLeavesOutOneItWouldTurn()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "PICTURE" };
            db.BlockTable.Add(block);
            block.AppendEntity(Of(db, "D10").Clone());
            block.AppendEntity(new DxfLine(P(0, 0, 0), P(10, 0, 0)));

            var moved = new DxfBlockReference(P(100, 100, 0), block);
            db.ModelSpace.AppendEntity(moved);
            DxfOle2Frame placed = Assert.Single(moved.Explode().OfType<DxfOle2Frame>());
            Same(P(110, 120, 0), placed.Location);

            var turned = new DxfBlockReference(P(100, 100, 0), block) { Rotation = Math.PI / 6 };
            db.ModelSpace.AppendEntity(turned);
            Assert.IsType<DxfLine>(Assert.Single(turned.Explode()));
        }

        [Fact]
        public void AFrameOfNoCorners_IsLeftAsItIsByAMove()
        {
            const string bare =
                "  0\nOLE2FRAME\n  5\nD30\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbOle2Frame\n 70\n2\n 73\n2\n";
            DxfDatabase db = DxfDatabase.Parse(DxfOle2FrameDrawings.With(DxfFileEzdxfDrawings.R2018, bare));
            byte[] before = Saved(db);

            Of(db, "D30").TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0)));

            Assert.Equal(before, Saved(db));
        }

        /// <summary>The value of a group of a record's own part, or the whole own part where no code is given.</summary>
        private static string Body(string text, string handle, int code = -1)
        {
            string record = Records(text).First(r => HandleOf(r) == handle);
            string own = record.Substring(record.IndexOf("100\nAcDbOle2Frame\n", StringComparison.Ordinal));

            if (code < 0)
            {
                return own;
            }

            string[] rows = own.Split('\n');

            for (int i = 0; i + 1 < rows.Length; i += 2)
            {
                if (rows[i].Trim() == code.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    return rows[i + 1].Trim();
                }
            }

            return null;
        }
    }
}
