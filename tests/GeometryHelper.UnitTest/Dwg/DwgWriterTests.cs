using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The DWG writer: AutoCAD's own DXF of the test drawings opened as a database, written as a DWG of R2018 and read
    /// back by the reader to the same records, but for the types the writer leaves out; the same bytes every time; the
    /// shapes of a document; and what it refuses.
    /// </summary>
    public class DwgWriterTests
    {
        /// <summary>The types of AutoCAD's new drawing of R2018 that the writer does not write yet.</summary>
        private static readonly string[] NotWritten =
        {
            "ACDBDETAILVIEWSTYLE", "ACDBSECTIONVIEWSTYLE", "MATERIAL", "MLEADERSTYLE", "TABLESTYLE", "VISUALSTYLE", "MULTILEADER",
        };

        private static byte[] Written(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream);
                return stream.ToArray();
            }
        }

        private static DxfDatabase Source(string name) => DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false));

        [Theory]
        [InlineData("curves_AC1032")]
        [InlineData("blocks_AC1032")]
        [InlineData("props_AC1032")]
        [InlineData("vports_AC1032")]
        [InlineData("hatch_AC1032")]
        [InlineData("leaders_AC1032")]
        [InlineData("images_AC1032")]
        [InlineData("mline_AC1032")]
        public void ADrawingWrittenAsDwg_ReadsBackToTheSameRecords_ButForTheTypesNotWritten(string name)
        {
            byte[] dwg = Written(Source(name));

            Assert.Equal("AC1032", DwgContainer.VersionOf(dwg));
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(name), DwgReader.ToDxf(dwg));

            // What differs is what the types left out lead to: the dictionaries that listed them, and the material the header
            // names, which is a MATERIAL.
            Assert.All(result.Differences, difference => Assert.True(
                difference.Contains("$CMATERIAL") || difference.Contains("OBJECTS DICTIONARY"), difference));
            Assert.All(result.Missing.Keys, type => Assert.Contains(type, NotWritten));
            Assert.True(result.Paired > 300, result.Paired + " records paired");
        }

        [Fact]
        public void TheEntitiesOfADrawingWrittenAsDwg_ComeBackWithTheirValues()
        {
            DxfDatabase database = DwgReader.Open(new MemoryStream(Written(Source("curves_AC1032")), false));

            // Three lines, the second from (1.5, -2.25, 3.125); two circles, the first of radius 5 about (10, 20, 0).
            DxfLine[] lines = database.ModelSpace.OfType<DxfLine>().ToArray();
            Assert.Equal(3, lines.Length);
            Assert.Equal(1.5, lines[1].StartPoint.X, 12);
            Assert.Equal(-2.25, lines[1].StartPoint.Y, 12);
            Assert.Equal(3.125, lines[1].StartPoint.Z, 12);
            DxfCircle circle = database.ModelSpace.OfType<DxfCircle>().First();
            Assert.Equal(10.0, circle.Center.X, 12);
            Assert.Equal(20.0, circle.Center.Y, 12);
            Assert.Equal(5.0, circle.Radius, 12);
        }

        [Fact]
        public void TheSameDrawing_IsWrittenInTheSameBytes()
        {
            Assert.Equal(Written(Source("props_AC1032")), Written(Source("props_AC1032")));
        }

        [Fact]
        public void TheShapesOfADocument_AreWrittenAndReadBack()
        {
            DxfDocument document = DxfReader.Read(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false));

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(document, stream);
                stream.Position = 0;
                DxfDocument back = DwgReader.Read(stream);

                Assert.Equal(document.Shapes.Count, back.Shapes.Count);
                Assert.Equal(document.Layers, back.Layers);
            }
        }

        [Fact]
        public void AVersionNotWritten_IsRefusedByName()
        {
            DxfDatabase database = Source("curves_AC1032");

            using (var stream = new MemoryStream())
            {
                var refused = Assert.Throws<NotSupportedException>(() => DwgWriter.Write(database, stream, "AC1015"));
                Assert.Contains("AC1015", refused.Message);
                Assert.Equal(0, stream.Length);
            }
        }

        [Fact]
        public void NothingToWrite_OrNowhereToWriteIt_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => DwgWriter.Write((DxfDatabase)null, new MemoryStream()));
            Assert.Throws<ArgumentNullException>(() => DwgWriter.Write(Source("curves_AC1032"), (Stream)null));
            Assert.Throws<ArgumentNullException>(() => DwgWriter.Write((DxfDocument)null, "x.dwg"));
            Assert.Throws<ArgumentNullException>(() => DwgWriter.Write(Source("curves_AC1032"), "x.dwg", null));
        }

        [Fact]
        public void TheLeftOutTypes_AreCountedInOneWarnLine()
        {
            var lines = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            Action<GeometryHelperLogLevel, string, Exception> writer = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, fault) =>
                {
                    if (level == GeometryHelperLogLevel.Warn && message.StartsWith("DwgWriter", StringComparison.Ordinal))
                    {
                        lines.Add(message);
                    }
                };

                Written(Source("curves_AC1032"));
            }
            finally
            {
                GeometryHelperLog.Writer = writer;
                GeometryHelperLog.Enable = enabled;
            }

            string line = Assert.Single(lines);
            Assert.Contains("VISUALSTYLE 24", line);
            Assert.Contains("MATERIAL 3", line);
        }
    }
}
