using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The public face of the DWG reader: a DWG opened as a drawing database and read as a document, from a path and
    /// from a stream.
    /// </summary>
    public class DwgReaderTests
    {
        private static MemoryStream Drawing(string name) => new MemoryStream(DwgAnswerKey.Dwg(name), false);

        [Fact]
        public void ADwgOpenedAsADatabase_HoldsTheLineAndTheCircleThatWereDrawn()
        {
            DxfDatabase database = DwgReader.Open(Drawing("curves"));

            Assert.Equal("AC1015", database.Version);

            // Three lines: the first from (0, 0, 0) to (100, 50, 0), the second from (1.5, -2.25, 3.125).
            DxfLine[] lines = database.ModelSpace.OfType<DxfLine>().ToArray();
            Assert.Equal(3, lines.Length);
            DxfLine line = lines[0];
            Assert.Equal(0.0, line.StartPoint.X, 9);
            Assert.Equal(0.0, line.StartPoint.Y, 9);
            Assert.Equal(100.0, line.EndPoint.X, 9);
            Assert.Equal(50.0, line.EndPoint.Y, 9);
            Assert.Equal(0.0, line.EndPoint.Z, 9);
            Assert.Equal(1.5, lines[1].StartPoint.X, 9);
            Assert.Equal(-2.25, lines[1].StartPoint.Y, 9);
            Assert.Equal(3.125, lines[1].StartPoint.Z, 9);

            // Two circles: the first of radius 5 about (10, 20, 0), the second of radius 7.25.
            DxfCircle[] circles = database.ModelSpace.OfType<DxfCircle>().ToArray();
            Assert.Equal(2, circles.Length);
            DxfCircle circle = circles[0];
            Assert.Equal(10.0, circle.Center.X, 9);
            Assert.Equal(20.0, circle.Center.Y, 9);
            Assert.Equal(5.0, circle.Radius, 9);
            Assert.Equal(7.25, circles[1].Radius, 9);

            // Two arcs: the first of radius 12.5 about (50, 60, 0).
            DxfArc arc = database.ModelSpace.OfType<DxfArc>().First();
            Assert.Equal(50.0, arc.Center.X, 9);
            Assert.Equal(60.0, arc.Center.Y, 9);
            Assert.Equal(12.5, arc.Radius, 9);
        }

        [Fact]
        public void ATextBeyondTheCodePage_ComesBackLetterForLetter()
        {
            DxfDatabase database = DwgReader.Open(Drawing("text"));

            string[] texts = database.ModelSpace.OfType<DxfDBText>().Select(text => text.TextString).ToArray();

            Assert.Contains("Plain text", texts);
            Assert.Contains("Tiếng Việt café 日本語 Ø ±", texts);
        }

        [Fact]
        public void TheLayers_ComeWithTheirNames_AlsoTheOneBeyondTheCodePage()
        {
            DxfDatabase database = DwgReader.Open(Drawing("props"));

            Assert.True(database.LayerTable.Has("steel"));
            Assert.True(database.LayerTable.Has("Lớp ẩn"));
            Assert.True(database.LayerTable.Has("truecolour"));
            Assert.Contains(database.ModelSpace, entity => entity.Layer == "steel");
        }

        [Fact]
        public void ADwgReadAsADocument_GivesItsShapes_ItsLayers_AndItsVersion()
        {
            DxfDocument document = DwgReader.Read(Drawing("props"));

            Assert.Equal("AC1015", document.Version);
            Assert.NotEmpty(document.Shapes);
            Assert.Contains("steel", document.Layers);
        }

        [Fact]
        public void TheOptions_AreThoseOfTheDxfReader()
        {
            DxfDocument flat = DwgReader.Read(Drawing("curves"), new DxfReadOptions { PlanAs2D = true });
            DxfDocument solid = DwgReader.Read(Drawing("curves"), new DxfReadOptions { PlanAs2D = false });

            Assert.NotEmpty(flat.Shapes);
            Assert.Equal(flat.Shapes.Count + flat.Skipped.Values.Sum(), solid.Shapes.Count + solid.Skipped.Values.Sum());
        }

        [Fact]
        public void AStream_IsReadFromWhereItStands_AndLeftOpen()
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            var stream = new MemoryStream();
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
            stream.Write(file, 0, file.Length);
            stream.Position = 3;

            DxfDatabase database = DwgReader.Open(stream);

            Assert.Empty(database.ModelSpace);
            Assert.True(stream.CanRead);
            Assert.Equal(stream.Length, stream.Position);
        }

        [Fact]
        public void AFileOnDisk_IsReadByItsPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "GeometryHelper-" + Guid.NewGuid().ToString("N") + ".dwg");
            File.WriteAllBytes(path, DwgAnswerKey.Dwg("curves"));

            try
            {
                Assert.Equal(2, DwgReader.Open(path).ModelSpace.OfType<DxfCircle>().Count());
                Assert.NotEmpty(DwgReader.Read(path).Shapes);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ANullPath_OrANullStream_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => DwgReader.Read((string)null));
            Assert.Throws<ArgumentNullException>(() => DwgReader.Read((Stream)null));
            Assert.Throws<ArgumentNullException>(() => DwgReader.Open((string)null));
            Assert.Throws<ArgumentNullException>(() => DwgReader.Open((Stream)null));
        }

        [Fact]
        public void AFileThatIsNotThere_IsAnInputOutputFault()
        {
            Assert.ThrowsAny<IOException>(() => DwgReader.Open(Path.Combine(Path.GetTempPath(), "GeometryHelper-no-such-file.dwg")));
        }
    }
}
