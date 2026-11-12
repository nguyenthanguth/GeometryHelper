using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// SHAPE entities, which a DWG keeps by their number in the file of shapes of their text style: read from AutoCAD's DWG
    /// of four versions to the DXF AutoCAD wrote of it, named from a file of shapes found in the support folders, and
    /// written back by their number.
    /// </summary>
    /// <remarks>
    /// shapes_AC10xx was made by probe GhSat in AutoCAD 2021: TRACK1 (shape 130 of ltypeshp.shx) at the origin with every
    /// default, at (10, 20, 30) of size 2.5, turned 0.3 rad, 0.8 wide, slanted 0.2 rad, 4 thick on the normal
    /// (0, 0.6, 0.8), and at (0, -10, 0) of width 1; BOX (shape 132) on the normal (0, 0, -1); and shape 7 of
    /// ghmissing.shx, a file that is nowhere, which AutoCAD writes by its number (group 70). The tests make their own
    /// ltypeshp.shx of the two names; AutoCAD's file is not used.
    /// </remarks>
    public class DwgShapeTests
    {
        /// <summary>A compiled file of shapes of the shapes given, each a number and a name, its strokes a line one unit long.</summary>
        internal static byte[] ShapeFile(params KeyValuePair<int, string>[] shapes)
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\u001A"));
            List<byte[]> bodies = shapes
                .Select(shape => Encoding.ASCII.GetBytes(shape.Value).Concat(new byte[] { 0, 0x14, 0 }).ToArray())
                .ToList();
            bytes.AddRange(BitConverter.GetBytes((ushort)shapes.Min(shape => shape.Key)));
            bytes.AddRange(BitConverter.GetBytes((ushort)shapes.Max(shape => shape.Key)));
            bytes.AddRange(BitConverter.GetBytes((ushort)shapes.Length));

            for (int i = 0; i < shapes.Length; i++)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)shapes[i].Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)bodies[i].Length));
            }

            foreach (byte[] body in bodies)
            {
                bytes.AddRange(body);
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// A folder of its own holding ltypeshp.shx with TRACK1 as shape 130 and BOX as 132, as AutoCAD's file numbers them,
        /// for the test; deleted after it.
        /// </summary>
        internal static T WithShapes<T>(Func<string, T> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-shapes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(
                    Path.Combine(folder, "ltypeshp.shx"),
                    ShapeFile(new KeyValuePair<int, string>(130, "TRACK1"), new KeyValuePair<int, string>(132, "BOX"))
                );
                return test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>The DXF of a DWG read with the folder of <see cref="WithShapes"/>.</summary>
        internal static byte[] ToDxfWithShapes(byte[] dwg) =>
            WithShapes(folder => DwgReader.ToDxf(dwg, new[] { folder }));

        private static List<DwgAnswerKey.Record> Shapes(byte[] dxf) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Type == "SHAPE").ToList();

        private static string Value(DwgAnswerKey.Record record, int code) =>
            record.Groups.Where(g => g.Key == code).Select(g => g.Value.Trim()).SingleOrDefault();

        private static byte[] Written(string text)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(
                    DxfDatabase.Open(new MemoryStream(Encoding.GetEncoding(1252).GetBytes(text), false)),
                    stream
                );
                return stream.ToArray();
            }
        }

        [Theory]
        [InlineData("shapes_AC1015")]
        [InlineData("shapes_AC1018")]
        [InlineData("shapes_AC1024")]
        [InlineData("shapes_AC1032")]
        public void TheShapesOfEveryVersion_ReadWithTheirFileOfShapesInAFolder_AreTheDxfAutoCadWroteOfThem(
            string drawing
        )
        {
            byte[] dxf = ToDxfWithShapes(DwgAnswerKey.Dwg(drawing));
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(drawing), dxf);

            Assert.Empty(result.Differences);
            Assert.False(result.Missing.ContainsKey("SHAPE"));
            Assert.Equal(
                new[] { "TRACK1", "TRACK1", "TRACK1", "BOX", null },
                Shapes(dxf).Select(shape => Value(shape, 2)).ToArray()
            );
            Assert.Equal("7", Value(Shapes(dxf).Last(), 70));
        }

        [Fact]
        public void AShapeWhoseFileIsInNoFolder_GivesItsNumber_AsAutoCadWritesAShapeWhoseFileItDoesNotFind()
        {
            List<DwgAnswerKey.Record> shapes = Shapes(DwgReader.ToDxf(DwgAnswerKey.Dwg("shapes_AC1032")));

            Assert.Equal(new[] { "130", "130", "130", "132", "7" }, shapes.Select(shape => Value(shape, 70)).ToArray());
            Assert.All(shapes, shape => Assert.Null(Value(shape, 2)));
        }

        [Fact]
        public void ADrawingOpened_WithTheSupportFoldersOfItsOptions_NamesItsShapes()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-shapes-" + Guid.NewGuid().ToString("N") + ".dwg");
            File.WriteAllBytes(path, DwgAnswerKey.Dwg("shapes_AC1018"));

            try
            {
                string text = WithShapes(folder =>
                    DwgReader
                        .Open(path, new DxfReadOptions { SupportFolders = { Path.Combine(folder, "nothing"), folder } })
                        .ToText()
                );
                string none = DwgReader.Open(path, null).ToText();

                Assert.Equal(
                    new[] { "TRACK1", "TRACK1", "TRACK1", "BOX", null },
                    Shapes(Encoding.GetEncoding(1252).GetBytes(text)).Select(shape => Value(shape, 2)).ToArray()
                );
                Assert.All(Shapes(Encoding.GetEncoding(1252).GetBytes(none)), shape => Assert.Null(Value(shape, 2)));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AFileOfShapesThatNamesANumberTwice_NamesTheShapeByTheFirst()
        {
            Dictionary<int, string> names = DxfSupportFiles.ShapeNames(
                ShapeFile(
                    new KeyValuePair<int, string>(130, "TRACK1"),
                    new KeyValuePair<int, string>(130, "TRACK2"),
                    new KeyValuePair<int, string>(132, "BOX")
                )
            );

            Assert.Equal("TRACK1", names[130]);
            Assert.Equal("BOX", names[132]);
            Assert.Null(DxfSupportFiles.ShapeNames(Encoding.ASCII.GetBytes("AutoCAD-86 bigfont 1.0\r\n\u001A")));
        }

        [Theory]
        [InlineData("shapes_AC1015")]
        [InlineData("shapes_AC1032")]
        public void ShapesGivenByTheirNumber_AreWritten_AndReadBackTheSame(string drawing)
        {
            // Read with no file of shapes, each SHAPE gives its number (70), which the writer keeps; the text style it
            // writes is the first of a file of shapes, ltypeshp.shx, so that with the file the four are named again.
            string text = Encoding.GetEncoding(1252).GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg(drawing)));
            byte[] dwg = Written(text);

            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                Encoding.GetEncoding(1252).GetBytes(text),
                DwgReader.ToDxf(dwg)
            );
            Assert.DoesNotContain(result.Differences, difference => difference.Contains(" SHAPE "));
            Assert.False(result.Missing.ContainsKey("SHAPE"));
            Assert.Equal(5, Shapes(DwgReader.ToDxf(dwg)).Count);

            Assert.Equal(
                new[] { "TRACK1", "TRACK1", "TRACK1", "BOX", null },
                Shapes(ToDxfWithShapes(dwg)).Select(shape => Value(shape, 2)).ToArray()
            );
        }

        [Fact]
        public void AShapeGivenByItsNumberAsAName_IsWritten_WithThatNumber()
        {
            // Group 2 of "130", a number where a name goes, is taken as the number; a name the writer cannot look up,
            // TRACK1, leaves its SHAPE out.
            string text = Encoding.GetEncoding(1252).GetString(DwgAnswerKey.Key("shapes_AC1032"));
            int first = text.IndexOf("TRACK1", StringComparison.Ordinal);
            text = text.Substring(0, first) + "130" + text.Substring(first + "TRACK1".Length);

            List<DwgAnswerKey.Record> shapes = Shapes(DwgReader.ToDxf(Written(text)));

            Assert.Equal(new[] { "130", "7" }, shapes.Select(shape => Value(shape, 70)).ToArray());
        }
    }
}
