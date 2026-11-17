using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The OLE2FRAME of a DWG, which the reader once left out with the types not read yet: a drawing whose pictures were
    /// pasted opened in the presenter as a drawing with no pictures. The tests write frames made in code (a static bitmap, as
    /// AutoCAD 2021 makes one of a bitmap pasted) as DWG of every version the writer knows and read them back: the binary
    /// with its compound file and its head, the corners, the kind, the plot quality and the space.
    /// </summary>
    public class DwgOle2FrameTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfDatabase Reopened(DxfDatabase database, string version)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream, version);
                return DwgReader.Open(new MemoryStream(stream.ToArray(), false));
            }
        }

        private static DxfOle2Frame Placed(DxfBlockTableRecord space, byte[] bitmap)
        {
            var frame = new DxfOle2Frame(bitmap);
            space.AppendEntity(frame);
            frame.Position3d = new DxfRectangle3d(
                new GeoPoint3(10, 40, 0),
                new GeoPoint3(40, 40, 0),
                new GeoPoint3(10, 20, 0),
                new GeoPoint3(40, 20, 0)
            );
            return frame;
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void AStaticPictureInModelSpace_WrittenAsDwg_IsReadBackWithItsPictureAndItsPlace(string version)
        {
            byte[] bitmap = DxfOle2FrameDrawings.Bitmap();
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Placed(source.ModelSpace, bitmap);

            DxfDatabase back = Reopened(source, version);

            DxfOle2Frame frame = Assert.Single(back.ModelSpace.OfType<DxfOle2Frame>());
            Assert.Equal(DxfOle2FrameItemType.Static, frame.Type);
            Assert.Equal("Picture (Device Independent Bitmap)", frame.UserType);
            Assert.Equal(bitmap, frame.GetPictureData(out DxfOlePictureFormat format));
            Assert.Equal(DxfOlePictureFormat.Dib, format);
            Assert.True(frame.Location.IsEqualTo(new GeoPoint3(10, 20, 0), Tol), frame.Location.ToString());
            Assert.Equal(30.0, frame.WcsWidth, 9);
            Assert.Equal(20.0, frame.WcsHeight, 9);
            Assert.Equal(2, frame.OutputQuality);
        }

        [Fact]
        public void AFrameInPaperSpace_WrittenAsDwg_StaysInPaperSpace()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfOle2Frame made = Placed(source.PaperSpace, DxfOle2FrameDrawings.Bitmap());

            DxfDatabase back = Reopened(source, "AC1032");

            Assert.Empty(back.ModelSpace.OfType<DxfOle2Frame>());
            DxfOle2Frame frame = Assert.Single(back.PaperSpace.OfType<DxfOle2Frame>());
            Assert.True(frame.Location.IsEqualTo(made.Location, Tol), frame.Location.ToString());
        }

        [Fact]
        public void ThreeFramesInADrawing_AreAllReadBack_InTheirOrderWithTheirOwnPictures()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            byte[] small = DxfOle2FrameMakeTests.Bitmap32(3, 2, 3780);
            byte[] large = DxfOle2FrameMakeTests.Bitmap32(300, 200, 3780);
            source.ModelSpace.AppendEntity(new DxfOle2Frame(small));
            source.ModelSpace.AppendEntity(new DxfOle2Frame(large));
            source.ModelSpace.AppendEntity(new DxfOle2Frame(small));

            DxfOle2Frame[] frames = Reopened(source, "AC1032").ModelSpace.OfType<DxfOle2Frame>().ToArray();

            Assert.Equal(3, frames.Length);
            Assert.Equal(small, frames[0].GetPictureData(out _));
            Assert.Equal(large, frames[1].GetPictureData(out _));
            Assert.Equal(small, frames[2].GetPictureData(out _));
        }

        [Fact]
        public void TheExtendedDataOfAFrame_SurvivesTheDwg()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfOle2Frame made = Placed(source.ModelSpace, DxfOle2FrameDrawings.Bitmap());
            made.LockAspect = false;

            DxfOle2Frame frame = Assert.Single(Reopened(source, "AC1032").ModelSpace.OfType<DxfOle2Frame>());

            Assert.False(frame.LockAspect);
            Assert.Equal(made.ScaleWidth, frame.ScaleWidth, 9);
        }
    }
}
